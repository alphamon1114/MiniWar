using System.Collections.Generic;
using MiniWar.Data;
using UnityEngine;

namespace MiniWar.Runtime
{
    /// <summary>
    /// 던전을 나가도 남는 것들. 돈·보유 무기·장착·강화·잔탄·클리어 기록.
    ///
    /// 인던 구조에서는 <b>무엇이 남고 무엇이 초기화되는지</b>가 곧 게임의 성격이다.
    /// 여기 있는 것은 남고(마을에서 쌓인다), 체력과 진행 위치는 던전마다 초기화된다.
    /// 강화와 무기가 남기 때문에 "이번 던전은 못 깨겠다 → 쉬운 던전 돌고 사서 다시" 라는
    /// 되돌아가기가 성립한다. 한 판짜리 게임이었다면 없었을 선택지다.
    /// </summary>
    public sealed class PlayerProfile
    {
        const int RoleCount = 4;

        readonly Dictionary<WeaponData, int[]> _levels = new Dictionary<WeaponData, int[]>();
        readonly Dictionary<WeaponData, int> _ammo = new Dictionary<WeaponData, int>();
        readonly HashSet<WeaponData> _owned = new HashSet<WeaponData>();
        readonly WeaponData[] _equipped = new WeaponData[RoleCount];
        readonly HashSet<StageData> _cleared = new HashSet<StageData>();

        public int Money { get; private set; }

        /// <summary>판 전체 누적 구매 횟수. 강화 가격이 이 값으로 정해진다.</summary>
        public int UpgradesPurchased { get; private set; }

        public int DungeonsRun { get; private set; }

        public PlayerProfile(int startingMoney) => Money = Mathf.Max(0, startingMoney);

        // ── 돈 ───────────────────────────────────────────────────

        public void SetMoney(int value) => Money = Mathf.Max(0, value);

        public void Add(int amount) => Money = Mathf.Max(0, Money + amount);

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Money < amount) return false;
            Money -= amount;
            return true;
        }

        public void CountUpgrade() => UpgradesPurchased++;

        // ── 보유 무기 ────────────────────────────────────────────

        public bool Owns(WeaponData data) => data != null && _owned.Contains(data);

        public int OwnedCount => _owned.Count;

        /// <summary>
        /// 시작 장비를 준다. 값이 0인 무기(권총)가 원칙이지만,
        /// <b>맨손으로 출발하는 일은 어떤 경우에도 없어야 한다.</b>
        ///
        /// price는 새로 생긴 직렬화 필드라 에셋이 정리되기 전에는 전부 0이다.
        /// 그 상태를 그대로 믿으면 모든 총을 공짜로 주게 되고, 반대로 값이 다 매겨져
        /// 있으면 한 자루도 못 준다 — 양쪽 다 게임이 시작되지 않는다.
        /// 그래서 데이터 상태와 무관하게 최소 한 자루를 보장한다.
        /// </summary>
        public void GrantStarterWeapons(IEnumerable<WeaponData> all)
        {
            if (all == null) return;

            var list = new List<WeaponData>();
            foreach (var w in all)
                if (w != null) list.Add(w);

            if (list.Count == 0)
            {
                Debug.LogError("무기 에셋이 하나도 없습니다. MiniWar → 씬 생성 을 실행하세요.");
                return;
            }

            int free = 0;
            foreach (var w in list)
                if (w.price <= 0) free++;

            // 값이 전부 0 = 아직 정리되지 않은 에셋. 이때는 공짜로 다 주면 안 된다.
            bool priced = free < list.Count;

            if (priced)
            {
                foreach (var w in list)
                    if (w.price <= 0) Acquire(w);
            }

            if (_owned.Count > 0) return;

            // 마지막 보루 — 가장 싸게 굴릴 수 있는 한 자루. 보통 권총이 걸린다.
            var fallback = CheapestToRun(list, WeaponRole.Sidearm) ?? CheapestToRun(list, null);
            if (fallback == null) return;

            Acquire(fallback);

            Debug.LogWarning(priced
                ? $"값이 0인 시작 무기가 없어 {fallback.displayName}을(를) 지급했습니다."
                : "무기 가격이 설정되지 않았습니다. MiniWar → 무기 에셋 정리 를 실행하세요. "
                  + $"지금은 {fallback.displayName}만 지급합니다.");
        }

        /// <summary>
        /// 유지비가 가장 싼 무기. 값 → 장전비 → 위력 순으로 고른다.
        /// 에셋이 정리되기 전이라 값이 전부 같아도 장전비가 권총을 집어낸다.
        /// </summary>
        static WeaponData CheapestToRun(List<WeaponData> list, WeaponRole? role)
        {
            WeaponData best = null;

            foreach (var w in list)
            {
                if (role.HasValue && w.role != role.Value) continue;
                if (best == null || Better(w, best)) best = w;
            }

            return best;
        }

        static bool Better(WeaponData a, WeaponData b)
        {
            if (a.price != b.price) return a.price < b.price;
            if (a.reloadCost != b.reloadCost) return a.reloadCost < b.reloadCost;
            return a.damagePerPellet < b.damagePerPellet;
        }

        public void Acquire(WeaponData data)
        {
            if (data == null || !_owned.Add(data)) return;

            // 빈 칸이면 자동으로 장착한다. 사놓고 안 끼워서 빈손으로 나가는 일이 없게.
            if (_equipped[(int)data.role] == null) _equipped[(int)data.role] = data;
        }

        // ── 장착 ─────────────────────────────────────────────────

        public WeaponData EquippedIn(WeaponRole role) => _equipped[(int)role];

        public bool Equip(WeaponData data)
        {
            if (data == null || !Owns(data)) return false;
            _equipped[(int)data.role] = data;
            return true;
        }

        public void Unequip(WeaponRole role) => _equipped[(int)role] = null;

        /// <summary>지금 장착한 무기들. 빈 칸은 건너뛴다.</summary>
        public List<WeaponData> EquippedList()
        {
            var list = new List<WeaponData>(RoleCount);
            for (int i = 0; i < RoleCount; i++)
                if (_equipped[i] != null) list.Add(_equipped[i]);
            return list;
        }

        // ── 클리어 기록 ──────────────────────────────────────────

        public bool IsCleared(StageData stage) => stage != null && _cleared.Contains(stage);

        public void MarkCleared(StageData stage)
        {
            if (stage != null) _cleared.Add(stage);
        }

        public void CountRun() => DungeonsRun++;

        public int ClearedCount => _cleared.Count;

        // ── 무기 상태 ────────────────────────────────────────────

        /// <summary>던전에 들어가기 전, 저장된 강화·잔탄을 무기에 되돌려 놓는다.</summary>
        public void Restore(Loadout loadout)
        {
            foreach (var w in loadout.Weapons)
            {
                if (_levels.TryGetValue(w.Data, out var levels)) w.SetLevels(levels);

                if (_ammo.TryGetValue(w.Data, out int ammo)) w.SetAmmo(ammo);
                else w.FillMagazine();      // 새로 산 무기는 한 탄창 채워서 준다
            }
        }

        /// <summary>던전에서 나올 때(클리어든 실패든) 현재 상태를 저장한다.</summary>
        public void Capture(Loadout loadout)
        {
            foreach (var w in loadout.Weapons)
            {
                _levels[w.Data] = w.GetLevels();
                _ammo[w.Data] = w.Ammo;
            }
        }

        public int LevelOf(WeaponData data, UpgradeAxis axis)
            => data != null && _levels.TryGetValue(data, out var l) ? l[(int)axis] : 0;

        /// <summary>장착하지 않은 무기의 잔탄. 무기상 목록에 보여준다.</summary>
        public int AmmoOf(WeaponData data)
            => data != null && _ammo.TryGetValue(data, out int a) ? a : 0;
    }
}
