using UnityEngine;

namespace MechaXCOM.Tests
{
    // Test fixtures use actual WeaponData, without exposing runtime combat setters.
    public static class TestWeaponFactory
    {
        public static BattleUnit AddTestBattleUnit(this GameObject root)
        {
            var unit = root.AddComponent<BattleUnit>();
            var combat = root.GetComponent<UnitCombat>();
            combat.ConfigureTestRanged(5, 80, 7, 3); combat.ConfigureTestMelee(8, 95, 2, 3);
            return unit;
        }
        public static UnitWeaponLoadout EnsureTestLoadout(this GameObject root) =>
            root.GetComponent<UnitWeaponLoadout>() ?? root.AddComponent<UnitWeaponLoadout>();
        public static void ConfigureTestRanged(this UnitCombat combat, int damage, int accuracy, int range, int cost) =>
            Set(combat, false, damage, accuracy, range, cost);
        public static void ConfigureTestMelee(this UnitCombat combat, int damage, int accuracy, int range, int cost) =>
            Set(combat, true, damage, accuracy, range, cost);
        private static void Set(UnitCombat combat, bool melee, int damage, int accuracy, int range, int cost)
        {
            WeaponData weapon = null;
            if (damage > 0 && range > 0)
            {
                weapon = ScriptableObject.CreateInstance<WeaponData>();
                JsonUtility.FromJsonOverwrite($"{{\"displayName\":\"Test {(melee ? "Melee" : "Ranged")}\",\"minDamage\":{damage},\"maxDamage\":{damage},\"baseHitChance\":{Mathf.Clamp(accuracy, 0, 100)},\"range\":{range},\"apCost\":{Mathf.Max(1, cost)},\"attackType\":{(melee ? 1 : 0)},\"gripRequirement\":0,\"twoHandHitMultiplier\":1}}", weapon);
                var owner = combat.GetComponent<TestWeaponOwner>() ?? combat.gameObject.AddComponent<TestWeaponOwner>();
                owner.Own(weapon);
            }
            var loadout = combat.gameObject.EnsureTestLoadout();
            loadout.Configure(melee ? weapon : loadout.LeftWeapon, melee ? GripMode.OneHand : loadout.LeftGrip,
                melee ? loadout.RightWeapon : weapon, melee ? loadout.RightGrip : GripMode.OneHand);
        }
    }
}
