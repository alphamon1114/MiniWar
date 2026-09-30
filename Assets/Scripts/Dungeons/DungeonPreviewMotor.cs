using MiniWar.Online;
using System.Collections.Generic;
using UnityEngine;

namespace MiniWar.Dungeons
{
    // Local authoring preview only. Online movement remains authoritative in LobbyWorld.
    public sealed class DungeonPreviewMotor
    {
        public Vector2 Position { get; private set; }
        public float VerticalSpeed { get; private set; }
        public float HorizontalSpeed { get; private set; }
        public bool Grounded { get; private set; }
        public int Facing { get; private set; } = 1;
        public bool Dashing => remaining > 0;
        public float Cooldown => Mathf.Max(0, readyAt - now);
        public bool CanChain => uses == 1 && now <= chainUntil;
        float now, remaining, readyAt, chainUntil;
        int uses, direction = 1, queued;
        const float HalfWidth = .3f, Height = 2.3f, Epsilon = .0001f;
        readonly DungeonLayout layout;
        readonly HashSet<string> droppingThrough = new HashSet<string>();
        public bool CanDrop
        {
            get
            {
                if (!Grounded) return false;
                bool platform = false;
                foreach (var floor in layout.floors)
                {
                    if (!Supports(floor)) continue;
                    if (!floor.oneWay) return false;
                    platform = true;
                }
                return platform;
            }
        }
        bool Supports(DungeonFloor floor) => Position.x + HalfWidth > floor.rect.xMin
            && Position.x - HalfWidth < floor.rect.xMax && Mathf.Abs(Position.y - floor.rect.yMax) < .06f;

        public DungeonPreviewMotor(DungeonLayout layout) { this.layout = layout; Reset(); }

        public void Reset(Vector2? spawn = null)
        {
            Position = spawn ?? layout.entrance; VerticalSpeed = HorizontalSpeed = remaining = readyAt = chainUntil = now = 0;
            uses = queued = 0; Facing = direction = 1; Grounded = layout.HasSupport(Position);
            droppingThrough.Clear();
        }

        public void Step(float dt, float move, bool jump, bool dash, bool drop = false)
        {
            if (dt <= 0) return;
            move = Mathf.Clamp(move, -1, 1);
            if (move != 0) Facing = move < 0 ? -1 : 1;
            if (dash)
            {
                if (uses == 1 && now <= chainUntil)
                {
                    uses = 2;
                    if (Dashing) queued = Facing;
                    else BeginDash(Facing);
                }
                else if (!Dashing && now >= readyAt) { uses = 1; chainUntil = now + LanRules.DashChainWindow; BeginDash(Facing); }
            }
            if (drop && CanDrop)
            {
                // Ignore this support level only; a lower platform must still catch the player.
                foreach (var floor in layout.floors)
                    if (floor.oneWay && Mathf.Abs(Position.y - floor.rect.yMax) < .06f) droppingThrough.Add(floor.id);
                VerticalSpeed = -2; Grounded = false;
            }
            else if (jump && !drop && Grounded)
            { VerticalSpeed = Mathf.Sqrt(2 * LanRules.Gravity * layout.jumpHeight); Grounded = false; }
            float left = dt, travel = 0;
            // Split at dash boundaries so distance is independent of FixedUpdate frequency.
            while (left > .000001f)
            {
                float step = Dashing ? Mathf.Min(left, remaining) : left;
                travel += (Dashing ? direction * LanRules.DashSpeed : move * LanRules.WalkSpeed) * step;
                now += step; left -= step;
                remaining = Mathf.Max(0, remaining - step);
                if (!Dashing && queued != 0) { int next = queued; queued = 0; BeginDash(next); }
            }
            HorizontalSpeed = travel / Mathf.Max(dt, .000001f);
            VerticalSpeed -= LanRules.Gravity * dt;
            Move(new Vector2(travel, VerticalSpeed * dt));
            droppingThrough.RemoveWhere(id => !layout.floors.Exists(f => f.id == id && Position.y >= f.rect.yMax - .06f));
            if (Position.y < layout.bounds.yMin - 3) Reset();
        }

        void BeginDash(int facing) { direction = facing; remaining = LanRules.DashDuration; readyAt = now + LanRules.DashCooldown; }

        void Move(Vector2 delta)
        {
            var p = Position;
            float dx = delta.x;
            foreach (var floor in layout.floors)
            {
                if (floor.oneWay) continue;
                var r = floor.rect;
                if (p.y + Height <= r.yMin + Epsilon || p.y >= r.yMax - Epsilon) continue;
                if (dx > 0 && p.x + HalfWidth <= r.xMin + Epsilon) dx = Mathf.Min(dx, r.xMin - p.x - HalfWidth);
                if (dx < 0 && p.x - HalfWidth >= r.xMax - Epsilon) dx = Mathf.Max(dx, r.xMax - p.x + HalfWidth);
            }
            p.x = Mathf.Clamp(p.x + dx, layout.bounds.xMin + HalfWidth, layout.bounds.xMax - HalfWidth);
            float dy = delta.y;
            Grounded = false;
            foreach (var floor in layout.floors)
            {
                if (droppingThrough.Contains(floor.id)) continue;
                var r = floor.rect;
                if (p.x + HalfWidth <= r.xMin + Epsilon || p.x - HalfWidth >= r.xMax - Epsilon) continue;
                if (dy <= 0 && p.y >= r.yMax - Epsilon && p.y + dy <= r.yMax)
                { dy = r.yMax - p.y; Grounded = true; VerticalSpeed = 0; }
                if (!floor.oneWay && dy > 0 && p.y + Height <= r.yMin + Epsilon && p.y + Height + dy >= r.yMin)
                { dy = r.yMin - p.y - Height; VerticalSpeed = 0; }
            }
            p.y += dy; Position = p;
        }
    }
}
