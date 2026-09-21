using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniWar.Runtime
{
    /// <summary>
    /// A / D 전진·후진, 스페이스 점프. 능선을 따라 걷는다.
    ///
    /// <b>후퇴로는 없다.</b> 카메라가 도달한 가장 먼 지점을 기준으로 왼쪽 벽이 따라오므로
    /// 조금 물러설 수는 있어도 지나온 길로 돌아갈 수는 없다 — 전진이 유일한 방향이다.
    /// </summary>
    public sealed class PlayerMotor : MonoBehaviour
    {
        [Header("이동")]
        [SerializeField] float moveSpeed = 4.2f;
        [SerializeField] float backSpeedFactor = 0.6f;   // 후진은 느리다
        [SerializeField] float jumpSpeed = 9f;
        [SerializeField] float gravity = 26f;

        [Header("접지")]
        [Tooltip("발이 지면보다 이만큼 위에 온다. 스프라이트 중심 보정.")]
        [SerializeField] float footOffset = 0.7f;

        float _velocityY;
        bool _grounded;

        /// <summary>돌아갈 수 없는 왼쪽 경계. CameraRig가 매 프레임 밀어 올린다.</summary>
        public float MinX { get; set; } = float.NegativeInfinity;

        public bool IsGrounded => _grounded;
        public float DistanceTravelled { get; private set; }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            float dt = Time.deltaTime;
            var pos = transform.position;

            // ── 수평 ──
            float input = 0f;
            if (kb.dKey.isPressed) input += 1f;
            if (kb.aKey.isPressed) input -= 1f;

            float speed = moveSpeed * (input < 0f ? backSpeedFactor : 1f);
            pos.x += input * speed * dt;

            if (pos.x < MinX) pos.x = MinX;

            // ── 수직 ──
            float groundY = GroundAt(pos.x);

            if (_grounded && kb.spaceKey.wasPressedThisFrame)
            {
                _velocityY = jumpSpeed;
                _grounded = false;
            }

            if (!_grounded)
            {
                _velocityY -= gravity * dt;
                pos.y += _velocityY * dt;

                if (pos.y <= groundY && _velocityY <= 0f)
                {
                    pos.y = groundY;
                    _velocityY = 0f;
                    _grounded = true;
                }
            }
            else
            {
                // 능선을 따라 붙어 걷는다. 내리막에서 공중에 뜨지 않게.
                pos.y = groundY;
            }

            transform.position = pos;
            DistanceTravelled = Mathf.Max(DistanceTravelled, pos.x);
        }

        float GroundAt(float x)
        {
            var terrain = TerrainGenerator.Instance;
            return (terrain != null ? terrain.HeightAt(x) : -2.2f) + footOffset;
        }
    }
}
