namespace MechaXCOM
{
    public static class AttackPreviewText
    {
        public static string DamageRange(AttackPreview preview) => preview.MinDamage == preview.MaxDamage
            ? preview.MinDamage.ToString() : $"{preview.MinDamage}-{preview.MaxDamage}";
        public static string HitAndDamage(AttackPreview preview) => preview.BlockedByBuilding ? "Blocked by building" :
            $"Hit {preview.HitChance}% / Damage {DamageRange(preview)}";
        public static string DamageAndAP(AttackPreview preview) => preview.CanAttack
            ? $"Damage {DamageRange(preview)} / AP {preview.APCost}" : preview.FailureReason;
    }
}
