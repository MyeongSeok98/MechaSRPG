using UnityEngine;

namespace MechaXCOM
{
    public readonly struct WeaponAmmoStatus
    {
        public readonly WeaponData Weapon;
        public readonly bool UsesAmmo;
        public readonly int Loaded, Capacity, SpareMagazines, ReloadAPCost;
        internal WeaponAmmoStatus(WeaponData weapon, bool usesAmmo, int loaded, int capacity, int spares, int reloadCost)
        { Weapon = weapon; UsesAmmo = usesAmmo; Loaded = loaded; Capacity = capacity; SpareMagazines = spares; ReloadAPCost = reloadCost; }
    }

    [DisallowMultipleComponent]
    public class UnitWeaponAmmo : MonoBehaviour
    {
        private sealed class Magazine
        {
            internal readonly WeaponData Weapon;
            internal readonly bool UsesAmmo;
            internal readonly int Capacity, ReloadAPCost;
            internal int Loaded, Spares;
            internal Magazine(WeaponData weapon)
            {
                Weapon = weapon; UsesAmmo = weapon.UsesAmmo;
                Capacity = UsesAmmo ? Mathf.Max(1, weapon.MagazineCapacity) : 0;
                Loaded = Capacity; Spares = UsesAmmo ? Mathf.Max(0, weapon.SpareMagazineCount) : 0;
                ReloadAPCost = UsesAmmo ? Mathf.Max(1, weapon.ReloadAPCost) : 0;
            }
            internal WeaponAmmoStatus Status => new WeaponAmmoStatus(Weapon, UsesAmmo, Loaded, Capacity, Spares, ReloadAPCost);
        }
        private BattleUnit unit;
        private UnitWeaponLoadout loadout;
        private Magazine left, right;
        internal void Initialize(BattleUnit owner)
        {
            if (unit && unit != owner) return;
            unit = owner; RefreshLoadout();
        }
        // Definitions can be equipped after battle initialization. Retain the state of
        // the same physical weapon when its grip changes or it transfers hands.
        private void RefreshLoadout()
        {
            if (!unit || !unit.IsInitialized) return;
            var current = unit.GetComponent<UnitWeaponLoadout>();
            if (loadout != current)
            {
                Unsubscribe(); loadout = current;
                if (loadout && isActiveAndEnabled) loadout.Changed += RefreshLoadout;
            }
            var leftWeapon = loadout ? loadout.LeftWeapon : null;
            var rightWeapon = loadout ? loadout.RightWeapon : null;
            var oldLeft = left; var oldRight = right;
            left = !leftWeapon ? null : oldLeft != null && oldLeft.Weapon == leftWeapon ? oldLeft :
                oldRight != null && oldRight.Weapon == leftWeapon && rightWeapon != oldRight.Weapon ? oldRight : new Magazine(leftWeapon);
            right = !rightWeapon ? null : oldRight != null && oldRight != left && oldRight.Weapon == rightWeapon ? oldRight :
                oldLeft != null && oldLeft != left && oldLeft.Weapon == rightWeapon ? oldLeft : new Magazine(rightWeapon);
        }
        private Magazine GetMagazine(WeaponSlot slot)
        {
            RefreshLoadout();
            return slot == WeaponSlot.LeftHand ? left : slot == WeaponSlot.RightHand ? right : null;
        }
        public WeaponAmmoStatus GetStatus(WeaponSlot slot) => GetMagazine(slot)?.Status ?? default;
        public bool CanFire(WeaponSlot slot)
        {
            var magazine = GetMagazine(slot);
            return isActiveAndEnabled && unit && unit.IsInitialized && magazine != null &&
                loadout && loadout.IsValid(out _) && (!magazine.UsesAmmo || magazine.Loaded > 0);
        }
        public bool CanReload(WeaponSlot slot)
        {
            var magazine = GetMagazine(slot);
            return isActiveAndEnabled && unit && unit.IsInitialized && magazine != null &&
                loadout && loadout.IsValid(out _) && magazine.UsesAmmo && magazine.Loaded < magazine.Capacity && magazine.Spares > 0;
        }
        internal bool TryConsumeShot(WeaponSlot slot)
        {
            if (!CanFire(slot)) return false;
            var magazine = GetMagazine(slot);
            if (magazine.UsesAmmo) magazine.Loaded--;
            return true;
        }
        internal bool TryReload(WeaponSlot slot)
        {
            if (!CanReload(slot)) return false;
            var magazine = GetMagazine(slot);
            magazine.Loaded = magazine.Capacity; magazine.Spares--;
            return true;
        }
        private void Unsubscribe()
        {
            if (loadout) loadout.Changed -= RefreshLoadout;
            loadout = null;
        }
        private void OnEnable() { if (unit) { Unsubscribe(); RefreshLoadout(); } }
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();
    }
}
