using UnityEngine;

namespace MechaXCOM
{
    public static class CombatMath
    {
        public const int MaxPilotCombatStat = 50;
        public const int MaxPilotTechnique = MaxPilotCombatStat;
        public const float PilotShootingHitPerPoint = .01f;
        public const float PilotMeleeHitPerPoint = .005f;
        public const float PilotMeleeDamagePerPoint = .005f;
        public const int MaxAttackHitChance = 99;
        public static float CalculatePilotHitMultiplier(AttackType type, int attack, int evasion)
        {
            int difference = Mathf.Clamp(attack, 0, MaxPilotCombatStat) - Mathf.Clamp(evasion, 0, MaxPilotCombatStat);
            return 1f + difference * (type == AttackType.Melee ? PilotMeleeHitPerPoint : PilotShootingHitPerPoint);
        }
        public static float CalculatePilotDamageMultiplier(AttackType type, int melee) => type == AttackType.Melee
            ? 1f + Mathf.Clamp(melee, 0, MaxPilotCombatStat) * PilotMeleeDamagePerPoint : 1f;
        public const float BaseCriticalChance = 5f;
        public const float MaxCriticalChance = 50f;
        public const float CriticalAdvantagePerPoint = .9f;
        public const float CriticalDisadvantagePerPoint = .25f;
        public const float BaseCriticalDamageMultiplier = 1.25f;
        public static int CalculateCoverDamage(int raw, float multiplier)
        {
            if (raw <= 0 || float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier <= 0) return 0;
            if (raw * (double)multiplier >= int.MaxValue) return int.MaxValue;
            decimal rounded = System.Math.Floor(raw * (decimal)multiplier + .5m);
            return (int)System.Math.Min(int.MaxValue, rounded);
        }
        public const float ArmorResistanceMultiplier = .8f;
        public const float ArmorWeaknessMultiplier = 1.1f;
        public static float CalculateDamageTakenMultiplier(AttackType type, RangedWeaponCategory category, int beamPercent, int ballisticPercent)
        {
            if (type != AttackType.Ranged) return 1f;
            int percent;
            if (category == RangedWeaponCategory.Beam) percent = beamPercent;
            else if (category == RangedWeaponCategory.Ballistic) percent = ballisticPercent;
            else return 1f;
            return Mathf.Clamp(percent, MechaData.MinDamageTakenPercent, MechaData.MaxDamageTakenPercent) / 100f;
        }
        // Legacy armor-table interpretation only. Live combat uses the target's final lookup.
        public static float CalculateArmorMultiplier(ArmorType armor, AttackType attackType, RangedWeaponCategory category)
        {
            if (attackType != AttackType.Ranged) return 1f;
            if (category == RangedWeaponCategory.Beam)
            {
                if (armor == ArmorType.BeamResistant) return ArmorResistanceMultiplier;
                if (armor == ArmorType.BallisticResistant) return ArmorWeaknessMultiplier;
            }
            else if (category == RangedWeaponCategory.Ballistic)
            {
                if (armor == ArmorType.BallisticResistant) return ArmorResistanceMultiplier;
                if (armor == ArmorType.BeamResistant) return ArmorWeaknessMultiplier;
            }
            return 1f;
        }
        public static float CalculateCriticalChance(int attackerTechnique, int targetTechnique)
        {
            int difference = Mathf.Clamp(attackerTechnique, 0, MaxPilotTechnique) -
                Mathf.Clamp(targetTechnique, 0, MaxPilotTechnique);
            return Mathf.Clamp(BaseCriticalChance + difference * (difference >= 0
                ? CriticalAdvantagePerPoint : CriticalDisadvantagePerPoint), 0f, MaxCriticalChance);
        }
        public static bool IsCritical(float chance, float roll) =>
            roll >= 0f && roll < 100f && roll < Mathf.Clamp(chance, 0f, MaxCriticalChance);
        public static int CalculateCriticalDamage(int damage, bool critical) => CalculateAttackDamage(damage, critical, 1f);
        public static int CalculateAttackDamage(int damage, bool critical, float armorMultiplier)
            => CalculateModifiedAttackDamage(damage, critical, armorMultiplier, 1f);
        public static int CalculateModifiedAttackDamage(int damage, bool critical, float armorMultiplier, float pilotMultiplier)
        {
            // Preserve authored decimal coefficients (.8, 1.1, 1.25), including large raw rolls.
            // Keep the raw weapon roll until every damage multiplier has been applied.
            decimal value = System.Math.Max(0, damage) * (decimal)armorMultiplier * (decimal)pilotMultiplier *
                (critical ? (decimal)BaseCriticalDamageMultiplier : 1m);
            return (int)System.Math.Min(int.MaxValue, System.Math.Floor(value + .5m));
        }
        // Balance coefficients: use the net stat advantage, so equal ratings cancel.
        public const float MechaAccuracyAdvantagePerPoint = .01f;
        public const float MechaEvasionAdvantagePerPoint = .008f;
        public static float CalculateMechaHitMultiplier(int accuracy, int evasion)
        {
            int difference = Mathf.Clamp(accuracy, 0, MechaData.MaxCombatStat) -
                Mathf.Clamp(evasion, 0, MechaData.MaxCombatStat);
            float perPoint = difference >= 0 ? MechaAccuracyAdvantagePerPoint : MechaEvasionAdvantagePerPoint;
            return Mathf.Max(0f, 1f + difference * perPoint);
        }
        public static long GetGridDistance(Vector2Int attacker, Vector2Int target) =>
            System.Math.Abs((long)target.x - attacker.x) + System.Math.Abs((long)target.y - attacker.y);
        public static bool IsInRange(Vector2Int attacker, Vector2Int target, int range) =>
            range >= 0 && GetGridDistance(attacker, target) <= range;
        public static CoverDirection GetIncomingDirection(Vector2Int attacker, Vector2Int target)
        {
            var delta = attacker - target;
            float angle = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
            int sector = Mathf.FloorToInt((Mathf.Repeat(angle, 360f) + 22.5f) / 45f) % 8;
            return CoverDirections.Clockwise[sector];
        }
        public static bool IsHit(int chance, int roll) => roll >= 0 && roll < 100 && roll < Mathf.Clamp(chance, 0, 100);
        public static int CalculateAttackHitChance(int baseChance, float partMultiplier, float coverMultiplier,
            float headMultiplier = 1f, float gripMultiplier = 1f, float distanceMultiplier = 1f, float reactionMultiplier = 1f,
            float mechaMultiplier = 1f, float pilotMultiplier = 1f)
        {
            // Keep finite float inputs in double precision, including very large multipliers followed by zero.
            // Apply every multiplier before the single final clamp and half-up rounding.
            double chance = (double)baseChance * gripMultiplier * distanceMultiplier * headMultiplier *
                partMultiplier * coverMultiplier * reactionMultiplier * mechaMultiplier * pilotMultiplier;
            // Return to the game's float precision only after all factors and clamping.
            // This preserves half-up rounding for authored float values such as 10 * .45f = 4.5f.
            float clamped = (float)System.Math.Max(0d, System.Math.Min(MaxAttackHitChance, chance));
            return Mathf.FloorToInt(clamped + .5f);
        }
    }
}
