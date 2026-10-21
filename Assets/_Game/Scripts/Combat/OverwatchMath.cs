using UnityEngine;

namespace MechaXCOM
{
    public static class OverwatchMath
    {
        // A cardinal 90-degree cone includes both 45-degree edges, but not its origin.
        public static bool IsInForwardCone(Vector2Int origin, Vector2Int target, FacingDirection facing)
        {
            long dx = (long)target.x - origin.x, dz = (long)target.y - origin.y;
            switch (facing)
            {
                case FacingDirection.North: return dz > 0 && dz >= System.Math.Abs(dx);
                case FacingDirection.East: return dx > 0 && dx >= System.Math.Abs(dz);
                case FacingDirection.South: return dz < 0 && -dz >= System.Math.Abs(dx);
                case FacingDirection.West: return dx < 0 && -dx >= System.Math.Abs(dz);
                default: return false;
            }
        }
    }
}
