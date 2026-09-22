using UnityEngine;

namespace MiniWar.Runtime
{
    /// <summary>
    /// 마을 카메라. 던전의 CameraRig와 달리 앞뒤로 자유롭게 따라간다 —
    /// 마을은 돌아다니는 곳이고, 되돌아갈 수 없다는 규칙은 던전에만 있다.
    ///
    /// 양 끝에서는 멈춘다. 벽 너머 빈 공간이 보이면 마을이 작아 보인다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class TownCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] float smooth = 6f;
        [SerializeField] float minX = -4f;
        [SerializeField] float maxX = 36f;
        [SerializeField] float baseY;

        Camera _camera;

        void Awake() => _camera = GetComponent<Camera>();

        public void SetBounds(float min, float max) { minX = min; maxX = max; }

        void LateUpdate()
        {
            if (target == null) return;

            float half = _camera.orthographicSize * _camera.aspect;

            // 마을 폭이 화면보다 좁으면 가운데 고정. 아니면 양 끝에서 멈춘다.
            float lo = minX + half;
            float hi = maxX - half;
            float desired = lo > hi ? (minX + maxX) * 0.5f
                                    : Mathf.Clamp(target.position.x, lo, hi);

            var pos = transform.position;
            pos.x = Mathf.Lerp(pos.x, desired, Time.deltaTime * smooth);
            pos.y = Mathf.Lerp(pos.y, baseY, Time.deltaTime * smooth);
            transform.position = pos;
        }
    }
}
