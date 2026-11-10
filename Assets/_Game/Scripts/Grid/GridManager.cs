using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM
{
    public class GridManager : MonoBehaviour
    {
        [SerializeField, Min(1)] private int width = 10;
        [SerializeField, Min(1)] private int height = 10;
        [SerializeField, Min(0.1f)] private float cellSize = 2f;
        private GridCell[,] cells;
        private sealed class ObstacleFootprint
        {
            internal List<GridCell> Cells;
            internal bool BlocksShots;
            internal bool TerrainRegistration, CoverRegistration;
        }
        private readonly Dictionary<BoxCollider, ObstacleFootprint> obstacleSources = new Dictionary<BoxCollider, ObstacleFootprint>();
        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;

        public void Configure(int width, int height, float cellSize)
        {
            this.width = width;
            this.height = height;
            this.cellSize = cellSize;
            OnValidate();
        }
        private void OnValidate()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            cellSize = Mathf.Max(0.1f, cellSize);
        }
        public void Generate()
        {
            OnValidate();
            obstacleSources.Clear();
            cells = new GridCell[width, height];
            for (int x = 0; x < width; x++)
                for (int z = 0; z < height; z++)
                {
                    var position = new Vector2Int(x, z);
                    cells[x, z] = new GridCell(position, GetWorldPosition(position));
                }
        }
        public Vector3 GetWorldPosition(Vector2Int position) =>
            transform.position + new Vector3((position.x + 0.5f) * cellSize, 0, (position.y + 0.5f) * cellSize);

        public bool TryGetCell(Vector2Int position, out GridCell cell)
        {
            cell = null;
            if (cells == null || position.x < 0 || position.y < 0 ||
                position.x >= width || position.y >= height) return false;
            cell = cells[position.x, position.y];
            return true;
        }
        public bool TryWorldToGrid(Vector3 worldPosition, out Vector2Int position)
        {
            var relative = worldPosition - transform.position;
            position = new Vector2Int(Mathf.FloorToInt(relative.x / cellSize), Mathf.FloorToInt(relative.z / cellSize));
            return TryGetCell(position, out _);
        }
        public IEnumerable<GridCell> GetCells()
        {
            if (cells == null) yield break;
            foreach (var cell in cells) yield return cell;
        }
        public void SetWalkable(Vector2Int position, bool walkable)
        {
            if (TryGetCell(position, out var cell)) { cell.BaseWalkable = walkable; RefreshTerrainFlags(cell); }
        }
        private static void RefreshTerrainFlags(GridCell cell)
        {
            cell.Walkable = cell.BaseWalkable && cell.ObstacleCount == 0;
            cell.BlocksShots = cell.ShotBlockerCount > 0; cell.BlocksVision = cell.BlocksShots;
        }
        public bool UnregisterObstacle(BoxCollider obstacle)
        {
            if (ReferenceEquals(obstacle, null) || !obstacleSources.TryGetValue(obstacle, out var source)) return false;
            obstacleSources.Remove(obstacle);
            foreach (var cell in source.Cells)
            { cell.ObstacleCount--; if (source.BlocksShots) cell.ShotBlockerCount--; RefreshTerrainFlags(cell); }
            return true;
        }
        internal void UnregisterCoverObstacle(BoxCollider obstacle)
        {
            if (ReferenceEquals(obstacle, null) || !obstacleSources.TryGetValue(obstacle, out var source) || !source.CoverRegistration) return;
            if (source.TerrainRegistration) RebuildObstacle(obstacle, true, false);
            else UnregisterObstacle(obstacle);
        }
        internal void PruneInactiveObstacles()
        {
            var inactive = new List<BoxCollider>();
            foreach (var pair in obstacleSources)
                if (!pair.Key || !pair.Key.enabled || !pair.Key.gameObject.activeInHierarchy) inactive.Add(pair.Key);
            foreach (var collider in inactive) UnregisterObstacle(collider);
        }
        public IReadOnlyList<GridCell> GetObstacleCells(BoxCollider obstacle) =>
            !ReferenceEquals(obstacle, null) && obstacleSources.TryGetValue(obstacle, out var source)
                ? source.Cells.AsReadOnly() : (IReadOnlyList<GridCell>)System.Array.Empty<GridCell>();
        public List<GridCell> RegisterObstacle(BoxCollider obstacle)
        {
            bool cover = !ReferenceEquals(obstacle, null) && obstacleSources.TryGetValue(obstacle, out var source) && source.CoverRegistration;
            return RebuildObstacle(obstacle, true, cover);
        }
        internal List<GridCell> RegisterCoverObstacle(BoxCollider obstacle)
        {
            bool terrain = !ReferenceEquals(obstacle, null) && obstacleSources.TryGetValue(obstacle, out var source) && source.TerrainRegistration;
            return RebuildObstacle(obstacle, terrain, true);
        }
        private List<GridCell> RebuildObstacle(BoxCollider obstacle, bool terrainRegistration, bool coverRegistration)
        {
            var occupied = new List<GridCell>();
            UnregisterObstacle(obstacle);
            if (!obstacle || !obstacle.enabled || !obstacle.gameObject.activeInHierarchy) return occupied;
            if (Quaternion.Angle(obstacle.transform.rotation, Quaternion.identity) > 0.1f)
            {
                Debug.LogWarning($"{obstacle.name}: obstacles must be axis-aligned.", obstacle);
                return occupied;
            }
            Physics.SyncTransforms();
            var bounds = obstacle.bounds;
            bool blocksShots = obstacle.TryGetComponent<GridObstacle>(out var terrain) &&
                terrain.isActiveAndEnabled && terrain.BlocksShots;
            foreach (var cell in GetCells())
            {
                var center = cell.WorldPosition;
                if (center.x < bounds.min.x || center.x > bounds.max.x ||
                    center.z < bounds.min.z || center.z > bounds.max.z) continue;
                cell.ObstacleCount++; if (blocksShots) cell.ShotBlockerCount++; RefreshTerrainFlags(cell);
                occupied.Add(cell);
            }
            obstacleSources[obstacle] = new ObstacleFootprint { Cells = new List<GridCell>(occupied), BlocksShots = blocksShots,
                TerrainRegistration = terrainRegistration, CoverRegistration = coverRegistration };
            if (occupied.Count == 0)
                Debug.LogWarning($"{obstacle.name}: footprint contains no cell centers.", obstacle);
            return occupied;
        }
        // Melee reach requires a short traversable route, independent of remaining movement AP.
        public bool HasMeleeReach(Vector2Int from, Vector2Int to, int range) => HasMeleeReachCore(from, to, range, false);
        public bool HasMeleeReachToObstacle(Vector2Int from, Vector2Int to, int range, BoxCollider obstacle)
        {
            if (!obstacle || !obstacleSources.TryGetValue(obstacle, out var footprint) || !TryGetCell(to, out var cell) ||
                !footprint.Cells.Contains(cell) || !cell.BaseWalkable || cell.ObstacleCount != 1 || cell.Occupied) return false;
            return HasMeleeReachCore(from, to, range, true);
        }
        private bool HasMeleeReachCore(Vector2Int from, Vector2Int to, int range, bool allowDestinationObstacle)
        {
            if (from == to || !CombatMath.IsInRange(from, to, range) ||
                !TryGetCell(from, out var source) || !source.Walkable ||
                !TryGetCell(to, out var destination) || (!allowDestinationObstacle && !destination.Walkable)) return false;
            var distances = new Dictionary<Vector2Int, int> { [from] = 0 };
            var queue = new Queue<Vector2Int>(); queue.Enqueue(from);
            var steps = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (distances[current] >= range) continue;
                foreach (var step in steps)
                {
                    var next = current + step;
                    if (distances.ContainsKey(next) || !TryGetCell(next, out var cell)) continue;
                    if (next == to && allowDestinationObstacle) return true;
                    if (!cell.Walkable) continue;
                    if (next == to) return true;
                    if (cell.Occupied) continue;
                    distances[next] = distances[current] + 1; queue.Enqueue(next);
                }
            }
            return false;
        }
        public bool HasLineOfSight(Vector2Int from, Vector2Int to) => HasClearLine(from, to, true);
        public bool HasLineOfSightToCell(Vector2Int from, Vector2Int to) => HasClearLine(from, to, true, true);
        public bool HasClearShot(Vector2Int from, Vector2Int to) => HasClearLine(from, to, false);
        public bool HasClearShotToObstacle(Vector2Int from, Vector2Int to, BoxCollider obstacle) =>
            HasClearLineToObstacle(from, to, obstacle, false);
        public bool HasClearBlastToObstacle(Vector2Int from, Vector2Int to, BoxCollider obstacle) =>
            HasClearLineToObstacle(from, to, obstacle, true);
        private bool HasClearLineToObstacle(Vector2Int from, Vector2Int to, BoxCollider obstacle, bool blast)
        {
            if (!obstacle || !obstacleSources.TryGetValue(obstacle, out var source) ||
                !TryGetCell(to, out var cell) || !source.Cells.Contains(cell)) return false;
            // Only the target's own last blocker may be hit at the endpoint.
            // Another building overlapping that cell still blocks the attack.
            bool allowDestination = source.BlocksShots && cell.ShotBlockerCount == 1;
            return HasClearLine(from, to, false, allowDestination, blast);
        }
        // Indirect launch uses building footprints, not cover height or an actual arc.
        public bool HasClearIndirectLaunchPath(Vector2Int from, Vector2Int to) => HasClearLine(from, to, false);
        public bool HasClearBlast(Vector2Int from, Vector2Int to) => HasClearLine(from, to, false, false, true);
        private bool HasClearLine(Vector2Int from, Vector2Int to, bool vision, bool allowBlockingDestination = false, bool ignoreBlockingSource = false)
        {
            if (!TryGetCell(from, out _) || (!ignoreBlockingSource && !LineCellClear(from, vision)) || !TryGetCell(to, out _) ||
                (!allowBlockingDestination && !LineCellClear(to, vision))) return false;
            var current = from;
            int dx = Mathf.Abs(to.x - from.x), dz = Mathf.Abs(to.y - from.y);
            int stepX = System.Math.Sign(to.x - from.x), stepZ = System.Math.Sign(to.y - from.y);
            int crossedX = 0, crossedZ = 0;
            while (current != to)
            {
                // Compare exact grid-boundary crossing times without floating-point corner errors.
                long boundaryX = dx == 0 ? long.MaxValue : (2L * crossedX + 1) * dz;
                long boundaryZ = dz == 0 ? long.MaxValue : (2L * crossedZ + 1) * dx;
                if (boundaryX == boundaryZ)
                {
                    // A corner touches both side cells as well as the next diagonal cell.
                    if (!LineCellClear(current + new Vector2Int(stepX, 0), vision) ||
                        !LineCellClear(current + new Vector2Int(0, stepZ), vision)) return false;
                    current += new Vector2Int(stepX, stepZ);
                    crossedX++; crossedZ++;
                }
                else if (boundaryX < boundaryZ)
                {
                    current.x += stepX;
                    crossedX++;
                }
                else
                {
                    current.y += stepZ;
                    crossedZ++;
                }
                if (!(allowBlockingDestination && current == to) && !LineCellClear(current, vision)) return false;
            }
            return true;
        }
        private bool LineCellClear(Vector2Int position, bool vision) =>
            TryGetCell(position, out var cell) && !(vision ? cell.BlocksVision : cell.BlocksShots);
        public bool TryPlaceUnit(BattleUnit unit, Vector2Int position)
        {
            if (!unit || !TryGetCell(position, out var cell) ||
                !cell.Walkable || (cell.Occupied && cell.OccupyingUnit != unit)) return false;
            ReleaseUnit(unit);
            cell.OccupyingUnit = unit;
            unit.SetGridPosition(position);
            return true;
        }
        public bool TryMoveUnit(BattleUnit unit, Vector2Int position)
        {
            if (!unit || !TryGetCell(unit.CurrentGridPosition, out var source) ||
                source.OccupyingUnit != unit) return false;
            return TryPlaceUnit(unit, position);
        }
        public void ReleaseUnit(BattleUnit unit)
        {
            foreach (var cell in GetCells())
                if (ReferenceEquals(cell.OccupyingUnit, unit)) cell.OccupyingUnit = null;
        }
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.5f, 0.4f);
            var origin = transform.position;
            for (int x = 0; x <= width; x++)
                Gizmos.DrawLine(origin + new Vector3(x * cellSize, 0.02f, 0),
                    origin + new Vector3(x * cellSize, 0.02f, height * cellSize));
            for (int z = 0; z <= height; z++)
                Gizmos.DrawLine(origin + new Vector3(0, 0.02f, z * cellSize),
                    origin + new Vector3(width * cellSize, 0.02f, z * cellSize));
        }
    }
}
