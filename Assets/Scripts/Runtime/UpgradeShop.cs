using MiniWar.Data;
using UnityEngine;

namespace MiniWar.Runtime
{
    /// <summary>
    /// 마을 정비소. 강화와 보급은 <b>여기서만</b> 한다.
    ///
    /// 던전 안에는 상점이 없다. 들어가기 전에 정한 화력과 탄약으로 끝까지 가야 하고,
    /// 도중에 돈이 마르면 파산으로 실패한다 — 인던의 긴장은 거기서 나온다.
    /// 대신 마을에서는 <b>돈이 되는 만큼</b> 얼마든지 산다. 횟수 제한은 없다.
    ///
    /// 유일한 제동은 가격이 살 때마다 배로 오른다는 것뿐이고,
    /// 그래서 "이번 던전을 한 번 더 돌아 다음 강화를 살 것인가"가 계속 물어진다.
    /// </summary>
    public sealed class UpgradeShop
    {
        readonly UpgradeTable _table;
        readonly PlayerProfile _profile;

        public UpgradeShop(UpgradeTable table, PlayerProfile profile)
        {
            _table = table;
            _profile = profile;
        }

        public string LastMessage { get; private set; } = "";

        public int Money => _profile != null ? _profile.Money : 0;

        public int NextCost => _table != null && _profile != null
            ? _table.CostOf(_profile.UpgradesPurchased)
            : 0;

        public bool CanAfford => _profile != null && _profile.Money >= NextCost;

        public int PurchasedTotal => _profile != null ? _profile.UpgradesPurchased : 0;

        public void ClearMessage() => LastMessage = "";

        public bool TryBuy(WeaponInstance weapon, UpgradeAxis axis)
        {
            if (weapon == null || _profile == null || _table == null) return false;

            int cost = NextCost;
            if (!_profile.TrySpend(cost))
            {
                LastMessage = $"${cost:N0} 필요 — 잔액 ${_profile.Money:N0}";
                return false;
            }

            weapon.ApplyUpgrade(axis);
            _profile.CountUpgrade();
            LastMessage = $"{weapon.Data.displayName} · {UpgradeTable.LabelOf(axis)} 강화 (−${cost:N0})";
            return true;
        }

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

            var order = new System.Collections.Generic.List<WeaponInstance>(loadout.Weapons);
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
                if (!w.IsFull) sum += w.ReloadCost;
            return sum;
        }

        // ── 무기 구매 ────────────────────────────────────────────

        /// <summary>
        /// 무기를 산다. 시작 장비는 권총 한 자루뿐이고 나머지는 전부 여기서 나온다.
        ///
        /// 무기를 돈으로 사게 하면 강화와 정면으로 경쟁한다 —
        /// "권총을 두 번 강화할 것인가, 연사형을 한 자루 살 것인가"는
        /// 강화 축을 고르는 것과 전혀 다른 층위의 질문이고, 답이 판마다 달라진다.
        /// </summary>
        public bool TryBuyWeapon(WeaponData data)
        {
            if (data == null || _profile == null) return false;

            if (_profile.Owns(data))
            {
                LastMessage = $"{data.displayName}은(는) 이미 가지고 있다";
                return false;
            }

            if (!_profile.TrySpend(data.price))
            {
                LastMessage = $"${data.price:N0} 필요 — 잔액 ${_profile.Money:N0}";
                return false;
            }

            _profile.Acquire(data);
            LastMessage = $"{data.displayName} 구입 (−${data.price:N0})";
            return true;
        }

        /// <summary>같은 칸의 다른 무기로 갈아 끼운다.</summary>
        public bool TryEquip(WeaponData data)
        {
            if (data == null || _profile == null) return false;

            if (!_profile.Owns(data))
            {
                LastMessage = $"{data.displayName}을(를) 아직 가지고 있지 않다";
                return false;
            }

            _profile.Equip(data);
            LastMessage = $"{WeaponData.LabelOf(data.role)} 칸에 {data.displayName} 장착";
            return true;
        }
    }
}
