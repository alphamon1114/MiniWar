using MiniWar.Data;
using UnityEngine;

namespace MiniWar.Combat
{
    /// <summary>
    /// 적의 겉모습과 행동. 플레이어를 향해 전진하다가 사정거리 안에 들어오면 초당 피해를 준다.
    /// 느린 무기로 잡으면 그만큼 오래 맞는다는 것이 이 게임 경제의 유일한 억제 장치라
    /// (권총은 싸지만 오래 걸린다) 이 값은 밸런싱에서 매우 민감하다.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer body;
        [SerializeField] Transform healthBarFill;
        [SerializeField] float threatRange = 1.5f;

        EnemyHealth _health;
        Transform _target;
        float _dps;
        float _speed;
        System.Action<float> _onDealDamage;

        public bool IsEngaged { get; private set; }

        void Awake()
        {
            _health = GetComponent<EnemyHealth>();
        }

        public void Bind(EnemyData data, Transform target, System.Action<float> onDealDamage)
        {
            _target = target;
            _dps = data.damagePerSecond;
            _speed = data.moveSpeed;
            _onDealDamage = onDealDamage;

            if (body != null)
            {
                // 흑백 실루엣. 장갑형만 조금 밝게 해서 눈으로 구분되게 한다.
                body.color = data.armor > 0f ? new Color(0.35f, 0.35f, 0.35f) : Color.black;
                float scale = data.isBoss ? 2.2f : (data.armor > 0f ? 1.35f : 1f);
                body.transform.localScale = Vector3.one * scale;
            }

            _health.Damaged += (amount, point) => DamagePopup.Spawn(point, amount);
        }

        void Update()
        {
            if (_health == null || _health.IsDead || _target == null) return;

            float distance = Mathf.Abs(transform.position.x - _target.position.x);
            IsEngaged = distance <= threatRange;

            if (!IsEngaged)
            {
                float dir = Mathf.Sign(_target.position.x - transform.position.x);
                transform.position += new Vector3(dir * _speed * Time.deltaTime, 0f, 0f);
            }
            else
            {
                _onDealDamage?.Invoke(_dps * Time.deltaTime);
            }

            if (healthBarFill != null)
            {
                var s = healthBarFill.localScale;
                s.x = Mathf.Clamp01(_health.HealthRatio);
                healthBarFill.localScale = s;
            }
        }
    }
}
