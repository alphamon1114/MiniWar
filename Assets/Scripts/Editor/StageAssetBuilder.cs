using System.IO;
using System.Linq;
using MiniWar.Data;
using UnityEditor;
using UnityEngine;

namespace MiniWar.EditorTools
{
    /// <summary>
    /// 던전·보스·목록 에셋을 코드로 만든다. YAML을 손으로 쓰지 않는 이유는
    /// 새 스크립트의 GUID를 알 수 없기 때문이다 — Unity에게 만들게 하면 참조가 절대 깨지지 않는다.
    ///
    /// 여러 번 눌러도 안전하다. 이미 있으면 수치만 덮어쓰고 GUID는 유지한다.
    /// </summary>
    public static class StageAssetBuilder
    {
        const string DataDir = "Assets/Data";
        const string DungeonDir = DataDir + "/Dungeons";
        const string SegmentDir = DataDir + "/Segments";
        const string CatalogPath = DataDir + "/SO_DungeonCatalog.asset";
        const string TankBossPath = DataDir + "/SO_Boss_Tank.asset";
        const string AirshipBossPath = DataDir + "/SO_Boss_Airship.asset";

        [MenuItem("MiniWar/스테이지 에셋 생성")]
        public static void BuildMenu()
        {
            var catalog = EnsureAssets();
            EditorUtility.DisplayDialog("완료",
                catalog == null
                    ? "구간(SegmentData) 에셋을 찾지 못했습니다."
                    : $"던전 {catalog.Count}개 생성/갱신됨.\n\n" +
                      string.Join("\n", catalog.dungeons.Select(
                          d => $"· {d.displayName} — {d.legs.Length}구간 {d.TotalLength:F0}m " +
                               $"/ 보스 {(d.boss != null ? d.boss.displayName : "없음")}")),
                "확인");
        }

        /// <summary>SceneBuilder가 씬을 만들기 전에 부른다.</summary>
        public static DungeonCatalog EnsureAssets()
        {
            EnsureFolder(DataDir);
            EnsureFolder(DungeonDir);
            EnsureFolder(SegmentDir);

            EnsureTutorialSegment();
            AssetDatabase.SaveAssets();

            var segments = LoadAll<SegmentData>().OrderBy(s => s.index).ToArray();
            if (segments.Length == 0)
            {
                Debug.LogError("SegmentData 에셋을 찾지 못했습니다. 던전을 만들 수 없습니다. "
                             + "Assets/Data/Segments 아래에 SO_Segment_*.asset 이 있는지 확인하세요.");
                return null;
            }
            Debug.Log($"구간 {segments.Length}개 발견: "
                    + string.Join(" · ", segments.Select(x => $"{x.index}구간 {x.lengthMeters:F0}m")));

            // 던전 구조 이전의 단일 스테이지 에셋. 남겨두면 목록에 없는 유령 던전이 된다.
            const string legacyStage = DataDir + "/SO_Stage_1.asset";
            if (AssetDatabase.LoadAssetAtPath<StageData>(legacyStage) != null)
            {
                AssetDatabase.DeleteAsset(legacyStage);
                Debug.Log("구 SO_Stage_1.asset을 지웠습니다 — 던전 목록으로 대체됨");
            }

            var tank = BuildTankBoss();
            var airship = BuildAirshipBoss();

            // 같은 구간을 조합만 바꿔 던전을 만든다. 구간이 재사용 가능한 부품이라
            // 던전을 늘리는 데 새 스폰표가 매번 필요하지 않다 — 빅샷도 구간을 퀘스트 사이에 옮겼다.
            //
            // 구간은 배열 위치가 아니라 index로 찾는다. 앞에 훈련 구간이 끼면서
            // 위치가 한 칸씩 밀렸는데, 그걸 눈치 못 채면 던전 구성이 조용히 어긋난다.
            var d0 = BuildDungeon("SO_Dungeon_0", 1, "훈련장 · 외곽 초소",
                "권총 한 자루로 도는 훈련 구역. 보스가 없다. 돈이 모자라면 언제든 여기로.",
                ByIndex(segments, 0), null, 1f, 250, 400, 0f);

            var d1 = BuildDungeon("SO_Dungeon_1", 2, "국경 능선",
                "전초선을 뚫고 능선 너머의 전차를 잡는다. 권총 한 자루로는 벅차다.",
                ByIndex(segments, 1, 2), tank, 1.0f, 400, 600, 34f);

            var d2 = BuildDungeon("SO_Dungeon_2", 3, "보급로 습격",
                "보급로는 방비가 두껍다. 같은 전차지만 더 오래 버틴다.",
                ByIndex(segments, 2, 3), tank, 1.5f, 700, 1000, 34f);

            var d3 = BuildDungeon("SO_Dungeon_3", 4, "포병 진지",
                "전 구간 돌파. 하늘에 떠 있는 비행선은 수류탄이 닿지 않는다.",
                ByIndex(segments, 1, 2, 3), airship, 1.0f, 1200, 1800, 40f);

            var catalog = AssetDatabase.LoadAssetAtPath<DungeonCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DungeonCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.dungeons = new[] { d0, d1, d2, d3 }.Where(d => d != null).ToArray();
            EditorUtility.SetDirty(catalog);

            if (catalog.Count == 0)
            {
                Debug.LogError($"던전이 하나도 만들어지지 않았습니다 (구간 {segments.Length}개). "
                             + "구간이 2개 이상 있어야 1번 던전이 성립합니다.");
            }
            else
            {
                Debug.Log($"던전 {catalog.Count}개 구성: "
                        + string.Join(" · ", catalog.dungeons.Select(
                            d => $"{d.displayName}({d.legs.Length}구간 {d.TotalLength:F0}m)")));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return catalog;
        }

        /// <summary>SegmentData.index로 찾는다. 배열 위치가 아니라 이름표를 쓰는 것이 안전하다.</summary>
        static SegmentData[] ByIndex(SegmentData[] all, params int[] indices)
            => indices.Select(i => all.FirstOrDefault(s => s.index == i))
                      .Where(s => s != null)
                      .ToArray();

        /// <summary>
        /// 훈련 구간. 권총 한 자루로 돌 수 있어야 하므로 장갑차도 헬기도 없고 체력 배율도 낮다.
        ///
        /// 이 구간이 있는 이유는 1번 던전의 보스(장갑 18)가 권총으로는 51초 걸리기 때문이다 —
        /// 시작 장비만으로 첫 던전을 깨라고 하면 첫 실패가 너무 이르다.
        /// 여기서 총검이나 연사형을 살 돈을 벌고 나가는 것이 설계된 순서다.
        /// </summary>
        static SegmentData EnsureTutorialSegment()
        {
            var seg = Ensure<SegmentData>($"{SegmentDir}/SO_Segment_0.asset");

            seg.index = 0;
            seg.lengthMeters = 70f;
            seg.healthMultiplier = 0.7f;
            seg.rewardMultiplier = 1f;

            var rusher = FindEnemy("돌격병");
            var shooter = FindEnemy("소총병");

            if (rusher == null || shooter == null)
            {
                Debug.LogError("훈련 구간을 만들 적(돌격병 / 소총병)을 찾지 못했습니다.");
                return seg;
            }

            seg.spawns = new[]
            {
                Spawn(8f, rusher, 1),
                Spawn(20f, rusher, 2),
                Spawn(34f, shooter, 1),
                Spawn(46f, rusher, 1),
                Spawn(58f, shooter, 1),
            };

            EditorUtility.SetDirty(seg);
            return seg;
        }

        static SpawnEntry Spawn(float distance, EnemyData enemy, int count)
            => new SpawnEntry { distance = distance, enemy = enemy, count = count };

        static EnemyData FindEnemy(string displayName)
            => LoadAll<EnemyData>().FirstOrDefault(e => e.displayName == displayName);

        // ── 보스 ─────────────────────────────────────────────────

        /// <summary>
        /// 대형 전차. 페이즈마다 장갑이 바뀌는 것이 설계의 축이다.
        ///
        /// 1페이즈 장갑 18 — 권총(25)은 7밖에 못 넣어 아주 느리고,
        ///   대전차포(250·관통)는 3방이면 끝난다.
        /// 2페이즈 장갑 0 — 연사형이 갑자기 최고 효율이 된다. 대신 보스가 밀고 들어온다.
        /// 3페이즈 장갑 10 — 중간. 대신 패턴이 가장 빠르다.
        ///
        /// 한 자루만 강화한 플레이어는 셋 중 하나에서 반드시 손해를 본다.
        /// </summary>
        static BossData BuildTankBoss()
        {
            var boss = Ensure<BossData>(TankBossPath);

            boss.displayName = "대형 전차";
            boss.totalHealth = 1800f;
            boss.reward = 0;
            boss.bodySize = new Vector2(4.2f, 2.1f);
            boss.hoverHeight = 0f;
            boss.contactDamagePerSecond = 22f;
            boss.standoffDistance = 5f;

            boss.phases = new[]
            {
                Phase("포탑", 0.40f, 18f, 0f, 2.2f, 26f,
                      new Color(0.18f, 0.18f, 0.20f),
                      "대형 전차 — 포탑이 돌아간다. 장갑이 두껍다",
                      BossAttack.Cannon, BossAttack.Barrage),

                Phase("장갑 이탈", 0.35f, 0f, 1.6f, 1.6f, 20f,
                      new Color(0.34f, 0.30f, 0.30f),
                      "장갑판이 떨어져 나갔다 — 지금은 뭘 쏘든 들어간다",
                      BossAttack.Mortar, BossAttack.Barrage),

                Phase("폭주", 0.25f, 10f, 2.4f, 1.1f, 30f,
                      new Color(0.50f, 0.20f, 0.20f),
                      "폭주 — 밀고 들어온다",
                      BossAttack.Cannon, BossAttack.Mortar, BossAttack.Barrage),
            };

            EditorUtility.SetDirty(boss);
            return boss;
        }

        /// <summary>
        /// 공격 비행선. 떠 있어서 <b>수류탄이 닿지 않는다</b> — 무기 선택이 통째로 달라진다.
        /// 몸통 충돌 피해도 없다. 대신 곡사 폭격으로 발밑을 계속 두드린다.
        /// </summary>
        static BossData BuildAirshipBoss()
        {
            var boss = Ensure<BossData>(AirshipBossPath);

            boss.displayName = "공격 비행선";
            boss.totalHealth = 2400f;
            boss.reward = 0;
            boss.bodySize = new Vector2(5.2f, 1.3f);
            boss.hoverHeight = 4.6f;
            boss.contactDamagePerSecond = 0f;   // 높이 떠 있어 닿지 않는다
            boss.standoffDistance = 6f;

            boss.phases = new[]
            {
                Phase("폭격 항로", 0.35f, 12f, 1.2f, 1.4f, 22f,
                      new Color(0.22f, 0.22f, 0.26f),
                      "공격 비행선 — 발밑을 두드린다. 서 있지 마라",
                      BossAttack.Mortar),

                Phase("측면 포문", 0.35f, 4f, 2.0f, 1.3f, 24f,
                      new Color(0.34f, 0.32f, 0.36f),
                      "측면 포문 개방 — 장갑이 얇아졌다",
                      BossAttack.Barrage, BossAttack.Cannon),

                Phase("강하", 0.30f, 8f, 2.8f, 0.9f, 30f,
                      new Color(0.50f, 0.22f, 0.24f),
                      "고도를 낮춘다 — 전탄 발사",
                      BossAttack.Cannon, BossAttack.Mortar, BossAttack.Barrage),
            };

            EditorUtility.SetDirty(boss);
            return boss;
        }

        static BossPhase Phase(string label, float share, float armor, float speed,
                               float interval, float shell, Color tint, string notice,
                               params BossAttack[] attacks)
            => new BossPhase
            {
                label = label,
                healthShare = share,
                armor = armor,
                moveSpeed = speed,
                attackInterval = interval,
                shellDamage = shell,
                tint = tint,
                enterNotice = notice,
                attacks = attacks,
            };

        // ── 던전 ─────────────────────────────────────────────────

        static StageData BuildDungeon(string fileName, int number, string name, string briefing,
                                      SegmentData[] segments, BossData boss, float bossHealth,
                                      int clearReward, int firstClearBonus, float arenaLength)
        {
            if (segments.Length == 0)
            {
                Debug.LogWarning($"{name}: 구간을 찾지 못해 만들지 않았습니다.");
                return null;
            }

            var stage = Ensure<StageData>($"{DungeonDir}/{fileName}.asset");

            stage.stageNumber = number;
            stage.displayName = name;
            stage.briefing = briefing;
            stage.boss = boss;
            stage.bossHealthMultiplier = bossHealth;
            stage.clearReward = clearReward;
            stage.firstClearBonus = firstClearBonus;
            stage.arenaLength = arenaLength;
            stage.arenaFloorHeight = -2.2f;
            stage.healPerCheckpoint = 60f;

            string[] labels = { "훈련 구역", "전초선", "보급로", "포병 진지" };

            stage.legs = segments.Select((s, i) => new StageLeg
            {
                segment = s,
                label = $"{i + 1}구간 · {(s.index >= 0 && s.index < labels.Length ? labels[s.index] : "전선")}",
            }).ToArray();

            EditorUtility.SetDirty(stage);
            return stage;
        }

        // ── 유틸 ─────────────────────────────────────────────────

        static T Ensure<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static System.Collections.Generic.IEnumerable<T> LoadAll<T>() where T : ScriptableObject
            => AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(a => a != null);

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
