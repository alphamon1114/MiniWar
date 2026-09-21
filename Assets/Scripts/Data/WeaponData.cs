using UnityEngine;

namespace MiniWar.Data
{
    /// <summary>
    /// 무기 한 종의 기본 스펙. 강화는 런타임(WeaponInstance)에서 곱해지므로
    /// 이 에셋의 값은 항상 "강화 0회" 기준이다.
    /// </summary>
    [CreateAssetMenu(menuName = "무기전쟁/Weapon Data", fileName = "SO_Weapon_")]
    public sealed class WeaponData : ScriptableObject
    {
        [Header("식별")]
        public string displayName = "권총";

        [Tooltip("숫자키 전환 슬롯. 1~4.")]
        [Range(1, 4)] public int slot = 1;

        [Header("화력")]
        [Tooltip("펠릿 하나당 위력. 방어력은 펠릿마다 감산된다.")]
        [Min(0f)] public float damagePerPellet = 25f;

        [Tooltip("1발에 나가는 펠릿 수. 산탄형만 2 이상.")]
        [Min(1)] public int pellets = 1;

        [Tooltip("켜면 방어력을 무시한다. 고위력형 전용.")]
        public bool piercing;

        [Header("탄약과 비용")]
        [Min(1)] public int magazineSize = 12;

        [Tooltip("탄창 하나를 채우는 비용. 권총을 포함해 모든 총기가 유료다.")]
        [Min(0)] public int reloadCost = 9;

        [Header("속도")]
        [Min(0.01f)] public float shotsPerSecond = 2f;
        [Min(0.01f)] public float reloadSeconds = 1.2f;

        /// <summary>강화 0회 기준 발당 탄약비. 밸런싱 표와 대조할 때 쓴다.</summary>
        public float CostPerShot => magazineSize <= 0 ? 0f : (float)reloadCost / magazineSize;

        /// <summary>강화 0회 기준 초당 피해량(방어력 0 상대).</summary>
        public float RawDps => damagePerPellet * pellets * shotsPerSecond;
    }
}
