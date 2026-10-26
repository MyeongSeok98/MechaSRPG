using UnityEngine;

namespace MechaXCOM
{
    [DisallowMultipleComponent]
    public class OverwatchDecisionController : MonoBehaviour, IOverwatchDecisionReceiver
    {
        private OverwatchSystem system;
        private PlayerInputController input;
        private BodyTargetUI view;
        private bool registered;
        private long displayedId;
        private TargetBodyPart selectedPart = TargetBodyPart.Torso;

        public bool IsReady => isActiveAndEnabled && system && system.isActiveAndEnabled &&
            input && input.IsReadyForOverwatch(this) && view && view.isActiveAndEnabled && view.IsBoundToOverwatch(this);
        public OverwatchDecision? CurrentDecision
        {
            get
            {
                if (!IsReady) { Suspend(); return null; }
                var decision = registered ? system.PendingDecision : null;
                long id = decision.HasValue ? decision.Value.Id : 0;
                if (displayedId != id) { displayedId = id; selectedPart = TargetBodyPart.Torso; }
                return decision;
            }
        }
        public bool IsPending => CurrentDecision.HasValue;
        public TargetBodyPart SelectedPart { get { _ = CurrentDecision; return selectedPart; } }
        public AttackPreview CurrentPreview
        {
            get
            {
                var decision = CurrentDecision;
                return decision.HasValue ? system.GetDecisionPreview(decision.Value.Id, selectedPart) : default;
            }
        }

        public void Initialize(OverwatchSystem system, PlayerInputController input, BodyTargetUI view)
        {
            if (this.system != system || this.input != input || this.view != view) Suspend();
            this.system = system; this.input = input; this.view = view;
            if (input) input.ConfigureOverwatchDecision(this);
            if (view) view.InitializeOverwatch(this);
            RefreshRegistration();
        }
        public void RefreshRegistration()
        {
            if (!IsReady) { Suspend(); return; }
            if (registered) return;
            system.ConfigureDecisionReceiver(this); registered = true;
        }
        public void Suspend()
        {
            if (registered && system) system.UnregisterDecisionReceiver(this);
            registered = false; displayedId = 0; selectedPart = TargetBodyPart.Torso;
        }
        public bool TrySelectPart(TargetBodyPart part)
        {
            if (!IsReady || displayedId == 0 || system.PendingDecision?.Id != displayedId) return false;
            part = BodyTargetRules.Normalize(part);
            if (part < TargetBodyPart.Torso || part > TargetBodyPart.RightLeg) return false;
            selectedPart = part; return true;
        }
        public AttackResult TryFire()
        {
            // Use the displayed request, not a newer request discovered by this click.
            if (!IsReady || displayedId == 0) return new AttackResult { FailureReason = "No displayed overwatch decision." };
            var result = system.TryFire(displayedId, selectedPart);
            if (result.WasExecuted)
            {
                input.RecordAttackResult(result);
                displayedId = 0; selectedPart = TargetBodyPart.Torso;
            }
            return result;
        }
        public bool TrySkip()
        {
            if (!IsReady || displayedId == 0 || !system.TrySkip(displayedId)) return false;
            displayedId = 0; selectedPart = TargetBodyPart.Torso; return true;
        }
        private void Update() { RefreshRegistration(); _ = CurrentDecision; }
        private void OnEnable() => RefreshRegistration();
        private void OnDisable() => Suspend();
        private void OnDestroy() => Suspend();
    }
}
