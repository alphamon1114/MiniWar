using System.Net;
using MiniWar.Online;
using MiniWar.Server;

static class DiscoveryTests
{
    public static async Task Run(Action<bool, string> check, string root, string password)
    {
        string nonce = Guid.NewGuid().ToString("N"), pin = new string('A', 64);
        check(LanDiscovery.RequestNonce(LanDiscovery.Request(nonce)) == nonce, "Discovery query nonce roundtrip");
        check(LanDiscovery.RequestNonce("MINIWAR_FIND|0|" + nonce) == null && LanDiscovery.RequestNonce("MINIWAR_FIND|4|invalid") == null,
            "Discovery rejects incompatible and malformed requests");
        string reply = LanDiscovery.Reply(nonce, 7777, pin, "Classroom");
        check(LanDiscovery.Parse(reply, "wrong", IPAddress.Loopback) == null, "Discovery rejects unrelated nonce response");
        check(LanDiscovery.Parse(LanDiscovery.Reply(nonce, 0, pin, "Classroom"), nonce, IPAddress.Loopback) == null
            && LanDiscovery.Parse(LanDiscovery.Reply(nonce, 7777, "bad-pin", "Classroom"), nonce, IPAddress.Loopback) == null,
            "Discovery rejects invalid endpoint and certificate data");
        check(LanDiscovery.Parse(reply, nonce, IPAddress.Loopback).Address == "127.0.0.1", "Discovery uses packet sender as address");
        await using var host = new LanHost(Path.Combine(root, "discovery"), 0, IPAddress.Loopback); host.Start();
        await using var responder = new DiscoveryResponder(host, 0, IPAddress.Loopback);
        var found = await Task.Run(() => LanDiscovery.Search(CancellationToken.None, responder.Port, true));
        check(found.Count == 1 && found[0].Port == host.Port && found[0].Fingerprint == host.Fingerprint,
            "Actual UDP discovery returns TLS endpoint without manually supplied pin and deduplicates repeated replies");
        await using var client = await TestClient.Connect(found[0].Port, found[0].Fingerprint);
        await client.Send(new LanCommand { op = "register", nickname = "AutoPlayer", password = password });
        check((await client.Wait("welcome")).profile.nickname == "AutoPlayer", "Discovered server accepts encrypted nickname/password registration");
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        check(LanDiscovery.Search(cancel.Token, responder.Port, true).Count == 0, "Discovery cancellation stops without connecting");
    }
}
