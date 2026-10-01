using MiniWar.Online;

namespace MiniWar.Server;

public sealed class LobbyPlayer
{
    public required string Id;
    public required LanProfile Profile;
    public int Channel;
    public float X = LanRules.TownSpawnX, Y, VelocityY, Move;
    public bool Jump;
    public bool Drop;
    public bool Dead;
    public bool ReviveUsed;
    public CombatFighter Combat = new();
    public string WaitingPortal = "";
    public string InstanceId = "";
    public LanDungeonRoom? DungeonRoom;
    public readonly HashSet<string> DroppingThrough = new();
    public bool AirJumpAvailable = true;
    public int Facing = 1;
    public float AimAngle;
    public bool Aiming;
    public bool Fire;
    public double NextShot;
    public long LastSequence = -1;
    public float DashRemaining;
    public int DashDirection = 1, DashUses, QueuedDashDirection;
    public bool DashQueued;
    public double DashReadyAt, DashChainUntil = -1;
    public long DashSequence;
    public double LastInput, LastChat = -10, LastEquip = -10;
}

// All methods are called under the server world's lock. Simulation consumes input at a fixed rate.
public sealed partial class LobbyWorld
{
    readonly Dictionary<string, LobbyPlayer> players = new();
    readonly List<LanEvent> shots = new();
    long shotId;
    public IReadOnlyList<LanEvent> Shots => shots;
    public long TickNumber { get; private set; }
    public double Time { get; private set; }
    public bool DashMotionThisTick { get; private set; }
    public IReadOnlyCollection<LobbyPlayer> Players => players.Values;
    public LobbyPlayer? Find(string id) => players.GetValueOrDefault(id);

    public (LobbyPlayer? Player, string Error) Join(string id, LanProfile profile)
    {
        if (players.Values.Any(x => string.Equals(x.Profile.nickname, profile.nickname, StringComparison.OrdinalIgnoreCase)))
            return (null, "이미 접속 중인 계정입니다.");
        int channel = Enumerable.Range(1, LanRules.MaxChannels).FirstOrDefault(c => players.Values.Count(p => p.Channel == c) < LanRules.ChannelCapacity);
        if (channel == 0) return (null, "모든 마을 채널이 가득 찼습니다.");
        var player = new LobbyPlayer { Id = id, Profile = profile, Channel = channel, LastInput = Time,
            X = LanRules.TownSpawnX + players.Values.Count(p => p.Channel == channel) % 5 * .65f };
        players.Add(id, player);
        PartyRevision++;
        return (player, "");
    }

    public void Leave(string id)
    {
        RemoveApplication(id);
        LeaveParty(id);
        if (players.Remove(id)) PartyRevision++;
    }

    public bool Input(string id, LanCommand cmd)
    {
        if (!players.TryGetValue(id, out var p) || !float.IsFinite(cmd.move) || !float.IsFinite(cmd.aimAngle) || cmd.sequence <= p.LastSequence || cmd.sequence < 0) return false;
        p.LastSequence = cmd.sequence;
        p.LastInput = Time;
        if (p.Dead || p.WaitingPortal.Length > 0) { ResetMotion(p); return true; }
        p.Move = Math.Clamp(cmd.move, -1, 1);
        p.Jump |= cmd.jump;
        p.Drop |= cmd.drop;
        p.AimAngle = Math.Clamp(cmd.aimAngle, -180, 180);
        p.Aiming = cmd.aiming;
        p.Fire = cmd.fire && cmd.aiming;
        p.LastInput = Time;
        if (cmd.dash) RequestDash(p);
        return true;
    }

    public string ChangeChannel(string id, int channel)
    {
        var p = Find(id);
        if (p == null) return "로그인이 필요합니다.";
        if (channel < 1 || channel > LanRules.MaxChannels) return "없는 채널입니다.";
        if (p.Channel == channel) return "";
        if (memberships.ContainsKey(id)) return "파티에서 탈퇴한 뒤 채널을 이동하세요.";
        if (players.Values.Count(x => x.Channel == channel) >= LanRules.ChannelCapacity) return "해당 채널이 가득 찼습니다.";
        if (RemoveApplication(id)) NotifyParty(id, "채널을 이동하여 참가 신청을 취소했습니다.");
        p.Channel = channel; p.Move = 0; p.Jump = false; p.X = LanRules.TownSpawnX; p.Y = 0; p.VelocityY = 0;
        p.AirJumpAvailable = true;
        p.Fire = false; p.Aiming = false;
        p.DashRemaining = 0; p.DashQueued = false; p.DashUses = 0; p.DashChainUntil = -1;
        // Keep DashReadyAt: changing channel cannot refresh the cooldown.
        PartyRevision++;
        return "";
    }

    public LanEvent? Chat(string id, string? text)
    {
        var p = Find(id);
        if (p == null || Time - p.LastChat < 0.75 || string.IsNullOrWhiteSpace(text)) return null;
        text = new string(text.Where(c => !char.IsControl(c)).Take(160).ToArray()).Trim();
        if (text.Length == 0) return null;
        p.LastChat = Time;
        return new LanEvent { op = "chat", sender = p.Profile.nickname, text = text, channel = p.Channel, instanceId = p.InstanceId };
    }

    public void Tick(float dt)
    {
        if (dt <= 0 || dt > 0.1f) throw new ArgumentOutOfRangeException(nameof(dt));
        Time += dt; TickNumber++;
        DashMotionThisTick = false;
        shots.Clear();
        foreach (var p in players.Values)
        {
            if (p.Dead || p.WaitingPortal.Length > 0) { ResetMotion(p); continue; }
            if (Time - p.LastInput > 0.5) { p.Move = 0; p.Jump = p.Drop = false; p.Aiming = false; p.Fire = false; p.DashQueued = false; }
            TickHorizontal(p, dt);
            if (Math.Abs(p.Move) > 0.01) p.Facing = p.Move > 0 ? 1 : -1;
            if (p.Aiming) p.Facing = LanAim.Facing(p.AimAngle, p.Facing);
            if (p.DungeonRoom != null) TickDungeonVertical(p, dt);
            else
            {
                if (p.Jump)
                {
                    if (p.Y <= 0) p.VelocityY = LanRules.JumpSpeed;
                    else if (p.AirJumpAvailable)
                    { p.VelocityY = LanRules.AirJumpSpeed; p.AirJumpAvailable = false; }
                }
                p.Jump = p.Drop = false;
                p.VelocityY -= LanRules.Gravity * dt;
                p.Y = Math.Max(0, p.Y + p.VelocityY * dt);
                if (p.Y <= 0) { p.VelocityY = 0; p.AirJumpAvailable = true; }
            }
            if (p.DungeonRoom == null && p.Fire && Time + .00001 >= p.NextShot)
            {
                var weapon = p.Profile.items.FirstOrDefault(i => i.id == p.Profile.activeItemId);
                if (weapon == null || !float.IsFinite(LanShooting.Interval(weapon.family))) continue;
                // No catch-up bursts, even if a client floods inputs or switches weapons between ticks.
                p.NextShot = Time + LanShooting.Interval(weapon.family);
                shots.Add(new LanEvent { op = "shot", channel = p.Channel, instanceId = p.InstanceId, tick = TickNumber,
                    shot = new LanShot { id = ++shotId, shooter = p.Profile.nickname, family = weapon.family, tier = weapon.tier,
                        facing = p.Facing, x = p.X, y = p.Y + LanShooting.AimHeight, angle = p.AimAngle } });
            }
        }
        TickDungeonCombat(dt);
        TickPortalGroups();
    }

    public LanEvent Snapshot(int channel, string instanceId = "") => new()
    {
        op = "snapshot", tick = TickNumber, channel = channel, instanceId = instanceId,
        channelCounts = Enumerable.Range(1, LanRules.MaxChannels).Select(c => players.Values.Count(p => p.Channel == c)).ToArray(),
        actors = players.Values.Where(p => p.Channel == channel && p.InstanceId == instanceId).Select(p =>
        {
            var weapon = p.Profile.items.FirstOrDefault(i => i.id == p.Profile.activeItemId);
            return new LanActor { nickname = p.Profile.nickname, x = p.X, y = p.Y, facing = p.Facing,
                body = p.Profile.body, weaponFamily = weapon?.family ?? (int)WeaponFamily.Melee,
                weaponTier = weapon?.tier ?? 1, weaponEnhance = weapon?.enhance ?? 0,
                aimAngle = p.AimAngle, aiming = p.Aiming,
                grounded = p.DungeonRoom == null ? p.Y <= 0 : p.VelocityY <= 0 && OnDungeonGround(p),
                dead = p.Dead, portalId = p.WaitingPortal,
                hp = p.Combat.Health, reviveUsed = p.ReviveUsed,
                dashRemaining = p.DashRemaining, dashDirection = p.DashDirection, dashSequence = p.DashSequence,
                dashCooldown = (float)Math.Max(0, p.DashReadyAt - Time),
                dashChainWindow = p.DashUses == 1 ? (float)Math.Max(0, p.DashChainUntil - Time) : 0 };
        }).ToArray()
    };
}
