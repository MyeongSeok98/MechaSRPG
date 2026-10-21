using System;
using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM
{
    public class VisibilitySystem : MonoBehaviour
    {
        private GridManager grid;
        private BattleUnit[] units = Array.Empty<BattleUnit>();
        private readonly HashSet<BattleUnit> subscriptions = new HashSet<BattleUnit>();
        private readonly HashSet<BattleUnit>[] visible =
            { new HashSet<BattleUnit>(), new HashSet<BattleUnit>() };
        private bool initialized;
        private bool[][,] currentCells;
        private bool[][,] exploredCells;
        public bool IsReady => initialized && isActiveAndEnabled && grid;
        public event Action Changed;
        public CellVisibility GetCellVisibility(UnitTeam team, Vector2Int position)
        {
            if (!initialized || !grid || !ValidTeam(team) || !grid.TryGetCell(position, out _) || currentCells == null)
                return CellVisibility.Unexplored;
            int side = (int)team;
            if (IsReady && currentCells[side][position.x, position.y]) return CellVisibility.Visible;
            return exploredCells[side][position.x, position.y] ? CellVisibility.Explored : CellVisibility.Unexplored;
        }

        public void Initialize(GridManager grid, BattleUnit[] units)
        {
            Unsubscribe();
            this.grid = grid;
            this.units = units == null ? Array.Empty<BattleUnit>() : (BattleUnit[])units.Clone();
            initialized = grid;
            currentCells = grid ? new[] { new bool[grid.Width, grid.Height], new bool[grid.Width, grid.Height] } : null;
            exploredCells = grid ? new[] { new bool[grid.Width, grid.Height], new bool[grid.Width, grid.Height] } : null;
            if (isActiveAndEnabled) Subscribe();
            Refresh();
        }

        public void Refresh()
        {
            foreach (var set in visible) set.Clear();
            if (currentCells != null)
                foreach (var cells in currentCells) Array.Clear(cells, 0, cells.Length);
            if (IsReady)
                foreach (var observer in units)
                {
                    if (!Registered(observer) || !observer.Health.CanFight ||
                        !observer.Health.isActiveAndEnabled || !observer.Vision ||
                        !observer.Vision.isActiveAndEnabled || !ValidTeam(observer.Team)) continue;
                    int side = (int)observer.Team;
                    long range = observer.Vision.DetectionRange;
                    foreach (var cell in grid.GetCells())
                    {
                        long dx = (long)cell.Position.x - observer.CurrentGridPosition.x;
                        long dz = (long)cell.Position.y - observer.CurrentGridPosition.y;
                        if (dx * dx + dz * dz <= range * range &&
                            grid.HasLineOfSightToCell(observer.CurrentGridPosition, cell.Position))
                        {
                            currentCells[side][cell.Position.x, cell.Position.y] = true;
                            exploredCells[side][cell.Position.x, cell.Position.y] = true;
                        }
                    }
                    foreach (var target in units)
                    {
                        if (!Registered(target)) continue;
                        long dx = (long)target.CurrentGridPosition.x - observer.CurrentGridPosition.x;
                        long dz = (long)target.CurrentGridPosition.y - observer.CurrentGridPosition.y;
                        if (dx * dx + dz * dz <= range * range &&
                            grid.HasLineOfSight(observer.CurrentGridPosition, target.CurrentGridPosition))
                            visible[(int)observer.Team].Add(target);
                    }
                }
            Changed?.Invoke();
        }

        public bool IsVisibleTo(UnitTeam team, BattleUnit target)
        {
            if (!IsReady || !ValidTeam(team) || !Registered(target) || Array.IndexOf(units, target) < 0)
                return false;
            return target.Team == team || visible[(int)team].Contains(target);
        }

        private bool Registered(BattleUnit unit) => unit && unit.isActiveAndEnabled && unit.IsAlive &&
            grid && grid.TryGetCell(unit.CurrentGridPosition, out var cell) && cell.OccupyingUnit == unit;
        private static bool ValidTeam(UnitTeam team) => team == UnitTeam.Player || team == UnitTeam.Enemy;

        private void Subscribe()
        {
            foreach (var unit in units)
                if (unit && subscriptions.Add(unit))
                {
                    unit.VisibilityChanged += Refresh;
                    if (unit.Vision) unit.Vision.Changed += Refresh;
                }
        }
        private void Unsubscribe()
        {
            foreach (var unit in subscriptions)
                if (unit)
                {
                    unit.VisibilityChanged -= Refresh;
                    if (unit.Vision) unit.Vision.Changed -= Refresh;
                }
            subscriptions.Clear();
        }
        private void OnEnable()
        {
            if (!initialized) return;
            Subscribe();
            Refresh();
        }
        private void OnDisable()
        {
            Unsubscribe();
            Refresh();
        }
        private void OnDestroy() => Unsubscribe();
    }
}
