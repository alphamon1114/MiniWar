using System;
using UnityEngine;

namespace MiniWar.Data
{
    /// <summary>거리(m) 기준 스폰 1건. 시간이 아니라 거리를 키로 쓴다.</summary>
    [Serializable]
    public struct SpawnEntry
    {
        [Tooltip("구간 시작점으로부터의 거리(m).")]
        [Min(0f)] public float distance;

        public EnemyData enemy;

        [Min(1)] public int count;
    }

    /// <summary>
    /// 전진 구간 하나. 난이도는 체력 배율로 올리고 보상 배율은 그보다 훨씬 낮게 둔다.
    /// 보상 배율이 체력 배율을 따라가면 킬당 마진이 양수로 남아 압박이 사라진다.
    /// </summary>
    [CreateAssetMenu(menuName = "무기전쟁/Segment Data", fileName = "SO_Segment_")]
    public sealed class SegmentData : ScriptableObject
    {
        [Header("식별")]
        [Min(1)] public int index = 1;
        [Min(1f)] public float lengthMeters = 100f;

        [Header("배율")]
        [Tooltip("적 체력에 곱해진다. 1.0 / 1.6 / 2.5")]
        [Min(0.1f)] public float healthMultiplier = 1f;

        [Tooltip("격파 보상에 곱해진다. 1.00 / 1.15 / 1.30 — 체력 배율보다 반드시 낮게.")]
        [Min(0.1f)] public float rewardMultiplier = 1f;

        [Header("스폰")]
        public SpawnEntry[] spawns = Array.Empty<SpawnEntry>();

        public float HealthOf(EnemyData e) => e.baseHealth * healthMultiplier;
        public int RewardOf(EnemyData e) => Mathf.RoundToInt(e.baseReward * rewardMultiplier);

#if UNITY_EDITOR
        void OnValidate()
        {
            if (rewardMultiplier >= healthMultiplier && index > 1)
            {
                Debug.LogWarning(
                    $"[{name}] 보상 배율({rewardMultiplier})이 체력 배율({healthMultiplier}) 이상입니다. " +
                    "이러면 구간이 올라갈수록 오히려 돈이 남아 경제 압박이 사라집니다.", this);
            }
        }
#endif
    }
}
