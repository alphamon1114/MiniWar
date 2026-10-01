using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniWar.Online
{
    /// <summary>First online slice: accounts, saved starter inventory, authoritative town, two channels and chat.</summary>
    public sealed partial class OnlineLobby : MonoBehaviour
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
        GUIStyle label, title, field, button, small, hudSmall, hudLabel, hudTitle, chatShadow;
        OnlineUiSkin uiSkin;
        string host = "127.0.0.1", portText = "7777", nickname = "", password = "", fingerprint = "";
        string notice = "", message = "";
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
            nickname = PlayerPrefs.GetString("MiniWar.LAN.Nickname", "");
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
            townRoot = new GameObject("Town scenery").transform;
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
                bg.transform.SetParent(townRoot, false);
                bg.sprite = Sprite.Create(backdrop, new Rect(0, 0, backdrop.width, backdrop.height), new Vector2(0.5f, 0.5f), backdrop.height / 13f);
                bg.transform.position = new Vector3(21, -1.1f, 0); bg.sortingOrder = -20;
                bg.transform.localScale = new Vector3(44f / bg.sprite.bounds.size.x, 1, 1);
            }
            else MakePart("Ground", townRoot, new Vector3(21, Ground - 1, 0), new Vector2(44, 2), new Color(0.13f, 0.17f, 0.23f), -5);
            for (int i = 0; i < 3; i++)
            {
                float x = 9 + i * 13;
                var npc = new GameObject("Refuge NPC " + i);
                npc.transform.SetParent(townRoot, false);
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

        long lastShotId;
        int shotsReceived, peerShotsReceived;
        void ClearShots()
        {
            foreach (var shot in FindObjectsByType<PracticeShotView>(FindObjectsSortMode.None)) Destroy(shot.gameObject);
        }
        bool PointerOverUi()
        {
            if (Mouse.current == null) return true;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            Vector2 screen = Mouse.current.position.ReadValue();
            var p = new Vector2((screen.x - (Screen.width - 1280 * scale) / 2) / scale,
                (Screen.height - screen.y - (Screen.height - 720 * scale) / 2) / scale);
            return p.x < 0 || p.x > 1280 || p.y < 0 || p.y > 720 || p.y < 65
                || partyPanel || new Rect(930, 76, 325, 170).Contains(p)
                || new Rect(18,459,505,243).Contains(p);
        }

        void Update()
        {
            UpdateDiscovery();
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
                        else if (rememberLogin)
                        {
                            PlayerPrefs.SetString("MiniWar.LAN.Nickname", nickname); PlayerPrefs.Save();
                        }
                        break;
                    case "snapshot": snapshot = e; channel = e.channel; SyncDungeon(e.dungeon); SyncActors(); break;
                    case "profile": profile = e.profile; notice = e.text; break;
                    case "parties":
                        if (MyParty?.id != e.party?.id)
                        {
                            partyScroll = Vector2.zero;
                            if (e.party != null) { selectedDungeon = e.party.dungeonId; partyPage = 1; }
                            else partyPage = 0;
                        }
                        partyState = e; break;
                    case "party_notice": partyNotice = e.text; notice = e.text; break;
                    case "shot":
                        if (e.shot == null || e.channel != channel || e.shot.id <= lastShotId || (e.instanceId ?? "") != (snapshot?.instanceId ?? "")) break;
                        lastShotId = e.shot.id; shotsReceived++;
                        if (profile != null && e.shot.shooter != profile.nickname) peerShotsReceived++;
                        Vector3 muzzle = new Vector3(e.shot.x, WorldGround + e.shot.y, 0);
                        if (views.TryGetValue(e.shot.shooter, out var shooter)) muzzle = shooter.PlayShot(e.shot);
                        if(!InDungeon)PracticeShotView.Spawn(e.shot, muzzle);
                        break;
                    case "chat": chat.Add($"{e.sender}  {e.text}"); if (chat.Count > 70) chat.RemoveAt(0); scroll.y = float.MaxValue; break;
                    case "channel": channel = e.channel; chat.Clear(); ClearShots(); notice = e.text; break;
                    case "error": notice = e.text; partyNotice = e.text; connecting = false; if (profile == null) { client.Dispose(); client = null; } break;
                    case "disconnected": notice = e.text; Logout(false); break;
                }
            }
            if (profile == null || client == null) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && partyPanel) SetPartyPanel(false);
            bool partyTyping = partyPanel && partyTitleFocused;
            if (kb != null && kb.pKey.wasPressedThisFrame && !chatFocused && !partyTyping) SetPartyPanel(!partyPanel);
            if (kb != null && kb.iKey.wasPressedThisFrame && !chatFocused && !partyPanel) inventory = !inventory;
            if (kb != null && !chatFocused && !partyPanel)
            {
                if (kb.digit1Key.wasPressedThisFrame && profile.equipped.Length > 0) Equip(profile.equipped[0]);
                if (kb.digit2Key.wasPressedThisFrame && profile.equipped.Length > 1) Equip(profile.equipped[1]);
            }
            bool movementAllowed = kb != null && Application.isFocused && !chatFocused && !inventory && !partyPanel;
            if(movementAllowed&&InDungeon&&kb.rKey.wasPressedThisFrame)
                client.Send(new LanCommand{op="dungeon_revive",roomId=dungeonVisit.roomId,roomSequence=dungeonVisit.roomSequence});
            if (HandlePortalInput(kb, movementAllowed)) movementAllowed = false;
            bool jump = movementAllowed && (kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame);
            bool drop = jump && InDungeon && (kb.sKey.isPressed || kb.downArrowKey.isPressed);
            if (drop) jump = false;
            bool dash = movementAllowed && (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame);
            if (Time.unscaledTime >= sendAt || jump || dash || drop)
            {
                sendAt = Time.unscaledTime + 0.05f;
                float move = !movementAllowed ? 0 : (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
                float aimAngle = 0;
                bool fire = movementAllowed && Mouse.current != null && Mouse.current.leftButton.isPressed && Application.isFocused
                    && !chatFocused && !inventory && !PointerOverUi();
                bool aiming = Mouse.current != null && (Mouse.current.rightButton.isPressed || fire) && !chatFocused && !inventory && !partyPanel;
                if (aiming && actors.TryGetValue(profile.nickname, out var self))
                {
                    Vector3 origin = views.TryGetValue(profile.nickname, out var selfView) && selfView.Rig != null
                        ? selfView.Rig.MuzzlePosition : self.position + Vector3.up * CharacterRig.AimHeight;
                    if(InDungeon)origin=self.position+Vector3.up*LanShooting.AimHeight;
                    Vector2 delta = worldCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue()) - origin;
                    aimAngle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                }
                client.Send(new LanCommand { op = "input", sequence = ++sequence, move = move, jump = jump, drop = drop, dash = dash, aimAngle = aimAngle, aiming = aiming, fire = fire });
            }
            if (Time.unscaledTime >= pingAt) { pingAt = Time.unscaledTime + 5; client.Send(new LanCommand { op = "ping" }); }
            foreach (var kv in actors)
            {
                float follow = views.TryGetValue(kv.Key, out var actorView) ? actorView.PositionFollowRate : 18;
                if (positions.TryGetValue(kv.Key, out var desired)) kv.Value.position = Vector3.Lerp(kv.Value.position, desired, 1 - Mathf.Exp(-follow * Time.unscaledDeltaTime));
            }
            if (actors.TryGetValue(profile.nickname, out var own))
            {
                if (InDungeon)
                {
                    var selfState=System.Array.Find(snapshot.actors,a=>a.nickname==profile.nickname);
                    var alive=selfState!=null&&selfState.dead?System.Array.Find(snapshot.actors,a=>!a.dead):null;
                    FollowDungeonCamera(alive!=null&&actors.TryGetValue(alive.nickname,out var ally)?ally.position:own.position);return;
                }
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
                var desired = new Vector3(a.x, WorldGround + a.y, 0);
                if (!actors.TryGetValue(a.nickname, out var root))
                {
                    root = new GameObject("Player " + a.nickname).transform; root.position = desired; actors.Add(a.nickname, root);
                    bool own = profile != null && a.nickname == profile.nickname;
                    var view = root.gameObject.AddComponent<OnlineActorView>(); view.Initialize(visuals); views.Add(a.nickname, view);
                    names[a.nickname] = MakeName(root, a.nickname + (own ? " · 나" : ""), 2.78f);
                }
                positions[a.nickname] = desired;
                views[a.nickname].Apply(a);
                names[a.nickname].text = a.nickname + (a.dead ? " · 관전" : !string.IsNullOrEmpty(a.portalId) ? " · 포탈 대기" : a.nickname == profile?.nickname ? " · 나" : "");
            }
            foreach (string name in actors.Keys.Where(n => !live.Contains(n)).ToArray())
            { Destroy(actors[name].gameObject); actors.Remove(name); positions.Remove(name); names.Remove(name); views.Remove(name); }
        }

        void Styles()
        {
            if (label != null) return;
            uiSkin = new OnlineUiSkin(GUI.skin, font);
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 17, wordWrap = true, richText = false };
            label.normal.textColor = OnlineUiSkin.Ink;
            title = new GUIStyle(label) { fontSize = 30, fontStyle = FontStyle.Bold };
            small = new GUIStyle(label) { fontSize = 13 };
            hudSmall = new GUIStyle(small); hudSmall.normal.textColor = Color.white;
            hudLabel = new GUIStyle(label); hudLabel.normal.textColor = Color.white;
            hudTitle = new GUIStyle(title); hudTitle.normal.textColor = Color.white;
            chatShadow = new GUIStyle(hudSmall); chatShadow.normal.textColor = new Color32(5, 10, 15, 255);
            field = uiSkin.Field; button = uiSkin.Button;
        }

        void UiWindow(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, uiSkin.Window);
            GUI.Box(new Rect(rect.x + 5, rect.y + 5, rect.width - 10, 65), GUIContent.none, uiSkin.Header);
        }
        void UiSlot(Rect rect, bool empty = false) => GUI.Box(rect, GUIContent.none, empty ? uiSkin.Inset : uiSkin.Slot);

        static void Panel(Rect rect, Color color) { Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
        void OnGUI()
        {
            Styles();
            var previousSkin = GUI.skin; GUI.skin = uiSkin.Skin;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            var previous = GUI.matrix; GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2), Quaternion.identity, Vector3.one * scale);
            if (profile == null) DrawLogin(); else DrawLobby();
            GUI.matrix = previous; GUI.skin = previousSkin;
        }

        void DrawLogin()
        {
            Panel(new Rect(0, 0, 1280, 720), new Color(.02f, .03f, .07f, .58f));
            if (localPreview)
            {
                UiWindow(new Rect(340, 230, 600, 260));
                GUI.Label(new Rect(380, 265, 520, 60), connecting ? "마을에 입장하는 중…" : "로컬 미리보기", title);
                GUI.Label(new Rect(380, 338, 520, 66), connecting ? "잠시만 기다려 주세요." : notice, label);
                if (!connecting && GUI.Button(new Rect(480, 422, 320, 45), "다시 연결", button)) StartLocalPreview();
                return;
            }
            GUI.Label(new Rect(65, 85, 650, 70), "MINIWAR", hudTitle);
            GUI.Label(new Rect(68, 155, 610, 70), "잃어버린 왕국을 되찾는 사람들", hudLabel);
            GUI.Label(new Rect(68, 242, 610, 55), "왕국 탈환군", hudTitle);
            OnlineVisuals.DrawSprite(new Rect(175, 305, 260, 270), visuals.Body(selectedBody, true));
            GUI.Label(new Rect(68, 575, 550, 28), "새 계정은 선택한 캐릭터로 생성됩니다.", hudSmall);
            GUI.Label(new Rect(68, 610, 550, 52), "학원 내부망 · Windows\n온라인 기반 개발 빌드 — 공용 마을", hudSmall);
            UiWindow(new Rect(730, 55, 490, 530));
            GUI.Label(new Rect(760, 79, 420, 35), "탈환군 등록 / 접속", label);
            DrawServerSelection();
            GUI.Label(new Rect(760, 212, 420, 25), "닉네임 · 2~16자", small);
            nickname = GUI.TextField(new Rect(760, 237, 425, 42), nickname, 16, field);
            GUI.Label(new Rect(760, 293, 420, 25), "비밀번호 · 8자 이상", small);
            password = GUI.PasswordField(new Rect(760, 318, 425, 42), password, '●', 128, field);
            GUI.enabled = !connecting;
            selectedBody = GUI.SelectionGrid(new Rect(760, 374, 425, 38), selectedBody, new[] { "남성", "여성" }, 2, button);
            GUI.enabled = !connecting && selectedServer != null;
            if (GUI.Button(new Rect(760, 427, 205, 48), "접속", button)) BeginLogin(false);
            if (GUI.Button(new Rect(980, 427, 205, 48), "새 계정 등록", button)) BeginLogin(true);
            GUI.enabled = true;
            GUI.Label(new Rect(760, 496, 425, 77), connecting ? "접속 중…" : notice, small);
        }

        void BeginLogin(bool register)
        {
            if (!localPreview)
            {
                if (selectedServer == null) { notice = "접속불가"; SearchServers(); return; }
                host = selectedServer.Address; portText = selectedServer.Port.ToString(); fingerprint = selectedServer.Fingerprint;
            }
            host = host.Trim(); fingerprint = fingerprint.Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();
            if (host.Length == 0 || !int.TryParse(portText, out int port) || port < 1 || port > 65535) { notice = "올바른 서버 주소와 포트를 입력하세요."; return; }
            if (fingerprint.Length != 64 || fingerprint.Any(c => !Uri.IsHexDigit(c))) { notice = "접속불가"; return; }
            if (password.Length < 8) { notice = "비밀번호는 8자 이상입니다."; return; }
            client?.Dispose(); client = new LanClient(); sequence = 0; connecting = true;
            lastShotId = 0; shotsReceived = 0; peerShotsReceived = 0;
            client.Connect(host, port, fingerprint, nickname, password, register, selectedBody);
        }

        void Equip(string itemId) { if (itemId != profile.activeItemId) client.Send(new LanCommand { op = "equip", itemId = itemId }); }

        void DrawLobby()
        {
            GUI.enabled = !partyPanel;
            GUI.Box(new Rect(8, 4, 1264, 57), GUIContent.none, uiSkin.Header);
            GUI.Label(new Rect(25, 16, 640, 35), InDungeon ? $"{LanDungeons.Find(dungeonVisit.dungeonId)?.Name}  /  {dungeonVisit.roomName}" : localPreview ? "생존자 거점  /  로컬 미리보기" : $"생존자 거점  /  CH {channel}  /  {profile.nickname}", label);
            if (localPreview && GUI.Button(new Rect(488, 12, 190, 40), selectedBody == 0 ? "여성으로 보기" : "남성으로 보기", button))
            {
                selectedBody = 1 - selectedBody; Logout(false); StartLocalPreview(); return;
            }
            for (int i = 1; !InDungeon && i <= LanRules.MaxChannels; i++)
            {
                int count = snapshot != null && snapshot.channelCounts.Length >= i ? snapshot.channelCounts[i - 1] : 0;
                if (GUI.Button(new Rect(690 + (i - 1) * 136, 12, 130, 40), $"CH {i} · {count}/30", button)) client.Send(new LanCommand { op = "channel", channel = i });
            }
            if (GUI.Button(new Rect(972, 12, 130, 40), "장비 [I]", button)) inventory = !inventory;
            if (GUI.Button(new Rect(1110, 12, 145, 40), "로그아웃", button)) { Logout(true); return; }
            GUI.Label(new Rect(25, 80, 875, 40), notice, hudSmall);
            DrawPartySummary();
            DrawOnlinePortals();
            DrawCombatHUD();
            DrawChat();
            GUI.Label(new Rect(650, 650, 620, 65), "A/D 이동 · Space ×2 점프 · Shift 대시 · 우클릭 조준 · 좌클릭 사격\n1/2 무기 교체 · I 장비 · P 파티" + (InDungeon ? " · ↓+Space 하강\nS 포탈 진입 · R 부활 1회 / 이후 관전" : "\n마을 연습탄: 탄약 소모·피해 없음"), hudSmall);
            if (views.TryGetValue(profile.nickname, out var dashView))
                GUI.Label(new Rect(1010, 621, 260, 25), dashView.DashStatus, hudSmall);
            if (inventory)
            {
                UiWindow(new Rect(660, 145, 535, 380));
                GUI.Label(new Rect(686, 172, 490, 50), "탈환군 장비", title);
                GUI.Label(new Rect(686, 237, 490, 36), $"보유금 {profile.money:N0}  /  부속 {profile.parts}", label);
                for (int i = 0; i < profile.items.Length; i++)
                {
                    var item = profile.items[i];
                    UiSlot(new Rect(677, 284 + i * 64, 500, 58));
                    OnlineVisuals.DrawSprite(new Rect(680, 292 + i * 64, 100, 50), visuals.Weapon(item.family, item.tier));
                    GUI.Label(new Rect(790, 298 + i * 64, 275, 45), $"{OnlineVisuals.WeaponName(item)}  +{item.enhance}", label);
                    GUI.enabled = item.id != profile.activeItemId;
                    if (GUI.Button(new Rect(1070, 292 + i * 64, 100, 42), GUI.enabled ? "장착" : "장착 중", button)) Equip(item.id);
                    GUI.enabled = true;
                }
                GUI.Label(new Rect(686, 445, 485, 55), "이 계정의 장비와 재화는 서버에 저장됩니다.", small);
            }
            GUI.enabled = true;
            if (partyPanel) DrawPartyPanel();
        }

        void DrawChat()
        {
            var previousSkin = GUI.skin;
            GUI.skin = uiSkin.ChatSkin;
            GUI.Box(new Rect(18, 459, 505, 243), GUIContent.none, uiSkin.ChatWindow);
            GUI.Box(new Rect(23, 464, 495, 34), GUIContent.none, uiSkin.ChatHeader);
            ChatText(new Rect(32, 471, 470, 25), InDungeon ? "파티 채팅" : "마을 채팅");
            scroll = GUI.BeginScrollView(new Rect(30, 504, 480, 138), scroll, new Rect(0, 0, 455, Mathf.Max(138, chat.Count * 24)));
            for (int i = 0; i < chat.Count; i++) ChatText(new Rect(3, i * 24, 450, 25), chat[i]);
            GUI.EndScrollView();
            GUI.SetNextControlName("town-chat");
            message = GUI.TextField(new Rect(30, 651, 375, 37), message, 160, uiSkin.ChatField);
            chatFocused = GUI.GetNameOfFocusedControl() == "town-chat";
            bool enter = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && chatFocused;
            if (GUI.Button(new Rect(414, 651, 95, 37), "전송", uiSkin.ChatButton) || enter)
            {
                if (!string.IsNullOrWhiteSpace(message)) client.Send(new LanCommand { op = "chat", text = message });
                message = ""; GUI.FocusControl(""); chatFocused = false;
                if (enter) Event.current.Use();
            }
            GUI.skin = previousSkin;
        }

        void ChatText(Rect rect, string text)
        {
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, chatShadow);
            GUI.Label(rect, text, hudSmall);
        }

        void Logout(bool user)
        {
            SyncDungeon(null);
            client?.Dispose(); client = null; profile = null; snapshot = null; connecting = false; inventory = false; chatFocused = false;
            partyState = null; partyPanel = false; partyNotice = ""; partyScroll = Vector2.zero;
            chat.Clear(); positions.Clear(); names.Clear(); views.Clear();
            ClearShots();
            foreach (var a in actors.Values) Destroy(a.gameObject);
            actors.Clear();
            if (user) notice = "로그아웃했습니다.";
        }
        void OnDestroy() { discoveryStop.Cancel(); client?.Dispose(); uiSkin?.Dispose(); }
        void OnApplicationQuit() { client?.Dispose(); }
    }
}
