using System.Collections.Generic;
using System.IO;
using System.Linq;
using MiniWar.Data;
using UnityEditor;
using UnityEngine;

namespace MiniWar.EditorTools
{
    /// <summary>
    /// 무기 에셋의 칸(역할)·가격·그림을 한자리에서 맞춘다.
    ///
    /// 그림은 코드로 그린다. 픽셀 아트를 손으로 그릴 단계가 아니고,
    /// 지금 필요한 것은 "왼쪽 아래를 봤을 때 무엇을 들고 있는지 0.2초 안에 안다"뿐이라
    /// 실루엣만 맞으면 충분하다. 나중에 진짜 그림이 들어오면 icon 필드만 갈아 끼우면 된다.
    /// </summary>
    public static class WeaponAssetBuilder
    {
        const string WeaponDir = "Assets/Data/Weapons";
        const string IconDir = "Assets/Art/Generated/WeaponIcons";
        const int IconW = 96;
        const int IconH = 40;

        /// <summary>가로세로 비율이 1:1이 아닌 도트 뭉치. (x, y, w, h) — y는 아래가 0.</summary>
        struct Part
        {
            public float x, y, w, h;
            public Part(float x, float y, float w, float h) { this.x = x; this.y = y; this.w = w; this.h = h; }
        }

        [MenuItem("MiniWar/무기 에셋 정리")]
        public static void BuildMenu()
        {
            var list = EnsureAssets();
            EditorUtility.DisplayDialog("완료",
                $"무기 {list.Count}종 정리됨.\n\n" +
                string.Join("\n", list.Select(w =>
                    $"· [{w.slot}] {WeaponData.LabelOf(w.role)} — {w.displayName}" +
                    (w.price == 0 ? "  (시작 장비)" : $"  ${w.price:N0}"))),
                "확인");
        }

        /// <summary>SceneBuilder가 씬을 만들기 전에 부른다.</summary>
        public static List<WeaponData> EnsureAssets()
        {
            EnsureFolder(WeaponDir);
            EnsureFolder(IconDir);

            // 근접무기는 새로 만든다. 나머지는 기존 에셋에 칸과 값만 붙인다.
            EnsureMelee();
            AssetDatabase.SaveAssets();   // 방금 만든 것도 아래 검색에 걸리게

            var all = AssetDatabase.FindAssets("t:WeaponData")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<WeaponData>)
                .Where(w => w != null)
                .ToList();

            foreach (var w in all) Configure(w);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return all.OrderBy(w => w.role).ThenBy(w => w.price).ToList();
        }

        // ── 칸 · 가격 ────────────────────────────────────────────

        /// <summary>
        /// 이름으로 찾아 붙인다. 에셋 파일명이 바뀌어도 displayName은 그대로라
        /// 여기서 끊길 일이 없다.
        /// </summary>
        static void Configure(WeaponData w)
        {
            switch (w.displayName)
            {
                case "권총":
                    w.role = WeaponRole.Sidearm;
                    w.price = 0;                       // 유일한 시작 장비
                    w.blurb = "싸고 약하다. 돈이 마르면 결국 여기로 돌아온다.";
                    break;

                case "연사형":
                    w.role = WeaponRole.Primary;
                    w.price = 900;
                    w.blurb = "장갑 없는 적을 순식간에 녹인다. 대신 탄값이 무섭게 나간다.";
                    break;

                case "산탄형":
                    w.role = WeaponRole.Primary;
                    w.price = 1300;
                    w.blurb = "가까이서 전탄을 맞히면 가장 효율이 좋다. 멀면 아무 일도 안 일어난다.";
                    break;

                case "대전차포":
                    w.role = WeaponRole.Primary;
                    w.price = 2600;
                    w.blurb = "방어력을 무시한다. 탄속이 느려 움직이는 적은 예측해서 쏴야 한다.";
                    break;

                case "수류탄":
                    w.role = WeaponRole.Special;
                    w.price = 700;
                    w.blurb = "능선 너머와 뭉친 적. 곡사라 지형이 막지 않는다. 던질 때마다 돈이 나간다.";
                    break;

                case "총검":
                    w.role = WeaponRole.Melee;
                    w.price = 450;
                    w.blurb = "탄약도 돈도 들지 않는다. 대신 그 거리에 서 있어야 한다.";
                    break;
            }

            w.icon = MakeIcon(w);
            EditorUtility.SetDirty(w);
        }

        static void EnsureMelee()
        {
            string path = $"{WeaponDir}/SO_Weapon_Bayonet.asset";
            var w = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (w == null)
            {
                w = ScriptableObject.CreateInstance<WeaponData>();
                AssetDatabase.CreateAsset(w, path);
            }

            w.displayName = "총검";
            w.role = WeaponRole.Melee;
            w.ammoMode = AmmoMode.Melee;

            // 공짜 피해지만 그 거리에 서 있어야 한다 — 돈 대신 체력으로 낸다.
            w.damagePerPellet = 58f;
            w.pellets = 1;
            w.piercing = false;          // 장갑차 앞에서는 여전히 무력하다
            w.magazineSize = 1;
            w.reloadCost = 0;
            w.shotsPerSecond = 1.8f;
            w.meleeRange = 1.5f;
            w.projectileSpeed = 1f;
            w.projectileRange = 1f;
            w.blastRadius = 0f;
            w.ballistic = false;

            EditorUtility.SetDirty(w);
        }

        // ── 그림 ─────────────────────────────────────────────────

        static List<Part> ShapeOf(WeaponData w)
        {
            var p = new List<Part>();

            switch (w.displayName)
            {
                case "권총":
                    p.Add(new Part(0.34f, 0.46f, 0.30f, 0.16f));   // 슬라이드
                    p.Add(new Part(0.62f, 0.49f, 0.16f, 0.09f));   // 총열
                    p.Add(new Part(0.36f, 0.24f, 0.13f, 0.24f));   // 손잡이
                    p.Add(new Part(0.48f, 0.38f, 0.05f, 0.09f));   // 방아쇠울
                    break;

                case "연사형":
                    p.Add(new Part(0.22f, 0.46f, 0.44f, 0.15f));   // 몸통
                    p.Add(new Part(0.64f, 0.49f, 0.20f, 0.08f));   // 총열
                    p.Add(new Part(0.34f, 0.22f, 0.11f, 0.25f));   // 탄창
                    p.Add(new Part(0.10f, 0.47f, 0.13f, 0.12f));   // 개머리
                    p.Add(new Part(0.50f, 0.36f, 0.05f, 0.11f));   // 손잡이
                    break;

                case "산탄형":
                    p.Add(new Part(0.20f, 0.47f, 0.40f, 0.14f));   // 몸통
                    p.Add(new Part(0.58f, 0.49f, 0.30f, 0.10f));   // 긴 총열
                    p.Add(new Part(0.60f, 0.38f, 0.14f, 0.08f));   // 펌프
                    p.Add(new Part(0.08f, 0.40f, 0.14f, 0.18f));   // 개머리
                    break;

                case "대전차포":
                    p.Add(new Part(0.16f, 0.44f, 0.30f, 0.22f));   // 후부 통
                    p.Add(new Part(0.44f, 0.48f, 0.44f, 0.13f));   // 굵고 긴 포신
                    p.Add(new Part(0.84f, 0.44f, 0.08f, 0.21f));   // 포구 제퇴기
                    p.Add(new Part(0.30f, 0.22f, 0.05f, 0.23f));   // 다리
                    p.Add(new Part(0.44f, 0.22f, 0.05f, 0.23f));   // 다리
                    p.Add(new Part(0.24f, 0.66f, 0.12f, 0.07f));   // 조준경
                    break;

                case "수류탄":
                    p.Add(new Part(0.38f, 0.22f, 0.24f, 0.40f));   // 몸통
                    p.Add(new Part(0.42f, 0.62f, 0.16f, 0.10f));   // 신관
                    p.Add(new Part(0.58f, 0.44f, 0.06f, 0.26f));   // 안전 손잡이
                    p.Add(new Part(0.64f, 0.66f, 0.10f, 0.05f));   // 안전핀
                    break;

                case "총검":
                    p.Add(new Part(0.30f, 0.44f, 0.46f, 0.13f));   // 칼날
                    p.Add(new Part(0.76f, 0.47f, 0.10f, 0.07f));   // 칼끝
                    p.Add(new Part(0.26f, 0.34f, 0.05f, 0.33f));   // 코등이
                    p.Add(new Part(0.12f, 0.44f, 0.14f, 0.12f));   // 손잡이
                    break;

                default:
                    p.Add(new Part(0.25f, 0.44f, 0.50f, 0.14f));
                    break;
            }

            return p;
        }

        static Sprite MakeIcon(WeaponData w)
        {
            var parts = ShapeOf(w);

            var px = new Color32[IconW * IconH];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

            var ink = new Color32(26, 26, 31, 255);

            foreach (var part in parts)
            {
                int x0 = Mathf.RoundToInt(part.x * IconW);
                int y0 = Mathf.RoundToInt(part.y * IconH);
                int x1 = Mathf.RoundToInt((part.x + part.w) * IconW);
                int y1 = Mathf.RoundToInt((part.y + part.h) * IconH);

                for (int y = Mathf.Max(0, y0); y < Mathf.Min(IconH, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(IconW, x1); x++)
                    px[y * IconW + x] = ink;
            }

            var tex = new Texture2D(IconW, IconH, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();

            string safe = SafeName(w.displayName);
            string path = $"{IconDir}/ICON_{safe}.png";
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

        /// <summary>한글 파일명은 플랫폼마다 말썽이라 영문으로 고정한다.</summary>
        static string SafeName(string displayName)
        {
            switch (displayName)
            {
                case "권총": return "Pistol";
                case "연사형": return "SMG";
                case "산탄형": return "Shotgun";
                case "대전차포": return "AtGun";
                case "수류탄": return "Grenade";
                case "총검": return "Bayonet";
                default: return "Weapon";
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
