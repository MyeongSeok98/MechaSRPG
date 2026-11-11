using UnityEngine;

namespace MechaXCOM
{
    public enum UnitCommandAction { None, Move, Attack, Overwatch, Eject, Reload }

    // Presentation only. UnitActionUI retains input routing and all action eligibility rules.
    public static class UnitCommandWheel
    {
        // IMGUI renders synchronously. These are scoped to Draw and restored even if drawing fails.
        private static Texture2D raster;
        private static float rasterStrength;
        private static readonly Vector2[] GlowDirections = {
            Vector2.left, Vector2.right, Vector2.up, Vector2.down,
            new Vector2(-.707f, -.707f), new Vector2(.707f, -.707f), new Vector2(-.707f, .707f), new Vector2(.707f, .707f)
        };
        public static bool TryPlaceChoices(Rect main, Vector2 size, Rect viewport, Rect[] exclusions, float gap, out Rect result)
        {
            float y = Mathf.Clamp(main.y, viewport.yMin + gap, viewport.yMax - gap - size.y);
            var candidates = new[] {
                new Rect(main.xMax + gap, y, size.x, size.y), new Rect(main.xMin - gap - size.x, y, size.x, size.y),
                new Rect(main.xMax - size.x, main.yMax + gap, size.x, size.y), new Rect(main.xMax - size.x, main.yMin - gap - size.y, size.x, size.y),
                new Rect(main.xMin, main.yMax + gap, size.x, size.y), new Rect(main.xMin, main.yMin - gap - size.y, size.x, size.y),
                new Rect(main.center.x - size.x / 2, main.yMax + gap, size.x, size.y), new Rect(main.center.x - size.x / 2, main.yMin - gap - size.y, size.x, size.y)
            };
            foreach (var candidate in candidates)
            {
                if (candidate.xMin < viewport.xMin + gap || candidate.yMin < viewport.yMin + gap ||
                    candidate.xMax > viewport.xMax - gap || candidate.yMax > viewport.yMax - gap || candidate.Overlaps(main)) continue;
                bool blocked = false;
                foreach (var exclusion in exclusions) if (candidate.Overlaps(exclusion)) { blocked = true; break; }
                if (!blocked) { result = candidate; return true; }
            }
            result = default; return false;
        }
        public static Rect ButtonRect(Rect bounds, UnitCommandAction action)
        {
            float s = bounds.width / 720f;
            if (action == UnitCommandAction.Reload)
                return new Rect(bounds.center.x - 58 * s, bounds.yMax - 36 * s, 116 * s, 28 * s);
            bool right = action == UnitCommandAction.Attack || action == UnitCommandAction.Eject;
            bool bottom = action == UnitCommandAction.Overwatch || action == UnitCommandAction.Eject;
            return new Rect(bounds.x + (right ? 452 : 35) * s, bounds.y + (bottom ? 334 : 106) * s - 39 * s, 233 * s, 34 * s);
        }
        public static void Draw(Rect bounds, Vector2 ringCenter, CommandWheelAnimation animation, UnitCommandAction selected,
            Color green, Color selectedFill, Font font, System.Func<UnitCommandAction, bool> enabled, float glowIntensity = 1, float glowRadius = 6,
            float persistence = .12f, Texture2D phosphorRaster = null, float textureStrength = .04f)
        {
            if (Event.current.type != EventType.Repaint) return;
            var oldRaster = raster; float oldStrength = rasterStrength;
            raster = phosphorRaster; rasterStrength = Mathf.Clamp(textureStrength, 0, .08f);
            try
            {
                // Four bounded samples; no frame buffer, per-frame allocations, or text ghosts.
                for (int i = 4; i >= 1; i--)
                {
                    float age = i * .04f;
                    float opacity = CommandWheelPhosphor.TrailOpacity(animation.Seconds, age, persistence);
                    if (opacity > .001f)
                        Geometry(bounds, ringCenter, new CommandWheelAnimation(animation.Seconds - age), Alpha(green, opacity));
                }
                Geometry(bounds, ringCenter, animation, green);
                Label(bounds, UnitCommandAction.Move, "Move", "", animation, selected, green, selectedFill, font, enabled(UnitCommandAction.Move), glowIntensity, glowRadius);
                Label(bounds, UnitCommandAction.Attack, "Attack", "", animation, selected, green, selectedFill, font, enabled(UnitCommandAction.Attack), glowIntensity, glowRadius);
                Label(bounds, UnitCommandAction.Overwatch, "Overwatch", " / AP 2", animation, selected, green, selectedFill, font, enabled(UnitCommandAction.Overwatch), glowIntensity, glowRadius);
                Label(bounds, UnitCommandAction.Eject, "Eject", " / AP 1", animation, selected, green, selectedFill, font, enabled(UnitCommandAction.Eject), glowIntensity, glowRadius);
                Label(bounds, UnitCommandAction.Reload, "Reload", "", animation, selected, green, selectedFill, font, enabled(UnitCommandAction.Reload), glowIntensity, glowRadius);
            }
            finally { raster = oldRaster; rasterStrength = oldStrength; }
        }
        private static void Geometry(Rect bounds, Vector2 ringCenter, CommandWheelAnimation animation, Color green)
        {
            float s = bounds.width / 720f;
            Vector2 center = ringCenter;
            CommandWheelHud.Draw(center, s, animation, green);
            float scale = .82f + .18f * animation.RingProgress;
            Color ring = Alpha(green, animation.RingProgress);
            Arc(center, 96 * s * scale, -90 + animation.Rotation, 360 * animation.RingReveal, 3 * s, ring);
            for (int i = 0; i < 4; i++)
                Arc(center, 78 * s * scale, i * 90 + 8 - animation.Rotation * .7f, 60 * animation.RingReveal, 2.5f * s, ring);
            if (animation.LineProgress > 0)
            {
                Connection(bounds, center, 96 * s, new Vector2(268, 106), new Vector2(35, 106), animation.LineProgress, green);
                Connection(bounds, center, 96 * s, new Vector2(452, 106), new Vector2(685, 106), animation.LineProgress, green);
                Connection(bounds, center, 96 * s, new Vector2(268, 334), new Vector2(35, 334), animation.LineProgress, green);
                Connection(bounds, center, 96 * s, new Vector2(452, 334), new Vector2(685, 334), animation.LineProgress, green);
            }
            CommandWheelHud.DrawTerminal(Point(bounds, new Vector2(35, 106)), s, false, 0, animation, green);
            CommandWheelHud.DrawTerminal(Point(bounds, new Vector2(685, 106)), s, true, 1, animation, green);
            CommandWheelHud.DrawTerminal(Point(bounds, new Vector2(35, 334)), s, false, 2, animation, green);
            CommandWheelHud.DrawTerminal(Point(bounds, new Vector2(685, 334)), s, true, 3, animation, green);
        }
        private static void Label(Rect bounds, UnitCommandAction action, string name, string cost,
            CommandWheelAnimation animation, UnitCommandAction selected, Color green, Color fill, Font font, bool enabled, float glowIntensity, float glowRadius)
        {
            Rect rect = ButtonRect(bounds, action); float s = bounds.width / 720f;
            bool chosen = selected == action;
            if (chosen)
            {
                Fill(rect, fill);
                Outline(rect, Mathf.Max(1.5f, 2 * s), green);
            }
            int count = animation.CharacterCount(name.Length + cost.Length);
            string word = name.Substring(0, Mathf.Min(count, name.Length));
            string resource = cost.Substring(0, Mathf.Clamp(count - name.Length, 0, cost.Length));
            var style = new GUIStyle { font = font, fontSize = Mathf.Max(10, Mathf.RoundToInt((action == UnitCommandAction.Reload ? 17 : 28) * s)),
                fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, wordWrap = false };
            style.normal.textColor = Alpha(Color.Lerp(green, new Color(.78f, 1, .83f, green.a), .42f), enabled || chosen ? 1 : .35f);
            var costStyle = new GUIStyle(style) { fontSize = Mathf.Max(9, Mathf.RoundToInt(20 * s)) };
            float wordWidth = style.CalcSize(new GUIContent(word)).x, costWidth = costStyle.CalcSize(new GUIContent(resource)).x;
            bool right = action == UnitCommandAction.Attack || action == UnitCommandAction.Eject;
            float x = action == UnitCommandAction.Reload ? rect.center.x - (wordWidth + costWidth) / 2 :
                right ? rect.xMax - wordWidth - costWidth - 4 * s : rect.x + 4 * s;
            GlowingLabel(new Rect(x, rect.y, wordWidth + 1, rect.height), word, style, glowRadius * s, glowIntensity);
            GlowingLabel(new Rect(x + wordWidth, rect.y, costWidth + 1, rect.height), resource, costStyle, glowRadius * s, glowIntensity);
            if (count > 0 && count < name.Length + cost.Length)
                Fill(new Rect(x + wordWidth + costWidth + 3 * s, rect.center.y - 9 * s, Mathf.Max(1, 1.5f * s), 18 * s), style.normal.textColor);
        }
        private static void GlowingLabel(Rect rect, string text, GUIStyle style, float radius, float intensity)
        {
            if (string.IsNullOrEmpty(text)) return;
            Color core = style.normal.textColor;
            if (radius > 0 && intensity > 0)
            {
                // Several low-alpha glyph shells approximate a soft outer glow without changing the solid text fill.
                for (int layer = 2; layer >= 0; layer--)
                {
                    float distance = radius * (layer == 2 ? 1 : layer == 1 ? .5f : .18f);
                    style.normal.textColor = Alpha(core, intensity * (layer == 2 ? .025f : layer == 1 ? .06f : .13f));
                    foreach (var direction in GlowDirections)
                        GUI.Label(new Rect(rect.position + direction * distance, rect.size), text, style);
                }
            }
            style.normal.textColor = core;
            GUI.Label(rect, text, style);
            // Darken only glyph pixels: no stripe is painted over the world between letters.
            if (rasterStrength > 0)
            {
                style.normal.textColor = new Color(0, 0, 0, core.a * rasterStrength);
                for (float y = 1; y < rect.height; y += 3)
                {
                    GUI.BeginGroup(new Rect(rect.x, rect.y + y, rect.width, 1));
                    GUI.Label(new Rect(0, -y, rect.width, rect.height), text, style);
                    GUI.EndGroup();
                }
                style.normal.textColor = core;
            }
        }
        private static Vector2 Point(Rect rect, Vector2 p) => rect.position + p * (rect.width / 720f);
        private static void Connection(Rect bounds, Vector2 center, float radius, Vector2 localElbow, Vector2 localEnd, float progress, Color color)
        {
            Vector2 elbow = Point(bounds, localElbow), end = Point(bounds, localEnd);
            var closest = new Vector2(Mathf.Clamp(center.x, Mathf.Min(elbow.x, end.x), Mathf.Max(elbow.x, end.x)), elbow.y);
            bool crossesRing = Vector2.Distance(closest, center) < radius + 2;
            Vector2 direction = (crossesRing ? end : elbow) - center;
            if (direction.sqrMagnitude < .01f) direction = Vector2.up;
            Vector2 start = center + direction.normalized * radius;
            if (crossesRing) elbow = start;
            float a = Vector2.Distance(start, elbow), b = Vector2.Distance(elbow, end), distance = (a + b) * progress;
            Vector2 tip = a > .01f ? Vector2.Lerp(start, elbow, distance / a) : start;
            float thickness = Mathf.Max(1.5f, 3 * bounds.width / 720f);
            Line(start, tip, thickness, color);
            if (distance > a && b > .01f) Line(elbow, Vector2.Lerp(elbow, end, (distance - a) / b), thickness, color);
            float s = bounds.width / 720f;
            var lead = distance > a && b > .01f ? Vector2.Lerp(elbow, end, (distance - a) / b) : tip;
            if (progress < .999f)
            {
                Fill(new Rect(lead.x - 4 * s, lead.y - 4 * s, 8 * s, 8 * s), Alpha(color, .15f));
                Fill(new Rect(lead.x - 1.5f * s, lead.y - 1.5f * s, 3 * s, 3 * s), color);
            }
            else Fill(new Rect(end.x - 1.5f * s, end.y - 1.5f * s, 3 * s, 3 * s), color);
        }
        internal static void Arc(Vector2 center, float radius, float start, float sweep, float width, Color color)
        {
            int count = Mathf.CeilToInt(sweep / 5);
            for (int i = 0; i < count; i++)
            {
                float a = (start + sweep * i / count) * Mathf.Deg2Rad, b = (start + sweep * (i + 1) / count) * Mathf.Deg2Rad;
                Line(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, Mathf.Max(1, width), color);
            }
        }
        internal static void Line(Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 delta = to - from; if (delta.sqrMagnitude < .01f || color.a <= 0) return;
            var matrix = GUI.matrix; var previous = GUI.color;
            GUI.color = Alpha(color, .12f); GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            GUI.DrawTexture(new Rect(from.x, from.y - width * 2, delta.magnitude, width * 4), Texture2D.whiteTexture);
            GUI.color = color; GUI.DrawTexture(new Rect(from.x, from.y - width / 2, delta.magnitude, width), Texture2D.whiteTexture);
            if (raster && rasterStrength > 0)
            {
                GUI.color = new Color(1, 1, 1, color.a * rasterStrength);
                GUI.DrawTextureWithTexCoords(new Rect(from.x, from.y - width / 2, delta.magnitude, width), raster,
                    new Rect(0, 0, delta.magnitude / 32f, width / 32f));
            }
            GUI.matrix = matrix; GUI.color = previous;
        }
        internal static void Fill(Rect rect, Color color)
        { var previous = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous; }
        private static void Outline(Rect r, float w, Color color)
        {
            Fill(new Rect(r.x, r.y, r.width, w), color); Fill(new Rect(r.x, r.yMax - w, r.width, w), color);
            Fill(new Rect(r.x, r.y, w, r.height), color); Fill(new Rect(r.xMax - w, r.y, w, r.height), color);
        }
        private static Color Alpha(Color color, float factor) { color.a *= factor; return color; }
    }
}
