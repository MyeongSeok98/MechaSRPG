using UnityEngine;

namespace MechaXCOM
{
    [DisallowMultipleComponent]
    public class UnitOverwatch : MonoBehaviour
    {
        private BattleUnit unit;
        private UnitWeaponLoadout loadout;
        private OverwatchSystem system;
        private bool active, subscribed;
        private Vector2Int origin;
        private UnitTeam team;
        private GripMode grip;
        public WeaponSlot Slot { get; private set; }
        public WeaponData Weapon { get; private set; }
        public FacingDirection Facing { get; private set; }
        public bool IsActive
        {
            get { ValidateState(); return active; }
        }

        internal void Initialize(BattleUnit owner)
        {
            Unsubscribe(); Cancel(); unit = owner;
            loadout = unit ? unit.GetComponent<UnitWeaponLoadout>() : null;
            Subscribe();
        }
        internal void Arm(WeaponSlot slot, WeaponData weapon, FacingDirection facing)
        {
            // Equipment can be added or its component replaced after unit initialization.
            Unsubscribe();
            loadout = unit.GetComponent<UnitWeaponLoadout>();
            Subscribe();
            Slot = slot; Weapon = weapon; Facing = facing;
            origin = unit.CurrentGridPosition; team = unit.Team;
            grip = slot == WeaponSlot.LeftHand ? loadout.LeftGrip : loadout.RightGrip;
            active = true;
        }
        internal void BindSystem(OverwatchSystem owner)
        {
            if (system != owner) Cancel();
            system = owner;
        }
        internal void Cancel() { active = false; Weapon = null; }
        internal bool TryConsume()
        {
            if (!IsActive) return false;
            Cancel(); return true;
        }
        private void ValidateState()
        {
            if (!active) return;
            if (!isActiveAndEnabled || !system || !system.IsReady || !unit || !unit.isActiveAndEnabled || !unit.IsInitialized ||
                !unit.IsRegisteredOnGrid || unit.CurrentGridPosition != origin || unit.Team != team ||
                !unit.Health || !unit.Health.isActiveAndEnabled || !unit.Health.CanFight ||
                !unit.Combat || !unit.Combat.isActiveAndEnabled ||
                !unit.ActionPoints || !unit.ActionPoints.isActiveAndEnabled || !loadout ||
                !unit.Combat.TryGetAttack(Slot, out var attack, out _) ||
                attack.Type != AttackType.Ranged || attack.Weapon != Weapon ||
                (Slot == WeaponSlot.LeftHand ? loadout.LeftGrip : loadout.RightGrip) != grip)
                Cancel();
        }
        private void Subscribe()
        {
            if (subscribed || !isActiveAndEnabled || !unit) return;
            unit.VisibilityChanged += ValidateState;
            if (loadout) loadout.Changed += ValidateState;
            subscribed = true;
        }
        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (unit) unit.VisibilityChanged -= ValidateState;
            if (loadout) loadout.Changed -= ValidateState;
            subscribed = false;
        }
        private void OnEnable() => Subscribe();
        private void OnDisable() { Cancel(); Unsubscribe(); }
    }
}
