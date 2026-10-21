using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM
{
    public static class Pathfinder
    {
        private static readonly Vector2Int[] Steps =
            { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        public static List<GridCell> GetReachableCells(GridManager grid, BattleUnit unit)
        {
            var result = new List<GridCell>();
            var parents = Search(grid, unit);
            foreach (var position in parents.Keys)
                if (position != unit.CurrentGridPosition && grid.TryGetCell(position, out var cell))
                    result.Add(cell);
            return result;
        }

        public static List<GridCell> FindPath(GridManager grid, BattleUnit unit, Vector2Int destination)
        {
            var path = new List<GridCell>();
            if (!grid || !unit || destination == unit.CurrentGridPosition) return path;
            var parents = Search(grid, unit);
            if (!parents.ContainsKey(destination)) return path;
            var current = destination;
            while (current != unit.CurrentGridPosition)
            {
                grid.TryGetCell(current, out var cell);
                path.Add(cell);
                current = parents[current];
            }
            path.Reverse();
            return path;
        }

        // Search the whole grid for an attack position; actual movement still uses FindPath's budget.
        public static List<GridCell> FindPathToRange(GridManager grid, BattleUnit unit, Vector2Int target, int range, AttackType type)
        {
            var path = new List<GridCell>();
            if (!grid || !unit || range <= 0 || (type != AttackType.Melee && type != AttackType.Ranged) || !grid.TryGetCell(target, out _) ||
                !grid.TryGetCell(unit.CurrentGridPosition, out var start) ||
                start.OccupyingUnit != unit || !start.Walkable ||
                InAttackPosition(grid, start.Position, target, range, type)) return path;

            var parents = new Dictionary<Vector2Int, Vector2Int>();
            var queue = new Queue<Vector2Int>();
            parents[start.Position] = start.Position;
            queue.Enqueue(start.Position);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (InAttackPosition(grid, current, target, range, type))
                {
                    while (current != start.Position)
                    {
                        grid.TryGetCell(current, out var cell);
                        path.Add(cell);
                        current = parents[current];
                    }
                    path.Reverse();
                    return path;
                }
                foreach (var step in Steps)
                {
                    var next = current + step;
                    if (parents.ContainsKey(next) || !grid.TryGetCell(next, out var cell) ||
                        !cell.Walkable || cell.Occupied) continue;
                    parents[next] = current;
                    queue.Enqueue(next);
                }
            }
            return path;
        }
        public static List<GridCell> FindPathToRange(GridManager grid, BattleUnit unit, Vector2Int target, int range)
            => FindPathToRange(grid, unit, target, range, AttackType.Ranged);
        private static bool InAttackPosition(GridManager grid, Vector2Int from, Vector2Int target, int range, AttackType type) =>
            CombatMath.IsInRange(from, target, range) && (type == AttackType.Melee
                ? grid.HasMeleeReach(from, target, range) : grid.HasClearShot(from, target));

        private static Dictionary<Vector2Int, Vector2Int> Search(GridManager grid, BattleUnit unit)
        {
            var parents = new Dictionary<Vector2Int, Vector2Int>();
            if (!grid || !unit || !grid.TryGetCell(unit.CurrentGridPosition, out var start) ||
                start.OccupyingUnit != unit || !start.Walkable) return parents;
            var distances = new Dictionary<Vector2Int, int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start.Position);
            parents[start.Position] = start.Position;
            distances[start.Position] = 0;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (distances[current] >= unit.AvailableMovementRange) continue;
                foreach (var step in Steps)
                {
                    var next = current + step;
                    if (parents.ContainsKey(next) || !grid.TryGetCell(next, out var cell) ||
                        !cell.Walkable || cell.Occupied) continue;
                    parents[next] = current;
                    distances[next] = distances[current] + 1;
                    queue.Enqueue(next);
                }
            }
            return parents;
        }
    }
}
