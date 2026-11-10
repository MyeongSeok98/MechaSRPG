using System.Collections.Generic;

namespace MechaXCOM
{
    public static class UnitInspectionText
    {
        public static string Name(BattleUnit unit) => !unit ? string.Empty : unit.MechaData ? unit.MechaData.DisplayName : unit.name;
        public static string PartHP(BattleUnit unit, TargetBodyPart part) => !unit || !unit.Health ? string.Empty :
            $"{BodyTargetRules.Label(BodyTargetRules.Normalize(part))} {unit.Health.GetCurrentHP(part)}/{unit.Health.GetMaxHP(part)}";
        public static string Health(BattleUnit unit)
        {
            if (!unit) return string.Empty;
            var lines = new List<string>();
            for (int i = (int)TargetBodyPart.Torso; i <= (int)TargetBodyPart.RightLeg; i++) lines.Add(PartHP(unit, (TargetBodyPart)i));
            return string.Join("\n", lines);
        }
        public static string Equipment(BattleUnit unit)
        {
            if (!unit) return string.Empty;
            var loadout = unit.GetComponent<UnitWeaponLoadout>();
            if (!loadout) return "L.Hand: None\nR.Hand: None\nBackpack: None";
            var lines = new List<string>();
            if (loadout.LeftWeapon && loadout.LeftGrip == GripMode.TwoHands)
                lines.Add("Both hands: " + loadout.LeftWeapon.DisplayName);
            else if (loadout.RightWeapon && loadout.RightGrip == GripMode.TwoHands)
                lines.Add("Both hands: " + loadout.RightWeapon.DisplayName);
            else
            {
                lines.Add("L.Hand: " + WeaponName(loadout.LeftWeapon));
                lines.Add("R.Hand: " + WeaponName(loadout.RightWeapon));
            }
            lines.Add("Backpack: " + (loadout.Backpack ? loadout.Backpack.DisplayName : "None"));
            if (loadout.Backpack) lines.Add("Backpack weapon: " + WeaponName(loadout.Backpack.Weapon));
            return string.Join("\n", lines);
        }
        private static string WeaponName(WeaponData value) => value ? value.DisplayName : "None";
    }
}
