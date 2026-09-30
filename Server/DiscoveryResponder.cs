using System.Net;
using System.Net.Sockets;
using System.Text;
using MiniWar.Online;

namespace MiniWar.Server;

public sealed class DiscoveryResponder : IAsyncDisposable
{
    readonly UdpClient udp;
    readonly CancellationTokenSource stop = new();
    readonly Task run;
    public int Port => ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
    public DiscoveryResponder(LanHost host, int port = LanDiscovery.Port, IPAddress? address = null)
    {
        udp = new UdpClient(new IPEndPoint(address ?? IPAddress.Any, port));
        run = Run(host);
    }
    async Task Run(LanHost host)
    {
        var window = DateTime.UtcNow; int replies = 0;
        try
        {
            while (!stop.IsCancellationRequested)
            {
                UdpReceiveResult packet;
                try { packet = await udp.ReceiveAsync(stop.Token); }
                catch (SocketException) when (!stop.IsCancellationRequested) { continue; }
                if (packet.Buffer.Length > 128) continue;
                var nonce = LanDiscovery.RequestNonce(Encoding.UTF8.GetString(packet.Buffer));
                if (nonce == null) continue;
                if ((DateTime.UtcNow - window).TotalSeconds >= 1) { window = DateTime.UtcNow; replies = 0; }
                if (++replies > 120) continue;
                var name = Environment.MachineName;
                if (name.Length > 64) name = name[..64];
                var bytes = Encoding.UTF8.GetBytes(LanDiscovery.Reply(nonce, host.Port, host.Fingerprint, name));
                try { await udp.SendAsync(bytes, packet.RemoteEndPoint, stop.Token); }
                catch (SocketException) { }
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) when (stop.IsCancellationRequested) { }
    }
    public async ValueTask DisposeAsync()
    {
        stop.Cancel(); udp.Dispose(); await run; stop.Dispose();
    }
}
