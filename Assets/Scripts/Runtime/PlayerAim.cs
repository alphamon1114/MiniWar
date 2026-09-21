using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniWar.Runtime
{
    /// <summary>
    /// 마우스 조준. 원작처럼 십자 조준점이 커서를 따라다니고, 그 지점이 곧 명중 지점이다.
    /// 플레이어 이동은 없다 — 전진은 자동이고 플레이어는 조준에만 집중한다.
    /// </summary>
    public sealed class PlayerAim : MonoBehaviour
    {
        [SerializeField] Transform crosshair;
        [SerializeField] Camera targetCamera;
        [SerializeField] bool hideHardwareCursor = true;

        /// <summary>현재 조준 지점(월드 좌표). 사격 판정은 전부 이 값을 쓴다.</summary>
        public Vector2 AimWorldPosition { get; private set; }

        void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
        }

        void OnEnable()
        {
            if (hideHardwareCursor) Cursor.visible = false;
        }

        void OnDisable()
        {
            Cursor.visible = true;
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || targetCamera == null) return;

            Vector2 screen = mouse.position.ReadValue();
            Vector3 world = targetCamera.ScreenToWorldPoint(
                new Vector3(screen.x, screen.y, -targetCamera.transform.position.z));
            world.z = 0f;

            AimWorldPosition = world;
            if (crosshair != null) crosshair.position = world;
        }
    }
}
