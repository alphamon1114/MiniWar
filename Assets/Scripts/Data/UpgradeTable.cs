using UnityEngine;

namespace MiniWar.Data
{
    /// <summary>
    /// 강화 축. 위력·연사는 체력 곡선을, 장탄수·장전비 인하는 경제 곡선을 상쇄한다.
    /// 플레이어가 "적이 안 죽어서 힘든가, 돈이 안 모여서 힘든가"를 판단해 고르게 하는 것이 설계 의도.
    /// </summary>
    public enum UpgradeAxis
    {
        Damage = 0,       // 위력    ×1.35 / 회 · 즉시 효과
        FireRate = 1,     // 연사    ×1.25 / 회 · 즉시 효과
        MagazineSize = 2, // 장탄수  ×1.30 / 회 · 즉시 효과
        ReloadCost = 3,   // 장전비  ×0.70 / 회 · 누적 효과(남은 구간이 길수록 이득)
    }

    [CreateAssetMenu(menuName = "무기전쟁/Upgrade Table", fileName = "SO_UpgradeTable")]
    public sealed class UpgradeTable : ScriptableObject
    {
        [Header("회당 배율")]
        [Min(1f)] public float damagePerLevel = 1.35f;
        [Min(1f)] public float fireRatePerLevel = 1.25f;
        [Min(1f)] public float magazinePerLevel = 1.30f;

        [Tooltip("1 미만이어야 비용이 줄어든다.")]
        [Range(0.1f, 1f)] public float reloadCostPerLevel = 0.70f;

        [Header("가격 — 강화는 돈으로 산다")]
        [Tooltip("첫 강화 가격. 이후 살 때마다 배율만큼 오른다. "
               + "돈이 탄약·점수와 경쟁하게 만드는 것이 목적이다.")]
        [Min(0)] public int firstUpgradeCost = 350;

        [Tooltip("구매할 때마다 가격에 곱해지는 배율. 기하급수라 무한 구매를 스스로 막는다.")]
        [Min(1f)] public float costGrowth = 2f;

        [Tooltip("정비 지점 한 곳에서 살 수 있는 최대 개수. 0이면 돈이 되는 만큼.")]
        [Min(0)] public int maxPurchasesPerStop = 0;

        /// <summary>이미 purchased번 샀을 때 다음 강화의 가격.</summary>
        public int CostOf(int purchased)
            => Mathf.RoundToInt(firstUpgradeCost * Mathf.Pow(costGrowth, Mathf.Max(0, purchased)));

        public float MultiplierPerLevel(UpgradeAxis axis) => axis switch
        {
            UpgradeAxis.Damage => damagePerLevel,
            UpgradeAxis.FireRate => fireRatePerLevel,
            UpgradeAxis.MagazineSize => magazinePerLevel,
            UpgradeAxis.ReloadCost => reloadCostPerLevel,
            _ => 1f,
        };

        public static string LabelOf(UpgradeAxis axis) => axis switch
        {
            UpgradeAxis.Damage => "위력",
            UpgradeAxis.FireRate => "연사",
            UpgradeAxis.MagazineSize => "장탄수",
            UpgradeAxis.ReloadCost => "장전 비용 인하",
            _ => axis.ToString(),
        };
    }
}
