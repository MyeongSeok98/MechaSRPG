using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MechaXCOM
{
    // IMGUI has no inline font tag. Draw resource runs separately so Korean text keeps its font.
    internal static class ResourceTypography
    {
        private static Font resourceFont;
        private static readonly Regex ResourcesPattern = new Regex(
            @"\b(?:HP|AP|Ammo)\b(?:\s*:?\s*\d+(?:\.\d+)?(?:/\d+)?)?|\d+(?:\.\d+)?\s+AP\b|\d+/\d+|(?<=(?:예비 팩 |예비 탄창 |paid: |prepaid: ))\d+(?:\.\d+)?",
            RegexOptions.CultureInvariant);
        private static readonly Regex Words = new Regex(@"\n|[^\S\n]+|[^\s]+", RegexOptions.CultureInvariant);
        private struct Run
        {
            public string Text;
            public bool Resource;
            public float Width;
        }

        public static void Label(Rect rect, string text, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!resourceFont) resourceFont = UnityEngine.Resources.Load<Font>("Oxanium-Regular");
            var matches = ResourcesPattern.Matches(text);
            if (!resourceFont || matches.Count == 0) { GUI.Label(rect, text, style); return; }

            var mask = new bool[text.Length];
            foreach (Match match in matches)
                for (int i = match.Index; i < match.Index + match.Length; i++) mask[i] = true;
            var normal = TextStyle(style);
            var resource = new GUIStyle(normal) { font = resourceFont };
            float available = Mathf.Max(1, rect.width - style.padding.horizontal);
            float lineHeight = Mathf.Max(normal.lineHeight, resource.lineHeight);
            var lines = new List<List<Run>> { new List<Run>() };
            var widths = new List<float> { 0 };
            foreach (Match word in Words.Matches(text))
            {
                if (word.Value == "\n") { lines.Add(new List<Run>()); widths.Add(0); continue; }
                var runs = new List<Run>();
                int end = word.Index + word.Length;
                for (int start = word.Index; start < end;)
                {
                    int next = start + 1;
                    while (next < end && mask[next] == mask[start]) next++;
                    string value = text.Substring(start, next - start);
                    runs.Add(new Run { Text = value, Resource = mask[start],
                        Width = (mask[start] ? resource : normal).CalcSize(new GUIContent(value)).x });
                    start = next;
                }
                float wordWidth = 0; foreach (var run in runs) wordWidth += run.Width;
                int line = lines.Count - 1;
                if (style.wordWrap && widths[line] > 0 && widths[line] + wordWidth > available && !char.IsWhiteSpace(word.Value[0]))
                { lines.Add(new List<Run>()); widths.Add(0); line++; }
                lines[line].AddRange(runs); widths[line] += wordWidth;
            }
            int horizontal = (int)style.alignment % 3, vertical = (int)style.alignment / 3;
            float y = style.padding.top + Mathf.Max(0, rect.height - style.padding.vertical - lines.Count * lineHeight) * vertical / 2;
            GUI.BeginGroup(rect);
            for (int line = 0; line < lines.Count; line++)
            {
                float x = style.padding.left + Mathf.Max(0, available - widths[line]) * horizontal / 2;
                foreach (var run in lines[line])
                {
                    GUI.Label(new Rect(x, y, run.Width + 1, lineHeight), run.Text, run.Resource ? resource : normal);
                    x += run.Width;
                }
                y += lineHeight;
            }
            GUI.EndGroup();
        }

        public static void LayoutLabel(string text, GUIStyle style, params GUILayoutOption[] options)
        {
            var content = new GUIContent(text);
            Label(GUILayoutUtility.GetRect(content, style, options), text, style);
        }

        public static void Button(Rect rect, string text, GUIStyle style)
        {
            // Existing Input System handlers own clicks; keep the original button rectangle and state.
            if (!ResourcesPattern.IsMatch(text)) { GUI.Button(rect, text, style); return; }
            GUI.Button(rect, GUIContent.none, style);
            var label = TextStyle(style);
            label.padding = new RectOffset(style.padding.left, style.padding.right, style.padding.top, style.padding.bottom);
            label.alignment = style.alignment;
            label.wordWrap = style.wordWrap;
            Label(rect, text, label);
        }

        private static GUIStyle TextStyle(GUIStyle source)
        {
            // Cloning "button" keeps its native skin identity and frame even after clearing textures.
            // Start with an undecorated style and copy only typography, never button geometry.
            var result = new GUIStyle { font = source.font, fontSize = source.fontSize, fontStyle = source.fontStyle,
                richText = source.richText, alignment = TextAnchor.UpperLeft, wordWrap = false,
                clipping = TextClipping.Clip, imagePosition = ImagePosition.TextOnly };
            result.normal.textColor = source.normal.textColor;
            result.hover.textColor = source.hover.textColor;
            result.active.textColor = source.active.textColor;
            result.focused.textColor = source.focused.textColor;
            result.onNormal.textColor = source.onNormal.textColor;
            result.onHover.textColor = source.onHover.textColor;
            result.onActive.textColor = source.onActive.textColor;
            result.onFocused.textColor = source.onFocused.textColor;
            return result;
        }
    }
}
