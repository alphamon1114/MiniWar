using UnityEngine;

namespace MiniWar.Combat
{
    /// <summary>
    /// 날아가는 탄 하나. 두 가지 모드가 있다.
    ///
    /// <b>직사</b> — 조준점은 방향만 정하므로, 조준점을 가까이 두어도 탄은 그 선을 따라
    /// 계속 날아가 뒤에 있는 적에게 맞는다.
    ///
    /// <b>곡사</b> — 조준점이 착탄 지점이 된다. 포물선으로 날아가 지면이나 적에 닿으면 폭발한다.
    ///
    /// 이동은 CircleCast로 처리한다. 프레임당 이동 거리가 적 크기보다 크면
    /// transform.position만 옮길 경우 적을 뚫고 지나가(터널링) 안 맞는다.
    /// </summary>
    public sealed class Projectile : MonoBehaviour
    {
        const float Gravity = 20f;

        float radius = 0.06f;   // Launch에서 탄 크기에 맞춰 설정된다

        ProjectilePool _pool;
        SpriteRenderer _renderer;

        Vector2 _velocity;
        float _damagePerPellet;
        bool _armorPiercing;
        LayerMask _mask;          // 적
        LayerMask _blockerMask;   // 지형 — 능선이 탄을 막는다
        float _remainingRange;
        bool _active;
        bool _ballistic;
        float _groundY;
        float _blastRadius;
        float _blastFalloff;
        System.Action<float, float> _onResolved;   // (원래 위력, 방어력 적용 후)

        // 폭발 섬광 상태
        bool _flashing;
        float _flashAge;

        void Awake() => _renderer = GetComponent<SpriteRenderer>();

        public void Bind(ProjectilePool pool) => _pool = pool;

        /// <summary>직사. direction 방향으로 등속 비행한다.</summary>
        public void Launch(Vector2 origin, Vector2 direction, float speed,
                           float damagePerPellet, bool armorPiercing, LayerMask mask, float range,
                           Vector2 scale, LayerMask blockerMask, System.Action<float, float> onResolved = null)
        {
            Prepare(origin, damagePerPellet, armorPiercing, mask, scale, onResolved);
            _blockerMask = blockerMask;
            _ballistic = false;
            _velocity = direction.normalized * speed;
            _remainingRange = range;
            transform.right = _velocity;
        }

        /// <summary>곡사. target 지점에 flightTime 후 떨어지도록 초기 속도를 역산한다.</summary>
        public void LaunchBallistic(Vector2 origin, Vector2 target, float flightTime,
                                    float damagePerPellet, bool armorPiercing, LayerMask mask,
                                    Vector2 scale, float groundY, float blastRadius, float blastFalloff,
                                    LayerMask blockerMask, System.Action<float, float> onResolved = null)
        {
            Prepare(origin, damagePerPellet, armorPiercing, mask, scale, onResolved);
            _blockerMask = blockerMask;
            _ballistic = true;
            _groundY = groundY;
            _blastRadius = blastRadius;
            _blastFalloff = blastFalloff;
            _remainingRange = float.MaxValue;

            // pos(T) = origin + v0*T - 0.5*g*T^2 = target  →  v0 = (target-origin)/T + 0.5*g*T
            float t = Mathf.Max(0.1f, flightTime);
            _velocity = (target - origin) / t + Vector2.up * (0.5f * Gravity * t);
        }

        void Prepare(Vector2 origin, float damagePerPellet, bool armorPiercing, LayerMask mask,
                     Vector2 scale, System.Action<float, float> onResolved)
        {
            transform.position = origin;
            transform.localScale = new Vector3(scale.x, scale.y, 1f);
            transform.rotation = Quaternion.identity;

            _damagePerPellet = damagePerPellet;
            _armorPiercing = armorPiercing;
            _mask = mask;
            _onResolved = onResolved;
            _blastRadius = 0f;
            _flashing = false;
            _flashAge = 0f;
            _active = true;

            // 큰 탄은 판정도 커야 보이는 대로 맞는다.
            radius = Mathf.Max(0.05f, Mathf.Min(scale.x, scale.y) * 0.5f);

            if (_renderer != null) _renderer.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            gameObject.SetActive(true);
        }

        void Update()
        {
            if (_flashing) { TickFlash(); return; }
            if (!_active) return;

            float dt = Time.deltaTime;
            if (_ballistic) _velocity += Vector2.down * (Gravity * dt);

            Vector2 from = transform.position;
            Vector2 step = _velocity * dt;
            float distance = step.magnitude;
            if (distance <= 0f) { Finish(from); return; }

            Vector2 direction = step / distance;

            // 적과 지형을 함께 검사한다. 먼저 닿는 쪽이 이긴다 —
            // 능선 뒤에 숨은 적은 직사로 못 맞힌다.
            var hit = Physics2D.CircleCast(from, radius, direction, distance, _mask | _blockerMask);
            if (hit.collider != null)
            {
                bool terrain = (_blockerMask.value & (1 << hit.collider.gameObject.layer)) != 0;
                Finish(hit.point, terrain ? null : hit.collider);
                return;
            }

            Vector2 next = from + step;

            if (_ballistic && next.y <= _groundY)
            {
                Finish(new Vector2(next.x, _groundY));
                return;
            }

            transform.position = next;
            if (!_ballistic)
            {
                transform.right = direction;
                _remainingRange -= distance;
                if (_remainingRange <= 0f) Despawn();
            }
        }

        void Finish(Vector2 point, Collider2D direct = null)
        {
            if (_blastRadius > 0f) Explode(point);
            else HitSingle(point, direct);
        }

        void HitSingle(Vector2 point, Collider2D collider)
        {
            if (collider != null)
            {
                var enemy = collider.GetComponentInParent<EnemyHealth>();
                if (enemy != null && !enemy.IsDead) ApplyTo(enemy, point, 1f);
            }
            Despawn();
        }

        /// <summary>반경 안의 모든 적을 때린다. 중심에서 멀수록 피해가 줄어든다.</summary>
        void Explode(Vector2 center)
        {
            var hits = Physics2D.OverlapCircleAll(center, _blastRadius, _mask);
            var seen = new System.Collections.Generic.HashSet<EnemyHealth>();

            foreach (var col in hits)
            {
                var enemy = col.GetComponentInParent<EnemyHealth>();
                if (enemy == null || enemy.IsDead || !seen.Add(enemy)) continue;

                float d = Vector2.Distance(center, enemy.transform.position);
                float t = Mathf.Clamp01(d / Mathf.Max(0.01f, _blastRadius));
                ApplyTo(enemy, enemy.transform.position, Mathf.Lerp(1f, _blastFalloff, t));
            }

            StartFlash(center);
        }

        void ApplyTo(EnemyHealth enemy, Vector2 point, float scale)
        {
            float raw = _damagePerPellet * scale;
            float damage = DamageCalculator.PerPelletAgainst(raw, _armorPiercing, enemy.Armor);
            enemy.ApplyDamage(damage, point);

            bool absorbed = raw > 0f && damage < raw * 0.6f;
            DamagePopup.Spawn(point, damage, absorbed);
            _onResolved?.Invoke(raw, damage);
        }

        void StartFlash(Vector2 center)
        {
            _active = false;
            _flashing = true;
            _flashAge = 0f;
            transform.position = center;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one * (_blastRadius * 2f);
            if (_renderer != null) _renderer.color = new Color(0.25f, 0.25f, 0.28f, 0.55f);
        }

        void TickFlash()
        {
            _flashAge += Time.deltaTime;
            if (_renderer != null)
            {
                var c = _renderer.color;
                c.a = Mathf.Lerp(0.55f, 0f, _flashAge / 0.18f);
                _renderer.color = c;
            }
            if (_flashAge >= 0.18f) Despawn();
        }

        void Despawn()
        {
            _active = false;
            _flashing = false;
            if (_pool != null) _pool.Release(this);
            else gameObject.SetActive(false);
        }
    }
}
