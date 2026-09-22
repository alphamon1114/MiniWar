using UnityEngine;

namespace MiniWar.Runtime
{
    /// <summary>
    /// OnGUI용 색과 그리기 도구. HUD가 둘(마을·던전)로 늘어나서 한 곳에 모았다.
    ///
    /// 기본 GUI.skin.label은 어두운 배경을 전제로 한 밝은 회색이다.
    /// 이 게임의 배경은 흰색이라 그대로 쓰면 글자가 보이지 않는다 —
    /// 글자색은 <b>반드시</b> 직접 지정해야 한다.
    /// </summary>
    public static class HudStyle
    {
        public static readonly Color Ink = new Color(0.10f, 0.10f, 0.12f);   // 본문
        public static readonly Color Dim = new Color(0.38f, 0.38f, 0.42f);   // 보조 설명
        public static readonly Color Warn = new Color(0.72f, 0.12f, 0.12f);  // 경고·부족
        public static readonly Color Gain = new Color(0.13f, 0.42f, 0.20f);  // 획득·성공
        public static readonly Color Notice = new Color(0.62f, 0.40f, 0.05f);// 알림

        public static readonly Color Paper = new Color(0.965f, 0.960f, 0.945f);
        public static readonly Color PanelEdge = new Color(0.10f, 0.10f, 0.12f);
        public static readonly Color Scrim = new Color(0.08f, 0.08f, 0.10f, 0.55f);

        /// <summary>리치텍스트용 16진 색 문자열. $"<color={HudStyle.HexDim}>…"</summary>
        public const string HexDim = "#61616b";
        public const string HexWarn = "#b81f1f";
        public const string HexGain = "#226b33";
        public const string HexNotice = "#9e660d";

        /// <summary>
        /// 상단 상태 바. 불투명 흰 판은 도트 배경 위에 종이를 붙여 놓은 것처럼 보인다 —
        /// 반투명 어둠으로 깔고 글자를 밝게 뒤집는다.
        /// </summary>
        public static readonly Color StatusBar = new Color(0.06f, 0.05f, 0.08f, 0.62f);

        public static readonly Color InkLight = new Color(0.94f, 0.91f, 0.84f);

        public const string HexLight = "#f0ece1";
        public const string HexLightDim = "#a59d8e";

        static Texture2D _pixel;
        static Font _font;
        static bool _fontTried;

        /// <summary>
        /// 도트 폰트. Assets/Resources/Fonts 에 있으면 자동으로 집어 쓰고,
        /// 없으면 기본 폰트로 조용히 돌아간다 — 폰트 하나 때문에 HUD가 통째로 죽으면 안 된다.
        /// </summary>
        public static Font PixelFont
        {
            get
            {
                if (_fontTried) return _font;
                _fontTried = true;
                _font = Resources.Load<Font>("Fonts/DNFBitBitv2");
                if (_font == null)
                    Debug.LogWarning("Resources/Fonts/DNFBitBitv2 를 못 찾아 기본 폰트로 그립니다.");
                return _font;
            }
        }

        static Texture2D Pixel
        {
            get
            {
                if (_pixel != null) return _pixel;
                _pixel = new Texture2D(1, 1);
                _pixel.SetPixel(0, 0, Color.white);
                _pixel.Apply();
                _pixel.hideFlags = HideFlags.HideAndDontSave;
                return _pixel;
            }
        }

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Pixel);
            GUI.color = old;
        }

        /// <summary>화면 전체를 덮어 뒤를 죽인다. 팝업 위에 시선을 모으는 유일한 방법.</summary>
        public static void DimScreen() => Fill(new Rect(0, 0, Screen.width, Screen.height), Scrim);

        /// <summary>불투명 패널 + 테두리. 반투명으로 두면 뒤의 글자와 섞여 읽을 수 없다.</summary>
        public static void Panel(Rect r)
        {
            Fill(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), PanelEdge);
            Fill(r, Paper);
            Fill(new Rect(r.x, r.y, r.width, 4f), PanelEdge);
        }

        /// <summary>진행 바 한 줄.</summary>
        public static void Bar(Rect r, float ratio, Color fill)
        {
            Fill(r, new Color(0.10f, 0.10f, 0.12f, 0.14f));
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(ratio), r.height), fill);
        }

        public static GUIStyle Label(int size = 16, TextAnchor anchor = TextAnchor.UpperLeft)
            => Label(size, anchor, Ink);

        public static GUIStyle Label(int size, TextAnchor anchor, Color ink)
        {
            var s = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                richText = true,
                alignment = anchor,
                wordWrap = false,
                normal = { textColor = ink },
                hover = { textColor = ink },
                active = { textColor = ink },
                focused = { textColor = ink },
            };

            var f = PixelFont;
            if (f != null) s.font = f;
            return s;
        }
    }
}
