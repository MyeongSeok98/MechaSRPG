using System;
using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM
{
    public partial class CombatSystem
    {
        public event Action<IReadOnlyList<CombatFeedbackEntry>> AttackFeedback;
        private bool CanShowFeedback(BattleUnit target) => target &&
            (target.Team == UnitTeam.Player || (turns && turns.Visibility && turns.Visibility.IsVisibleTo(UnitTeam.Player, target)));

        private static Vector3 FeedbackPosition(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return target.transform.position + Vector3.up * 2;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return new Vector3(bounds.center.x, bounds.max.y + .4f, bounds.center.z);
        }
        private void PublishFeedback(List<CombatFeedbackEntry> entries) => AttackFeedback?.Invoke(entries.AsReadOnly());

        private AttackResult ExecuteAttack(BattleUnit target, AttackPreview preview)
        {
            bool visible = CanShowFeedback(target);
            Vector3 position = FeedbackPosition(target.gameObject);
            turns.BeginDamageResolution();
            try
            {
                var result = ResolveAttack(target, preview);
                var entries = new List<CombatFeedbackEntry>();
                if (visible) entries.Add(new CombatFeedbackEntry(position, CombatFeedbackText.ForAttack(result), !result.Hit, result.IsCritical));
                PublishFeedback(entries);
                return result;
            }
            finally { turns.EndDamageResolution(); }
        }
    }
}
