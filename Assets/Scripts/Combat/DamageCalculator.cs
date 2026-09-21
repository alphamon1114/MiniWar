using MiniWar.Data;
using MiniWar.Runtime;
using UnityEngine;

namespace MiniWar.Combat
{
    /// <summary>
    /// 피해 계산의 단일 진실 소스. 밸런싱 표·검산 프로브·실제 사격이 모두 이 함수를 통과해야
    /// "표에서는 맞는데 게임에서는 다른" 사태가 생기지 않는다.
    ///
    /// 규칙: 방어력은 <b>펠릿마다</b> 감산된다. 산탄형이 장갑형 앞에서 급격히 약해지는 이유이자,
    /// 관통(고위력형)이 장갑형의 답이 되는 이유다.
    /// </summary>
    public static class DamageCalculator
    {
        /// <summary>
        /// 방어력을 적용한 펠릿 1개의 피해. 투사체는 무기 인스턴스를 들고 다니지 않으므로
        /// 이 오버로드로 계산한다 — 계산식은 한 군데뿐이어야 한다.
        /// </summary>
        public static float PerPelletAgainst(float damagePerPellet, bool piercing, float armor)
        {
            float effectiveArmor = piercing ? 0f : armor;
            return Mathf.Max(0f, damagePerPellet - effectiveArmor);
        }

        public static float PerPellet(WeaponInstance weapon, float armor)
            => PerPelletAgainst(weapon.DamagePerPellet, weapon.Data.piercing, armor);

        /// <summary>
        /// 1발이 넣는 총 피해. <b>전탄 명중을 가정한 상한</b>이다.
        /// 펠릿이 1개인 무기는 실제와 같지만, 산탄형은 퍼짐 때문에 근거리에서만 이 값에 근접한다.
        /// 밸런싱 표의 산탄형 수치를 "최대치"로 읽어야 하는 이유.
        /// </summary>
        public static float PerShot(WeaponInstance weapon, float armor)
            => PerPellet(weapon, armor) * weapon.Data.pellets;

        /// <summary>처치에 필요한 발수. 피해가 0이면 int.MaxValue (= 이 무기로는 못 잡는다).</summary>
        public static int ShotsToKill(WeaponInstance weapon, float health, float armor)
        {
            float per = PerShot(weapon, armor);
            if (per <= 0f) return int.MaxValue;
            return Mathf.CeilToInt(health / per);
        }

        /// <summary>처치에 드는 탄약비. 못 잡으면 float.PositiveInfinity.</summary>
        public static float CostToKill(WeaponInstance weapon, float health, float armor)
        {
            int shots = ShotsToKill(weapon, health, armor);
            if (shots == int.MaxValue) return float.PositiveInfinity;
            return shots * weapon.CostPerShot;
        }

        /// <summary>처치에 걸리는 시간(초). 장전 시간은 포함하지 않는다.</summary>
        public static float SecondsToKill(WeaponInstance weapon, float health, float armor)
        {
            int shots = ShotsToKill(weapon, health, armor);
            if (shots == int.MaxValue) return float.PositiveInfinity;
            return shots / Mathf.Max(0.01f, weapon.ShotsPerSecond);
        }

        /// <summary>
        /// 킬당 마진 = 보상 - 탄약비. 이 값의 <b>부호</b>가 게임의 난이도를 결정한다.
        /// 구간이 올라갈수록 비싼 총의 마진이 음수로 돌아서야 압박이 생긴다.
        /// </summary>
        public static float KillMargin(WeaponInstance weapon, EnemyData enemy, SegmentData segment)
        {
            float health = segment.HealthOf(enemy);
            float cost = CostToKill(weapon, health, enemy.armor);
            if (float.IsPositiveInfinity(cost)) return float.NegativeInfinity;
            return segment.RewardOf(enemy) - cost;
        }
    }
}
