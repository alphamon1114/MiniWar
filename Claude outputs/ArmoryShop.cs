using System.Collections.Generic;
using MiniWar.Data;

namespace MiniWar.Runtime
{
    /// <summary>
    /// 마을 정비소. <b>강화와 보급</b>은 여기서만 한다.
    ///
    /// 던전 안에는 상점이 없다. 들어가기 전에 정한 화력과 탄약으로 끝까지 가야 하고,
    /// 도중에 돈이 마르면 파산으로 실패한다 — 인던의 긴장은 거기서 나온다.
    ///
    /// 강화는 위력만 올린다. 장전비가 고정이므로 강화는 곧 탄 한 발당 마진이고,
    /// 그래서 "한 판 더 돌아 다음 단계를 걸 것인가"가 계속 물어진다.
    /// </summary>
    public sealed class ArmoryShop
    {
        readonly PlayerProfile _profile;
        readonly System.Random _rng;

        public ArmoryShop(PlayerProfile profile, System.Random rng = null)
        {
            _profile = profile;
            _rng = rng ?? new System.Random();
        }

        public string LastMessage { get; private set; } = "";

        /// <summary>마지막 강화 결과. 연출에 쓴다.</summary>
        public EnhanceResult LastResult { get; private set; } = EnhanceResult.Kept;

        public int Money => _profile != null ? _profile.Money : 0;

        public void ClearMessage() => LastMessage = "";

        // ── 강화 ─────────────────────────────────────────────────

        /// <summary>
        /// 한 단계 올린다. 방지권은 걸린 것만 소모하고, 성공하면 소모하지 않는다 —
        /// 실패했을 때만 일하는 물건이므로 성공에 태우면 그냥 값이 오르는 것과 같다.
        /// </summary>
        public bool TryEnhance(WeaponInstance weapon, bool useWedge, bool useBarrel)
        {
            LastResult = EnhanceResult.Kept;
            if (weapon == null || _profile == null) return false;

            if (weapon.IsMaxEnhance)
            {
                LastMessage = $"{weapon.Data.displayName}은(는) 이미 +{EnhanceTable.MaxLevel}이다";
                return false;
            }

            int cost = weapon.NextEnhanceCost;
            if (_profile.Money < cost)
            {
                LastMessage = $"${cost:N0} 필요 — 잔액 ${_profile.Money:N0}";
                return false;
            }

            // 방지권이 없는데 걸겠다고 하면 조용히 무시한다. 없는 걸 쓴 셈 치면 안 된다.
            bool wedge = useWedge && _profile.Wedges > 0;
            bool barrel = useBarrel && _profile.SpareBarrels > 0;

            _profile.TrySpend(cost);
            _profile.CountEnhance();

            int before = weapon.Enhance;
            var result = EnhanceTable.Roll(before, wedge, barrel, _rng);
            LastResult = result;

            // 실패했을 때만 방지권이 소모된다.
            if (result != EnhanceResult.Success)
            {
                if (wedge) _profile.ConsumeWedge();
                if (barrel) _profile.ConsumeSpareBarrel();
            }

            if (result == EnhanceResult.Destroyed)
            {
                _profile.Remove(weapon);
                LastMessage = $"{weapon.Data.displayName} +{before} 파괴 (−${cost:N0})";
                return true;
            }

            weapon.ApplyEnhance(result);

            LastMessage = result == EnhanceResult.Success
                ? $"{weapon.Data.displayName} +{weapon.Enhance} 성공 (−${cost:N0})"
                : $"{EnhanceTable.LabelOf(result)} — {weapon.Data.displayName} +{weapon.Enhance} (−${cost:N0})";
            return true;
        }

        /// <summary>부적을 쓴다. 등급이 안 맞거나 이미 그 이상이면 거절한다.</summary>
        public bool TryUseTalisman(int index, WeaponInstance target)
        {
            if (_profile == null || target == null) return false;

            var list = _profile.Talismans;
            if (index < 0 || index >= list.Count) return false;

            var t = list[index];
            if (target.Tier > t.maxTier)
            {
                LastMessage = $"이 부적은 {WeaponFamilyInfo.TierName(t.maxTier)} 이하에만 쓸 수 있다";
                return false;
            }
            if (target.Enhance >= t.level)
            {
                LastMessage = $"{target.Data.displayName}은(는) 이미 +{target.Enhance}다";
                return false;
            }

            if (!_profile.UseTalisman(index, target)) return false;

            LastMessage = $"{target.Data.displayName} +{target.Enhance} — 부적 적용";
            return true;
        }

        // ── 보급 ─────────────────────────────────────────────────

        /// <summary>탄창 하나 보급. 던전 안의 장전과 같은 값을 쓴다.</summary>
        public bool TryReload(WeaponInstance weapon)
        {
            if (weapon == null || _profile == null) return false;
            if (weapon.IsFull)
            {
                LastMessage = $"{weapon.Data.displayName}은(는) 이미 가득 찼다";
                return false;
            }

            int cost = weapon.ReloadCost;
            if (!_profile.TrySpend(cost))
            {
                LastMessage = $"${cost:N0} 필요 — 잔액 ${_profile.Money:N0}";
                return false;
            }

            weapon.FillMagazine();
            LastMessage = $"{weapon.Data.displayName} 보급 (−${cost:N0})";
            return true;
        }

        /// <summary>
        /// 전 무기 보급. 돈이 모자라면 <b>싼 것부터</b> 채운다 —
        /// 비싼 총부터 채우면 잔액이 바닥나 권총조차 빈 채로 던전에 들어가게 된다.
        /// </summary>
        public void ReloadAll(Loadout loadout)
        {
            if (loadout == null || _profile == null) return;

            int spent = 0, filled = 0;

            var order = new List<WeaponInstance>(loadout.Weapons);
            order.Sort((a, b) => a.ReloadCost.CompareTo(b.ReloadCost));

            foreach (var w in order)
            {
                if (w.IsFull) continue;
                int cost = w.ReloadCost;
                if (!_profile.TrySpend(cost)) continue;

                w.FillMagazine();
                spent += cost;
                filled++;
            }

            LastMessage = filled == 0
                ? "보급할 것이 없거나 잔액이 부족하다"
                : $"{filled}종 보급 (−${spent:N0})";
        }

        /// <summary>전부 채우는 데 드는 돈. 던전에 들어가기 전 판단 근거.</summary>
        public int FullReloadCost(Loadout loadout)
        {
            if (loadout == null) return 0;
            int sum = 0;
            foreach (var w in loadout.Weapons)
            {
                if (!w.IsFull) sum += w.ReloadCost;
            }
            return sum;
        }
    }
}
