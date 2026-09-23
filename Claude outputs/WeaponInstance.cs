using System;
using MiniWar.Data;
using UnityEngine;

namespace MiniWar.Runtime
{
    /// <summary>
    /// 소지한 무기 한 정. 같은 <see cref="WeaponData"/>라도 인스턴스마다
    /// 강화 단계가 다르므로 <b>중복 보유가 전제</b>다 — 셋을 모아 합치는 것이 진행이다.
    ///
    /// 강화는 <b>위력 하나</b>만 올린다. 장전비는 그대로이므로 강화는 곧
    /// 탄 한 발당 마진을 올린다 — 이 게임에서 강화는 화력이자 경제다.
    /// </summary>
    public sealed class WeaponInstance
    {
        static int _nextId = 1;

        /// <summary>같은 무기 여러 정을 구별하는 열쇠. 합성·분해가 이걸로 지목한다.</summary>
        public int Id { get; }

        public WeaponData Data { get; }
        public int Ammo { get; private set; }

        /// <summary>+0 ~ +15.</summary>
        public int Enhance { get; private set; }

        public event Action<WeaponInstance> Changed;

        public WeaponInstance(WeaponData data, int enhance = 0, int id = 0)
        {
            Data = data != null ? data : throw new ArgumentNullException(nameof(data));
            Enhance = EnhanceTable.Clamp(enhance);
            Id = id > 0 ? id : _nextId++;
            if (id >= _nextId) _nextId = id + 1;
            Ammo = MagazineSize;
        }

        public int Tier => Data.tier;
        public WeaponFamily Family => Data.family;

        // ── 강화가 반영된 실효 스탯 ──────────────────────────────

        public float DamagePerPellet => Data.damagePerPellet * EnhanceTable.PowerMultiplier(Enhance);

        // 연사·장탄·장전비는 강화로 움직이지 않는다. 축을 하나로 둔 이유가 여기 있다 —
        // 위력만 오르고 장전비가 고정이라야 "강화 = 마진"이라는 관계가 흐려지지 않는다.
        public float ShotsPerSecond => Data.shotsPerSecond;
        public int MagazineSize => Mathf.Max(1, Data.magazineSize);
        public int ReloadCost => Mathf.Max(0, Data.reloadCost);

        public float CostPerShot => MagazineSize <= 0 ? 0f : (float)ReloadCost / MagazineSize;

        /// <summary>탄창 없이 쓸 때마다 돈이 나가는 곡사류인가.</summary>
        public bool IsPerThrow => Data.ammoMode == AmmoMode.PerThrow;

        /// <summary>탄약도 돈도 들지 않는 근접무기인가.</summary>
        public bool IsMelee => Data.ammoMode == AmmoMode.Melee;

        /// <summary>
        /// 탄창을 쓰지 않는 무기. 파산 판정에서 빠지는 것들.
        ///
        /// 칸이 둘로 줄면서 근접무기가 한 칸을 먹게 됐지만, 이 제외 규칙은 그대로 살린다.
        /// 넣어두면 근접을 들고 있다는 이유만으로 파산이 절대 성립하지 않는다.
        /// </summary>
        public bool IsMagazineless => IsPerThrow || IsMelee;

        /// <summary>1회 사용 비용. 탄창형은 장전비, 곡사는 투척비, 근접은 0.</summary>
        public int UseCost => IsMelee ? 0 : ReloadCost;

        public bool IsEmpty => !IsMagazineless && Ammo <= 0;
        public bool IsFull => IsMagazineless || Ammo >= MagazineSize;

        public bool IsMaxEnhance => EnhanceTable.IsMax(Enhance);

        /// <summary>다음 강화 비용. 등급에 따라 다르다.</summary>
        public int NextEnhanceCost => EnhanceTable.Cost(Enhance, Tier);

        public float NextEnhanceRate => EnhanceTable.SuccessRate(Enhance);

        public EnhanceFailure NextFailure => EnhanceTable.FailureAt(Enhance);

        /// <summary>목록에 뜨는 이름. "제식 · 돌격소총 +9" 꼴.</summary>
        public string Label => Enhance > 0 ? $"{Data.FullName} +{Enhance}" : Data.FullName;

        // ── 상태 변경 ───────────────────────────────────────────

        /// <summary>강화 결과를 적용한다. 파괴는 부르는 쪽이 인스턴스를 버려서 처리한다.</summary>
        public void ApplyEnhance(EnhanceResult result)
        {
            if (result == EnhanceResult.Success)
                Enhance = EnhanceTable.Clamp(Enhance + 1);
            else if (result == EnhanceResult.Dropped)
                Enhance = Mathf.Max(0, Enhance - 1);

            Changed?.Invoke(this);
        }

        /// <summary>부적으로 단계를 끌어올린다. 이미 그 이상이면 아무 일도 없다.</summary>
        public bool ApplyTalisman(int level)
        {
            int target = EnhanceTable.Clamp(level);
            if (target <= Enhance) return false;
            Enhance = target;
            Changed?.Invoke(this);
            return true;
        }

        /// <summary>저장에서 되살릴 때만 쓴다.</summary>
        public void SetEnhance(int level)
        {
            Enhance = EnhanceTable.Clamp(level);
            Changed?.Invoke(this);
        }

        public void SetAmmo(int ammo)
        {
            Ammo = Mathf.Clamp(ammo, 0, MagazineSize);
            Changed?.Invoke(this);
        }

        /// <summary>1발 소모. 잔탄이 없으면 false를 돌려주고 아무것도 하지 않는다.</summary>
        public bool TryConsumeShot()
        {
            if (IsMagazineless) return true;    // 탄창이 아니라 돈(또는 거리)으로 계산한다
            if (Ammo <= 0) return false;
            Ammo--;
            Changed?.Invoke(this);
            return true;
        }

        /// <summary>비용 처리는 EconomySystem이 맡는다. 여기서는 탄창만 채운다.</summary>
        public void FillMagazine()
        {
            Ammo = MagazineSize;
            Changed?.Invoke(this);
        }

        public override string ToString()
            => $"{Label} {Ammo}/{MagazineSize} (장전비 ${ReloadCost})";
    }
}
