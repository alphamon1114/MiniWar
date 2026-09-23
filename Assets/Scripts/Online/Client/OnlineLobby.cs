using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniWar.Online
{
    /// <summary>First online slice: accounts, saved starter inventory, authoritative town, two channels and chat.</summary>
    public sealed class OnlineLobby : MonoBehaviour
    {
        public Texture2D backdrop;
        public Sprite[] npcPortraits;
        public Font font;
        LanClient client;
        LanProfile profile;
        LanEvent snapshot;
        readonly List<string> chat = new List<string>();
        readonly Dictionary<string, Transform> actors = new Dictionary<string, Transform>();
        readonly Dictionary<string, Vector3> positions = new Dictionary<string, Vector3>();
        readonly Dictionary<string, TextMesh> names = new Dictionary<string, TextMesh>();
        readonly Dictionary<string, OnlineActorView> views = new Dictionary<string, OnlineActorView>();
        OnlineVisuals visuals;
        Camera worldCamera;
        Sprite square;
        GUIStyle label, title, field, button, small;
        string host = "127.0.0.1", portText = "7777", nickname = "", password = "", fingerprint = "";
        string notice = "서버를 실행한 뒤 주소와 인증 코드를 입력하세요.", message = "";
        bool connecting, chatFocused, inventory;
        bool localPreview;
        int channel = 1;
        int selectedBody;
        float sendAt, pingAt;
        long sequence;
        Vector2 scroll;
        const float Ground = -2.8f;

        [Serializable] sealed class LocalPreviewConfig { public int port; public string fingerprint; public int body; }

        void Awake()
        {
            Application.runInBackground = true;
            worldCamera = Camera.main;
            host = PlayerPrefs.GetString("MiniWar.LAN.Host", "127.0.0.1");
            nickname = PlayerPrefs.GetString("MiniWar.LAN.Nickname", "");
            fingerprint = PlayerPrefs.GetString("MiniWar.LAN.Fingerprint." + host, "");
            if (font == null) font = Resources.Load<Font>("Fonts/DNFBitBitv2");
            var tex = new Texture2D(1, 1); tex.SetPixel(0, 0, Color.white); tex.Apply();
            square = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            visuals = new OnlineVisuals();
            BuildWorld();
        }

        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--miniwar-local-preview");
            if (index < 0) return;
            localPreview = true;
            host = "127.0.0.1"; fingerprint = "";
            try
            {
                if (index + 1 >= args.Length) throw new ArgumentException("Missing preview configuration.");
                var config = JsonUtility.FromJson<LocalPreviewConfig>(System.IO.File.ReadAllText(args[index + 1]));
                if (config == null || config.port < 1 || config.port > 65535 || config.fingerprint == null
                    || config.fingerprint.Length != 64 || config.fingerprint.Any(c => !Uri.IsHexDigit(c)))
                    throw new ArgumentException("Invalid preview configuration.");
                host = "127.0.0.1"; portText = config.port.ToString(); fingerprint = config.fingerprint;
                selectedBody = Mathf.Clamp(config.body, 0, 1);
                StartLocalPreview();
            }
            catch (Exception)
            { notice = "미리보기 설정을 읽지 못했습니다. PlayMiniWar.cmd로 다시 실행해 주세요."; }
        }

        void StartLocalPreview()
        {
            // Disposable local preview credentials exist only in memory; real account settings stay untouched.
            host = "127.0.0.1";
            nickname = "미리보기" + Guid.NewGuid().ToString("N").Substring(0, 6);
            password = Guid.NewGuid().ToString("N");
            BeginLogin(true);
        }

        void BuildWorld()
        {
            if (worldCamera == null)
            {
                var cameraObject = new GameObject("Online Camera"); worldCamera = cameraObject.AddComponent<Camera>(); cameraObject.tag = "MainCamera";
            }
            worldCamera.orthographic = true; worldCamera.orthographicSize = 6;
            worldCamera.backgroundColor = new Color(0.06f, 0.08f, 0.14f);
            worldCamera.transform.position = new Vector3(10, 0, -10);
            if (backdrop != null)
            {
                var bg = new GameObject("Survivors Refuge").AddComponent<SpriteRenderer>();
                bg.sprite = Sprite.Create(backdrop, new Rect(0, 0, backdrop.width, backdrop.height), new Vector2(0.5f, 0.5f), backdrop.height / 13f);
                bg.transform.position = new Vector3(21, -1.1f, 0); bg.sortingOrder = -20;
                bg.transform.localScale = new Vector3(44f / bg.sprite.bounds.size.x, 1, 1);
            }
            else MakePart("Ground", null, new Vector3(21, Ground - 1, 0), new Vector2(44, 2), new Color(0.13f, 0.17f, 0.23f), -5);
            for (int i = 0; i < 3; i++)
            {
                float x = 9 + i * 13;
                var npc = new GameObject("Refuge NPC " + i);
                npc.transform.position = new Vector3(x, Ground + 1.3f, 0);
                var sr = npc.AddComponent<SpriteRenderer>();
                if (npcPortraits != null && i < npcPortraits.Length && npcPortraits[i] != null)
                { sr.sprite = npcPortraits[i]; float size = 2.6f / sr.sprite.bounds.size.y; npc.transform.localScale = Vector3.one * size; }
                else { sr.sprite = square; npc.transform.localScale = new Vector3(0.65f, 2.2f, 1); sr.color = new Color(0.62f, 0.59f, 0.48f); }
                sr.sortingOrder = -1;
                MakeName(npc.transform, new[] { "정비병", "보급 담당관", "출격 관제병" }[i], 1.4f / npc.transform.localScale.y);
            }
        }

        Transform MakePart(string partName, Transform parent, Vector3 position, Vector2 size, Color color, int order)
        {
            var go = new GameObject(partName); go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = new Vector3(size.x, size.y, 1);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = square; sr.color = color; sr.sortingOrder = order;
            return go.transform;
        }

        TextMesh MakeName(Transform parent, string text, float y)
        {
            var go = new GameObject("Name"); go.transform.SetParent(parent, false); go.transform.localPosition = new Vector3(0, y, 0);
            go.transform.localScale = new Vector3(1 / parent.localScale.x, 1 / parent.localScale.y, 1);
            var tm = go.AddComponent<TextMesh>(); tm.text = text; tm.fontSize = 30; tm.characterSize = 0.04f; tm.anchor = TextAnchor.MiddleCenter; tm.color = Color.white;
            if (font != null) { tm.font = font; go.GetComponent<MeshRenderer>().sharedMaterial = font.material; }
            go.GetComponent<MeshRenderer>().sortingOrder = 12;
            return tm;
        }

        void Update()
        {
            while (client != null && client.Events.TryDequeue(out var e))
            {
                switch (e.op)
                {
                    case "welcome":
                        profile = e.profile; channel = e.channel; connecting = false; password = ""; notice = e.text;
                        if (localPreview)
                        {
                            notice = "로컬 미리보기 · 이동, 점프, 조준, 장비 교체를 확인해 보세요.";
                            Debug.Log("MiniWar local preview connected to town.");
                        }
                        else
                        {
                            PlayerPrefs.SetString("MiniWar.LAN.Host", host); PlayerPrefs.SetString("MiniWar.LAN.Nickname", nickname);
                            PlayerPrefs.SetString("MiniWar.LAN.Fingerprint." + host, fingerprint); PlayerPrefs.Save();
                        }
                        break;
                    case "snapshot": snapshot = e; channel = e.channel; SyncActors(); break;
                    case "profile": profile = e.profile; notice = e.text; break;
                    case "chat": chat.Add($"{e.sender}  {e.text}"); if (chat.Count > 70) chat.RemoveAt(0); scroll.y = float.MaxValue; break;
                    case "channel": channel = e.channel; chat.Clear(); notice = e.text; break;
                    case "error": notice = e.text; connecting = false; if (profile == null) { client.Dispose(); client = null; } break;
                    case "disconnected": notice = e.text; Logout(false); break;
                }
            }
            if (profile == null || client == null) return;
            var kb = Keyboard.current;
            if (kb != null && kb.iKey.wasPressedThisFrame && !chatFocused) inventory = !inventory;
            if (kb != null && !chatFocused)
            {
                if (kb.digit1Key.wasPressedThisFrame && profile.equipped.Length > 0) Equip(profile.equipped[0]);
                if (kb.digit2Key.wasPressedThisFrame && profile.equipped.Length > 1) Equip(profile.equipped[1]);
            }
            bool jump = kb != null && !chatFocused && !inventory && (kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame);
            if (Time.unscaledTime >= sendAt || jump)
            {
                sendAt = Time.unscaledTime + 0.05f;
                float move = kb == null || chatFocused || inventory ? 0 : (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
                float aimAngle = 0;
                bool aiming = Mouse.current != null && Mouse.current.rightButton.isPressed && !chatFocused && !inventory;
                if (aiming && actors.TryGetValue(profile.nickname, out var self))
                {
                    Vector2 delta = worldCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue()) - (self.position + Vector3.up * 1.35f);
                    aimAngle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                }
                client.Send(new LanCommand { op = "input", sequence = ++sequence, move = move, jump = jump, aimAngle = aimAngle, aiming = aiming });
            }
            if (Time.unscaledTime >= pingAt) { pingAt = Time.unscaledTime + 5; client.Send(new LanCommand { op = "ping" }); }
            foreach (var kv in actors)
            {
                if (positions.TryGetValue(kv.Key, out var desired)) kv.Value.position = Vector3.Lerp(kv.Value.position, desired, 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
            }
            if (actors.TryGetValue(profile.nickname, out var own))
            {
                float half = worldCamera.orthographicSize * worldCamera.aspect;
                float x = half * 2 >= LanRules.TownWidth ? LanRules.TownWidth / 2 : Mathf.Clamp(own.position.x, half, LanRules.TownWidth - half);
                worldCamera.transform.position = Vector3.Lerp(worldCamera.transform.position, new Vector3(x, 0, -10), 1 - Mathf.Exp(-7 * Time.unscaledDeltaTime));
            }
        }

        void SyncActors()
        {
            var live = new HashSet<string>();
            foreach (var a in snapshot.actors)
            {
                live.Add(a.nickname);
                var desired = new Vector3(a.x, Ground + a.y, 0);
                if (!actors.TryGetValue(a.nickname, out var root))
                {
                    root = new GameObject("Player " + a.nickname).transform; root.position = desired; actors.Add(a.nickname, root);
                    bool own = profile != null && a.nickname == profile.nickname;
                    var view = root.gameObject.AddComponent<OnlineActorView>(); view.Initialize(visuals); views.Add(a.nickname, view);
                    names[a.nickname] = MakeName(root, a.nickname + (own ? " · 나" : ""), 2.9f);
                }
                positions[a.nickname] = desired;
                views[a.nickname].Apply(a);
            }
            foreach (string name in actors.Keys.Where(n => !live.Contains(n)).ToArray())
            { Destroy(actors[name].gameObject); actors.Remove(name); positions.Remove(name); names.Remove(name); views.Remove(name); }
        }

        void Styles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 17, wordWrap = true, richText = false };
            label.normal.textColor = new Color(.9f, .91f, .88f);
            title = new GUIStyle(label) { fontSize = 36, fontStyle = FontStyle.Bold };
            small = new GUIStyle(label) { fontSize = 13 };
            field = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 18, padding = new RectOffset(10, 10, 8, 8) };
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 17, padding = new RectOffset(12, 12, 9, 9) };
        }

        static void Panel(Rect rect, Color color) { Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
        void OnGUI()
        {
            Styles();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            var previous = GUI.matrix; GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2), Quaternion.identity, Vector3.one * scale);
            if (profile == null) DrawLogin(); else DrawLobby();
            GUI.matrix = previous;
        }

        void DrawLogin()
        {
            Panel(new Rect(0, 0, 1280, 720), new Color(.02f, .03f, .07f, .58f));
            if (localPreview)
            {
                Panel(new Rect(340, 230, 600, 260), new Color(.04f, .06f, .1f, .98f));
                GUI.Label(new Rect(380, 265, 520, 60), connecting ? "마을에 입장하는 중…" : "로컬 미리보기", title);
                GUI.Label(new Rect(380, 338, 520, 66), connecting ? "잠시만 기다려 주세요." : notice, label);
                if (!connecting && GUI.Button(new Rect(480, 422, 320, 45), "다시 연결", button)) StartLocalPreview();
                return;
            }
            GUI.Label(new Rect(65, 85, 650, 70), "MINIWAR", title);
            GUI.Label(new Rect(68, 155, 610, 70), "잃어버린 왕국을 되찾는 사람들", label);
            GUI.Label(new Rect(68, 242, 610, 55), "왕국 탈환군", title);
            OnlineVisuals.DrawSprite(new Rect(175, 305, 260, 270), visuals.Body(selectedBody, true));
            GUI.Label(new Rect(68, 575, 550, 28), "새 계정은 선택한 캐릭터로 생성됩니다.", small);
            GUI.Label(new Rect(68, 610, 550, 52), "학원 내부망 · Windows\n온라인 기반 개발 빌드 — 공용 마을", small);
            Panel(new Rect(730, 55, 490, 610), new Color(.06f, .08f, .13f, .97f));
            GUI.Label(new Rect(760, 79, 420, 35), "탈환군 등록 / 접속", label);
            GUI.Label(new Rect(760, 127, 330, 25), "서버 주소", small);
            host = GUI.TextField(new Rect(760, 152, 310, 42), host, 128, field);
            portText = GUI.TextField(new Rect(1080, 152, 105, 42), portText, 5, field);
            GUI.Label(new Rect(760, 212, 420, 25), "서버 인증 코드 · 서버 창의 SHA-256", small);
            fingerprint = GUI.TextField(new Rect(760, 237, 425, 42), fingerprint, 95, field);
            GUI.Label(new Rect(760, 293, 420, 25), "닉네임 · 2~16자", small);
            nickname = GUI.TextField(new Rect(760, 318, 425, 42), nickname, 16, field);
            GUI.Label(new Rect(760, 374, 420, 25), "비밀번호 · 8자 이상", small);
            password = GUI.PasswordField(new Rect(760, 399, 425, 42), password, '●', 128, field);
            GUI.enabled = !connecting;
            selectedBody = GUI.SelectionGrid(new Rect(760, 455, 425, 38), selectedBody, new[] { "남성", "여성" }, 2, button);
            if (GUI.Button(new Rect(760, 508, 205, 48), "접속", button)) BeginLogin(false);
            if (GUI.Button(new Rect(980, 508, 205, 48), "새 계정 등록", button)) BeginLogin(true);
            GUI.enabled = true;
            GUI.Label(new Rect(760, 577, 425, 77), connecting ? "서버에 연결하는 중입니다…" : notice, small);
        }

        void BeginLogin(bool register)
        {
            host = host.Trim(); fingerprint = fingerprint.Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();
            if (host.Length == 0 || !int.TryParse(portText, out int port) || port < 1 || port > 65535) { notice = "올바른 서버 주소와 포트를 입력하세요."; return; }
            if (fingerprint.Length != 64 || fingerprint.Any(c => !Uri.IsHexDigit(c))) { notice = "서버 창에 표시된 64자리 인증 코드를 입력하세요."; return; }
            if (password.Length < 8) { notice = "비밀번호는 8자 이상입니다."; return; }
            client?.Dispose(); client = new LanClient(); sequence = 0; connecting = true;
            client.Connect(host, port, fingerprint, nickname, password, register, selectedBody);
        }

        void Equip(string itemId) { if (itemId != profile.activeItemId) client.Send(new LanCommand { op = "equip", itemId = itemId }); }

        void DrawLobby()
        {
            Panel(new Rect(0, 0, 1280, 65), new Color(.03f, .045f, .075f, .94f));
            GUI.Label(new Rect(25, 16, 470, 35), localPreview ? "생존자 거점  /  로컬 미리보기" : $"생존자 거점  /  CH {channel}  /  {profile.nickname}", label);
            if (localPreview && GUI.Button(new Rect(488, 12, 190, 40), selectedBody == 0 ? "여성으로 보기" : "남성으로 보기", button))
            {
                selectedBody = 1 - selectedBody; Logout(false); StartLocalPreview(); return;
            }
            for (int i = 1; i <= LanRules.MaxChannels; i++)
            {
                int count = snapshot != null && snapshot.channelCounts.Length >= i ? snapshot.channelCounts[i - 1] : 0;
                if (GUI.Button(new Rect(690 + (i - 1) * 136, 12, 130, 40), $"CH {i} · {count}/30", button)) client.Send(new LanCommand { op = "channel", channel = i });
            }
            if (GUI.Button(new Rect(972, 12, 130, 40), "장비 [I]", button)) inventory = !inventory;
            if (GUI.Button(new Rect(1110, 12, 145, 40), "로그아웃", button)) { Logout(true); return; }
            GUI.Label(new Rect(25, 80, 1100, 40), notice, small);
            Panel(new Rect(18, 459, 505, 243), new Color(.03f, .045f, .075f, .87f));
            GUI.Label(new Rect(32, 471, 470, 25), "마을 채팅", small);
            scroll = GUI.BeginScrollView(new Rect(30, 504, 480, 138), scroll, new Rect(0, 0, 455, Mathf.Max(138, chat.Count * 24)));
            for (int i = 0; i < chat.Count; i++) GUI.Label(new Rect(3, i * 24, 450, 25), chat[i], small);
            GUI.EndScrollView();
            GUI.SetNextControlName("town-chat");
            message = GUI.TextField(new Rect(30, 651, 375, 37), message, 160, field);
            chatFocused = GUI.GetNameOfFocusedControl() == "town-chat";
            bool enter = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && chatFocused;
            if (GUI.Button(new Rect(414, 651, 95, 37), "전송", button) || enter)
            {
                if (!string.IsNullOrWhiteSpace(message)) client.Send(new LanCommand { op = "chat", text = message });
                message = ""; GUI.FocusControl(""); chatFocused = false;
                if (enter) Event.current.Use();
            }
            GUI.Label(new Rect(650, 658, 620, 45), "A/D 이동 · Space 점프 · 우클릭 조준\n1/2 무기 교체 · I 장비", small);
            if (inventory)
            {
                Panel(new Rect(660, 145, 535, 380), new Color(.04f, .06f, .1f, .98f));
                GUI.Label(new Rect(686, 172, 490, 50), "탈환군 장비", title);
                GUI.Label(new Rect(686, 237, 490, 36), $"보유금 {profile.money:N0}  /  부속 {profile.parts}", label);
                for (int i = 0; i < profile.items.Length; i++)
                {
                    var item = profile.items[i];
                    OnlineVisuals.DrawSprite(new Rect(680, 292 + i * 64, 100, 50), visuals.Weapon(item.family, item.tier));
                    GUI.Label(new Rect(790, 298 + i * 64, 275, 45), $"{OnlineVisuals.WeaponName(item)}  +{item.enhance}", label);
                    GUI.enabled = item.id != profile.activeItemId;
                    if (GUI.Button(new Rect(1070, 292 + i * 64, 100, 42), GUI.enabled ? "장착" : "장착 중", button)) Equip(item.id);
                    GUI.enabled = true;
                }
                GUI.Label(new Rect(686, 445, 485, 55), "이 계정의 장비와 재화는 서버에 저장됩니다.", small);
            }
        }

        void Logout(bool user)
        {
            client?.Dispose(); client = null; profile = null; snapshot = null; connecting = false; inventory = false; chatFocused = false;
            chat.Clear(); positions.Clear(); names.Clear(); views.Clear();
            foreach (var a in actors.Values) Destroy(a.gameObject);
            actors.Clear();
            if (user) notice = "로그아웃했습니다.";
        }
        void OnDestroy() { client?.Dispose(); }
        void OnApplicationQuit() { client?.Dispose(); }
    }
}
