using UnityEngine;

namespace MechaXCOM
{
    [DisallowMultipleComponent]
    public class CoverTargetUI : MonoBehaviour
    {
        private PlayerInputController input;
        public void Initialize(PlayerInputController value) => input = value;
        private float Scale => Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), .25f, 1f);
        public bool IsVisible => isActiveAndEnabled && input && input.CanChooseAction && input.TargetCover;
        public Rect PanelRect => new Rect(Screen.width - 316 * Scale, (Screen.height - 260 * Scale) / 2, 300 * Scale, 260 * Scale);
        public Rect FireButtonRect => new Rect(PanelRect.x + 12 * Scale, PanelRect.y + 210 * Scale, 170 * Scale, 34 * Scale);
        public Rect CancelButtonRect => new Rect(PanelRect.x + 190 * Scale, PanelRect.y + 210 * Scale, 98 * Scale, 34 * Scale);
        public string TargetInfo
        {
            get
            {
                if (!input || !input.TargetCover) return string.Empty;
                var health = input.TargetCover.GetComponent<CoverDurability>();
                return $"{input.TargetCover.name}\nHP {health.CurrentHP}/{health.MaxHP}";
            }
        }
        public bool ContainsScreenPoint(Vector2 point) => IsVisible && PanelRect.Contains(new Vector2(point.x, Screen.height - point.y));
        public void HandleScreenClick(Vector2 point)
        {
            if (!ContainsScreenPoint(point)) return;
            var p = new Vector2(point.x, Screen.height - point.y);
            if (CancelButtonRect.Contains(p)) input.CancelCoverAim();
            else if (FireButtonRect.Contains(p) && input.CurrentCoverPreview.CanAttack) input.TryFireCoverSelected();
        }
        private void OnGUI()
        {
            if (!IsVisible) return;
            var preview = input.CurrentCoverPreview; var panel = PanelRect;
            var label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14 * Scale), wordWrap = true };
            var title = new GUIStyle(label) { fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(18 * Scale) };
            var button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(14 * Scale) };
            int depth = GUI.depth; GUI.depth = -10; GUI.Box(panel, GUIContent.none);
            ResourceTypography.Label(new Rect(panel.x + 12 * Scale, panel.y + 10 * Scale, 276 * Scale, 30 * Scale),
                input.TargetCover.GetComponent<MissionTarget>() ? "목표 시설 직접 공격" : input.TargetCover.GetComponent<CoverDurability>().Data ? "시설 직접 공격" : "엄폐물 직접 공격", title);
            ResourceTypography.Label(new Rect(panel.x + 12 * Scale, panel.y + 44 * Scale, 276 * Scale, 52 * Scale), TargetInfo, label);
            ResourceTypography.Label(new Rect(panel.x + 12 * Scale, panel.y + 100 * Scale, 276 * Scale, 55 * Scale),
                $"{preview.WeaponName}\n피해 {preview.MinDamage}-{preview.MaxDamage} / 확정 명중", label);
            ResourceTypography.Label(new Rect(panel.x + 12 * Scale, panel.y + 160 * Scale, 276 * Scale, 46 * Scale),
                preview.CanAttack ? (preview.UsesAmmo ? $"AP {preview.APCost} / 탄약 {preview.AmmoLoaded}/{preview.MagazineCapacity}" : $"AP {preview.APCost}") : preview.FailureReason, label);
            bool enabled = GUI.enabled; GUI.enabled = enabled && preview.CanAttack;
            GUI.Button(FireButtonRect, preview.Type == AttackType.Melee ? "Attack / 공격 확정" : "Fire / 사격 확정", button);
            GUI.enabled = enabled; GUI.Button(CancelButtonRect, "취소", button); GUI.depth = depth;
        }
    }
}

