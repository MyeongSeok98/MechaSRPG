using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM
{
    public class UnitMovement : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float movementSpeed = 12f;
        [SerializeField, Min(1f), Tooltip("Visual turn speed in degrees per second. Local +Z is forward.")]
        private float rotationSpeed = 360f;
        private Transform visual;
        private GridManager grid;
        private BattleUnit unit;
        private Coroutine routine;
        private OverwatchSystem overwatch;
        public bool IsMoving { get; private set; }
        internal bool IsResolvingStep { get; private set; }
        internal void ConfigureOverwatch(OverwatchSystem system) => overwatch = system;

        public void Initialize(GridManager grid, BattleUnit unit)
        {
            this.grid = grid;
            this.unit = unit;
            visual = transform.Find("Visual");
            unit.Facing = FacingDirections.FromForward(visual ? visual.forward : transform.forward);
            if (visual) visual.rotation = FacingDirections.Rotation(unit.Facing);
        }
        public bool TryMove(Vector2Int destination)
        {
            if (!isActiveAndEnabled || IsMoving || !grid || !unit || !unit.isActiveAndEnabled ||
                !unit.CanTakeAction || !unit.Mobility || !unit.ActionPoints.isActiveAndEnabled) return false;
            var path = Pathfinder.FindPath(grid, unit, destination);
            if (path.Count == 0) return false;
            unit.Mobility.ResetMovement();
            IsMoving = true;
            routine = StartCoroutine(FollowPath(path));
            return true;
        }
        private IEnumerator FollowPath(List<GridCell> path)
        {
            foreach (var cell in path)
            {
                var target = cell.WorldPosition + Vector3.up * unit.HeightOffset;
                var stepFacing = FacingDirections.FromForward(target - transform.position);
                if (visual)
                {
                    var direction = target - transform.position;
                    direction.y = 0;
                    if (direction.sqrMagnitude > .000001f)
                    {
                        var facing = Quaternion.LookRotation(direction, Vector3.up);
                        // Turn the model first; keep the grid root and selection collider unchanged.
                        while (Quaternion.Angle(visual.rotation, facing) > .01f)
                        {
                            if (!CanEnter(cell)) { Finish(true); yield break; }
                            visual.rotation = Quaternion.RotateTowards(visual.rotation, facing, rotationSpeed * Time.deltaTime);
                            yield return null;
                        }
                        visual.rotation = facing;
                    }
                }
                while (Vector3.Distance(transform.position, target) > 0.001f)
                {
                    if (!CanEnter(cell)) { Finish(true); yield break; }
                    transform.position = Vector3.MoveTowards(transform.position, target, movementSpeed * Time.deltaTime);
                    yield return null;
                }
                if (!CanEnter(cell) || !grid.TryMoveUnit(unit, cell.Position))
                {
                    Finish(true);
                    yield break;
                }
                transform.position = target;
                unit.Facing = stepFacing;
                unit.ActionPoints.TrySpend(1);
                unit.Mobility.RecordCellMoved();
                if (overwatch)
                {
                    IsResolvingStep = true;
                    try { yield return overwatch.ResolveAfterStep(unit); }
                    finally { IsResolvingStep = false; }
                }
                if (!IsMoving || !unit || !unit.CanContinueMovement)
                {
                    Finish(true); yield break;
                }
            }
            unit.HasPendingFacing = unit.RequiresArrivalFacing;
            Finish(false);
        }
        private bool CanEnter(GridCell cell) => grid && unit && unit.CanContinueMovement &&
            unit.Mobility && unit.Mobility.isActiveAndEnabled && unit.Mobility.MovedCells < unit.DamagedMovementRange &&
            unit.ActionPoints && unit.ActionPoints.isActiveAndEnabled && unit.ActionPoints.CanSpend(1) &&
            cell.Walkable && (!cell.Occupied || cell.OccupyingUnit == unit);

        private void Finish(bool restorePosition)
        {
            if (restorePosition && unit)
            {
                unit.HasPendingFacing = false;
                unit.IsConfirmingFacing = false;
                if (visual) visual.rotation = FacingDirections.Rotation(unit.Facing);
            }
            if (restorePosition && grid && unit)
                transform.position = grid.GetWorldPosition(unit.CurrentGridPosition) + Vector3.up * unit.HeightOffset;
            IsMoving = false;
            IsResolvingStep = false;
            routine = null;
        }
        public bool TryConfirmFacing(FacingDirection facing)
        {
            if (!unit || !unit.CanResolveFacing || !FacingDirections.IsValid(facing)) return false;
            unit.IsConfirmingFacing = true;
            routine = StartCoroutine(ConfirmFacing(facing));
            return true;
        }
        private IEnumerator ConfirmFacing(FacingDirection facing)
        {
            // Keep completion asynchronous even with no model or no angular change.
            yield return null;
            var rotation = FacingDirections.Rotation(facing);
            while (visual && Quaternion.Angle(visual.rotation, rotation) > .01f)
            {
                visual.rotation = Quaternion.RotateTowards(visual.rotation, rotation, rotationSpeed * Time.deltaTime);
                yield return null;
            }
            if (visual) visual.rotation = rotation;
            unit.Facing = facing;
            unit.IsConfirmingFacing = false;
            unit.HasPendingFacing = false;
            routine = null;
        }
        public bool TryKeepArrivalFacing()
        {
            if (!unit || !unit.CanResolveFacing) return false;
            unit.HasPendingFacing = false;
            return true;
        }
        public void CancelMove()
        {
            if (overwatch) overwatch.CancelForMover(unit);
            if (routine != null) StopCoroutine(routine);
            Finish(true);
        }
        private void OnDisable() => CancelMove();
        private void OnValidate()
        {
            movementSpeed = Mathf.Max(0.1f, movementSpeed);
            rotationSpeed = Mathf.Max(1f, rotationSpeed);
        }
    }
}
