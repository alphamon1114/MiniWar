using System;

namespace MiniWar.Online
{
    // Stable animation row order, mirrored horizontally for left-facing characters.
    public enum AimPose { Up, UpDiagonal, Forward, DownDiagonal, Down }

    public static class LanAim
    {
        public const float HalfSectorDegrees = 22.5f;

        public static float Normalize(float angle)
        {
            if (float.IsNaN(angle) || float.IsInfinity(angle)) return 0;
            angle %= 360;
            if (angle > 180) angle -= 360;
            if (angle < -180) angle += 360;
            return angle;
        }

        public static AimPose Pose(float worldAngle, int facing)
        {
            float local = Normalize(facing < 0 ? 180 - Normalize(worldAngle) : worldAngle);
            if (local >= 67.5f) return AimPose.Up;
            if (local >= HalfSectorDegrees) return AimPose.UpDiagonal;
            if (local > -HalfSectorDegrees) return AimPose.Forward;
            if (local > -67.5f) return AimPose.DownDiagonal;
            return AimPose.Down;
        }

        public static float LocalAngle(AimPose pose) => 90 - (int)pose * 45;
        public static float WorldAngle(AimPose pose, int facing) => Normalize(facing < 0 ? 180 - LocalAngle(pose) : LocalAngle(pose));

        // Vertical poses keep their facing so the body does not flip when the cursor crosses straight up/down.
        public static int Facing(float worldAngle, int previous)
        {
            float angle = Math.Abs(Normalize(worldAngle));
            if (angle >= 67.5f && angle <= 112.5f) return previous < 0 ? -1 : 1;
            return angle > 90 ? -1 : 1;
        }
    }
}
