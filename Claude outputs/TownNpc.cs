using System.Collections.Generic;
using UnityEngine;

namespace MiniWar.Runtime
{
    /// <summary>마을에서 말을 걸 수 있는 대상.</summary>
    public enum TownStation
    {
        Armory = 0,   // 정비병 — 강화 · 보급
        Workshop = 1, // 보급 담당관 — 합성 · 분해 · 제작 · 편성
        Gate = 2,     // 출격 게이트 — 던전 선택
    }

    /// <summary>
    /// 마을 NPC 하나. 위치와 사거리만 들고 있고, 누구와 말할지는 TownController가 정한다.
    ///
    /// 메뉴를 걸어다니게 만드는 이유는 분위기 때문만이 아니다 —
    /// 정비소와 작업대가 <b>다른 장소</b>에 있으면 "강화하러 가는 길에 작업대를 지나친다"는
    /// 동선이 생기고, 그 동선 자체가 순서를 제안한다. 탭으로 즉시 전환되는 화면에는 없는 것이다.
    /// </summary>
    public sealed class TownNpc : MonoBehaviour
    {
        /// <summary>씬에 있는 NPC 전부. 마을 씬이 작아 이 정도면 충분하다.</summary>
        public static readonly List<TownNpc> All = new List<TownNpc>();

        [SerializeField] TownStation station;
        [SerializeField] string displayName = "정비병";
        [SerializeField] string role = "정비소";

        [Tooltip("이 거리 안에 들어오면 말을 걸 수 있다.")]
        [SerializeField, Min(0.5f)] float radius = 2.4f;

        [Tooltip("켜면 다가가는 것만으로 열린다. 게이트처럼 '지나가면 곧 출발'인 곳에 쓴다.")]
        [SerializeField] bool autoOpen;

        [Header("대화")]
        [Tooltip("말을 걸면 뜨는 일러스트. 없으면 자리만 비워둔다.")]
        [SerializeField] Sprite portrait;

        [Tooltip("일러스트에서 실제로 보여줄 영역 (0~1). 전신 그림에서 허리 위만 잘라 쓴다. "
               + "(0,0)이 왼쪽 아래 — y를 올리면 더 위쪽을 보여준다. 얼굴이 잘리면 여기서 맞춘다.")]
        [SerializeField] Rect portraitCrop = new Rect(0.18f, 0.40f, 0.60f, 0.57f);

        [Tooltip("첫 대사부터 순서대로. 마지막 대사를 넘기면 상점이 열린다.")]
        [SerializeField, TextArea(2, 3)] string[] lines;

        [Tooltip("이미 한 번 말을 건 뒤에 쓰는 짧은 인사. 비우면 lines를 계속 쓴다.")]
        [SerializeField, TextArea(2, 3)] string[] repeatLines;

        public TownStation Station => station;
        public string DisplayName => displayName;
        public string Role => role;
        public bool AutoOpen => autoOpen;
        public Sprite Portrait => portrait;

        /// <summary>일러스트에서 잘라 쓸 영역. 대사창에는 전신이 아니라 상반신만 올린다.</summary>
        public Rect PortraitCrop => portraitCrop;
        public float X => transform.position.x;

        /// <summary>
        /// 이번에 할 대사. 두 번째부터는 짧은 쪽으로 바꾼다 —
        /// 정비소를 열 때마다 같은 자기소개를 다시 듣는 것만큼 지치는 것도 없다.
        /// </summary>
        public string[] LinesFor(bool firstTime)
        {
            if (!firstTime && repeatLines != null && repeatLines.Length > 0) return repeatLines;
            return lines != null ? lines : new string[0];
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        public bool InRange(float x) => Mathf.Abs(X - x) <= radius;

        public float DistanceTo(float x) => Mathf.Abs(X - x);
    }
}
