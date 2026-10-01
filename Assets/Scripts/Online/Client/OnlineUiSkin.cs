using System.Collections.Generic;
using UnityEngine;

namespace MiniWar.Online
{
    // Small nine-sliced surfaces keep rounded corners crisp at every window size.
    // Ordinary windows are opaque; chat uses separate translucent surfaces.
    sealed class OnlineUiSkin
    {
        readonly List<Texture2D> textures = new List<Texture2D>();
        public readonly GUIStyle Window, Header, Slot, Inset, Button, Field;
        public readonly GUIStyle ChatWindow, ChatHeader, ChatButton, ChatField;
        public readonly GUISkin Skin, ChatSkin;
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

            // Transparency belongs to the textures, never to GUI.color or the text.
            ChatWindow = Surface(new Color32(12, 22, 30, 56), new Color32(12, 22, 30, 56), 12, false);
            ChatHeader = Surface(new Color32(27, 66, 80, 70), new Color32(20, 43, 55, 70), 9, false);
            ChatField = new GUIStyle(Field);
            var chatInput = Tile(new Color32(10, 18, 25, 100), new Color32(10, 18, 25, 100), 6, false);
            var chatFocus = Tile(new Color32(15, 31, 41, 145), new Color32(10, 23, 31, 145), 6, false);
            Set(ChatField.normal, chatInput, Color.white); Set(ChatField.hover, chatFocus, Color.white);
            Set(ChatField.active, chatFocus, Color.white); Set(ChatField.focused, chatFocus, Color.white);
            ChatButton = new GUIStyle(Button);
            var chatButton = Tile(new Color32(39, 93, 110, 155), new Color32(22, 54, 70, 155), 8, false);
            var chatHover = Tile(new Color32(47, 113, 133, 195), new Color32(28, 70, 87, 195), 8, false);
            var chatPressed = Tile(new Color32(18, 51, 66, 210), new Color32(31, 80, 96, 210), 8, false);
            Set(ChatButton.normal, chatButton, Color.white); Set(ChatButton.hover, chatHover, Color.white);
            Set(ChatButton.active, chatPressed, Color.white); Set(ChatButton.focused, chatHover, Color.white);
            Set(ChatButton.onNormal, chatPressed, Color.white); Set(ChatButton.onHover, chatHover, Color.white);
            Set(ChatButton.onActive, chatPressed, Color.white); Set(ChatButton.onFocused, chatHover, Color.white);
            ChatSkin = Object.Instantiate(Skin); ChatSkin.hideFlags = HideFlags.HideAndDontSave;
            ChatSkin.settings.cursorColor = Color.white;
            ChatSkin.verticalScrollbar = new GUIStyle(ChatWindow) { fixedWidth = 10 };
            ChatSkin.verticalScrollbarThumb = Surface(new Color32(161, 214, 225, 170), new Color32(117, 170, 185, 170), 6, false);
            ChatSkin.verticalScrollbarThumb.fixedWidth = 10;
        }

        static void Set(GUIStyleState state, Texture2D texture) { state.background = texture; state.textColor = Ink; }
        static void Set(GUIStyleState state, Texture2D texture, Color ink) { state.background = texture; state.textColor = ink; }
        GUIStyle Surface(Color top, Color bottom, float radius, bool outlined = true)
        {
            var style = new GUIStyle { border = new RectOffset(14, 14, 14, 14) };
            style.normal.background = Tile(top, bottom, radius, outlined); return style;
        }
        Texture2D Tile(Color top, Color bottom, float radius, bool outlined = true)
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
                if (outlined && distance > -2) fill = new Color32(86, 119, 132, 255);
                else if (outlined && distance > -4) fill = y > 31 ? Color.white : new Color32(161, 181, 190, 255);
                fill.a *= Mathf.Clamp01(.5f - distance);
                pixels[y * size + x] = fill;
            }
            texture.SetPixels(pixels); texture.Apply(false, true); textures.Add(texture); return texture;
        }
        public void Dispose()
        {
            foreach (var texture in textures) Object.Destroy(texture);
            Object.Destroy(ChatSkin);
            Object.Destroy(Skin);
        }
    }
}
