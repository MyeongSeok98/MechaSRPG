using UnityEngine;

namespace MechaXCOM
{
    [DisallowMultipleComponent]
    public class BodyTargetUI : MonoBehaviour
    {
        public string WeaponInfoText => input ? input.CurrentAttackPreview.WeaponName : string.Empty;
        public string AttackInfoText => input ? AttackPreviewText.DamageAndAP(input.CurrentAttackPreview) : string.Empty;
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
        private float Scale => Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), .25f, 1f);
        public bool IsVisible => isActiveAndEnabled && input && input.CanChooseAction &&
            input.ActionMode == UnitActionMode.Attack && input.SelectedAttackType == AttackType.Ranged && input.TargetUnit;
        public Texture2D CurrentTexture => art ? art.GetImage(input ? input.SelectedBodyPart : TargetBodyPart.Whole) : null;
        public Rect PanelRect => new Rect(Screen.width - 316 * Scale, (Screen.height - 630 * Scale) / 2, 300 * Scale, 630 * Scale);
        public Rect ImageRect => new Rect(PanelRect.x + 12 * Scale, PanelRect.y + 60 * Scale, 276 * Scale, 331 * Scale);
        public Rect FireButtonRect => new Rect(PanelRect.x + 12 * Scale, PanelRect.y + 583 * Scale, 276 * Scale, 32 * Scale);
        private Rect WholeButtonRect => new Rect(PanelRect.x + 12 * Scale, PanelRect.y + 541 * Scale, 276 * Scale, 30 * Scale);
        public string SelectedPartInfo
        {
            get
            {
                if (!input || !input.TargetUnit) return string.Empty;
                var preview = input.CurrentAttackPreview;
                return $"{BodyTargetRules.Label(preview.TargetPart)} {preview.PartHP}/{preview.PartMaxHP} / 명중률 {preview.HitChance}%";
            }
        }
        public string GetPartHPText(TargetBodyPart part)
        {
            if (!input || !input.TargetUnit) return string.Empty;
            var health = input.TargetUnit.Health;
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
            GUI.Label(new Rect(panel.x, panel.y + 6 * Scale, panel.width, 26 * Scale),
                $"{input.TargetUnit.name} / {input.TargetUnit.Health.State.ToString().ToUpperInvariant()}", label);
            GUI.Label(new Rect(panel.x + 12 * Scale, panel.y + 32 * Scale, panel.width - 24 * Scale, 24 * Scale), WeaponInfoText, label);
            if (CurrentTexture) GUI.DrawTexture(ImageRect, CurrentTexture, ScaleMode.StretchToFill, true);
            else GUI.Label(ImageRect, "Target diagram unavailable", label);
            var preview = input.CurrentAttackPreview;
            GUI.Label(new Rect(panel.x, panel.y + 393 * Scale, panel.width, 24 * Scale),
                SelectedPartInfo, label);
            GUI.Label(new Rect(panel.x, panel.y + 415 * Scale, panel.width, 24 * Scale),
                AttackInfoText, label);
            for (int i = 0; i < 7; i++)
            {
                var part = (TargetBodyPart)(i + 1);
                GUI.Label(new Rect(panel.x + (6 + (i % 2) * 145) * Scale,
                    panel.y + (443 + (i / 2) * 22) * Scale, 145 * Scale, 22 * Scale), GetPartHPText(part), label);
            }
            GUI.Button(WholeButtonRect, "기본 조준 (Body)", button);
            bool oldEnabled = GUI.enabled; GUI.enabled = oldEnabled && preview.CanAttack;
            GUI.Button(FireButtonRect, "Fire", button);
            GUI.enabled = oldEnabled; GUI.depth = oldDepth;
        }
    }
}
