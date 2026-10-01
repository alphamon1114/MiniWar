using MiniWar.Online;

namespace MiniWar.Server;

public sealed partial class LobbyWorld
{
    const double DashEpsilon = .000001;

    void RequestDash(LobbyPlayer p)
    {
        bool chain = p.DashUses == 1 && Time <= p.DashChainUntil + DashEpsilon;
        bool ready = Time + DashEpsilon >= p.DashReadyAt && p.DashRemaining <= 0;
        if (p.DashQueued || (!chain && !ready)) return;
        int direction = Math.Abs(p.Move) > .01f ? (p.Move > 0 ? 1 : -1)
            : p.Aiming ? LanAim.Facing(p.AimAngle, p.Facing) : p.Facing;
        if (chain)
        {
            p.DashUses = 2;
            p.DashChainUntil = -1;
            // A very fast second tap is consumed once and starts after the current dash.
            if (p.DashRemaining > 0)
            {
                p.DashQueued = true;
                p.QueuedDashDirection = direction;
                return;
            }
        }
        else
        {
            p.DashUses = 1;
            p.DashChainUntil = Time + LanRules.DashChainWindow;
        }
        BeginDash(p, direction, Time);
    }

    static void BeginDash(LobbyPlayer p, int direction, double at)
    {
        p.DashDirection = direction < 0 ? -1 : 1;
        p.DashRemaining = LanRules.DashDuration;
        p.DashReadyAt = at + LanRules.DashCooldown;
        p.DashSequence++;
        if (!p.Aiming) p.Facing = p.DashDirection;
    }

    void TickHorizontal(LobbyPlayer p, float dt)
    {
        float remaining = dt;
        double cursor = Time - dt;
        while (remaining > .000001f)
        {
            if (p.DashRemaining <= .000001f && p.DashQueued)
            {
                p.DashQueued = false;
                BeginDash(p, p.QueuedDashDirection, cursor);
            }
            if (p.DashRemaining <= .000001f)
            {
                p.DashRemaining = 0;
                p.X = MoveX(p, p.Move * LanRules.WalkSpeed * remaining);
                break;
            }
            DashMotionThisTick = true;
            float step = Math.Min(remaining, p.DashRemaining);
            float wanted = p.X + p.DashDirection * LanRules.DashSpeed * step;
            p.X = MoveX(p, wanted - p.X);
            p.DashRemaining = Math.Max(0, p.DashRemaining - step);
            remaining -= step;
            cursor += step;
            if (p.X != wanted) p.DashRemaining = 0;
        }
    }
}
