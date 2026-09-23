using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiniWar.Data
{
    public enum RewardKind
    {
        Weapon = 0,
        Money = 1,
        Parts = 2,
    }

    /// <summary>뽑힌 카드 한 장. 뒤집기 전까지는 플레이어에게 안 보인다.</summary>
    public struct RewardCard
    {
        public RewardKind kind;
        public WeaponData weapon;
        public int amount;

        public string Label
        {
            get
            {
                switch (kind)
                {
                    case RewardKind.Weapon: return weapon != null ? weapon.FullName : "???";
                    case RewardKind.Money: return $"$ {amount:N0}";
                    default: return $"폐품 부속 {amount}";
                }
            }
        }

        public string Detail
        {
            get
            {
                switch (kind)
                {
                    case RewardKind.Weapon:
                        return weapon != null ? weapon.FamilyLabel : "";
                    case RewardKind.Money:
                        return "실탄값";
                    default:
                        return "방지권 재료";
                }
            }
        }
    }

    /// <summary>
    /// 던전 하나의 카드 풀. 무엇이 나올 수 있는지를 던전마다 다르게 둔다.
    ///
    /// 풀을 나누는 이유는 하나다 — <b>1번 던전에서 대전차포가 뜨면 뒤가 전부 헐거워진다.</b>
    /// 좋은 총은 뒤쪽 풀에만 넣는다.
    /// </summary>
    [Serializable]
    public struct RewardPool
    {
        [Tooltip("각 항목이 뽑힐 가중치. 0이면 그 던전에서는 안 나온다.")]
        [Min(0)] public int weightTier1;
        [Min(0)] public int weightTier2;
        [Min(0)] public int weightTier3;
        [Min(0)] public int weightMoney;
        [Min(0)] public int weightParts;

        [Header("돈 카드")]
        [Min(0)] public int moneyMin;
        [Min(0)] public int moneyMax;

        [Header("부속 카드")]
        [Min(0)] public int partsMin;
        [Min(0)] public int partsMax;

        public int TotalWeight => weightTier1 + weightTier2 + weightTier3 + weightMoney + weightParts;
    }

    /// <summary>
    /// 클리어 보상 카드를 뽑는다.
    ///
    /// 카드에 <b>돈과 부속도 섞는다.</b> "이번엔 총이 아니라 실탄"이 되는 판이 있어야
    /// 같은 던전을 반복해서 도는 이유가 생긴다.
    ///
    /// <para>
    /// 이 추첨은 나중에 <b>서버로 올라간다</b>. 뒤집기 전에 이미 서버가 정해둔 값이어야
    /// 하고, 클라이언트가 굴리면 원하는 카드가 늘 나오게 만들 수 있다.
    /// </para>
    /// </summary>
    public static class RewardTable
    {
        public const int CardCount = 4;

        public static List<RewardCard> Draw(RewardPool pool, WeaponCatalog catalog,
                                            System.Random rng, int count = CardCount)
        {
            var cards = new List<RewardCard>(count);
            if (pool.TotalWeight <= 0) return cards;

            for (int i = 0; i < count; i++)
                cards.Add(DrawOne(pool, catalog, rng));

            return cards;
        }

        static RewardCard DrawOne(RewardPool pool, WeaponCatalog catalog, System.Random rng)
        {
            int total = pool.TotalWeight;
            int roll = rng != null ? rng.Next(total) : UnityEngine.Random.Range(0, total);

            int acc = pool.weightTier1;
            if (roll < acc) return WeaponCard(1, catalog, rng, pool);

            acc += pool.weightTier2;
            if (roll < acc) return WeaponCard(2, catalog, rng, pool);

            acc += pool.weightTier3;
            if (roll < acc) return WeaponCard(3, catalog, rng, pool);

            acc += pool.weightMoney;
            if (roll < acc)
            {
                return new RewardCard
                {
                    kind = RewardKind.Money,
                    amount = RangeOf(pool.moneyMin, pool.moneyMax, rng),
                };
            }

            return new RewardCard
            {
                kind = RewardKind.Parts,
                amount = RangeOf(pool.partsMin, pool.partsMax, rng),
            };
        }

        static RewardCard WeaponCard(int tier, WeaponCatalog catalog, System.Random rng, RewardPool pool)
        {
            var list = catalog != null ? catalog.OfTier(tier) : null;

            // 그 등급이 비어 있으면 빈손으로 주지 말고 돈으로 바꿔준다.
            if (list == null || list.Count == 0)
            {
                return new RewardCard
                {
                    kind = RewardKind.Money,
                    amount = RangeOf(pool.moneyMin, pool.moneyMax, rng),
                };
            }

            int pick = rng != null ? rng.Next(list.Count) : UnityEngine.Random.Range(0, list.Count);
            return new RewardCard { kind = RewardKind.Weapon, weapon = list[pick] };
        }

        static int RangeOf(int min, int max, System.Random rng)
        {
            if (max <= min) return Mathf.Max(0, min);
            return min + (rng != null ? rng.Next(max - min + 1) : UnityEngine.Random.Range(0, max - min + 1));
        }
    }
}
