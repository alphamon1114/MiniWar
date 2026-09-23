using System.Text;
using MiniWar.Data;
using UnityEngine;

namespace MiniWar.Runtime
{
    /// <summary>
    /// 마을 화면. 걸어다닐 때는 월드 위에 이름표와 말걸기 안내만 띄우고,
    /// 말을 걸면 그때 화면이 덮인다 — 마을이 배경이 아니라 장소로 남게.
    /// </summary>
    [RequireComponent(typeof(TownController))]
    public sealed class TownHud : MonoBehaviour
    {
        /// <summary>대사창 높이. 일러스트를 여기에 걸쳐 앉히므로 두 군데서 같은 값을 쓴다.</summary>
        const float DialogueBoxH = 150f;

        /// <summary>위쪽 상태 바 높이. 일러스트가 여기를 침범하지 않게.</summary>
        const float StatusBarH = 74f;

        TownController _town;
        Camera _camera;

        /// <summary>이번 프레임에 그린 일러스트의 오른쪽 끝. 글과 패널을 그만큼 민다.</summary>
        float _speakerRight;

        /// <summary>이번 프레임 일러스트가 앉을 자리. 자리는 먼저 재고, 그림은 맨 마지막에 올린다.</summary>
        Rect _speakerBox;
        bool _hasSpeaker;
        GUIStyle _label, _small, _title, _center, _right, _tag, _dialogue;

        /// <summary>상단 바는 어두운 반투명이라 글자를 밝은 쪽으로 뒤집어야 한다.</summary>
        GUIStyle _labelLight, _titleLight, _rightLight;

        void Awake() => _town = GetComponent<TownController>();

        void Ensure()
        {
            if (_camera == null) _camera = Camera.main;
            if (_label != null) return;

            _label = HudStyle.Label(16);
            _small = HudStyle.Label(13);
            _title = HudStyle.Label(26);
            _center = HudStyle.Label(15, TextAnchor.UpperCenter);
            _right = HudStyle.Label(16, TextAnchor.UpperRight);
            _tag = HudStyle.Label(15, TextAnchor.UpperCenter);

            _labelLight = HudStyle.Label(16, TextAnchor.UpperLeft, HudStyle.InkLight);
            _titleLight = HudStyle.Label(26, TextAnchor.UpperLeft, HudStyle.InkLight);
            _rightLight = HudStyle.Label(16, TextAnchor.UpperRight, HudStyle.InkLight);

            _dialogue = HudStyle.Label(18, TextAnchor.UpperLeft);
            _dialogue.wordWrap = true;
        }

        void OnGUI()
        {
            if (_town == null || _town.Profile == null) return;
            Ensure();

            DrawWorldTags();
            DrawStatusBar();

            if (!_town.IsPanelOpen)
            {
                DrawWalkHints();
                return;
            }

            HudStyle.DimScreen();

            // 자리부터 잰다. 대사 글줄과 상점 창이 이 폭을 피해 배치되기 때문이다.
            _speakerRight = 0f;
            _hasSpeaker = false;
            // 결과와 카드는 NPC와 무관한 화면이다. 그림이 남아 있으면 창이 옆으로 밀려 어색하다.
            if (_town.Speaker != null
                && _town.Panel != TownPanel.Result
                && _town.Panel != TownPanel.Cards) MeasureSpeaker();

            switch (_town.Panel)
            {
                case TownPanel.Dialogue: DrawDialogue(); break;
                case TownPanel.Result: DrawResult(); break;
                case TownPanel.Cards: DrawCards(); break;
                case TownPanel.Dungeons: DrawDungeons(); break;
                case TownPanel.Armory: DrawArmory(); break;
                case TownPanel.Workshop: DrawWorkshop(); break;
            }

            // 그림은 맨 마지막에. OnGUI는 부른 순서대로 겹쳐 쌓이므로,
            // 대사창 뒤에 그리면 인물이 허리에서 잘려 보인다 — 인물이 창 앞으로 나와야 한다.
            if (_hasSpeaker) DrawSpeaker();
        }

        // ── 월드 위 표시 ─────────────────────────────────────────

        /// <summary>화면 좌표로. GUI는 y가 위에서 아래로 늘어난다.</summary>
        bool ToScreen(Vector3 world, out Vector2 screen)
        {
            screen = Vector2.zero;
            if (_camera == null) return false;

            var p = _camera.WorldToScreenPoint(world);
            if (p.z < 0f) return false;

            screen = new Vector2(p.x, Screen.height - p.y);
            return true;
        }

        void DrawWorldTags()
        {
            foreach (var npc in TownNpc.All)
            {
                if (npc == null) continue;
                if (!ToScreen(npc.transform.position + Vector3.up * 1.6f, out var at)) continue;

                bool active = _town.Nearby == npc;

                var box = new Rect(at.x - 80f, at.y - 40f, 160f, 42f);
                HudStyle.Fill(box, active
                    ? new Color(0.10f, 0.10f, 0.12f, 0.92f)
                    : new Color(0.965f, 0.960f, 0.945f, 0.85f));

                string ink = active ? "#f5f3ee" : "#1a1a1f";

                GUI.Label(new Rect(box.x, box.y + 4f, box.width, 22f),
                          $"<color={ink}><b>{npc.DisplayName}</b></color>", _tag);
                GUI.Label(new Rect(box.x, box.y + 22f, box.width, 20f),
                          $"<color={(active ? "#c9c5bb" : HudStyle.HexDim)}>{npc.Role}</color>", _tag);
            }
        }

        void DrawWalkHints()
        {
            var walker = _town.Walker;
            var nearby = _town.Nearby;

            if (walker != null && nearby != null && !nearby.AutoOpen
                && ToScreen(walker.transform.position + Vector3.up * 1.3f, out var at))
            {
                var box = new Rect(at.x - 70f, at.y - 34f, 140f, 30f);
                HudStyle.Fill(box, new Color(0.10f, 0.10f, 0.12f, 0.92f));
                GUI.Label(new Rect(box.x, box.y + 5f, box.width, 24f),
                          "<color=#f5f3ee><b>Ctrl</b> — 말을 건다</color>", _tag);
            }

            // 배경이 황혼 그림으로 바뀌면서 월드 위의 맨 글자는 읽히지 않는다.
            // 조작 안내는 항상 보여야 하는 줄이므로 자기 바탕을 깔고 간다.
            var strip = new Rect(20, Screen.height - 52, 520, 34);
            HudStyle.Fill(strip, new Color(0.10f, 0.10f, 0.12f, 0.72f));

            GUI.Label(new Rect(strip.x + 12, strip.y + 6, strip.width - 24, 26),
                "<color=#c9c5bb>A / D 이동 · 스페이스 또는 W 점프 · Ctrl 대화</color>", _label);
        }

        void DrawStatusBar()
        {
            var p = _town.Profile;

            // 반투명 어둠. 불투명 흰 판은 도트 배경 위에 종이를 붙여 놓은 것처럼 보인다.
            var box = new Rect(0, 0, Screen.width, StatusBarH);
            HudStyle.Fill(box, HudStyle.StatusBar);
            HudStyle.Fill(new Rect(0, box.height - 2f, Screen.width, 2f),
                          new Color(0.94f, 0.91f, 0.84f, 0.22f));

            GUI.Label(new Rect(28, 10, 400, 34), "전선 기지", _titleLight);

            GUI.Label(new Rect(28, 44, 1000, 24),
                $"<b>$ {p.Money:N0}</b>    "
              + $"<color={HudStyle.HexLightDim}>부속 {p.Parts}  ·  쐐기 {p.Wedges}  ·  총열 {p.SpareBarrels}"
              + $"  ·  클리어 {p.ClearedCount}/{(_town.Catalog != null ? _town.Catalog.Count : 0)}"
              + $"  ·  보유 {p.OwnedCount}정  ·  강화 {p.EnhanceAttempts}회</color>",
                _labelLight);

            // 출격 준비 상태 — 빈 탄창은 여기서 잡힌다.
            int refill = _town.Armory != null ? _town.Armory.FullReloadCost(_town.Loadout) : 0;
            if (refill > 0)
            {
                GUI.Label(new Rect(0, 44, Screen.width - 28, 24),
                    $"<color=#ff7a6b>전탄 보급 ${refill:N0} 필요 — 정비병에게</color>", _rightLight);
            }
        }

        // ── 대사 ─────────────────────────────────────────────────

        /// <summary>
        /// 일러스트가 앉을 자리를 잰다. 그리지는 않는다 —
        /// 대사 글줄과 상점 창이 이 폭을 피해 배치되어야 하므로 치수가 먼저 필요하고,
        /// 그림 자체는 그 창들 위에 올라가야 하므로 마지막에 그린다.
        /// </summary>
        void MeasureSpeaker()
        {
            var npc = _town.Speaker;
            float aspect = CroppedAspect(npc.Portrait, npc.PortraitCrop);

            float top = StatusBarH + 6f;
            float bottom = Screen.height;          // 아래로 꽉

            float h = bottom - top;
            float w = h * aspect;

            // 너무 넓어지면 글 자리를 다 먹는다. 폭을 먼저 제한하고 높이를 다시 낸다.
            float maxW = Screen.width * 0.34f;
            if (w > maxW) { w = maxW; h = w / Mathf.Max(0.01f, aspect); }

            _speakerBox = new Rect(24f, bottom - h, w, h);
            _speakerRight = _speakerBox.xMax;
            _hasSpeaker = true;
        }

        /// <summary>
        /// 말을 건 NPC의 일러스트. 왼쪽 기둥을 세로로 <b>가득 채우고</b>, 대사창 <b>앞</b>에 선다 —
        /// 대사창에서 중요한 건 표정이라, 창에 가려 상반신이 잘리면 있으나 마나다.
        /// 상점 창이 떠 있는 동안에도 남아 있어야 "사람에게 부탁하는 중"으로 읽힌다.
        /// </summary>
        void DrawSpeaker()
        {
            var npc = _town.Speaker;
            if (npc == null) return;

            var portrait = npc.Portrait;
            var box = _speakerBox;

            if (portrait != null && portrait.texture != null)
            {
                GUI.DrawTextureWithTexCoords(box, portrait.texture, npc.PortraitCrop, true);
            }
            else
            {
                // 아직 일러스트가 없을 때. 자리만 잡아둔다.
                HudStyle.Fill(box, new Color(0.965f, 0.960f, 0.945f, 0.25f));
                GUI.Label(new Rect(box.x, box.center.y - 20f, box.width, 40f),
                          $"<color=#c9c5bb>{npc.DisplayName}\n(일러스트 없음)</color>", _tag);
            }
        }

        /// <summary>잘라낸 영역의 가로세로 비. 이걸 써야 그림이 늘어나지 않는다.</summary>
        static float CroppedAspect(Sprite sprite, Rect crop)
        {
            if (sprite == null || sprite.texture == null) return 0.85f;

            float w = sprite.texture.width * crop.width;
            float h = sprite.texture.height * crop.height;
            return h <= 0f ? 0.85f : Mathf.Clamp(w / h, 0.4f, 2.2f);
        }

        void DrawDialogue()
        {
            var npc = _town.Speaker;
            if (npc == null) return;

            float boxH = DialogueBoxH;
            var box = new Rect(24f, Screen.height - boxH - 24f, Screen.width - 48f, boxH);

            HudStyle.Panel(box);

            // 이름표 — 대사창 오른쪽 위에 걸친다. 왼쪽은 일러스트 자리다.
            var plate = new Rect(box.xMax - 324f, box.y - 22f, 300f, 34f);
            HudStyle.Fill(new Rect(plate.x - 2, plate.y - 2, plate.width + 4, plate.height + 4),
                          HudStyle.PanelEdge);
            HudStyle.Fill(plate, new Color(0.10f, 0.10f, 0.12f, 1f));
            GUI.Label(new Rect(plate.x, plate.y + 5f, plate.width, 26f),
                      $"<color=#f5f3ee><b>{npc.DisplayName}</b></color>"
                    + $"  <color=#9a958a>({npc.Role})</color>", _tag);

            // 일러스트가 왼쪽을 차지하므로 글은 그 오른쪽에서 시작한다.
            float textLeft = Mathf.Max(box.x + 24f, _speakerRight + 24f);
            float textWidth = box.xMax - textLeft - 24f;

            GUI.Label(new Rect(textLeft, box.y + 34f, textWidth, boxH - 60f),
                      _town.CurrentLine, _dialogue);

            GUI.Label(new Rect(box.x, box.yMax - 32f, box.width - 24f, 26f),
                      _town.HasMoreLines
                          ? $"<color={HudStyle.HexDim}>Ctrl — 다음 ▾</color>"
                          : $"<color={HudStyle.HexDim}>Ctrl — 계속 ▾   ·   Esc — 그만</color>", _right);
        }

        // ── 던전 결과 ────────────────────────────────────────────

        void DrawResult()
        {
            var r = _town.Session.LastResult;

            var panel = Center(560f, 300f);
            HudStyle.Panel(panel);

            var sb = new StringBuilder();
            string title = r.cleared ? "던전 클리어" : "귀환 실패";
            string color = r.cleared ? HudStyle.HexGain : HudStyle.HexWarn;

            sb.AppendLine($"<size=22><b><color={color}>{title}</color></b></size>");
            sb.AppendLine($"<color={HudStyle.HexDim}>{(r.stage != null ? r.stage.displayName : "")}</color>");
            sb.AppendLine();

            if (!r.cleared)
                sb.AppendLine($"사유   <color={HudStyle.HexWarn}>{RunState.LabelOf(r.reason)}</color>");

            sb.AppendLine($"격파   {r.kills}");
            sb.AppendLine($"전리품 ${r.earnedInDungeon:N0}");

            if (r.clearReward > 0)
            {
                sb.AppendLine($"보상   <color={HudStyle.HexGain}>+${r.clearReward:N0}</color>"
                            + (r.firstClear ? "   <b>최초 클리어</b>" : ""));
            }

            sb.AppendLine();
            sb.AppendLine($"<b>$ {_town.Profile.Money:N0}</b>");
            sb.AppendLine();

            sb.AppendLine(_town.Session.HasPendingCards
                ? $"<color={HudStyle.HexGain}>Enter — 보상 카드 ▸</color>"
                : $"<color={HudStyle.HexDim}>Enter — 기지로</color>");

            GUI.Label(Inner(panel), sb.ToString(), _label);
        }

        // ── 보상 카드 ────────────────────────────────────────────

        /// <summary>
        /// 던파식 클리어 카드. 뒷면만 보이고 <b>한 장만</b> 가져간다.
        ///
        /// 네 장을 다 주면 그냥 보상이지만, 한 장만 고르게 하면 매번 작은 손실이 남는다.
        /// 그 손실이 다음 판을 돌게 만드는 힘이다.
        /// </summary>
        void DrawCards()
        {
            var cards = _town.Session.PendingCards;
            if (cards == null || cards.Count == 0) return;

            float cardW = 150f, cardH = 210f, gap = 18f;
            float totalW = cards.Count * cardW + (cards.Count - 1) * gap;

            var panel = Center(totalW + 80f, cardH + 160f);
            HudStyle.Panel(panel);
            Header(panel, "전리품", "A/D 선택 · Enter 한 장 가져간다");

            float x = panel.center.x - totalW * 0.5f;
            float y = panel.y + 76f;

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                bool sel = i == _town.CardCursor;

                var box = new Rect(x + i * (cardW + gap), sel ? y - 8f : y, cardW, cardH);

                HudStyle.Fill(new Rect(box.x - 3, box.y - 3, box.width + 6, box.height + 6),
                              sel ? HudStyle.PanelEdge : new Color(0.10f, 0.10f, 0.12f, 0.35f));
                HudStyle.Fill(box, sel
                    ? new Color(0.14f, 0.13f, 0.17f, 0.98f)
                    : new Color(0.10f, 0.10f, 0.12f, 0.92f));

                // 선택한 카드만 앞면을 보여준다. 나머지는 끝까지 뒷면이다 —
                // 버린 카드가 무엇이었는지 알면 고른 즐거움 대신 후회만 남는다.
                if (!sel)
                {
                    GUI.Label(new Rect(box.x, box.center.y - 20f, box.width, 40f),
                              "<size=30><color=#5a5560>?</color></size>", _tag);
                    continue;
                }

                string tint = card.kind == RewardKind.Weapon ? HudStyle.HexGain
                            : card.kind == RewardKind.Money ? HudStyle.HexLight
                            : HudStyle.HexNotice;

                if (card.kind == RewardKind.Weapon && card.weapon != null && card.weapon.icon != null)
                {
                    var art = new Rect(box.x + 12f, box.y + 40f, box.width - 24f, 60f);
                    GUI.DrawTexture(art, card.weapon.icon.texture, ScaleMode.ScaleToFit);
                }

                GUI.Label(new Rect(box.x + 8f, box.y + 116f, box.width - 16f, 60f),
                          $"<color={tint}><b>{card.Label}</b></color>", _tag);
                GUI.Label(new Rect(box.x + 8f, box.y + 168f, box.width - 16f, 30f),
                          $"<color={HudStyle.HexLightDim}>{card.Detail}</color>", _tag);
            }
        }

        // ── 출격 게이트 ──────────────────────────────────────────

        void DrawDungeons()
        {
            var catalog = _town.Catalog;

            var panel = Center(700f, 560f);
            HudStyle.Panel(panel);
            Header(panel, "출격 게이트", "W/S 선택 · Enter 출격 · Esc 닫기");

            if (catalog == null || catalog.Count == 0)
            {
                GUI.Label(new Rect(panel.x + 26, panel.y + 70, panel.width - 52, 40),
                    $"<color={HudStyle.HexWarn}>던전 목록이 없습니다. MiniWar → 스테이지 에셋 생성</color>", _label);
                return;
            }

            float y = panel.y + 70f;
            float w = panel.width - 52f;

            for (int i = 0; i < catalog.Count; i++)
            {
                var stage = catalog.At(i);
                bool selected = i == _town.DungeonCursor;
                bool unlocked = _town.Session.IsUnlocked(i);
                bool cleared = _town.Profile.IsCleared(stage);

                var row = new Rect(panel.x + 26, y, w, 74f);
                if (selected) Highlight(row);

                string name = stage != null ? stage.displayName : "???";
                string head = unlocked
                    ? $"{(selected ? "<b>▶</b> " : "   ")}<b>{i + 1}. {name}</b>"
                    : $"    <color={HudStyle.HexDim}>{i + 1}. ??? — 잠김</color>";

                if (cleared) head += $"   <color={HudStyle.HexGain}>클리어</color>";

                GUI.Label(new Rect(row.x, row.y, row.width, 26), head, _label);

                if (unlocked && stage != null)
                {
                    GUI.Label(new Rect(row.x + 26, row.y + 24, row.width - 26, 24),
                              $"<color={HudStyle.HexDim}>{stage.briefing}</color>", _small);

                    string bonus = cleared || stage.firstClearBonus <= 0
                        ? ""
                        : $"  <color={HudStyle.HexGain}>최초 +${stage.firstClearBonus:N0}</color>";

                    GUI.Label(new Rect(row.x + 26, row.y + 44, row.width - 26, 24),
                              $"<color={HudStyle.HexDim}>구간 {stage.legs.Length} · {stage.TotalLength:F0}m · "
                            + $"보스 {(stage.boss != null ? stage.boss.displayName : "없음")} · "
                            + $"보상 ${stage.clearReward:N0}</color>{bonus}", _small);
                }

                y += 82f;
            }

            DrawLoadoutStrip(new Rect(panel.x + 26, y + 6f, w, 70f));

            if (!string.IsNullOrEmpty(_town.Notice))
                GUI.Label(new Rect(panel.x + 26, panel.yMax - 28f, w, 24f),
                          $"<color={HudStyle.HexNotice}>{_town.Notice}</color>", _label);
        }

        /// <summary>출격 직전 두 칸. 빈 칸과 빈 탄창이 여기서 잡혀야 한다.</summary>
        void DrawLoadoutStrip(Rect area)
        {
            HudStyle.Fill(area, new Color(0.10f, 0.10f, 0.12f, 0.06f));

            GUI.Label(new Rect(area.x + 10, area.y + 6, area.width - 20, 22), "<b>출격 장비</b>", _small);

            float cellW = (area.width - 20f) / Loadout.SlotCount;

            for (int i = 0; i < Loadout.SlotCount; i++)
            {
                var inst = _town.Profile.EquippedIn(i);
                var cell = new Rect(area.x + 10 + cellW * i, area.y + 26, cellW, 40);

                if (inst == null)
                {
                    GUI.Label(cell, $"<color={HudStyle.HexDim}>{i + 1}번 칸\n— 비어 있음</color>", _small);
                    continue;
                }

                GUI.Label(cell, $"<color={HudStyle.HexDim}>{i + 1}번 칸 · {inst.Data.FamilyLabel}</color>\n"
                              + $"<b>{inst.Label}</b>  {AmmoText(inst)}", _small);
            }
        }

        static string AmmoText(WeaponInstance w)
        {
            if (w == null) return "";
            if (w.IsMelee) return $"<color={HudStyle.HexGain}>무료</color>";
            if (w.IsPerThrow) return $"1회 ${w.UseCost}";
            return w.Ammo == 0
                ? $"<color={HudStyle.HexWarn}>0/{w.MagazineSize}</color>"
                : $"{w.Ammo}/{w.MagazineSize}";
        }

        // ── 정비소 — 강화 · 보급 ─────────────────────────────────

        void DrawArmory()
        {
            var shop = _town.Armory;
            var owned = _town.Owned;
            var target = _town.ArmoryTarget;

            var panel = Center(760f, 560f);
            HudStyle.Panel(panel);
            Header(panel, "정비소", "W/S 선택 · Z 강화 · Q/E 방지권 · T 부적 · R/F 보급 · Esc 닫기");

            float w = panel.width - 52f;
            float y = DrawWeaponList(panel, owned, _town.ArmoryCursor, false);

            y += 10f;
            var box = new Rect(panel.x + 26, y, w, panel.yMax - y - 20f);
            HudStyle.Fill(box, new Color(0.10f, 0.10f, 0.12f, 0.06f));

            var sb = new StringBuilder();

            if (target == null)
            {
                sb.AppendLine($"<color={HudStyle.HexWarn}>강화할 무기가 없다.</color>");
            }
            else if (target.IsMaxEnhance)
            {
                sb.AppendLine($"<b>{target.Label}</b>  <color={HudStyle.HexGain}>최고 단계</color>");
                sb.AppendLine($"<color={HudStyle.HexDim}>위력 배수 x{EnhanceTable.PowerMultiplier(target.Enhance):F2}</color>");
            }
            else
            {
                var fail = EnhanceTable.FailureAt(target.Enhance);
                float rate = EnhanceTable.SuccessRate(target.Enhance);
                int cost = target.NextEnhanceCost;

                string price = _town.Profile.Money >= cost
                    ? $"<b>${cost:N0}</b>"
                    : $"<color={HudStyle.HexWarn}><b>${cost:N0}</b></color>";

                // 실패했을 때 무슨 일이 생기는지를 값보다 먼저 쓴다.
                // "확률"보다 "잃는 것"이 결정을 만든다.
                string risk = fail == EnhanceFailure.Keep
                    ? $"<color={HudStyle.HexGain}>실패해도 유지</color>"
                    : fail == EnhanceFailure.Drop
                    ? $"<color={HudStyle.HexNotice}>실패 시 한 단계 하락</color>"
                    : $"<color={HudStyle.HexWarn}>실패 시 하락 또는 파괴 "
                      + $"(파괴 {EnhanceTable.DestroyShare * 100f:F0}%)</color>";

                sb.AppendLine($"<b>{target.Label}</b>  →  <b>+{target.Enhance + 1}</b>"
                            + $"     성공 <b>{rate * 100f:F0}%</b>     {price}");
                sb.AppendLine($"<color={HudStyle.HexDim}>위력 {target.DamagePerPellet:F0} "
                            + $"(x{EnhanceTable.PowerMultiplier(target.Enhance):F2} → "
                            + $"x{EnhanceTable.PowerMultiplier(target.Enhance + 1):F2})</color>     {risk}");
                sb.AppendLine();

                sb.AppendLine(GuardLine("Q", "고정 쐐기", "하락 방지", _town.UseWedge,
                                        _town.Profile.Wedges,
                                        fail != EnhanceFailure.Keep));
                sb.AppendLine(GuardLine("E", "예비 총열", "파괴 방지", _town.UseSpareBarrel,
                                        _town.Profile.SpareBarrels,
                                        fail == EnhanceFailure.DropOrDestroy));
            }

            sb.AppendLine();
            sb.AppendLine($"  <b>T</b> 부적 사용 <color={HudStyle.HexDim}>(보유 {_town.Profile.Talismans.Count})</color>"
                        + $"     <b>R</b> 보급     <b>F</b> 전 무기 보급 "
                        + $"<color={HudStyle.HexDim}>(${shop.FullReloadCost(_town.Loadout):N0})</color>");

            if (!string.IsNullOrEmpty(shop.LastMessage))
                sb.AppendLine($"  <color={HudStyle.HexNotice}>{shop.LastMessage}</color>");
            else if (!string.IsNullOrEmpty(_town.Notice))
                sb.AppendLine($"  <color={HudStyle.HexNotice}>{_town.Notice}</color>");

            GUI.Label(new Rect(box.x + 16, box.y + 12, box.width - 32, box.height - 20),
                      sb.ToString(), _label);
        }

        /// <summary>방지권 한 줄. 지금 단계에서 의미가 없으면 흐리게 — 괜히 걸어 낭비하지 않게.</summary>
        static string GuardLine(string key, string name, string effect,
                                bool on, int stock, bool useful)
        {
            if (!useful)
                return $"  <color={HudStyle.HexDim}>{key} {name} — 이 단계에서는 필요 없다</color>";

            if (stock <= 0)
                return $"  <color={HudStyle.HexDim}>{key} {name} — 없음 (작업대에서 제작)</color>";

            return on
                ? $"  <b>{key} {name} <color={HudStyle.HexGain}>[걸어둠]</color></b>"
                  + $" <color={HudStyle.HexDim}>{effect} · 보유 {stock} · 실패할 때만 소모</color>"
                : $"  {key} {name} <color={HudStyle.HexDim}>[꺼짐] · {effect} · 보유 {stock}</color>";
        }

        // ── 작업대 — 합성 · 분해 · 제작 · 편성 ───────────────────

        void DrawWorkshop()
        {
            var shop = _town.Workshop;
            var owned = _town.Owned;

            var panel = Center(790f, 580f);
            HudStyle.Panel(panel);
            Header(panel, "작업대",
                   "W/S 선택 · Space 재료 · Enter 합성 · X 분해 · 1/2 편성 · C/V 제작 · Esc 닫기");

            float w = panel.width - 52f;
            float y = DrawWeaponList(panel, owned, _town.WorkshopCursor, true);

            y += 10f;
            var box = new Rect(panel.x + 26, y, w, panel.yMax - y - 20f);
            HudStyle.Fill(box, new Color(0.10f, 0.10f, 0.12f, 0.06f));

            var mats = _town.Materials;
            var verdict = _town.FusionPreview();

            var sb = new StringBuilder();
            sb.AppendLine($"<b>합성대</b>  {mats.Count}/{FusionTable.MaterialCount}"
                        + $"     <color={HudStyle.HexDim}>{MaterialNames(mats)}</color>");

            // 강화가 사라진다는 것은 반드시 미리 말해야 한다. 나중에 알면 사고다.
            string note = FusionTable.CanFuse(verdict)
                ? $"<color={HudStyle.HexGain}>{FusionTable.LabelOf(verdict)}</color>"
                  + $"   <color={HudStyle.HexWarn}>결과물은 +0 — 강화는 계승되지 않는다</color>"
                : $"<color={HudStyle.HexDim}>{FusionTable.LabelOf(verdict)}</color>";
            sb.AppendLine("  " + note);
            sb.AppendLine();

            var t = _town.WorkshopTarget;
            if (t != null)
            {
                sb.AppendLine($"  <b>X</b> {t.Label} 분해 "
                            + $"<color={HudStyle.HexDim}>→ 부속 {SalvageTable.PartsFrom(t.Tier, t.Enhance)}"
                            + (t.Enhance >= 2
                                ? $" · 부적 +{t.Enhance - 1} 확률 {SalvageTable.TalismanChance * 100f:F0}%"
                                : " · 부적 없음 (+2 이상부터)")
                            + "</color>");
            }

            sb.AppendLine($"  <b>C</b> 고정 쐐기 (부속 {SalvageTable.WedgeCost})"
                        + $"     <b>V</b> 예비 총열 (부속 {SalvageTable.SpareBarrelCost})"
                        + $"     <color={HudStyle.HexDim}>보유 부속 {_town.Profile.Parts}</color>");

            if (!string.IsNullOrEmpty(shop.LastMessage))
                sb.AppendLine($"  <color={HudStyle.HexNotice}>{shop.LastMessage}</color>");
            else if (!string.IsNullOrEmpty(_town.Notice))
                sb.AppendLine($"  <color={HudStyle.HexNotice}>{_town.Notice}</color>");

            GUI.Label(new Rect(box.x + 16, box.y + 12, box.width - 32, box.height - 20),
                      sb.ToString(), _label);
        }

        static string MaterialNames(System.Collections.Generic.List<WeaponInstance> mats)
        {
            if (mats.Count == 0) return "Space로 재료를 올린다";

            var names = new string[mats.Count];
            for (int i = 0; i < mats.Count; i++) names[i] = mats[i].Label;
            return string.Join(" + ", names);
        }

        // ── 보유 목록 ────────────────────────────────────────────

        /// <summary>
        /// 정비소와 작업대가 함께 쓰는 목록. 같은 화면을 두 곳에서 보게 해야
        /// "저기서 본 그 총"을 여기서 찾느라 헤매지 않는다. 다음 줄의 y를 돌려준다.
        /// </summary>
        float DrawWeaponList(Rect panel, System.Collections.Generic.IReadOnlyList<WeaponInstance> owned,
                             int cursor, bool showMaterials)
        {
            float y = panel.y + 66f;
            float w = panel.width - 52f;

            if (owned.Count == 0)
            {
                GUI.Label(new Rect(panel.x + 26, y, w, 26f),
                          $"<color={HudStyle.HexDim}>보유한 무기가 없다.</color>", _label);
                return y + 30f;
            }

            // 목록이 길어지면 커서 주변만 보여준다. 스크롤 대신 창을 밀어 단순하게 간다.
            const int Window = 9;
            int first = Mathf.Clamp(cursor - Window / 2, 0, Mathf.Max(0, owned.Count - Window));
            int last = Mathf.Min(owned.Count, first + Window);

            if (first > 0)
            {
                GUI.Label(new Rect(panel.x + 26, y, w, 18f),
                          $"<color={HudStyle.HexDim}>▲ 위로 {first}정</color>", _small);
            }
            y += 18f;

            for (int i = first; i < last; i++)
            {
                var wpn = owned[i];
                bool sel = i == cursor;

                var row = new Rect(panel.x + 26, y, w, 26f);
                if (sel) Highlight(row);

                string marks = "";
                if (_town.Profile.IsEquipped(wpn))
                {
                    int slot = _town.Profile.EquippedIn(0) == wpn ? 1 : 2;
                    marks += $" <color={HudStyle.HexGain}>[{slot}번 칸]</color>";
                }
                if (showMaterials && _town.IsMaterial(wpn))
                    marks += $" <color={HudStyle.HexNotice}>[재료]</color>";

                GUI.Label(new Rect(row.x, row.y, row.width, 24f),
                    $"{(sel ? "<b>▶</b> " : "   ")}<b>{wpn.Label}</b>"
                  + $"  <color={HudStyle.HexDim}>{wpn.Data.FamilyLabel}</color>{marks}", _label);

                GUI.Label(new Rect(row.x, row.y, row.width, 24f),
                    $"<color={HudStyle.HexDim}>위력 {wpn.DamagePerPellet:F0}"
                  + (wpn.Data.pellets > 1 ? $"x{wpn.Data.pellets}" : "")
                  + $" · {AmmoText(wpn)}</color>", _right);

                y += 28f;
            }

            if (last < owned.Count)
            {
                GUI.Label(new Rect(panel.x + 26, y, w, 18f),
                          $"<color={HudStyle.HexDim}>▼ 아래로 {owned.Count - last}정</color>", _small);
                y += 18f;
            }

            return y;
        }

        // ── 공통 ─────────────────────────────────────────────────

        /// <summary>
        /// 패널 자리. 말을 건 NPC가 있으면 왼쪽 일러스트를 피해 오른쪽으로 민다 —
        /// 창이 사람을 가리면 대화하던 흐름이 끊긴다.
        /// </summary>
        Rect Center(float w, float h)
        {
            w = Mathf.Min(w, Screen.width - 48f);
            h = Mathf.Min(h, Screen.height - 120f);

            float x = (Screen.width - w) * 0.5f;

            if (_town != null && _town.Speaker != null && _speakerRight > 0f)
            {
                x = Mathf.Min(Mathf.Max(x, _speakerRight + 20f), Screen.width - w - 24f);
            }

            return new Rect(x, (Screen.height - h) * 0.5f + 20f, w, h);
        }

        static Rect Inner(Rect panel)
            => new Rect(panel.x + 26, panel.y + 22, panel.width - 52, panel.height - 40);

        void Header(Rect panel, string title, string hint)
        {
            GUI.Label(new Rect(panel.x + 26, panel.y + 16, panel.width - 52, 30),
                      $"<size=20><b>{title}</b></size>", _label);
            GUI.Label(new Rect(panel.x + 26, panel.y + 20, panel.width - 52, 26),
                      $"<color={HudStyle.HexDim}>{hint}</color>", _right);
            HudStyle.Fill(new Rect(panel.x + 26, panel.y + 52, panel.width - 52, 1f),
                          new Color(0.10f, 0.10f, 0.12f, 0.30f));
        }

        static void Highlight(Rect row)
        {
            HudStyle.Fill(new Rect(row.x - 6, row.y - 3, row.width + 12, row.height),
                          new Color(0.10f, 0.10f, 0.12f, 0.08f));
            HudStyle.Fill(new Rect(row.x - 6, row.y - 3, 4f, row.height), HudStyle.PanelEdge);
        }
    }
}
