using UnityEngine;

namespace MechaXCOM
{
    public enum TurnBannerPhase { Hidden, Enter, Hold, Exit }
    public readonly struct TurnBannerFrame
    {
        public Rect Rect { get; }
        public float Alpha { get; }
        public TurnBannerPhase Phase { get; }
        public bool Visible => Phase != TurnBannerPhase.Hidden;
        public TurnBannerFrame(Rect rect, float alpha, TurnBannerPhase phase)
        { Rect = rect; Alpha = alpha; Phase = phase; }
    }
    public static class TurnBannerAnimation
    {
        public static TurnBannerFrame Evaluate(float elapsed, float enter, float hold, float exit, Rect centered, float screenWidth)
        {
            enter = Mathf.Max(.001f, enter); hold = Mathf.Max(0, hold); exit = Mathf.Max(.001f, exit);
            elapsed = Mathf.Max(0, elapsed);
            if (elapsed >= enter + hold + exit) return default;
            if (elapsed < enter)
            {
                float t = elapsed / enter;
                centered.x = Mathf.Lerp(screenWidth, centered.x, 1 - Mathf.Pow(1 - t, 3));
                return new TurnBannerFrame(centered, 1, TurnBannerPhase.Enter);
            }
            if (elapsed < enter + hold) return new TurnBannerFrame(centered, 1, TurnBannerPhase.Hold);
            float progress = Mathf.Clamp01((elapsed - enter - hold) / exit);
            centered.x = Mathf.Lerp(centered.x, -centered.width, progress * progress);
            return new TurnBannerFrame(centered, Mathf.SmoothStep(1, 0, progress), TurnBannerPhase.Exit);
        }
    }
}
