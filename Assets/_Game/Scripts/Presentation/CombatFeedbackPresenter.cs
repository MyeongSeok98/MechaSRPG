using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM
{
    [DisallowMultipleComponent]
    public class CombatFeedbackPresenter : MonoBehaviour
    {
        [SerializeField, Range(.05f, 3f), InspectorName("팝업 표시 시간")] private float duration = .8f;
        private CombatSystem combat;
        private TurnManager turns;
        private Camera battleCamera;
        private readonly List<CombatFeedbackEntry> entries = new List<CombatFeedbackEntry>();
        private float startedAt, activeDuration;
        private bool subscribed;
        public IReadOnlyList<CombatFeedbackEntry> Entries => entries.AsReadOnly();
        private bool Ready => Application.isPlaying && isActiveAndEnabled && combat && combat.isActiveAndEnabled &&
            turns && turns.isActiveAndEnabled && battleCamera && battleCamera.isActiveAndEnabled;
        public void Initialize(CombatSystem system, TurnManager manager, Camera camera)
        {
            Unsubscribe(); Clear(); combat = system; turns = manager; battleCamera = camera; Subscribe();
        }
        private void Subscribe()
        {
            if (subscribed || !isActiveAndEnabled || !combat) return;
            combat.AttackFeedback += Show; subscribed = true;
        }
        private void Unsubscribe()
        {
            if (subscribed && combat) combat.AttackFeedback -= Show;
            subscribed = false;
        }
        private void Show(IReadOnlyList<CombatFeedbackEntry> result)
        {
            if (!Ready || result.Count == 0) return;
            entries.Clear(); entries.AddRange(result); startedAt = Time.unscaledTime;
            activeDuration = float.IsNaN(duration) || float.IsInfinity(duration) ? .8f : Mathf.Clamp(duration, .05f, 3f);
            turns.HoldAttackPresentation(this, activeDuration);
        }
        private void Clear()
        {
            entries.Clear();
            if (turns) turns.ReleaseAttackPresentation(this);
        }
        private void Update()
        {
            if (entries.Count > 0 && (!Ready || Time.unscaledTime - startedAt >= activeDuration)) Clear();
        }
        private void OnEnable() => Subscribe();
        private void OnDisable() { Unsubscribe(); Clear(); }
        private void OnDestroy() { Unsubscribe(); Clear(); }
        private void OnGUI()
        {
            if (!Ready || entries.Count == 0) return;
            float progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / activeDuration);
            // At the default 0.8s: pop up for 0.15s, hold for 0.5s, fade in place for 0.15s.
            const float riseEnd = .1875f, holdEnd = .8125f;
            float riseTime = Mathf.Clamp01(progress / riseEnd) - 1;
            const float overshoot = 1.70158f;
            float riseEase = 1 + (overshoot + 1) * riseTime * riseTime * riseTime + overshoot * riseTime * riseTime;
            float riseOffset = (1 - riseEase) * 32;
            float alpha = 1 - Mathf.InverseLerp(holdEnd, 1, progress);
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), .25f, 1f);
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(19 * scale), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter, wordWrap = false };
            int depth = GUI.depth; GUI.depth = -50;
            foreach (var entry in entries)
            {
                var point = battleCamera.WorldToScreenPoint(entry.WorldPosition);
                if (point.z <= 0) continue;
                float height = style.CalcHeight(new GUIContent(entry.Text), 260 * scale);
                var rect = new Rect(point.x - 130 * scale, Screen.height - point.y - 35 * scale + riseOffset * scale - height,
                    260 * scale, height + 4 * scale);
                style.normal.textColor = new Color(0, 0, 0, alpha);
                GUI.Label(new Rect(rect.x + scale, rect.y + scale, rect.width, rect.height), entry.Text, style);
                Color color = entry.IsMiss ? Color.white : entry.IsCritical ? new Color(1, .78f, .15f) : new Color(1, .35f, .25f);
                color.a = alpha; style.normal.textColor = color; GUI.Label(rect, entry.Text, style);
            }
            GUI.depth = depth;
        }
    }
}
