using MiniWar.Online;

namespace MiniWar.Server;

public sealed class LobbyPlayer
{
    public required string Id;
    public required LanProfile Profile;
    public int Channel;
    public float X = LanRules.TownSpawnX, Y, VelocityY, Move;
    public bool Jump;
    public int Facing = 1;
    public float AimAngle;
    public bool Aiming;
    public long LastSequence = -1;
    public double LastInput, LastChat = -10, LastEquip = -10;
}

// All methods are called under the server world's lock. Simulation consumes input at a fixed rate.
public sealed class LobbyWorld
{
    readonly Dictionary<string, LobbyPlayer> players = new();
    public long TickNumber { get; private set; }
    public double Time { get; private set; }
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
        return (player, "");
    }

    public void Leave(string id) => players.Remove(id);

    public bool Input(string id, LanCommand cmd)
    {
        if (!players.TryGetValue(id, out var p) || !float.IsFinite(cmd.move) || !float.IsFinite(cmd.aimAngle) || cmd.sequence <= p.LastSequence || cmd.sequence < 0) return false;
        p.LastSequence = cmd.sequence;
        p.Move = Math.Clamp(cmd.move, -1, 1);
        p.Jump |= cmd.jump;
        p.AimAngle = Math.Clamp(cmd.aimAngle, -180, 180);
        p.Aiming = cmd.aiming;
        p.LastInput = Time;
        return true;
    }

    public string ChangeChannel(string id, int channel)
    {
        var p = Find(id);
        if (p == null) return "로그인이 필요합니다.";
        if (channel < 1 || channel > LanRules.MaxChannels) return "없는 채널입니다.";
        if (p.Channel == channel) return "";
        if (players.Values.Count(x => x.Channel == channel) >= LanRules.ChannelCapacity) return "해당 채널이 가득 찼습니다.";
        p.Channel = channel; p.Move = 0; p.Jump = false; p.X = LanRules.TownSpawnX; p.Y = 0; p.VelocityY = 0;
        return "";
    }

    public LanEvent? Chat(string id, string? text)
    {
        var p = Find(id);
        if (p == null || Time - p.LastChat < 0.75 || string.IsNullOrWhiteSpace(text)) return null;
        text = new string(text.Where(c => !char.IsControl(c)).Take(160).ToArray()).Trim();
        if (text.Length == 0) return null;
        p.LastChat = Time;
        return new LanEvent { op = "chat", sender = p.Profile.nickname, text = text, channel = p.Channel };
    }

    public void Tick(float dt)
    {
        if (dt <= 0 || dt > 0.1f) throw new ArgumentOutOfRangeException(nameof(dt));
        Time += dt; TickNumber++;
        foreach (var p in players.Values)
        {
            if (Time - p.LastInput > 0.5) { p.Move = 0; p.Aiming = false; }
            p.X = Math.Clamp(p.X + p.Move * LanRules.WalkSpeed * dt, 0.5f, LanRules.TownWidth - 0.5f);
            if (Math.Abs(p.Move) > 0.01) p.Facing = p.Move > 0 ? 1 : -1;
            if (p.Aiming) p.Facing = Math.Abs(p.AimAngle) > 90 ? -1 : 1;
            if (p.Jump && p.Y <= 0) p.VelocityY = LanRules.JumpSpeed;
            p.Jump = false;
            p.VelocityY -= LanRules.Gravity * dt;
            p.Y = Math.Max(0, p.Y + p.VelocityY * dt);
            if (p.Y <= 0) p.VelocityY = 0;
        }
    }

    public LanEvent Snapshot(int channel) => new()
    {
        op = "snapshot", tick = TickNumber, channel = channel,
        channelCounts = Enumerable.Range(1, LanRules.MaxChannels).Select(c => players.Values.Count(p => p.Channel == c)).ToArray(),
        actors = players.Values.Where(p => p.Channel == channel).Select(p =>
        {
            var weapon = p.Profile.items.FirstOrDefault(i => i.id == p.Profile.activeItemId);
            return new LanActor { nickname = p.Profile.nickname, x = p.X, y = p.Y, facing = p.Facing,
                body = p.Profile.body, weaponFamily = weapon?.family ?? (int)WeaponFamily.Melee,
                weaponTier = weapon?.tier ?? 1, weaponEnhance = weapon?.enhance ?? 0,
                aimAngle = p.AimAngle, aiming = p.Aiming };
        }).ToArray()
    };
}
