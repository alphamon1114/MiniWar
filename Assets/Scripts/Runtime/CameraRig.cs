using UnityEngine;

namespace MiniWar.Runtime
{
    /// <summary>
    /// 카메라는 앞으로만 간다. 가장 멀리 간 지점을 기억하고 뒤로는 되돌아가지 않으므로
    /// 지나온 길이 화면 밖으로 사라지고, 그만큼 플레이어의 왼쪽 벽이 밀려온다.
    ///
    /// 세로는 능선을 부드럽게 따라가되 폭을 제한해 화면이 출렁이지 않게 한다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] PlayerMotor player;
        [Tooltip("플레이어를 화면 왼쪽 몇 유닛 지점에 둘지. 클수록 앞이 많이 보인다.")]
        [SerializeField] float leadOffset = 3.5f;
        [SerializeField] float verticalSmooth = 2.5f;
        [SerializeField] float verticalRange = 1.8f;
        [SerializeField] float baseY = 0f;

        Camera _camera;
        float _maxX = float.NegativeInfinity;

        void Awake() => _camera = GetComponent<Camera>();

        void LateUpdate()
        {
            if (player == null) return;

            float desiredX = player.transform.position.x + leadOffset;
            _maxX = Mathf.Max(_maxX, desiredX);          // 뒤로 되돌아가지 않는다

            var pos = transform.position;
            pos.x = _maxX;

            float targetY = Mathf.Clamp(player.transform.position.y, baseY - verticalRange, baseY + verticalRange);
            pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * verticalSmooth);
            transform.position = pos;

            // 화면 왼쪽 가장자리가 곧 후퇴 한계선이 된다.
            float halfWidth = _camera.orthographicSize * _camera.aspect;
            player.MinX = _maxX - halfWidth + 0.6f;
        }
    }
}
