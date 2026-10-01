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
        public bool CanAirJump => !Grounded && airJumpAvailable;
        bool airJumpAvailable = true;
        public int Facing { get; private set; } = 1;
        public bool Dashing => remaining > 0;
        public float Cooldown => Mathf.Max(0, readyAt - now);
        public bool CanChain => uses == 1 && now <= chainUntil;
        float now, remaining, readyAt, chainUntil;
        int uses, direction = 1, queued;
        readonly DungeonLayout layout;
        readonly LanDungeonRoom geometry;
        readonly HashSet<string> droppingThrough = new HashSet<string>();
        public bool CanDrop
        {
            get
            {
                if (!Grounded) return false;
                return LanDungeonPhysics.CanDrop(geometry,Position.x,Position.y);
            }
        }

        public DungeonPreviewMotor(DungeonLayout layout) { this.layout = layout; geometry=new LanDungeonRoom(); Reset(); }

        void SyncGeometry()
        {
            geometry.x=layout.bounds.x;geometry.y=layout.bounds.y;geometry.width=layout.bounds.width;geometry.height=layout.bounds.height;
            geometry.jumpHeight=layout.jumpHeight;geometry.airJumpHeight=layout.airJumpHeight;
            if(geometry.floors.Length!=layout.floors.Count)geometry.floors=new LanDungeonFloor[layout.floors.Count];
            for(int i=0;i<layout.floors.Count;i++)
            {
                var from=layout.floors[i];var to=geometry.floors[i]??(geometry.floors[i]=new LanDungeonFloor());
                to.id=from.id;to.x=from.rect.x;to.y=from.rect.y;to.width=from.rect.width;to.height=from.rect.height;to.oneWay=from.oneWay;
            }
        }

        public void Reset(Vector2? spawn = null)
        {
            SyncGeometry();
            Position = spawn ?? layout.entrance; VerticalSpeed = HorizontalSpeed = remaining = readyAt = chainUntil = now = 0;
            uses = queued = 0; Facing = direction = 1; Grounded = layout.HasSupport(Position);
            airJumpAvailable = true;
            droppingThrough.Clear();
        }

        public void Step(float dt, float move, bool jump, bool dash, bool drop = false)
        {
            if (dt <= 0) return;
            SyncGeometry();
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
                LanDungeonPhysics.Drop(geometry,Position.y,droppingThrough);
                VerticalSpeed = -2; Grounded = false;
            }
            else if (jump && !drop && Grounded)
            { VerticalSpeed = LanDungeonPhysics.JumpSpeed(geometry); Grounded = false; }
            else if (jump && !drop && CanAirJump)
            {
                // Replace falling/rising velocity so the second press gives a consistent lift.
                VerticalSpeed = Mathf.Sqrt(2 * LanRules.Gravity * layout.airJumpHeight);
                airJumpAvailable = false; droppingThrough.Clear();
            }
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
            float x=LanDungeonPhysics.MoveX(geometry,Position.x,Position.y,travel),y=Position.y,speed=VerticalSpeed;
            Grounded=LanDungeonPhysics.Vertical(geometry,x,ref y,ref speed,droppingThrough,dt);
            Position=new Vector2(x,y);VerticalSpeed=speed;
            if (Grounded) airJumpAvailable = true;
            if (Position.y < layout.bounds.yMin - 3) Reset();
        }

        void BeginDash(int facing) { direction = facing; remaining = LanRules.DashDuration; readyAt = now + LanRules.DashCooldown; }

    }
}
