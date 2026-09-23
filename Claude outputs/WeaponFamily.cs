namespace MiniWar.Data
{
    /// <summary>
    /// 무기 계열 — <b>어떻게 쏘는가</b>.
    ///
    /// 등급은 얼마나 센가를 정하고, 계열은 성격을 정한다.
    /// 등급이 올라도 계열은 변하지 않는다 — 대전차포는 3등급이 되어도 여전히 느리고,
    /// 보병에게는 여전히 안 맞는다. 설정이 아니라 탄속이 그걸 강제한다.
    ///
    /// 합성이 "같은 계열"을 약속하려면 모든 등급에 모든 계열이 있어야 하므로
    /// 이 enum의 길이 × 등급 수가 곧 무기 총수다.
    /// </summary>
    public enum WeaponFamily
    {
        /// <summary>단발 — 정확하고 싸다. 돈이 마르면 돌아오는 자리.</summary>
        Single = 0,
        /// <summary>연사 — 빠르고 퍼진다. 보병 무리.</summary>
        Auto = 1,
        /// <summary>산탄 — 근거리에서 여러 발. 붙으면 강하다.</summary>
        Shot = 2,
        /// <summary>관통 — 느리고 무겁고 방어력을 무시한다. 장갑 전용.</summary>
        Pierce = 3,
        /// <summary>곡사 — 포물선. 탄창이 아니라 던질 때마다 돈.</summary>
        Arc = 4,
        /// <summary>근접 — 탄약비 0. 대신 맞을 각오를 해야 한다.</summary>
        Melee = 5,
    }

    public static class WeaponFamilyInfo
    {
        /// <summary>계열 수. 합성 무작위 추첨과 에셋 생성이 이 값을 쓴다.</summary>
        public const int Count = 6;

        /// <summary>등급 수. 1 ~ MaxTier.</summary>
        public const int MaxTier = 3;

        public static string LabelOf(WeaponFamily family)
        {
            switch (family)
            {
                case WeaponFamily.Single: return "단발";
                case WeaponFamily.Auto: return "연사";
                case WeaponFamily.Shot: return "산탄";
                case WeaponFamily.Pierce: return "관통";
                case WeaponFamily.Arc: return "곡사";
                case WeaponFamily.Melee: return "근접";
                default: return family.ToString();
            }
        }

        /// <summary>등급 이름. 숫자보다 이쪽이 화면에서 읽힌다.</summary>
        public static string TierName(int tier)
        {
            switch (tier)
            {
                case 1: return "지급품";
                case 2: return "제식";
                case 3: return "개조";
                default: return tier + "등급";
            }
        }
    }
}
