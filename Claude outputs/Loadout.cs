using System;
using System.Collections.Generic;

namespace MiniWar.Runtime
{
    /// <summary>
    /// 들고 나간 무기. <b>두 칸</b>이고, 어느 계열이든 자유롭게 들어간다.
    ///
    /// 역할별 칸(주무기·보조·근접·특수킷)을 없앤 이유는 포기를 칸 밖으로 내보내기 위해서다.
    /// 칸 안에서 고르게 하면 "주무기 셋 중 하나"라는 좁은 질문이 되지만,
    /// 자유 2칸이면 관통 둘을 들고 보병을 포기하거나, 근접과 연사로 탄약비를 아끼는
    /// 선택이 열린다. 근접도 한 칸을 먹으므로 공짜 화력에는 총 한 자루라는 값이 붙는다.
    /// </summary>
    public sealed class Loadout
    {
        public const int SlotCount = 2;

        readonly List<WeaponInstance> _weapons = new List<WeaponInstance>();
        int _currentIndex;

        public IReadOnlyList<WeaponInstance> Weapons => _weapons;
        public WeaponInstance Current => _weapons.Count > 0 ? _weapons[_currentIndex] : null;

        public event Action<WeaponInstance> CurrentChanged;

        /// <summary>편성된 순서 그대로 받는다. 숫자키 1·2가 곧 이 순서다.</summary>
        public Loadout(IEnumerable<WeaponInstance> equipped)
        {
            if (equipped == null) return;
            foreach (var w in equipped)
            {
                if (w != null && _weapons.Count < SlotCount) _weapons.Add(w);
            }
        }

        /// <summary>숫자키 슬롯(1부터)으로 전환. 비어 있는 칸이면 무시한다.</summary>
        public bool SelectSlot(int slot)
        {
            int i = slot - 1;
            if (i < 0 || i >= _weapons.Count) return false;
            if (i == _currentIndex) return true;
            _currentIndex = i;
            CurrentChanged?.Invoke(Current);
            return true;
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
        /// 곡사(투척당 과금)와 근접은 탄창이 없어 영원히 "비어 있지 않으므로" 여기서 빼야 한다.
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
        /// 곡사의 투척비와 근접(공짜)은 파산 기준에서 제외한다.
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

        public WeaponInstance FindById(int id)
        {
            foreach (var w in _weapons)
            {
                if (w.Id == id) return w;
            }
            return null;
        }
    }
}
