using System;

namespace MechaXCOM
{
    /// <summary>개별 패시브의 보정 제안. 최종 중첩과 전투 적용은 별도 책임이다.</summary>
    public readonly struct PilotAttackModifiers
    {
        private readonly float accuracyMultiplier;
        public int AttackAPReduction { get; }
        // default(struct)도 보정 없음으로 취급한다.
        public float AccuracyMultiplier => accuracyMultiplier == 0f ? 1f : accuracyMultiplier;
        public static PilotAttackModifiers None => default;

        public PilotAttackModifiers(int attackAPReduction = 0, float accuracyMultiplier = 1f)
        {
            if (attackAPReduction < 0) throw new ArgumentOutOfRangeException(nameof(attackAPReduction));
            if (float.IsNaN(accuracyMultiplier) || float.IsInfinity(accuracyMultiplier) || accuracyMultiplier <= 0f)
                throw new ArgumentOutOfRangeException(nameof(accuracyMultiplier));
            AttackAPReduction = attackAPReduction;
            this.accuracyMultiplier = accuracyMultiplier;
        }
    }
}
