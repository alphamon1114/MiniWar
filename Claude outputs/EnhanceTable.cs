using UnityEngine;

namespace MiniWar.Data
{
    /// <summary>강화 실패가 무엇을 하는가.</summary>
    public enum EnhanceFailure
    {
        /// <summary>단계 유지. 돈만 날아간다.</summary>
        Keep = 0,
        /// <summary>한 단계 하락.</summary>
        Drop = 1,
        /// <summary>하락 또는 파괴.</summary>
        DropOrDestroy = 2,
    }

    /// <summary>강화 1회 시도의 결과.</summary>
    public enum EnhanceResult
    {
        Success = 0,
        Kept = 1,
        Dropped = 2,
        Destroyed = 3,
    }

    /// <summary>
    /// 강화 곡선. +0 ~ +15.
    ///
    /// <b>성공률은 모든 총기가 같다. 다른 것은 비용뿐이다.</b>
    /// 그래서 "싼 총으로 감을 익히고 비싼 총에 건다"는 순서가 자연스럽게 생긴다.
    ///
    /// 벽은 12강에 선다 — 7%. 11강까지가 현실적인 적정 스펙이고,
    /// 13강부터는 작정하고 파는 구역, 15강은 엔드스펙이다.
    ///
    /// 올리는 것은 <b>위력 하나</b>다. 장전비는 그대로이므로 강화는 곧
    /// 탄 한 발당 마진을 올린다 — 이 게임에서 강화는 화력이자 경제다.
    ///
    /// <para>
    /// 상수로 박아둔 이유: 이 표는 나중에 <b>서버로 올라간다</b>. 확률을 클라이언트가
    /// 굴리면 15강 7%는 아무 의미가 없다. ScriptableObject로 빼두면 인스펙터에서
    /// 고칠 수 있게 되고, 그건 곧 클라이언트가 값을 안다는 뜻이다.
    /// 지금은 싱글이라 여기 있지만 옮길 것을 전제로 한 자리에 모아 둔다.
    /// </para>
    /// </summary>
    public static class EnhanceTable
    {
        public const int MaxLevel = 15;

        /// <summary>이 단계까지는 실패해도 안 떨어진다.</summary>
        public const int SafeUntil = 8;

        /// <summary>이 단계까지는 떨어지기만 한다. 넘으면 파괴가 붙는다.</summary>
        public const int DropUntil = 11;

        /// <summary>파괴 구간에서 실패했을 때 파괴로 갈 확률. 나머지는 하락.</summary>
        public const float DestroyShare = 0.40f;

        // index = 목표 단계. [0]은 쓰지 않는다.
        static readonly float[] Rate =
        {
            0f,
            1.00f, 1.00f, 1.00f, 0.95f, 0.90f,   // +1 ~ +5
            0.85f, 0.78f, 0.70f, 0.55f, 0.40f,   // +6 ~ +10
            0.20f, 0.07f, 0.04f, 0.02f, 0.01f,   // +11 ~ +15
        };

        // 위력 배수. index = 현재 단계.
        static readonly float[] Power =
        {
            1.00f,
            1.06f, 1.12f, 1.18f, 1.24f, 1.30f,   // +1 ~ +5
            1.36f, 1.42f, 1.48f, 1.57f, 1.66f,   // +6 ~ +10
            1.75f, 1.92f, 2.12f, 2.34f, 2.60f,   // +11 ~ +15
        };

        // 1등급 기준 시도 비용. index = 목표 단계.
        static readonly int[] BaseCost =
        {
            0,
            40, 60, 80, 110, 150,
            200, 270, 360, 480, 620,
            800, 1000, 1250, 1550, 1900,
        };

        // 등급 배수. index = 등급(1~3).
        static readonly float[] TierCost = { 0f, 1.0f, 2.0f, 3.5f };

        public static int Clamp(int level) => Mathf.Clamp(level, 0, MaxLevel);

        /// <summary>현재 단계의 위력 배수.</summary>
        public static float PowerMultiplier(int level) => Power[Clamp(level)];

        /// <summary><paramref name="from"/>에서 한 단계 올릴 때의 성공률. 만렙이면 0.</summary>
        public static float SuccessRate(int from)
        {
            int target = Clamp(from) + 1;
            return target > MaxLevel ? 0f : Rate[target];
        }

        /// <summary><paramref name="from"/>에서 한 단계 올리는 비용. 만렙이면 0.</summary>
        public static int Cost(int from, int tier)
        {
            int target = Clamp(from) + 1;
            if (target > MaxLevel) return 0;
            float mult = TierCost[Mathf.Clamp(tier, 1, WeaponFamilyInfo.MaxTier)];
            return Mathf.RoundToInt(BaseCost[target] * mult);
        }

        /// <summary><paramref name="from"/>에서 실패하면 무슨 일이 일어나는가.</summary>
        public static EnhanceFailure FailureAt(int from)
        {
            int target = Clamp(from) + 1;
            if (target <= SafeUntil) return EnhanceFailure.Keep;
            if (target <= DropUntil) return EnhanceFailure.Drop;
            return EnhanceFailure.DropOrDestroy;
        }

        public static bool IsMax(int level) => Clamp(level) >= MaxLevel;

        /// <summary>
        /// 한 번 굴린다. 방지권을 걸면 그만큼 나쁜 결과가 막힌다.
        /// 비용 차감과 방지권 소모는 부르는 쪽이 한다 — 여기서는 주사위만 굴린다.
        /// </summary>
        public static EnhanceResult Roll(int from, bool guardDrop, bool guardBreak, System.Random rng)
        {
            if (IsMax(from)) return EnhanceResult.Kept;

            double roll = rng != null ? rng.NextDouble() : Random.value;
            if (roll < SuccessRate(from)) return EnhanceResult.Success;

            switch (FailureAt(from))
            {
                case EnhanceFailure.Keep:
                    return EnhanceResult.Kept;

                case EnhanceFailure.Drop:
                    return guardDrop ? EnhanceResult.Kept : EnhanceResult.Dropped;

                default:
                    // 파괴 구간. 총열이 파괴를 막으면 하락으로, 쐐기까지 있으면 유지로 떨어진다.
                    bool destroy = (rng != null ? rng.NextDouble() : Random.value) < DestroyShare;
                    if (destroy && !guardBreak) return EnhanceResult.Destroyed;
                    return guardDrop ? EnhanceResult.Kept : EnhanceResult.Dropped;
            }
        }

        public static string LabelOf(EnhanceResult r)
        {
            switch (r)
            {
                case EnhanceResult.Success: return "성공";
                case EnhanceResult.Kept: return "실패 — 단계 유지";
                case EnhanceResult.Dropped: return "실패 — 한 단계 하락";
                case EnhanceResult.Destroyed: return "파괴";
                default: return r.ToString();
            }
        }
    }
}
