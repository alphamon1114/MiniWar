using UnityEngine;

namespace MiniWar.Data
{
    /// <summary>분해 한 번의 산출.</summary>
    public struct SalvageYield
    {
        /// <summary>폐품 부속. 확정으로 나온다.</summary>
        public int parts;

        /// <summary>강화 부적의 단계. 0이면 안 나온 것.</summary>
        public int talismanLevel;

        /// <summary>부적을 쓸 수 있는 최대 등급. 분해한 무기의 등급이다.</summary>
        public int talismanTier;

        public bool HasTalisman => talismanLevel > 0;
    }

    /// <summary>
    /// 분해와, 부품으로 만드는 방지권.
    ///
    /// 분해는 두 가지를 낸다 — <b>부품(확정)</b>과 <b>강화 부적(50%)</b>.
    /// 부적은 "강화는 합성을 넘지 못한다"는 규칙의 유일한 숨구멍이다.
    /// 한 단계와 동전 던지기를 대가로 같은 등급의 다른 총에 옮길 수 있다.
    /// </summary>
    public static class SalvageTable
    {
        /// <summary>부적이 나올 확률.</summary>
        public const float TalismanChance = 0.50f;

        /// <summary>강화 단계 1당 추가로 나오는 부품.</summary>
        public const int PartsPerLevel = 3;

        // index = 등급. 1등급 8 / 2등급 24 / 3등급 72 — 합성 비율(3배)과 같은 결을 쓴다.
        static readonly int[] BaseParts = { 0, 8, 24, 72 };

        // ── 방지권 ──────────────────────────────────────────────
        // 12강 이상에서는 사실상 둘 다 필요하다. 후반 부품 소모의 대부분이 여기서 나간다.

        /// <summary>고정 쐐기 — 실패해도 단계가 안 떨어진다. 파괴는 못 막는다.</summary>
        public const int WedgeCost = 8;

        /// <summary>예비 총열 — 실패해도 파괴되지 않는다. 하락은 한다.</summary>
        public const int SpareBarrelCost = 20;

        public static int PartsFrom(int tier, int enhanceLevel)
        {
            int t = Mathf.Clamp(tier, 1, WeaponFamilyInfo.MaxTier);
            return BaseParts[t] + Mathf.Max(0, enhanceLevel) * PartsPerLevel;
        }

        /// <summary>
        /// 분해를 굴린다. 부적은 <b>한 단계 낮게</b> 나오고,
        /// <b>분해한 무기의 등급 이하</b>에만 쓸 수 있다.
        ///
        /// 등급 제한이 없으면 설계가 무너진다 — 성공률은 등급과 무관하고 비용만 다르므로,
        /// 제한이 없으면 1등급을 싸게 올린 뒤 분해해 3등급에 꽂는 우회로가
        /// 직접 강화보다 훨씬 싸게 끝난다.
        /// </summary>
        public static SalvageYield Roll(int tier, int enhanceLevel, System.Random rng)
        {
            var y = new SalvageYield
            {
                parts = PartsFrom(tier, enhanceLevel),
                talismanLevel = 0,
                talismanTier = Mathf.Clamp(tier, 1, WeaponFamilyInfo.MaxTier),
            };

            // +1 이하를 분해하면 부적이 +0이 되어 아무 쓸모가 없다.
            if (enhanceLevel < 2) return y;

            double roll = rng != null ? rng.NextDouble() : Random.value;
            if (roll < TalismanChance) y.talismanLevel = enhanceLevel - 1;

            return y;
        }
    }
}
