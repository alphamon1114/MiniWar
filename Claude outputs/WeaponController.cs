using System;
using MiniWar.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniWar.Runtime
{
    /// <summary>
    /// 사격 · 장전 · 무기 전환. 명중 판정은 히트스캔이다 —
    /// 조준점 아래에 적 콜라이더가 있으면 맞는다.
    ///
    /// 피해량은 반드시 <see cref="DamageCalculator"/>를 통과시킨다.
    /// 여기서 따로 계산하면 밸런싱 표와 실제 게임이 갈라진다.
    /// </summary>
    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField] PlayerAim aim;
        [SerializeField] LayerMask enemyMask;
        [SerializeField] float hitRadius = 0.25f;

        Loadout _loadout;
        EconomySystem _economy;
        float _nextShotTime;

        public event Action<string> Notified;

        public WeaponInstance Current => _loadout?.Current;

        public void Bind(Loadout loadout, EconomySystem economy)
        {
            _loadout = loadout;
            _economy = economy;
        }

        void Update()
        {
            if (_loadout == null || _economy == null || _economy.IsBankrupt) return;

            HandleSwitch();
            HandleReload();
            HandleFire();
        }

        void HandleSwitch()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame) _loadout.SelectSlot(1);
            else if (kb.digit2Key.wasPressedThisFrame) _loadout.SelectSlot(2);
            else if (kb.digit3Key.wasPressedThisFrame) _loadout.SelectSlot(3);
            else if (kb.digit4Key.wasPressedThisFrame) _loadout.SelectSlot(4);
            else if (kb.qKey.wasPressedThisFrame) _loadout.SelectNext();
        }

        void HandleReload()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.rKey.wasPressedThisFrame) return;

            var weapon = _loadout.Current;
            if (weapon == null || weapon.IsFull) return;

            if (!_economy.TryReload(weapon))
            {
                Notified?.Invoke($"장전 실패 — ${weapon.ReloadCost} 필요, 잔액 ${_economy.Money}");
            }
        }

        void HandleFire()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed) return;

            var weapon = _loadout.Current;
            if (weapon == null) return;
            if (Time.time < _nextShotTime) return;

            if (weapon.IsEmpty)
            {
                // 빈 총으로 쏘려 하면 바로 파산인지 확인한다.
                _economy.CheckBankruptcy();
                Notified?.Invoke($"{weapon.Data.displayName} 탄창 비었음 — R로 장전 (${weapon.ReloadCost})");
                _nextShotTime = Time.time + 0.4f;
                return;
            }

            _nextShotTime = Time.time + 1f / Mathf.Max(0.01f, weapon.ShotsPerSecond);
            weapon.TryConsumeShot();

            Vector2 point = aim != null ? aim.AimWorldPosition : Vector2.zero;
            var hit = Physics2D.OverlapCircle(point, hitRadius, enemyMask);
            if (hit == null) return;

            var enemy = hit.GetComponentInParent<EnemyHealth>();
            if (enemy == null || enemy.IsDead) return;

            float damage = DamageCalculator.PerShot(weapon, enemy.Armor);
            enemy.ApplyDamage(damage, point);
        }
    }
}
