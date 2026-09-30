using System.Collections.Generic;
using UnityEngine;

namespace MiniWar.Online
{
    // Small nine-sliced surfaces keep rounded corners crisp at every window size.
    // The interiors are opaque; only the outside of each rounded corner is transparent.
    sealed class OnlineUiSkin
    {
        readonly List<Texture2D> textures = new List<Texture2D>();
        public readonly GUIStyle Window, Header, Slot, Inset, Button, Field;
        public readonly GUISkin Skin;
        public static readonly Color Ink = new Color32(39, 57, 64, 255);

        public OnlineUiSkin(GUISkin source, Font font)
        {
            Window = Surface(new Color32(245, 248, 248, 255), new Color32(217, 226, 230, 255), 12);
            Header = Surface(new Color32(229, 251, 255, 255), new Color32(140, 207, 221, 255), 9);
            Slot = Surface(new Color32(250, 252, 252, 255), new Color32(224, 232, 235, 255), 6);
            Inset = Surface(new Color32(192, 205, 211, 255), new Color32(213, 223, 227, 255), 6);
            Button = new GUIStyle(source.button)
            {
                font = font, fontSize = 17, richText = false, alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(10, 10, 5, 5), border = new RectOffset(12, 12, 12, 12)
            };
            var normal = Tile(new Color32(239, 251, 254, 255), new Color32(166, 205, 218, 255), 8);
            var hover = Tile(new Color32(250, 255, 255, 255), new Color32(118, 214, 235, 255), 8);
            var pressed = Tile(new Color32(106, 182, 203, 255), new Color32(192, 231, 239, 255), 8);
            Set(Button.normal, normal); Set(Button.hover, hover); Set(Button.active, pressed); Set(Button.focused, hover);
            Set(Button.onNormal, pressed); Set(Button.onHover, hover); Set(Button.onActive, pressed); Set(Button.onFocused, pressed);
            Field = new GUIStyle(source.textField)
            {
                font = font, fontSize = 18, richText = false,
                padding = new RectOffset(11, 11, 8, 7), border = new RectOffset(12, 12, 12, 12)
            };
            var input = Tile(new Color32(240, 245, 247, 255), Color.white, 6);
            Set(Field.normal, input); Set(Field.hover, input); Set(Field.active, input); Set(Field.focused, input);
            Skin = Object.Instantiate(source); Skin.hideFlags = HideFlags.HideAndDontSave;
            Skin.settings.cursorColor = Ink;
            Skin.settings.selectionColor = new Color32(117, 204, 225, 255);
            Skin.verticalScrollbar = new GUIStyle(Inset) { fixedWidth = 14 };
            Skin.verticalScrollbarThumb = new GUIStyle(Header) { fixedWidth = 14, fixedHeight = 0 };
            Skin.verticalScrollbarUpButton = new GUIStyle { fixedWidth = 0, fixedHeight = 0 };
            Skin.verticalScrollbarDownButton = new GUIStyle { fixedWidth = 0, fixedHeight = 0 };
        }

        static void Set(GUIStyleState state, Texture2D texture) { state.background = texture; state.textColor = Ink; }
        GUIStyle Surface(Color top, Color bottom, float radius)
        {
            var style = new GUIStyle { border = new RectOffset(14, 14, 14, 14) };
            style.normal.background = Tile(top, bottom, radius); return style;
        }
        Texture2D Tile(Color top, Color bottom, float radius)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "MiniWar rounded UI", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - 31.5f) - (31.5f - radius);
                float dy = Mathf.Abs(y - 31.5f) - (31.5f - radius);
                float distance = Mathf.Sqrt(Mathf.Max(dx, 0) * Mathf.Max(dx, 0) + Mathf.Max(dy, 0) * Mathf.Max(dy, 0))
                    + Mathf.Min(Mathf.Max(dx, dy), 0) - radius;
                Color fill = Color.Lerp(bottom, top, y / 63f);
                if (distance > -2) fill = new Color32(86, 119, 132, 255);
                else if (distance > -4) fill = y > 31 ? Color.white : new Color32(161, 181, 190, 255);
                fill.a = Mathf.Clamp01(.5f - distance);
                pixels[y * size + x] = fill;
            }
            texture.SetPixels(pixels); texture.Apply(false, true); textures.Add(texture); return texture;
        }
        public void Dispose()
        {
            foreach (var texture in textures) Object.Destroy(texture);
            Object.Destroy(Skin);
        }
    }
}
