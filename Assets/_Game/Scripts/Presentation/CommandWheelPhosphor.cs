using UnityEngine;

namespace MechaXCOM
{
    // Stateless history: earlier poses are always projected at today's anchor, never old screen coordinates.
    internal static class CommandWheelPhosphor
    {
        public static float TrailOpacity(float elapsed, float age, float persistence)
        {
            if (persistence <= 0 || age <= 0 || elapsed <= age || elapsed >= CommandWheelAnimation.Duration + .6f) return 0;
            float tail = Mathf.Max(0, elapsed - CommandWheelAnimation.Duration);
            return .11f * Mathf.Exp(-(age + tail) / persistence);
        }

        internal static Texture2D CreateRaster()
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) {
                name = "Command Wheel Phosphor Raster", hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point
            };
            var pixels = new Color32[32 * 32];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    uint hash = (uint)(x * 374761393 + y * 668265263);
                    hash = (hash ^ (hash >> 13)) * 1274126177u;
                    byte alpha = (byte)((y % 4 == 0 ? 170 : 0) + (hash % 65));
                    pixels[y * 32 + x] = new Color32(0, 0, 0, alpha);
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            return texture;
        }
    }
}
