#nullable disable
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MiniWar.Online
{
    public sealed class LanServerInfo
    {
        public string Address, Fingerprint, Name;
        public int Port;
        public string Key => Address + ":" + Port;
    }

    // Discovery trusts the local network; the advertised pin binds TLS to the discovered endpoint,
    // but is NOT independent proof of the server operator's identity.
    public static class LanDiscovery
    {
        public const int Port = 7778;
        public static string Request(string nonce) => "MINIWAR_FIND|" + LanRules.Version + "|" + nonce;
        public static string RequestNonce(string text)
        {
            var p = text.Split('|');
            Guid parsed;
            return p.Length == 3 && p[0] == "MINIWAR_FIND" && p[1] == LanRules.Version.ToString()
                && Guid.TryParseExact(p[2], "N", out parsed) ? p[2] : null;
        }
        public static string Reply(string nonce, int port, string pin, string name)
            => "MINIWAR_SERVER|" + LanRules.Version + "|" + nonce + "|" + port + "|" + pin + "|" + name.Replace('|', '_');
        public static LanServerInfo Parse(string text, string nonce, IPAddress sender)
        {
            var p = text.Split('|'); int port;
            if (p.Length != 6 || p[0] != "MINIWAR_SERVER" || p[1] != LanRules.Version.ToString() || p[2] != nonce
                || !int.TryParse(p[3], out port) || port < 1 || port > 65535 || p[4].Length != 64
                || p[5].Length < 1 || p[5].Length > 64 || sender.AddressFamily != AddressFamily.InterNetwork) return null;
            foreach (char c in p[4]) if (!Uri.IsHexDigit(c)) return null;
            foreach (char c in p[5]) if (char.IsControl(c)) return null;
            return new LanServerInfo { Address = sender.ToString(), Port = port, Fingerprint = p[4].ToUpperInvariant(), Name = p[5] };
        }
        public static List<LanServerInfo> Search(CancellationToken cancel, int discoveryPort = Port, bool loopbackOnly = false)
        {
            string nonce = Guid.NewGuid().ToString("N");
            byte[] query = Encoding.UTF8.GetBytes(Request(nonce));
            var results = new Dictionary<string, LanServerInfo>();
            using (var udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0)))
            {
                udp.EnableBroadcast = true; udp.Client.ReceiveTimeout = 150;
                var targets = new HashSet<IPAddress> { IPAddress.Loopback };
                if (!loopbackOnly)
                {
                    targets.Add(IPAddress.Broadcast);
                    foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                        foreach (var address in adapter.GetIPProperties().UnicastAddresses)
                        {
                            if (address.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address.Address) || address.IPv4Mask == null) continue;
                            var bytes = address.Address.GetAddressBytes(); var mask = address.IPv4Mask.GetAddressBytes();
                            for (int i = 0; i < 4; i++) bytes[i] = (byte)(bytes[i] | (mask[i] ^ 255));
                            targets.Add(new IPAddress(bytes));
                        }
                    }
                }
                var deadline = DateTime.UtcNow.AddSeconds(2.5); var nextSend = DateTime.MinValue;
                while (!cancel.IsCancellationRequested && DateTime.UtcNow < deadline)
                {
                    if (DateTime.UtcNow >= nextSend)
                    {
                        foreach (var target in targets)
                            try { udp.Send(query, query.Length, new IPEndPoint(target, discoveryPort)); } catch (SocketException) { }
                        nextSend = DateTime.UtcNow.AddMilliseconds(700);
                    }
                    try
                    {
                        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                        var bytes = udp.Receive(ref sender);
                        if (bytes.Length > 1024) continue;
                        var info = Parse(Encoding.UTF8.GetString(bytes), nonce, sender.Address);
                        if (info != null && results.Count < 32)
                        {
                            // One local host may reply through both loopback and its LAN adapter.
                            string key = info.Fingerprint + ":" + info.Port;
                            if (!results.ContainsKey(key) || IPAddress.IsLoopback(sender.Address)) results[key] = info;
                        }
                    }
                    catch (SocketException) { }
                }
            }
            return new List<LanServerInfo>(results.Values);
        }
    }
}
