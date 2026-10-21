using UnityEngine;

namespace MechaXCOM
{
    public enum FacingDirection { North, East, South, West }

    internal static class FacingDirections
    {
        internal static bool IsValid(FacingDirection direction) => direction >= FacingDirection.North && direction <= FacingDirection.West;
        internal static FacingDirection FromForward(Vector3 forward)
        {
            if (forward.x * forward.x + forward.z * forward.z < .000001f) return FacingDirection.North;
            float angle = Mathf.Repeat(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 360);
            return (FacingDirection)(Mathf.FloorToInt((angle + 45) / 90) % 4);
        }
        internal static Quaternion Rotation(FacingDirection facing) => Quaternion.Euler(0, (int)facing * 90, 0);
    }
}
