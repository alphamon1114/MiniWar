using System;
using UnityEngine;

namespace MiniWar.Data
{
    /// <summary>
    /// 스테이지 안의 구간 하나. 길이와 스폰표는 <see cref="SegmentData"/>가 들고 있고,
    /// 여기서는 "이 스테이지에서 몇 번째냐"만 붙인다.
    /// </summary>
    [Serializable]
    public struct StageLeg
    {
        public SegmentData segment;

        [Tooltip("구간 이름. 관문 화면과 진척 바에 뜬다.")]
        public string label;
    }

    /// <summary>
    /// 길이가 정해진 스테이지 하나. 빅샷 퀘스트모드의 구조를 그대로 가져왔다 —
    /// <b>구간 → 관문 → 구간 → 관문 → 보스방</b>.
    ///
    /// 끝이 있다는 것이 핵심이다. 무한 전진에서는 "얼마나 갔나"가 점수일 뿐이지만,
    /// 길이가 정해지면 "보스까지 이 탄약과 이 돈으로 버틸 수 있나"라는 계획 문제가 된다.
    /// 무기전쟁의 경제가 실제로 긴장을 만드는 건 후자 쪽이다.
    /// </summary>
    [CreateAssetMenu(menuName = "무기전쟁/Stage Data", fileName = "SO_Stage_")]
    public sealed class StageData : ScriptableObject
    {
        [Header("식별")]
        [Min(1)] public int stageNumber = 1;
        public string displayName = "국경 능선";

        [Tooltip("던전 선택 화면에 뜨는 한 줄. 무엇을 각오해야 하는지 말해준다.")]
        [TextArea(2, 3)] public string briefing = "";

        [Header("보상")]
        [Tooltip("클리어할 때마다 받는다. 반복해서 돌 수 있는 던전의 수입원.")]
        [Min(0)] public int clearReward = 400;

        [Tooltip("최초 클리어 1회에만 주는 보너스. 새 던전으로 밀어주는 힘.")]
        [Min(0)] public int firstClearBonus = 600;

        [Header("구성")]
        [Tooltip("앞에서부터 순서대로 전진한다. 각 구간의 길이·스폰표는 SegmentData에 있다.")]
        public StageLeg[] legs = Array.Empty<StageLeg>();

        public BossData boss;

        [Tooltip("같은 보스를 재활용할 때 체력만 올린다. 던전마다 보스를 새로 만들 필요가 없다.")]
        [Min(0.1f)] public float bossHealthMultiplier = 1f;

        [Header("보스방")]
        [Tooltip("보스방의 폭. 이 안에서는 앞으로도 뒤로도 나갈 수 없다. "
               + "0이면 보스방이 없다 — 보스가 비어 있는 훈련 스테이지가 그렇다.")]
        [Min(0f)] public float arenaLength = 30f;

        [Tooltip("보스방 바닥 높이. 능선이 여기서 평평해진다 — 보스 패턴이 지형에 가려지면 읽을 수 없다.")]
        public float arenaFloorHeight = -2.2f;

        [Header("관문")]
        [Tooltip("구간을 넘을 때 회복하는 체력. 보스전 진입 체력을 결정하는 값이라 매우 민감하다.")]
        [Min(0f)] public float healPerCheckpoint = 60f;

        /// <summary>구간 i의 시작 지점(스테이지 시작 기준 m).</summary>
        public float LegStart(int index)
        {
            float x = 0f;
            for (int i = 0; i < index && i < legs.Length; i++)
                x += LengthOf(i);
            return x;
        }

        public float LengthOf(int index)
        {
            if (index < 0 || index >= legs.Length) return 0f;
            var s = legs[index].segment;
            return s != null ? s.lengthMeters : 100f;
        }

        /// <summary>
        /// 보스가 없는 스테이지. 마지막 관문을 통과하는 것이 곧 클리어다 —
        /// 훈련 구역처럼 "돈을 벌러 들르는 곳"에는 보스가 오히려 방해가 된다.
        /// </summary>
        public bool HasBoss => boss != null;

        /// <summary>보스방이 시작되는 지점.</summary>
        public float ArenaStart => LegStart(legs.Length);

        public float TotalLength => ArenaStart + (HasBoss ? arenaLength : 0f);

        public string LabelOf(int index)
        {
            if (index < 0 || index >= legs.Length) return HasBoss ? "보스방" : "귀환";
            string label = legs[index].label;
            return string.IsNullOrEmpty(label) ? $"{index + 1}구간" : label;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (legs == null) return;
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i].segment == null)
                    Debug.LogWarning($"[{name}] {i + 1}번째 구간에 SegmentData가 비어 있습니다.", this);
            }
            // 보스가 없는 것 자체는 정상이다(훈련 스테이지). 다만 보스방 길이는 0이어야 한다.
            if (boss == null && arenaLength > 0f)
            {
                Debug.LogWarning($"[{name}] 보스가 없는데 보스방 길이가 {arenaLength}입니다. "
                               + "0으로 두면 마지막 관문이 곧 클리어가 됩니다.", this);
            }
        }
#endif
    }
}
