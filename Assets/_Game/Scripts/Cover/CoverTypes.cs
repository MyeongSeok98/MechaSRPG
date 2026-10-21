namespace MechaXCOM
{
    public enum CoverDirection { North, East, South, West, NorthEast, SouthEast, SouthWest, NorthWest }
    public enum CoverType { None, Half, Full }

    internal static class CoverDirections
    {
        // Keep existing enum values; angular order is separate from serialized values.
        internal static readonly CoverDirection[] Clockwise =
        {
            CoverDirection.North, CoverDirection.NorthEast, CoverDirection.East, CoverDirection.SouthEast,
            CoverDirection.South, CoverDirection.SouthWest, CoverDirection.West, CoverDirection.NorthWest
        };
        internal static readonly UnityEngine.Vector2Int[] Steps =
        {
            new UnityEngine.Vector2Int(0, 1), new UnityEngine.Vector2Int(1, 1),
            new UnityEngine.Vector2Int(1, 0), new UnityEngine.Vector2Int(1, -1),
            new UnityEngine.Vector2Int(0, -1), new UnityEngine.Vector2Int(-1, -1),
            new UnityEngine.Vector2Int(-1, 0), new UnityEngine.Vector2Int(-1, 1)
        };
    }
}
