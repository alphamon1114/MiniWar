using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MiniWar.Online
{
    /// <summary>One TLS connection. Network workers only exchange plain DTOs with the main thread.</summary>
    public sealed class LanClient : IDisposable
    {
        public readonly ConcurrentQueue<LanEvent> Events = new ConcurrentQueue<LanEvent>();
        readonly ConcurrentQueue<byte[]> outgoing = new ConcurrentQueue<byte[]>();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly object socketGate = new object();
        TcpClient socket;
        volatile bool stopped;
        volatile bool connected;
        public bool Connected => connected && !stopped;

        public void Connect(string host, int port, string fingerprint, string nickname, string password, bool register, int body = 0)
        {
            string auth = JsonUtility.ToJson(new LanCommand { op = register ? "register" : "login", nickname = nickname, password = password, body = body });
            Task.Run(() => Run(host, port, fingerprint, auth));
        }

        void Run(string host, int port, string expectedFingerprint, string authenticationJson)
        {
            Task sender = null;
            try
            {
                lock (socketGate)
                {
                    if (stopped) return;
                    socket = new TcpClient { NoDelay = true, ReceiveTimeout = 15000, SendTimeout = 5000 };
                }
                var opening = socket.ConnectAsync(host, port);
                if (!opening.Wait(10000)) throw new TimeoutException("서버 연결 시간이 초과됐습니다.");
                opening.GetAwaiter().GetResult();
                using (var stream = new SslStream(socket.GetStream(), false, (_, cert, __, ___) =>
                {
                    if (cert == null) return false;
                    using (var sha = SHA256.Create())
                    {
                        string actual = BitConverter.ToString(sha.ComputeHash(cert.GetRawCertData())).Replace("-", "");
                        return string.Equals(actual, expectedFingerprint, StringComparison.OrdinalIgnoreCase);
                    }
                }))
                {
                    stream.ReadTimeout = 15000;
                    stream.WriteTimeout = 5000;
                    stream.AuthenticateAsClient(host, null, SslProtocols.Tls12, false);
                    connected = true;
                    outgoing.Enqueue(Encoding.UTF8.GetBytes(authenticationJson));
                    authenticationJson = null;
                    sender = Task.Run(() => WriteLoop(stream));
                    wake.Set();
                    while (!stopped)
                    {
                        byte[] header = ReadExactly(stream, 4);
                        int count = (header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3];
                        if (count < 1 || count > 65536) throw new IOException("서버 응답 크기가 올바르지 않습니다.");
                        string json = Encoding.UTF8.GetString(ReadExactly(stream, count));
                        var message = JsonUtility.FromJson<LanEvent>(json);
                        if (message == null || message.version != LanRules.Version) throw new IOException("서버 버전이 다릅니다.");
                        if (Events.Count > 256) throw new IOException("수신 메시지가 너무 많습니다.");
                        Events.Enqueue(message);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!stopped)
                    Events.Enqueue(new LanEvent { op = "disconnected", text = ex is AuthenticationException
                        ? "서버 인증 코드가 일치하지 않습니다. 학원 서버 창의 SHA-256 코드를 확인하세요."
                        : "서버 연결이 끊겼습니다. 주소와 서버 실행 상태를 확인하세요." });
            }
            finally
            {
                stopped = true; connected = false;
                lock (socketGate) socket?.Close();
                wake.Set();
                if (sender != null) try { sender.Wait(1000); } catch (Exception) { }
            }
        }

        void WriteLoop(Stream stream)
        {
            try
            {
                while (!stopped)
                {
                    if (!outgoing.TryDequeue(out var bytes)) { wake.WaitOne(200); continue; }
                    byte[] header = { (byte)(bytes.Length >> 24), (byte)(bytes.Length >> 16), (byte)(bytes.Length >> 8), (byte)bytes.Length };
                    stream.Write(header, 0, 4);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush();
                }
            }
            catch (Exception) { lock (socketGate) socket?.Close(); }
        }

        static byte[] ReadExactly(Stream stream, int count)
        {
            var bytes = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int got = stream.Read(bytes, offset, count - offset);
                if (got == 0) throw new EndOfStreamException();
                offset += got;
            }
            return bytes;
        }

        public void Send(LanCommand command)
        {
            if (!Connected) return;
            if (outgoing.Count > 128) { Dispose(); return; }
            outgoing.Enqueue(Encoding.UTF8.GetBytes(JsonUtility.ToJson(command)));
            wake.Set();
        }

        public void Dispose()
        {
            stopped = true;
            lock (socketGate) socket?.Close();
            wake.Set();
            // The event remains valid until the background writer has exited.
        }
    }
}
