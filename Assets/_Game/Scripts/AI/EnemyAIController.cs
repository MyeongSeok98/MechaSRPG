using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM
{
    public class EnemyAIController : MonoBehaviour
    {
        private GridManager grid;
        private CombatSystem combat;
        private TurnManager turns;
        public BattleUnit ActingUnit { get; private set; }
        public bool IsReady => isActiveAndEnabled && grid && combat && combat.isActiveAndEnabled && turns;

        public void Initialize(GridManager grid, CombatSystem combat, TurnManager turns)
        {
            this.grid = grid;
            this.combat = combat;
            this.turns = turns;
        }

        public IEnumerator ExecuteUnitTurn(BattleUnit actor, IReadOnlyList<BattleUnit> units)
        {
            if (!IsReady || !ValidActor(actor) || !turns.CanAct(actor)) yield break;
            ActingUnit = actor;
            try
            {
                BattleUnit target = null;
                var slots = GetUsableSlots(actor);
                if (slots.Count == 0) yield break;
                WeaponSlot attackSlot = default;
                long nearestDistance = long.MaxValue;
                foreach (var unit in units)
                {
                    if (!ValidTarget(unit)) continue;
                    if (!TryImmediateSlot(actor, unit, slots, out var slot)) continue;
                    long distance = CombatMath.GetGridDistance(actor.CurrentGridPosition, unit.CurrentGridPosition);
                    if (distance >= nearestDistance) continue;
                    nearestDistance = distance;
                    target = unit;
                    attackSlot = slot;
                }
                if (target)
                {
                    combat.TryAttackWithWeapon(actor, target, attackSlot);
                    yield break;
                }

                List<GridCell> attackPath = null, approachPath = null;
                BattleUnit attackTarget = null, approachTarget = null;
                WeaponSlot plannedSlot = default;
                bool unaffordableInRange = false;
                foreach (var unit in units)
                {
                    if (!ValidTarget(unit)) continue;
                    foreach (var slot in slots)
                    {
                        if (!actor.Combat.TryGetAttack(slot, out var stats, out _)) continue;
                        if (!actor.ActionPoints.CanSpend(stats.APCost) &&
                            CombatMath.IsInRange(actor.CurrentGridPosition, unit.CurrentGridPosition, stats.Range))
                        { unaffordableInRange = true; continue; }
                        var path = Pathfinder.FindPathToRange(grid, actor, unit.CurrentGridPosition, stats.Range, stats.Type);
                        if (path.Count == 0) continue;
                        int attackBudget = Mathf.Min(actor.AvailableMovementRange, actor.ActionPoints.CurrentPoints - stats.APCost);
                        if (path.Count <= attackBudget && (attackPath == null || path.Count < attackPath.Count))
                        { attackPath = path; attackTarget = unit; plannedSlot = slot; }
                        if (approachPath == null || path.Count < approachPath.Count)
                        { approachPath = path; approachTarget = unit; }
                    }
                }
                // Preserve waiting when already in range but unable to pay for any attack plan.
                if (attackPath == null && unaffordableInRange) yield break;
                var chosenPath = attackPath ?? approachPath;
                target = attackPath != null ? attackTarget : approachTarget;
                if (chosenPath == null || actor.AvailableMovementRange == 0) yield break;
                int steps = Mathf.Min(chosenPath.Count, actor.AvailableMovementRange);
                if (!actor.Movement.TryMove(chosenPath[steps - 1].Position)) yield break;
                while (actor && actor.Movement && actor.Movement.IsMoving)
                {
                    if (!IsReady || ActingUnit != actor || !ValidActor(actor) || !ValidTarget(target))
                    { CancelCurrentAction(); yield break; }
                    yield return null;
                }
                if (attackPath != null && IsReady && ActingUnit == actor && ValidActor(actor) && ValidTarget(target))
                    combat.TryAttackWithWeapon(actor, target, plannedSlot);
            }
            finally
            {
                if (ActingUnit == actor) ActingUnit = null;
            }
        }

        private static List<WeaponSlot> GetUsableSlots(BattleUnit actor)
        {
            var slots = new List<WeaponSlot>(2);
            foreach (var type in new[] { AttackType.Melee, AttackType.Ranged })
                foreach (var slot in actor.Combat.GetAttackSlots(type))
                    if (actor.Combat.TryGetAttack(slot, out var stats, out _) && stats.Range > 0 && stats.MinDamage > 0) slots.Add(slot);
            return slots;
        }
        private bool TryImmediateSlot(BattleUnit actor, BattleUnit target, List<WeaponSlot> slots, out WeaponSlot chosen)
        {
            foreach (var slot in slots)
                if (combat.GetWeaponPreview(actor, target, slot).CanAttack) { chosen = slot; return true; }
            chosen = default; return false;
        }

        private bool Registered(BattleUnit unit) => unit && unit.isActiveAndEnabled && unit.IsAlive &&
            grid && grid.TryGetCell(unit.CurrentGridPosition, out var cell) && cell.OccupyingUnit == unit;
        private bool ValidTarget(BattleUnit unit) => Registered(unit) && unit.Team == UnitTeam.Player &&
            turns && turns.Visibility && turns.Visibility.IsVisibleTo(UnitTeam.Enemy, unit);
        private bool ValidActor(BattleUnit unit) => Registered(unit) && unit.Health.CanFight && unit.Team == UnitTeam.Enemy &&
            turns && turns.isActiveAndEnabled && !turns.IsBattleOver && turns.ActiveTeam == UnitTeam.Enemy &&
            unit.Movement && unit.Movement.isActiveAndEnabled && unit.Combat && unit.Combat.isActiveAndEnabled &&
            unit.ActionPoints && unit.ActionPoints.isActiveAndEnabled && unit.Mobility && unit.Mobility.isActiveAndEnabled;

        public void CancelCurrentAction()
        {
            if (ActingUnit && ActingUnit.Movement) ActingUnit.Movement.CancelMove();
            ActingUnit = null;
        }
        private void OnDisable()
        {
            CancelCurrentAction();
            if (turns) turns.NotifyAIUnavailable(this);
        }
    }
}
