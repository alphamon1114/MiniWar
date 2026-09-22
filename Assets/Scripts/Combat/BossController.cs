using System;
using MiniWar.Data;
using MiniWar.Runtime;
using UnityEngine;

namespace MiniWar.Combat
{
    /// <summary>
    /// 다단 페이즈 보스. 체력이 구간을 넘을 때마다 형태가 바뀐다 —
    /// 장갑, 속도, 패턴이 한꺼번에 갈아치워진다.
    ///
    /// 장갑이 페이즈마다 바뀌는 것이 설계의 축이다. 한 무기만 밀어준 플레이어는
    /// 어느 한 페이즈에서 반드시 손해를 본다. 무기전쟁의 강화가 "무기 단위"라는
    /// 규칙이 보스전에서 비용으로 돌아오는 유일한 지점이기도 하다.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class BossController : MonoBehaviour
    {
        const float TransitionSeconds = 1.1f;

        [SerializeField] SpriteRenderer body;
        [SerializeField] Transform muzzle;

        EnemyHealth _health;
        BossData _data;
        Transform _player;
        Action<float> _damagePlayer;
        HazardPool _hazards;

        int _phase = -1;
        float _nextAttack;
        float _transitionUntil;
        float _cannonFireAt;         // 조준선을 보여준 뒤 실제로 쏘는 시각
        Vector2 _cannonAim;
        LineRenderer _aimLine;

        public event Action<string> Notified;

        public int PhaseIndex => _phase;
        public string PhaseLabel => Valid(_phase) ? _data.phases[_phase].label : "";
        public int PhaseCount => _data != null && _data.phases != null ? _data.phases.Length : 0;
        public float HealthRatio => _health != null ? _health.HealthRatio : 0f;
        public string DisplayName => _data != null ? _data.displayName : "";

        void Awake() => _health = GetComponent<EnemyHealth>();

        public void Bind(BossData data, Transform player, Action<float> damagePlayer, HazardPool hazards,
                         float healthMultiplier = 1f)
        {
            _data = data;
            _player = player;
            _damagePlayer = damagePlayer;
            _hazards = hazards;

            // 같은 보스를 뒤 던전에서 다시 쓸 때는 체력만 올린다.
            // 패턴은 그대로라 플레이어가 배운 것이 헛되지 않고, 대신 준비가 더 필요해진다.
            _health.SetupDirect(data.displayName, data.totalHealth * Mathf.Max(0.1f, healthMultiplier),
                                data.phases.Length > 0 ? data.phases[0].armor : 0f, data.reward);

            if (body != null)
                body.transform.localScale = new Vector3(data.bodySize.x, data.bodySize.y, 1f);

            SetupAimLine();
            EnterPhase(0);
        }

        void SetupAimLine()
        {
            _aimLine = gameObject.AddComponent<LineRenderer>();
            _aimLine.useWorldSpace = true;
            _aimLine.positionCount = 2;
            _aimLine.startWidth = 0.06f;
            _aimLine.endWidth = 0.06f;
            _aimLine.sortingOrder = 50;

            var shader = Shader.Find("Sprites/Default");
            if (shader != null) _aimLine.material = new Material(shader);
            _aimLine.startColor = new Color(0.6f, 0.15f, 0.15f, 0.75f);
            _aimLine.endColor = new Color(0.6f, 0.15f, 0.15f, 0.15f);
            _aimLine.enabled = false;
        }

        bool Valid(int i) => _data != null && _data.phases != null && i >= 0 && i < _data.phases.Length;

        void Update()
        {
            if (_data == null || _health == null || _health.IsDead || _player == null) return;

            // 페이즈 경계를 넘었나
            int target = PhaseFor(_health.HealthRatio);
            if (target != _phase) { EnterPhase(target); return; }

            if (Time.time < _transitionUntil) { Flicker(); return; }
            if (_health.Invulnerable) EndTransition();

            Approach();
            TickCannon();

            if (Time.time >= _nextAttack) Attack();

            TouchDamage();
        }

        /// <summary>남은 체력 비율이 어느 페이즈에 해당하는가.</summary>
        int PhaseFor(float ratio)
        {
            for (int i = 0; i < _data.phases.Length; i++)
            {
                if (ratio > _data.PhaseFloor(i) + 0.0001f) return i;
            }
            return _data.phases.Length - 1;
        }

        void EnterPhase(int index)
        {
            if (!Valid(index)) return;

            _phase = index;
            var p = _data.phases[index];

            _health.SetArmor(p.armor);
            _health.Invulnerable = true;
            _transitionUntil = Time.time + TransitionSeconds;
            _nextAttack = _transitionUntil + 0.4f;
            _cannonFireAt = 0f;
            if (_aimLine != null) _aimLine.enabled = false;

            if (body != null) body.color = p.tint;

            string notice = string.IsNullOrEmpty(p.enterNotice)
                ? $"{_data.displayName} — {p.label}"
                : p.enterNotice;
            Notified?.Invoke($"{notice}  (장갑 {p.armor:F0})");
        }

        /// <summary>전환 중에는 깜빡이며 무적이다. 형태가 바뀌는 중이라는 신호.</summary>
        void Flicker()
        {
            if (body == null) return;
            var c = body.color;
            c.a = Mathf.PingPong(Time.time * 8f, 1f) * 0.6f + 0.4f;
            body.color = c;
        }

        /// <summary>전환이 끝나면 불투명도를 되돌리고 다시 맞기 시작한다.</summary>
        void EndTransition()
        {
            if (body != null)
            {
                var c = body.color;
                c.a = 1f;
                body.color = c;
            }
            _health.Invulnerable = false;
        }

        void Approach()
        {
            var p = _data.phases[_phase];
            if (p.moveSpeed <= 0f) { Snap(); return; }

            float gap = transform.position.x - _player.position.x;
            if (gap <= _data.standoffDistance) { Snap(); return; }

            var pos = transform.position;
            pos.x -= p.moveSpeed * Time.deltaTime;
            transform.position = pos;
            Snap();
        }

        /// <summary>
        /// 지상 보스는 능선 위에 서고, 비행 보스는 지면 위 hoverHeight에 뜬다.
        /// 뜬 보스에게는 곡사(수류탄)가 닿지 않으므로 무기 선택이 통째로 달라진다.
        /// </summary>
        void Snap()
        {
            var terrain = TerrainGenerator.Instance;
            if (terrain == null) return;

            var pos = transform.position;
            float ground = terrain.HeightAt(pos.x);
            float target = _data.hoverHeight > 0f
                ? ground + _data.hoverHeight
                : ground + _data.bodySize.y * 0.5f;

            pos.y = Mathf.Lerp(pos.y, target, Time.deltaTime * 8f);
            transform.position = pos;
        }

        void Attack()
        {
            var p = _data.phases[_phase];
            if (p.attacks == null || p.attacks.Length == 0)
            {
                _nextAttack = Time.time + 1.5f;
                return;
            }

            _nextAttack = Time.time + Mathf.Max(0.3f, p.attackInterval);

            switch (p.attacks[UnityEngine.Random.Range(0, p.attacks.Length)])
            {
                case BossAttack.Cannon: BeginCannon(p); break;
                case BossAttack.Mortar: FireMortar(p); break;
                case BossAttack.Barrage: FireBarrage(p); break;
            }
        }

        // ── 주포 ── 조준선을 먼저 보여준다. 예고 없는 직사는 피할 방법이 없다.

        void BeginCannon(BossPhase p)
        {
            _cannonAim = _player.position;
            _cannonFireAt = Time.time + 0.8f;

            if (_aimLine == null) return;
            _aimLine.enabled = true;
            _aimLine.SetPosition(0, Origin());
            _aimLine.SetPosition(1, _cannonAim);
        }

        void TickCannon()
        {
            if (_cannonFireAt <= 0f) return;

            if (Time.time < _cannonFireAt)
            {
                if (_aimLine != null) _aimLine.SetPosition(0, Origin());
                return;
            }

            _cannonFireAt = 0f;
            if (_aimLine != null) _aimLine.enabled = false;

            var shell = _hazards != null ? _hazards.Get() : null;
            if (shell == null) return;

            Vector2 origin = Origin();
            shell.LaunchDirect(origin, _cannonAim - origin, 26f,
                               _data.phases[_phase].shellDamage, _player, _damagePlayer);
        }

        // ── 곡사 ── 능선이 막아주지 않는다. 숨는 게 아니라 움직여야 한다.

        void FireMortar(BossPhase p)
        {
            if (_hazards == null) return;

            for (int i = 0; i < 3; i++)
            {
                var shell = _hazards.Get();
                if (shell == null) return;

                float offset = (i - 1) * 2.6f + UnityEngine.Random.Range(-0.6f, 0.6f);
                Vector2 target = new Vector2(_player.position.x + offset, _player.position.y);

                shell.LaunchMortar(Origin(), target, 1.25f + i * 0.18f,
                                   p.shellDamage * 0.8f, 1.8f, _player, _damagePlayer);
            }
        }

        // ── 탄막 ── 느린 포탄을 부채꼴로. 틈으로 걸어 들어가야 한다.

        void FireBarrage(BossPhase p)
        {
            if (_hazards == null) return;

            Vector2 origin = Origin();
            Vector2 toPlayer = ((Vector2)_player.position - origin).normalized;

            for (int i = -2; i <= 2; i++)
            {
                var shell = _hazards.Get();
                if (shell == null) return;

                shell.LaunchDirect(origin, Rotate(toPlayer, i * 9f), 11f,
                                   p.shellDamage * 0.55f, _player, _damagePlayer);
            }
        }

        void TouchDamage()
        {
            if (_data.contactDamagePerSecond <= 0f) return;

            float dx = Mathf.Abs(transform.position.x - _player.position.x);
            if (dx > _data.bodySize.x * 0.5f + 0.4f) return;

            _damagePlayer?.Invoke(_data.contactDamagePerSecond * Time.deltaTime);
        }

        Vector2 Origin()
            => muzzle != null ? (Vector2)muzzle.position : (Vector2)transform.position;

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }
    }
}
