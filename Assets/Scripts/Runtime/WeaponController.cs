using System;
using MiniWar.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniWar.Runtime
{
    /// <summary>
    /// 사격 · 장전 · 무기 전환.
    ///
    /// 명중은 투사체 방식이다. 조준점은 <b>방향만</b> 정하고 탄은 그 선을 따라 날아가므로,
    /// 조준점을 가까이 두어도 뒤에 있는 적이 맞는다. 탄속이 있으니 움직이는 적은 예측해서 쏴야 한다.
    ///
    /// 피해량 계산은 <see cref="DamageCalculator"/>에서만 한다 —
    /// 여기서 따로 계산하면 밸런싱 표와 실제 게임이 갈라진다.
    /// </summary>
    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField] PlayerAim aim;
        [SerializeField] Transform muzzle;
        [SerializeField] ProjectilePool projectilePool;
        [SerializeField] LayerMask enemyMask;
        [Tooltip("탄을 막는 지형 레이어. 능선 뒤의 적은 직사로 맞힐 수 없다.")]
        [SerializeField] LayerMask blockerMask;

        Loadout _loadout;
        EconomySystem _economy;
        float _nextShotTime;
        float _nextAbsorbNotice;

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
            else if (kb.digit5Key.wasPressedThisFrame) _loadout.SelectSlot(5);
            else if (kb.qKey.wasPressedThisFrame) _loadout.SelectNext();
        }

        void HandleReload()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.rKey.wasPressedThisFrame) return;

            var weapon = _loadout.Current;
            if (weapon == null || weapon.IsFull) return;

            if (!_economy.TryReload(weapon))
                Notified?.Invoke($"장전 실패 — ${weapon.ReloadCost} 필요, 잔액 ${_economy.Money}");
        }

        void HandleFire()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed) return;

            var weapon = _loadout.Current;
            if (weapon == null || Time.time < _nextShotTime) return;

            // 특수킷 — 탄창 대신 쓸 때마다 돈이 나간다.
            if (weapon.IsPerThrow)
            {
                if (!_economy.TrySpend(weapon.UseCost))
                {
                    Notified?.Invoke($"{weapon.Data.displayName} 투척 실패 — ${weapon.UseCost} 필요, 잔액 ${_economy.Money}");
                    _nextShotTime = Time.time + 0.4f;
                    return;
                }

                _nextShotTime = Time.time + 1f / Mathf.Max(0.01f, weapon.ShotsPerSecond);
                FireProjectiles(weapon);
                return;
            }

            if (weapon.IsEmpty)
            {
                _economy.CheckBankruptcy();   // 빈 총 = 파산 조건 확인 시점
                Notified?.Invoke($"{weapon.Data.displayName} 탄창 비었음 — R로 장전 (${weapon.ReloadCost})");
                _nextShotTime = Time.time + 0.4f;
                return;
            }

            _nextShotTime = Time.time + 1f / Mathf.Max(0.01f, weapon.ShotsPerSecond);
            weapon.TryConsumeShot();
            FireProjectiles(weapon);
        }

        void FireProjectiles(WeaponInstance weapon)
        {
            if (projectilePool == null || aim == null) return;

            Vector2 origin = muzzle != null ? (Vector2)muzzle.position : (Vector2)transform.position;
            Vector2 toAim = aim.AimWorldPosition - origin;
            if (toAim.sqrMagnitude < 0.0001f) return;

            var data = weapon.Data;

            // 곡사는 조준점이 착탄 지점이다 — 직사의 "방향만 정한다"와 반대.
            if (data.ballistic)
            {
                var grenade = projectilePool.Get();
                if (grenade == null) return;

                grenade.LaunchBallistic(origin, aim.AimWorldPosition, data.flightTime,
                                        weapon.DamagePerPellet, data.piercing, enemyMask,
                                        data.projectileScale, GroundUnder(aim.AimWorldPosition.x),
                                        data.blastRadius, data.blastFalloff, blockerMask, OnPelletResolved);
                return;
            }

            Vector2 baseDirection = toAim.normalized;
            float halfSpread = data.spreadDegrees * 0.5f;

            for (int i = 0; i < data.pellets; i++)
            {
                var projectile = projectilePool.Get();
                if (projectile == null) return;   // 풀 상한 도달

                Vector2 direction = halfSpread > 0f
                    ? Rotate(baseDirection, UnityEngine.Random.Range(-halfSpread, halfSpread))
                    : baseDirection;

                projectile.Launch(origin, direction, data.projectileSpeed,
                                  weapon.DamagePerPellet, data.piercing, enemyMask, data.projectileRange,
                                  data.projectileScale, blockerMask, OnPelletResolved);
            }
        }

        /// <summary>
        /// 장갑에 막히고 있다는 걸 말로도 알려준다. 숫자가 작게 뜨는 것만으로는
        /// "내 총이 약한 건지 저 적이 단단한 건지"를 구분하기 어렵다.
        /// </summary>
        void OnPelletResolved(float raw, float final)
        {
            if (raw <= 0f || final >= raw * 0.6f) return;
            if (Time.time < _nextAbsorbNotice) return;

            _nextAbsorbNotice = Time.time + 2f;
            Notified?.Invoke(final <= 0f
                ? "장갑에 완전히 막힘 — 4번 고위력형은 방어력을 무시한다"
                : $"장갑에 막힘 ({raw:F0} → {final:F0}) — 4번 고위력형은 방어력을 무시한다");
        }

        static float GroundUnder(float x)
        {
            var terrain = TerrainGenerator.Instance;
            return terrain != null ? terrain.HeightAt(x) : -2.2f;
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }
    }
}
