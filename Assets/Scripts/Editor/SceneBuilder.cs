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
        const string ScenePath = SceneDir + "/Playtest.unity";
        const string EnemyPrefabPath = PrefabDir + "/PFB_Enemy.prefab";
        const string ProjectilePrefabPath = PrefabDir + "/PFB_Projectile.prefab";
        const string TerrainMatPath = ArtDir + "/MAT_Terrain.mat";

        [MenuItem("MiniWar/테스트 씬 생성")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            int playerLayer = LayerMask.NameToLayer("Player");
            int terrainLayer = LayerMask.NameToLayer("Terrain");
            if (enemyLayer < 0 || terrainLayer < 0)
            {
                EditorUtility.DisplayDialog("레이어 없음",
                    "Enemy / Terrain 레이어가 없습니다.\nProject Settings → Tags and Layers에서 " +
                    "User Layer 6~9를 Player / Enemy / EnemyProjectile / Terrain으로 설정하세요.", "확인");
                return;
            }

            EnsureFolder(ArtDir);
            EnsureFolder(PrefabDir);
            EnsureFolder(SceneDir);

            var square = MakeSquare("SQ_White", 16);
            var crosshairSprite = MakeCrosshair("SQ_Crosshair", 64);
            var terrainMat = MakeTerrainMaterial();
            var enemyPrefab = BuildEnemyPrefab(square, enemyLayer);
            var projectilePrefab = BuildProjectilePrefab(square);

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

            // ── 지형 ── 앞으로 끝없이 뻗는 능선. 탄을 막는다.
            var terrainGo = new GameObject("Terrain") { layer = terrainLayer };
            var terrain = terrainGo.AddComponent<TerrainGenerator>();
            var terrainSo = new SerializedObject(terrain);
            terrainSo.FindProperty("follow").objectReferenceValue = player.transform;
            terrainSo.FindProperty("surfaceMaterial").objectReferenceValue = terrainMat;
            terrainSo.ApplyModifiedPropertiesWithoutUndo();

            // ── 조준점 ──
            var crosshair = MakeSprite("Crosshair", crosshairSprite, new Color(0.1f, 0.1f, 0.1f, 0.85f), 90);
            crosshair.transform.localScale = Vector3.one * 0.6f;

            // ── 탄 풀 ──
            var poolGo = new GameObject("ProjectilePool");
            var pool = poolGo.AddComponent<ProjectilePool>();
            var poolSo = new SerializedObject(pool);
            poolSo.FindProperty("prefab").objectReferenceValue = projectilePrefab.GetComponent<Projectile>();
            poolSo.ApplyModifiedPropertiesWithoutUndo();

            // ── 시스템 ──
            var systems = new GameObject("Systems");
            var aim = systems.AddComponent<PlayerAim>();
            var weaponController = systems.AddComponent<WeaponController>();
            var runner = systems.AddComponent<GameRunner>();
            systems.AddComponent<TestHud>();

            Wire(rig, "player", motor);
            WireAim(aim, crosshair.transform, cam);
            WireWeaponController(weaponController, aim, muzzle.transform, pool,
                                 1 << enemyLayer, 1 << terrainLayer);
            WireRunner(runner, enemyPrefab, player.transform, weaponController);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"테스트 씬 생성 완료: {ScenePath}");
            EditorUtility.DisplayDialog("완료",
                $"{ScenePath} 생성됨.\n\n" +
                "A / D 전진·후진 · 스페이스 점프\n" +
                "좌클릭 사격 · R 장전 · 1~5 무기 전환\n" +
                "5번 수류탄은 조준점에 떨어진다 — 능선 너머를 노려보세요.", "확인");
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
                                         ProjectilePool pool, int enemyMask, int blockerMask)
        {
            var so = new SerializedObject(controller);
            so.FindProperty("aim").objectReferenceValue = aim;
            so.FindProperty("muzzle").objectReferenceValue = muzzle;
            so.FindProperty("projectilePool").objectReferenceValue = pool;
            so.FindProperty("enemyMask").intValue = enemyMask;
            so.FindProperty("blockerMask").intValue = blockerMask;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireRunner(GameRunner runner, GameObject enemyPrefab, Transform player,
                               WeaponController controller)
        {
            var weapons = Load<WeaponData>().OrderBy(w => w.slot).ToArray();
            var segments = Load<SegmentData>().OrderBy(s => s.index).ToArray();
            var table = Load<UpgradeTable>().FirstOrDefault();
            var enemies = Load<EnemyData>().Where(e => !e.isBoss).ToArray();

            var so = new SerializedObject(runner);
            SetArray(so.FindProperty("weapons"), weapons);
            SetArray(so.FindProperty("spawnPool"), enemies);
            so.FindProperty("upgradeTable").objectReferenceValue = table;
            so.FindProperty("testSegment").objectReferenceValue = segments.FirstOrDefault();
            so.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab;
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("weaponController").objectReferenceValue = controller;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"자동 연결: 무기 {weapons.Length} · 적 {enemies.Length} · 구간 {segments.Length} · " +
                      $"강화표 {(table == null ? "없음" : table.name)}");
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
