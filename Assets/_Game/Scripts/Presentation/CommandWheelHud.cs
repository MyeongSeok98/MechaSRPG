using UnityEngine;

namespace MechaXCOM
{
    // Non-interactive decoration. Every unit-relative mark uses the fixed model anchor.
    internal static class CommandWheelHud
    {
        private static readonly Vector2[] Corners = { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) };
        private static readonly Vector2[] Axes = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
        internal static void Draw(Vector2 center, float s, CommandWheelAnimation t, Color color)
        {
            for (int i = 0; i < 60; i++)
            {
                float angle = i * 6;
                if (Mathf.Abs(angle % 90 - 45) < 10) continue;
                float light = CommandWheelAnimation.Ease((t.Seconds - .095f - i * .0024f) / .2f);
                bool major = i % 5 == 0;
                var direction = Direction(angle + t.Rotation * .25f);
                Line(center + direction * 108 * s, center + direction * (major ? 115 : 111) * s,
                    (major ? 1.4f : .8f) * s, Alpha(color, light * (major ? .65f : .26f)));
            }
            float pulse = Mathf.Clamp01((t.Seconds - .78f) / .28f);
            float opacity = Mathf.Clamp01((t.Seconds - .05f) / .15f) * (.65f + .35f * Mathf.Sin(pulse * Mathf.PI));
            Brackets(center, (54 + 42 * (1 - t.AlignmentProgress)) * s, 13 * s, 1.6f * s, Alpha(color, opacity));
            float gate = CommandWheelAnimation.Ease((t.Seconds - .09f) / .48f);
            Brackets(center, (84 + 45 * (1 - gate)) * s, 12 * s, s,
                Alpha(color, Mathf.Clamp01((t.Seconds - .06f) / .18f) * (.42f - .23f * Mathf.Clamp01((t.Seconds - .8f) / .42f))));
            Scan(center, s, t, color);
            float align = CommandWheelAnimation.Ease((t.Seconds - .04f) / .44f);
            float rail = Mathf.Clamp01((t.Seconds - .03f) / .15f) * (1 - .7f * Mathf.Clamp01((t.Seconds - .65f) / .32f));
            foreach (var axis in Axes)
            {
                float offset = 38 * (1 - align);
                var start = center + axis * (130 + offset) * s;
                var normal = new Vector2(-axis.y, axis.x);
                Line(start, center + axis * (144 + offset) * s, s, Alpha(color, rail * .48f));
                Line(start - normal * 4 * s, start + normal * 4 * s, s, Alpha(color, rail * .48f));
                var tab = center + axis * 99 * s;
                Line(tab - normal * 4 * s, tab + normal * 4 * s, 2 * s, Alpha(color, t.FinalLockProgress * .78f));
            }
            float sectorOpacity = t.SectorProgress * (1 - .6f * Mathf.Clamp01((t.Seconds - .9f) / .42f)) * .55f;
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120 + t.SectorRotation + 12, sweep = 37.24f * t.SectorProgress;
                UnitCommandWheel.Arc(center, 123 * s, angle, sweep, 1.3f * s, Alpha(color, sectorOpacity));
                var direction = Direction(angle + sweep);
                Line(center + direction * 120 * s, center + direction * 126 * s, 1.3f * s, Alpha(color, sectorOpacity));
            }
            if (t.Seconds >= .78f && t.Seconds < 1.06f)
                UnitCommandWheel.Arc(center, (97 + 19 * pulse) * s, 0, 360, s, Alpha(color, (1 - pulse) * .7f));
            float final = Mathf.Clamp01((t.Seconds - 1.1f) / .27f);
            if (t.Seconds > 1.1f && t.Seconds < 1.37f)
                UnitCommandWheel.Arc(center, (116 + 17 * final) * s, 0, 360, 1.1f * s, Alpha(color, (1 - final) * .45f));
            var chevron = Alpha(color, Mathf.Clamp01((t.Seconds - .72f) / .17f) * .7f);
            Line(center + new Vector2(-7, 118) * s, center + new Vector2(0, 112) * s, 1.3f * s, chevron);
            Line(center + new Vector2(0, 112) * s, center + new Vector2(7, 118) * s, 1.3f * s, chevron);
        }
        internal static void DrawTerminal(Vector2 point, float s, bool right, int index, CommandWheelAnimation t, Color color)
        {
            float p = t.ChannelLockProgress(index);
            if (p <= 0) return;
            float distance = (8 + 6 * (1 - CommandWheelAnimation.Ease(p))) * s;
            var stroke = Alpha(color, .5f + .5f * Mathf.Sin(p * Mathf.PI));
            var top = point - Vector2.one * distance; var bottom = point + Vector2.one * distance;
            Line(top + Vector2.right * 4 * p * s, top, 1.2f * s, stroke);
            Line(top, top + Vector2.up * 4 * p * s, 1.2f * s, stroke);
            Line(bottom - Vector2.right * 4 * p * s, bottom, 1.2f * s, stroke);
            Line(bottom, bottom - Vector2.up * 4 * p * s, 1.2f * s, stroke);
            for (int bit = 0; bit < 3; bit++)
            {
                float light = Mathf.Clamp01((p - bit * .16f) * 3);
                UnitCommandWheel.Fill(new Rect(point.x + (right ? -1 : 1) * (14 + bit * 6) * s - s,
                    point.y + 7 * s, 2 * s, (3 + bit * 2) * s), Alpha(color, light * .65f));
            }
        }
        private static void Scan(Vector2 center, float s, CommandWheelAnimation t, Color color)
        {
            if (t.ScanOpacity <= 0) return;
            float p = t.ScanProgress; p = p * p * (3 - 2 * p);
            float y = -69 + 138 * p;
            float halfWidth = Mathf.Sqrt(Mathf.Max(0, 69 * 69 - y * y));
            Line(center + new Vector2(-halfWidth, y) * s, center + new Vector2(halfWidth, y) * s, s, Alpha(color, t.ScanOpacity));
        }
        private static void Brackets(Vector2 center, float distance, float length, float width, Color color)
        {
            foreach (var corner in Corners)
            {
                var point = center + corner * distance;
                Line(point - new Vector2(corner.x * length, 0), point, width, color);
                Line(point, point - new Vector2(0, corner.y * length), width, color);
            }
        }
        private static Vector2 Direction(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
        private static void Line(Vector2 a, Vector2 b, float width, Color color) => UnitCommandWheel.Line(a, b, Mathf.Max(.75f, width), color);
        private static Color Alpha(Color color, float opacity) { color.a *= opacity; return color; }
    }
}
