using UnityEngine;

namespace MechaXCOM.Tests
{
    public static class AmmoTestWeapons
    {
        public static WeaponData Equip(BattleUnit unit, WeaponSlot slot = WeaponSlot.RightHand,
            int capacity = 2, int spares = 2, int reload = 2, int fire = 1, int damage = 1,
            RangedWeaponCategory category = RangedWeaponCategory.Beam)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponData>();
            JsonUtility.FromJsonOverwrite($"{{\"displayName\":\"Ammo test\",\"attackType\":0,\"rangedCategory\":{(int)category},\"minDamage\":{damage},\"maxDamage\":{damage},\"range\":7,\"apCost\":{fire},\"gripRequirement\":0,\"magazineCapacity\":{capacity},\"spareMagazineCount\":{spares},\"reloadAPCost\":{reload}}}", weapon);
            var owner = unit.GetComponent<TestWeaponOwner>() ?? unit.gameObject.AddComponent<TestWeaponOwner>(); owner.Own(weapon);
            var loadout = unit.gameObject.EnsureTestLoadout();
            loadout.Configure(slot == WeaponSlot.LeftHand ? weapon : loadout.LeftWeapon, GripMode.OneHand,
                slot == WeaponSlot.RightHand ? weapon : loadout.RightWeapon, GripMode.OneHand);
            return weapon;
        }
    }
}
