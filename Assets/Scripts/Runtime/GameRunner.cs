using System.Collections.Generic;
using MiniWar.Combat;
using MiniWar.Data;
using UnityEngine;

namespace MiniWar.Runtime
{
    public enum StagePhase
    {
        Advancing,   // 구간 전진 중
        GateHeld,    // 관문 앞 — 남은 적을 정리해야 열린다
        BossFight,   // 보스방
        Finished,    // 결과 대기 — 곧 마을로
    }

    /// <summary>
    /// 던전 한 판. 빅샷 퀘스트모드의 구조를 따른다 —
    /// <b>구간 전진 → 관문(정리) → 다음 구간 → … → 보스방</b>.
    ///
    /// <b>던전 안에는 상점이 없다.</b> 엘소드·던파식 인던이라 준비는 전부 마을에서 끝내고 들어온다.
    /// 도중에 돈이 마르면 장전을 못 해 파산으로 실패한다 — 그게 이 구조의 긴장이다.
    ///
    /// 스폰은 시간이 아니라 <b>거리</b>로 건다(SegmentData.spawns의 distance).
    /// 시간으로 걸면 가만히 서 있는 플레이어에게 적이 무한정 오고, 이 게임에서
    /// 그건 곧 무한정 돈이다. 거리로 걸면 한 구간에서 벌 수 있는 총액이 스폰표로 정해진다.
    /// </summary>
    public sealed class GameRunner : MonoBehaviour
    {
        const float ReturnDelay = 2.6f;

        [Header("대체값 — GameSession이 없을 때만 쓴다")]
        [SerializeField] StageData fallbackStage;
        [SerializeField] WeaponData[] fallbackWeapons;
        [SerializeField] UpgradeTable fallbackTable;
        [SerializeField] GameSession sessionInScene;

        [Header("씬 참조")]
        [SerializeField] GameObject enemyPrefab;
        [SerializeField] GameObject bossPrefab;
        [SerializeField] PlayerMotor player;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] WeaponController weaponController;
        [SerializeField] HazardPool hazardPool;

        [Header("설정")]
        [SerializeField] int fallbackMoney = 300;
        [SerializeField] float fallbackHealth = 280f;
        [Tooltip("플레이어보다 이만큼 앞에서 등장한다.")]
        [SerializeField] float spawnAhead = 13f;
        [Tooltip("같은 스폰 항목의 적을 이 간격으로 흘려보낸다.")]
        [SerializeField] float spawnSpacing = 0.65f;

        readonly List<EnemyHealth> _alive = new List<EnemyHealth>();
        readonly List<int> _pending = new List<int>();   // 대기 중인 스폰표 인덱스

        GameSession _session;
        StageData _stage;
        float _startX;
        int _leg;
        int _nextSpawnEntry;
        float _nextSpawnTime;
        float _returnAt;
        bool _handedOff;
        BossController _boss;

        public Loadout Loadout { get; private set; }
        public EconomySystem Economy { get; private set; }
        public RunState Run { get; private set; }
        public StageData Stage => _stage;
        public StagePhase Phase { get; private set; } = StagePhase.Advancing;
        public BossController Boss => _boss;
        public int Kills { get; private set; }
        public int Earned { get; private set; }
        public int AliveCount => _alive.Count;
        public int PendingCount => _pending.Count;
        public string LastNotice { get; private set; } = "";

        /// <summary>던전 시작점 기준 전진 거리(m).</summary>
        public float Progress
            => player != null ? Mathf.Max(0f, player.transform.position.x - _startX) : 0f;

        public float StageLength => _stage != null ? _stage.TotalLength : 1f;

        public string LegLabel => _stage != null ? _stage.LabelOf(_leg) : "";

        public SegmentData CurrentSegment
            => _stage != null && _leg >= 0 && _leg < _stage.legs.Length
             ? _stage.legs[_leg].segment : null;

        void Awake()
        {
            _session = GameSession.Instance != null ? GameSession.Instance : sessionInScene;
            if (_session != null) _session.SelectFallbackDungeon();

            _stage = _session != null && _session.SelectedDungeon != null
                ? _session.SelectedDungeon
                : fallbackStage;

            var table = _session != null && _session.UpgradeTable != null
                ? _session.UpgradeTable : fallbackTable;

            int money = fallbackMoney;
            float maxHealth = fallbackHealth;

            if (_session != null)
            {
                // 마을에서 장착한 것만 들고 들어간다. 사놓고 안 끼운 무기는 여기 없다.
                Loadout = new Loadout(_session.Profile.EquippedList(), table);
                _session.Profile.Restore(Loadout);
                money = _session.Profile.Money;
                maxHealth = _session.MaxHealth;
            }
            else
            {
                Loadout = new Loadout(fallbackWeapons, table);
            }

            Economy = new EconomySystem(Loadout, money);
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
            Run.Ended += OnRunEnded;

            if (weaponController != null)
            {
                weaponController.Bind(Loadout, Economy);
                weaponController.Notified += msg => LastNotice = msg;
            }
        }

        void Start()
        {
            _startX = player != null ? player.transform.position.x : 0f;

            // 보스방을 평지로 깎아둔다. 청크가 만들어지기 전이어야 한다.
            // 보스가 없는 훈련 스테이지에는 보스방이 없으므로 능선을 그대로 둔다.
            if (_stage != null && _stage.HasBoss && TerrainGenerator.Instance != null)
            {
                float arenaStart = _startX + _stage.ArenaStart;
                TerrainGenerator.Instance.SetFlatZone(
                    arenaStart, arenaStart + _stage.arenaLength, _stage.arenaFloorHeight);
            }

            EnterLeg(0);
        }

        void Update()
        {
            if (Run.IsOver) { TickReturn(); return; }

            _alive.RemoveAll(e => e == null || e.IsDead);

            switch (Phase)
            {
                case StagePhase.Advancing: TickAdvancing(); break;
                case StagePhase.GateHeld: TickGate(); break;
                case StagePhase.BossFight: TickBoss(); break;
            }
        }

        // ── 구간 전진 ────────────────────────────────────────────

        void EnterLeg(int index)
        {
            _leg = index;
            _nextSpawnEntry = 0;
            _pending.Clear();
            Phase = StagePhase.Advancing;

            if (player != null)
            {
                player.HardMinX = float.NegativeInfinity;
                player.MaxX = float.PositiveInfinity;
            }
            if (cameraRig != null) cameraRig.Unlock();

            if (_stage == null) return;
            if (index >= _stage.legs.Length) { EnterArena(); return; }

            Run.EnterSegment(index + 1, 0f);
            LastNotice = $"{_stage.LabelOf(index)} 진입";
        }

        void TickAdvancing()
        {
            var segment = CurrentSegment;
            if (segment == null) { EnterLeg(_leg + 1); return; }

            float legStart = _stage.LegStart(_leg);
            float length = _stage.LengthOf(_leg);
            float into = Progress - legStart;

            ReleaseSpawns(segment, into);
            SpawnDue(segment);

            if (into < length - 1.5f) return;

            // 관문 도달 — 여기서부터는 정리하기 전에 못 지나간다.
            Phase = StagePhase.GateHeld;
            if (player != null) player.MaxX = _startX + legStart + length - 1.5f;
        }

        /// <summary>거리를 넘긴 스폰 항목을 대기열로 옮긴다.</summary>
        void ReleaseSpawns(SegmentData segment, float into)
        {
            if (segment.spawns == null) return;

            while (_nextSpawnEntry < segment.spawns.Length
                   && segment.spawns[_nextSpawnEntry].distance <= into)
            {
                var entry = segment.spawns[_nextSpawnEntry];
                if (entry.enemy != null && !entry.enemy.isBoss)
                {
                    for (int i = 0; i < entry.count; i++) _pending.Add(_nextSpawnEntry);
                }
                _nextSpawnEntry++;
            }
        }

        void SpawnDue(SegmentData segment)
        {
            if (_pending.Count == 0 || Time.time < _nextSpawnTime) return;

            // 한 항목이 2~3마리면 한꺼번에 쏟아지지 않게 조금씩 흘려보낸다.
            _nextSpawnTime = Time.time + Mathf.Max(0.1f, spawnSpacing);

            int entryIndex = _pending[0];
            _pending.RemoveAt(0);

            if (segment.spawns == null || entryIndex < 0 || entryIndex >= segment.spawns.Length) return;
            Spawn(segment.spawns[entryIndex].enemy, segment);
        }

        void Spawn(EnemyData data, SegmentData segment)
        {
            if (data == null || enemyPrefab == null || player == null) return;

            float x = player.transform.position.x + spawnAhead;
            var terrain = TerrainGenerator.Instance;
            float ground = terrain != null ? terrain.HeightAt(x) : -2.2f;

            float y = data.locomotion == LocomotionKind.Air
                ? ground + data.cruiseAltitude + Random.Range(-0.4f, 0.8f)
                : ground + data.bodySize.y * 0.5f;

            var go = Instantiate(enemyPrefab, new Vector3(x, y, 0f), Quaternion.identity);
            go.name = $"Enemy_{data.displayName}";

            var health = go.GetComponent<EnemyHealth>();
            health.Setup(data, segment);
            health.Died += OnEnemyDied;

            var view = go.GetComponent<EnemyView>();
            if (view != null) view.Bind(data, player.transform, amount => Run.TakeDamage(amount));

            _alive.Add(health);
        }

        // ── 관문 ─────────────────────────────────────────────────

        void TickGate()
        {
            var segment = CurrentSegment;
            if (segment != null)
            {
                // 남은 스폰은 관문 앞에서 마저 내보낸다 — 여기서 끊으면 스폰표가 거짓말이 된다.
                ReleaseSpawns(segment, float.MaxValue);
                SpawnDue(segment);
            }

            if (_pending.Count > 0 || _alive.Count > 0) return;

            // 상점은 마을에만 있다. 관문은 숨 돌릴 회복과 통과만 준다.
            float heal = _stage != null ? _stage.healPerCheckpoint : 0f;
            Run.Heal(heal);
            LastNotice = heal > 0f ? $"관문 통과 — 체력 +{heal:F0}" : "관문 통과";

            EnterLeg(_leg + 1);
        }

        // ── 보스방 ───────────────────────────────────────────────

        void EnterArena()
        {
            if (_stage == null) { Finish(false); return; }

            // 보스가 없는 스테이지는 마지막 관문을 지난 것이 곧 클리어다.
            if (!_stage.HasBoss) { Finish(true); return; }

            Phase = StagePhase.BossFight;

            float arenaStart = _startX + _stage.ArenaStart;
            float arenaEnd = arenaStart + _stage.arenaLength;

            // 무대를 닫는다. 앞으로도 뒤로도 못 나간다 — 여기서부터는 이기거나 죽거나다.
            if (player != null)
            {
                player.HardMinX = arenaStart + 1f;
                player.MaxX = arenaEnd - 2f;
            }

            if (cameraRig != null)
                cameraRig.LockAt(arenaStart + _stage.arenaLength * 0.5f);

            SpawnBoss(arenaEnd - 5f);
            LastNotice = $"{_stage.boss.displayName} 출현";
        }

        void SpawnBoss(float x)
        {
            if (bossPrefab == null || player == null) return;

            var terrain = TerrainGenerator.Instance;
            float ground = terrain != null ? terrain.HeightAt(x) : -2.2f;

            float spawnY = _stage.boss.hoverHeight > 0f
                ? ground + _stage.boss.hoverHeight
                : ground + _stage.boss.bodySize.y * 0.5f;

            var go = Instantiate(bossPrefab, new Vector3(x, spawnY, 0f), Quaternion.identity);
            go.name = $"Boss_{_stage.boss.displayName}";

            var health = go.GetComponent<EnemyHealth>();
            if (health != null) health.Died += OnBossDied;

            _boss = go.GetComponent<BossController>();
            if (_boss == null) return;

            _boss.Notified += msg => LastNotice = msg;
            _boss.Bind(_stage.boss, player.transform, amount => Run.TakeDamage(amount), hazardPool,
                       _stage.bossHealthMultiplier);
        }

        void TickBoss()
        {
            // 보스가 사라졌는데 판이 안 끝났다면(프리팹 누락 등) 여기서 멈춘다.
            if (_boss == null) Finish(false);
        }

        void OnBossDied(int reward, EnemyHealth boss)
        {
            Kills++;
            if (reward > 0) { Economy.AddReward(reward); Earned += reward; }

            LastNotice = $"{boss.Label} 격파 — 던전 클리어";
            if (boss != null) Destroy(boss.gameObject, 0.4f);
            _boss = null;

            Finish(true);
        }

        // ── 종료 · 귀환 ──────────────────────────────────────────

        void Finish(bool cleared)
        {
            if (Run.IsOver) return;
            Phase = StagePhase.Finished;
            Run.End(cleared ? RunEndReason.Cleared : RunEndReason.HealthDepleted);
        }

        void OnRunEnded(RunEndReason reason)
        {
            Phase = StagePhase.Finished;
            LastNotice = $"{RunState.LabelOf(reason)} — 기지로 귀환한다";
            _returnAt = Time.time + ReturnDelay;

            if (player != null) player.InputLocked = true;
            if (weaponController != null) weaponController.InputLocked = true;
        }

        /// <summary>결과 화면을 잠깐 보여준 뒤 마을로 넘긴다.</summary>
        void TickReturn()
        {
            if (_handedOff || Time.time < _returnAt) return;
            _handedOff = true;

            if (_session == null) return;     // 던전 씬 단독 실행 — 그냥 멈춘다

            bool cleared = Run.EndReason == RunEndReason.Cleared;
            bool firstClear = cleared && _stage != null && !_session.Profile.IsCleared(_stage);

            int bonus = 0;
            if (cleared && _stage != null)
            {
                bonus = _stage.clearReward + (firstClear ? _stage.firstClearBonus : 0);
                Economy.AddReward(bonus);
            }

            // 실패해도 던전에서 번 돈과 남은 탄은 그대로 가져간다 —
            // 전부 날리면 한 번 실패한 플레이어가 영영 못 올라온다.
            _session.Profile.SetMoney(Economy.Money);
            _session.Profile.Capture(Loadout);

            _session.FinishDungeon(new DungeonResult
            {
                stage = _stage,
                cleared = cleared,
                reason = Run.EndReason,
                earnedInDungeon = Earned,
                clearReward = bonus,
                firstClear = firstClear,
                kills = Kills,
            });
        }

        // ── 공통 ─────────────────────────────────────────────────

        void OnEnemyDied(int reward, EnemyHealth enemy)
        {
            Kills++;
            Economy.AddReward(reward);
            Earned += reward;
            LastNotice = $"{enemy.Label} 격파 +${reward}";
            if (enemy != null) Destroy(enemy.gameObject, 0.05f);
        }
    }
}
