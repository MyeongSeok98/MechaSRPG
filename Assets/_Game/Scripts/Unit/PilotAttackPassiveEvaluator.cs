using System;
using System.Collections.Generic;

namespace MechaXCOM
{
    public readonly struct PilotAttackPassiveEffect
    {
        public string PassiveId { get; }
        public PilotAttackModifiers Modifiers { get; }

        public PilotAttackPassiveEffect(string passiveId, PilotAttackModifiers modifiers)
        {
            PassiveId = passiveId;
            Modifiers = modifiers;
        }
    }

    public static class PilotAttackPassiveEvaluator
    {
        public static IReadOnlyList<PilotAttackPassiveEffect> Evaluate(
            IEnumerable<IPilotAttackPassive> passives, PilotAttackContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var effects = new List<PilotAttackPassiveEffect>();
            if (passives == null) return effects.AsReadOnly();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var passive in passives)
            {
                if (passive == null) continue;
                string id = passive.Id;
                if (string.IsNullOrWhiteSpace(id))
                    throw new ArgumentException("A passive must have a nonblank ID.", nameof(passives));
                if (!ids.Add(id))
                    throw new ArgumentException("Duplicate passive ID: " + id, nameof(passives));
                if (passive.CanApply(context))
                    effects.Add(new PilotAttackPassiveEffect(id, passive.GetModifiers(context)));
            }
            return effects.AsReadOnly();
        }
    }
}
