using System.Collections.Generic;

namespace MechaXCOM
{
    // Read equipped slots rather than available attacks: damaged arms must remain visible.
    public readonly struct UnitWeaponInfo
    {
        public WeaponSlot Slot { get; }
        public string SlotLabel { get; }
        public string Name { get; }
        public string ResourceText { get; }
        public string AmmoText { get; }
        public string ReserveText { get; }
        public string StatusText { get; }
        public bool IsDisabled { get; }
        public bool ResourceShortage { get; }
        private UnitWeaponInfo(BattleUnit unit, UnitWeaponLoadout loadout, WeaponSlot slot)
        {
            Slot = slot;
            var weapon = loadout.GetWeapon(slot);
            bool twoHands = slot == WeaponSlot.LeftHand ? loadout.LeftGrip == GripMode.TwoHands :
                slot == WeaponSlot.RightHand && loadout.RightGrip == GripMode.TwoHands;
            SlotLabel = slot == WeaponSlot.Backpack ? "B" : twoHands ? "LR" : slot == WeaponSlot.LeftHand ? "L" : "R";
            Name = weapon.DisplayName;
            var ammo = unit.Ammo.GetStatus(slot);
            AmmoText = !weapon.UsesAmmo ? "MELEE" : $"AMMO {ammo.Loaded}/{ammo.Capacity}";
            ReserveText = !weapon.UsesAmmo ? "" : slot == WeaponSlot.Backpack ? "NO RELOAD" : $"RESERVE {ammo.SpareMagazines}";
            ResourceText = AmmoText + (ReserveText.Length > 0 ? " | " + ReserveText : "");
            IsDisabled = !unit.Combat.TryGetAttack(slot, out _, out _);
            StatusText = IsDisabled ? "사용 불가" : weapon.UsesAmmo && ammo.Loaded == 0 ? "탄약 없음" :
                unit.ActionPoints.CurrentPoints < weapon.APCost ? "AP 부족" : "";
            ResourceShortage = !IsDisabled && StatusText.Length > 0;
        }
        public static void Read(BattleUnit unit, List<UnitWeaponInfo> destination)
        {
            destination.Clear();
            if (!unit) return;
            var loadout = unit.GetComponent<UnitWeaponLoadout>();
            if (!loadout) return;
            foreach (var slot in Slots)
                if (loadout.GetWeapon(slot)) destination.Add(new UnitWeaponInfo(unit, loadout, slot));
        }
        private static readonly WeaponSlot[] Slots = { WeaponSlot.LeftHand, WeaponSlot.RightHand, WeaponSlot.Backpack };
    }
}

