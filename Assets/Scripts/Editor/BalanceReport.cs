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

        // 단축키는 붙이지 않는다. Ctrl+Shift+B는 Unity의 Build Profiles와 충돌한다.
        [MenuItem("MiniWar/밸런스 표 출력")]
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
            AppendUpgradeLadder(sb, table);
            AppendMarginTable(sb, weapons, enemies, segments, table);
            AppendBossGate(sb, weapons, table);
            AppendStage(sb);
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

        /// <summary>
        /// 강화 가격 사다리. 기하급수라 몇 번째부터 현실적으로 못 사는지가 바로 보인다.
        /// </summary>
        static void AppendUpgradeLadder(StringBuilder sb, UpgradeTable table)
        {
            sb.AppendLine("── 강화 가격 (판 전체 누적 구매 횟수 기준) ──");
            sb.Append("  ");
            int cumulative = 0;
            for (int i = 0; i < 6; i++)
            {
                int cost = table.CostOf(i);
                cumulative += cost;
                sb.Append($"{i + 1}회 ${cost:N0}(누적 ${cumulative:N0})   ");
            }
            sb.AppendLine();
            sb.AppendLine("  판정: 구간에서 벌 수 있는 총액보다 3~4회째가 비싸야 선택이 강제된다.");
            sb.AppendLine();
        }

        /// <summary>
        /// 보스는 페이즈마다 장갑이 다르므로 "총 몇 발"은 의미가 없다.
        /// 페이즈별로 나눠 봐야 <b>어느 형태에서 어느 무기가 죽는지</b>가 드러난다.
        /// </summary>
        static void AppendBossGate(StringBuilder sb, List<WeaponData> weapons, UpgradeTable table)
        {
            var bosses = Load<BossData>().OrderBy(b => b.totalHealth).ToList();
            if (bosses.Count == 0)
            {
                sb.AppendLine("── 보스 ── BossData 에셋이 없습니다. MiniWar → 스테이지 에셋 생성");
                sb.AppendLine();
                return;
            }

            foreach (var boss in bosses) AppendOneBoss(sb, weapons, table, boss);
        }

        static void AppendOneBoss(StringBuilder sb, List<WeaponData> weapons, UpgradeTable table,
                                  BossData boss)
        {
            if (boss.phases == null || boss.phases.Length == 0) return;

            sb.AppendLine($"── 보스 · {boss.displayName} 총 체력 {boss.totalHealth:F0}"
                        + (boss.hoverHeight > 0f ? " (비행 — 곡사 불가)" : "") + " ──");

            foreach (var d in weapons)
            {
                var w = new WeaponInstance(d, table);
                float totalSec = 0f, totalCost = 0f;
                bool stuck = false;

                sb.Append($"  {d.displayName,-8}");

                for (int i = 0; i < boss.phases.Length; i++)
                {
                    var p = boss.phases[i];
                    float hp = boss.totalHealth * p.healthShare;

                    int shots = DamageCalculator.ShotsToKill(w, hp, p.armor);
                    if (shots == int.MaxValue)
                    {
                        sb.Append($" | {p.label}: <무효>");
                        stuck = true;
                        continue;
                    }

                    float sec = DamageCalculator.SecondsToKill(w, hp, p.armor);
                    float cost = shots * w.CostPerShot;
                    totalSec += sec;
                    totalCost += cost;

                    sb.Append($" | {p.label}(장갑{p.armor:F0}): {shots}발 {sec:F0}s ${cost:F0}");
                }

                sb.AppendLine(stuck
                    ? "   => 이 무기만으로는 보스를 못 잡는다"
                    : $"   => 합계 {totalSec:F0}s ${totalCost:F0}");
            }

            sb.AppendLine("  판정: 한 무기가 모든 페이즈에서 최선이면 안 된다. " +
                          "페이즈마다 다른 무기가 싸거나 빨라야 '무기 단위 강화'가 대가를 갖는다.");
            sb.AppendLine();
        }

        static void AppendStage(StringBuilder sb)
        {
            var stages = Load<StageData>().OrderBy(s => s.stageNumber).ToList();
            if (stages.Count == 0) return;

            sb.AppendLine("── 던전 목록 ──");
            foreach (var stage in stages)
            {
                sb.AppendLine($"[{stage.stageNumber}] {stage.displayName} · {stage.TotalLength:F0}m · "
                            + $"보스 {(stage.boss != null ? stage.boss.displayName : "없음")} "
                            + $"x{stage.bossHealthMultiplier:F2} · "
                            + $"클리어 ${stage.clearReward:N0} (최초 +${stage.firstClearBonus:N0})");

                for (int i = 0; i < stage.legs.Length; i++)
                {
                    sb.AppendLine($"    {stage.LabelOf(i),-18} {stage.LegStart(i),5:F0}m ~ "
                                + $"{stage.LegStart(i) + stage.LengthOf(i),5:F0}m  (관문 · 회복 "
                                + $"{stage.healPerCheckpoint:F0})");
                }
                sb.AppendLine($"    {"보스방",-18} {stage.ArenaStart,5:F0}m ~ {stage.TotalLength,5:F0}m");
            }
            sb.AppendLine("  판정: 클리어 보상이 전탄 보급비보다 커야 반복해서 돌 수 있다.");
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
