using UnityEngine;

namespace MechaXCOM
{
    [DisallowMultipleComponent]
    public class BodyTargetUI : MonoBehaviour
    {
        private OverwatchDecisionController overwatch;
        private bool IsReaction => overwatch && overwatch.IsPending;
        private AttackPreview Preview => IsReaction ? overwatch.CurrentPreview : input ? input.CurrentAttackPreview : default;
        private BattleUnit Target => IsReaction ? overwatch.CurrentDecision.Value.Target : input ? input.TargetUnit : null;
        public string WeaponInfoText => Preview.WeaponName ?? string.Empty;
        public string AttackInfoText => AttackPreviewText.DamageAndAP(Preview);
        public string AmmoInfoText => WeaponAmmoText.Summary(Preview);
        public string AdditionalDamageInfoText => AttackPreviewText.AdditionalDamage(Preview);
        private float AdditionalRow => Preview.AdditionalHitPartCount > 0 ? 22 : 0;
        public string ContextInfoText => !Target ? string.Empty : IsReaction
            ? $"OVERWATCH: {overwatch.CurrentDecision.Value.Watcher.name}\n{Target.name} / {Target.Health.State.ToString().ToUpperInvariant()}"
            : $"{Target.name} / {Target.Health.State.ToString().ToUpperInvariant()}";
        private PlayerInputController input;
        private BodyTargetArt art;
        private static readonly TargetBodyPart[] HitOrder = {
            TargetBodyPart.Head, TargetBodyPart.Cockpit, TargetBodyPart.RightArm,
            TargetBodyPart.LeftArm, TargetBodyPart.Torso, TargetBodyPart.RightLeg, TargetBodyPart.LeftLeg };
        public void Initialize(PlayerInputController input)
        {
            this.input = input;
            art = Resources.Load<BodyTargetArt>("BodyTargetArt");
        }
        public void InitializeOverwatch(OverwatchDecisionController controller) => overwatch = controller;
        internal bool IsBoundToOverwatch(OverwatchDecisionController controller) => overwatch == controller;
        private float Scale => Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), .25f, 1f);
        public bool IsVisible => isActiveAndEnabled && (IsReaction || (input && input.CanChooseAction &&
            input.ActionMode == UnitActionMode.Attack && !input.IsIndirectAttackSelected && input.SelectedAttackType == AttackType.Ranged && input.TargetUnit));
        public Texture2D CurrentTexture => art ? art.GetImage(IsReaction ? overwatch.SelectedPart : input ? input.SelectedBodyPart : TargetBodyPart.Whole) : null;
        public Rect PanelRect => new Rect(Screen.width - 316 * Scale, (Screen.height - (650 + AdditionalRow) * Scale) / 2, 300 * Scale, (650 + AdditionalRow) * Scale);
        public Rect ImageRect => new Rect(PanelRect.x + 12 * Scale, PanelRect.y + 60 * Scale, 276 * Scale, 331 * Scale);
        public Rect FireButtonRect => new Rect(PanelRect.x + 12 * Scale, PanelRect.y + (603 + AdditionalRow) * Scale, (IsReaction ? 132 : 276) * Scale, 32 * Scale);
        public Rect SkipButtonRect => IsReaction ? new Rect(PanelRect.x + 156 * Scale, PanelRect.y + (603 + AdditionalRow) * Scale, 132 * Scale, 32 * Scale) : default;
        private Rect WholeButtonRect => new Rect(PanelRect.x + 12 * Scale, PanelRect.y + (561 + AdditionalRow) * Scale, 276 * Scale, 30 * Scale);
        public string SelectedPartInfo
        {
            get
            {
                if (!Target) return string.Empty;
                var preview = Preview;
                return $"{BodyTargetRules.Label(preview.TargetPart)} {preview.PartHP}/{preview.PartMaxHP} / 명중률 {preview.HitChance}%\n" +
                    $"장갑 ×{preview.ArmorMultiplier:0.##} / 치명타 {preview.CriticalChance:0.##}% ×{preview.CriticalDamageMultiplier:0.##}";
            }
        }
        public string GetPartHPText(TargetBodyPart part)
        {
            if (!Target) return string.Empty;
            var health = Target.Health;
            int hp = health.GetCurrentHP(part);
            return $"{BodyTargetRules.Label(BodyTargetRules.Normalize(part))} {hp}/{health.GetMaxHP(part)}{(hp == 0 ? " 파괴" : "")}";
        }
        public Rect GetPartRect(TargetBodyPart part)
        {
            // Normalized hit areas on the diagram, from the mech's own left/right perspective.
            Rect area;
            switch (part)
            {
                case TargetBodyPart.Head: area = new Rect(.38f, .015f, .2f, .16f); break;
                case TargetBodyPart.Cockpit: area = new Rect(.40f, .25f, .09f, .13f); break;
                case TargetBodyPart.Torso: area = new Rect(.32f, .17f, .38f, .30f); break;
                case TargetBodyPart.RightArm: area = new Rect(.18f, .06f, .22f, .49f); break;
                case TargetBodyPart.LeftArm: area = new Rect(.64f, .10f, .17f, .47f); break;
                case TargetBodyPart.RightLeg: area = new Rect(.19f, .47f, .33f, .49f); break;
                case TargetBodyPart.LeftLeg: area = new Rect(.55f, .47f, .26f, .51f); break;
                default: return default;
            }
            var image = ImageRect;
            return new Rect(image.x + area.x * image.width, image.y + area.y * image.height,
                area.width * image.width, area.height * image.height);
        }
        public bool ContainsScreenPoint(Vector2 point) => IsVisible && PanelRect.Contains(new Vector2(point.x, Screen.height - point.y));
        public void HandleScreenClick(Vector2 point)
        {
            if (!ContainsScreenPoint(point)) return;
            var guiPoint = new Vector2(point.x, Screen.height - point.y);
            if (IsReaction)
            {
                if (FireButtonRect.Contains(guiPoint)) { if (Preview.CanAttack) overwatch.TryFire(); return; }
                if (SkipButtonRect.Contains(guiPoint)) { overwatch.TrySkip(); return; }
                if (WholeButtonRect.Contains(guiPoint)) { overwatch.TrySelectPart(TargetBodyPart.Torso); return; }
                foreach (var part in HitOrder)
                    if (GetPartRect(part).Contains(guiPoint)) { overwatch.TrySelectPart(part); return; }
                return;
            }
            if (FireButtonRect.Contains(guiPoint))
            {
                if (input.CurrentAttackPreview.CanAttack) input.TryAttackSelected();
                return;
            }
            if (WholeButtonRect.Contains(guiPoint)) { input.TrySelectBodyPart(TargetBodyPart.Whole); return; }
            foreach (var part in HitOrder)
                if (GetPartRect(part).Contains(guiPoint)) { input.TrySelectBodyPart(part); return; }
        }
        private void OnGUI()
        {
            if (!IsVisible) return;
            int oldDepth = GUI.depth; GUI.depth = -10;
            var label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14 * Scale), alignment = TextAnchor.MiddleCenter };
            var button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(14 * Scale) };
            var panel = PanelRect;
            GUI.Box(panel, GUIContent.none);
            var header = new GUIStyle(label) { fontSize = Mathf.RoundToInt((IsReaction ? 12 : 14) * Scale) };
            ResourceTypography.Label(new Rect(panel.x + 6 * Scale, panel.y + 2 * Scale, panel.width - 12 * Scale, 30 * Scale), ContextInfoText, header);
            ResourceTypography.Label(new Rect(panel.x + 12 * Scale, panel.y + 32 * Scale, panel.width - 24 * Scale, 24 * Scale), WeaponInfoText, label);
            if (CurrentTexture) GUI.DrawTexture(ImageRect, CurrentTexture, ScaleMode.StretchToFill, true);
            else ResourceTypography.Label(ImageRect, "Target diagram unavailable", label);
            var preview = Preview;
            ResourceTypography.Label(new Rect(panel.x, panel.y + 393 * Scale, panel.width, 44 * Scale),
                SelectedPartInfo, label);
            ResourceTypography.Label(new Rect(panel.x, panel.y + 437 * Scale, panel.width, 22 * Scale),
                AttackInfoText, label);
            if (AdditionalRow > 0)
                ResourceTypography.Label(new Rect(panel.x, panel.y + 459 * Scale, panel.width, 22 * Scale), AdditionalDamageInfoText, label);
            ResourceTypography.Label(new Rect(panel.x, panel.y + (459 + AdditionalRow) * Scale, panel.width, 22 * Scale), AmmoInfoText, label);
            for (int i = 0; i < 7; i++)
            {
                var part = (TargetBodyPart)(i + 1);
                ResourceTypography.Label(new Rect(panel.x + (6 + (i % 2) * 145) * Scale,
                    panel.y + (483 + AdditionalRow + (i / 2) * 18) * Scale, 145 * Scale, 18 * Scale), GetPartHPText(part), label);
            }
            GUI.Button(WholeButtonRect, "기본 조준 (Body)", button);
            bool oldEnabled = GUI.enabled; GUI.enabled = oldEnabled && preview.CanAttack;
            GUI.Button(FireButtonRect, "Fire", button);
            GUI.enabled = oldEnabled;
            if (IsReaction) GUI.Button(SkipButtonRect, "Skip", button);
            GUI.depth = oldDepth;
        }
        private void OnDisable() { if (overwatch) overwatch.Suspend(); }
        private void OnEnable() { if (overwatch) overwatch.RefreshRegistration(); }
    }
}

