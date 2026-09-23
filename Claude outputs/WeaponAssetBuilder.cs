using System.Collections.Generic;
using System.IO;
using System.Linq;
using MiniWar.Data;
using UnityEditor;
using UnityEngine;

namespace MiniWar.EditorTools
{
    /// <summary>
    /// 무기 격자를 통째로 만든다 — <b>계열 6 × 등급 3 = 18정.</b>
    ///
    /// 18개를 손으로 관리하면 반드시 어긋난다. 그래서 계열마다 <b>기준 스펙 하나</b>만 적고,
    /// 등급은 거기에 배수를 곱해 낸다. 밸런스를 고칠 때 만질 곳이 6줄이라는 뜻이다.
    ///
    /// 그림도 코드로 그린다. 지금 필요한 것은 "왼쪽 아래를 봤을 때 무엇을 들고 있는지
    /// 0.2초 안에 안다"뿐이라 실루엣과 등급 표시만 맞으면 충분하다.
    /// </summary>
    public static class WeaponAssetBuilder
    {
        const string WeaponDir = "Assets/Data/Weapons";
        const string CatalogPath = "Assets/Data/SO_WeaponCatalog.asset";
        const string IconDir = "Assets/Art/Generated/WeaponIcons";
        const int IconW = 96;
        const int IconH = 40;

        /// <summary>계열 하나의 1등급 기준 스펙. 등급은 여기에 배수를 곱한다.</summary>
        struct FamilySpec
        {
            public WeaponFamily family;
            public string[] names;          // 등급 1·2·3의 이름
            public string code;             // 파일명용 영문
            public AmmoMode ammo;
            public float damage;
            public int pellets;
            public bool piercing;
            public int magazine;
            public int reloadCost;
            public float shotsPerSecond;
            public float projectileSpeed;
            public float spread;
            public float range;
            public Vector2 projectileScale;
            public bool ballistic;
            public float flightTime;
            public float blastRadius;
            public float meleeRange;
        }

        // 등급 배수. 위력이 비용보다 빨리 오르므로 등급이 오르면 발당 효율이 조금씩 좋아진다.
        static readonly float[] DamageMult = { 0f, 1.00f, 1.55f, 2.35f };
        static readonly float[] CostMult = { 0f, 1.00f, 1.70f, 2.70f };
        static readonly float[] MagMult = { 0f, 1.00f, 1.25f, 1.50f };

        static readonly FamilySpec[] Specs =
        {
            new FamilySpec
            {
                family = WeaponFamily.Single, code = "Single",
                names = new[] { "권총", "자동권총", "기병총" },
                ammo = AmmoMode.Magazine,
                damage = 25f, pellets = 1, piercing = false,
                magazine = 12, reloadCost = 9, shotsPerSecond = 2.0f,
                projectileSpeed = 22f, spread = 1f, range = 30f,
                projectileScale = new Vector2(0.28f, 0.06f),
            },
            new FamilySpec
            {
                family = WeaponFamily.Auto, code = "Auto",
                names = new[] { "기관단총", "돌격소총", "분대지원화기" },
                ammo = AmmoMode.Magazine,
                damage = 14f, pellets = 1, piercing = false,
                magazine = 30, reloadCost = 240, shotsPerSecond = 8.0f,
                projectileSpeed = 26f, spread = 4.5f, range = 26f,
                projectileScale = new Vector2(0.24f, 0.05f),
            },
            new FamilySpec
            {
                family = WeaponFamily.Shot, code = "Shot",
                names = new[] { "산탄총", "이연발 산탄총", "자동 산탄총" },
                ammo = AmmoMode.Magazine,
                damage = 22f, pellets = 4, piercing = false,
                magazine = 6, reloadCost = 200, shotsPerSecond = 1.1f,
                projectileSpeed = 18f, spread = 11f, range = 18f,
                projectileScale = new Vector2(0.20f, 0.06f),
            },
            new FamilySpec
            {
                family = WeaponFamily.Pierce, code = "Pierce",
                names = new[] { "대전차 소총", "무반동포", "대전차포" },
                ammo = AmmoMode.Magazine,
                damage = 150f, pellets = 1, piercing = true,
                magazine = 4, reloadCost = 520, shotsPerSecond = 0.7f,
                // 탄속이 역할을 강제한다. 권총(22)보다 느려서 움직이는 보병에게는 안 맞는다.
                projectileSpeed = 14f, spread = 0.5f, range = 36f,
                projectileScale = new Vector2(0.55f, 0.18f),
            },
            new FamilySpec
            {
                family = WeaponFamily.Arc, code = "Arc",
                names = new[] { "파편 수류탄", "총류탄", "박격포" },
                ammo = AmmoMode.PerThrow,
                damage = 120f, pellets = 1, piercing = true,
                magazine = 1, reloadCost = 180, shotsPerSecond = 0.8f,
                projectileSpeed = 12f, spread = 0f, range = 24f,
                projectileScale = new Vector2(0.22f, 0.22f),
                ballistic = true, flightTime = 0.85f, blastRadius = 2.0f,
            },
            new FamilySpec
            {
                family = WeaponFamily.Melee, code = "Melee",
                names = new[] { "총검", "참호칼", "전투도끼" },
                ammo = AmmoMode.Melee,
                damage = 58f, pellets = 1, piercing = false,
                magazine = 1, reloadCost = 0, shotsPerSecond = 1.8f,
                projectileSpeed = 1f, spread = 0f, range = 1f,
                projectileScale = new Vector2(0.2f, 0.2f),
                meleeRange = 1.5f,
            },
        };

        [MenuItem("MiniWar/무기 격자 생성")]
        public static void BuildMenu()
        {
            var catalog = EnsureAssets();
            var holes = catalog != null ? catalog.Validate() : new List<string>();

            EditorUtility.DisplayDialog("완료",
                $"무기 {(catalog != null ? catalog.weapons.Length : 0)}정 생성됨 "
                + $"(계열 {WeaponFamilyInfo.Count} × 등급 {WeaponFamilyInfo.MaxTier}).\n\n"
                + (holes.Count == 0 ? "격자가 온전합니다." : "빈 칸: " + string.Join(", ", holes)),
                "확인");
        }

        /// <summary>SceneBuilder가 씬을 만들기 전에 부른다.</summary>
        public static WeaponCatalog EnsureAssets()
        {
            EnsureFolder(WeaponDir);
            EnsureFolder(IconDir);

            var made = new List<WeaponData>();
            var keep = new HashSet<string>();

            foreach (var spec in Specs)
            {
                for (int tier = 1; tier <= WeaponFamilyInfo.MaxTier; tier++)
                {
                    string path = $"{WeaponDir}/SO_Weapon_{spec.code}_T{tier}.asset";
                    keep.Add(path);
                    made.Add(Build(spec, tier, path));
                }
            }

            // 구 로스터(권총·연사형·산탄형…)는 격자에 자리가 없다. 남겨두면
            // 카드 추첨과 합성이 격자 밖 무기를 집어 계열 규칙이 깨진다.
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (keep.Contains(path)) continue;
                AssetDatabase.DeleteAsset(path);
                Debug.Log($"구 무기 에셋 제거: {path}");
            }

            var catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<WeaponCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.weapons = made.ToArray();
            EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return catalog;
        }

        static WeaponData Build(FamilySpec spec, int tier, string path)
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (w == null)
            {
                w = ScriptableObject.CreateInstance<WeaponData>();
                AssetDatabase.CreateAsset(w, path);
            }

            w.displayName = spec.names[tier - 1];
            w.tier = tier;
            w.family = spec.family;
            w.ammoMode = spec.ammo;

            w.damagePerPellet = Mathf.Round(spec.damage * DamageMult[tier]);
            w.pellets = spec.pellets;
            w.piercing = spec.piercing;

            // 근접과 곡사는 탄창 개념이 없으므로 장탄을 늘리지 않는다.
            bool hasMagazine = spec.ammo == AmmoMode.Magazine;
            w.magazineSize = hasMagazine
                ? Mathf.Max(1, Mathf.RoundToInt(spec.magazine * MagMult[tier]))
                : 1;
            w.reloadCost = Mathf.RoundToInt(spec.reloadCost * CostMult[tier]);

            w.shotsPerSecond = spec.shotsPerSecond;
            w.reloadSeconds = 1.2f;
            w.projectileSpeed = spec.projectileSpeed;
            w.spreadDegrees = spec.spread;
            w.projectileRange = spec.range;
            w.projectileScale = spec.projectileScale;

            w.ballistic = spec.ballistic;
            w.flightTime = spec.flightTime > 0f ? spec.flightTime : 0.85f;
            w.blastRadius = spec.blastRadius;
            w.blastFalloff = 0.4f;
            w.meleeRange = spec.meleeRange > 0f ? spec.meleeRange : 1.4f;

            w.icon = MakeIcon(spec, tier);
            EditorUtility.SetDirty(w);
            return w;
        }

        // ── 그림 ─────────────────────────────────────────────────

        /// <summary>가로세로 비율이 1:1이 아닌 도트 뭉치. (x, y, w, h) — y는 아래가 0.</summary>
        struct Part
        {
            public float x, y, w, h;
            public Part(float x, float y, float w, float h) { this.x = x; this.y = y; this.w = w; this.h = h; }
        }

        static List<Part> ShapeOf(WeaponFamily family)
        {
            var p = new List<Part>();

            switch (family)
            {
                case WeaponFamily.Single:
                    p.Add(new Part(0.34f, 0.46f, 0.30f, 0.16f));   // 슬라이드
                    p.Add(new Part(0.62f, 0.49f, 0.16f, 0.09f));   // 총열
                    p.Add(new Part(0.36f, 0.24f, 0.13f, 0.24f));   // 손잡이
                    p.Add(new Part(0.48f, 0.38f, 0.05f, 0.09f));   // 방아쇠울
                    break;

                case WeaponFamily.Auto:
                    p.Add(new Part(0.22f, 0.46f, 0.44f, 0.15f));   // 몸통
                    p.Add(new Part(0.64f, 0.49f, 0.20f, 0.08f));   // 총열
                    p.Add(new Part(0.34f, 0.22f, 0.11f, 0.25f));   // 탄창
                    p.Add(new Part(0.10f, 0.47f, 0.13f, 0.12f));   // 개머리
                    p.Add(new Part(0.50f, 0.36f, 0.05f, 0.11f));   // 손잡이
                    break;

                case WeaponFamily.Shot:
                    p.Add(new Part(0.20f, 0.47f, 0.40f, 0.14f));   // 몸통
                    p.Add(new Part(0.58f, 0.49f, 0.30f, 0.10f));   // 긴 총열
                    p.Add(new Part(0.60f, 0.38f, 0.14f, 0.08f));   // 펌프
                    p.Add(new Part(0.08f, 0.40f, 0.14f, 0.18f));   // 개머리
                    break;

                case WeaponFamily.Pierce:
                    p.Add(new Part(0.16f, 0.44f, 0.30f, 0.22f));   // 후부 통
                    p.Add(new Part(0.44f, 0.48f, 0.44f, 0.13f));   // 굵고 긴 포신
                    p.Add(new Part(0.84f, 0.44f, 0.08f, 0.21f));   // 포구 제퇴기
                    p.Add(new Part(0.30f, 0.22f, 0.05f, 0.23f));   // 다리
                    p.Add(new Part(0.44f, 0.22f, 0.05f, 0.23f));   // 다리
                    p.Add(new Part(0.24f, 0.66f, 0.12f, 0.07f));   // 조준경
                    break;

                case WeaponFamily.Arc:
                    p.Add(new Part(0.38f, 0.22f, 0.24f, 0.40f));   // 몸통
                    p.Add(new Part(0.42f, 0.62f, 0.16f, 0.10f));   // 신관
                    p.Add(new Part(0.58f, 0.44f, 0.06f, 0.26f));   // 안전 손잡이
                    p.Add(new Part(0.64f, 0.66f, 0.10f, 0.05f));   // 안전핀
                    break;

                default:  // Melee
                    p.Add(new Part(0.30f, 0.44f, 0.46f, 0.13f));   // 칼날
                    p.Add(new Part(0.76f, 0.47f, 0.10f, 0.07f));   // 칼끝
                    p.Add(new Part(0.26f, 0.34f, 0.05f, 0.33f));   // 코등이
                    p.Add(new Part(0.12f, 0.44f, 0.14f, 0.12f));   // 손잡이
                    break;
            }

            return p;
        }

        static Sprite MakeIcon(FamilySpec spec, int tier)
        {
            var px = new Color32[IconW * IconH];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

            // 등급이 오르면 조금 밝아진다. 같은 실루엣이라도 손에 든 것이 다르다는 표시.
            byte v = (byte)(26 + (tier - 1) * 34);
            var ink = new Color32(v, v, (byte)(v + 5), 255);

            foreach (var part in ShapeOf(spec.family))
                FillRect(px, part.x, part.y, part.w, part.h, ink);

            // 등급 눈금 — 왼쪽 아래에 tier개. 실루엣이 같은 계열끼리 구분된다.
            var pip = new Color32(230, 190, 90, 255);
            for (int i = 0; i < tier; i++)
                FillRect(px, 0.03f + i * 0.055f, 0.06f, 0.038f, 0.11f, pip);

            var tex = new Texture2D(IconW, IconH, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();

            string path = $"{IconDir}/ICON_{spec.code}_T{tier}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void FillRect(Color32[] px, float fx, float fy, float fw, float fh, Color32 c)
        {
            int x0 = Mathf.RoundToInt(fx * IconW);
            int y0 = Mathf.RoundToInt(fy * IconH);
            int x1 = Mathf.RoundToInt((fx + fw) * IconW);
            int y1 = Mathf.RoundToInt((fy + fh) * IconH);

            for (int y = Mathf.Max(0, y0); y < Mathf.Min(IconH, y1); y++)
            {
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(IconW, x1); x++)
                    px[y * IconW + x] = c;
            }
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
