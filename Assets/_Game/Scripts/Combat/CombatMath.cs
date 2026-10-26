using UnityEngine;

namespace MechaXCOM
{
    public static class CombatMath
    {
        public const int MaxPilotTechnique = 50;
        public const float BaseCriticalChance = 5f;
        public const float MaxCriticalChance = 50f;
        public const float CriticalAdvantagePerPoint = .9f;
        public const float CriticalDisadvantagePerPoint = .25f;
        public const float BaseCriticalDamageMultiplier = 1.25f;
        public const float ArmorResistanceMultiplier = .8f;
        public const float ArmorWeaknessMultiplier = 1.1f;
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
        {
            // Preserve authored decimal coefficients (.8, 1.1, 1.25), including large raw rolls.
            // Keep the raw weapon roll until every damage multiplier has been applied.
            decimal value = System.Math.Max(0, damage) * (decimal)armorMultiplier *
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
            float mechaMultiplier = 1f)
        {
            // Keep finite float inputs in double precision, including very large multipliers followed by zero.
            // Apply every multiplier before the single final clamp and half-up rounding.
            double chance = (double)baseChance * gripMultiplier * distanceMultiplier * headMultiplier *
                partMultiplier * coverMultiplier * reactionMultiplier * mechaMultiplier;
            // Return to the game's float precision only after all factors and clamping.
            // This preserves half-up rounding for authored float values such as 10 * .45f = 4.5f.
            float clamped = (float)System.Math.Max(0d, System.Math.Min(100d, chance));
            return Mathf.FloorToInt(clamped + .5f);
        }
    }
}
