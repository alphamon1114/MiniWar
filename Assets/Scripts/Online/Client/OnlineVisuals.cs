using System.Linq;
using UnityEngine;

namespace MiniWar.Online
{
    /// <summary>Body and weapon atlases stay independent so every owned weapon has its own visible sprite.</summary>
    public sealed class OnlineVisuals
    {
        readonly Sprite[][] bodies = new Sprite[2][];
        readonly Sprite[] weapons;
        readonly Sprite[] handguns;
        readonly GameObject[] rigs = new GameObject[2];
        readonly Sprite[] portraits = new Sprite[2];
        public static readonly string[] FamilyNames = { "피스톨", "기관단총", "샷건", "소총", "저격총", "근접무기", "리볼버" };
        public static readonly float[] WeaponWidths = { .52f, .95f, 1.3f, 1.35f, 1.65f, 1.25f, .62f };

        public OnlineVisuals()
        {
            bodies[0] = Slice(Resources.Load<Texture2D>("Online/GunnerMale"), 4, 2, true);
            bodies[1] = Slice(Resources.Load<Texture2D>("Online/GunnerFemale"), 4, 2, true);
            weapons = Resources.LoadAll<Sprite>("Online/PixelV1/Weapons").OrderBy(x => x.name).ToArray();
            handguns = Resources.LoadAll<Sprite>("Online/PixelV1/Handguns").OrderBy(x => x.name).ToArray();
            if (weapons.Length != 18 || handguns.Length != 6)
                Debug.LogError("Pixel equipment sprites missing. Run MiniWar > Characters > Build part sprites and rigs.");
            rigs[0] = Resources.Load<GameObject>("Online/Rigs/GunnerMale");
            rigs[1] = Resources.Load<GameObject>("Online/Rigs/GunnerFemale");
            portraits[0] = Resources.Load<Sprite>("Online/RedrawV1/MalePortrait");
            portraits[1] = Resources.Load<Sprite>("Online/RedrawV1/FemalePortrait");
        }

        // Trim transparent cell gutters without rewriting the source art. Every cell remains independently addressable.
        static Sprite[] Slice(Texture2D texture, int columns, int rows, bool body, bool handgun = false)
        {
            var result = new Sprite[columns * rows];
            if (texture == null) return result;
            var pixels = texture.GetPixels32();
            // Generated weapon gutters are slightly offset from mathematical sixths; these measured cuts avoid adjacent sprites.
            int[] weaponCuts = { 0, 277, 560, 882, 1174, 1490, 1774 };
            for (int row = 0; row < rows; row++)
            for (int col = 0; col < columns; col++)
            {
                int x0 = col * texture.width / columns, x1 = (col + 1) * texture.width / columns;
                if (!body && !handgun) { x0 = weaponCuts[col] * texture.width / 1774; x1 = weaponCuts[col + 1] * texture.width / 1774; }
                int y0 = (rows - row - 1) * texture.height / rows, y1 = (rows - row) * texture.height / rows;
                int left = x1, right = x0, bottom = y1, top = y0;
                for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    if (pixels[y * texture.width + x].a > 32)
                    { left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y); }
                if (right < left || top < bottom) continue;
                var rect = new Rect(left, bottom, right - left + 1, top - bottom + 1);
                var grips = new[] { new Vector2(.22f, .25f), new Vector2(.42f, .4f), new Vector2(.3f, .34f), new Vector2(.4f, .3f), new Vector2(.3f, .3f), new Vector2(.15f, .5f) };
                Vector2 pivot = body ? new Vector2(.5f, 0) : handgun ? new Vector2(.22f, .25f) : grips[col];
                result[row * columns + col] = Sprite.Create(texture, rect, pivot, 100, 0, SpriteMeshType.FullRect);
                result[row * columns + col].name = texture.name + "_" + row + "_" + col;
            }
            return result;
        }

        public Sprite Body(int body, bool pistolReady, int frame = 0)
        {
            int index = Mathf.Clamp(body, 0, 1);
            return portraits[index] != null ? portraits[index] : bodies[index][(pistolReady ? 4 : 0) + frame % 4];
        }
        public GameObject RigPrefab(int body) => rigs[Mathf.Clamp(body, 0, 1)];
        public Sprite Weapon(int family, int tier)
        {
            int row = Mathf.Clamp(tier, 1, 3) - 1;
            if (LanRules.IsHandgun(family)) return handguns[row * 2 + (family == (int)WeaponFamily.Revolver ? 1 : 0)];
            return weapons[row * 6 + Mathf.Clamp(family, 0, 5)];
        }
        public static string WeaponName(LanItem item) => FamilyNames[Mathf.Clamp(item.family, 0, 6)] + " · " + item.tier + "등급";

        public static void DrawSprite(Rect box, Sprite sprite)
        {
            if (sprite == null) return;
            var r = sprite.rect;
            float scale = Mathf.Min(box.width / r.width, box.height / r.height);
            var target = new Rect(box.center.x - r.width * scale / 2, box.center.y - r.height * scale / 2, r.width * scale, r.height * scale);
            GUI.DrawTextureWithTexCoords(target, sprite.texture, new Rect(r.x / sprite.texture.width, r.y / sprite.texture.height, r.width / sprite.texture.width, r.height / sprite.texture.height), true);
        }
    }

}
