using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM
{
    public class CoverSystem : MonoBehaviour
    {
        [SerializeField, Range(0, 1)] private float halfCoverMultiplier = .75f;
        [SerializeField, Range(0, 1)] private float fullCoverMultiplier = .5f;
        private GridManager grid;
        private readonly List<CoverObject> registeredCovers = new List<CoverObject>();
        private readonly Dictionary<CoverObject, BoxCollider> colliders = new Dictionary<CoverObject, BoxCollider>();
        public GridManager Grid => grid;
        public IReadOnlyList<CoverObject> RegisteredCovers => registeredCovers.AsReadOnly();
        public event System.Action Changed;

        public void Build(GridManager grid, IEnumerable<CoverObject> covers)
        {
            if (!grid) return;
            var incoming = covers == null ? new List<CoverObject>() : new List<CoverObject>(covers);
            Unsubscribe();
            if (this.grid)
                foreach (var collider in colliders.Values) this.grid.UnregisterCoverObstacle(collider);
            this.grid = grid; registeredCovers.Clear(); colliders.Clear();
            foreach (var cover in incoming)
                if (cover && !colliders.ContainsKey(cover))
                {
                    registeredCovers.Add(cover); colliders.Add(cover, cover.Collider);
                    cover.Changed += Rebuild;
                }
            Rebuild();
        }
        private void Rebuild()
        {
            if (!grid) return;
            grid.PruneInactiveObstacles();
            foreach (var cell in grid.GetCells()) cell.ClearCover();
            var sources = new Dictionary<GridCell, CoverType>();
            Physics.SyncTransforms();
            foreach (var cover in registeredCovers)
            {
                var collider = colliders[cover];
                if (!cover || !cover.isActiveAndEnabled || (cover.TryGetComponent<CoverDurability>(out var health) && health.IsDestroyed))
                { grid.UnregisterCoverObstacle(collider); continue; }
                var footprint = grid.RegisterCoverObstacle(collider);
                if (!cover.IsProvidingCover) continue;
                foreach (var cell in footprint)
                {
                    if (!sources.TryGetValue(cell, out var existing) || cover.Type > existing)
                        sources[cell] = cover.Type;
                }
            }
            foreach (var source in sources)
                for (int direction = 0; direction < CoverDirections.Steps.Length; direction++)
                {
                    // Neighbor -> source direction, e.g. a southern neighbor receives North.
                    var neighborPosition = source.Key.Position - CoverDirections.Steps[direction];
                    if (grid.TryGetCell(neighborPosition, out var neighbor) && neighbor.Walkable)
                        neighbor.SetCover(CoverDirections.Clockwise[direction], source.Value);
                }
            Changed?.Invoke();
        }
        private void Unsubscribe()
        { foreach (var cover in registeredCovers) if (cover) cover.Changed -= Rebuild; }
        private void OnDestroy() => Unsubscribe();

        public float GetCoverMultiplier(GridCell cell, CoverDirection direction)
        {
            if (cell == null) return 1;
            switch (cell.GetCover(direction))
            {
                case CoverType.Half: return halfCoverMultiplier;
                case CoverType.Full: return fullCoverMultiplier;
                default: return 1;
            }
        }
        public float GetAttackCoverMultiplier(GridCell cell, CoverDirection direction)
        {
            int index = System.Array.IndexOf(CoverDirections.Clockwise, direction);
            if (cell == null || index < 0) return 1;
            float front = GetCoverMultiplier(cell, direction);
            if (index % 2 == 0) return front;
            var left = CoverDirections.Clockwise[(index + 7) % 8];
            var right = CoverDirections.Clockwise[(index + 1) % 8];
            return Mathf.Min(front, Mathf.Min(GetCoverMultiplier(cell, left), GetCoverMultiplier(cell, right)));
        }
        private void OnValidate()
        {
            halfCoverMultiplier = Mathf.Clamp01(halfCoverMultiplier);
            fullCoverMultiplier = Mathf.Clamp01(fullCoverMultiplier);
        }
    }
}
