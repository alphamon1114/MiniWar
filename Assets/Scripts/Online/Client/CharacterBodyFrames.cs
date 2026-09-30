using UnityEngine;

namespace MiniWar.Online
{
    /// <summary>Connected body artwork; shoulder sockets follow each authored frame.</summary>
    public sealed class CharacterBodyFrames : ScriptableObject
    {
        public Sprite[] frames;
        public Vector2[] frontShoulders;
        public Vector2[] backShoulders;
        [Min(.01f)] public float walkCycleRadiansPerSecond = 10f;
        public int Select(float cycle, float speed, bool airborne, float verticalSpeed)
        {
            if (airborne) return verticalSpeed > 2 ? 13 : verticalSpeed < -2 ? 15 : 14;
            if (Mathf.Abs(speed) < .25f) return Mathf.FloorToInt(Mathf.Repeat(cycle, Mathf.PI * 2) / (Mathf.PI * .5f));
            float phase = Mathf.Repeat(speed < 0 ? -cycle : cycle, Mathf.PI * 2);
            return 4 + Mathf.Min(7, Mathf.FloorToInt(phase / (Mathf.PI * .25f)));
        }
    }
}
