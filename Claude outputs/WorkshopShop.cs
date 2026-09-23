using System.Collections.Generic;
using MiniWar.Data;

namespace MiniWar.Runtime
{
    /// <summary>
    /// 보급 담당관의 작업대. <b>합성 · 분해 · 방지권 제작 · 편성</b>.
    ///
    /// 무기를 파는 곳이 아니다 — 무기는 던전에서 떨어진다.
    /// 여기서 하는 일은 쌓인 것을 정리하는 것이고, 그 정리 방식이 곧 선택이다.
    /// 같은 총을 합성에 넣을 것인가(강화 소멸, 등급 상승) 분해할 것인가(부품 + 부적 기회) —
    /// 둘 다 할 수는 없다.
    /// </summary>
    public sealed class WorkshopShop
    {
        readonly PlayerProfile _profile;
        readonly WeaponCatalog _catalog;
        readonly System.Random _rng;

        public WorkshopShop(PlayerProfile profile, WeaponCatalog catalog, System.Random rng = null)
        {
            _profile = profile;
            _catalog = catalog;
            _rng = rng ?? new System.Random();
        }

        public string LastMessage { get; private set; } = "";

        public void ClearMessage() => LastMessage = "";

        // ── 합성 ─────────────────────────────────────────────────

        /// <summary>재료 셋이 어떤 결과를 낼지 미리 알려준다. 넣기 전에 보여줄 값.</summary>
        public FusionVerdict Preview(IReadOnlyList<WeaponInstance> materials)
        {
            var datas = new List<WeaponData>();
            if (materials != null)
            {
                foreach (var m in materials) datas.Add(m != null ? m.Data : null);
            }
            return FusionTable.Judge(datas);
        }

        /// <summary>
        /// 셋을 합친다. 성공하면 재료 셋은 사라지고 결과물 한 정이 들어온다.
        /// <b>결과물은 언제나 +0이다</b> — 강화는 합성을 넘지 못한다.
        /// </summary>
        public WeaponInstance TryFuse(IReadOnlyList<WeaponInstance> materials)
        {
            if (_profile == null || _catalog == null) return null;

            var datas = new List<WeaponData>();
            if (materials != null)
            {
                foreach (var m in materials) datas.Add(m != null ? m.Data : null);
            }

            var result = FusionTable.Resolve(datas, _catalog, _rng, out var verdict);
            if (result == null)
            {
                LastMessage = FusionTable.LabelOf(verdict);
                return null;
            }

            // 재료가 정말 내 것인지 확인하고 나서 지운다. 같은 정을 두 번 넣는 것도 막는다.
            var seen = new HashSet<int>();
            foreach (var m in materials)
            {
                if (m == null || _profile.FindById(m.Id) == null || !seen.Add(m.Id))
                {
                    LastMessage = "재료가 올바르지 않다";
                    return null;
                }
            }

            foreach (var m in materials) _profile.Remove(m);

            var made = _profile.Acquire(result);
            LastMessage = verdict == FusionVerdict.SameFamily
                ? $"{made.Data.FullName} — 계열 유지"
                : $"{made.Data.FullName} — 무작위 계열";
            return made;
        }

        // ── 분해 ─────────────────────────────────────────────────

        /// <summary>
        /// 한 정을 분해한다. 부품은 확정, 부적은 50%.
        /// 부적은 한 단계 낮게 나오고, 분해한 무기의 등급 이하에만 쓸 수 있다.
        /// </summary>
        public bool TrySalvage(WeaponInstance weapon)
        {
            if (weapon == null || _profile == null) return false;
            if (_profile.FindById(weapon.Id) == null) return false;

            if (_profile.IsEquipped(weapon))
            {
                LastMessage = "편성된 무기는 분해할 수 없다. 칸에서 먼저 빼라";
                return false;
            }

            var y = SalvageTable.Roll(weapon.Tier, weapon.Enhance, _rng);

            _profile.Remove(weapon);
            _profile.AddParts(y.parts);

            if (y.HasTalisman)
            {
                _profile.AddTalisman(y.talismanLevel, y.talismanTier);
                LastMessage = $"부속 +{y.parts} · 강화 부적 +{y.talismanLevel} "
                            + $"({WeaponFamilyInfo.TierName(y.talismanTier)} 이하)";
            }
            else
            {
                LastMessage = weapon.Enhance >= 2
                    ? $"부속 +{y.parts} · 부적은 나오지 않았다"
                    : $"부속 +{y.parts}";
            }
            return true;
        }

        // ── 방지권 제작 ──────────────────────────────────────────

        public bool TryCraftWedge()
        {
            if (_profile == null) return false;
            if (!_profile.CraftWedge())
            {
                LastMessage = $"부속 {SalvageTable.WedgeCost}개 필요 — 보유 {_profile.Parts}";
                return false;
            }
            LastMessage = $"고정 쐐기 제작 (−부속 {SalvageTable.WedgeCost})";
            return true;
        }

        public bool TryCraftSpareBarrel()
        {
            if (_profile == null) return false;
            if (!_profile.CraftSpareBarrel())
            {
                LastMessage = $"부속 {SalvageTable.SpareBarrelCost}개 필요 — 보유 {_profile.Parts}";
                return false;
            }
            LastMessage = $"예비 총열 제작 (−부속 {SalvageTable.SpareBarrelCost})";
            return true;
        }

        // ── 편성 ─────────────────────────────────────────────────

        public bool TryEquip(int slot, WeaponInstance weapon)
        {
            if (_profile == null) return false;
            if (!_profile.Equip(slot, weapon))
            {
                LastMessage = "편성할 수 없다";
                return false;
            }

            LastMessage = weapon != null
                ? $"{slot + 1}번 칸에 {weapon.Label}"
                : $"{slot + 1}번 칸을 비웠다";
            return true;
        }
    }
}
