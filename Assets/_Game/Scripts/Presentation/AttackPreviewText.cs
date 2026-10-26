namespace MechaXCOM
{
    public static class AttackPreviewText
    {
        public static string DamageRange(AttackPreview preview)
        {
            // A manually constructed legacy preview has no armor multiplier set.
            float armor = preview.ArmorMultiplier > 0f ? preview.ArmorMultiplier : 1f;
            int minimum = CombatMath.CalculateAttackDamage(preview.MinDamage, false, armor);
            int maximum = CombatMath.CalculateAttackDamage(preview.MaxDamage, false, armor);
            return minimum == maximum ? minimum.ToString() : $"{minimum}-{maximum}";
        }
        public static string HitAndDamage(AttackPreview preview) => preview.BlockedByBuilding ? "Blocked by building" :
            $"Hit {preview.HitChance}% / Damage {DamageRange(preview)}";
        public static string DamageAndAP(AttackPreview preview) => preview.CanAttack
            ? $"Damage {DamageRange(preview)} / AP {preview.APCost}" : preview.FailureReason;
    }
    public static class WeaponAmmoText
    {
        public static string Summary(WeaponAmmoStatus status) => status.Weapon && status.UsesAmmo
            ? Format(status.Weapon.RangedCategory == RangedWeaponCategory.Beam, status.Loaded, status.Capacity, status.SpareMagazines)
            : string.Empty;
        public static string Summary(AttackPreview preview) => preview.UsesAmmo
            ? Format(preview.UsesEnergyPack, preview.AmmoLoaded, preview.MagazineCapacity, preview.SpareMagazines)
            : string.Empty;
        private static string Format(bool energy, int loaded, int capacity, int spares) => energy
            ? $"에너지 {loaded}/{capacity} / 예비 팩 {spares}개"
            : $"탄약 {loaded}/{capacity} / 예비 탄창 {spares}개";
    }
}
