using UnityEngine;

namespace MiniWar.Data
{
    /// <summary>이동 방식. 비행형은 지형을 무시하고 고도를 유지한다.</summary>
    public enum LocomotionKind
    {
        Ground = 0,
        Air = 1,
    }

    /// <summary>
    /// 적 한 종의 기본 스펙. 구간 배율(SegmentData)이 체력과 보상에 곱해지므로
    /// 이 에셋의 값은 항상 "1구간" 기준이다.
    ///
    /// 컨셉은 "혼자 군대를 상대한다" — 적은 병사·차량·항공기로 이루어진 편제이고,
    /// 실루엣만 봐도 어떤 총을 꺼내야 할지 읽혀야 한다.
    /// </summary>
    [CreateAssetMenu(menuName = "무기전쟁/Enemy Data", fileName = "SO_Enemy_")]
    public sealed class EnemyData : ScriptableObject
    {
        [Header("식별")]
        public string displayName = "돌격병";
        public bool isBoss;

        [Header("이동 방식")]
        [Tooltip("비행형은 고도를 유지하며 빠르게 접근한다. 탄속이 있으므로 예측 사격이 필요하다.")]
        public LocomotionKind locomotion = LocomotionKind.Ground;

        [Tooltip("비행형이 유지하는 고도(월드 Y).")]
        public float cruiseAltitude = 2.4f;

        [Tooltip("비행형이 위아래로 흔들리는 폭. 조준을 조금 어렵게 만든다.")]
        [Min(0f)] public float bobAmplitude = 0.3f;

        [Header("실루엣")]
        [Tooltip("몸체 크기. 병사는 세로로 길고 차량·항공기는 가로로 넓다. "
               + "플레이스홀더 단계에서도 이것만으로 종류가 구분된다.")]
        public Vector2 bodySize = new Vector2(0.7f, 1.3f);

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
