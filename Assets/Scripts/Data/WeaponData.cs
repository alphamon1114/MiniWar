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
    /// 무기 칸. 칸마다 하나씩만 들고 나간다.
    ///
    /// 역할로 칸을 나누면 "무엇을 포기할 것인가"가 칸 안에서만 물어진다 —
    /// 주무기 셋 중 하나를 고르는 것은 화력의 성격을 정하는 일이고,
    /// 보조·근접·특수킷은 그 선택이 막히는 상황을 메우는 보험이다.
    /// </summary>
    public enum WeaponRole
    {
        /// <summary>주무기 — 화력의 중심. 연사·산탄·대전차 중 하나.</summary>
        Primary = 0,
        /// <summary>보조무기 — 싸고 약하다. 돈이 마르면 여기로 돌아온다.</summary>
        Sidearm = 1,
        /// <summary>근접무기 — 공짜다. 대신 맞을 각오를 해야 한다.</summary>
        Melee = 2,
        /// <summary>특수킷 — 쓸 때마다 돈. 능선 너머나 뭉친 적을 처리한다.</summary>
        Special = 3,
    }

    /// <summary>
    /// 무기 한 종의 기본 스펙. 강화는 런타임(WeaponInstance)에서 곱해지므로
    /// 이 에셋의 값은 항상 "강화 0회" 기준이다.
    /// </summary>
    [CreateAssetMenu(menuName = "무기전쟁/Weapon Data", fileName = "SO_Weapon_")]
    public sealed class WeaponData : ScriptableObject
    {
        [Header("식별")]
        public string displayName = "권총";

        [Tooltip("이 무기가 들어갈 칸. 숫자키 1~4와 그대로 대응한다.")]
        public WeaponRole role = WeaponRole.Sidearm;

        [Tooltip("HUD 왼쪽 아래에 뜨는 총기 그림. 씬 생성 시 자동으로 만들어 꽂는다.")]
        public Sprite icon;

        [Tooltip("마을 무기상에서 사는 값. 0이면 처음부터 가지고 있다(권총).")]
        [Min(0)] public int price;

        [Tooltip("무기상 목록에 뜨는 한 줄. 무엇에 쓰는 물건인지 말해준다.")]
        [TextArea(2, 3)] public string blurb = "";

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

        /// <summary>숫자키 슬롯. 칸(역할)에서 곧바로 나온다.</summary>
        public int slot => (int)role + 1;

        /// <summary>강화 0회 기준 발당 탄약비. 밸런싱 표와 대조할 때 쓴다.</summary>
        public float CostPerShotValue => magazineSize <= 0 ? 0f : (float)reloadCost / magazineSize;

        /// <summary>강화 0회 기준 초당 피해량(방어력 0 상대).</summary>
        public float RawDps => damagePerPellet * pellets * shotsPerSecond;

        public static string LabelOf(WeaponRole role)
        {
            switch (role)
            {
                case WeaponRole.Primary: return "주무기";
                case WeaponRole.Sidearm: return "보조무기";
                case WeaponRole.Melee: return "근접무기";
                case WeaponRole.Special: return "특수킷";
                default: return role.ToString();
            }
        }
    }
}
