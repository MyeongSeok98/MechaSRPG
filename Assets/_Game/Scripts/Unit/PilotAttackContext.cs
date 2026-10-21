using System;

namespace MechaXCOM
{
    /// <summary>호출자가 확정한 공격 상황. 유닛과 공유 데이터는 조회에만 사용한다.</summary>
    public sealed class PilotAttackContext
    {
        public BattleUnit Attacker { get; }
        public BattleUnit Target { get; }
        public bool HasTarget => Target;
        public WeaponData Weapon { get; }
        public AttackType AttackType => Weapon.AttackType;
        public GripMode Grip { get; }
        public TargetBodyPart BodyPart { get; }
        public long DistanceInCells { get; }
        public int MovedCells { get; }

        // 대상 선택 전에는 target=null, distanceInCells=0, bodyPart=Whole을 사용한다.
        // 대상이 필요한 패시브는 CanApply에서 HasTarget을 확인해야 한다.
        public PilotAttackContext(WeaponData weapon, int movedCells,
            long distanceInCells = 0, BattleUnit attacker = null, BattleUnit target = null,
            TargetBodyPart bodyPart = TargetBodyPart.Whole, GripMode grip = GripMode.OneHand)
        {
            if (!weapon) throw new ArgumentNullException(nameof(weapon));
            if (movedCells < 0) throw new ArgumentOutOfRangeException(nameof(movedCells));
            if (distanceInCells < 0) throw new ArgumentOutOfRangeException(nameof(distanceInCells));
            if (!Enum.IsDefined(typeof(TargetBodyPart), bodyPart)) throw new ArgumentOutOfRangeException(nameof(bodyPart));
            if (!Enum.IsDefined(typeof(GripMode), grip)) throw new ArgumentOutOfRangeException(nameof(grip));
            Weapon = weapon;
            MovedCells = movedCells;
            DistanceInCells = distanceInCells;
            Attacker = attacker;
            Target = target;
            BodyPart = bodyPart;
            Grip = grip;
        }
    }
}
