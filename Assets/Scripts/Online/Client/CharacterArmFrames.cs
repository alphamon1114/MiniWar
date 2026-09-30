using UnityEngine;

namespace MiniWar.Online
{
    /// <summary>Each sprite contains both visible arms and hands, with elbows painted in place.</summary>
    public sealed class CharacterArmFrames : ScriptableObject
    {
        public Sprite[] frames;
        public Vector2[] grips;

        // 0..4 long-gun aim, 5 carry; 6..10 handgun aim, 11 raised handgun ready.
        public int Select(bool handgun, bool aiming, AimPose pose)
            => aiming ? (handgun ? 6 : 0) + (int)pose : handgun ? 11 : 5;

        public float Angle(bool handgun, bool aiming, AimPose pose)
            => aiming ? LanAim.LocalAngle(pose) : handgun ? 90 : 0;
    }
}
