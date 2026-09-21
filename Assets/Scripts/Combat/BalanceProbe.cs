using System.Text;
using MiniWar.Data;
using MiniWar.Runtime;
using UnityEngine;

namespace MiniWar.Combat
{
    /// <summary>
    /// 킬당 마진 표를 콘솔에 찍는 검산 도구. 기획서 07-4 표와 대조해
    /// 에셋에 넣은 수치가 설계와 어긋나지 않았는지 확인한다.
    ///
    /// 빈 GameObject에 붙이고 무기·적·구간 에셋을 꽂은 뒤
    /// 인스펙터 우클릭 → "킬당 마진 표 출력".
    /// </summary>
    public sealed class BalanceProbe : MonoBehaviour
    {
        [SerializeField] WeaponData[] weapons;
        [SerializeField] EnemyData[] enemies;
        [SerializeField] SegmentData[] segments;
        [SerializeField] UpgradeTable upgradeTable;

        [Header("강화 가정")]
        [Tooltip("모든 무기에 이 축을 이만큼 강화한 상태로 계산한다. 0이면 강화 없음.")]
        [SerializeField] UpgradeAxis assumedAxis = UpgradeAxis.Damage;
        [SerializeField, Min(0)] int assumedLevels;

        [ContextMenu("킬당 마진 표 출력")]
        public void PrintMarginTable()
        {
            if (!IsReady()) return;

            var sb = new StringBuilder();
            sb.AppendLine($"=== 킬당 마진 (보상 - 탄약비) · 강화 {UpgradeTable.LabelOf(assumedAxis)} {assumedLevels}회 ===");

            foreach (var segment in segments)
            {
                if (segment == null) continue;
                sb.AppendLine($"[{segment.index}구간] 체력 x{segment.healthMultiplier} · 보상 x{segment.rewardMultiplier}");

                foreach (var enemy in enemies)
                {
                    if (enemy == null) continue;
                    sb.Append($"  {enemy.displayName,-12} 보상 ${segment.RewardOf(enemy),-5}");

                    foreach (var weaponData in weapons)
                    {
                        if (weaponData == null) continue;
                        var w = BuildInstance(weaponData);
                        float health = segment.HealthOf(enemy);
                        int shots = DamageCalculator.ShotsToKill(w, health, enemy.armor);

                        if (shots == int.MaxValue)
                        {
                            sb.Append($" | {weaponData.displayName}: 무효");
                            continue;
                        }

                        float margin = DamageCalculator.KillMargin(w, enemy, segment);
                        float seconds = DamageCalculator.SecondsToKill(w, health, enemy.armor);
                        sb.Append($" | {weaponData.displayName}: {margin,+7:F0} ({shots}발 {seconds:F1}s)");
                    }
                    sb.AppendLine();
                }
            }

            sb.AppendLine("체크: 마지막 구간에서 비싼 총의 마진이 음수여야 경제 압박이 성립한다.");
            Debug.Log(sb.ToString(), this);
        }

        [ContextMenu("무기별 실효 스탯 출력")]
        public void PrintWeaponStats()
        {
            if (!IsReady()) return;

            var sb = new StringBuilder();
            sb.AppendLine($"=== 무기 실효 스탯 · 강화 {UpgradeTable.LabelOf(assumedAxis)} {assumedLevels}회 ===");

            foreach (var weaponData in weapons)
            {
                if (weaponData == null) continue;
                var w = BuildInstance(weaponData);
                sb.AppendLine(
                    $"  {w.Data.displayName,-8} 위력 {w.DamagePerPellet,6:F1} x{w.Data.pellets}펠릿" +
                    $" · 장탄 {w.MagazineSize,3} · 장전비 ${w.ReloadCost,5}" +
                    $" · 발당 ${w.CostPerShot,6:F2} · 연사 {w.ShotsPerSecond,4:F1}/s" +
                    (w.Data.piercing ? " · 관통" : ""));
            }

            Debug.Log(sb.ToString(), this);
        }

        WeaponInstance BuildInstance(WeaponData data)
        {
            var instance = new WeaponInstance(data, upgradeTable);
            for (int i = 0; i < assumedLevels; i++) instance.ApplyUpgrade(assumedAxis);
            return instance;
        }

        bool IsReady()
        {
            if (upgradeTable == null) { Debug.LogError("UpgradeTable이 비어 있습니다.", this); return false; }
            if (weapons == null || weapons.Length == 0) { Debug.LogError("무기 목록이 비어 있습니다.", this); return false; }
            if (enemies == null || enemies.Length == 0) { Debug.LogError("적 목록이 비어 있습니다.", this); return false; }
            if (segments == null || segments.Length == 0) { Debug.LogError("구간 목록이 비어 있습니다.", this); return false; }
            return true;
        }
    }
}
