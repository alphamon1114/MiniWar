using System.IO;
using System.Linq;
using MiniWar.Combat;
using MiniWar.Data;
using MiniWar.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MiniWar.EditorTools
{
    /// <summary>
    /// 테스트 씬을 통째로 만들어 준다. 씬 YAML을 손으로 쓰지 않고
    /// Unity가 직접 오브젝트를 만들게 하므로 fileID·GUID가 어긋날 일이 없다.
    /// 여러 번 눌러도 같은 결과가 나오도록 덮어쓴다.
    /// </summary>
    public static class SceneBuilder
    {
        const string ArtDir = "Assets/Art/Generated";
        const string PrefabDir = "Assets/Prefabs";
        const string SceneDir = "Assets/Scenes";
        const string TownScenePath = SceneDir + "/Town.unity";
        const string DungeonScenePath = SceneDir + "/Dungeon.unity";
        const string EnemyPrefabPath = PrefabDir + "/PFB_Enemy.prefab";
        const string BossPrefabPath = PrefabDir + "/PFB_Boss.prefab";
        const string ProjectilePrefabPath = PrefabDir + "/PFB_Projectile.prefab";
        const string HazardPrefabPath = PrefabDir + "/PFB_Hazard.prefab";
        const string TerrainMatPath = ArtDir + "/MAT_Terrain.mat";
        const string PortraitDir = "Assets/Art/Portraits";
        const string BackdropDir = "Assets/Art/Backdrop";

        // Resources 아래여야 한다. OnGUI는 인스펙터로 참조를 꽂을 자리가 없어서
        // HudStyle이 Resources.Load로 직접 집어 온다.
        const string FontDir = "Assets/Resources/Fonts";

        /// <summary>
        /// 배경이 덮어야 하는 카메라 좌우 거리(m). ortho 3.6이면 세로 7.2m이므로
        /// 화면비 4:1까지 버티는 값이다 — Free Aspect로 창을 옆으로 늘려도 끝이 안 끊긴다.
        /// </summary>
        const float CoverHalfWidth = 15f;

        [MenuItem("MiniWar/씬 생성 (마을 + 던전)")]
        public static void Build()
        {
            // 저장 여부를 묻는 건 메뉴에서 눌렀을 때뿐이다.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            int made = BuildSilent();
            if (made < 0) return;

            EditorUtility.DisplayDialog("완료",
                $"마을과 던전 씬이 생성됐습니다. 던전 {made}개.\n\n" +
                "지금 열려 있는 Town 씬에서 Play를 누르세요.\n\n" +
                "[마을]  A/D 로 걸어다니며 NPC에게 Ctrl 로 말을 겁니다.\n" +
                "        대사를 넘기면 그 NPC의 창이 열립니다.\n" +
                "        일러스트는 Assets/Art/Portraits 에 PORTRAIT_Armory / Market / Gate .png\n" +
                "        배경은 Assets/Art/Backdrop 에 BG_Sky / Castle / Wall / Town / Road .png\n" +
                "        정비병(x=9): 1~4 무기 · Z/X/C/V 강화 · R 보급 · F 전체 보급\n" +
                "        보급 담당관(x=22): W/S 선택 · Enter 구매 또는 장착\n" +
                "        오른쪽 끝 관제병(x=35)에 닿으면 던전 선택창이 열립니다.\n" +
                "        모든 창은 Esc 로 닫습니다.\n\n" +
                "[던전]  A/D 이동 · 스페이스/W 점프 · 좌클릭 사격 · R 장전 · 1~4 전환\n" +
                "        구간 끝 관문은 남은 적을 정리해야 열립니다. 상점은 마을에만 있습니다.", "확인");
        }

        /// <summary>
        /// 대화상자 없이 씬을 만든다. 던전 개수를 돌려주고, 못 만들었으면 −1.
        /// 외부 도구(Unity MCP 등)에서 부를 수 있게 갈라 뒀다 —
        /// 모달 대화상자는 사람이 누르기 전까지 호출한 쪽을 멈춰 세운다.
        /// </summary>
        public static int BuildSilent()
        {
            // 묻지 않고 그냥 저장한다. 외부 호출에는 누를 사람이 없고,
            // 어차피 아래에서 씬을 새로 만들어 덮어쓴다.
            EditorSceneManager.SaveOpenScenes();

            // 밖에서 폴더에 떨군 PNG는 아직 AssetDatabase에 없다. 이걸 먼저 하지 않으면
            // 아래 FindAssets가 전부 빈손으로 돌아오고, 배경이 통째로 조용히 빠진다.
            // ─ 실제로 한 번 물렸다. 에디터가 포커스를 받기 전에 씬을 만들면 이렇게 된다.
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            int playerLayer = LayerMask.NameToLayer("Player");
            int terrainLayer = LayerMask.NameToLayer("Terrain");
            if (enemyLayer < 0 || terrainLayer < 0)
            {
                EditorUtility.DisplayDialog("레이어 없음",
                    "Enemy / Terrain 레이어가 없습니다.\nProject Settings → Tags and Layers에서 " +
                    "User Layer 6~9를 Player / Enemy / EnemyProjectile / Terrain으로 설정하세요.", "확인");
                return -1;
            }

            EnsureFolder(ArtDir);
            EnsureFolder(PrefabDir);
            EnsureFolder(SceneDir);
            EnsureFolder(PortraitDir);
            EnsureFolder(BackdropDir);
            EnsureFolder(FontDir);
            EnsurePortraitImport();
            EnsureBackdropImport();
            EnsureFontImport();

            // 무기 칸·가격·그림을 먼저 맞춘다. 던전보다 먼저여야 세션이 온전한 목록을 받는다.
            WeaponAssetBuilder.EnsureAssets();

            // 던전·보스·목록 에셋이 없으면 만든다. 없으면 씬을 만들어도 진행이 안 된다.
            var catalog = StageAssetBuilder.EnsureAssets();

            var square = MakeSquare("SQ_White", 16);
            var crosshairSprite = MakeCrosshair("SQ_Crosshair", 64);
            var terrainMat = MakeTerrainMaterial();
            var enemyPrefab = BuildEnemyPrefab(square, enemyLayer);
            var bossPrefab = BuildBossPrefab(square, enemyLayer);
            var projectilePrefab = BuildProjectilePrefab(square);
            var hazardPrefab = BuildHazardPrefab(square);

            BuildDungeonScene(square, crosshairSprite, terrainMat, enemyPrefab, bossPrefab,
                              projectilePrefab, hazardPrefab, playerLayer, enemyLayer, terrainLayer);
            BuildTownScene(square);

            RegisterScenes();

            // 인던 구조 이전의 단일 씬. 남겨두면 어느 씬에서 Play해야 하는지 헷갈린다.
            const string legacyScene = SceneDir + "/Playtest.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(legacyScene) != null)
            {
                AssetDatabase.DeleteAsset(legacyScene);
                Debug.Log("구 Playtest.unity를 지웠습니다 — Town / Dungeon으로 대체됨");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 마을이 시작 씬이다. 여기서 Play를 눌러야 전체 흐름이 돈다.
            EditorSceneManager.OpenScene(TownScenePath);

            int count = catalog != null ? catalog.Count : 0;
            Debug.Log($"씬 생성 완료: {TownScenePath} · {DungeonScenePath} (던전 {count}개)");
            return count;
        }

        /// <summary>던전 씬 — 지형·전투·보스. 예전 Playtest 씬의 내용이 그대로 온다.</summary>
        static void BuildDungeonScene(Sprite square, Sprite crosshairSprite, Material terrainMat,
                                      GameObject enemyPrefab, GameObject bossPrefab,
                                      GameObject projectilePrefab, GameObject hazardPrefab,
                                      int playerLayer, int enemyLayer, int terrainLayer)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── 카메라 ── 흑백 실루엣이므로 배경은 흰색
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            var rig = camGo.AddComponent<CameraRig>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.97f, 0.97f, 0.96f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            // ── 플레이어 ── 몸체와 총구를 자식으로 둔다. 몸체 스케일이 총구 위치를 왜곡하지 않게.
            var player = new GameObject("Player") { layer = playerLayer >= 0 ? playerLayer : 0 };
            player.transform.position = new Vector3(0f, 0f, 0f);
            var motor = player.AddComponent<PlayerMotor>();

            var playerBody = MakeSprite("Body", square, Color.black, 0);
            playerBody.transform.SetParent(player.transform, false);
            playerBody.transform.localScale = new Vector3(0.5f, 1.4f, 1f);

            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(player.transform, false);
            muzzle.transform.localPosition = new Vector3(0.45f, 0.35f, 0f);

            // 근접 연출 — 휘두를 때만 잠깐 켜진다. 월드에 떠 있으므로 플레이어 자식이 아니다.
            var swing = MakeSprite("MeleeSwing", square, new Color(0.12f, 0.12f, 0.14f, 0.55f), 4);
            var swingRenderer = swing.GetComponent<SpriteRenderer>();
            swingRenderer.enabled = false;

            // ── 지형 ── 앞으로 뻗는 능선. 탄을 막는다.
            var terrainGo = new GameObject("Terrain") { layer = terrainLayer };
            var terrain = terrainGo.AddComponent<TerrainGenerator>();
            var terrainSo = new SerializedObject(terrain);
            terrainSo.FindProperty("follow").objectReferenceValue = player.transform;
            terrainSo.FindProperty("surfaceMaterial").objectReferenceValue = terrainMat;
            terrainSo.ApplyModifiedPropertiesWithoutUndo();

            // ── 조준점 ──
            var crosshair = MakeSprite("Crosshair", crosshairSprite, new Color(0.1f, 0.1f, 0.1f, 0.85f), 90);
            crosshair.transform.localScale = Vector3.one * 0.6f;

            // ── 탄 풀 ── 아군 탄과 적 탄을 따로 둔다. 보스 탄막이 플레이어 탄을 굶기면 안 된다.
            var poolGo = new GameObject("ProjectilePool");
            var pool = poolGo.AddComponent<ProjectilePool>();
            Wire(pool, "prefab", projectilePrefab.GetComponent<Projectile>());

            var hazardGo = new GameObject("HazardPool");
            var hazards = hazardGo.AddComponent<HazardPool>();
            Wire(hazards, "prefab", hazardPrefab.GetComponent<HazardProjectile>());

            // ── 세션 ── 마을에서 넘어왔다면 이 오브젝트는 스스로 사라진다.
            var session = BuildSession();

            // ── 시스템 ──
            var systems = new GameObject("Systems");
            var aim = systems.AddComponent<PlayerAim>();
            var weaponController = systems.AddComponent<WeaponController>();
            var runner = systems.AddComponent<GameRunner>();
            systems.AddComponent<TestHud>();

            Wire(rig, "player", motor);
            WireAim(aim, crosshair.transform, cam);
            WireWeaponController(weaponController, aim, muzzle.transform, pool,
                                 1 << enemyLayer, 1 << terrainLayer, swingRenderer);
            WireRunner(runner, enemyPrefab, bossPrefab, motor, rig, weaponController, hazards, session);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, DungeonScenePath);
        }

        /// <summary>
        /// 마을 씬. 걸어다니는 1자 맵이다.
        ///
        /// 메뉴를 공간으로 펼친 이유는 분위기 때문만이 아니다 — 정비소와 무기상이
        /// 다른 자리에 있으면 "강화하러 가는 길에 무기상을 지나친다"는 동선이 생기고,
        /// 그 동선이 준비 순서를 스스로 제안한다. 탭 전환 화면에는 없는 것이다.
        /// </summary>
        static void BuildTownScene(Sprite square)
        {
            const float Ground = -2.2f;
            const float LeftWall = -6f;
            const float RightWall = 38f;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── 카메라 ──
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.orthographic = true;

            // 던전(5)보다 당겨 잡는다. 마을은 싸우는 곳이 아니라 보는 곳이라
            // 넓게 잡으면 건물이 화면을 꽉 채워 하늘도 성벽도 안 보인다.
            // 배경 레이어의 높이값이 전부 이 값을 전제로 계산돼 있다.
            cam.orthographicSize = 3.6f;

            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            // 헤이즈 판이 덮으므로 보통은 안 보인다. 보인다면 그림이 빠진 것이고,
            // 그때 흰 화면보다는 황혼 그늘에 가까운 쪽이 덜 어색하다.
            cam.backgroundColor = new Color(0.11f, 0.09f, 0.14f);

            var townCam = camGo.AddComponent<TownCamera>();

            // ── 배경 ── 겹마다 속도가 달라야 깊이가 생긴다. 카메라를 만든 직후에 세운다.
            BuildBackdrop(camGo.transform);

            // ── 지면 ── 던전과 달리 평지 한 장이면 된다.
            var ground = MakeSprite("Ground", square, new Color(0.10f, 0.10f, 0.11f), -5);
            ground.transform.position = new Vector3(16f, Ground - 4f, 0f);
            ground.transform.localScale = new Vector3(110f, 8f, 1f);

            // ── 플레이어 ── 던전과 같은 몸으로 걷는다. 지형이 없으면 -2.2를 바닥으로 삼는다.
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0f, 0f);
            var motor = player.AddComponent<PlayerMotor>();

            // 배경이 어두운 갈색으로 깔리면서 검은 사각형은 그 안에 묻힌다.
            // 플레이어는 배경 팔레트에 없는 밝은 값이어야 눈이 먼저 찾는다.
            var body = MakeSprite("Body", square, new Color(0.94f, 0.91f, 0.84f), 0);
            body.transform.SetParent(player.transform, false);
            body.transform.localScale = new Vector3(0.5f, 1.4f, 1f);

            Wire(townCam, "target", player.transform);
            var camSo = new SerializedObject(townCam);
            camSo.FindProperty("minX").floatValue = LeftWall;
            camSo.FindProperty("maxX").floatValue = RightWall;
            camSo.ApplyModifiedPropertiesWithoutUndo();

            // 배경에 실제 건물과 아치가 그려지면서 천막·게이트 사각형은 치웠다.
            // 자리를 알려주는 건 이제 NPC와 그 위의 이름표다 — 검은 상자를 덧대면
            // 그림 위에 종이를 붙인 것처럼 보이고, 광장을 비워둔 뜻도 사라진다.

            // NPC 대사는 규칙을 가르치는 자리다 — 튜토리얼 문구를 따로 띄우는 것보다
            // 사람이 말해주는 쪽이 읽히고, 두 번째부터는 짧은 인사로 바뀐다.
            MakeNpc(square, "정비병", "강화 · 보급", TownStation.Armory, 9f, Ground, false,
                new[]
                {
                    "어서 와. 총 상태 좀 볼까?",
                    "강화는 돈만 있으면 얼마든지 해줄게.\n대신 값이 살 때마다 배로 뛰어. 미리 말해두는 거야.",
                },
                new[] { "또 왔구나. 뭘 손봐줄까?" });

            MakeNpc(square, "보급 담당관", "무기 구매 · 장착", TownStation.Market, 22f, Ground, false,
                new[]
                {
                    "오, 살아 돌아왔네. 물건 좀 보고 갈래?",
                    "칸마다 하나씩만 들고 나갈 수 있어.\n주무기 · 보조 · 근접 · 특수킷 — 딱 네 자리야.",
                },
                new[] { "골라봐. 값은 그대로야." });

            MakeNpc(square, "관제병", "출격 · 던전 선택", TownStation.Gate, 35f, Ground, true,
                new[]
                {
                    "출격 신청이지? ...탄약은 확인했고?",
                    "한 번 들어가면 안에선 아무것도 못 사.\n돌아올 준비는 나가기 전에 끝내는 거야.",
                },
                new[] { "어느 쪽으로 보낼까?" });

            // ── 세션 · 마을 관리 ──
            var session = BuildSession();

            var townGo = new GameObject("Town");
            var town = townGo.AddComponent<TownController>();
            townGo.AddComponent<TownHud>();

            var so = new SerializedObject(town);
            so.FindProperty("sessionInScene").objectReferenceValue = session;
            so.FindProperty("walker").objectReferenceValue = motor;
            so.FindProperty("leftWall").floatValue = LeftWall;
            so.FindProperty("rightWall").floatValue = RightWall;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TownScenePath);
        }

        static void MakeNpc(Sprite square, string displayName, string role, TownStation station,
                            float x, float ground, bool autoOpen, string[] lines, string[] repeatLines)
        {
            var go = new GameObject($"NPC_{displayName}");
            go.transform.position = new Vector3(x, ground + 0.65f, 0f);

            // 게이트도 몸을 세운다. 예전엔 아치 사각형이 표지 노릇을 했지만
            // 그걸 치웠으므로, 서 있는 사람이 없으면 이름표만 허공에 뜬다.
            // 플레이어(밝은 크림)와도, 배경(갈색)과도 구별되는 값.
            var body = MakeSprite("Body", square, new Color(0.60f, 0.67f, 0.79f), 1);
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(0.5f, 1.3f, 1f);

            var npc = go.AddComponent<TownNpc>();
            var so = new SerializedObject(npc);
            so.FindProperty("station").enumValueIndex = (int)station;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("role").stringValue = role;
            so.FindProperty("radius").floatValue = autoOpen ? 2.0f : 2.4f;
            so.FindProperty("autoOpen").boolValue = autoOpen;
            so.FindProperty("portrait").objectReferenceValue = FindPortrait(station);

            // 전신 그림에서 허리 위만 쓴다. (0,0)이 왼쪽 아래라 y 0.46이면 위쪽 54%다.
            so.FindProperty("portraitCrop").rectValue = new Rect(0.18f, 0.40f, 0.60f, 0.57f);

            SetStringArray(so.FindProperty("lines"), lines);
            SetStringArray(so.FindProperty("repeatLines"), repeatLines);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetStringArray(SerializedProperty prop, string[] values)
        {
            prop.arraySize = values != null ? values.Length : 0;
            for (int i = 0; i < prop.arraySize; i++)
                prop.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        // ── 마을 배경 ────────────────────────────────────────────

        /// <summary>
        /// 겹겹의 배경. 깊이를 그리는 방법은 하나뿐이다 — <b>멀리 있는 것은 천천히 지나간다</b>.
        /// 그래서 이 함수가 정하는 것도 결국 겹마다의 속도 하나다.
        ///
        /// 각 겹은 가로로 무한 반복되므로(<see cref="ParallaxLayer"/>) 마을이 몇 미터든
        /// 그림은 한 장이면 된다. 파일이 아직 없는 겹은 조용히 건너뛴다 —
        /// 배경 없이도 마을은 돌아가야 한다.
        /// </summary>
        static void BuildBackdrop(Transform cam)
        {
            var root = new GameObject("Backdrop");

            // 높이와 중심y는 카메라 ortho 3.6(세로 7.2m, 지면 -2.2)을 기준으로 맞춘 값이다.
            // 카메라를 당기거나 밀면 여기도 같이 움직여야 한다.
            //
            // 엘소드 마을의 구조를 따랐다 — 건물열이 걷는 줄까지 내려오지 않는다.
            // 아래 1.9m는 아무것도 없는 포장 광장이고, 건물은 그 뒤쪽 끝에 선다.
            // 이 빈 띠가 무대다. 여기를 채우면 플레이어와 NPC가 배경에 묻힌다.
            //
            // 헤이즈 — 지붕선 위는 노을빛, 아래로 갈수록 어두워지는 세로 그라데이션 한 장.
            // 이게 없으면 타일 이음매와 건물 사이 틈으로 카메라 배경색이 그대로 새어
            // 밝은 실선처럼 보인다. 시차 0이라 카메라에 붙어 절대 흐르지 않는다.
            BuildLayer(root, cam, "BG_Haze", 0.00f, 0.00f, 7.4f, -60, 1f, 26f);

            //        파일         시차   중심y   높이  정렬  불투명도  폭(0이면 그림 비율대로)
            BuildLayer(root, cam, "BG_Sky", 0.05f, 4.50f, 5.0f, -50, 1f);

            // 원경 도시는 대부분 성벽 뒤에 숨는다. 성벽 위로 삐져나오는 종탑과 지붕선만 보이면 된다.
            BuildLayer(root, cam, "BG_Castle", 0.20f, 0.45f, 5.9f, -46, 1f);

            BuildLayer(root, cam, "BG_Wall", 0.42f, 0.10f, 5.8f, -42, 1f);
            BuildLayer(root, cam, "BG_Town", 0.70f, 0.35f, 4.1f, -38, 1f);

            // 포장 광장. 지면(정렬 -5)보다 위다. 그림 비율을 버리고 납작하게 쓴다 —
            // 비스듬히 내려다본 포장길은 원래 세로로 눌려 보인다.
            //
            // 윗변이 건물 밑동(-1.7)보다 0.4m 위로 올라온다. 두 그림이 일직선으로 맞닿으면
            // 위아래로 붙여 놓은 것처럼 보이므로, 포장이 건물 아래로 스며들게 겹친다.
            // 겹치는 구간은 텍스처에서 알파로 풀려 있고 같은 자리에 접지 그림자가 들어 있다.
            BuildLayer(root, cam, "BG_Road", 1.00f, -2.45f, 2.3f, -4, 1f, 6f);

            // BG_Fore(전경 소품)는 마을에서 쓰지 않는다. 걷는 줄 위에 물건이 깔리면
            // 플레이어가 그 안에 묻힌다 — 던전 전경으로 돌릴 자리에 남겨둔다.
        }

        /// <summary>
        /// 배경 한 겹. 타일 세 장을 폭 간격으로 깔고 <see cref="ParallaxLayer"/>에 맡긴다.
        ///
        /// 타일 개수는 <b>폭에서 거꾸로 계산한다</b>. 뿌리는 타일 폭 절반 안으로 접히므로
        /// 한쪽에 k장을 깔면 카메라 기준 <c>±k·w</c>까지만 확실히 덮인다 —
        /// 세 장(k=1)이면 ±w뿐이라, 폭 6m짜리 길바닥은 화면이 조금만 넓어져도 끝이 끊긴다.
        /// 실제로 그렇게 끊겼다.
        /// </summary>
        static void BuildLayer(GameObject root, Transform cam, string file, float parallax,
                               float centerY, float worldHeight, int order, float alpha,
                               float widthOverride = 0f)
        {
            var sprite = FindBackdrop(file);
            if (sprite == null) return;

            float aspect = sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 16f / 9f;
            float width = widthOverride > 0f ? widthOverride : worldHeight * aspect;

            var layer = new GameObject($"Layer_{file}");
            layer.transform.SetParent(root.transform, false);
            layer.transform.position = new Vector3(0f, centerY, 0f);

            var size = sprite.bounds.size;
            var scale = new Vector3(size.x > 0f ? width / size.x : 1f,
                                    size.y > 0f ? worldHeight / size.y : 1f, 1f);

            int k = Mathf.Clamp(Mathf.CeilToInt(CoverHalfWidth / width), 1, 12);

            for (int i = -k; i <= k; i++)
            {
                var tile = MakeSprite($"Tile{i + k}", sprite, new Color(1f, 1f, 1f, alpha), order);
                tile.transform.SetParent(layer.transform, false);
                tile.transform.localPosition = new Vector3(width * i, 0f, 0f);
                tile.transform.localScale = scale;
            }

            layer.AddComponent<ParallaxLayer>().Configure(cam, parallax, width);
        }

        static Sprite FindBackdrop(string file)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{file} t:Sprite", new[] { BackdropDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // 이름이 정확히 같아야 한다. Contains로 두면 BG_Town을 찾을 때
                // BG_Town2가 먼저 걸린다 — 합쳐 쓰고 남은 원본이 폴더에 있을 수 있다.
                if (Path.GetFileNameWithoutExtension(path) != file) continue;

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) return sprite;
            }

            // 조용히 넘어가면 배경이 통째로 빠진 채 씬이 만들어지고, 왜인지 알 길이 없다.
            Debug.LogWarning($"배경 {file}을(를) 못 찾았습니다. {BackdropDir}/{file}.png 를 넣고 다시 생성하세요.");
            return null;
        }

        /// <summary>
        /// 배경 PNG의 임포트 설정. Mesh Type이 <b>Full Rect</b>여야 한다 —
        /// 기본값 Tight는 투명한 가장자리를 잘라내서 타일마다 폭이 달라지고,
        /// 그러면 이어붙인 자리가 한 픽셀씩 어긋나 이음매가 눈에 띈다.
        /// </summary>
        static void EnsureBackdropImport()
        {
            foreach (var guid in AssetDatabase.FindAssets("BG_ t:Texture2D", new[] { BackdropDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    dirty = true;
                }
                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    dirty = true;
                }
                if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
                if (importer.wrapMode != TextureWrapMode.Clamp)
                {
                    importer.wrapMode = TextureWrapMode.Clamp;
                    dirty = true;
                }

                // 도트 그림의 생명은 필터링을 끄는 것이다. Bilinear로 두면
                // 확대할 때 픽셀 사이가 뭉개져서 '도트풍'이 아니라 그냥 저해상도가 된다.
                if (importer.filterMode != FilterMode.Point)
                {
                    importer.filterMode = FilterMode.Point;
                    dirty = true;
                }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    dirty = true;
                }
                if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; dirty = true; }

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (settings.spriteMeshType != SpriteMeshType.FullRect)
                {
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    importer.SetTextureSettings(settings);
                    dirty = true;
                }

                if (dirty) importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// 도트 폰트의 임포트 설정. <b>Hinted Raster</b>가 핵심이다 —
        /// 기본값 Smooth는 글자 가장자리를 부드럽게 갈아서, 도트 폰트를 써도
        /// 흐릿한 보통 글꼴처럼 보인다. 글리프 텍스처 필터도 Point로 내린다.
        /// </summary>
        static void EnsureFontImport()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Font", new[] { FontDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
                if (importer == null) continue;

                bool dirty = false;
                if (importer.fontRenderingMode != FontRenderingMode.HintedRaster)
                {
                    importer.fontRenderingMode = FontRenderingMode.HintedRaster;
                    dirty = true;
                }
                if (importer.fontTextureCase != FontTextureCase.Dynamic)
                {
                    // 한글은 글리프가 수천 자다. 정적 아틀라스로 구우면 감당이 안 된다.
                    importer.fontTextureCase = FontTextureCase.Dynamic;
                    dirty = true;
                }
                if (importer.fontSize != 16) { importer.fontSize = 16; dirty = true; }

                if (dirty) importer.SaveAndReimport();

                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (font != null && font.material != null && font.material.mainTexture != null)
                    font.material.mainTexture.filterMode = FilterMode.Point;
            }
        }

        /// <summary>
        /// 일러스트 PNG의 임포트 설정을 맞춘다. alphaIsTransparency가 꺼져 있으면
        /// 잘라낸 배경 가장자리에 흰 테두리가 남는다 — 배경을 지우고도 티가 나는 흔한 원인.
        /// </summary>
        static void EnsurePortraitImport()
        {
            foreach (var guid in AssetDatabase.FindAssets("PORTRAIT_ t:Texture2D", new[] { PortraitDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    dirty = true;
                }
                if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    dirty = true;
                }
                if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; dirty = true; }

                if (dirty) importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// 대사창에 띄울 일러스트를 파일 이름으로 찾는다.
        /// VARCO에서 받은 PNG를 <c>Assets/Art/Portraits/</c>에 아래 이름으로 넣어두면
        /// 씬을 다시 만들 때 자동으로 꽂힌다 — 인스펙터에서 손으로 끌 필요가 없다.
        /// </summary>
        static Sprite FindPortrait(TownStation station)
        {
            string name = station == TownStation.Armory ? "PORTRAIT_Armory"
                        : station == TownStation.Market ? "PORTRAIT_Market"
                        : "PORTRAIT_Gate";

            foreach (var guid in AssetDatabase.FindAssets($"{name} t:Sprite"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains(name)) continue;

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) return sprite;
            }

            Debug.Log($"{name} 일러스트가 없습니다. {PortraitDir}/{name}.png 로 넣으면 자동으로 붙습니다.");
            return null;
        }

        /// <summary>
        /// 두 씬 모두에 GameSession을 하나씩 둔다. 나중에 로드된 쪽이 스스로 사라지므로
        /// 어느 씬에서 Play를 눌러도 게임이 성립한다 — 테스트 속도가 여기서 갈린다.
        /// </summary>
        static GameSession BuildSession()
        {
            var go = new GameObject("GameSession");
            var session = go.AddComponent<GameSession>();

            var weapons = Load<WeaponData>().OrderBy(w => (int)w.role).ThenBy(w => w.price).ToArray();
            var table = Load<UpgradeTable>().FirstOrDefault();
            var catalog = Load<DungeonCatalog>().FirstOrDefault();

            var so = new SerializedObject(session);
            SetArray(so.FindProperty("allWeapons"), weapons);
            so.FindProperty("upgradeTable").objectReferenceValue = table;
            so.FindProperty("catalog").objectReferenceValue = catalog;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (catalog == null)
                Debug.LogWarning("DungeonCatalog가 없습니다. 마을에서 던전을 고를 수 없습니다.");

            return session;
        }

        /// <summary>SceneManager.LoadScene이 이름으로 찾으려면 빌드 설정에 있어야 한다.</summary>
        static void RegisterScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TownScenePath, true),
                new EditorBuildSettingsScene(DungeonScenePath, true),
            };
        }

        // ── 씬 오브젝트 ────────────────────────────────────────────

        static GameObject MakeSprite(string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return go;
        }

        static GameObject BuildEnemyPrefab(Sprite square, int enemyLayer)
        {
            var root = new GameObject("PFB_Enemy") { layer = enemyLayer };

            var body = MakeSprite("Body", square, Color.black, 1);
            body.transform.SetParent(root.transform, false);
            body.layer = enemyLayer;

            var barBack = MakeSprite("HealthBar", square, new Color(0.2f, 0.2f, 0.2f, 0.5f), 2);
            barBack.transform.SetParent(root.transform, false);
            barBack.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            barBack.transform.localScale = new Vector3(1.0f, 0.12f, 1f);

            var pivot = new GameObject("FillPivot");
            pivot.transform.SetParent(barBack.transform, false);
            pivot.transform.localPosition = new Vector3(-0.5f, 0f, 0f);

            var fill = MakeSprite("Fill", square, new Color(0.25f, 0.75f, 0.25f), 3);
            fill.transform.SetParent(pivot.transform, false);
            fill.transform.localPosition = new Vector3(0.5f, 0f, 0f);

            var collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.6f;

            root.AddComponent<EnemyHealth>();
            var view = root.AddComponent<EnemyView>();

            var so = new SerializedObject(view);
            so.FindProperty("body").objectReferenceValue = body.GetComponent<SpriteRenderer>();
            so.FindProperty("healthBarFill").objectReferenceValue = pivot.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>보스. 잡몹과 프리팹을 나눈 이유는 BossController가 붙고 몸집이 다르기 때문이다.</summary>
        static GameObject BuildBossPrefab(Sprite square, int enemyLayer)
        {
            var root = new GameObject("PFB_Boss") { layer = enemyLayer };

            var body = MakeSprite("Body", square, new Color(0.18f, 0.18f, 0.20f), 1);
            body.transform.SetParent(root.transform, false);
            body.layer = enemyLayer;

            // 포구는 몸체 바깥 왼쪽 위 — 조준선이 몸통에서 나오면 어디서 쏘는지 안 보인다.
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(-2.0f, 0.8f, 0f);

            var collider = root.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(4.2f, 2.1f);

            root.AddComponent<EnemyHealth>();
            var boss = root.AddComponent<BossController>();

            var so = new SerializedObject(boss);
            so.FindProperty("body").objectReferenceValue = body.GetComponent<SpriteRenderer>();
            so.FindProperty("muzzle").objectReferenceValue = muzzle.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject BuildProjectilePrefab(Sprite square)
        {
            var root = new GameObject("PFB_Projectile");

            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = new Color(0.12f, 0.12f, 0.12f);
            sr.sortingOrder = 5;
            root.transform.localScale = new Vector3(0.28f, 0.06f, 1f);

            root.AddComponent<Projectile>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>적 탄. 붉은색이라 아군 탄과 한눈에 구분된다.</summary>
        static GameObject BuildHazardPrefab(Sprite square)
        {
            var root = new GameObject("PFB_Hazard");

            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = new Color(0.45f, 0.12f, 0.12f);
            sr.sortingOrder = 6;
            root.transform.localScale = new Vector3(0.55f, 0.18f, 1f);

            root.AddComponent<HazardProjectile>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, HazardPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static Material MakeTerrainMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(TerrainMatPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, TerrainMatPath);
            }
            mat.shader = shader;

            var color = new Color(0.10f, 0.10f, 0.11f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ── 직렬화 필드 연결 ──────────────────────────────────────

        static void Wire(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireAim(PlayerAim aim, Transform crosshair, Camera cam)
        {
            var so = new SerializedObject(aim);
            so.FindProperty("crosshair").objectReferenceValue = crosshair;
            so.FindProperty("targetCamera").objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireWeaponController(WeaponController controller, PlayerAim aim, Transform muzzle,
                                         ProjectilePool pool, int enemyMask, int blockerMask,
                                         SpriteRenderer meleeSwing)
        {
            var so = new SerializedObject(controller);
            so.FindProperty("aim").objectReferenceValue = aim;
            so.FindProperty("muzzle").objectReferenceValue = muzzle;
            so.FindProperty("projectilePool").objectReferenceValue = pool;
            so.FindProperty("enemyMask").intValue = enemyMask;
            so.FindProperty("blockerMask").intValue = blockerMask;
            so.FindProperty("meleeSwing").objectReferenceValue = meleeSwing;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireRunner(GameRunner runner, GameObject enemyPrefab, GameObject bossPrefab,
                               PlayerMotor player, CameraRig rig, WeaponController controller,
                               HazardPool hazards, GameSession session)
        {
            // 평소에는 GameSession이 무기·강화표·던전을 넘겨준다.
            // 아래 대체값은 던전 씬에서 곧바로 Play를 눌렀을 때만 쓰인다.
            var weapons = Load<WeaponData>().OrderBy(w => (int)w.role).ToArray();
            var table = Load<UpgradeTable>().FirstOrDefault();
            var catalog = Load<DungeonCatalog>().FirstOrDefault();
            var stage = catalog != null && catalog.Count > 0 ? catalog.At(0) : null;

            var so = new SerializedObject(runner);
            SetArray(so.FindProperty("fallbackWeapons"), weapons);
            so.FindProperty("fallbackTable").objectReferenceValue = table;
            so.FindProperty("fallbackStage").objectReferenceValue = stage;
            so.FindProperty("sessionInScene").objectReferenceValue = session;
            so.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab;
            so.FindProperty("bossPrefab").objectReferenceValue = bossPrefab;
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("cameraRig").objectReferenceValue = rig;
            so.FindProperty("weaponController").objectReferenceValue = controller;
            so.FindProperty("hazardPool").objectReferenceValue = hazards;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"자동 연결: 무기 {weapons.Length} · 강화표 {(table == null ? "없음" : table.name)} · " +
                      $"던전 {(catalog == null ? 0 : catalog.Count)}개");

            if (catalog == null || catalog.Count == 0)
                Debug.LogWarning("던전 목록이 비어 있습니다. MiniWar → 스테이지 에셋 생성을 실행하세요.");
        }

        static void SetArray(SerializedProperty prop, Object[] values)
        {
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static System.Collections.Generic.IEnumerable<T> Load<T>() where T : ScriptableObject
            => AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(a => a != null);

        // ── 플레이스홀더 스프라이트 ──────────────────────────────

        static Sprite MakeSquare(string name, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            return SaveSprite(tex, name, size);
        }

        static Sprite MakeCrosshair(string name, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            float outer = size * 0.42f, inner = size * 0.34f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                bool ring = d <= outer && d >= inner;
                bool tick = (Mathf.Abs(dx) < 1.2f || Mathf.Abs(dy) < 1.2f) && d <= size * 0.5f && d >= inner * 0.55f;
                bool dot = d <= size * 0.045f;
                px[y * size + x] = (ring || tick || dot)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(255, 255, 255, 0);
            }
            tex.SetPixels32(px);
            return SaveSprite(tex, name, size);
        }

        static Sprite SaveSprite(Texture2D tex, string name, int ppu)
        {
            tex.Apply();
            string path = $"{ArtDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

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
