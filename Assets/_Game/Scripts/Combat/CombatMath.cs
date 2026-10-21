using UnityEngine;

namespace MechaXCOM
{
    public static class CombatMath
    {
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
            float headMultiplier = 1f, float gripMultiplier = 1f, float distanceMultiplier = 1f, float reactionMultiplier = 1f)
        {
            // Keep finite float inputs in double precision, including very large multipliers followed by zero.
            // Apply every multiplier before the single final clamp and half-up rounding.
            double chance = (double)baseChance * gripMultiplier * distanceMultiplier * headMultiplier *
                partMultiplier * coverMultiplier * reactionMultiplier;
            // Return to the game's float precision only after all factors and clamping.
            // This preserves half-up rounding for authored float values such as 10 * .45f = 4.5f.
            float clamped = (float)System.Math.Max(0d, System.Math.Min(100d, chance));
            return Mathf.FloorToInt(clamped + .5f);
        }
    }
}
