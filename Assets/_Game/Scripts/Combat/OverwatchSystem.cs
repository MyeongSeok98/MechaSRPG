using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM
{
    [DisallowMultipleComponent]
    public class OverwatchSystem : MonoBehaviour
    {
        public const int EntryAPCost = 2;
        public const float ReactionHitMultiplier = .75f;
        private GridManager grid;
        private CombatSystem combat;
        private TurnManager turns;
        private BattleUnit[] units = Array.Empty<BattleUnit>();
        private BattleUnit currentMover, currentWatcher;
        private int resolutionVersion;
        private long lastDecisionId, executingDecisionId;
        private TargetBodyPart executingPart;
        private OverwatchDecision? pendingDecision;
        private IOverwatchDecisionReceiver decisionReceiver;
        public bool IsResolving { get; private set; }
        public OverwatchDecision? PendingDecision
        {
            get { ValidatePending(); return pendingDecision; }
        }
        private bool HasDecisionReceiver => decisionReceiver != null &&
            (!(decisionReceiver is UnityEngine.Object owner) || owner != null) && decisionReceiver.IsReady;
        internal bool IsReady => isActiveAndEnabled && grid && grid.isActiveAndEnabled &&
            combat && combat.isActiveAndEnabled && turns && turns.isActiveAndEnabled && !turns.IsBattleOver;

        public void ConfigureDecisionReceiver(IOverwatchDecisionReceiver receiver)
        {
            if (ReferenceEquals(decisionReceiver, receiver)) return;
            ClearResolution(); decisionReceiver = receiver;
        }
        public void UnregisterDecisionReceiver(IOverwatchDecisionReceiver receiver)
        {
            if (!ReferenceEquals(decisionReceiver, receiver)) return;
            ClearResolution(); decisionReceiver = null;
        }
        public bool IsResolvingMover(BattleUnit mover)
        {
            ValidatePending(); return IsResolving && currentMover == mover;
        }

        public void Initialize(GridManager grid, CombatSystem combat, TurnManager turns, BattleUnit[] units)
        {
            CancelAll();
            this.grid = grid; this.combat = combat; this.turns = turns;
            this.units = units == null ? Array.Empty<BattleUnit>() : (BattleUnit[])units.Clone();
            foreach (var unit in this.units)
                if (unit)
                {
                    if (unit.Overwatch) unit.Overwatch.BindSystem(this);
                    if (unit.Movement) unit.Movement.ConfigureOverwatch(this);
                }
            if (combat) combat.ConfigureOverwatch(this);
            if (turns) turns.ConfigureOverwatch(this);
        }
        private bool ValidUnit(BattleUnit unit) => IsReady && unit && unit.isActiveAndEnabled && unit.IsInitialized &&
            Array.IndexOf(units, unit) >= 0 && unit.Health && unit.Health.isActiveAndEnabled && unit.Health.CanFight &&
            unit.Combat && unit.Combat.isActiveAndEnabled && unit.ActionPoints && unit.ActionPoints.isActiveAndEnabled &&
            grid.TryGetCell(unit.CurrentGridPosition, out var cell) && cell.OccupyingUnit == unit;

        public bool CanBegin(BattleUnit unit, WeaponSlot slot) => ValidUnit(unit) &&
            unit.Overwatch && unit.Overwatch.isActiveAndEnabled && !unit.Overwatch.IsActive &&
            unit.Movement && unit.Movement.isActiveAndEnabled && unit.CanTakeAction && turns.CanAct(unit) &&
            unit.ActionPoints.CanSpend(EntryAPCost) &&
            unit.Combat.TryGetAttack(slot, out var attack, out _) && attack.Type == AttackType.Ranged && !attack.Weapon.IsIndirect &&
            unit.Ammo && unit.Ammo.CanFire(slot);

        public bool TryBegin(BattleUnit unit, WeaponSlot slot, FacingDirection facing)
        {
            if (!FacingDirections.IsValid(facing) || !CanBegin(unit, slot) ||
                !unit.Combat.TryGetAttack(slot, out var attack, out _) || !unit.ActionPoints.TrySpend(EntryAPCost)) return false;
            unit.Facing = facing;
            var visual = unit.transform.Find("Visual");
            if (visual) visual.rotation = FacingDirections.Rotation(facing);
            unit.Overwatch.Arm(slot, attack.Weapon, facing);
            return true;
        }

        public List<GridCell> GetCoverageCells(BattleUnit unit, WeaponSlot slot, FacingDirection facing)
        {
            var result = new List<GridCell>();
            if (!ValidUnit(unit) || !FacingDirections.IsValid(facing) || !unit.Vision || !unit.Vision.isActiveAndEnabled ||
                !unit.Ammo || !unit.Ammo.CanFire(slot) ||
                !unit.Combat.TryGetAttack(slot, out var attack, out _) || attack.Type != AttackType.Ranged) return result;
            foreach (var cell in grid.GetCells())
                if (CoversCell(unit, cell.Position, attack, facing)) result.Add(cell);
            return result;
        }
        private bool CoversCell(BattleUnit unit, Vector2Int position, AttackStats attack, FacingDirection facing)
        {
            if (!unit.Vision || !unit.Vision.isActiveAndEnabled) return false;
            long dx = (long)position.x - unit.CurrentGridPosition.x;
            long dz = (long)position.y - unit.CurrentGridPosition.y;
            long detection = unit.Vision.DetectionRange;
            return OverwatchMath.IsInForwardCone(unit.CurrentGridPosition, position, facing) &&
                CombatMath.IsInRange(unit.CurrentGridPosition, position, attack.Range) &&
                dx * dx + dz * dz <= detection * detection &&
                grid.HasLineOfSight(unit.CurrentGridPosition, position) && grid.HasClearShot(unit.CurrentGridPosition, position);
        }
        private bool ValidMover(BattleUnit mover) => IsReady && mover && mover.isActiveAndEnabled && mover.IsAlive &&
            Array.IndexOf(units, mover) >= 0 && mover.Team == turns.ActiveTeam &&
            mover.Health && mover.Health.isActiveAndEnabled && mover.Health.IsPresent &&
            mover.Movement && mover.Movement.isActiveAndEnabled && mover.Movement.IsMoving && mover.Movement.IsResolvingStep &&
            grid.TryGetCell(mover.CurrentGridPosition, out var cell) && cell.OccupyingUnit == mover;

        private bool ValidReaction(BattleUnit watcher, BattleUnit target, WeaponSlot slot) =>
            ValidMover(target) && ValidUnit(watcher) &&
            watcher.Team != target.Team && watcher.Overwatch && watcher.Overwatch.CanReactTo(target) && watcher.Overwatch.Slot == slot &&
            watcher.Combat.TryGetAttack(slot, out var attack, out _) && attack.Type == AttackType.Ranged &&
            CoversCell(watcher, target.CurrentGridPosition, attack, watcher.Overwatch.Facing);

        internal bool CanPreviewDecision(BattleUnit watcher, BattleUnit target, WeaponSlot slot) =>
            IsResolving && !turns.IsPresentingTurn && !turns.IsPresentingAttack && HasDecisionReceiver && pendingDecision.HasValue && currentMover == target && currentWatcher == watcher &&
            pendingDecision.Value.Watcher == watcher && pendingDecision.Value.Target == target && pendingDecision.Value.Slot == slot &&
            ValidReaction(watcher, target, slot);

        internal bool CanResolveShot(BattleUnit watcher, BattleUnit target, WeaponSlot slot, TargetBodyPart part) =>
            executingDecisionId != 0 && pendingDecision.HasValue && executingDecisionId == pendingDecision.Value.Id &&
            executingPart == BodyTargetRules.Normalize(part) && CanPreviewDecision(watcher, target, slot);

        private void ValidatePending()
        {
            // A target opportunity is consumed before the damage callbacks run. Do not mistake
            // that intentional consumption for cancellation of the in-flight decision.
            if (!IsResolving || executingDecisionId != 0) return;
            if (!HasDecisionReceiver || !ValidMover(currentMover)) { ClearResolution(); return; }
            if (pendingDecision.HasValue && !ValidReaction(pendingDecision.Value.Watcher,
                pendingDecision.Value.Target, pendingDecision.Value.Slot)) pendingDecision = null;
        }
        private bool HasPending(long id)
        {
            ValidatePending();
            return !turns.IsPresentingAttack && executingDecisionId == 0 && pendingDecision.HasValue && pendingDecision.Value.Id == id;
        }
        public AttackPreview GetDecisionPreview(long id, TargetBodyPart part)
        {
            if (!HasPending(id)) return new AttackPreview { FailureReason = "No matching overwatch decision." };
            var decision = pendingDecision.Value;
            return combat.GetOverwatchPreview(decision.Watcher, decision.Target, decision.Slot, part);
        }
        public AttackResult TryFire(long id, TargetBodyPart part)
        {
            var preview = GetDecisionPreview(id, part);
            if (!preview.CanAttack) return new AttackResult { FailureReason = preview.FailureReason };
            var decision = pendingDecision.Value;
            int version = resolutionVersion;
            executingDecisionId = id; executingPart = preview.TargetPart;
            try { return combat.TryOverwatchAttack(decision.Watcher, decision.Target, decision.Slot, preview.TargetPart); }
            finally
            {
                if (version == resolutionVersion)
                {
                    executingDecisionId = 0;
                    if (pendingDecision.HasValue && pendingDecision.Value.Id == id) pendingDecision = null;
                }
            }
        }
        public bool TrySkip(long id)
        {
            if (!HasPending(id)) return false;
            var decision = pendingDecision.Value;
            if (!decision.Watcher.Overwatch.TryConsumeTarget(decision.Target)) return false;
            pendingDecision = null; return true;
        }

        public IEnumerator ResolveAfterStep(BattleUnit mover)
        {
            if (IsResolving || !ValidMover(mover) || !HasDecisionReceiver) yield break;
            int version = ++resolutionVersion;
            currentMover = mover; IsResolving = true;
            try
            {
                foreach (var watcher in units)
                {
                    if (version != resolutionVersion || !ValidMover(mover) || !HasDecisionReceiver) break;
                    currentWatcher = watcher;
                    if (!watcher || !watcher.Overwatch) continue;
                    var slot = watcher.Overwatch.Slot;
                    if (!ValidReaction(watcher, mover, slot)) continue;
                    if (lastDecisionId == long.MaxValue) yield break;
                    pendingDecision = new OverwatchDecision(++lastDecisionId, watcher, mover, slot);
                    while (version == resolutionVersion && pendingDecision.HasValue)
                    {
                        ValidatePending();
                        if (version != resolutionVersion || !pendingDecision.HasValue) break;
                        yield return null;
                    }
                    while (version == resolutionVersion && turns && turns.IsPresentingAttack) yield return null;
                }
            }
            finally
            {
                if (version == resolutionVersion) ClearResolution();
            }
        }
        private void ClearResolution()
        {
            resolutionVersion++;
            IsResolving = false; currentMover = null; currentWatcher = null;
            pendingDecision = null; executingDecisionId = 0;
        }
        internal void CancelForMover(BattleUnit mover)
        {
            if (currentMover == mover) ClearResolution();
        }
        internal void ExpireTeam(UnitTeam team)
        {
            foreach (var unit in units)
                if (unit && unit.Team == team && unit.Overwatch) unit.Overwatch.Cancel();
        }
        internal void CancelAll()
        {
            ClearResolution();
            foreach (var unit in units)
                if (unit && unit.Overwatch) unit.Overwatch.Cancel();
        }
        private void OnDisable() => CancelAll();
    }
}
