using UnityEngine;

namespace MiniWar.Runtime
{
    /// <summary>
    /// 배경 한 겹. 카메라를 자기 속도로 따라가고, 가로로 <b>끝없이 반복</b>된다.
    ///
    /// 깊이를 그리는 방법은 하나뿐이다 — 멀리 있는 것은 천천히 지나간다.
    /// 그래서 이 컴포넌트가 하는 일도 딱 그것이다. <see cref="parallax"/>가 0이면
    /// 카메라에 붙어 전혀 흐르지 않고(무한히 먼 하늘), 1이면 지면과 같이 흐른다(발밑).
    /// 1을 넘기면 지면보다 빨라져서 <b>앞으로</b> 튀어나온다 — 전경 난간이 그렇다.
    ///
    /// 반복은 타일을 옮기는 것이 아니라 <b>뿌리를 접어서</b> 만든다. 타일 폭 하나 안으로
    /// 위치를 되감으면(Mathf.Repeat) 자식 타일 몇 장이 영원히 화면을 덮는다.
    /// 마을이 44m든 400m든 배경은 세 장이면 끝난다.
    /// </summary>
    public sealed class ParallaxLayer : MonoBehaviour
    {
        [Tooltip("따라갈 카메라. 비우면 Camera.main을 찾는다.")]
        [SerializeField] Transform view;

        [Tooltip("0 = 카메라에 붙어 안 움직인다(먼 하늘) · 1 = 지면과 같다 · 1 초과 = 지면보다 빨라 앞으로 튀어나온다.")]
        [SerializeField, Range(0f, 2f)] float parallax = 0.5f;

        [Tooltip("타일 한 장의 월드 폭. 이 값으로 되감으므로 자식 타일 간격과 반드시 같아야 한다.")]
        [SerializeField, Min(0.01f)] float tileWidth = 20f;

        float _homeX;
        Transform _view;
        bool _ready;

        void OnEnable()
        {
            // 첫 프레임에 위치를 덮어쓰기 전의 x가 이 레이어의 기준점이다.
            _homeX = transform.position.x;
            _view = view != null ? view : (Camera.main != null ? Camera.main.transform : null);
            _ready = _view != null;
        }

        void LateUpdate()
        {
            if (!_ready)
            {
                // 씬 로드 순서에 따라 Camera.main이 늦게 잡힐 수 있다.
                _view = view != null ? view : (Camera.main != null ? Camera.main.transform : null);
                if (_view == null) return;
                _ready = true;
            }

            float camX = _view.position.x;

            // 카메라 기준으로 이 레이어가 얼마나 밀려 있어야 하는가.
            // parallax가 작을수록 카메라를 따라붙어 화면에서 덜 흐른다.
            float relative = _homeX - camX * parallax;

            // 타일 폭 하나 안으로 접는다. 접어도 그림이 같으므로 눈에는 이어져 보인다.
            float half = tileWidth * 0.5f;
            float wrapped = Mathf.Repeat(relative + half, tileWidth) - half;

            var p = transform.position;
            p.x = camX + wrapped;
            transform.position = p;
        }

#if UNITY_EDITOR
        /// <summary>씬 빌더가 쓴다. 자식 타일 간격과 tileWidth가 어긋나면 이음매가 벌어진다.</summary>
        public void Configure(Transform camera, float parallaxFactor, float tileWidthWorld)
        {
            view = camera;
            parallax = parallaxFactor;
            tileWidth = tileWidthWorld;
        }
#endif
    }
}
