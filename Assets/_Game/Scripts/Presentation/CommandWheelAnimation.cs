using UnityEngine;

namespace MechaXCOM
{
    public readonly struct CommandWheelAnimation
    {
        public float Seconds { get; }
        public CommandWheelAnimation(float seconds) { Seconds = Mathf.Max(0, seconds); }
        public float RingProgress => Ease(Seconds / .43f);
        public float RingReveal => Mathf.Clamp01(Seconds / .34f);
        public float Rotation => -207 * (1 - Ease(Seconds / .47f));
        public float LineProgress => Ease((Seconds - .44f) / .28f);
        public const float Duration = 1.38f;
        public bool IsComplete => Seconds >= Duration;
        public float AlignmentProgress => Ease((Seconds - .08f) / .37f);
        public float ScanProgress => Mathf.Clamp01((Seconds - .13f) / .47f);
        public float ScanOpacity => Seconds <= .13f || Seconds >= .74f ? 0 : .32f * (1 - Mathf.Clamp01((Seconds - .6f) / .14f));
        public float SectorProgress => Ease((Seconds - .12f) / .48f);
        public float SectorRotation => 185 * (1 - Ease((Seconds - .12f) / .65f));
        public float FinalLockProgress => Mathf.Clamp01((Seconds - 1.12f) / .2f);
        public float ChannelLockProgress(int index) => index < 0 || index > 3 ? 0 : Mathf.Clamp01((Seconds - .78f - index * .065f) / .18f);
        public int CharacterCount(int length) => Seconds < .44f ? 0 :
            Mathf.Clamp(Mathf.FloorToInt((Seconds - .44f) / .024f) + 1, 0, length);
        internal static float Ease(float t) => 1 - Mathf.Pow(1 - Mathf.Clamp01(t), 3);
    }
}
