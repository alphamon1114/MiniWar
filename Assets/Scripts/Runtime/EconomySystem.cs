using System;
using UnityEngine;

namespace MiniWar.Runtime
{
    /// <summary>
    /// 돈. 이 게임에서 돈은 점수이자 탄약이다.
    ///
    /// 파산 규칙: 모든 무기의 잔탄이 0이고 잔액이 최저 장전비보다 적으면 즉시 게임 오버.
    /// 잔액만 0이 되는 것은 아직 패배가 아니다 — 남은 탄을 다 쓸 때까지는 살아 있다.
    /// 즉사 판정이므로 <see cref="IsInDanger"/>로 미리 경고해 억울함을 없앤다.
    /// </summary>
    public sealed class EconomySystem
    {
        readonly Loadout _loadout;
        readonly float _warningMultiple;

        int _money;
        bool _wasInDanger;
        bool _bankrupt;

        public event Action<int> MoneyChanged;
        public event Action<bool> DangerStateChanged;
        public event Action Bankrupt;

        public EconomySystem(Loadout loadout, int startingMoney = 300, float warningMultiple = 3f)
        {
            _loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            _warningMultiple = Mathf.Max(1f, warningMultiple);
            _money = Mathf.Max(0, startingMoney);
        }

        public int Money => _money;
        public bool IsBankrupt => _bankrupt;

        /// <summary>잔액이 최저 장전비의 N배 아래로 떨어진 상태. HUD의 $ 표시를 경고색으로 바꿀 신호.</summary>
        public bool IsInDanger => _money < _loadout.CheapestReloadCost * _warningMultiple;

        public void AddReward(int amount)
        {
            if (amount <= 0) return;
            _money += amount;
            MoneyChanged?.Invoke(_money);
            RefreshDangerState();
        }

        public bool CanAfford(WeaponInstance weapon) => weapon != null && _money >= weapon.ReloadCost;

        /// <summary>
        /// 장전 시도. 성공하면 비용을 차감하고 탄창을 채운다.
        /// 실패(잔액 부족)했다면 곧바로 파산인지 확인한다.
        /// </summary>
        public bool TryReload(WeaponInstance weapon)
        {
            if (weapon == null || _bankrupt) return false;
            if (weapon.IsFull) return false;

            if (!CanAfford(weapon))
            {
                CheckBankruptcy();
                return false;
            }

            _money -= weapon.ReloadCost;
            weapon.FillMagazine();
            MoneyChanged?.Invoke(_money);
            RefreshDangerState();
            return true;
        }

        /// <summary>
        /// 사격 직후, 그리고 장전 실패 시에 호출한다.
        /// 전탄 0 + 최저 장전비 미만이면 그 자리에서 게임 오버.
        /// </summary>
        public bool CheckBankruptcy()
        {
            if (_bankrupt) return true;
            if (!_loadout.AllEmpty) return false;
            if (_money >= _loadout.CheapestReloadCost) return false;

            _bankrupt = true;
            Bankrupt?.Invoke();
            return true;
        }

        void RefreshDangerState()
        {
            bool danger = IsInDanger;
            if (danger == _wasInDanger) return;
            _wasInDanger = danger;
            DangerStateChanged?.Invoke(danger);
        }
    }
}
