using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MechaXCOM
{
    public readonly struct CombatFeedbackEntry
    {
        public Vector3 WorldPosition { get; }
        public string Text { get; }
        public bool IsMiss { get; }
        public bool IsCritical { get; }
        public CombatFeedbackEntry(Vector3 position, string text, bool miss = false, bool critical = false)
        { WorldPosition = position; Text = text; IsMiss = miss; IsCritical = critical; }
    }

    public static class CombatFeedbackText
    {
        public static string ForAttack(AttackResult result)
        {
            if (!result.WasExecuted) return string.Empty;
            if (!result.Hit) return "MISS";
            var text = new StringBuilder(result.IsCritical ? "CRITICAL!" : string.Empty);
            AppendDamage(text, result.TargetPart, result.DamageDealt);
            if (result.AdditionalPartDamage != null)
                foreach (var hit in result.AdditionalPartDamage) AppendDamage(text, hit.Part, hit.DamageDealt);
            return text.ToString();
        }
        public static string ForParts(IReadOnlyList<PartDamageResult> hits)
        {
            var text = new StringBuilder();
            foreach (var hit in hits) AppendDamage(text, hit.Part, hit.DamageDealt);
            return text.ToString();
        }
        public static string ForCover(CoverAttackResult result) =>
            $"-{result.DamageDealt}";
        private static void AppendDamage(StringBuilder text, TargetBodyPart part, int damage)
        {
            if (text.Length > 0) text.Append('\n');
            text.Append(BodyTargetRules.Label(BodyTargetRules.Normalize(part))).Append(" -").Append(damage);
        }
    }
}
