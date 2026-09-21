using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MiniWar.Combat;
using MiniWar.Data;
using MiniWar.Runtime;
using UnityEditor;
using UnityEngine;

namespace MiniWar.EditorTools
{
    /// <summary>
    /// 프로젝트의 모든 데이터 에셋을 스스로 찾아 밸런스 표를 뽑는다.
    /// BalanceProbe와 달리 인스펙터에 에셋을 꽂을 필요가 없다.
    /// 결과는 콘솔과 프로젝트 루트의 BalanceReport.txt 양쪽에 남는다.
    /// </summary>
    public static class BalanceReport
    {
        const string ReportFileName = "BalanceReport.txt";

        [MenuItem("MiniWar/밸런스 표 출력 %#b")]
        public static void Generate()
        {
            var weapons = Load<WeaponData>().OrderBy(w => w.slot).ToList();
            var enemies = Load<EnemyData>().ToList();
            var segments = Load<SegmentData>().OrderBy(s => s.index).ToList();
            var table = Load<UpgradeTable>().FirstOrDefault();

            if (table == null || weapons.Count == 0 || enemies.Count == 0 || segments.Count == 0)
            {
                Debug.LogError($"에셋을 찾지 못했습니다. 무기 {weapons.Count} · 적 {enemies.Count} · " +
                               $"구간 {segments.Count} · 강화표 {(table == null ? 0 : 1)}");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"MiniWar 밸런스 리포트 · Unity {Application.unityVersion}");
            sb.AppendLine($"무기 {weapons.Count} · 적 {enemies.Count} · 구간 {segments.Count}");
            sb.AppendLine();

            AppendWeaponStats(sb, weapons, table);
            AppendMarginTable(sb, weapons, enemies, segments, table);
            AppendBossGate(sb, weapons, enemies, table);
            AppendSegmentComposition(sb, segments);

            string text = sb.ToString();
            Debug.Log(text);

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ReportFileName));
            File.WriteAllText(path, text, new UTF8Encoding(false));
            Debug.Log($"리포트 저장: {path}");
        }

        static IEnumerable<T> Load<T>() where T : ScriptableObject
            => AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(a => a != null);

        static WeaponInstance Build(WeaponData data, UpgradeTable table, UpgradeAxis axis, int levels)
        {
            var w = new WeaponInstance(data, table);
            for (int i = 0; i < levels; i++) w.ApplyUpgrade(axis);
            return w;
        }

        static void AppendWeaponStats(StringBuilder sb, List<WeaponData> weapons, UpgradeTable table)
        {
            sb.AppendLine("── 무기 실효 스탯 (강화 없음) ──");
            foreach (var d in weapons)
            {
                var w = new WeaponInstance(d, table);
                sb.AppendLine($"  [{d.slot}] {d.displayName,-8} 위력 {w.DamagePerPellet,5:F0} x{d.pellets}펠릿" +
                              $" · 장탄 {w.MagazineSize,3} · 장전비 ${w.ReloadCost,4}" +
                              $" · 발당 ${w.CostPerShot,6:F2} · 연사 {w.ShotsPerSecond,4:F1}/s" +
                              (d.piercing ? " · 관통" : ""));
            }
            sb.AppendLine();
        }

        static void AppendMarginTable(StringBuilder sb, List<WeaponData> weapons, List<EnemyData> enemies,
                                      List<SegmentData> segments, UpgradeTable table)
        {
            sb.AppendLine("── 킬당 마진 (보상 - 탄약비) · 강화 없음 ──");
            foreach (var seg in segments)
            {
                sb.AppendLine($"[{seg.index}구간] 체력 x{seg.healthMultiplier} · 보상 x{seg.rewardMultiplier}");
                foreach (var e in enemies.Where(e => !e.isBoss))
                {
                    sb.Append($"  {e.displayName,-14} 보상 ${seg.RewardOf(e),-5}");
                    foreach (var d in weapons)
                    {
                        var w = new WeaponInstance(d, table);
                        float hp = seg.HealthOf(e);
                        int shots = DamageCalculator.ShotsToKill(w, hp, e.armor);
                        if (shots == int.MaxValue) { sb.Append($" | {d.displayName}: 무효"); continue; }
                        float margin = DamageCalculator.KillMargin(w, e, seg);
                        float sec = DamageCalculator.SecondsToKill(w, hp, e.armor);
                        sb.Append($" | {d.displayName}: {margin,+6:F0} ({shots}발 {sec:F1}s)");
                    }
                    sb.AppendLine();
                }
            }
            sb.AppendLine("  판정: 마지막 구간에서 비싼 총의 마진이 음수여야 경제 압박이 성립한다.");
            sb.AppendLine();
        }

        static void AppendBossGate(StringBuilder sb, List<WeaponData> weapons, List<EnemyData> enemies,
                                   UpgradeTable table)
        {
            var boss = enemies.FirstOrDefault(e => e.isBoss);
            if (boss == null) return;

            sb.AppendLine($"── 보스 게이트 · 체력 {boss.baseHealth:F0} 방어력 {boss.armor:F0} 초당피해 {boss.damagePerSecond:F0} ──");
            for (int lv = 0; lv <= table.upgradesPerRun; lv++)
            {
                sb.Append($"  위력 강화 {lv}회");
                foreach (var d in weapons)
                {
                    var w = Build(d, table, UpgradeAxis.Damage, lv);
                    int shots = DamageCalculator.ShotsToKill(w, boss.baseHealth, boss.armor);
                    if (shots == int.MaxValue) { sb.Append($" | {d.displayName}: 무효"); continue; }
                    float sec = DamageCalculator.SecondsToKill(w, boss.baseHealth, boss.armor);
                    float dmg = sec * boss.damagePerSecond;
                    sb.Append($" | {d.displayName}: {shots}발 {sec:F0}s 피해{dmg:F0}");
                }
                sb.AppendLine();
            }
            sb.AppendLine("  판정: 고위력형 위력 강화 0회는 사망, 1회부터 생존이 설계 의도.");
            sb.AppendLine();
        }

        static void AppendSegmentComposition(StringBuilder sb, List<SegmentData> segments)
        {
            sb.AppendLine("── 구간 구성 ──");
            foreach (var seg in segments)
            {
                var tally = new Dictionary<string, int>();
                foreach (var s in seg.spawns)
                {
                    if (s.enemy == null) { tally["** 참조 끊김 **"] = tally.GetValueOrDefault("** 참조 끊김 **") + s.count; continue; }
                    tally[s.enemy.displayName] = tally.GetValueOrDefault(s.enemy.displayName) + s.count;
                }
                sb.AppendLine($"  {seg.index}구간 {seg.lengthMeters:F0}m · " +
                              string.Join(" · ", tally.Select(kv => $"{kv.Key} {kv.Value}")));
            }
        }
    }
}
