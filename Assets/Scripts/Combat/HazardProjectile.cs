using MiniWar.Runtime;
using UnityEngine;

namespace MiniWar.Combat
{
    /// <summary>
    /// 적이 쏘는 탄. 플레이어에게는 콜라이더가 없으므로 거리로 판정한다 —
    /// 플레이어는 물리로 움직이지 않고 지형에 스냅되기 때문에, 물리 충돌을 붙이면
    /// 오히려 지형에 끼는 문제가 생긴다.
    ///
    /// 두 종류가 있고, 요구하는 대응이 다른 것이 요점이다.
    /// <b>직사</b>는 능선이 막아준다(숨어라). <b>곡사</b>는 막아주지 않는다(움직여라).
    /// </summary>
    public sealed class HazardProjectile : MonoBehaviour
    {
        const float Gravity = 20f;

        HazardPool _pool;
        SpriteRenderer _renderer;
        Transform _marker;
        SpriteRenderer _markerRenderer;

        Transform _player;
        System.Action<float> _onHitPlayer;

        Vector2 _velocity;
        float _damage;
        float _hitRadius;
        float _blastRadius;
        float _life;
        bool _ballistic;
        bool _active;

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();

            // 착탄 표시. 곡사일 때만 켠다.
            var markerGo = new GameObject("Marker");
            _marker = markerGo.transform;
            _marker.SetParent(transform.parent, false);
            _markerRenderer = markerGo.AddComponent<SpriteRenderer>();
            _markerRenderer.sprite = _renderer != null ? _renderer.sprite : null;
            _markerRenderer.sortingOrder = -4;
            markerGo.SetActive(false);
        }

        public void Bind(HazardPool pool) => _pool = pool;

        /// <summary>직사. 능선에 막힌다.</summary>
        public void LaunchDirect(Vector2 origin, Vector2 direction, float speed, float damage,
                                 Transform player, System.Action<float> onHitPlayer, float range = 40f)
        {
            Prepare(origin, damage, player, onHitPlayer, new Vector2(0.55f, 0.18f));
            _ballistic = false;
            _hitRadius = 0.55f;
            _blastRadius = 0f;
            _velocity = direction.normalized * speed;
            _life = range / Mathf.Max(1f, speed);
            transform.right = _velocity;
        }

        /// <summary>곡사. 능선을 넘어가고, 떨어진 자리에서 터진다.</summary>
        public void LaunchMortar(Vector2 origin, Vector2 target, float flightTime, float damage,
                                 float blastRadius, Transform player, System.Action<float> onHitPlayer)
        {
            Prepare(origin, damage, player, onHitPlayer, new Vector2(0.4f, 0.4f));
            _ballistic = true;
            _hitRadius = 0.5f;
            _blastRadius = blastRadius;
            _life = flightTime + 0.5f;

            float t = Mathf.Max(0.15f, flightTime);
            _velocity = (target - origin) / t + Vector2.up * (0.5f * Gravity * t);

            // 착탄 표시 — 어디에 떨어질지 먼저 보여주지 않으면 피할 수가 없다.
            _marker.position = new Vector3(target.x, GroundAt(target.x) + 0.05f, 0f);
            _marker.localScale = new Vector3(blastRadius * 2f, 0.12f, 1f);
            _markerRenderer.color = new Color(0.15f, 0.15f, 0.15f, 0.45f);
            _marker.gameObject.SetActive(true);
        }

        void Prepare(Vector2 origin, float damage, Transform player,
                     System.Action<float> onHitPlayer, Vector2 scale)
        {
            transform.position = origin;
            transform.rotation = Quaternion.identity;
            transform.localScale = new Vector3(scale.x, scale.y, 1f);

            _damage = damage;
            _player = player;
            _onHitPlayer = onHitPlayer;
            _active = true;

            if (_renderer != null) _renderer.color = new Color(0.45f, 0.12f, 0.12f, 1f);
            gameObject.SetActive(true);
        }

        void Update()
        {
            if (!_active) return;

            float dt = Time.deltaTime;

            _life -= dt;
            if (_life <= 0f) { Despawn(); return; }

            if (_ballistic) _velocity += Vector2.down * (Gravity * dt);

            Vector2 next = (Vector2)transform.position + _velocity * dt;

            // 플레이어 명중
            if (_player != null && Vector2.Distance(next, _player.position) <= _hitRadius)
            {
                Detonate(next, true);
                return;
            }

            // 지면 착탄. 직사와 곡사를 나누는 규칙은 따로 없다 —
            // 직사는 수평으로 날아가니 앞에 솟은 능선에 그대로 박히고,
            // 곡사는 위로 넘어가니 능선 뒤에 떨어진다. 궤적 자체가 규칙이다.
            float ground = GroundAt(next.x);
            if (next.y <= ground)
            {
                Detonate(new Vector2(next.x, ground), false);
                return;
            }

            transform.position = next;
            if (!_ballistic) transform.right = _velocity;
        }

        void Detonate(Vector2 point, bool directHit)
        {
            if (directHit)
            {
                _onHitPlayer?.Invoke(_damage);
            }
            else if (_blastRadius > 0f && _player != null)
            {
                float d = Vector2.Distance(point, _player.position);
                if (d <= _blastRadius)
                {
                    // 중심에서 멀수록 약해진다. 가장자리는 절반.
                    float t = Mathf.Clamp01(d / _blastRadius);
                    _onHitPlayer?.Invoke(_damage * Mathf.Lerp(1f, 0.5f, t));
                }
            }

            Despawn();
        }

        static float GroundAt(float x)
        {
            var terrain = TerrainGenerator.Instance;
            return terrain != null ? terrain.HeightAt(x) : -2.2f;
        }

        void Despawn()
        {
            _active = false;
            if (_marker != null) _marker.gameObject.SetActive(false);
            if (_pool != null) _pool.Release(this);
            else gameObject.SetActive(false);
        }
    }
}
