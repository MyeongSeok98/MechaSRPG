using UnityEngine;

namespace MechaXCOM
{
    public sealed class GridCell
    {
        private readonly CoverType[] cover = new CoverType[8];
        public Vector2Int Position { get; }
        public Vector3 WorldPosition { get; }
        public bool Walkable { get; internal set; } = true;
        public bool BlocksShots { get; internal set; }
        public bool BlocksVision { get; internal set; }
        public BattleUnit OccupyingUnit { get; internal set; }
        public bool Occupied => OccupyingUnit != null;

        public GridCell(Vector2Int position, Vector3 worldPosition)
        {
            Position = position;
            WorldPosition = worldPosition;
        }

        public CoverType GetCover(CoverDirection direction) => cover[(int)direction];
        public void SetCover(CoverDirection direction, CoverType type)
        {
            if (type > cover[(int)direction]) cover[(int)direction] = type;
        }
        public void ClearCover() => System.Array.Clear(cover, 0, cover.Length);
    }
}
