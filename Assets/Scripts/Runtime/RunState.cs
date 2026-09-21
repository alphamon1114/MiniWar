using System;
using UnityEngine;

namespace MiniWar.Runtime
{
    public enum RunEndReason
    {
        None,
        HealthDepleted, // 체력 소진
        Bankrupt,       // 파산 — 전탄 0 + 최저 장전비 미만
        Cleared,        // 보스 격파
    }

    /// <summary>
    /// 한 판의 상태. 패배 조건이 둘(체력, 파산)이라는 것이 이 게임의 특징이므로
    /// 종료 사유를 명시적으로 들고 다닌다. 결과 화면에서 무엇 때문에 끝났는지 보여줘야
    /// 플레이어가 다음 판에 다른 선택을 한다.
    /// </summary>
    public sealed class RunState
    {
        readonly float _maxHealth;
        float _health;

        public event Action<float, float> HealthChanged; // (현재, 최대)
        public event Action<RunEndReason> Ended;

        public RunState(float maxHealth = 280f)
        {
            _maxHealth = Mathf.Max(1f, maxHealth);
            _health = _maxHealth;
        }

        public float Health => _health;
        public float MaxHealth => _maxHealth;
        public float HealthRatio => _health / _maxHealth;

        public int SegmentIndex { get; private set; } = 1;
        public float DistanceMeters { get; private set; }
        public RunEndReason EndReason { get; private set; } = RunEndReason.None;
        public bool IsOver => EndReason != RunEndReason.None;

        public void Advance(float meters)
        {
            if (IsOver || meters <= 0f) return;
            DistanceMeters += meters;
        }

        public void EnterSegment(int index, float healPerSegment)
        {
            if (IsOver) return;
            SegmentIndex = index;
            if (healPerSegment > 0f) Heal(healPerSegment);
        }

        public void TakeDamage(float amount)
        {
            if (IsOver || amount <= 0f) return;
            _health = Mathf.Max(0f, _health - amount);
            HealthChanged?.Invoke(_health, _maxHealth);
            if (_health <= 0f) End(RunEndReason.HealthDepleted);
        }

        public void Heal(float amount)
        {
            if (IsOver || amount <= 0f) return;
            _health = Mathf.Min(_maxHealth, _health + amount);
            HealthChanged?.Invoke(_health, _maxHealth);
        }

        public void End(RunEndReason reason)
        {
            if (IsOver || reason == RunEndReason.None) return;
            EndReason = reason;
            Ended?.Invoke(reason);
        }

        public static string LabelOf(RunEndReason reason) => reason switch
        {
            RunEndReason.HealthDepleted => "체력 소진",
            RunEndReason.Bankrupt => "파산 — 쏠 탄도 살 돈도 없다",
            RunEndReason.Cleared => "클리어",
            _ => "진행 중",
        };
    }
}
