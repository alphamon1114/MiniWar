using UnityEngine;

namespace MiniWar.Data
{
    /// <summary>
    /// 적 한 종의 기본 스펙. 구간 배율(SegmentData)이 체력과 보상에 곱해지므로
    /// 이 에셋의 값은 항상 "1구간" 기준이다.
    /// </summary>
    [CreateAssetMenu(menuName = "무기전쟁/Enemy Data", fileName = "SO_Enemy_")]
    public sealed class EnemyData : ScriptableObject
    {
        [Header("식별")]
        public string displayName = "근접 돌진형";
        public bool isBoss;

        [Header("방어")]
        [Min(1f)] public float baseHealth = 100f;

        [Tooltip("고정 감산 방어력. 관통 무기에는 무시된다. 장갑형과 보스에만 0보다 크게 둘 것 — "
               + "전체 적에게 주면 권총 피해가 0에 수렴해 최후의 수단이 사라진다.")]
        [Min(0f)] public float armor;

        [Header("보상과 위협")]
        [Tooltip("격파 보상. 권총 장전비보다 반드시 커야 파산에서 복귀할 길이 남는다.")]
        [Min(0)] public int baseReward = 60;

        [Tooltip("살아 있는 동안 플레이어에게 주는 초당 피해. 느린 무기의 대가를 만드는 값.")]
        [Min(0f)] public float damagePerSecond = 6f;

        [Min(0f)] public float moveSpeed = 1.5f;
    }
}
