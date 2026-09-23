using UnityEngine;

namespace MiniWar.Data
{
    /// <summary>탄약·비용 방식.</summary>
    public enum AmmoMode
    {
        /// <summary>탄창 단위. 장전할 때 reloadCost만큼 나간다.</summary>
        Magazine = 0,
        /// <summary>탄창 없음. 쓸 때마다 reloadCost만큼 즉시 나간다.</summary>
        PerThrow = 1,
        /// <summary>탄약도 돈도 들지 않는다. 대신 적에게 붙어야 한다(근접).</summary>
        Melee = 2,
    }

    /// <summary>
    /// 무기 한 종의 기본 스펙. 강화는 런타임(WeaponInstance)에서 곱해지므로
    /// 이 에셋의 값은 항상 <b>+0 기준</b>이다.
    ///
    /// 무기는 사는 것이 아니라 던전에서 떨어지고, 셋씩 합쳐 등급을 올린다.
    /// 그래서 이 에셋에 가격이 없고 대신 <see cref="tier"/>와 <see cref="family"/>가 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "무기전쟁/Weapon Data", fileName = "SO_Weapon_")]
    public sealed class WeaponData : ScriptableObject
    {
        [Header("식별")]
        public string displayName = "권총";

        [Tooltip("등급 1~3. 같은 등급 셋을 합치면 다음 등급이 된다.")]
        [Range(1, WeaponFamilyInfo.MaxTier)] public int tier = 1;

        [Tooltip("계열 — 어떻게 쏘는가. 등급이 올라도 이건 변하지 않는다. "
               + "같은 계열 셋을 합치면 다음 등급의 같은 계열이 나온다.")]
        public WeaponFamily family = WeaponFamily.Single;

        [Tooltip("HUD 왼쪽 아래에 뜨는 총기 그림. 씬 생성 시 자동으로 만들어 꽂는다.")]
        public Sprite icon;

        [Tooltip("Magazine: 탄창을 채울 때 돈이 나간다(총기). "
               + "PerThrow: 탄창 없이 쓸 때마다 돈이 나간다(수류탄 등 특수킷).")]
        public AmmoMode ammoMode = AmmoMode.Magazine;

        [Header("화력")]
        [Tooltip("펠릿 하나당 위력. 방어력은 펠릿마다 감산된다.")]
        [Min(0f)] public float damagePerPellet = 25f;

        [Tooltip("1발에 나가는 펠릿 수. 산탄형만 2 이상.")]
        [Min(1)] public int pellets = 1;

        [Tooltip("켜면 방어력을 무시한다. 고위력형 전용.")]
        public bool piercing;

        [Header("탄약과 비용")]
        [Min(1)] public int magazineSize = 12;

        [Tooltip("탄창 하나를 채우는 비용. 권총을 포함해 모든 총기가 유료다.")]
        [Min(0)] public int reloadCost = 9;

        [Header("속도")]
        [Min(0.01f)] public float shotsPerSecond = 2f;
        [Min(0.01f)] public float reloadSeconds = 1.2f;

        [Header("탄도")]
        [Tooltip("탄이 날아가는 속도(유닛/초). 조준점은 방향만 정하고, 탄은 그 선을 따라 계속 날아간다. "
               + "느릴수록 움직이는 적을 예측해서 쏴야 한다.")]
        [Min(1f)] public float projectileSpeed = 22f;

        [Tooltip("퍼짐 각도(도). 펠릿마다 이 범위 안에서 흩어진다. 산탄형만 크게 준다.")]
        [Min(0f)] public float spreadDegrees = 1f;

        [Tooltip("탄이 날아갈 수 있는 최대 거리(유닛). 넘으면 사라진다.")]
        [Min(1f)] public float projectileRange = 30f;

        [Tooltip("탄의 겉크기. 대전차 유탄처럼 무겁고 느린 탄은 크게 그려 한눈에 구분되게 한다.")]
        public Vector2 projectileScale = new Vector2(0.28f, 0.06f);

        [Header("곡사와 폭발 (수류탄류)")]
        [Tooltip("켜면 포물선으로 던진다. 이때 조준점은 방향이 아니라 <b>착탄 지점</b>이 된다.")]
        public bool ballistic;

        [Tooltip("곡사일 때 던져서 떨어질 때까지 걸리는 시간(초). 짧을수록 낮고 빠르게 날아간다.")]
        [Min(0.1f)] public float flightTime = 0.85f;

        [Tooltip("폭발 반경(유닛). 0이면 단일 타격이다.")]
        [Min(0f)] public float blastRadius;

        [Tooltip("폭발 가장자리에서 남는 피해 비율. 중심은 100%, 가장자리는 이 값.")]
        [Range(0f, 1f)] public float blastFalloff = 0.4f;

        [Header("근접")]
        [Tooltip("근접 공격이 닿는 거리(유닛). ammoMode가 Melee일 때만 쓴다.")]
        [Min(0.2f)] public float meleeRange = 1.4f;

        /// <summary>+0 기준 발당 탄약비. 밸런싱 표와 대조할 때 쓴다.</summary>
        public float CostPerShotValue => magazineSize <= 0 ? 0f : (float)reloadCost / magazineSize;

        /// <summary>+0 기준 초당 피해량(방어력 0 상대).</summary>
        public float RawDps => damagePerPellet * pellets * shotsPerSecond;

        /// <summary>목록에 뜨는 이름. "제식 · 돌격소총" 꼴.</summary>
        public string FullName =>
            $"{WeaponFamilyInfo.TierName(tier)} · {displayName}";

        public string FamilyLabel => WeaponFamilyInfo.LabelOf(family);
    }
}
