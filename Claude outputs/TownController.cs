using System.Collections.Generic;
using MiniWar.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniWar.Runtime
{
    public enum TownPanel
    {
        Walking = 0,   // 마을을 돌아다니는 중 — 기본 상태
        Dialogue = 1,  // NPC 대사 — 일러스트가 뜨고 말을 한다
        Result = 2,    // 방금 돌아온 던전 결과
        Cards = 3,     // 클리어 보상 카드 뒤집기
        Dungeons = 4,  // 출격 게이트 — 던전 선택
        Armory = 5,    // 정비병 — 강화 · 보급
        Workshop = 6,  // 보급 담당관 — 합성 · 분해 · 제작 · 편성
    }

    /// <summary>
    /// 마을. 던전 사이의 모든 준비가 여기서 일어난다.
    ///
    /// 엘소드·던파식 인던 구조를 따른다 — 던전 안에서는 아무것도 못 하고,
    /// 나와서 마을에서 정리한다. 이 분리가 만드는 것은 <b>출발 전 결정</b>이다.
    ///
    /// 무기를 파는 곳은 없다. 무기는 던전에서 카드로 떨어지고,
    /// 마을에서 하는 일은 <b>쌓인 것을 어떻게 정리하느냐</b>다 —
    /// 합칠 것인가, 분해할 것인가, 강화에 걸 것인가.
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

        readonly HashSet<TownNpc> _met = new HashSet<TownNpc>();
        readonly List<int> _materials = new List<int>();   // 합성대에 올린 무기 Id

        string[] _lines = new string[0];
        int _lineIndex;

        public Loadout Loadout { get; private set; }
        public ArmoryShop Armory { get; private set; }
        public WorkshopShop Workshop { get; private set; }

        public TownPanel Panel { get; private set; } = TownPanel.Walking;
        public int DungeonCursor { get; private set; }
        public int CardCursor { get; private set; }
        public string Notice { get; private set; } = "";

        /// <summary>정비소와 작업대가 각자 가리키는 보유 목록 위치.</summary>
        public int ArmoryCursor { get; private set; }
        public int WorkshopCursor { get; private set; }

        /// <summary>이번 강화에 방지권을 걸 것인가. 창을 닫아도 유지된다.</summary>
        public bool UseWedge { get; private set; }
        public bool UseSpareBarrel { get; private set; }

        /// <summary>지금 말을 걸 수 있는 NPC. 없으면 null.</summary>
        public TownNpc Nearby { get; private set; }

        /// <summary>대화 중이거나 창을 연 NPC. 일러스트를 계속 띄워두려고 들고 있는다.</summary>
        public TownNpc Speaker { get; private set; }

        public string CurrentLine
            => _lineIndex >= 0 && _lineIndex < _lines.Length ? _lines[_lineIndex] : "";

        public bool HasMoreLines => _lineIndex < _lines.Length - 1;

        public GameSession Session => _session;
        public PlayerMotor Walker => walker;
        public PlayerProfile Profile => _session != null ? _session.Profile : null;
        public DungeonCatalog Catalog => _session != null ? _session.Catalog : null;

        /// <summary>보유 무기 전체. 정비소·작업대가 같은 목록을 본다.</summary>
        public IReadOnlyList<WeaponInstance> Owned
            => Profile != null ? Profile.Owned : (IReadOnlyList<WeaponInstance>)new WeaponInstance[0];

        /// <summary>합성대에 올라간 무기들. 화면이 이 순서대로 그린다.</summary>
        public List<WeaponInstance> Materials
        {
            get
            {
                var list = new List<WeaponInstance>(_materials.Count);
                foreach (int id in _materials)
                {
                    var w = Profile != null ? Profile.FindById(id) : null;
                    if (w != null) list.Add(w);
                }
                return list;
            }
        }

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

            Armory = new ArmoryShop(_session.Profile);
            Workshop = new WorkshopShop(_session.Profile, _session.Weapons);
            RebuildLoadout();

            if (walker != null)
            {
                walker.HardMinX = leftWall;
                walker.MaxX = rightWall;
            }

            // 돌아온 직후라면 결과부터, 그 다음이 카드다.
            if (_session.LastResult.valid) Panel = TownPanel.Result;
            else if (_session.HasPendingCards) Panel = TownPanel.Cards;
            else Panel = TownPanel.Walking;

            DungeonCursor = FirstUnclearedUnlocked();
        }

        /// <summary>
        /// 편성된 무기만으로 로드아웃을 다시 만든다. 편성이 바뀌거나 무기가 사라지면 불러야 한다.
        /// </summary>
        public void RebuildLoadout()
        {
            if (Loadout != null) _session.Profile.Capture(Loadout);

            Loadout = new Loadout(_session.Profile.EquippedList());
            _session.Profile.Restore(Loadout);

            if (Loadout.Weapons.Count == 0)
            {
                Debug.LogWarning("편성된 무기가 없습니다. 작업대에서 한 정이라도 칸에 넣으세요.");
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

            ClampCursors();

            switch (Panel)
            {
                case TownPanel.Walking: TickWalking(kb); break;
                case TownPanel.Dialogue: TickDialogue(kb); break;
                case TownPanel.Result: TickResult(kb); break;
                case TownPanel.Cards: TickCards(kb); break;
                case TownPanel.Dungeons: TickDungeonSelect(kb); break;
                case TownPanel.Armory: TickArmory(kb); break;
                case TownPanel.Workshop: TickWorkshop(kb); break;
            }
        }

        /// <summary>
        /// 무기는 분해·합성·파괴로 사라진다. 커서가 사라진 자리를 가리키고 있으면
        /// 다음 조작이 엉뚱한 무기에 걸리므로 매 프레임 범위를 붙잡아 둔다.
        /// </summary>
        void ClampCursors()
        {
            int n = Owned.Count;
            ArmoryCursor = n == 0 ? 0 : Mathf.Clamp(ArmoryCursor, 0, n - 1);
            WorkshopCursor = n == 0 ? 0 : Mathf.Clamp(WorkshopCursor, 0, n - 1);

            for (int i = _materials.Count - 1; i >= 0; i--)
            {
                if (Profile == null || Profile.FindById(_materials[i]) == null)
                    _materials.RemoveAt(i);
            }
        }

        public WeaponInstance ArmoryTarget
            => Owned.Count == 0 ? null : Owned[Mathf.Clamp(ArmoryCursor, 0, Owned.Count - 1)];

        public WeaponInstance WorkshopTarget
            => Owned.Count == 0 ? null : Owned[Mathf.Clamp(WorkshopCursor, 0, Owned.Count - 1)];

        public bool IsMaterial(WeaponInstance w) => w != null && _materials.Contains(w.Id);

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
            // NPC 앞에서 점프할 때마다 창이 열리면 못 쓴다.
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
        /// 말을 건다. 창은 <b>대사를 지나야</b> 열린다 —
        /// 엘소드·던파가 그렇듯, NPC가 한 번 말을 하고 나서 창이 뜨면
        /// 그 창이 메뉴가 아니라 사람에게 부탁하는 일처럼 읽힌다.
        /// </summary>
        void Talk(TownNpc npc)
        {
            Speaker = npc;
            ClearMessages();

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

            if (Speaker != null) Open(Speaker.Station);
            else Close();
        }

        void Open(TownStation station)
        {
            ClearMessages();

            switch (station)
            {
                case TownStation.Armory: Panel = TownPanel.Armory; break;
                case TownStation.Workshop: Panel = TownPanel.Workshop; break;
                case TownStation.Gate: Panel = TownPanel.Dungeons; break;
            }
        }

        void Close()
        {
            Panel = TownPanel.Walking;
            Speaker = null;
            ClearMessages();
        }

        void ClearMessages()
        {
            Notice = "";
            if (Armory != null) Armory.ClearMessage();
            if (Workshop != null) Workshop.ClearMessage();
        }

        /// <summary>모든 화면은 Esc로 닫힌다. 하나뿐인 규칙이라 헤맬 일이 없다.</summary>
        bool CloseRequested(Keyboard kb)
            => kb.escapeKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame;

        static bool Confirmed(Keyboard kb)
            => kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;

        /// <summary>목록을 위아래로. W/S와 화살표 둘 다 받는다.</summary>
        static int MoveCursor(Keyboard kb, int cursor, int count)
        {
            if (count <= 0) return 0;
            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
                return (cursor - 1 + count) % count;
            if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
                return (cursor + 1) % count;
            return cursor;
        }

        // ── 던전 결과 ────────────────────────────────────────────

        void TickResult(Keyboard kb)
        {
            if (!Confirmed(kb) && !kb.spaceKey.wasPressedThisFrame
                && !kb.escapeKey.wasPressedThisFrame) return;

            _session.ClearResult();

            // 결과를 닫으면 곧바로 카드다. 이 순서가 "고생 → 보상"을 만든다.
            Panel = _session.HasPendingCards ? TownPanel.Cards : TownPanel.Walking;
            CardCursor = 0;
        }

        // ── 보상 카드 ────────────────────────────────────────────

        void TickCards(Keyboard kb)
        {
            var cards = _session.PendingCards;
            if (cards == null || cards.Count == 0) { Panel = TownPanel.Walking; return; }

            // 카드는 Esc로 못 닫는다. 한 장은 반드시 골라야 한다 —
            // 실수로 닫아 보상을 통째로 날리는 일이 없어야 한다.
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)
                CardCursor = (CardCursor - 1 + cards.Count) % cards.Count;
            else if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
                CardCursor = (CardCursor + 1) % cards.Count;

            if (!Confirmed(kb) && !kb.spaceKey.wasPressedThisFrame) return;

            var picked = cards[CardCursor];
            _session.TakeCard(CardCursor);

            Notice = $"{picked.Label} 획득";
            CardCursor = 0;
            Panel = TownPanel.Walking;

            // 무기가 빈 칸에 자동으로 들어갔을 수 있다.
            if (picked.kind == RewardKind.Weapon) RebuildLoadout();
        }

        // ── 출격 게이트 ──────────────────────────────────────────

        void TickDungeonSelect(Keyboard kb)
        {
            if (CloseRequested(kb)) { Close(); return; }

            var catalog = Catalog;
            if (catalog == null || catalog.Count == 0) return;

            DungeonCursor = MoveCursor(kb, DungeonCursor, catalog.Count);

            if (!Confirmed(kb)) return;

            if (!_session.IsUnlocked(DungeonCursor))
            {
                Notice = "앞 던전을 먼저 클리어해야 열린다";
                return;
            }

            if (Loadout == null || Loadout.Weapons.Count == 0)
            {
                Notice = "무기를 한 정도 편성하지 않았다";
                return;
            }

            var stage = catalog.At(DungeonCursor);
            _session.Profile.Capture(Loadout);
            _session.EnterDungeon(stage);
        }

        // ── 정비소 — 강화 · 보급 ─────────────────────────────────

        void TickArmory(Keyboard kb)
        {
            if (CloseRequested(kb)) { Close(); return; }

            ArmoryCursor = MoveCursor(kb, ArmoryCursor, Owned.Count);

            // 방지권은 켜고 끄는 값이다. 걸어둔 채로 창을 닫아도 유지된다 —
            // 매번 다시 켜게 하면 정작 중요한 판에서 빠뜨린다.
            if (kb.qKey.wasPressedThisFrame) UseWedge = !UseWedge;
            if (kb.eKey.wasPressedThisFrame) UseSpareBarrel = !UseSpareBarrel;

            var target = ArmoryTarget;
            if (target == null) return;

            bool changed = false;

            if (kb.zKey.wasPressedThisFrame || Confirmed(kb))
            {
                bool wedge = UseWedge && EnhanceTable.FailureAt(target.Enhance) != EnhanceFailure.Keep;
                bool barrel = UseSpareBarrel
                           && EnhanceTable.FailureAt(target.Enhance) == EnhanceFailure.DropOrDestroy;

                changed = Armory.TryEnhance(target, wedge, barrel);
            }
            else if (kb.tKey.wasPressedThisFrame)
            {
                changed = UseBestTalisman(target);
            }
            else if (kb.rKey.wasPressedThisFrame)
            {
                changed = Armory.TryReload(target);
            }
            else if (kb.fKey.wasPressedThisFrame)
            {
                Armory.ReloadAll(Loadout);
                changed = true;
            }

            // 강화로 파괴됐다면 편성 칸이 비었을 수 있다.
            if (changed) RebuildLoadout();
        }

        /// <summary>
        /// 쓸 수 있는 부적 중 <b>가장 낮은 것</b>을 쓴다.
        /// 높은 부적을 낮은 총에 태우면 그대로 손해라, 아끼는 쪽이 기본이어야 한다.
        /// </summary>
        bool UseBestTalisman(WeaponInstance target)
        {
            var list = Profile.Talismans;
            int best = -1;

            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (target.Tier > t.maxTier) continue;
                if (t.level <= target.Enhance) continue;
                if (best < 0 || t.level < list[best].level) best = i;
            }

            if (best < 0)
            {
                Notice = "쓸 수 있는 부적이 없다";
                return false;
            }

            return Armory.TryUseTalisman(best, target);
        }

        // ── 작업대 — 합성 · 분해 · 제작 · 편성 ───────────────────

        void TickWorkshop(Keyboard kb)
        {
            if (CloseRequested(kb)) { Close(); return; }

            WorkshopCursor = MoveCursor(kb, WorkshopCursor, Owned.Count);

            if (kb.cKey.wasPressedThisFrame) { Workshop.TryCraftWedge(); return; }
            if (kb.vKey.wasPressedThisFrame) { Workshop.TryCraftSpareBarrel(); return; }

            var target = WorkshopTarget;

            if (kb.spaceKey.wasPressedThisFrame && target != null) { ToggleMaterial(target); return; }

            if (Confirmed(kb)) { Fuse(); return; }

            if (target == null) return;

            if (kb.xKey.wasPressedThisFrame)
            {
                if (Workshop.TrySalvage(target)) RebuildLoadout();
                return;
            }

            // 숫자키로 칸에 넣는다. 이미 그 칸에 있으면 뺀다 — 같은 키로 넣고 빼는 편이 헤매지 않는다.
            for (int slot = 0; slot < Loadout.SlotCount && slot < 2; slot++)
            {
                var key = slot == 0 ? kb.digit1Key : kb.digit2Key;
                if (!key.wasPressedThisFrame) continue;

                bool here = Profile.EquippedIn(slot) == target;
                if (Workshop.TryEquip(slot, here ? null : target)) RebuildLoadout();
                return;
            }
        }

        void ToggleMaterial(WeaponInstance w)
        {
            if (_materials.Remove(w.Id)) return;

            if (Profile.IsEquipped(w))
            {
                Notice = "편성된 무기는 재료로 쓸 수 없다. 칸에서 먼저 빼라";
                return;
            }

            if (_materials.Count >= FusionTable.MaterialCount)
            {
                Notice = $"재료는 {FusionTable.MaterialCount}정까지다";
                return;
            }

            _materials.Add(w.Id);
            Notice = "";
        }

        void Fuse()
        {
            var mats = Materials;
            if (mats.Count == 0) { Notice = "합성대가 비어 있다"; return; }

            var made = Workshop.TryFuse(mats);
            if (made == null) return;

            _materials.Clear();
            RebuildLoadout();
        }

        /// <summary>합성대에 올린 것들이 어떤 결과를 낼지. 화면이 미리 보여준다.</summary>
        public FusionVerdict FusionPreview() => Workshop != null
            ? Workshop.Preview(Materials)
            : FusionVerdict.NeedThree;
    }
}
