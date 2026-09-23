using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Channels;
using MiniWar.Online;

namespace MiniWar.Server;

public sealed class LanHost : IAsyncDisposable
{
    sealed class Peer(TcpClient socket)
    {
        public readonly string Id = Guid.NewGuid().ToString("N");
        public readonly TcpClient Socket = socket;
        public readonly Channel<LanEvent> Outbox = Channel.CreateBounded<LanEvent>(64);
        public void Send(LanEvent value) { if (!Outbox.Writer.TryWrite(value)) Socket.Dispose(); }
    }

    readonly TcpListener listener;
    readonly X509Certificate2 certificate;
    readonly AccountStore accounts;
    readonly LobbyWorld world = new();
    readonly object gate = new();
    readonly ConcurrentDictionary<string, Peer> peers = new();
    readonly ConcurrentDictionary<string, Task> sessions = new();
    readonly SemaphoreSlim authentication = new(2);
    readonly Dictionary<string, Queue<DateTime>> attempts = new();
    readonly CancellationTokenSource stopping = new();
    Task? accepting, ticking;
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    public string Fingerprint => Convert.ToHexString(SHA256.HashData(certificate.RawData));

    public LanHost(string dataDirectory, int port = LanRules.Port, IPAddress? bindAddress = null)
    {
        accounts = new AccountStore(dataDirectory);
        listener = new TcpListener(bindAddress ?? IPAddress.Any, port);
        string certPath = Path.Combine(dataDirectory, "server.pfx");
        if (File.Exists(certPath)) certificate = X509CertificateLoader.LoadPkcs12FromFile(certPath, null);
        else
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=MiniWar LAN", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
            File.WriteAllBytes(certPath, generated.Export(X509ContentType.Pfx));
            // SChannel requires a usable key container; reload the PFX just as on subsequent launches.
            certificate = X509CertificateLoader.LoadPkcs12FromFile(certPath, null);
        }
        File.WriteAllText(Path.Combine(dataDirectory, "server-fingerprint.txt"), Fingerprint);
    }

    public void Start()
    {
        listener.Start(64);
        accepting = AcceptLoop();
        ticking = TickLoop();
    }

    async Task AcceptLoop()
    {
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                var socket = await listener.AcceptTcpClientAsync(stopping.Token);
                if (peers.Count >= 72) { socket.Dispose(); continue; }
                socket.NoDelay = true;
                var peer = new Peer(socket);
                peers.TryAdd(peer.Id, peer);
                var task = Serve(peer);
                sessions[peer.Id] = task;
                _ = task.ContinueWith(_ => sessions.TryRemove(peer.Id, out var ignored), TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) when (stopping.IsCancellationRequested) { }
    }

    bool AllowAuthentication(string address)
    {
        lock (attempts)
        {
            var now = DateTime.UtcNow;
            foreach (var key in attempts.Where(kv => kv.Value.Count == 0 || now - kv.Value.Last() > TimeSpan.FromMinutes(1)).Select(kv => kv.Key).ToArray()) attempts.Remove(key);
            if (!attempts.TryGetValue(address, out var queue))
            {
                if (attempts.Count >= 256) return false;
                attempts[address] = queue = new Queue<DateTime>();
            }
            while (queue.Count > 0 && now - queue.Peek() > TimeSpan.FromMinutes(1)) queue.Dequeue();
            if (queue.Count >= 60) return false;
            queue.Enqueue(now);
            return true;
        }
    }

    async Task Serve(Peer peer)
    {
        using var life = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
        Task? writer = null;
        try
        {
            using var stream = new SslStream(peer.Socket.GetStream(), false);
            using (var handshake = CancellationTokenSource.CreateLinkedTokenSource(life.Token))
            {
                handshake.CancelAfter(TimeSpan.FromSeconds(10));
                await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = certificate, EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    ClientCertificateRequired = false
                }, handshake.Token);
            }
            writer = SendLoop(peer, stream, life.Token);
            bool loggedIn = false;
            var window = DateTime.UtcNow;
            int packets = 0, loginAttempts = 0;
            while (!life.IsCancellationRequested)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(life.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(loggedIn ? 15 : 30));
                byte[] bytes = await ReadFrame(stream, timeout.Token, 8192);
                if ((DateTime.UtcNow - window).TotalSeconds >= 1) { window = DateTime.UtcNow; packets = 0; }
                if (++packets > 80) break;
                var cmd = JsonSerializer.Deserialize<LanCommand>(bytes, AccountStore.Json);
                if (cmd == null || cmd.version != LanRules.Version) { peer.Send(Error("클라이언트와 서버 버전이 다릅니다.")); break; }
                if (!loggedIn)
                {
                    if (cmd.op != "login" && cmd.op != "register") { peer.Send(Error("먼저 로그인하세요.")); continue; }
                    string address = ((IPEndPoint)peer.Socket.Client.RemoteEndPoint!).Address.ToString();
                    if (++loginAttempts > 5 || !AllowAuthentication(address)) { peer.Send(Error("로그인 시도가 많습니다. 잠시 후 다시 시도하세요.")); break; }
                    if (!await authentication.WaitAsync(TimeSpan.FromSeconds(5), life.Token)) { peer.Send(Error("로그인 처리 중입니다. 잠시 후 다시 시도하세요.")); continue; }
                    (LanProfile? Profile, string Error) auth;
                    try { auth = await Task.Run(() => accounts.Authenticate(cmd.nickname, cmd.password, cmd.op == "register", cmd.body), life.Token); }
                    finally { cmd.password = ""; authentication.Release(); }
                    if (auth.Profile == null) { peer.Send(Error(auth.Error)); continue; }
                    lock (gate)
                    {
                        var join = world.Join(peer.Id, auth.Profile);
                        if (join.Player == null) peer.Send(Error(join.Error));
                        else
                        {
                            loggedIn = true;
                            peer.Send(new LanEvent { op = "welcome", profile = auth.Profile, channel = join.Player.Channel, text = "왕국 탈환군 거점에 오신 것을 환영합니다." });
                        }
                    }
                    continue;
                }
                lock (gate)
                {
                    switch (cmd.op)
                    {
                        case "input": world.Input(peer.Id, cmd); break;
                        case "equip":
                            var player = world.Find(peer.Id);
                            if (player == null || world.Time - player.LastEquip < .3) break;
                            player.LastEquip = world.Time;
                            var equip = accounts.Equip(player.Profile.nickname, cmd.itemId);
                            if (equip.Profile == null) peer.Send(Error(equip.Error));
                            else { player.Profile = equip.Profile; peer.Send(new LanEvent { op = "profile", profile = equip.Profile, text = "장착 무기를 변경했습니다." }); }
                            break;
                        case "chat":
                            var chat = world.Chat(peer.Id, cmd.text);
                            if (chat != null) foreach (var p in world.Players.Where(p => p.Channel == chat.channel))
                                if (peers.TryGetValue(p.Id, out var recipient)) recipient.Send(chat);
                            break;
                        case "channel":
                            string error = world.ChangeChannel(peer.Id, cmd.channel);
                            if (error.Length > 0) peer.Send(Error(error));
                            else peer.Send(new LanEvent { op = "channel", channel = cmd.channel, text = $"채널 {cmd.channel}에 입장했습니다." });
                            break;
                        case "ping": peer.Send(new LanEvent { op = "pong" }); break;
                        default: peer.Send(Error("지원하지 않는 요청입니다.")); break;
                    }
                }
            }
        }
        catch (AuthenticationException ex) { Console.Error.WriteLine("TLS negotiation failed: " + ex.Message + " " + ex.InnerException?.Message); }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or JsonException or ObjectDisposedException)
        { /* Malformed/disconnected clients never stop the host. Do not log credential-bearing payloads. */ }
        catch (Exception ex) { Console.Error.WriteLine($"Client handler failed: {ex.GetType().Name}"); }
        finally
        {
            life.Cancel();
            peer.Outbox.Writer.TryComplete();
            peer.Socket.Dispose();
            lock (gate) world.Leave(peer.Id);
            peers.TryRemove(peer.Id, out _);
            if (writer != null) try { await writer; } catch (Exception) { }
        }
    }

    static LanEvent Error(string text) => new() { op = "error", text = text };
    async Task SendLoop(Peer peer, Stream stream, CancellationToken token)
    {
        try
        {
            await foreach (var value in peer.Outbox.Reader.ReadAllAsync(token))
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                var bytes = JsonSerializer.SerializeToUtf8Bytes(value, AccountStore.Json);
                await WriteFrame(stream, bytes, timeout.Token);
            }
        }
        finally { peer.Socket.Dispose(); }
    }

    async Task TickLoop()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        try
        {
            while (await timer.WaitForNextTickAsync(stopping.Token))
            {
                lock (gate)
                {
                    world.Tick(0.05f);
                    if (world.TickNumber % 2 != 0) continue;
                    for (int c = 1; c <= LanRules.MaxChannels; c++)
                    {
                        var snapshot = world.Snapshot(c);
                        foreach (var p in world.Players.Where(p => p.Channel == c)) if (peers.TryGetValue(p.Id, out var peer)) peer.Send(snapshot);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    public static async Task<byte[]> ReadFrame(Stream stream, CancellationToken token, int maxLength = 65536)
    {
        byte[] header = new byte[4];
        await stream.ReadExactlyAsync(header, token);
        int count = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header);
        if (count < 1 || count > maxLength) throw new IOException("Invalid packet length.");
        var bytes = new byte[count];
        await stream.ReadExactlyAsync(bytes, token);
        return bytes;
    }

    public static async Task WriteFrame(Stream stream, byte[] bytes, CancellationToken token)
    {
        byte[] header = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header, bytes.Length);
        await stream.WriteAsync(header, token);
        await stream.WriteAsync(bytes, token);
    }

    public async ValueTask DisposeAsync()
    {
        stopping.Cancel(); listener.Stop();
        foreach (var peer in peers.Values) peer.Socket.Dispose();
        if (accepting != null) await accepting;
        if (ticking != null) await ticking;
        await Task.WhenAll(sessions.Values);
        certificate.Dispose(); stopping.Dispose(); authentication.Dispose();
    }
}
