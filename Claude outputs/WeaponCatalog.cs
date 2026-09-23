using System.Collections.Generic;
using UnityEngine;

namespace MiniWar.Data
{
    /// <summary>
    /// 등급 × 계열 격자. 합성 결과와 카드 추첨이 "다음 등급의 이 계열"을 물어보는 곳.
    ///
    /// 격자를 꽉 채워 두는 것이 전제다 — 비어 있는 칸이 있으면 합성이
    /// "같은 계열"을 약속할 수 없다. <see cref="Validate"/>가 그걸 확인한다.
    /// </summary>
    [CreateAssetMenu(menuName = "무기전쟁/Weapon Catalog", fileName = "SO_WeaponCatalog")]
    public sealed class WeaponCatalog : ScriptableObject
    {
        [Tooltip("18정 전부. 순서는 상관없다 — 등급과 계열로 찾는다.")]
        public WeaponData[] weapons = new WeaponData[0];

        public WeaponData Find(int tier, WeaponFamily family)
        {
            if (weapons == null) return null;
            foreach (var w in weapons)
            {
                if (w != null && w.tier == tier && w.family == family) return w;
            }
            return null;
        }

        /// <summary>해당 등급의 무기 전부. 카드 추첨과 무작위 합성이 쓴다.</summary>
        public List<WeaponData> OfTier(int tier)
        {
            var list = new List<WeaponData>();
            if (weapons == null) return list;
            foreach (var w in weapons)
            {
                if (w != null && w.tier == tier) list.Add(w);
            }
            return list;
        }

        /// <summary>가장 싼 1등급 단발 — 처음 쥐여주는 총.</summary>
        public WeaponData Starter => Find(1, WeaponFamily.Single);

        /// <summary>빈 칸을 찾아 문자열로 돌려준다. 비어 있으면 격자가 온전한 것.</summary>
        public List<string> Validate()
        {
            var holes = new List<string>();
            for (int tier = 1; tier <= WeaponFamilyInfo.MaxTier; tier++)
            {
                for (int f = 0; f < WeaponFamilyInfo.Count; f++)
                {
                    var family = (WeaponFamily)f;
                    if (Find(tier, family) == null)
                        holes.Add($"{WeaponFamilyInfo.TierName(tier)} · {WeaponFamilyInfo.LabelOf(family)}");
                }
            }
            return holes;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (weapons == null || weapons.Length == 0) return;
            var holes = Validate();
            if (holes.Count > 0)
                Debug.LogWarning($"[{name}] 격자에 빈 칸 {holes.Count}개: {string.Join(", ", holes)}", this);
        }
#endif
    }
}
