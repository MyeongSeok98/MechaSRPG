using UnityEngine;

namespace MechaXCOM
{
    [DisallowMultipleComponent]
    public class UnitVisibilityPresenter : MonoBehaviour
    {
        private BattleUnit unit;
        private VisibilitySystem visibility;
        private Renderer[] hiddenRenderers;
        private bool[] originalEnabled;
        private bool subscribed;

        public void Initialize(BattleUnit unit, VisibilitySystem visibility)
        {
            Unsubscribe();
            Restore();
            this.unit = unit;
            this.visibility = visibility;
            if (!isActiveAndEnabled) return;
            Subscribe();
            Apply();
        }
        private void Apply()
        {
            if (!unit || !unit.IsAlive) return;
            if (unit.Team == UnitTeam.Player || (visibility && visibility.IsVisibleTo(UnitTeam.Player, unit)))
            {
                Restore();
                return;
            }
            if (hiddenRenderers == null)
            {
                hiddenRenderers = unit.GetComponentsInChildren<Renderer>(true);
                originalEnabled = new bool[hiddenRenderers.Length];
                for (int i = 0; i < hiddenRenderers.Length; i++)
                    originalEnabled[i] = hiddenRenderers[i].enabled;
            }
            foreach (var renderer in hiddenRenderers)
                if (renderer) renderer.enabled = false;
        }
        private void Restore(bool preserveInactive = false)
        {
            // UnitHealth owns destruction/ejection hiding; never undo it during cleanup.
            // A temporarily disabled unit must retain its pre-fog renderer flags for reactivation.
            if (preserveInactive && unit && unit.IsAlive && !unit.isActiveAndEnabled) return;
            if (hiddenRenderers != null && unit && unit.IsAlive)
                for (int i = 0; i < hiddenRenderers.Length; i++)
                    if (hiddenRenderers[i]) hiddenRenderers[i].enabled = originalEnabled[i];
            hiddenRenderers = null;
            originalEnabled = null;
        }
        private void Subscribe()
        {
            if (subscribed || !visibility) return;
            visibility.Changed += Apply;
            subscribed = true;
        }
        private void Unsubscribe()
        {
            if (subscribed && visibility) visibility.Changed -= Apply;
            subscribed = false;
        }
        private void OnEnable() { Subscribe(); Apply(); }
        private void OnDisable() { Unsubscribe(); Restore(true); }
        private void OnDestroy() { Unsubscribe(); Restore(); }
    }
}
