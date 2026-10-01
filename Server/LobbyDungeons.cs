using System.Security.Cryptography;
using System.Text.Json;
using MiniWar.Online;

namespace MiniWar.Server;

public sealed partial class LobbyWorld
{
    static readonly (LanDungeonMap Map, string Revision) GateMap = LoadGateMap();
    public static string DungeonRevision => GateMap.Revision;
    static (LanDungeonMap, string) LoadGateMap()
    {
        using var stream = typeof(LobbyWorld).Assembly.GetManifestResourceStream("MiniWar.GateOutskirts.json")
            ?? throw new InvalidOperationException("Export the authored dungeon before building the server.");
        using var bytes = new MemoryStream(); stream.CopyTo(bytes);
        var data = bytes.ToArray();
        var map = JsonSerializer.Deserialize<LanDungeonMap>(data, AccountStore.Json)!;
        if (map.id != LanDungeons.Gate || !map.rooms.Any(r => r.id == map.startRoomId && r.floors.Length > 0))
            throw new InvalidOperationException("Invalid authored dungeon export.");
        return (map, Convert.ToHexString(SHA256.HashData(data)));
    }

    string StartDungeon(string id, LanCommand cmd)
    {
        if (!memberships.TryGetValue(id, out var key) || key != cmd.partyId || groups[key].Leader != id)
            return "파티장만 던전에 출발할 수 있습니다.";
        var group = groups[key];
        if (group.InstanceId.Length > 0) return "이미 던전에 입장했습니다.";
        var info = LanDungeons.Find(group.DungeonId);
        if (info == null || !info.Available || info.Id != GateMap.Map.id) return "아직 준비 중인 던전입니다.";
        if (group.Members.Count < info.Minimum || group.Members.Count > info.Capacity) return "던전 입장 인원을 확인하세요.";
        if (cmd.dungeonRevision != GateMap.Revision) return "맵이 변경되었습니다. 최신 서버 구동기와 게임으로 다시 실행해 주세요.";
        var room = GateMap.Map.rooms.Single(r => r.id == GateMap.Map.startRoomId);
        group.InstanceId = Guid.NewGuid().ToString("N");
        group.Rally.Reset(); group.Visited.Clear(); group.ClearedRooms.Clear();
        group.Combat.Clear(); group.Completed = false;
        group.RoomId = room.id; group.RoomSequence++; group.Visited.Add(room.id);
        group.Power = info.Capacity == 6 ? 1 : LanRules.DungeonPower(group.Members.Count);
        ClearApplications(key, "파티가 던전에 출발하여 참가 신청이 종료되었습니다.");
        foreach (var member in group.Members)
        {
            var p = players[member]; ResetMotion(p);
            p.Dead = false; p.WaitingPortal = "";
            p.ReviveUsed = false; p.Combat = new CombatFighter { Id = member, ProtectedUntil = Time + 2 };
            p.InstanceId = group.InstanceId; p.DungeonRoom = room;
            p.X = room.spawnX; p.Y = room.spawnY;
            NotifyParty(member, info.Name + "에 입장했습니다.");
        }
        PartyRevision++;
        return "";
    }

    string ReturnPartyToTown(string id, string? partyId)
    {
        if (!memberships.TryGetValue(id, out var key) || key != partyId || groups[key].Leader != id)
            return "파티장만 파티 전체를 마을로 복귀시킬 수 있습니다.";
        var group = groups[key];
        if (group.InstanceId.Length == 0) return "이미 마을에 있습니다.";
        foreach (string member in group.Members) { ReturnPlayerToTown(players[member]); NotifyParty(member, "파티와 함께 마을로 복귀했습니다."); }
        group.InstanceId = ""; group.Power = 0; group.Rally.Reset(); PartyRevision++;
        return "";
    }

    static void ResetMotion(LobbyPlayer p)
    {
        p.Move = p.VelocityY = p.DashRemaining = 0;
        p.Jump = p.Drop = p.Fire = p.Aiming = p.DashQueued = false;
        p.DashUses = 0; p.DashChainUntil = -1; p.AirJumpAvailable = true;
        p.DroppingThrough.Clear();
    }
    static void ReturnPlayerToTown(LobbyPlayer p)
    {
        if (p.InstanceId.Length == 0) return;
        ResetMotion(p); p.InstanceId = ""; p.DungeonRoom = null;
        p.WaitingPortal = ""; p.Dead = false;
        p.Combat = new CombatFighter { Id = p.Id }; p.ReviveUsed = false;
        p.X = LanRules.TownSpawnX; p.Y = 0;
    }
    public LanEvent SnapshotFor(string id)
    {
        var p = players[id];
        var result = Snapshot(p.Channel, p.InstanceId);
        if (p.DungeonRoom != null && memberships.TryGetValue(id, out var key))
        {
            var g = groups[key];
            var combat = CombatRoom(g, p.DungeonRoom);
            result.dungeon = new LanDungeonVisit { instanceId = p.InstanceId, dungeonId = groups[key].DungeonId,
                roomId = p.DungeonRoom.id, roomName = p.DungeonRoom.name, revision = GateMap.Revision, power = groups[key].Power,
                roomSequence = g.RoomSequence, traversalPreview = GateMap.Map.traversalPreview,
                portalsOpen = GateMap.Map.traversalPreview || g.ClearedRooms.Contains(g.RoomId),
                countingPortalId = g.Rally.CountingPortal, countdown = g.Rally.Remaining(Time),
                aliveCount = g.Members.Count(m => !players[m].Dead), visitedRooms = g.Visited.ToArray(),
                enemies = combat.Enemies, projectiles = combat.Projectiles, enemiesRemaining = combat.Remaining, completed = g.Completed,
                portals = p.DungeonRoom.portals.Select(portal => new LanPortalWaiting { id = portal.id, direction = portal.direction,
                    targetName = GateMap.Map.rooms.FirstOrDefault(r => r.id == portal.targetRoomId)?.name ?? "?",
                    entered = g.Rally.Count(portal.id), open = CanUsePortal(g, portal) }).ToArray() };
        }
        return result;
    }

    static bool OnDungeonGround(LobbyPlayer p) => LanDungeonPhysics.Grounded(p.DungeonRoom!,p.X,p.Y,p.DroppingThrough);
    static float MoveX(LobbyPlayer p, float dx)
    {
        var room = p.DungeonRoom;
        if (room == null) return Math.Clamp(p.X + dx, .5f, LanRules.TownWidth - .5f);
        return LanDungeonPhysics.MoveX(room,p.X,p.Y,dx);
    }
    static void TickDungeonVertical(LobbyPlayer p, float dt)
    {
        var room = p.DungeonRoom!;
        bool grounded = OnDungeonGround(p);
        if (p.Drop && grounded && LanDungeonPhysics.CanDrop(room,p.X,p.Y))
        {
            LanDungeonPhysics.Drop(room,p.Y,p.DroppingThrough);
            p.VelocityY = -2;
        }
        else if (p.Jump && !p.Drop)
        {
            if (grounded) p.VelocityY = LanDungeonPhysics.JumpSpeed(room);
            else if (p.AirJumpAvailable)
            { p.VelocityY = MathF.Sqrt(2 * LanRules.Gravity * room.airJumpHeight); p.AirJumpAvailable = false; p.DroppingThrough.Clear(); }
        }
        p.Jump = p.Drop = false;
        bool landed=LanDungeonPhysics.Vertical(room,p.X,ref p.Y,ref p.VelocityY,p.DroppingThrough,dt);
        if (landed) p.AirJumpAvailable = true;
        if (p.Y < room.y - 3) { ResetMotion(p); p.X = room.spawnX; p.Y = room.spawnY; }
    }
}
