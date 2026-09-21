using System.Collections.Generic;
using MiniWar.Combat;
using MiniWar.Data;
using UnityEngine;

namespace MiniWar.Runtime
{
    /// <summary>
    /// Day 2 테스트 러너. 조준·사격·명중·경제·파산이 실제로 맞물리는지 확인하는 것이 목적이다.
    /// 전진 시스템과 구간 전환(Day 4·6)은 아직 없고, 적을 같은 자리에 계속 다시 내보낸다.
    /// </summary>
    public sealed class GameRunner : MonoBehaviour
    {
        [Header("데이터")]
        [SerializeField] WeaponData[] weapons;
        [SerializeField] UpgradeTable upgradeTable;
        [SerializeField] SegmentData testSegment;
        [SerializeField] EnemyData[] spawnPool;

        [Header("씬 참조")]
        [SerializeField] GameObject enemyPrefab;
        [SerializeField] Transform player;
        [SerializeField] WeaponController weaponController;

        [Header("설정")]
        [SerializeField] int startingMoney = 300;
        [SerializeField] float maxHealth = 280f;
        [SerializeField] float spawnInterval = 2.5f;
        [Tooltip("플레이어보다 이만큼 앞에서 등장한다.")]
        [SerializeField] float spawnAhead = 13f;
        [SerializeField] int maxAlive = 5;

        readonly List<EnemyHealth> _alive = new List<EnemyHealth>();
        readonly List<EnemyData> _spawnTable = new List<EnemyData>();
        float _nextSpawn;

        public Loadout Loadout { get; private set; }
        public EconomySystem Economy { get; private set; }
        public RunState Run { get; private set; }
        public int Kills { get; private set; }
        public string LastNotice { get; private set; } = "";

        void Awake()
        {
            Loadout = new Loadout(weapons, upgradeTable);
            Economy = new EconomySystem(Loadout, startingMoney);
            Run = new RunState(maxHealth);

            Economy.Bankrupt += () =>
            {
                Run.End(RunEndReason.Bankrupt);
                LastNotice = "파산 — 쏠 탄도 살 돈도 없다";
            };
            Economy.DangerStateChanged += danger =>
            {
                if (danger) LastNotice = "잔액 위험 — 장전할 돈이 얼마 남지 않았다";
            };
            Run.Ended += reason => LastNotice = $"게임 오버 · {RunState.LabelOf(reason)}";

            if (weaponController != null) weaponController.Bind(Loadout, Economy);
            if (weaponController != null) weaponController.Notified += msg => LastNotice = msg;

            BuildSpawnTable();
        }

        /// <summary>
        /// 스폰 비율을 구간 에셋에서 가져온다. 균등 랜덤으로 뽑으면 장갑형이 1/3로 나와
        /// 설계(1구간 10마리 중 1마리)보다 훨씬 자주 등장하고, 체감 난이도가 완전히 달라진다.
        /// </summary>
        void BuildSpawnTable()
        {
            _spawnTable.Clear();

            if (testSegment != null && testSegment.spawns != null)
            {
                foreach (var entry in testSegment.spawns)
                {
                    if (entry.enemy == null || entry.enemy.isBoss) continue;
                    for (int i = 0; i < entry.count; i++) _spawnTable.Add(entry.enemy);
                }
            }

            if (_spawnTable.Count == 0 && spawnPool != null)
            {
                foreach (var e in spawnPool)
                    if (e != null && !e.isBoss) _spawnTable.Add(e);
            }
        }

        void Update()
        {
            if (Run.IsOver) return;

            _alive.RemoveAll(e => e == null || e.IsDead);

            if (Time.time >= _nextSpawn && _alive.Count < maxAlive)
            {
                _nextSpawn = Time.time + spawnInterval;
                SpawnOne();
            }
        }

        void SpawnOne()
        {
            if (enemyPrefab == null || _spawnTable.Count == 0) return;

            var data = _spawnTable[Random.Range(0, _spawnTable.Count)];

            // 플레이어 앞쪽 능선 위에서 등장한다. 비행형은 그 위 순항 고도.
            float x = (player != null ? player.position.x : 0f) + spawnAhead;
            var terrain = TerrainGenerator.Instance;
            float ground = terrain != null ? terrain.HeightAt(x) : -2.2f;

            float y = data.locomotion == LocomotionKind.Air
                ? ground + data.cruiseAltitude + Random.Range(-0.4f, 0.8f)
                : ground + data.bodySize.y * 0.5f;

            var go = Instantiate(enemyPrefab, new Vector3(x, y, 0f), Quaternion.identity);
            go.name = $"Enemy_{data.displayName}";

            var health = go.GetComponent<EnemyHealth>();
            health.Setup(data, testSegment);
            health.Died += OnEnemyDied;

            var view = go.GetComponent<EnemyView>();
            if (view != null) view.Bind(data, player, amount => Run.TakeDamage(amount));

            _alive.Add(health);
        }

        void OnEnemyDied(int reward, EnemyHealth enemy)
        {
            Kills++;
            Economy.AddReward(reward);
            LastNotice = $"{enemy.Data.displayName} 격파 +${reward}";
            if (enemy != null) Destroy(enemy.gameObject, 0.05f);
        }
    }
}
