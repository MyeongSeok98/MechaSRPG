using System;
using System.Collections.Generic;

namespace MechaXCOM.Tests
{
    // Independent specification table for scenes whose user-authored armor may change.
    // Never call the production damage or armor math to build the test's expected value.
    public static class AuthoredWeaponDamageExpectation
    {
        private static readonly Dictionary<(ArmorType, RangedWeaponCategory), decimal> Table =
            new Dictionary<(ArmorType, RangedWeaponCategory), decimal>
            {
                { (ArmorType.BeamResistant, RangedWeaponCategory.Beam), .8m },
                { (ArmorType.BeamResistant, RangedWeaponCategory.Ballistic), 1.1m },
                { (ArmorType.BallisticResistant, RangedWeaponCategory.Beam), 1.1m },
                { (ArmorType.BallisticResistant, RangedWeaponCategory.Ballistic), .8m }
            };
        public static int Normal(BattleUnit target, WeaponData weapon, int raw)
        {
            decimal multiplier = 1m;
            if (weapon.AttackType == AttackType.Ranged && Table.TryGetValue((target.ArmorType, weapon.RangedCategory), out var value))
                multiplier = value;
            return (int)Math.Min(int.MaxValue, Math.Round(raw * multiplier, 0, MidpointRounding.AwayFromZero));
        }
    }
}
