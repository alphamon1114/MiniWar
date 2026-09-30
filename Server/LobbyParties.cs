using MiniWar.Online;

namespace MiniWar.Server;

// Ephemeral recruitment state. Every entry point is called under LanHost's world lock.
public sealed partial class LobbyWorld
{
    sealed class Group(string title, int channel, string leader)
    {
        public readonly string Id = Guid.NewGuid().ToString("N"), Title = title;
        public readonly int Channel = channel;
        public readonly List<string> Members = [leader];
        public string Leader => Members[0];
    }
    sealed record Application(string Id, string PartyId, string PlayerId);
    readonly Dictionary<string, Group> groups = new();
    readonly Dictionary<string, string> memberships = new();
    readonly Dictionary<string, Application> applications = new(); // One pending request per player.
    readonly Queue<(string Recipient, LanEvent Event)> partyNotifications = new();
    public long PartyRevision { get; private set; }

    void NotifyParty(string id, string text) => partyNotifications.Enqueue((id, new LanEvent { op = "party_notice", text = text }));
    public IEnumerable<(string Recipient, LanEvent Event)> DrainPartyNotifications()
    {
        while (partyNotifications.TryDequeue(out var item)) yield return item;
    }

    public LanEvent PartyState(string id)
    {
        var result = new LanEvent { op = "parties" };
        var player = Find(id);
        if (player == null) return result;
        result.channel = player.Channel;
        result.parties = groups.Values.Where(g => g.Channel == player.Channel).Select(g => new LanPartyListing
        {
            id = g.Id, title = g.Title, leader = players[g.Leader].Profile.nickname,
            count = g.Members.Count
        }).ToArray();
        if (memberships.TryGetValue(id, out var groupId))
        {
            var g = groups[groupId];
            result.party = new LanParty
            {
                id = g.Id, title = g.Title, leader = players[g.Leader].Profile.nickname,
                members = g.Members.Select(member => new LanPartyMember
                { nickname = players[member].Profile.nickname, body = players[member].Profile.body }).ToArray(),
                applications = id != g.Leader ? [] : applications.Values.Where(a => a.PartyId == g.Id).Select(a => new LanPartyApplication
                { id = a.Id, nickname = players[a.PlayerId].Profile.nickname, body = players[a.PlayerId].Profile.body }).ToArray()
            };
        }
        if (applications.TryGetValue(id, out var pending))
        {
            result.pendingPartyId = pending.PartyId;
            result.pendingPartyTitle = groups[pending.PartyId].Title;
        }
        return result;
    }

    public string PartyCommand(string id, LanCommand cmd)
    {
        var player = Find(id);
        if (player == null) return "로그인이 필요합니다.";
        switch (cmd.op)
        {
            case "party_create":
                if (memberships.ContainsKey(id)) return "이미 파티에 가입되어 있습니다.";
                if (applications.ContainsKey(id)) return "참가 신청을 취소한 뒤 파티를 만드세요.";
                string title = new string((cmd.text ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
                if (title.Length < 2 || title.Length > 24) return "파티 이름을 2~24자로 입력하세요.";
                var created = new Group(title, player.Channel, id);
                groups.Add(created.Id, created); memberships.Add(id, created.Id);
                PartyRevision++; NotifyParty(id, "파티를 만들었습니다. 참가 신청을 기다려 주세요.");
                return "";
            case "party_apply":
                if (memberships.ContainsKey(id)) return "이미 파티에 가입되어 있습니다.";
                if (applications.ContainsKey(id)) return "이미 참가 신청 중입니다. 먼저 기존 신청을 취소하세요.";
                if (!groups.TryGetValue(cmd.partyId ?? "", out var target) || target.Channel != player.Channel) return "이 채널에서 모집 중인 파티가 아닙니다.";
                if (target.Members.Count >= LanRules.PartyCapacity) return "파티 인원이 가득 찼습니다.";
                applications.Add(id, new Application(Guid.NewGuid().ToString("N"), target.Id, id));
                PartyRevision++;
                NotifyParty(id, $"'{target.Title}' 파티에 참가 신청했습니다.");
                NotifyParty(target.Leader, $"{player.Profile.nickname} 님이 참가 신청했습니다. [P]에서 확인하세요.");
                return "";
            case "party_cancel":
                // Match the party too, so a delayed cancellation cannot cancel an unrelated application.
                if (!applications.TryGetValue(id, out var own) || own.PartyId != cmd.partyId) return "해당 파티에 대기 중인 신청이 없습니다.";
                RemoveApplication(id); NotifyParty(id, "참가 신청을 취소했습니다."); return "";
            case "party_accept":
            case "party_reject":
                if (!memberships.TryGetValue(id, out var groupId) || groupId != cmd.partyId || groups[groupId].Leader != id)
                    return "해당 파티의 파티장만 신청을 처리할 수 있습니다.";
                var group = groups[groupId];
                // Server-generated ticket identity prevents accepting a cancelled/replaced request.
                var request = applications.Values.FirstOrDefault(a => a.Id == cmd.applicationId && a.PartyId == groupId);
                if (request == null) return "이미 처리되거나 취소된 참가 신청입니다.";
                if (cmd.op == "party_reject")
                {
                    RemoveApplication(request.PlayerId);
                    NotifyParty(request.PlayerId, $"'{group.Title}' 파티의 참가 신청이 거절되었습니다.");
                    NotifyParty(id, "참가 신청을 거절했습니다."); return "";
                }
                var applicant = Find(request.PlayerId);
                if (applicant == null || applicant.Channel != group.Channel || memberships.ContainsKey(request.PlayerId))
                { RemoveApplication(request.PlayerId); return "현재 참가할 수 없는 플레이어입니다."; }
                if (group.Members.Count >= LanRules.PartyCapacity) return "파티 인원이 가득 찼습니다.";
                RemoveApplication(request.PlayerId);
                memberships.Add(request.PlayerId, groupId); group.Members.Add(request.PlayerId); PartyRevision++;
                foreach (string member in group.Members) NotifyParty(member, $"{applicant.Profile.nickname} 님이 파티에 참가했습니다. ({group.Members.Count}/{LanRules.PartyCapacity})");
                if (group.Members.Count == LanRules.PartyCapacity) ClearApplications(groupId, "파티 인원이 가득 차 참가 신청이 종료되었습니다.");
                return "";
            case "party_leave":
                if (!memberships.TryGetValue(id, out var leaving) || leaving != cmd.partyId) return "해당 파티에 가입되어 있지 않습니다.";
                LeaveParty(id); NotifyParty(id, "파티에서 탈퇴했습니다."); return "";
            default: return "지원하지 않는 파티 요청입니다.";
        }
    }

    bool RemoveApplication(string id)
    {
        if (!applications.Remove(id)) return false;
        PartyRevision++; return true;
    }
    void ClearApplications(string groupId, string reason)
    {
        foreach (var request in applications.Values.Where(a => a.PartyId == groupId).ToArray())
        { RemoveApplication(request.PlayerId); NotifyParty(request.PlayerId, reason); }
    }
    void LeaveParty(string id)
    {
        if (!memberships.Remove(id, out var groupId)) return;
        var group = groups[groupId]; bool wasLeader = group.Leader == id;
        group.Members.Remove(id); PartyRevision++;
        if (group.Members.Count == 0)
        {
            ClearApplications(groupId, "파티가 해산되어 참가 신청이 종료되었습니다.");
            groups.Remove(groupId); return;
        }
        string text = $"{players[id].Profile.nickname} 님이 파티를 떠났습니다.";
        if (wasLeader) text += $" 새 파티장: {players[group.Leader].Profile.nickname}";
        foreach (string member in group.Members) NotifyParty(member, text);
    }
}
