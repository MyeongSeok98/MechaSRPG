using UnityEngine;

namespace MechaXCOM
{
    // IMGUI equivalent of a Sliced Image with the default Canvas reference PPU (100).
    internal static class SlicedSpriteGUI
    {
        public static void Draw(Rect target, Sprite sprite)
        {
            if (!sprite || target.width <= 0 || target.height <= 0) return;
            var texture = sprite.texture;
            var source = sprite.textureRect;
            var border = sprite.border;
            float units = 100f / sprite.pixelsPerUnit;
            float scaleX = border.x + border.z > 0 ? Mathf.Min(1, target.width / ((border.x + border.z) * units)) : 1;
            float scaleY = border.y + border.w > 0 ? Mathf.Min(1, target.height / ((border.y + border.w) * units)) : 1;
            float[] x = { target.x, target.x + border.x * units * scaleX, target.xMax - border.z * units * scaleX, target.xMax };
            float[] y = { target.y, target.y + border.w * units * scaleY, target.yMax - border.y * units * scaleY, target.yMax };
            float[] u = { source.x / texture.width, (source.x + border.x) / texture.width,
                (source.xMax - border.z) / texture.width, source.xMax / texture.width };
            float[] v = { source.yMax / texture.height, (source.yMax - border.w) / texture.height,
                (source.y + border.y) / texture.height, source.y / texture.height };
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                {
                    var destination = Rect.MinMaxRect(x[column], y[row], x[column + 1], y[row + 1]);
                    if (destination.width <= 0 || destination.height <= 0) continue;
                    GUI.DrawTextureWithTexCoords(destination, texture,
                        new Rect(u[column], v[row + 1], u[column + 1] - u[column], v[row] - v[row + 1]));
                }
        }
    }
}
