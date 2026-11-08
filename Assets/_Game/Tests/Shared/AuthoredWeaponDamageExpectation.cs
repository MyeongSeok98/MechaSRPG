using System;

namespace MechaXCOM.Tests
{
    // Independent arithmetic for scenes whose user-authored damage percentages may change.
    // Never call the production damage or armor math to build the test's expected value.
    public static class AuthoredWeaponDamageExpectation
    {
        public static int Normal(BattleUnit target, WeaponData weapon, int raw)
        {
            decimal multiplier = 1m;
            if (weapon.AttackType == AttackType.Ranged)
            {
                if (weapon.RangedCategory == RangedWeaponCategory.Beam) multiplier = target.BeamDamageTakenPercent / 100m;
                else if (weapon.RangedCategory == RangedWeaponCategory.Ballistic) multiplier = target.BallisticDamageTakenPercent / 100m;
            }
            return (int)Math.Min(int.MaxValue, Math.Round(raw * multiplier, 0, MidpointRounding.AwayFromZero));
        }
    }
}
