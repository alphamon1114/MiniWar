using MiniWar.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniWar.Runtime
{
    public enum TownPanel
    {
        Walking = 0,  // 마을을 돌아다니는 중 — 기본 상태
        Dialogue = 1, // NPC 대사 — 일러스트가 뜨고 말을 한다
        Result = 2,   // 방금 돌아온 던전 결과
        Dungeons = 3, // 출격 게이트 — 던전 선택
        Armory = 4,   // 정비병 — 강화 · 보급
        Market = 5,   // 보급 담당관 — 구매 · 장착
    }

    /// <summary>
    /// 마을. 던전 사이의 모든 준비가 여기서 일어난다.
    ///
    /// 엘소드·던파식 인던 구조를 따른다 — 던전 안에서는 아무것도 못 사고,
    /// 나와서 마을에서 돈을 쓴다. 이 분리가 만드는 것은 <b>출발 전 결정</b>이다.
    /// "강화에 쓸 것인가, 탄약을 채울 것인가"를 던전 밖에서 한 번에 정하고 들어가면,
    /// 던전 안의 실패는 조작이 아니라 준비의 실패가 된다.
    ///
    /// 마을은 걸어다니는 1자 맵이다. 정비소와 무기상이 서로 다른 자리에 있고
    /// 오른쪽 끝이 출격 게이트라, 준비 과정이 <b>동선</b>으로 드러난다.
    /// </summary>
    public sealed class TownController : MonoBehaviour
    {
        [SerializeField] GameSession sessionInScene;
        [SerializeField] PlayerMotor walker;

        [Header("마을 범위")]
        [SerializeField] float leftWall = -4f;
        [SerializeField] float rightWall = 36f;

        GameSession _session;
        TownNpc _autoOpened;   // 자동으로 열린 NPC — 사거리를 벗어나야 다시 열린다

        readonly System.Collections.Generic.HashSet<TownNpc> _met
            = new System.Collections.Generic.HashSet<TownNpc>();

        string[] _lines = new string[0];
        int _lineIndex;

        public Loadout Loadout { get; private set; }
        public UpgradeShop Shop { get; private set; }
        public TownPanel Panel { get; private set; } = TownPanel.Walking;
        public int DungeonCursor { get; private set; }
        public int MarketCursor { get; private set; }
        public string Notice { get; private set; } = "";

        /// <summary>지금 말을 걸 수 있는 NPC. 없으면 null.</summary>
        public TownNpc Nearby { get; private set; }

        /// <summary>대화 중이거나 상점을 연 NPC. 일러스트를 계속 띄워두려고 들고 있는다.</summary>
        public TownNpc Speaker { get; private set; }

        public string CurrentLine
            => _lineIndex >= 0 && _lineIndex < _lines.Length ? _lines[_lineIndex] : "";

        public bool HasMoreLines => _lineIndex < _lines.Length - 1;

        /// <summary>무기상 목록. 칸 순서 → 값 순서.</summary>
        public WeaponData[] Market { get; private set; } = new WeaponData[0];

        public GameSession Session => _session;
        public PlayerMotor Walker => walker;
        public PlayerProfile Profile => _session != null ? _session.Profile : null;
        public DungeonCatalog Catalog => _session != null ? _session.Catalog : null;

        public bool IsPanelOpen => Panel != TownPanel.Walking;

        void Start()
        {
            _session = GameSession.Instance != null ? GameSession.Instance : sessionInScene;
            if (_session == null)
            {
                Debug.LogError("GameSession이 없습니다. MiniWar → 씬 생성을 다시 실행하세요.");
                enabled = false;
                return;
            }

            RebuildLoadout();
            Shop = new UpgradeShop(_session.UpgradeTable, _session.Profile);
            BuildMarketList();

            if (walker != null)
            {
                walker.HardMinX = leftWall;
                walker.MaxX = rightWall;
            }

            // 돌아온 직후라면 결과부터 보여준다.
            Panel = _session.LastResult.valid ? TownPanel.Result : TownPanel.Walking;

            DungeonCursor = FirstUnclearedUnlocked();
        }

        void BuildMarketList()
        {
            var market = new System.Collections.Generic.List<WeaponData>();
            if (_session.AllWeapons != null)
            {
                foreach (var w in _session.AllWeapons)
                    if (w != null) market.Add(w);
            }

            // 같은 칸의 무기가 붙어 있어야 비교가 된다.
            market.Sort((a, b) => a.role != b.role
                ? a.role.CompareTo(b.role)
                : a.price.CompareTo(b.price));

            Market = market.ToArray();
        }

        /// <summary>
        /// 장착한 무기만으로 로드아웃을 다시 만든다. 무기를 사거나 갈아 끼우면 불러야 한다 —
        /// WeaponInstance는 WeaponData 하나에 묶여 있어서 목록이 바뀌면 새로 만드는 수밖에 없다.
        /// </summary>
        public void RebuildLoadout()
        {
            if (Loadout != null) _session.Profile.Capture(Loadout);

            Loadout = new Loadout(_session.Profile.EquippedList(), _session.UpgradeTable);
            _session.Profile.Restore(Loadout);

            if (Loadout.Weapons.Count == 0)
            {
                Debug.LogError("장착된 무기가 하나도 없습니다. 무기 에셋이 정리되지 않았을 수 있습니다 — "
                             + "MiniWar → 씬 생성 (마을 + 던전) 을 다시 실행하세요.");
            }
        }

        int FirstUnclearedUnlocked()
        {
            var catalog = Catalog;
            if (catalog == null) return 0;

            for (int i = 0; i < catalog.Count; i++)
            {
                if (!_session.IsUnlocked(i)) break;
                if (!_session.Profile.IsCleared(catalog.At(i))) return i;
            }
            return Mathf.Max(0, catalog.Count - 1);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || _session == null) return;

            // 화면이 열려 있는 동안에는 걷지 않는다.
            if (walker != null) walker.InputLocked = IsPanelOpen;

            switch (Panel)
            {
                case TownPanel.Walking: TickWalking(kb); break;
                case TownPanel.Dialogue: TickDialogue(kb); break;
                case TownPanel.Result: TickResult(kb); break;
                case TownPanel.Dungeons: TickDungeonSelect(kb); break;
                case TownPanel.Armory: TickArmory(kb); break;
                case TownPanel.Market: TickMarket(kb); break;
            }
        }

        // ── 걸어다니기 ───────────────────────────────────────────

        void TickWalking(Keyboard kb)
        {
            float x = walker != null ? walker.transform.position.x : 0f;

            Nearby = FindNearest(x);

            // 자동으로 열리는 곳(게이트)은 한 번 벗어나야 다시 열린다 —
            // 닫자마자 같은 자리에서 또 열리면 빠져나올 수가 없다.
            if (_autoOpened != null && !_autoOpened.InRange(x)) _autoOpened = null;

            if (Nearby == null) return;

            if (Nearby.AutoOpen && _autoOpened != Nearby)
            {
                _autoOpened = Nearby;
                Talk(Nearby);
                return;
            }

            // 상호작용은 Ctrl. W는 점프로 넘겼으므로 여기서 쓰면 안 된다 —
            // NPC 앞에서 점프할 때마다 상점이 열리면 못 쓴다.
            if (kb.leftCtrlKey.wasPressedThisFrame || kb.rightCtrlKey.wasPressedThisFrame)
                Talk(Nearby);
        }

        TownNpc FindNearest(float x)
        {
            TownNpc best = null;
            float bestDistance = float.MaxValue;

            foreach (var npc in TownNpc.All)
            {
                if (npc == null || !npc.InRange(x)) continue;

                float d = npc.DistanceTo(x);
                if (d >= bestDistance) continue;

                best = npc;
                bestDistance = d;
            }

            return best;
        }

        /// <summary>
        /// 말을 건다. 상점은 <b>대사를 지나야</b> 열린다 —
        /// 엘소드·던파가 그렇듯, NPC가 한 번 말을 하고 나서 창이 뜨면
        /// 상점이 메뉴가 아니라 사람에게 부탁하는 일처럼 읽힌다.
        /// </summary>
        void Talk(TownNpc npc)
        {
            Speaker = npc;
            Notice = "";
            Shop.ClearMessage();

            _lines = npc.LinesFor(!_met.Contains(npc));
            _lineIndex = 0;
            _met.Add(npc);

            if (_lines.Length == 0) { Open(npc.Station); return; }

            Panel = TownPanel.Dialogue;
        }

        void TickDialogue(Keyboard kb)
        {
            if (kb.escapeKey.wasPressedThisFrame) { Close(); return; }

            bool advance = kb.leftCtrlKey.wasPressedThisFrame || kb.rightCtrlKey.wasPressedThisFrame
                        || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
                        || kb.spaceKey.wasPressedThisFrame;
            if (!advance) return;

            if (HasMoreLines) { _lineIndex++; return; }

            // 대사가 끝나면 그 NPC의 창으로 넘어간다.
            if (Speaker != null) Open(Speaker.Station);
            else Close();
        }

        void Open(TownStation station)
        {
            Notice = "";
            Shop.ClearMessage();

            switch (station)
            {
                case TownStation.Armory: Panel = TownPanel.Armory; break;
                case TownStation.Market: Panel = TownPanel.Market; break;
                case TownStation.Gate: Panel = TownPanel.Dungeons; break;
            }
        }

        void Close()
        {
            Panel = TownPanel.Walking;
            Speaker = null;
            Notice = "";
            Shop.ClearMessage();
        }

        /// <summary>모든 화면은 Esc로 닫힌다. 하나뿐인 규칙이라 헤맬 일이 없다.</summary>
        bool CloseRequested(Keyboard kb)
            => kb.escapeKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame;

        // ── 던전 결과 ────────────────────────────────────────────

        void TickResult(Keyboard kb)
        {
            if (!kb.enterKey.wasPressedThisFrame && !kb.numpadEnterKey.wasPressedThisFrame
                && !kb.spaceKey.wasPressedThisFrame && !kb.escapeKey.wasPressedThisFrame) return;

            _session.ClearResult();
            Panel = TownPanel.Walking;
        }

        // ── 출격 게이트 ──────────────────────────────────────────

        void TickDungeonSelect(Keyboard kb)
        {
            if (CloseRequested(kb)) { Close(); return; }

            var catalog = Catalog;
            if (catalog == null || catalog.Count == 0) return;

            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
                DungeonCursor = (DungeonCursor - 1 + catalog.Count) % catalog.Count;
            else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
                DungeonCursor = (DungeonCursor + 1) % catalog.Count;

            if (!kb.enterKey.wasPressedThisFrame && !kb.numpadEnterKey.wasPressedThisFrame) return;

            if (!_session.IsUnlocked(DungeonCursor))
            {
                Notice = "앞 던전을 먼저 클리어해야 열린다";
                return;
            }

            var stage = catalog.At(DungeonCursor);
            _session.Profile.Capture(Loadout);
            _session.EnterDungeon(stage);
        }

        // ── 무기상 ───────────────────────────────────────────────

        void TickMarket(Keyboard kb)
        {
            if (CloseRequested(kb)) { Close(); return; }
            if (Market.Length == 0) return;

            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
                MarketCursor = (MarketCursor - 1 + Market.Length) % Market.Length;
            else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
                MarketCursor = (MarketCursor + 1) % Market.Length;

            if (!kb.enterKey.wasPressedThisFrame && !kb.numpadEnterKey.wasPressedThisFrame) return;

            var data = Market[MarketCursor];
            var profile = _session.Profile;

            // 같은 키로 산다 / 끼운다. 가지고 있으면 살 이유가 없고, 없으면 끼울 수가 없다.
            bool changed = profile.Owns(data) ? Shop.TryEquip(data) : Shop.TryBuyWeapon(data);
            if (changed) RebuildLoadout();
        }

        // ── 정비소 ───────────────────────────────────────────────

        void TickArmory(Keyboard kb)
        {
            if (CloseRequested(kb)) { Close(); return; }

            var current = Loadout.Current;
            bool changed = false;

            if (kb.digit1Key.wasPressedThisFrame) Loadout.SelectSlot(1);
            else if (kb.digit2Key.wasPressedThisFrame) Loadout.SelectSlot(2);
            else if (kb.digit3Key.wasPressedThisFrame) Loadout.SelectSlot(3);
            else if (kb.digit4Key.wasPressedThisFrame) Loadout.SelectSlot(4);

            if (current != null)
            {
                if (kb.zKey.wasPressedThisFrame) changed = Shop.TryBuy(current, UpgradeAxis.Damage);
                else if (kb.xKey.wasPressedThisFrame) changed = Shop.TryBuy(current, UpgradeAxis.FireRate);
                else if (kb.cKey.wasPressedThisFrame) changed = Shop.TryBuy(current, UpgradeAxis.MagazineSize);
                else if (kb.vKey.wasPressedThisFrame) changed = Shop.TryBuy(current, UpgradeAxis.ReloadCost);
                else if (kb.rKey.wasPressedThisFrame) changed = Shop.TryReload(current);
            }

            if (kb.fKey.wasPressedThisFrame) { Shop.ReloadAll(Loadout); changed = true; }

            // 바뀐 프레임에만 저장한다. 매 프레임 부르면 무기 수만큼 배열이 새로 생긴다.
            if (changed) _session.Profile.Capture(Loadout);
        }
    }
}
