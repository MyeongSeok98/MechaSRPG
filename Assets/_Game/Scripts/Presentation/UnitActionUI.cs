using UnityEngine;

namespace MechaXCOM
{
    [DisallowMultipleComponent]
    public class UnitActionUI : MonoBehaviour
    {
        private bool IsOverwatchWeaponMenu => input && input.ActionMode == UnitActionMode.OverwatchWeapon;
        private bool IsOverwatchFacing => input && input.ActionMode == UnitActionMode.OverwatchFacing;
        private bool IsDirectionMenu => input && (input.ActionMode == UnitActionMode.Facing || IsOverwatchFacing);
        private System.Collections.Generic.IReadOnlyList<WeaponSlot> WeaponChoices => IsOverwatchWeaponMenu
            ? input.GetOverwatchWeaponSlots() : input.GetAvailableWeaponSlots();
        public Rect GetWeaponButtonRect(WeaponSlot slot)
        {
            if (!input || (!input.IsWeaponMenuOpen && !IsOverwatchWeaponMenu)) return default;
            var slots = WeaponChoices;
            for (int i = 0; i < slots.Count; i++) if (slots[i] == slot) return AttackRow(i);
            return default;
        }
        public string GetWeaponButtonText(WeaponSlot slot)
        {
            if (!input || !input.SelectedUnit || !input.SelectedUnit.Combat.TryGetAttack(slot, out var stats, out _)) return string.Empty;
            var loadout = input.SelectedUnit.GetComponent<UnitWeaponLoadout>();
            var grip = slot == WeaponSlot.LeftHand ? loadout.LeftGrip : loadout.RightGrip;
            string hand = grip == GripMode.TwoHands ? "Both hands" : slot == WeaponSlot.LeftHand ? "Left" : "Right";
            return $"{hand}: {stats.DisplayName}";
        }
        public string WeaponInfoText => input ? input.CurrentAttackPreview.WeaponName : string.Empty;
        public string AttackInfoText => input ? AttackPreviewText.HitAndDamage(input.CurrentAttackPreview) : string.Empty;
        public string UnitStateText => !input || !input.SelectedUnit ? string.Empty :
            input.SelectedUnit.Overwatch && input.SelectedUnit.Overwatch.IsActive ? "ACTIVE / OVERWATCH" :
            input.SelectedUnit.Health.State.ToString().ToUpperInvariant();
        private PlayerInputController input;
        private Camera battleCamera;
        private BodyTargetUI bodyTargetUI;
        public void Initialize(PlayerInputController input, Camera battleCamera)
        { this.input = input; this.battleCamera = battleCamera; bodyTargetUI = GetComponent<BodyTargetUI>(); }
        private float Scale => Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), .25f, 1f);
        public bool IsVisible => isActiveAndEnabled && input && (input.CanManageSelectedUnit || input.IsFacingPanelVisible) &&
            battleCamera && (IsDirectionMenu || IsOverwatchWeaponMenu || input.ActionMode == UnitActionMode.Menu ||
                (input.ActionMode == UnitActionMode.Attack && input.TargetUnit)) && InView();
        private bool InView()
        {
            var point = battleCamera.WorldToViewportPoint(input.SelectedUnit.transform.position);
            return point.z > 0 && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1;
        }
        public bool TryGetPanelRect(out Rect rect)
        {
            rect = default;
            if (!IsVisible) return false;
            var point = battleCamera.WorldToScreenPoint(input.SelectedUnit.transform.position);
            float width = 190 * Scale;
            bool target = input.ActionMode == UnitActionMode.Attack && input.TargetUnit;
            float height = (IsDirectionMenu ? 224 : target ? 320 : 192) * Scale;
            // Additional action rows grow upward from the original three-action menu's lower edge,
            // rather than covering nearby unit clicks below that edge.
            float verticalOffset = IsDirectionMenu || target ? height / 2 : height - 80 * Scale;
            float rightEdge = bodyTargetUI && bodyTargetUI.IsVisible
                ? bodyTargetUI.PanelRect.xMin - 8 * Scale : Screen.width - 8 * Scale;
            float x = point.x + 24 * Scale;
            if (x + width > rightEdge) x = point.x - width - 24 * Scale;
            rect = new Rect(Mathf.Clamp(x, 8 * Scale, rightEdge - width),
                Mathf.Clamp(Screen.height - point.y - verticalOffset, 8 * Scale, Screen.height - height - 8 * Scale), width, height);
            return true;
        }
        private Rect Row(int row)
        {
            if (!TryGetPanelRect(out var rect)) return default;
            return new Rect(rect.x + 8 * Scale, rect.y + (52 + row * 32) * Scale, rect.width - 16 * Scale, 28 * Scale);
        }
        public Rect MoveButtonRect => Row(0);
        public Rect AttackButtonRect => Row(1);
        public Rect EjectButtonRect => Row(2);
        public Rect OverwatchButtonRect => input && input.ActionMode == UnitActionMode.Attack ? default : Row(3);
        public Rect FireButtonRect => Row(7);
        public Rect GetFacingButtonRect(FacingDirection facing) => Row((int)facing);
        public Rect ConfirmFacingButtonRect => Row(4);
        public bool TryGetAttackMenuRect(out Rect rect)
        {
            rect = default;
            if (!input || (!input.IsAttackMenuOpen && !input.IsWeaponMenuOpen && !IsOverwatchWeaponMenu) || !TryGetPanelRect(out var main)) return false;
            float width = 220 * Scale, gap = 8 * Scale, height = 108 * Scale;
            float x = main.xMax + gap;
            if (x + width > Screen.width - gap) x = main.xMin - gap - width;
            rect = new Rect(Mathf.Clamp(x, gap, Screen.width - width - gap),
                Mathf.Clamp(main.y, gap, Screen.height - height - gap), width, height);
            return true;
        }
        private Rect AttackRow(int row)
        {
            if (!TryGetAttackMenuRect(out var rect)) return default;
            return new Rect(rect.x + 8 * Scale, rect.y + (32 + row * 32) * Scale, rect.width - 16 * Scale, 28 * Scale);
        }
        private Vector2 GuiPoint(Vector2 screenPoint) => new Vector2(screenPoint.x, Screen.height - screenPoint.y);
        public bool ContainsScreenPoint(Vector2 point) =>
            (TryGetPanelRect(out var rect) && rect.Contains(GuiPoint(point))) ||
            (TryGetAttackMenuRect(out var choices) && choices.Contains(GuiPoint(point)));
        public void HandleScreenClick(Vector2 point)
        {
            if (!ContainsScreenPoint(point)) return;
            var guiPoint = GuiPoint(point);
            if (IsDirectionMenu)
            {
                for (int i = 0; i < 4; i++)
                    if (GetFacingButtonRect((FacingDirection)i).Contains(guiPoint))
                    {
                        if (IsOverwatchFacing) input.TryChooseOverwatchFacing((FacingDirection)i);
                        else input.TryChooseFacing((FacingDirection)i);
                        return;
                    }
                if (ConfirmFacingButtonRect.Contains(guiPoint))
                {
                    if (IsOverwatchFacing) input.TryConfirmOverwatch(); else input.TryConfirmFacing();
                }
                return;
            }
            if ((input.IsWeaponMenuOpen || IsOverwatchWeaponMenu) && TryGetAttackMenuRect(out var weaponMenu) && weaponMenu.Contains(guiPoint))
            {
                foreach (var slot in WeaponChoices)
                    if (GetWeaponButtonRect(slot).Contains(guiPoint))
                    {
                        if (IsOverwatchWeaponMenu) input.BeginOverwatch(slot); else input.BeginWeaponAttack(slot);
                        return;
                    }
                return;
            }
            if (MoveButtonRect.Contains(guiPoint)) input.BeginMove();
            else if (EjectButtonRect.Contains(guiPoint)) input.TryEjectSelected();
            else if (AttackButtonRect.Contains(guiPoint)) input.OpenAttackMenu();
            else if (OverwatchButtonRect.Contains(guiPoint)) input.OpenOverwatchMenu();
            else if (input.ActionMode == UnitActionMode.Attack && input.TargetUnit &&
                FireButtonRect.Contains(guiPoint) && input.CurrentAttackPreview.CanAttack) input.TryAttackSelected();
        }
        private void OnGUI()
        {
            if (!TryGetPanelRect(out var rect)) return;
            int oldDepth = GUI.depth; GUI.depth = -5;
            var title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * Scale), fontStyle = FontStyle.Bold };
            var text = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * Scale), wordWrap = true };
            var button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(14 * Scale) };
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 8 * Scale, rect.y + 5 * Scale, rect.width - 16 * Scale, 24 * Scale),
                $"{input.SelectedUnit.name} / AP {input.SelectedUnit.ActionPoints.CurrentPoints}", title);
            GUI.Label(new Rect(rect.x + 8 * Scale, rect.y + 27 * Scale, rect.width - 16 * Scale, 20 * Scale),
                UnitStateText, text);
            bool oldEnabled = GUI.enabled;
            if (IsDirectionMenu)
            {
                GUI.enabled = oldEnabled && (IsOverwatchFacing ? input.CanConfirmOverwatch : input.CanChooseFacing);
                string[] names = { "North (+Z)", "East (+X)", "South (-Z)", "West (-X)" };
                var selectedFacing = IsOverwatchFacing ? input.SelectedOverwatchFacing : input.SelectedFacing;
                for (int i = 0; i < 4; i++)
                    GUI.Button(GetFacingButtonRect((FacingDirection)i), names[i] + (selectedFacing == (FacingDirection)i ? " [selected]" : ""), button);
                GUI.Button(ConfirmFacingButtonRect, IsOverwatchFacing ? "Confirm / AP 2" :
                    input.SelectedUnit.IsConfirmingFacing ? "Turning..." : "Confirm / AP 0", button);
                GUI.enabled = oldEnabled;
                GUI.depth = oldDepth;
                return;
            }
            // Input System clicks are handled once by PlayerInputController before map raycasts.
            // IMGUI draws the controls here; its legacy-input return values are intentionally unused.
            GUI.enabled = oldEnabled && input.CanBeginMove;
            GUI.Button(MoveButtonRect, input.ActionMode == UnitActionMode.Move ? "Move [selected]" : "Move", button);
            GUI.enabled = oldEnabled && input.CanOpenAttackMenu;
            GUI.Button(AttackButtonRect, input.IsAttackMenuOpen ? "Attack [selected]" : "Attack", button);
            GUI.enabled = oldEnabled && input.CanEjectSelected;
            GUI.Button(EjectButtonRect, "Eject / AP 1", button);
            if (input.ActionMode != UnitActionMode.Attack)
            {
                GUI.enabled = oldEnabled && input.CanOpenOverwatchMenu;
                GUI.Button(OverwatchButtonRect, input.SelectedUnit.Overwatch && input.SelectedUnit.Overwatch.IsActive
                    ? "Overwatch [active]" : "Overwatch / AP 2", button);
            }
            GUI.enabled = oldEnabled;
            if (TryGetAttackMenuRect(out var choices))
            {
                GUI.Box(choices, GUIContent.none);
                GUI.Label(new Rect(choices.x + 8 * Scale, choices.y + 5 * Scale, choices.width - 16 * Scale, 24 * Scale),
                    IsOverwatchWeaponMenu ? "Overwatch weapon" : "Choose weapon", title);
                foreach (var slot in WeaponChoices)
                {
                    GUI.enabled = oldEnabled && (IsOverwatchWeaponMenu ? input.CanBeginOverwatch(slot) : input.CanBeginWeaponAttack(slot));
                    GUI.Button(GetWeaponButtonRect(slot), GetWeaponButtonText(slot), button);
                }
                GUI.enabled = oldEnabled;
            }
            if (input.ActionMode == UnitActionMode.Attack && input.TargetUnit)
            {
                var preview = input.CurrentAttackPreview;
                GUI.Label(Row(3), $"Target: {input.TargetUnit.name}", text);
                GUI.Label(Row(4), WeaponInfoText, text);
                GUI.Label(Row(5), AttackInfoText, text);
                GUI.Label(Row(6), preview.CanAttack ? $"Cost: {preview.APCost} AP" : preview.FailureReason, text);
                GUI.enabled = oldEnabled && preview.CanAttack;
                GUI.Button(FireButtonRect, input.SelectedAttackType == AttackType.Melee ? "Strike" : "Fire", button);
                GUI.enabled = oldEnabled;
            }
            GUI.depth = oldDepth;
        }
    }
}
