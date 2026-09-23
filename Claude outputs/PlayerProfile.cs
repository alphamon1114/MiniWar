using System.Collections.Generic;
using MiniWar.Data;
using UnityEngine;

namespace MiniWar.Runtime
{
    /// <summary>강화 부적. 단계와, 쓸 수 있는 최대 등급을 들고 있다.</summary>
    public struct Talisman
    {
        public int level;
        public int maxTier;

        public override string ToString()
            => $"강화 부적 +{level} ({WeaponFamilyInfo.TierName(maxTier)} 이하)";
    }

    /// <summary>
    /// 던전을 나가도 남는 것들. 돈·보유 무기·편성·부품·부적·방지권·클리어 기록.
    ///
    /// 인던 구조에서는 <b>무엇이 남고 무엇이 초기화되는지</b>가 곧 게임의 성격이다.
    /// 여기 있는 것은 남고, 체력과 진행 위치는 던전마다 초기화된다.
    ///
    /// 보유 무기를 <b>인스턴스 목록</b>으로 두는 것이 핵심이다 — 같은 무기를 여러 정
    /// 가질 수 있어야 셋을 모아 합칠 수 있고, 강화 단계도 정마다 다르다.
    /// </summary>
    public sealed class PlayerProfile
    {
        readonly List<WeaponInstance> _owned = new List<WeaponInstance>();
        readonly int[] _equipped = new int[Loadout.SlotCount];   // WeaponInstance.Id, 0이면 빈 칸
        readonly List<Talisman> _talismans = new List<Talisman>();
        readonly HashSet<StageData> _cleared = new HashSet<StageData>();

        public int Money { get; private set; }

        /// <summary>폐품 부속. 방지권을 만드는 재료.</summary>
        public int Parts { get; private set; }

        /// <summary>고정 쐐기 — 하락 방지.</summary>
        public int Wedges { get; private set; }

        /// <summary>예비 총열 — 파괴 방지.</summary>
        public int SpareBarrels { get; private set; }

        /// <summary>판 전체 누적 강화 시도. 표시용.</summary>
        public int EnhanceAttempts { get; private set; }

        public int DungeonsRun { get; private set; }

        public PlayerProfile(int startingMoney) => Money = Mathf.Max(0, startingMoney);

        // ── 돈과 재료 ────────────────────────────────────────────

        public void SetMoney(int value) => Money = Mathf.Max(0, value);

        public void Add(int amount) => Money = Mathf.Max(0, Money + amount);

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Money < amount) return false;
            Money -= amount;
            return true;
        }

        public void AddParts(int amount) => Parts = Mathf.Max(0, Parts + amount);

        public void CountEnhance() => EnhanceAttempts++;

        /// <summary>고정 쐐기를 만든다. 부품이 모자라면 false.</summary>
        public bool CraftWedge()
        {
            if (Parts < SalvageTable.WedgeCost) return false;
            Parts -= SalvageTable.WedgeCost;
            Wedges++;
            return true;
        }

        /// <summary>예비 총열을 만든다. 부품이 모자라면 false.</summary>
        public bool CraftSpareBarrel()
        {
            if (Parts < SalvageTable.SpareBarrelCost) return false;
            Parts -= SalvageTable.SpareBarrelCost;
            SpareBarrels++;
            return true;
        }

        public bool ConsumeWedge()
        {
            if (Wedges <= 0) return false;
            Wedges--;
            return true;
        }

        public bool ConsumeSpareBarrel()
        {
            if (SpareBarrels <= 0) return false;
            SpareBarrels--;
            return true;
        }

        // ── 보유 무기 ────────────────────────────────────────────

        public IReadOnlyList<WeaponInstance> Owned => _owned;

        public int OwnedCount => _owned.Count;

        public WeaponInstance FindById(int id)
        {
            foreach (var w in _owned)
            {
                if (w.Id == id) return w;
            }
            return null;
        }

        /// <summary>같은 무기를 몇 정 가지고 있나. 합성 가능 여부를 보여줄 때 쓴다.</summary>
        public int CountOf(WeaponData data)
        {
            if (data == null) return 0;
            int n = 0;
            foreach (var w in _owned)
            {
                if (w.Data == data) n++;
            }
            return n;
        }

        /// <summary>새 무기 한 정을 받는다. 카드 보상과 합성 결과가 여기로 들어온다.</summary>
        public WeaponInstance Acquire(WeaponData data, int enhance = 0)
        {
            if (data == null) return null;

            var inst = new WeaponInstance(data, enhance);
            _owned.Add(inst);

            // 빈 칸이면 자동으로 편성한다. 받아놓고 안 끼워서 빈손으로 나가는 일이 없게.
            for (int i = 0; i < _equipped.Length; i++)
            {
                if (_equipped[i] == 0) { _equipped[i] = inst.Id; break; }
            }
            return inst;
        }

        /// <summary>목록에서 지운다. 합성 재료로 들어가거나 분해되거나 강화로 파괴될 때.</summary>
        public bool Remove(WeaponInstance inst)
        {
            if (inst == null || !_owned.Remove(inst)) return false;

            for (int i = 0; i < _equipped.Length; i++)
            {
                if (_equipped[i] == inst.Id) _equipped[i] = 0;
            }
            return true;
        }

        /// <summary>
        /// 처음 쥐여주는 총. 1등급 단발 한 자루다.
        ///
        /// <b>맨손으로 출발하는 일은 어떤 경우에도 없어야 한다.</b>
        /// 격자에 1등급 단발이 없으면(에셋이 아직 안 만들어졌으면) 아무거나 한 자루라도 준다.
        /// </summary>
        public void GrantStarter(WeaponCatalog catalog)
        {
            if (_owned.Count > 0) return;

            if (catalog == null)
            {
                Debug.LogError("무기 격자가 없습니다. MiniWar → 씬 생성 을 실행하세요.");
                return;
            }

            var starter = catalog.Starter;
            if (starter == null)
            {
                var tier1 = catalog.OfTier(1);
                if (tier1.Count > 0)
                {
                    starter = tier1[0];
                    Debug.LogWarning($"1등급 단발이 없어 {starter.displayName}을(를) 지급했습니다.");
                }
            }

            if (starter == null)
            {
                Debug.LogError("1등급 무기가 하나도 없습니다. MiniWar → 씬 생성 을 실행하세요.");
                return;
            }

            Acquire(starter);
        }

        // ── 편성 ─────────────────────────────────────────────────

        public WeaponInstance EquippedIn(int slot)
        {
            if (slot < 0 || slot >= _equipped.Length) return null;
            return _equipped[slot] == 0 ? null : FindById(_equipped[slot]);
        }

        /// <summary>칸에 끼운다. 이미 다른 칸에 있으면 그 칸을 비운다(같은 정을 두 칸에 못 넣는다).</summary>
        public bool Equip(int slot, WeaponInstance inst)
        {
            if (slot < 0 || slot >= _equipped.Length) return false;
            if (inst != null && FindById(inst.Id) == null) return false;

            if (inst != null)
            {
                for (int i = 0; i < _equipped.Length; i++)
                {
                    if (_equipped[i] == inst.Id) _equipped[i] = 0;
                }
            }

            _equipped[slot] = inst != null ? inst.Id : 0;
            return true;
        }

        public bool IsEquipped(WeaponInstance inst)
        {
            if (inst == null) return false;
            foreach (int id in _equipped)
            {
                if (id == inst.Id) return true;
            }
            return false;
        }

        /// <summary>지금 편성한 무기들. 빈 칸은 건너뛴다.</summary>
        public List<WeaponInstance> EquippedList()
        {
            var list = new List<WeaponInstance>(Loadout.SlotCount);
            for (int i = 0; i < _equipped.Length; i++)
            {
                var w = EquippedIn(i);
                if (w != null) list.Add(w);
            }
            return list;
        }

        // ── 부적 ─────────────────────────────────────────────────

        public IReadOnlyList<Talisman> Talismans => _talismans;

        public void AddTalisman(int level, int maxTier)
        {
            if (level <= 0) return;
            _talismans.Add(new Talisman { level = level, maxTier = maxTier });
        }

        /// <summary>부적을 쓴다. 등급이 안 맞거나 이미 그 이상이면 아무 일도 없다.</summary>
        public bool UseTalisman(int index, WeaponInstance target)
        {
            if (index < 0 || index >= _talismans.Count || target == null) return false;

            var t = _talismans[index];
            if (target.Tier > t.maxTier) return false;
            if (!target.ApplyTalisman(t.level)) return false;

            _talismans.RemoveAt(index);
            return true;
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
        //
        // 강화는 이제 WeaponInstance 자신이 들고 다니므로 따로 저장할 것이 없다.
        // 잔탄만 던전을 나올 때 그대로 남는다 — 인스턴스가 곧 소지품이기 때문이다.

        /// <summary>던전에 들어가기 전. 편성된 무기 중 처음 쓰는 것은 한 탄창 채워 준다.</summary>
        public void Restore(Loadout loadout)
        {
            if (loadout == null) return;
            foreach (var w in loadout.Weapons)
            {
                if (w.Ammo <= 0 && w.IsMagazineless) w.FillMagazine();
            }
        }

        /// <summary>던전에서 나올 때. 인스턴스를 공유하므로 잔탄은 이미 반영돼 있다.</summary>
        public void Capture(Loadout loadout)
        {
            // 남겨둔다 — 나중에 서버 저장을 붙일 때 여기가 직렬화 지점이 된다.
        }
    }
}
