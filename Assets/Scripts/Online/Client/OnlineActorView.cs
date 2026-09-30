using UnityEngine;

namespace MiniWar.Online
{
    public sealed class OnlineActorView : MonoBehaviour
    {
        OnlineVisuals visuals;
        CharacterRig rig;
        LanActor state;
        int renderedBody = -1;
        float receivedAt, speed, verticalSpeed, smoothSpeed, cycle;
        float fastFollowUntil, trailAt;
        DashAfterimages dashTrail;

        public CharacterRig Rig => rig;
        public bool IsDashing => state != null && state.dashRemaining > Time.unscaledTime - receivedAt;
        public float PositionFollowRate => Time.unscaledTime < fastFollowUntil ? 45 : 18;
        public long DashSequence => state != null ? state.dashSequence : 0;
        public string DashStatus
        {
            get
            {
                if (state == null) return "";
                float elapsed = Time.unscaledTime - receivedAt;
                float chain = Mathf.Max(0, state.dashChainWindow - elapsed);
                float cooldown = Mathf.Max(0, state.dashCooldown - elapsed);
                return chain > 0 ? "Shift · 추가 대시 " + chain.ToString("0.0") + "초"
                    : cooldown > 0 ? "Shift 대시 · " + cooldown.ToString("0.0") + "초"
                    : "Shift 대시 · 준비 완료";
            }
        }
        public void Initialize(OnlineVisuals library) { visuals = library; }
        public Vector3 PlayShot(LanShot shot)
        {
            if (rig == null || state == null) return transform.position + Vector3.up * LanShooting.AimHeight;
            state.aiming = true; state.aimAngle = shot.angle; state.facing = shot.facing;
            state.weaponFamily = shot.family; state.weaponTier = shot.tier;
            rig.SetWeapon(visuals.Weapon(shot.family, shot.tier), OnlineVisuals.WeaponWidths[shot.family]);
            rig.Pose(cycle, smoothSpeed, state.y > .025f, verticalSpeed, state.facing, true, shot.angle, shot.family, IsDashing);
            Vector3 muzzle = rig.MuzzlePosition;
            rig.Recoil();
            return muzzle;
        }

        public void Apply(LanActor actor)
        {
            float now = Time.unscaledTime;
            if (state != null && now > receivedAt + .001f)
            {
                float dt = now - receivedAt;
                float limit = actor.dashRemaining > 0 || state.dashRemaining > 0 || actor.dashSequence != state.dashSequence
                    ? LanRules.DashSpeed : LanRules.WalkSpeed;
                speed = Mathf.Clamp((actor.x - state.x) / dt, -limit, limit);
                verticalSpeed = Mathf.Clamp((actor.y - state.y) / dt, -20, 20);
                if (Mathf.Abs(actor.x - state.x) > limit * dt * 2 + .5f) speed = 0;
            }
            if (actor.dashRemaining > 0) fastFollowUntil = now + actor.dashRemaining + .12f;
            state = actor; receivedAt = now;
            if (renderedBody != actor.body)
            {
                if (rig != null) Destroy(rig.gameObject);
                var prefab = visuals.RigPrefab(actor.body);
                if (prefab == null) { Debug.LogError("Character rig prefab missing. Run MiniWar > Characters > Build part sprites and rigs."); return; }
                rig = Instantiate(prefab, transform).GetComponent<CharacterRig>();
                renderedBody = actor.body;
            }
            rig.SetWeapon(visuals.Weapon(actor.weaponFamily, actor.weaponTier), OnlineVisuals.WeaponWidths[Mathf.Clamp(actor.weaponFamily, 0, 6)]);
        }

        void LateUpdate()
        {
            if (state == null || rig == null || !rig.proceduralAnimation) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            rig.AdvanceEffects(dt);
            float targetSpeed = Time.unscaledTime - receivedAt > .35f ? 0 : speed;
            smoothSpeed = Mathf.Lerp(smoothSpeed, targetSpeed, 1 - Mathf.Exp(-14 * dt));
            float walkRate = rig.bodyFrames != null ? rig.bodyFrames.walkCycleRadiansPerSecond : 10;
            cycle += dt * Mathf.Lerp(2, walkRate, Mathf.Clamp01(Mathf.Abs(smoothSpeed) / LanRules.WalkSpeed));
            bool dashing = IsDashing;
            rig.Pose(cycle, smoothSpeed, state.y > .025f, verticalSpeed, state.facing, state.aiming, state.aimAngle, state.weaponFamily, dashing);
            if (dashTrail != null) dashTrail.Advance(dt);
            if (dashing && Time.unscaledTime >= trailAt)
            {
                if (dashTrail == null) dashTrail = new DashAfterimages();
                dashTrail.Emit(rig);
                trailAt = Time.unscaledTime + .04f;
            }
        }

        void OnDestroy() { if (dashTrail != null) dashTrail.Dispose(); }
    }
}
