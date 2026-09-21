using System;
using MiniWar.Data;
using UnityEngine;

namespace MiniWar.Combat
{
    /// <summary>
    /// 적 한 마리의 체력과 보상. 구간 배율은 스폰 시점에 <see cref="Setup"/>으로 주입한다.
    /// 피해 숫자 연출과 보상 지급은 이벤트로 밖에 넘긴다.
    /// </summary>
    public sealed class EnemyHealth : MonoBehaviour
    {
        [SerializeField] EnemyData data;

        float _health;
        int _reward;
        bool _dead;

        /// <summary>(피해량, 월드 위치) — 빨간 피해 숫자를 띄울 때 쓴다.</summary>
        public event Action<float, Vector3> Damaged;

        /// <summary>(보상액, 이 적) — EconomySystem.AddReward로 연결한다.</summary>
        public event Action<int, EnemyHealth> Died;

        public EnemyData Data => data;
        public float Armor => data != null ? data.armor : 0f;
        public float Health => _health;
        public float MaxHealth { get; private set; }
        public float HealthRatio => MaxHealth <= 0f ? 0f : _health / MaxHealth;
        public bool IsDead => _dead;

        public void Setup(EnemyData enemyData, SegmentData segment)
        {
            data = enemyData;
            MaxHealth = segment != null ? segment.HealthOf(enemyData) : enemyData.baseHealth;
            _reward = segment != null ? segment.RewardOf(enemyData) : enemyData.baseReward;
            _health = MaxHealth;
            _dead = false;
        }

        /// <summary>
        /// 이미 방어력이 반영된 최종 피해량을 받는다.
        /// 방어력 계산은 <see cref="DamageCalculator"/>에서만 한다 — 두 군데서 계산하면 반드시 어긋난다.
        /// </summary>
        public void ApplyDamage(float finalDamage, Vector3 hitPoint)
        {
            if (_dead || finalDamage <= 0f) return;

            _health -= finalDamage;
            Damaged?.Invoke(finalDamage, hitPoint);

            if (_health > 0f) return;

            _health = 0f;
            _dead = true;
            Died?.Invoke(_reward, this);
        }
    }
}
