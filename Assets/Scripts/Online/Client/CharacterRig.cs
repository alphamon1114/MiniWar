using UnityEngine;

namespace MiniWar.Online
{
    /// <summary>Two painted character layers: connected body and whole arm pair. Weapon stays separate.</summary>
    public sealed class CharacterRig : MonoBehaviour
    {
        public static readonly string[] PartNames = { "Head", "Torso", "Pelvis", "FrontUpperArm", "FrontForearm",
            "BackUpperArm", "BackForearm", "FrontThigh", "FrontShinBoot", "BackThigh", "BackShinBoot", "CoatTails" };

        [Tooltip("Turn off when an Animator or hand-authored joint poses should drive this rig.")]
        public bool proceduralAnimation = true;
        public Transform facingRoot, pelvis, torso, head, coat;
        public Transform frontUpperArm, frontForearm, backUpperArm, backForearm;
        public Transform frontThigh, frontShin, backThigh, backShin;
        public Transform weaponGrip;
        public SpriteRenderer weapon;
        public SpriteRenderer[] parts;
        public int body;
        public CharacterBodyFrames bodyFrames;
        public SpriteRenderer connectedBody;
        public CharacterArmFrames armFrames;
        public SpriteRenderer connectedArms;
        [Header("Authored proportions (1 preserves the drawing)")]
        [Range(.5f, 1.2f)] public float silhouetteWidth = 1f;
        [Range(.5f, 1.2f)] public float silhouetteHeight = 1f;
        [Range(.5f, 1f)] public float armThickness = 1f;
        Transform armShapePivot;
        public int CurrentArmFrame { get; private set; }
        public float upperArmLength = UpperArmLength, forearmLength = ForearmLength;
        [UnityEngine.Serialization.FormerlySerializedAs("compactArmPoses")]
        public bool useShoulderRelativePoses;
        [System.NonSerialized] public int bodyFrameOverride = -1;
        public int CurrentBodyFrame { get; private set; }
        float recoil, landingTime;
        bool wasAirborne;
        public bool IsHybrid => bodyFrames != null && connectedBody != null;
        public bool IsTwoPiece => IsHybrid && armFrames != null && connectedArms != null;
        public Vector3 MuzzlePosition => weapon.sprite == null ? weaponGrip.position
            : weapon.transform.TransformPoint(new Vector3(weapon.sprite.bounds.max.x, weapon.sprite.bounds.max.y * .6f, 0));
        public void Recoil() { recoil = 1; }
        public void AdvanceEffects(float dt) { recoil = Mathf.Max(0, recoil - dt * 10); landingTime = Mathf.Max(0, landingTime - dt); }

        public const float UpperArmLength = .40f, ForearmLength = .40f;
        public const float ThighLength = .74f, ShinLength = .76f;
        public const float StandingHipHeight = 1.51f, WalkingHipHeight = 1.45f;
        public const float AimHeight = StandingHipHeight + .56f;

        // Hand targets relative to the torso, in AimPose row order. Keep vertical shots in front of the face/body.
        static readonly Vector2[] AimGrips = {
            new Vector2(.47f,.91f), new Vector2(.48f,.77f), new Vector2(.48f,.51f),
            new Vector2(.42f,.26f), new Vector2(.34f,.17f)
        };
        // Relative to the shoulder. Keep the hands in front of the chest instead of tucked into it.
        static readonly Vector2[] ShoulderAimGrips = {
            new Vector2(.62f,.50f), new Vector2(.78f,.30f), new Vector2(.86f,-.08f),
            new Vector2(.79f,-.46f), new Vector2(.58f,-.68f)
        };

        static readonly Color[] PartColors = {
            new Color(1,.91f,.43f), new Color(.3f,.87f,1), new Color(.29f,.54f,.95f),
            new Color(.66f,.38f,1), new Color(.79f,.52f,1), new Color(1,.42f,.72f), new Color(1,.64f,.83f),
            new Color(.39f,.91f,.4f), new Color(.62f,1,.65f), new Color(1,.36f,.34f), new Color(1,.57f,.42f),
            new Color(.53f,.66f,.82f)
        };

        public void ShowPartColors(bool show)
        {
            for (int i = 0; i < parts.Length; i++) parts[i].color = show ? PartColors[i] : Color.white;
            if (connectedBody != null) connectedBody.color = show ? new Color(.65f,.85f,1) : Color.white;
            if (connectedArms != null) connectedArms.color = show ? new Color(1,.55f,.75f) : Color.white;
        }

        public void SetWeapon(Sprite sprite, float width)
        {
            weapon.sprite = sprite;
            weapon.enabled = sprite != null;
            if (sprite != null) weapon.transform.localScale = Vector3.one * (width / sprite.bounds.size.x);
        }

        static void Angle(Transform joint, float degrees) => joint.localRotation = Quaternion.Euler(0, 0, degrees);

        /// <summary>Solves in the joint parent's space, including mirrored roots and a leaning torso.</summary>
        public static void SolveLimb(Transform upper, Transform lower, Transform space, Vector2 end,
            float upperLength, float lowerLength, float bend)
        {
            Vector2 goal = upper.parent.InverseTransformPoint(space.TransformPoint(end));
            Vector2 delta = goal - (Vector2)upper.localPosition;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(upperLength - lowerLength) + .001f,
                upperLength + lowerLength - .001f);
            Vector2 forward = delta.sqrMagnitude > .000001f ? delta.normalized : Vector2.down;
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2 * distance);
            float height = Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - along * along));
            Vector2 elbow = forward * along + new Vector2(-forward.y, forward.x) * height * bend;
            Vector2 second = forward * distance - elbow;
            float firstAngle = Mathf.Atan2(elbow.y, elbow.x) * Mathf.Rad2Deg + 90;
            Angle(upper, firstAngle);
            lower.localPosition = new Vector3(0, -upperLength, 0);
            Angle(lower, Mathf.Atan2(second.y, second.x) * Mathf.Rad2Deg + 90 - firstAngle);
        }

        /// <param name="speed">Signed horizontal world speed; aiming and locomotion remain independent.</param>
        public void Pose(float cycle, float speed, bool airborne, float verticalSpeed, int facing,
            bool aiming, float worldAim, int weaponFamily, bool dashing = false)
        {
            facing = facing < 0 ? -1 : 1;
            facingRoot.localScale = IsTwoPiece ? new Vector3(facing * silhouetteWidth, silhouetteHeight, 1) : new Vector3(facing, 1, 1);
            if (IsHybrid) { PoseHybrid(cycle, speed, airborne, verticalSpeed, facing, aiming, worldAim, weaponFamily, dashing); return; }
            float move = Mathf.Clamp01(Mathf.Abs(speed) / LanRules.WalkSpeed);
            float travel = speed * facing < 0 ? -1 : 1;
            float stride = Mathf.Sin(cycle) * move * travel;
            float breathe = Mathf.Sin(cycle * .35f) * .014f * (1 - move);
            float bounce = Mathf.Abs(Mathf.Cos(cycle)) * .025f * move;
            float hipHeight = Mathf.Lerp(StandingHipHeight, WalkingHipHeight, move) + breathe + bounce;
            pelvis.localPosition = new Vector3(0, hipHeight, 0);
            Angle(pelvis, 0);
            Angle(torso, airborne ? -5 : -stride * 2 - move * 4);
            Angle(coat, stride * 9 + (airborne ? 13 : 0));

            if (airborne)
            {
                Angle(frontThigh, verticalSpeed > 0 ? 32 : 18); Angle(frontShin, -48);
                Angle(backThigh, -22); Angle(backShin, -30);
            }
            else
            {
                var frontFoot = new Vector2(.11f + stride * .44f, .03f + Mathf.Max(0, Mathf.Cos(cycle)) * move * .18f);
                var backFoot = new Vector2(-.11f - stride * .44f, .03f + Mathf.Max(0, -Mathf.Cos(cycle)) * move * .18f);
                SolveLimb(frontThigh, frontShin, facingRoot, frontFoot, ThighLength, ShinLength, 1);
                SolveLimb(backThigh, backShin, facingRoot, backFoot, ThighLength, ShinLength, 1);
            }

            bool handgun = LanRules.IsHandgun(weaponFamily);
            var aimPose = LanAim.Pose(worldAim, facing);
            float localAim = aiming ? LanAim.LocalAngle(aimPose)
                : handgun ? 72 : weaponFamily == (int)WeaponFamily.Melee ? -32 : 0;
            Angle(head, aiming ? Mathf.Clamp(localAim, -35, 35) * .13f : stride * 2);
            Vector2 direction = new Vector2(Mathf.Cos(localAim * Mathf.Deg2Rad), Mathf.Sin(localAim * Mathf.Deg2Rad));
            Vector2 grip = aiming ? (Vector2)facingRoot.InverseTransformPoint(torso.TransformPoint(AimGrips[(int)aimPose]))
                : handgun ? new Vector2(.34f, hipHeight + .60f)
                : new Vector2(.36f, hipHeight + .39f);
            SolveLimb(frontUpperArm, frontForearm, facingRoot, grip, UpperArmLength, ForearmLength, -1);
            Vector2 support = handgun ? grip + direction * .035f + Vector2.down * .025f
                : grip + direction * .28f + new Vector2(-direction.y, direction.x) * .13f;
            if (weaponFamily == (int)WeaponFamily.Melee)
                support = new Vector2(-.12f - stride * .16f, hipHeight + .12f);
            SolveLimb(backUpperArm, backForearm, facingRoot, support, UpperArmLength, ForearmLength, -1);

            // The grip is an actual child of the hand, so it cannot drift away when any arm joint rotates.
            weaponGrip.localRotation = Quaternion.identity;
            Vector3 muzzleWorld = facingRoot.TransformVector(direction);
            float desiredWorld = Mathf.Atan2(muzzleWorld.y, muzzleWorld.x) * Mathf.Rad2Deg;
            Vector3 currentWorld = weaponGrip.TransformVector(Vector3.right);
            float currentAngle = Mathf.Atan2(currentWorld.y, currentWorld.x) * Mathf.Rad2Deg;
            Angle(weaponGrip, Mathf.DeltaAngle(currentAngle, desiredWorld) * facing);
        }

        void PoseHybrid(float cycle, float speed, bool airborne, float verticalSpeed, int facing, bool aiming, float worldAim, int family, bool dashing)
        {
            if (wasAirborne && !airborne) landingTime = .10f;
            wasAirborne = airborne;
            int frame = bodyFrameOverride >= 0 ? Mathf.Clamp(bodyFrameOverride, 0, 15)
                : !airborne && (dashing || landingTime > 0) ? 12 : bodyFrames.Select(cycle, speed * facing, airborne, verticalSpeed);
            CurrentBodyFrame = frame;
            connectedBody.sprite = bodyFrames.frames[frame];
            if (IsTwoPiece)
            {
                bool isHandgun = LanRules.IsHandgun(family);
                var pose = LanAim.Pose(worldAim, facing);
                CurrentArmFrame = armFrames.Select(isHandgun, aiming, pose);
                connectedArms.sprite = armFrames.frames[CurrentArmFrame];
                float angle = armFrames.Angle(isHandgun, aiming, pose);
                var shotDirection = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                // A tiny translation stays inside the shoulder overlap. No elbow/wrist IK or arm stretching.
                if (armShapePivot == null)
                {
                    armShapePivot = facingRoot.Find("Arm silhouette pivot");
                    if (armShapePivot == null)
                    {
                        armShapePivot = new GameObject("Arm silhouette pivot").transform;
                        armShapePivot.SetParent(facingRoot, false);
                    }
                    connectedArms.transform.SetParent(armShapePivot, false);
                }
                // Compress across the painted arm pose, not along its reach. Both shoulder and
                // grip follow this same transform, preserving attachment through all five poses.
                armShapePivot.localPosition = bodyFrames.frontShoulders[frame] - shotDirection * (.018f * recoil);
                Angle(armShapePivot, angle);
                armShapePivot.localScale = new Vector3(1, armThickness, 1);
                connectedArms.transform.localPosition = Vector3.zero;
                Angle(connectedArms.transform, -angle);
                weaponGrip.localPosition = armFrames.grips[CurrentArmFrame];
                // Nonuniform silhouette scaling must not change the five-way weapon direction.
                Vector3 aimWorld = transform.TransformVector(new Vector3(shotDirection.x * facing, shotDirection.y, 0));
                Vector3 gripDirection = connectedArms.transform.InverseTransformVector(aimWorld);
                Angle(weaponGrip, Mathf.Atan2(gripDirection.y, gripDirection.x) * Mathf.Rad2Deg);
                return;
            }
            // Only the arm hierarchy moves. Head, neck, waist and legs stay joined within the selected sprite.
            frontUpperArm.localPosition = bodyFrames.frontShoulders[frame];
            backUpperArm.localPosition = bodyFrames.backShoulders[frame];
            bool handgun = LanRules.IsHandgun(family);
            var aimPose = LanAim.Pose(worldAim, facing);
            float localAim = aiming ? LanAim.LocalAngle(aimPose) : handgun ? 72 : family == (int)WeaponFamily.Melee ? -32 : 0;
            var direction = new Vector2(Mathf.Cos(localAim * Mathf.Deg2Rad), Mathf.Sin(localAim * Mathf.Deg2Rad));
            Vector2 shoulder = frontUpperArm.localPosition;
            Vector2 grip = aiming ? shoulder + AimGrips[(int)aimPose] - new Vector2(-.19f,.51f)
                : shoulder + (handgun ? new Vector2(.53f,.09f) : new Vector2(.55f,-.12f));
            if (useShoulderRelativePoses)
                grip = shoulder + (aiming ? ShoulderAimGrips[(int)aimPose]
                    : handgun ? new Vector2(.66f,.16f) : new Vector2(.62f,-.55f));
            grip -= direction * (.07f * recoil);
            weaponGrip.localPosition = Vector3.down * forearmLength;
            SolveLimb(frontUpperArm, frontForearm, facingRoot, grip, upperArmLength, forearmLength, -1);
            // Solve the supporting hand from the actual gripping hand after reach clamping.
            grip = facingRoot.InverseTransformPoint(weaponGrip.position);
            float supportReach = useShoulderRelativePoses && aiming && aimPose == AimPose.Down ? .16f : .28f;
            Vector2 support = handgun ? grip + direction * .035f + Vector2.down * .025f
                : grip + direction * supportReach + new Vector2(-direction.y, direction.x) * .13f;
            if (family == (int)WeaponFamily.Melee) support = shoulder + new Vector2(.08f,-.94f);
            SolveLimb(backUpperArm, backForearm, facingRoot, support, upperArmLength, forearmLength, -1);
            weaponGrip.localRotation = Quaternion.identity;
            Vector3 desired = facingRoot.TransformVector(direction), current = weaponGrip.TransformVector(Vector3.right);
            Angle(weaponGrip, Mathf.DeltaAngle(Mathf.Atan2(current.y,current.x) * Mathf.Rad2Deg,
                Mathf.Atan2(desired.y,desired.x) * Mathf.Rad2Deg) * facing);
        }
    }
}
