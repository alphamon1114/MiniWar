using System.Collections.Generic;
using UnityEngine;

namespace MiniWar.Data
{
    /// <summary>합성 시도의 판정 결과.</summary>
    public enum FusionVerdict
    {
        /// <summary>계열까지 같은 셋 — 다음 등급의 같은 계열이 나온다.</summary>
        SameFamily = 0,
        /// <summary>등급만 같은 셋 — 다음 등급에서 무작위 계열이 나온다.</summary>
        MixedFamily = 1,
        /// <summary>셋이 아니다.</summary>
        NeedThree = 2,
        /// <summary>등급이 섞였다.</summary>
        TierMismatch = 3,
        /// <summary>천장. 3등급은 재료가 되지 않는다.</summary>
        AtCeiling = 4,
    }

    /// <summary>
    /// 미니파이터식 합성. <b>같은 등급 3정 → 다음 등급 1정.</b>
    ///
    /// 계열이 섞여 있으면 무작위 계열, 계열까지 같으면 같은 계열이 나온다.
    /// 이 한 줄이 규칙의 전부고, 여기서 <b>속도와 방향의 맞바꿈</b>이 생긴다 —
    /// 잡템 셋을 아무렇게나 넣어 빨리 올릴 것인가, 같은 계열이 셋 모일 때까지
    /// 기다려 원하는 총을 지목할 것인가.
    ///
    /// <b>강화는 계승되지 않는다.</b> 결과물은 언제나 +0이다.
    /// 강화한 총을 정리할 때 "합성에 넣을 것인가(강화 소멸), 분해할 것인가(부적 기회)"가
    /// 갈리는 것이 이 규칙의 의도다.
    /// </summary>
    public static class FusionTable
    {
        public const int MaterialCount = 3;

        /// <summary>재료 셋이 합성 가능한지, 가능하면 어느 쪽인지.</summary>
        public static FusionVerdict Judge(IReadOnlyList<WeaponData> materials)
        {
            if (materials == null || materials.Count != MaterialCount) return FusionVerdict.NeedThree;

            var first = materials[0];
            if (first == null) return FusionVerdict.NeedThree;

            for (int i = 1; i < materials.Count; i++)
            {
                if (materials[i] == null) return FusionVerdict.NeedThree;
                if (materials[i].tier != first.tier) return FusionVerdict.TierMismatch;
            }

            if (first.tier >= WeaponFamilyInfo.MaxTier) return FusionVerdict.AtCeiling;

            for (int i = 1; i < materials.Count; i++)
            {
                if (materials[i].family != first.family) return FusionVerdict.MixedFamily;
            }
            return FusionVerdict.SameFamily;
        }

        public static bool CanFuse(FusionVerdict v)
            => v == FusionVerdict.SameFamily || v == FusionVerdict.MixedFamily;

        /// <summary>
        /// 결과물을 뽑는다. 합성이 불가능하면 null.
        ///
        /// 무작위 계열은 여기서 굴린다 — 나중에 서버로 올라갈 자리다.
        /// 클라이언트가 이 주사위를 굴리면 늘 원하는 계열이 나오게 만들 수 있다.
        /// </summary>
        public static WeaponData Resolve(IReadOnlyList<WeaponData> materials,
                                         WeaponCatalog catalog,
                                         System.Random rng,
                                         out FusionVerdict verdict)
        {
            verdict = Judge(materials);
            if (!CanFuse(verdict) || catalog == null) return null;

            int nextTier = materials[0].tier + 1;

            if (verdict == FusionVerdict.SameFamily)
                return catalog.Find(nextTier, materials[0].family);

            var pool = catalog.OfTier(nextTier);
            if (pool.Count == 0) return null;

            int pick = rng != null
                ? rng.Next(pool.Count)
                : Random.Range(0, pool.Count);
            return pool[pick];
        }

        public static string LabelOf(FusionVerdict v)
        {
            switch (v)
            {
                case FusionVerdict.SameFamily: return "같은 계열 — 계열이 유지된다";
                case FusionVerdict.MixedFamily: return "계열 혼합 — 무작위 계열이 나온다";
                case FusionVerdict.NeedThree: return "재료 3정을 채워야 한다";
                case FusionVerdict.TierMismatch: return "같은 등급끼리만 합칠 수 있다";
                case FusionVerdict.AtCeiling: return "개조 등급은 더 올라갈 곳이 없다";
                default: return v.ToString();
            }
        }
    }
}
