using System.Text;
using UnityEngine;

namespace MiniWar.Runtime
{
    /// <summary>
    /// 임시 HUD. OnGUI라 Day 7의 진짜 HUD와는 무관하며, Day 2~6 동안 수치를 눈으로 보려고 쓴다.
    /// 여기 보이는 값이 기획서 수치와 맞는지 확인하는 것이 이 컴포넌트의 존재 이유다.
    /// </summary>
    [RequireComponent(typeof(GameRunner))]
    public sealed class TestHud : MonoBehaviour
    {
        GameRunner _runner;
        GUIStyle _style;

        void Awake() => _runner = GetComponent<GameRunner>();

        void OnGUI()
        {
            if (_runner == null || _runner.Economy == null) return;

            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };

            var eco = _runner.Economy;
            var run = _runner.Run;
            var sb = new StringBuilder();

            sb.AppendLine($"<b>$ {eco.Money:N0}</b>{(eco.IsInDanger ? "   <color=#cc2222>잔액 위험</color>" : "")}");
            sb.AppendLine($"체력 {run.Health:F0} / {run.MaxHealth:F0}      격파 {_runner.Kills}");
            sb.AppendLine();

            foreach (var w in _runner.Loadout.Weapons)
            {
                bool current = w == _runner.Loadout.Current;
                string mark = current ? "▶" : "  ";
                string ammo = w.IsEmpty ? "<color=#cc2222>0</color>" : w.Ammo.ToString();
                sb.AppendLine($"{mark} [{w.Data.slot}] {w.Data.displayName,-6} {ammo}/{w.MagazineSize}   장전 ${w.ReloadCost}");
            }

            sb.AppendLine();
            sb.AppendLine("<color=#888888>좌클릭 사격 · R 장전 · 1~4 무기 전환</color>");
            if (!string.IsNullOrEmpty(_runner.LastNotice))
                sb.AppendLine($"<color=#ddaa33>{_runner.LastNotice}</color>");

            GUI.Label(new Rect(14, 10, 520, 260), sb.ToString(), _style);

            if (run.IsOver)
            {
                var big = new GUIStyle(GUI.skin.label) { fontSize = 34, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 60),
                          RunState.LabelOf(run.EndReason), big);
            }
        }
    }
}
