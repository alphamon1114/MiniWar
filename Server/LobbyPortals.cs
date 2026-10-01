using MiniWar.Online;

namespace MiniWar.Server;

public sealed partial class LobbyWorld
{
    bool CanUsePortal(Group group, LanDungeonPortal portal)
    {
        if (group.Completed) return false;
        if (!GateMap.Map.traversalPreview && !group.ClearedRooms.Contains(group.RoomId)) return false;
        if (!GateMap.Map.allowBacktracking && group.Visited.Contains(portal.targetRoomId)) return false;
        var target = GateMap.Map.rooms.FirstOrDefault(r => r.id == portal.targetRoomId);
        return target != null && target.id != group.RoomId && target.portals.Any(p => p.id == portal.targetPortalId
            && p.targetRoomId == group.RoomId && p.targetPortalId == portal.id);
    }

    public string PortalCommand(string id, LanCommand cmd)
    {
        var p = Find(id);
        if (p == null || p.DungeonRoom == null || !memberships.TryGetValue(id, out var key)) return "던전 안에서 사용할 수 있습니다.";
        var group = groups[key];
        if (p.Dead) return "관전 중에는 포탈에 들어갈 수 없습니다.";
        if (cmd.roomId != group.RoomId || cmd.roomSequence != group.RoomSequence) return "이미 다른 방으로 이동했습니다.";
        var portal = p.DungeonRoom.portals.FirstOrDefault(x => x.id == cmd.portalId);
        if (portal == null || !CanUsePortal(group, portal)) return "아직 사용할 수 없는 포탈입니다.";
        if (p.WaitingPortal.Length > 0 && p.WaitingPortal != portal.id) return "먼저 현재 포탈에서 나와주세요.";
        if (!portal.Contains(p.X, p.Y) || !OnDungeonGround(p) || p.VelocityY > .01f || p.DashRemaining > 0)
            return "포탈 앞에 서서 S를 눌러주세요.";
        group.Rally.Toggle(id, portal.id);
        p.WaitingPortal = group.Rally.Entered(id) ?? "";
        ResetMotion(p);
        // Preserve feet height at the supported entry point; the portal itself is only a trigger.
        return "";
    }

    void TickPortalGroups()
    {
        foreach (var group in groups.Values)
        {
            if (group.InstanceId.Length == 0) continue;
            var room = GateMap.Map.rooms.First(r => r.id == group.RoomId);
            var usable = new HashSet<string>(room.portals.Where(p => CanUsePortal(group, p)).Select(p => p.id));
            var voters = group.Members.Select(id => new PortalVoter { Id = id, Alive = !players[id].Dead }).ToArray();
            string? destination = group.Rally.Evaluate(voters, usable, Time);
            foreach (var id in group.Members) players[id].WaitingPortal = group.Rally.Entered(id) ?? "";
            if (destination == null) continue;
            var portal = room.portals.Single(p => p.id == destination);
            var target = GateMap.Map.rooms.Single(r => r.id == portal.targetRoomId);
            var arrival = target.portals.Single(p => p.id == portal.targetPortalId);
            CombatRoom(group, room).Pause();
            group.RoomId = target.id; group.RoomSequence++; group.Visited.Add(target.id); group.Rally.Reset();
            foreach (var id in group.Members)
            {
                var p = players[id]; ResetMotion(p); p.WaitingPortal = "";
                p.DungeonRoom = target; p.X = arrival.arrivalX; p.Y = arrival.arrivalY;
                p.Combat.ProtectedUntil = Time + 1;
                NotifyParty(id, target.name + "으로 이동했습니다.");
            }
        }
    }
}
