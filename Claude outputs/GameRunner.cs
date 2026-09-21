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
        [SerializeField] float spawnX = 9f;
        [SerializeField] int maxAlive = 5;

        readonly List<EnemyHealth> _alive = new List<EnemyHealth>();
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
            if (enemyPrefab == null || spawnPool == null || spawnPool.Length == 0) return;

            var data = spawnPool[Random.Range(0, spawnPool.Length)];
            var go = Instantiate(enemyPrefab, new Vector3(spawnX, Random.Range(-1.4f, 0.6f), 0f), Quaternion.identity);
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
