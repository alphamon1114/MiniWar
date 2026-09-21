using System;
using MiniWar.Data;
using UnityEngine;

namespace MiniWar.Runtime
{
    /// <summary>
    /// 한 판 동안 살아 있는 무기 하나. 강화는 무기 단위로 누적되므로
    /// 같은 WeaponData라도 인스턴스마다 스탯이 다르다.
    /// </summary>
    public sealed class WeaponInstance
    {
        const int AxisCount = 4;

        readonly UpgradeTable _table;
        readonly int[] _levels = new int[AxisCount];

        public WeaponData Data { get; }
        public int Ammo { get; private set; }

        public event Action<WeaponInstance> Changed;

        public WeaponInstance(WeaponData data, UpgradeTable table)
        {
            Data = data != null ? data : throw new ArgumentNullException(nameof(data));
            _table = table != null ? table : throw new ArgumentNullException(nameof(table));
            Ammo = MagazineSize;
        }

        public int LevelOf(UpgradeAxis axis) => _levels[(int)axis];

        public int TotalLevels
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < AxisCount; i++) sum += _levels[i];
                return sum;
            }
        }

        float Multiplier(UpgradeAxis axis)
            => Mathf.Pow(_table.MultiplierPerLevel(axis), _levels[(int)axis]);

        // ── 강화가 반영된 실효 스탯 ──────────────────────────────

        public float DamagePerPellet => Data.damagePerPellet * Multiplier(UpgradeAxis.Damage);
        public float ShotsPerSecond => Data.shotsPerSecond * Multiplier(UpgradeAxis.FireRate);

        public int MagazineSize =>
            Mathf.Max(1, Mathf.RoundToInt(Data.magazineSize * Multiplier(UpgradeAxis.MagazineSize)));

        public int ReloadCost =>
            Mathf.Max(0, Mathf.RoundToInt(Data.reloadCost * Multiplier(UpgradeAxis.ReloadCost)));

        public float CostPerShot => MagazineSize <= 0 ? 0f : (float)ReloadCost / MagazineSize;

        public bool IsEmpty => Ammo <= 0;
        public bool IsFull => Ammo >= MagazineSize;

        // ── 상태 변경 ───────────────────────────────────────────

        public void ApplyUpgrade(UpgradeAxis axis)
        {
            _levels[(int)axis]++;
            // 장탄수 강화는 탄창을 넓히기만 하고 공짜로 채워주지는 않는다.
            Ammo = Mathf.Min(Ammo, MagazineSize);
            Changed?.Invoke(this);
        }

        /// <summary>1발 소모. 잔탄이 없으면 false를 돌려주고 아무것도 하지 않는다.</summary>
        public bool TryConsumeShot()
        {
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
            => $"{Data.displayName} {Ammo}/{MagazineSize} (장전비 ${ReloadCost})";
    }
}
