using MiniWar.Data;
using UnityEngine;

namespace MiniWar.Runtime
{
    /// <summary>
    /// 던전 HUD.
    ///
    /// 왼쪽 아래에 <b>체력 게이지 · 무기 그림 · 잔탄</b>을 한 덩어리로 모았다.
    /// 전투 중 눈이 가는 곳은 조준점과 적이지 화면 구석의 글자가 아니므로,
    /// 즉시 판단해야 하는 세 가지(더 버틸 수 있나 / 뭘 들고 있나 / 몇 발 남았나)만
    /// 한 자리에 크게 두고 나머지는 작게 민다.
    ///
    /// 글자색은 반드시 직접 지정한다 — 기본 GUI 스킨은 어두운 배경을 전제한 밝은 회색이라
    /// 흰 배경인 이 게임에서는 그대로 두면 읽히지 않는다.
    /// </summary>
    [RequireComponent(typeof(GameRunner))]
    public sealed class TestHud : MonoBehaviour
    {
        const float PanelW = 392f;
        const float PanelH = 116f;

        GameRunner _runner;
        GUIStyle _label, _small, _center, _big, _ammo, _name, _right;

        void Awake() => _runner = GetComponent<GameRunner>();

        void Ensure()
        {
            if (_label != null) return;

            _label = HudStyle.Label(16);
            _small = HudStyle.Label(13);
            _center = HudStyle.Label(15, TextAnchor.UpperCenter);
            _big = HudStyle.Label(34, TextAnchor.MiddleCenter);
            _ammo = HudStyle.Label(30, TextAnchor.MiddleRight);
            _name = HudStyle.Label(15, TextAnchor.UpperCenter);
            _right = HudStyle.Label(13, TextAnchor.UpperRight);
        }

        void OnGUI()
        {
            if (_runner == null || _runner.Economy == null) return;
            Ensure();

            DrawProgress();
            DrawTopLine();
            DrawLoadoutPanel();
            DrawControlHint();

            if (_runner.Phase == StagePhase.BossFight) DrawBossBar();
            if (_runner.Run.IsOver) DrawResult();
        }

        /// <summary>
        /// 던전 진척 바. 길이가 정해진 던전에서 가장 중요한 정보는 "보스까지 얼마 남았나"다 —
        /// 남은 탄과 돈을 이 거리에 맞춰 배분해야 하기 때문이다.
        /// </summary>
        void DrawProgress()
        {
            var stage = _runner.Stage;
            if (stage == null) return;

            float w = Mathf.Min(560f, Screen.width - 40f);
            float x = (Screen.width - w) * 0.5f;
            float total = Mathf.Max(1f, stage.TotalLength);

            HudStyle.Fill(new Rect(x, 14f, w, 10f), new Color(0.10f, 0.10f, 0.12f, 0.15f));

            float arenaT = stage.ArenaStart / total;
            HudStyle.Fill(new Rect(x + w * arenaT, 14f, w * (1f - arenaT), 10f),
                          new Color(0.62f, 0.16f, 0.16f, 0.45f));

            for (int i = 1; i <= stage.legs.Length; i++)
            {
                float t = stage.LegStart(i) / total;
                HudStyle.Fill(new Rect(x + w * t - 1f, 9f, 2f, 20f), HudStyle.PanelEdge);
            }

            float p = Mathf.Clamp01(_runner.Progress / total);
            HudStyle.Fill(new Rect(x, 14f, w * p, 10f), HudStyle.Ink);
            HudStyle.Fill(new Rect(x + w * p - 2f, 7f, 4f, 24f), HudStyle.Ink);

            GUI.Label(new Rect(x, 30f, w, 24f),
                      $"{stage.displayName} · {_runner.LegLabel} · "
                    + $"<color={HudStyle.HexDim}>{_runner.Progress:F0} / {total:F0} m</color>", _center);
        }

        /// <summary>돈과 알림만. 나머지는 전부 아래 패널로 내렸다.</summary>
        void DrawTopLine()
        {
            var eco = _runner.Economy;

            var box = new Rect(14, 14, 300, 58);
            HudStyle.Fill(box, new Color(0.965f, 0.960f, 0.945f, 0.80f));

            GUI.Label(new Rect(box.x + 12, box.y + 6, box.width - 24, 30),
                $"<size=22><b>$ {eco.Money:N0}</b></size>"
              + (eco.IsInDanger ? $"  <color={HudStyle.HexWarn}><b>잔액 위험</b></color>" : ""), _label);

            GUI.Label(new Rect(box.x + 12, box.y + 34, box.width - 24, 22),
                $"<color={HudStyle.HexDim}>격파 {_runner.Kills} · 전리품 ${_runner.Earned:N0}</color>", _small);

            float y = box.yMax + 6f;

            if (_runner.Phase == StagePhase.GateHeld)
            {
                GUI.Label(new Rect(box.x + 12, y, 460, 24),
                    $"<color={HudStyle.HexWarn}><b>관문이 막혀 있다 — "
                  + $"남은 적 {_runner.AliveCount + _runner.PendingCount}</b></color>", _label);
                y += 24f;
            }

            if (!string.IsNullOrEmpty(_runner.LastNotice))
                GUI.Label(new Rect(box.x + 12, y, 560, 24),
                          $"<color={HudStyle.HexNotice}>{_runner.LastNotice}</color>", _label);
        }

        /// <summary>조작 안내 한 줄. 프로토타입 동안만 둔다.</summary>
        void DrawControlHint()
        {
            if (_runner.Phase == StagePhase.BossFight) return;   // 보스 바와 겹친다

            GUI.Label(new Rect(Screen.width - 640f, Screen.height - 40f, 620f, 24f),
                $"<color={HudStyle.HexDim}>A / D 이동 · 스페이스 또는 W 점프 · "
              + $"좌클릭 사격 · R 장전 · 1~4 전환</color>", _right);
        }

        // ── 왼쪽 아래 패널 ───────────────────────────────────────

        void DrawLoadoutPanel()
        {
            var loadout = _runner.Loadout;
            if (loadout == null) return;

            var panel = new Rect(16f, Screen.height - PanelH - 16f, PanelW, PanelH);
            HudStyle.Panel(panel);

            DrawHealthGauge(new Rect(panel.x + 16f, panel.y + 14f, 26f, PanelH - 30f));
            DrawCurrentWeapon(new Rect(panel.x + 58f, panel.y + 10f, 150f, PanelH - 22f));
            DrawAmmo(new Rect(panel.x + 214f, panel.y + 14f, 160f, 54f));
            DrawSlotChips(new Rect(panel.x + 214f, panel.y + 72f, 164f, 28f));
        }

        /// <summary>세로 게이지. 줄어드는 것이 한눈에 보이는 게 숫자보다 빠르다.</summary>
        void DrawHealthGauge(Rect r)
        {
            var run = _runner.Run;
            float ratio = Mathf.Clamp01(run.HealthRatio);

            float barH = r.height - 22f;
            var track = new Rect(r.x, r.y, r.width, barH);

            HudStyle.Fill(track, new Color(0.10f, 0.10f, 0.12f, 0.14f));

            // 아래에서 위로 찬다.
            float fillH = barH * ratio;
            var fill = new Rect(track.x, track.yMax - fillH, track.width, fillH);

            Color c = ratio > 0.5f ? HudStyle.Ink
                    : ratio > 0.25f ? new Color(0.62f, 0.40f, 0.05f)
                    : HudStyle.Warn;
            HudStyle.Fill(fill, c);

            // 25% 눈금 — 절반 남았는지 가늠할 기준선이 없으면 게이지가 의미를 잃는다.
            for (int i = 1; i < 4; i++)
            {
                float y = track.yMax - barH * (i * 0.25f);
                HudStyle.Fill(new Rect(track.x, y, track.width, 1f), new Color(1f, 1f, 1f, 0.45f));
            }

            GUI.Label(new Rect(r.x - 6f, r.yMax - 20f, r.width + 12f, 20f),
                      $"<b>{run.Health:F0}</b>", _name);
        }

        void DrawCurrentWeapon(Rect r)
        {
            var weapon = _runner.Loadout.Current;
            if (weapon == null)
            {
                GUI.Label(r, $"<color={HudStyle.HexDim}>빈손</color>", _name);
                return;
            }

            var frame = new Rect(r.x, r.y, r.width, r.height - 22f);
            HudStyle.Fill(frame, new Color(0.10f, 0.10f, 0.12f, 0.06f));
            HudStyle.Fill(new Rect(frame.x, frame.y, frame.width, 1f), new Color(0.10f, 0.10f, 0.12f, 0.35f));
            HudStyle.Fill(new Rect(frame.x, frame.yMax - 1f, frame.width, 1f), new Color(0.10f, 0.10f, 0.12f, 0.35f));

            var icon = weapon.Data.icon;
            if (icon != null && icon.texture != null)
            {
                var inner = new Rect(frame.x + 8f, frame.y + 6f, frame.width - 16f, frame.height - 12f);
                GUI.DrawTexture(inner, icon.texture, ScaleMode.ScaleToFit, true);
            }
            else
            {
                GUI.Label(frame, $"<color={HudStyle.HexDim}>(그림 없음)</color>", _name);
            }

            GUI.Label(new Rect(r.x, r.yMax - 20f, r.width, 20f),
                      $"<b>{weapon.Data.displayName}</b>", _name);
        }

        void DrawAmmo(Rect r)
        {
            var weapon = _runner.Loadout.Current;
            if (weapon == null) return;

            var eco = _runner.Economy;

            if (weapon.IsMelee)
            {
                GUI.Label(r, "<size=22><b>근접</b></size>", _ammo);
                GUI.Label(new Rect(r.x, r.yMax - 16f, r.width, 20f),
                          $"<color={HudStyle.HexGain}>탄약 없음 · 무료</color>", _ammo);
                return;
            }

            if (weapon.IsPerThrow)
            {
                bool can = eco.Money >= weapon.UseCost;
                GUI.Label(r, can ? "<size=22><b>투척</b></size>"
                                 : $"<size=22><b><color={HudStyle.HexWarn}>투척</color></b></size>", _ammo);
                GUI.Label(new Rect(r.x, r.yMax - 16f, r.width, 20f),
                          can ? $"<color={HudStyle.HexDim}>1회 ${weapon.UseCost}</color>"
                              : $"<color={HudStyle.HexWarn}>1회 ${weapon.UseCost} — 부족</color>", _ammo);
                return;
            }

            string ammoColor = weapon.Ammo == 0 ? HudStyle.HexWarn
                             : weapon.Ammo <= weapon.MagazineSize * 0.25f ? HudStyle.HexNotice
                             : null;

            string ammoText = ammoColor == null
                ? $"<b>{weapon.Ammo}</b>"
                : $"<color={ammoColor}><b>{weapon.Ammo}</b></color>";

            GUI.Label(r, $"{ammoText}<size=18> / {weapon.MagazineSize}</size>", _ammo);

            bool affordable = eco.Money >= weapon.ReloadCost;
            GUI.Label(new Rect(r.x, r.yMax - 16f, r.width, 20f),
                affordable
                    ? $"<color={HudStyle.HexDim}>R 장전 ${weapon.ReloadCost}</color>"
                    : $"<color={HudStyle.HexWarn}>장전 ${weapon.ReloadCost} — 부족</color>", _ammo);
        }

        /// <summary>1~4 칸. 지금 든 것이 어느 칸인지, 다른 칸에 뭐가 있는지 한 줄로.</summary>
        void DrawSlotChips(Rect r)
        {
            var loadout = _runner.Loadout;
            float w = r.width / 4f;

            for (int i = 0; i < 4; i++)
            {
                var role = (WeaponRole)i;
                var chip = new Rect(r.x + w * i, r.y, w - 4f, r.height);

                WeaponInstance found = null;
                foreach (var wpn in loadout.Weapons)
                {
                    if (wpn.Data.role != role) continue;
                    found = wpn;
                    break;
                }

                bool selected = found != null && found == loadout.Current;

                HudStyle.Fill(chip, selected
                    ? new Color(0.10f, 0.10f, 0.12f, 0.85f)
                    : new Color(0.10f, 0.10f, 0.12f, 0.08f));

                string text;
                if (found == null)
                    text = $"<color={HudStyle.HexDim}>{i + 1} —</color>";
                else if (selected)
                    text = $"<color=#f5f3ee><b>{i + 1}</b></color>";
                else if (found.IsEmpty)
                    text = $"<color={HudStyle.HexWarn}><b>{i + 1}</b></color>";
                else
                    text = $"<b>{i + 1}</b>";

                GUI.Label(new Rect(chip.x, chip.y + 3f, chip.width, 22f), text, _name);
            }
        }

        // ── 보스 · 결과 ──────────────────────────────────────────

        void DrawBossBar()
        {
            var boss = _runner.Boss;
            if (boss == null) return;

            float w = Mathf.Min(560f, Screen.width - 480f);
            float x = Screen.width - w - 24f;
            float y = Screen.height - 64f;

            HudStyle.Fill(new Rect(x - 12, y - 34, w + 24, 62),
                          new Color(0.965f, 0.960f, 0.945f, 0.85f));

            HudStyle.Bar(new Rect(x, y, w, 16f), boss.HealthRatio, new Color(0.62f, 0.16f, 0.16f));

            var data = _runner.Stage != null ? _runner.Stage.boss : null;
            if (data != null)
            {
                for (int i = 0; i < data.phases.Length - 1; i++)
                {
                    float t = data.PhaseFloor(i);
                    HudStyle.Fill(new Rect(x + w * t - 1f, y - 3f, 2f, 22f), HudStyle.PanelEdge);
                }
            }

            GUI.Label(new Rect(x, y - 28f, w, 24f),
                      $"<b>{boss.DisplayName}</b>  <color={HudStyle.HexWarn}><b>{boss.PhaseLabel}</b></color>"
                    + $"  <color={HudStyle.HexDim}>({boss.PhaseIndex + 1}/{boss.PhaseCount})</color>", _center);
        }

        void DrawResult()
        {
            HudStyle.DimScreen();

            float w = 460f, h = 150f;
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            HudStyle.Panel(panel);

            bool cleared = _runner.Run.EndReason == RunEndReason.Cleared;
            string color = cleared ? HudStyle.HexGain : HudStyle.HexWarn;

            GUI.Label(new Rect(panel.x, panel.y + 26, panel.width, 46),
                      $"<color={color}>{RunState.LabelOf(_runner.Run.EndReason)}</color>", _big);

            GUI.Label(new Rect(panel.x, panel.y + 88, panel.width, 30),
                      $"<color={HudStyle.HexDim}>기지로 귀환하는 중…</color>", _center);
        }
    }
}
