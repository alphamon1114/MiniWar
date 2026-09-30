using System.Net;
using MiniWar.Online;
using MiniWar.Server;

static class PartyTests
{
    public static async Task Run(Action<bool, string> check, string root, string password)
    {
        var world = new LobbyWorld();
        string Cmd(string id, string op, string? group = null, string? ticket = null, string? title = null)
            => world.PartyCommand(id, new LanCommand { op = op, partyId = group!, applicationId = ticket!, text = title! });
        LanEvent State(string id) => world.PartyState(id);
        check(Cmd("missing", "party_create", title: "test").Length > 0, "Unauthenticated party request rejected");
        for (int i = 0; i < 9; i++) world.Join(i.ToString(), new LanProfile { nickname = "Player" + i, body = i % 2 });
        check(Cmd("0", "party_create", title: " ").Length > 0 && Cmd("0", "party_create", title: new string('x', 25)).Length > 0,
            "Party name validates length and empty input");
        check(Cmd("0", "party_create", title: " 성문\n외곽 ") == "", "Leader creates party with sanitized title");
        string a = State("0").party.id;
        check(State("0").party.title == "성문외곽" && State("0").party.members.Length == 1 && State("1").parties.Length == 1,
            "Creator is leader and party appears in current channel");
        check(Cmd("0", "party_create", title: "duplicate").Length > 0, "Member cannot create a second party");
        Cmd("1", "party_create", title: "다른 파티"); string b = State("1").party.id;
        check(Cmd("2", "party_apply", a) == "" && State("2").pendingPartyId == a, "Applicant waits for approval without joining");
        string oldTicket = State("0").party.applications.Single().id;
        check(Cmd("2", "party_apply", b).Length > 0 && Cmd("2", "party_create", title: "new").Length > 0,
            "Only one application and no create while waiting");
        check(State("3").party == null && State("3").pendingPartyId == null && State("1").party.applications.Length == 0,
            "Application identity is private to applicant's target leader");
        check(Cmd("1", "party_accept", a, oldTicket).Length > 0 && Cmd("2", "party_accept", a, oldTicket).Length > 0,
            "Another leader and applicant cannot accept requests");
        check(Cmd("0", "party_reject", a, oldTicket) == "" && State("2").pendingPartyId == null,
            "Rejection clears request and permits reapplication");
        check(world.DrainPartyNotifications().Any(n => n.Recipient == "2" && n.Event.text.Contains("거절")), "Applicant receives rejection notice");
        Cmd("2", "party_apply", a); string ticket = State("0").party.applications.Single().id;
        check(ticket != oldTicket && Cmd("0", "party_accept", a, oldTicket).Length > 0,
            "Replayed approval cannot accept a new application");
        check(Cmd("0", "party_accept", a, ticket) == "" && State("2").party.members.Length == 2 && State("2").pendingPartyId == null,
            "Approval atomically joins and removes pending application");
        check(Cmd("0", "party_accept", a, ticket).Length > 0 && State("0").party.members.Length == 2, "Duplicate acceptance cannot duplicate membership");
        check(world.ChangeChannel("2", 2).Length > 0, "Member must leave party before changing channel");
        Cmd("3", "party_apply", a); ticket = State("0").party.applications.Single().id;
        check(Cmd("3", "party_cancel", b).Length > 0 && Cmd("3", "party_cancel", a) == "", "Only own matching application can be withdrawn");
        check(Cmd("0", "party_accept", a, ticket).Length > 0, "Cancelled request cannot be accepted");
        Cmd("3", "party_apply", a);
        check(world.ChangeChannel("3", 2) == "" && State("3").pendingPartyId == null && State("3").parties.Length == 0,
            "Channel change clears pending request and hides other channel's parties");
        check(Cmd("3", "party_apply", a).Length > 0, "Forged cross-channel join denied");
        world.ChangeChannel("3", 1);
        foreach (string id in new[] { "3", "4", "5" }) Cmd(id, "party_apply", a);
        check(State("2").party.applications.Length == 0 && State("0").party.applications.Length == 3,
            "Ordinary members never receive leader application inbox");
        foreach (string name in new[] { "Player3", "Player4" })
            Cmd("0", "party_accept", a, State("0").party.applications.Single(r => r.nickname == name).id);
        check(State("0").party.members.Length == 4 && State("0").party.applications.Length == 0 && State("5").pendingPartyId == null,
            "Last seat filled atomically; remaining requests notified and cleared");
        check(Cmd("5", "party_apply", a).Length > 0, "Fifth member cannot enter full party");
        check(Cmd("0", "party_leave", b).Length > 0, "Leave command must name own party");
        Cmd("0", "party_leave", a);
        check(State("2").party.leader == "Player2" && State("0").party == null, "Leader departure transfers to earliest remaining member");
        Cmd("5", "party_apply", a); world.Leave("2");
        check(State("3").party.leader == "Player3" && State("3").party.applications.Length == 1, "Disconnect transfers leadership and application inbox");
        Cmd("3", "party_accept", a, State("3").party.applications.Single().id);
        check(State("5").party.leader == "Player3", "New leader can accept pending applicant");
        Cmd("6", "party_apply", b); world.Leave("1");
        check(State("6").pendingPartyId == null && State("6").parties.All(p => p.id != b), "Last member disconnect disbands and clears applicants");
        Cmd("7", "party_apply", a); world.Leave("7");
        check(State("3").party.applications.Length == 0, "Disconnected applicant disappears from leader inbox");
        foreach (string id in new[] { "3", "4", "5" }) world.Leave(id);
        check(State("0").parties.Length == 0, "No stale parties after all members leave");
        check(world.DrainPartyNotifications().Any(n => n.Event.text.Contains("해산")), "Disbanded party notifies pending applicants");

        await Network(check, Path.Combine(root, "party-network"), password);
    }

    static async Task Network(Action<bool, string> check, string path, string password)
    {
        await using var host = new LanHost(path, 0, IPAddress.Loopback); host.Start();
        await using var leader = await TestClient.Connect(host.Port, host.Fingerprint);
        await using var applicant = await TestClient.Connect(host.Port, host.Fingerprint);
        await using var observer = await TestClient.Connect(host.Port, host.Fingerprint);
        foreach (var pair in new[] { (leader, "Leader"), (applicant, "Applicant"), (observer, "Observer") })
        {
            await pair.Item1.Send(new LanCommand { op = "register", nickname = pair.Item2, password = password });
            await pair.Item1.Wait("welcome");
        }
        await leader.Send(new LanCommand { op = "party_create", text = "성문 외곽 모집" });
        var state = await leader.Wait(e => e.op == "parties" && e.party != null);
        string partyId = state.party.id;
        check((await applicant.Wait(e => e.op == "parties" && e.parties.Length == 1)).parties[0].leader == "Leader", "TLS party list reaches another client");
        await observer.Send(new LanCommand { op = "channel", channel = 2 }); await observer.Wait("channel");
        check((await observer.Wait(e => e.op == "parties" && e.channel == 2)).parties.Length == 0, "TLS list isolated to town channel");
        await applicant.Send(new LanCommand { op = "party_apply", partyId = partyId });
        state = await leader.Wait(e => e.op == "parties" && e.party?.applications.Length == 1);
        string ticket = state.party.applications[0].id;
        var pending = await applicant.Wait(e => e.op == "parties" && e.pendingPartyId == partyId);
        check(pending.party == null, "TLS applicant remains outside party while waiting");
        await applicant.Send(new LanCommand { op = "party_accept", partyId = partyId, applicationId = ticket });
        check((await applicant.Wait("error")).text.Contains("파티장"), "TLS forged leader approval rejected");
        await leader.Send(new LanCommand { op = "party_reject", partyId = partyId, applicationId = ticket });
        check((await applicant.Wait("party_notice")).text.Contains("거절"), "TLS rejection delivered to applicant");
        await applicant.Wait(e => e.op == "parties" && e.pendingPartyId == null);
        await Task.Delay(600);
        await applicant.Send(new LanCommand { op = "party_apply", partyId = partyId });
        state = await leader.Wait(e => e.op == "parties" && e.party?.applications.Length == 1 && e.party.applications[0].id != ticket);
        await leader.Send(new LanCommand { op = "party_accept", partyId = partyId, applicationId = state.party.applications[0].id });
        state = await applicant.Wait(e => e.op == "parties" && e.party?.members.Length == 2);
        check(state.party.leader == "Leader" && state.party.applications.Length == 0 && state.pendingPartyId == null,
            "TLS acceptance updates member list and private inbox correctly");
        await leader.Send(new LanCommand { op = "party_leave", partyId = partyId });
        await applicant.Wait(e => e.op == "parties" && e.party?.leader == "Applicant");
        await observer.Send(new LanCommand { op = "channel", channel = 1 }); await observer.Wait("channel");
        await observer.Send(new LanCommand { op = "party_apply", partyId = partyId });
        state = await applicant.Wait(e => e.op == "parties" && e.party?.applications.Length == 1);
        await applicant.Send(new LanCommand { op = "party_accept", partyId = partyId, applicationId = state.party.applications[0].id });
        await observer.Wait(e => e.op == "parties" && e.party?.members.Length == 2);
        await applicant.DisposeAsync();
        state = await observer.Wait(e => e.op == "parties" && e.party?.leader == "Observer");
        check(state.party.members.Length == 1, "Real socket disconnect transfers leader without stale member");
        await observer.Send(new LanCommand { op = "party_leave", partyId = partyId });
        state = await leader.Wait(e => e.op == "parties" && e.parties.Length == 0);
        check(state.party == null, "TLS disband removes listing for remaining online clients");
    }
}
