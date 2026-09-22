using System;
using UnityEngine;

namespace MiniWar.Data
{
    /// <summary>보스가 쓰는 공격. 각각 플레이어에게 요구하는 대응이 다르다.</summary>
    public enum BossAttack
    {
        /// <summary>주포 — 조준선을 0.8초 보여준 뒤 직사로 쏜다. 능선 뒤로 숨으면 막힌다.</summary>
        Cannon = 0,

        /// <summary>곡사 포격 — 착탄 표시 후 위에서 떨어진다. 능선이 막아주지 않으니 자리를 옮겨야 한다.</summary>
        Mortar = 1,

        /// <summary>탄막 — 느린 포탄을 부채꼴로 흩뿌린다. 틈으로 걸어 들어가야 한다.</summary>
        Barrage = 2,
    }

    /// <summary>
    /// 보스의 한 형태. 체력 구간마다 장갑·속도·패턴이 통째로 바뀐다.
    ///
    /// 장갑을 페이즈마다 바꾸는 것이 이 설계의 핵심이다 —
    /// 1페이즈는 관통이 필요하고 2페이즈는 연사가 유리하게 만들면,
    /// "무기 하나만 강화한다"는 선택이 보스전에서 반드시 대가를 치른다.
    /// </summary>
    [Serializable]
    public struct BossPhase
    {
        [Tooltip("페이즈 이름. 전환할 때 화면에 뜬다.")]
        public string label;

        [Tooltip("이 페이즈가 차지하는 총 체력 비율. 전부 합쳐 1이 되게 둔다.")]
        [Range(0.05f, 1f)] public float healthShare;

        [Tooltip("이 페이즈 동안의 방어력. 페이즈마다 유효한 무기가 달라지는 지점.")]
        [Min(0f)] public float armor;

        [Tooltip("플레이어 쪽으로 밀고 오는 속도.")]
        [Min(0f)] public float moveSpeed;

        [Tooltip("이 페이즈에서 쓰는 패턴. 매 사이클 이 중 하나를 고른다.")]
        public BossAttack[] attacks;

        [Min(0.3f)] public float attackInterval;

        [Tooltip("포탄 1발의 피해량.")]
        [Min(1f)] public float shellDamage;

        [Tooltip("실루엣 명도. 형태가 바뀐 걸 색으로도 알려준다.")]
        public Color tint;

        [Tooltip("페이즈 진입 시 띄울 한 줄.")]
        public string enterNotice;
    }

    /// <summary>
    /// 다단 페이즈 보스. 빅샷의 디스트로이어(총 3번 형태가 변하는 진행식 보스)가 원형이다.
    /// </summary>
    [CreateAssetMenu(menuName = "무기전쟁/Boss Data", fileName = "SO_Boss_")]
    public sealed class BossData : ScriptableObject
    {
        [Header("식별")]
        public string displayName = "대형 전차";

        [Header("기본")]
        [Min(1f)] public float totalHealth = 1800f;
        [Min(0)] public int reward = 0;
        public Vector2 bodySize = new Vector2(4.2f, 2.1f);

        [Tooltip("0보다 크면 지면 위 이 높이에 떠 있다. 비행 보스는 곡사가 닿지 않는다.")]
        [Min(0f)] public float hoverHeight;

        [Tooltip("몸통에 닿아 있을 때 초당 피해.")]
        [Min(0f)] public float contactDamagePerSecond = 22f;

        [Tooltip("이 거리 안으로는 들어오지 않는다. 0이면 끝까지 밀고 온다.")]
        [Min(0f)] public float standoffDistance = 5f;

        [Header("형태")]
        public BossPhase[] phases = Array.Empty<BossPhase>();

        /// <summary>페이즈 i가 끝나는 체력 비율(1 → 0 방향).</summary>
        public float PhaseFloor(int index)
        {
            float remaining = 1f;
            for (int i = 0; i <= index && i < phases.Length; i++)
                remaining -= phases[i].healthShare;
            return Mathf.Clamp01(remaining);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (phases == null || phases.Length == 0) return;

            float sum = 0f;
            foreach (var p in phases) sum += p.healthShare;

            if (Mathf.Abs(sum - 1f) > 0.01f)
            {
                Debug.LogWarning(
                    $"[{name}] 페이즈 체력 비율 합이 {sum:F2}입니다. 1이 아니면 마지막 형태가 " +
                    "너무 빨리 끝나거나 영영 오지 않습니다.", this);
            }
        }
#endif
    }
}
