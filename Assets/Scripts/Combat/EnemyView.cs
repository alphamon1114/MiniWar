using MiniWar.Data;
using MiniWar.Runtime;
using UnityEngine;

namespace MiniWar.Combat
{
    /// <summary>
    /// 적의 겉모습과 행동. 지상형은 지면을 따라 밀려오고, 비행형은 고도를 유지하며 빠르게 접근한다.
    ///
    /// 느린 무기로 잡으면 그만큼 오래 맞는다는 것이 이 게임 경제의 유일한 억제 장치라
    /// (권총은 싸지만 오래 걸린다) 초당 피해 값은 밸런싱에서 매우 민감하다.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer body;
        [SerializeField] Transform healthBarFill;

        EnemyHealth _health;
        Transform _target;
        EnemyData _data;
        float _threatRange;
        float _bobSeed;
        float _groundY;
        float _footOffset;

        public bool IsEngaged { get; private set; }

        void Awake() => _health = GetComponent<EnemyHealth>();

        public void Bind(EnemyData data, Transform target, System.Action<float> onDealDamage)
        {
            _data = data;
            _target = target;
            _onDealDamage = onDealDamage;
            _bobSeed = Random.Range(0f, 10f);
            _groundY = transform.position.y;
            _footOffset = data.bodySize.y * 0.5f;

            // 비행형은 더 멀리서 공격한다 — 접근을 끊으려면 원거리에서 격추해야 한다.
            _threatRange = data.locomotion == LocomotionKind.Air ? 4.5f : 1.5f;

            if (body != null)
            {
                body.color = Silhouette(data);
                body.transform.localScale = new Vector3(data.bodySize.x, data.bodySize.y, 1f);
            }

            if (healthBarFill != null)
            {
                var bar = healthBarFill.parent;
                if (bar != null) bar.localPosition = new Vector3(0f, data.bodySize.y * 0.5f + 0.35f, 0f);
            }
        }

        System.Action<float> _onDealDamage;

        /// <summary>흑백 실루엣 안에서도 위협도가 읽히도록 명도를 나눈다.</summary>
        static Color Silhouette(EnemyData data)
        {
            if (data.isBoss) return new Color(0.18f, 0.18f, 0.20f);
            if (data.armor >= 15f) return new Color(0.30f, 0.30f, 0.33f);   // 장갑차 — 확실히 다르게
            if (data.locomotion == LocomotionKind.Air) return new Color(0.22f, 0.22f, 0.26f);
            return Color.black;                                              // 보병
        }

        void Update()
        {
            if (_health == null || _health.IsDead || _target == null || _data == null) return;

            float distance = Mathf.Abs(transform.position.x - _target.position.x);
            IsEngaged = distance <= _threatRange;

            if (!IsEngaged) Advance();
            else _onDealDamage?.Invoke(_data.damagePerSecond * Time.deltaTime);

            if (healthBarFill != null)
            {
                var s = healthBarFill.localScale;
                s.x = Mathf.Clamp01(_health.HealthRatio);
                healthBarFill.localScale = s;
            }
        }

        void Advance()
        {
            float dir = Mathf.Sign(_target.position.x - transform.position.x);
            var pos = transform.position;
            pos.x += dir * _data.moveSpeed * Time.deltaTime;

            if (_data.locomotion == LocomotionKind.Air)
            {
                // 고도로 부드럽게 올라가며 위아래로 흔들린다 — 예측 사격을 조금 어렵게 만든다.
                // 고도는 지면 기준 상대값이다 — 능선이 솟으면 헬기도 같이 올라간다.
                var air = TerrainGenerator.Instance;
                float ground = air != null ? air.HeightAt(pos.x) : -2.2f;
                float bob = Mathf.Sin((Time.time + _bobSeed) * 2.2f) * _data.bobAmplitude;
                pos.y = Mathf.Lerp(pos.y, ground + _data.cruiseAltitude + bob, Time.deltaTime * 3f);
            }
            else
            {
                // 지상형은 능선을 따라 오르내린다.
                var terrain = TerrainGenerator.Instance;
                float h = terrain != null ? terrain.HeightAt(pos.x) + _footOffset : _groundY;
                pos.y = Mathf.Lerp(pos.y, h, Time.deltaTime * 12f);
            }

            transform.position = pos;
        }
    }
}
