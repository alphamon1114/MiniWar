using MiniWar.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniWar.Runtime
{
    /// <summary>던전에서 나온 직후의 결과. 마을 화면이 이걸 읽어 보고한다.</summary>
    public struct DungeonResult
    {
        public StageData stage;
        public bool cleared;
        public RunEndReason reason;
        public int earnedInDungeon;   // 격파 보상 합
        public int clearReward;       // 클리어 보상 + 최초 보너스
        public bool firstClear;
        public int kills;
        public bool valid;
    }

    /// <summary>
    /// 씬을 넘어 살아남는 한 개. 마을 ↔ 던전을 오가는 동안 프로필과 선택 상태를 들고 있다.
    ///
    /// 두 씬 모두에 하나씩 놓여 있고, 나중에 로드된 쪽이 스스로 사라진다.
    /// 어느 씬에서 Play를 눌러도 게임이 성립하게 하려는 것 —
    /// 프로토타입 단계에서 "던전 씬에서 바로 실행하면 아무것도 안 된다"는
    /// 상황이 생기면 테스트 속도가 확 떨어진다.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        public const string TownScene = "Town";
        public const string DungeonScene = "Dungeon";

        public static GameSession Instance { get; private set; }

        [Header("데이터")]
        [SerializeField] DungeonCatalog catalog;

        [Tooltip("게임에 존재하는 모든 무기. 마을 무기상의 목록이 된다. "
               + "값이 0인 무기(권총)만 처음부터 주어진다.")]
        [SerializeField] WeaponData[] allWeapons;

        [SerializeField] UpgradeTable upgradeTable;

        [Header("설정")]
        [SerializeField] int startingMoney = 300;
        [SerializeField] float maxHealth = 280f;

        PlayerProfile _profile;

        /// <summary>
        /// 지연 생성. GameObject 사이의 Awake 순서는 보장되지 않으므로,
        /// GameRunner가 먼저 깨어나도 프로필이 비어 있지 않게 해야 한다.
        /// </summary>
        public PlayerProfile Profile
        {
            get
            {
                if (_profile != null) return _profile;

                _profile = new PlayerProfile(startingMoney);
                _profile.GrantStarterWeapons(allWeapons);   // 시작은 권총 한 자루
                return _profile;
            }
        }

        public DungeonCatalog Catalog => catalog;

        /// <summary>무기상 목록. 보유 여부와는 별개다.</summary>
        public WeaponData[] AllWeapons => allWeapons;

        public UpgradeTable UpgradeTable => upgradeTable;
        public float MaxHealth => maxHealth;

        /// <summary>지금 들어갈(또는 들어가 있는) 던전.</summary>
        public StageData SelectedDungeon { get; private set; }

        public DungeonResult LastResult { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 해금 ─────────────────────────────────────────────────

        /// <summary>앞 던전을 깨야 다음이 열린다. 첫 번째는 항상 열려 있다.</summary>
        public bool IsUnlocked(int index)
        {
            if (catalog == null || index < 0 || index >= catalog.Count) return false;
            if (index == 0) return true;
            return Profile.IsCleared(catalog.At(index - 1));
        }

        // ── 씬 이동 ──────────────────────────────────────────────

        public void EnterDungeon(StageData stage)
        {
            if (stage == null) return;
            SelectedDungeon = stage;
            Profile.CountRun();
            SceneManager.LoadScene(DungeonScene);
        }

        /// <summary>던전이 끝났다. 결과를 담아 마을로 돌려보낸다.</summary>
        public void FinishDungeon(DungeonResult result)
        {
            result.valid = true;
            LastResult = result;

            if (result.cleared && result.stage != null) Profile.MarkCleared(result.stage);

            SceneManager.LoadScene(TownScene);
        }

        public void ClearResult() => LastResult = new DungeonResult();

        /// <summary>던전 씬에서 바로 Play를 눌렀을 때의 보조 수단.</summary>
        public void SelectFallbackDungeon()
        {
            if (SelectedDungeon == null && catalog != null && catalog.Count > 0)
                SelectedDungeon = catalog.At(0);
        }
    }
}
