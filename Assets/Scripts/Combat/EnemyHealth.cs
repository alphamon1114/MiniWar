using System;
using MiniWar.Data;
using UnityEngine;

namespace MiniWar.Combat
{
    /// <summary>
    /// 적 한 마리의 체력과 보상. 구간 배율은 스폰 시점에 <see cref="Setup"/>으로 주입한다.
    /// 피해 숫자 연출과 보상 지급은 이벤트로 밖에 넘긴다.
    ///
    /// 보스도 같은 컴포넌트를 쓴다(<see cref="SetupDirect"/>). 피해 경로가 하나여야
    /// 방어력·관통·피해 숫자 규칙이 잡몹과 보스에서 갈라지지 않는다.
    /// </summary>
    public sealed class EnemyHealth : MonoBehaviour
    {
        [SerializeField] EnemyData data;

        float _health;
        int _reward;
        bool _dead;
        float _armorOverride = -1f;   // 0 이상이면 data.armor 대신 이 값을 쓴다

        /// <summary>(피해량, 월드 위치) — 빨간 피해 숫자를 띄울 때 쓴다.</summary>
        public event Action<float, Vector3> Damaged;

        /// <summary>(보상액, 이 적) — EconomySystem.AddReward로 연결한다.</summary>
        public event Action<int, EnemyHealth> Died;

        public EnemyData Data => data;
        public float Armor => _armorOverride >= 0f ? _armorOverride
                            : (data != null ? data.armor : 0f);
        public float Health => _health;
        public float MaxHealth { get; private set; }
        public float HealthRatio => MaxHealth <= 0f ? 0f : _health / MaxHealth;
        public bool IsDead => _dead;

        /// <summary>표시용 이름. 보스는 EnemyData가 없으므로 따로 들고 있는다.</summary>
        public string Label { get; private set; } = "";

        /// <summary>페이즈 전환 중에는 피해가 들어가지 않는다.</summary>
        public bool Invulnerable { get; set; }

        public void Setup(EnemyData enemyData, SegmentData segment)
        {
            data = enemyData;
            Label = enemyData != null ? enemyData.displayName : "";
            MaxHealth = segment != null ? segment.HealthOf(enemyData) : enemyData.baseHealth;
            _reward = segment != null ? segment.RewardOf(enemyData) : enemyData.baseReward;
            _health = MaxHealth;
            _dead = false;
            _armorOverride = -1f;
            Invulnerable = false;
        }

        /// <summary>보스처럼 EnemyData 없이 수치를 직접 넣는 경우.</summary>
        public void SetupDirect(string label, float maxHealth, float armor, int reward)
        {
            data = null;
            Label = label;
            MaxHealth = Mathf.Max(1f, maxHealth);
            _reward = reward;
            _health = MaxHealth;
            _dead = false;
            _armorOverride = Mathf.Max(0f, armor);
            Invulnerable = false;
        }

        /// <summary>페이즈가 바뀌면 장갑도 바뀐다.</summary>
        public void SetArmor(float armor) => _armorOverride = Mathf.Max(0f, armor);

        /// <summary>
        /// 이미 방어력이 반영된 최종 피해량을 받는다.
        /// 방어력 계산은 <see cref="DamageCalculator"/>에서만 한다 — 두 군데서 계산하면 반드시 어긋난다.
        /// </summary>
        public void ApplyDamage(float finalDamage, Vector3 hitPoint)
        {
            if (_dead || Invulnerable || finalDamage <= 0f) return;

            _health -= finalDamage;
            Damaged?.Invoke(finalDamage, hitPoint);

            if (_health > 0f) return;

            _health = 0f;
            _dead = true;
            Died?.Invoke(_reward, this);
        }
    }
}
