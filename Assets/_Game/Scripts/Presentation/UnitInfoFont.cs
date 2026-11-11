using UnityEngine;
using UnityEngine.UI;

namespace MechaXCOM
{
    internal static class UnitInfoFont
    {
        internal static void Apply(Text label, Font display, Font fallback)
        {
            if (!label) return;
            bool supported = display;
            if (supported)
                foreach (char character in label.text)
                    // These UI display fonts are Latin fonts. Unity's OS substitution can make
                    // HasCharacter report Hangul as available; choose the Korean font explicitly.
                    if (IsHangul(character) || (!char.IsWhiteSpace(character) && !display.HasCharacter(character)))
                    { supported = false; break; }
            label.font = supported ? display : fallback;
        }
        private static bool IsHangul(char value) => (value >= '\uAC00' && value <= '\uD7AF') ||
            (value >= '\u1100' && value <= '\u11FF') || (value >= '\u3130' && value <= '\u318F') ||
            (value >= '\uA960' && value <= '\uA97F') || (value >= '\uD7B0' && value <= '\uD7FF');
    }
}
