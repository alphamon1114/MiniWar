using UnityEngine;

namespace MiniWar.Online
{
    /// <summary>Body and weapon atlases stay independent so every owned weapon has its own visible sprite.</summary>
    public sealed class OnlineVisuals
    {
        readonly Sprite[][] bodies = new Sprite[2][];
        readonly Sprite[] weapons;
        readonly Sprite[] handguns;
        public static readonly string[] FamilyNames = { "피스톨", "기관단총", "샷건", "소총", "저격총", "근접무기", "리볼버" };
        public static readonly float[] WeaponWidths = { .65f, .95f, 1.3f, 1.35f, 1.65f, 1.25f, .75f };

        public OnlineVisuals()
        {
            bodies[0] = Slice(Resources.Load<Texture2D>("Online/GunnerMale"), 4, 2, true);
            bodies[1] = Slice(Resources.Load<Texture2D>("Online/GunnerFemale"), 4, 2, true);
            weapons = Slice(Resources.Load<Texture2D>("Online/Weapons"), 6, 3, false);
            handguns = Slice(Resources.Load<Texture2D>("Online/Handguns"), 2, 3, false, true);
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

        public Sprite Body(int body, bool pistolReady, int frame = 0) => bodies[Mathf.Clamp(body, 0, 1)][(pistolReady ? 4 : 0) + frame % 4];
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

    public sealed class OnlineActorView : MonoBehaviour
    {
        SpriteRenderer body, weapon;
        Transform socket;
        OnlineVisuals visuals;
        LanActor state;

        public void Initialize(OnlineVisuals library)
        {
            visuals = library;
            var bodyObject = new GameObject("Character body"); bodyObject.transform.SetParent(transform, false);
            body = bodyObject.AddComponent<SpriteRenderer>(); body.sortingOrder = 3;
            socket = new GameObject("Weapon grip").transform; socket.SetParent(transform, false);
            weapon = socket.gameObject.AddComponent<SpriteRenderer>(); weapon.sortingOrder = 5;
        }

        public void Apply(LanActor actor) { state = actor; Render(); }
        void Update() { if (state != null) Render(); }

        void Render()
        {
            bool pistolReady = LanRules.IsHandgun(state.weaponFamily) && !state.aiming;
            body.sprite = visuals.Body(state.body, pistolReady, (int)(Time.unscaledTime * 4) % 4);
            if (body.sprite != null) body.transform.localScale = Vector3.one * (2.5f / body.sprite.bounds.size.y);
            body.flipX = state.facing < 0;
            weapon.sprite = visuals.Weapon(state.weaponFamily, state.weaponTier);
            float size = weapon.sprite == null ? 1 : OnlineVisuals.WeaponWidths[Mathf.Clamp(state.weaponFamily, 0, 6)] / weapon.sprite.bounds.size.x;
            float gripX = pistolReady ? state.body == 0 ? .39f : .29f : state.body == 0 ? -.07f : .05f;
            socket.localPosition = new Vector3(gripX * state.facing, pistolReady ? 2.02f : 1.43f, 0);
            float angle = state.aiming ? state.aimAngle : pistolReady ? 90 : state.facing > 0 ? 0 : 180;
            socket.localRotation = Quaternion.Euler(0, 0, angle);
            socket.localScale = new Vector3(size, size * state.facing, 1);
        }
    }
}
