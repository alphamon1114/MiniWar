using System;

namespace MiniWar.Online
{
    /// <summary>Town practice ballistics. No damage or inventory changes; dungeon combat has its own rules.</summary>
    public static class LanShooting
    {
        public const float AimHeight = 2.07f;
        public const float Lifetime = .8f;
        public static float Interval(int family)
        {
            switch ((WeaponFamily)family)
            {
                case WeaponFamily.Pistol: return .32f;
                case WeaponFamily.SubmachineGun: return .10f;
                case WeaponFamily.Shotgun: return .85f;
                case WeaponFamily.Rifle: return .16f;
                case WeaponFamily.SniperRifle: return 1.1f;
                case WeaponFamily.Revolver: return .55f;
                default: return float.PositiveInfinity;
            }
        }
        public static float Speed(int family) => family == (int)WeaponFamily.SniperRifle ? 36 : family == (int)WeaponFamily.Rifle ? 25 : 20;
        public static int Pellets(int family) => family == (int)WeaponFamily.Shotgun ? 5 : 1;
        public static float PelletAngle(LanShot shot, int pellet) => shot.angle + (Pellets(shot.family) == 1 ? 0 : (pellet - 2) * 2);
        public static void Position(LanShot shot, int pellet, float age, out float x, out float y)
        {
            double radians = PelletAngle(shot, pellet) * Math.PI / 180;
            float distance = Speed(shot.family) * Math.Max(0, Math.Min(age, Lifetime));
            x = shot.x + (float)Math.Cos(radians) * distance;
            y = shot.y + (float)Math.Sin(radians) * distance;
        }
    }
}
