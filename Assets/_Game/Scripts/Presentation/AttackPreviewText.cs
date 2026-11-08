namespace MechaXCOM
{
    public static class AttackPreviewText
    {
        public static string AdditionalDamage(AttackPreview preview) => preview.AdditionalHitPartCount > 0
            ? $"추가 {preview.AdditionalHitPartCount}부위 이하 / {preview.AdditionalDamageMinRatio * 100:0.##}-{preview.AdditionalDamageMaxRatio * 100:0.##}% · {preview.AdditionalDamageStepPercent}%p"
            : string.Empty;
        public static string AdditionalDamageResult(AttackResult result)
        {
            if (result.AdditionalPartDamage == null || result.AdditionalPartDamage.Count == 0) return string.Empty;
            var items = new System.Collections.Generic.List<string>();
            foreach (var hit in result.AdditionalPartDamage)
                items.Add($"{BodyTargetRules.Label(hit.Part)} {hit.DamageDealt} (HP {hit.PartHPAfter})");
            return "추가 피해: " + string.Join(", ", items);
        }
        public static string DamageRange(AttackPreview preview)
        {
            // A manually constructed legacy preview has no armor multiplier set.
            float armor = preview.ArmorMultiplier > 0f ? preview.ArmorMultiplier : 1f;
            float pilot = preview.PilotDamageMultiplier > 0f ? preview.PilotDamageMultiplier : 1f;
            int minimum = CombatMath.CalculateModifiedAttackDamage(preview.MinDamage, false, armor, pilot);
            int maximum = CombatMath.CalculateModifiedAttackDamage(preview.MaxDamage, false, armor, pilot);
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
        public static string Summary(WeaponAmmoStatus status, WeaponSlot slot) => status.Weapon && status.UsesAmmo
            ? Format(status.Weapon.RangedCategory == RangedWeaponCategory.Beam, status.Loaded, status.Capacity,
                status.SpareMagazines, slot == WeaponSlot.Backpack) : string.Empty;
        public static string Summary(AttackPreview preview) => preview.UsesAmmo
            ? Format(preview.UsesEnergyPack, preview.AmmoLoaded, preview.MagazineCapacity, preview.SpareMagazines,
                preview.WeaponSlot == WeaponSlot.Backpack)
            : string.Empty;
        private static string Format(bool energy, int loaded, int capacity, int spares, bool noReload = false) => noReload
            ? $"{(energy ? "에너지" : "탄약")} {loaded}/{capacity} / 재장전 불가"
            : energy ? $"에너지 {loaded}/{capacity} / 예비 팩 {spares}개"
            : $"탄약 {loaded}/{capacity} / 예비 탄창 {spares}개";
    }
}
