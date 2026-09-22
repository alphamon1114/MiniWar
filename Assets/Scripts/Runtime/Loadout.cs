using System;
using System.Collections.Generic;
using MiniWar.Data;

namespace MiniWar.Runtime
{
    /// <summary>플레이어가 들고 있는 무기 전체. 파산 판정에 필요한 집계를 여기서 낸다.</summary>
    public sealed class Loadout
    {
        readonly List<WeaponInstance> _weapons = new();
        int _currentIndex;

        public IReadOnlyList<WeaponInstance> Weapons => _weapons;
        public WeaponInstance Current => _weapons.Count > 0 ? _weapons[_currentIndex] : null;

        public event Action<WeaponInstance> CurrentChanged;

        public Loadout(IEnumerable<WeaponData> datas, UpgradeTable table)
        {
            foreach (var d in datas)
            {
                if (d != null) _weapons.Add(new WeaponInstance(d, table));
            }
            _weapons.Sort((a, b) => a.Data.slot.CompareTo(b.Data.slot));
        }

        /// <summary>숫자키 슬롯으로 전환. 없는 슬롯이면 무시한다.</summary>
        public bool SelectSlot(int slot)
        {
            for (int i = 0; i < _weapons.Count; i++)
            {
                if (_weapons[i].Data.slot != slot) continue;
                if (i == _currentIndex) return true;
                _currentIndex = i;
                CurrentChanged?.Invoke(Current);
                return true;
            }
            return false;
        }

        public void SelectNext()
        {
            if (_weapons.Count == 0) return;
            _currentIndex = (_currentIndex + 1) % _weapons.Count;
            CurrentChanged?.Invoke(Current);
        }

        /// <summary>
        /// 모든 <b>탄창형</b> 무기의 잔탄이 0인가. 파산 판정의 전제 조건.
        ///
        /// 특수킷(수류탄)과 근접무기는 탄창이 없어 영원히 "비어 있지 않으므로" 여기서 빼야 한다.
        /// 넣어두면 그걸 들고 있다는 이유만으로 파산이 절대 성립하지 않는다.
        ///
        /// 근접무기가 공짜라서 "싸울 수단이 남았는데 왜 게임 오버냐"는 말이 나올 수 있지만,
        /// 총알 없이 군대를 상대할 수는 없다는 것이 이 게임의 전제다 — 파산은 그대로 패배다.
        /// </summary>
        public bool AllEmpty
        {
            get
            {
                foreach (var w in _weapons)
                {
                    if (w.IsMagazineless) continue;
                    if (!w.IsEmpty) return false;
                }
                return true;
            }
        }

        /// <summary>
        /// 가장 싼 탄창형 장전 비용. 이 값도 못 내면 파산이다.
        /// 특수킷의 투척비와 근접무기(공짜)는 파산 기준에서 제외한다.
        /// </summary>
        public int CheapestReloadCost
        {
            get
            {
                int min = int.MaxValue;
                foreach (var w in _weapons)
                {
                    if (w.IsMagazineless) continue;
                    if (w.ReloadCost < min) min = w.ReloadCost;
                }
                return min == int.MaxValue ? 0 : min;
            }
        }

        public WeaponInstance Find(WeaponData data)
        {
            foreach (var w in _weapons)
            {
                if (w.Data == data) return w;
            }
            return null;
        }
    }
}
