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

        [Tooltip("근접 공격 연출용 스프라이트. 휘두를 때만 잠깐 켠다.")]
        [SerializeField] SpriteRenderer meleeSwing;

        Loadout _loadout;
        EconomySystem _economy;
        float _nextShotTime;
        float _nextAbsorbNotice;
        float _swingUntil;
        readonly System.Collections.Generic.HashSet<EnemyHealth> _swingHits
            = new System.Collections.Generic.HashSet<EnemyHealth>();

        public event Action<string> Notified;

        public WeaponInstance Current => _loadout?.Current;

        /// <summary>정비소가 열려 있는 동안에는 사격·장전 입력을 받지 않는다.</summary>
        public bool InputLocked { get; set; }

        public void Bind(Loadout loadout, EconomySystem economy)
        {
            _loadout = loadout;
            _economy = economy;
        }

        void Update()
        {
            if (InputLocked) return;
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
                Notified?.Invoke($"장전 실패 — ${weapon.ReloadCost} 필요, 잔액 ${_economy.Money}");
        }

        void HandleFire()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed) return;

            var weapon = _loadout.Current;
            if (weapon == null || Time.time < _nextShotTime) return;

            // 근접 — 탄약도 돈도 들지 않는다. 대가는 거리다.
            if (weapon.IsMelee)
            {
                _nextShotTime = Time.time + 1f / Mathf.Max(0.01f, weapon.ShotsPerSecond);
                Swing(weapon);
                return;
            }

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

        /// <summary>
        /// 근접 일격. 조준 방향으로 사거리만큼 앞을 훑어 닿는 적을 전부 때린다.
        ///
        /// 공짜 피해라 경제의 구멍처럼 보이지만, 그 거리에 서 있는 동안은
        /// 적의 초당 피해를 그대로 맞는다 — 돈 대신 <b>체력</b>으로 내는 것이다.
        /// 이 게임에서 체력은 이미 권총 farming의 유일한 제동 장치였고, 여기서도 같다.
        /// </summary>
        void Swing(WeaponInstance weapon)
        {
            if (aim == null) return;

            Vector2 origin = muzzle != null ? (Vector2)muzzle.position : (Vector2)transform.position;
            Vector2 dir = aim.AimWorldPosition - origin;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir.Normalize();

            float range = Mathf.Max(0.2f, weapon.Data.meleeRange);
            Vector2 center = origin + dir * (range * 0.5f);

            _swingHits.Clear();
            var hits = Physics2D.OverlapCircleAll(center, range * 0.5f, enemyMask);

            foreach (var col in hits)
            {
                var enemy = col.GetComponentInParent<EnemyHealth>();
                if (enemy == null || enemy.IsDead || !_swingHits.Add(enemy)) continue;

                float raw = weapon.DamagePerPellet;
                float damage = DamageCalculator.PerPelletAgainst(raw, weapon.Data.piercing, enemy.Armor);
                enemy.ApplyDamage(damage, enemy.transform.position);

                bool absorbed = raw > 0f && damage < raw * 0.6f;
                DamagePopup.Spawn(enemy.transform.position, damage, absorbed);
                OnPelletResolved(raw, damage);
            }

            ShowSwing(center, dir, range);
        }

        void ShowSwing(Vector2 center, Vector2 dir, float range)
        {
            if (meleeSwing == null) return;

            meleeSwing.transform.position = center;
            meleeSwing.transform.right = dir;
            meleeSwing.transform.localScale = new Vector3(range, range * 0.55f, 1f);
            meleeSwing.color = new Color(0.12f, 0.12f, 0.14f, 0.55f);
            meleeSwing.enabled = true;
            _swingUntil = Time.time + 0.09f;
        }

        void LateUpdate()
        {
            if (meleeSwing == null || !meleeSwing.enabled) return;
            if (Time.time >= _swingUntil) meleeSwing.enabled = false;
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
