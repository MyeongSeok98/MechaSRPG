using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM
{
    public class CoverSystem : MonoBehaviour
    {
        [SerializeField, Range(0, 1)] private float halfCoverMultiplier = .75f;
        [SerializeField, Range(0, 1)] private float fullCoverMultiplier = .5f;

        public void Build(GridManager grid, IEnumerable<CoverObject> covers)
        {
            if (!grid) return;
            foreach (var cell in grid.GetCells()) cell.ClearCover();
            var sources = new Dictionary<GridCell, CoverType>();
            if (covers == null) return;
            Physics.SyncTransforms();
            foreach (var cover in covers)
            {
                if (!cover || !cover.isActiveAndEnabled) continue;
                foreach (var cell in grid.RegisterObstacle(cover.Collider))
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
        }

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
