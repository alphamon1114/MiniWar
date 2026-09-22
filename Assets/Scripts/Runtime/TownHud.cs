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
            if (_town.Speaker != null && _town.Panel != TownPanel.Result) MeasureSpeaker();

            switch (_town.Panel)
            {
                case TownPanel.Dialogue: DrawDialogue(); break;
                case TownPanel.Result: DrawResult(); break;
                case TownPanel.Dungeons: DrawDungeons(); break;
                case TownPanel.Armory: DrawArmory(); break;
                case TownPanel.Market: DrawMarket(); break;
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

            GUI.Label(new Rect(28, 44, 900, 24),
                $"<b>$ {p.Money:N0}</b>    "
              + $"<color={HudStyle.HexLightDim}>클리어 {p.ClearedCount}/{(_town.Catalog != null ? _town.Catalog.Count : 0)}"
              + $"  ·  출격 {p.DungeonsRun}회  ·  보유 무기 {p.OwnedCount}  ·  강화 {p.UpgradesPurchased}회</color>",
                _labelLight);

            // 출격 준비 상태 — 빈 탄창은 여기서 잡힌다.
            int refill = _town.Shop != null ? _town.Shop.FullReloadCost(_town.Loadout) : 0;
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
            sb.AppendLine($"<color={HudStyle.HexDim}>Enter — 기지로</color>");

            GUI.Label(Inner(panel), sb.ToString(), _label);
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

        /// <summary>출격 직전 4칸. 빈 칸과 빈 탄창이 여기서 잡혀야 한다.</summary>
        void DrawLoadoutStrip(Rect area)
        {
            HudStyle.Fill(area, new Color(0.10f, 0.10f, 0.12f, 0.06f));

            GUI.Label(new Rect(area.x + 10, area.y + 6, area.width - 20, 22), "<b>출격 장비</b>", _small);

            float cellW = (area.width - 20f) / 4f;

            for (int i = 0; i < 4; i++)
            {
                var role = (WeaponRole)i;
                var data = _town.Profile.EquippedIn(role);
                var cell = new Rect(area.x + 10 + cellW * i, area.y + 26, cellW, 40);

                string label = WeaponData.LabelOf(role);

                if (data == null)
                {
                    GUI.Label(cell, $"<color={HudStyle.HexDim}>{i + 1} {label}\n— 비어 있음</color>", _small);
                    continue;
                }

                var inst = Find(data);
                string ammo = inst == null ? ""
                    : inst.IsMelee ? $"<color={HudStyle.HexGain}>무료</color>"
                    : inst.IsPerThrow ? $"1회 ${inst.UseCost}"
                    : inst.Ammo == 0 ? $"<color={HudStyle.HexWarn}>0 / {inst.MagazineSize}</color>"
                    : $"{inst.Ammo} / {inst.MagazineSize}";

                GUI.Label(cell, $"<color={HudStyle.HexDim}>{i + 1} {label}</color>\n"
                              + $"<b>{data.displayName}</b>  {ammo}", _small);
            }
        }

        WeaponInstance Find(WeaponData data)
        {
            if (_town.Loadout == null) return null;
            foreach (var w in _town.Loadout.Weapons)
                if (w.Data == data) return w;
            return null;
        }

        // ── 정비소 ───────────────────────────────────────────────

        void DrawArmory()
        {
            var loadout = _town.Loadout;
            var shop = _town.Shop;
            var current = loadout.Current;

            var panel = Center(720f, 470f);
            HudStyle.Panel(panel);
            Header(panel, "정비소", "1~4 무기 선택 · Esc 닫기");

            float y = panel.y + 72f;
            float w = panel.width - 52f;

            foreach (var wpn in loadout.Weapons)
            {
                bool sel = wpn == current;
                var row = new Rect(panel.x + 26, y, w, 26f);
                if (sel) Highlight(row);

                string ammo = wpn.IsMelee ? $"<color={HudStyle.HexGain}>무료</color>"
                            : wpn.IsPerThrow ? $"1회 ${wpn.UseCost}"
                            : $"{wpn.Ammo}/{wpn.MagazineSize}";

                string levels = "";
                for (int a = 0; a < 4; a++)
                {
                    int lv = wpn.LevelOf((UpgradeAxis)a);
                    if (lv > 0) levels += $" {UpgradeTable.LabelOf((UpgradeAxis)a)}+{lv}";
                }
                if (levels.Length == 0) levels = $" <color={HudStyle.HexDim}>강화 없음</color>";

                GUI.Label(row,
                    $"{(sel ? "<b>▶</b>" : "  ")} [{wpn.Data.slot}] <b>{wpn.Data.displayName}</b>  {ammo}   "
                  + $"위력 {wpn.DamagePerPellet:F0} · 연사 {wpn.ShotsPerSecond:F1}/s · 장전 ${wpn.ReloadCost}"
                  + levels, _label);

                y += 28f;
            }

            y += 14f;

            var box = new Rect(panel.x + 26, y, w, 168f);
            HudStyle.Fill(box, new Color(0.10f, 0.10f, 0.12f, 0.06f));

            var sb = new StringBuilder();
            string price = shop.CanAfford
                ? $"<b>${shop.NextCost:N0}</b>"
                : $"<color={HudStyle.HexWarn}><b>${shop.NextCost:N0}</b></color>";

            sb.AppendLine($"<b>강화</b>  대상 {(current != null ? current.Data.displayName : "-")}"
                        + $"    다음 가격 {price}");
            sb.AppendLine($"<color={HudStyle.HexDim}>살 때마다 배로 오른다 · 지금까지 {shop.PurchasedTotal}회</color>");
            sb.AppendLine();
            sb.AppendLine($"  <b>Z</b> {UpgradeTable.LabelOf(UpgradeAxis.Damage)}"
                        + $"     <b>X</b> {UpgradeTable.LabelOf(UpgradeAxis.FireRate)}"
                        + $"     <b>C</b> {UpgradeTable.LabelOf(UpgradeAxis.MagazineSize)}"
                        + $"     <b>V</b> {UpgradeTable.LabelOf(UpgradeAxis.ReloadCost)}");
            sb.AppendLine();
            sb.AppendLine($"  <b>R</b> 선택 무기 보급     <b>F</b> 전 무기 보급 "
                        + $"<color={HudStyle.HexDim}>(${shop.FullReloadCost(loadout):N0})</color>");

            if (!string.IsNullOrEmpty(shop.LastMessage))
                sb.AppendLine($"  <color={HudStyle.HexNotice}>{shop.LastMessage}</color>");

            GUI.Label(new Rect(box.x + 16, box.y + 12, box.width - 32, box.height - 20), sb.ToString(), _label);
        }

        // ── 무기상 ───────────────────────────────────────────────

        void DrawMarket()
        {
            var market = _town.Market;
            var profile = _town.Profile;

            var panel = Center(760f, 540f);
            HudStyle.Panel(panel);
            Header(panel, "무기상", "W/S 선택 · Enter 구매 또는 장착 · Esc 닫기");

            float y = panel.y + 70f;
            float w = panel.width - 52f;

            WeaponRole? lastRole = null;

            for (int i = 0; i < market.Length; i++)
            {
                var data = market[i];
                bool owned = profile.Owns(data);
                bool equipped = profile.EquippedIn(data.role) == data;
                bool selected = i == _town.MarketCursor;

                if (lastRole == null || lastRole.Value != data.role)
                {
                    lastRole = data.role;
                    GUI.Label(new Rect(panel.x + 26, y, w, 20),
                        $"<color={HudStyle.HexDim}>── {WeaponData.LabelOf(data.role)} ──</color>", _small);
                    y += 20f;
                }

                var row = new Rect(panel.x + 26, y, w, 42f);
                if (selected) Highlight(row);

                string state = equipped ? $"<color={HudStyle.HexGain}><b>장착 중</b></color>"
                             : owned ? $"<color={HudStyle.HexDim}>보유 — Enter로 장착</color>"
                             : profile.Money >= data.price ? $"<b>${data.price:N0}</b>"
                             : $"<color={HudStyle.HexWarn}>${data.price:N0}</color>";

                GUI.Label(new Rect(row.x, row.y, row.width, 24f),
                    $"{(selected ? "<b>▶</b> " : "   ")}<b>{data.displayName}</b>", _label);

                GUI.Label(new Rect(row.x, row.y, row.width, 24f), state, _right);

                string spec = data.ammoMode == AmmoMode.Melee
                    ? $"위력 {data.damagePerPellet:F0} · {data.shotsPerSecond:F1}회/s · 사거리 {data.meleeRange:F1} · 탄약비 없음"
                    : data.ammoMode == AmmoMode.PerThrow
                    ? $"위력 {data.damagePerPellet:F0} · 폭발 {data.blastRadius:F1} · 1회 ${data.reloadCost}"
                    : $"위력 {data.damagePerPellet:F0}"
                      + (data.pellets > 1 ? $"x{data.pellets}" : "")
                      + $" · {data.shotsPerSecond:F1}발/s · 장탄 {data.magazineSize} · 장전 ${data.reloadCost}"
                      + (data.piercing ? " · 관통" : "");

                GUI.Label(new Rect(row.x + 26, row.y + 21, row.width - 38f, 20f),
                          $"<color={HudStyle.HexDim}>{spec}</color>", _small);

                y += 44f;
            }

            if (!string.IsNullOrEmpty(_town.Shop.LastMessage))
                GUI.Label(new Rect(panel.x + 26, panel.yMax - 28f, w, 24f),
                          $"<color={HudStyle.HexNotice}>{_town.Shop.LastMessage}</color>", _label);
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
