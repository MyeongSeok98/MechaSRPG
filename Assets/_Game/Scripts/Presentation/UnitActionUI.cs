using UnityEngine;

namespace MechaXCOM
{
    [DisallowMultipleComponent]
    public class UnitActionUI : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float selectionAnimationSpeed = 1;
        [SerializeField] private Color commandColor = new Color32(101, 239, 154, 255);
        [SerializeField] private Color selectedCommandFill = new Color32(91, 96, 94, 220);
        [SerializeField, Range(0, 2), InspectorName("글자 외곽 발광 강도")] private float textGlowIntensity = 1;
        [SerializeField, Min(0), InspectorName("글자 외곽 발광 반경")] private float textGlowRadius = 6;
        [SerializeField, Range(0, .18f), InspectorName("인광 잔광 시간 (초)")] private float phosphorPersistence = .12f;
        [SerializeField, Range(0, .08f), InspectorName("인광 주사선·입자 강도")] private float phosphorTextureStrength = .04f;
        private Texture2D phosphorRaster;
        private void OnDestroy() { if (phosphorRaster) Destroy(phosphorRaster); }
        private float selectedAt;
        private Font commandFont;
        private BattleUnit anchorUnit;
        private GameObject anchorModel;
        private Renderer[] anchorRenderers;
        public Vector2 RingCenter
        {
            get
            {
                if (!input || !input.SelectedUnit || !battleCamera) return default;
                var point = battleCamera.WorldToScreenPoint(SelectionAnchorWorldPoint());
                return new Vector2(point.x, Screen.height - point.y);
            }
        }
        private Vector3 SelectionAnchorWorldPoint()
        {
            var unit = input.SelectedUnit;
            var visual = unit.GetComponent<UnitMechaVisual>();
            var model = visual ? visual.ModelInstance : null;
            if (anchorUnit != unit || anchorModel != model || anchorRenderers == null)
            {
                anchorUnit = unit; anchorModel = model;
                anchorRenderers = model ? model.GetComponentsInChildren<Renderer>(true) : unit.GetComponents<Renderer>();
            }
            bool found = false; float bottom = 0, top = 0;
            foreach (var renderer in anchorRenderers)
            {
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))) continue;
                var bounds = renderer.bounds;
                if (!found) { bottom = bounds.min.y; top = bounds.max.y; found = true; }
                else { bottom = Mathf.Min(bottom, bounds.min.y); top = Mathf.Max(top, bounds.max.y); }
            }
            var position = unit.transform.position;
            // Keep root X/Z so a protruding weapon cannot pull the ring sideways.
            if (found) return new Vector3(position.x, (bottom + top) / 2, position.z);
            var collider = unit.GetComponent<Collider>();
            return collider && collider.enabled ? new Vector3(position.x, collider.bounds.center.y, position.z) : position;
        }
        private bool UsesCommandWheel => !IsDirectionMenu && !(input && input.ActionMode == UnitActionMode.Attack &&
            (input.TargetUnit || input.TargetCover || input.SelectedImpactCell.HasValue));
        public bool IsCommandWheelVisible => IsVisible && UsesCommandWheel;
        public float SelectionAnimationSeconds => Mathf.Max(0, Time.unscaledTime - selectedAt) * selectionAnimationSpeed;
        public void NotifySelection() => selectedAt = Time.unscaledTime;
        public UnitCommandAction SelectedCommand => !input ? UnitCommandAction.None :
            input.ActionMode == UnitActionMode.Move ? UnitCommandAction.Move :
            input.ActionMode == UnitActionMode.Attack || input.IsAttackMenuOpen || input.IsWeaponMenuOpen ? UnitCommandAction.Attack :
            IsOverwatchWeaponMenu || IsOverwatchFacing || (input.SelectedUnit && input.SelectedUnit.Overwatch && input.SelectedUnit.Overwatch.IsActive) ? UnitCommandAction.Overwatch :
            IsReloadWeaponMenu ? UnitCommandAction.Reload : UnitCommandAction.None;
        private bool IsOverwatchWeaponMenu => input && input.ActionMode == UnitActionMode.OverwatchWeapon;
        private bool IsOverwatchFacing => input && input.ActionMode == UnitActionMode.OverwatchFacing;
        private bool IsReloadWeaponMenu => input && input.IsReloadWeaponMenuOpen;
        private bool IsDirectionMenu => input && (input.ActionMode == UnitActionMode.Facing || IsOverwatchFacing);
        private System.Collections.Generic.IReadOnlyList<WeaponSlot> WeaponChoices => IsReloadWeaponMenu
            ? input.GetReloadWeaponSlots() : IsOverwatchWeaponMenu ? input.GetOverwatchWeaponSlots() : input.GetAvailableWeaponSlots();
        public Rect GetWeaponButtonRect(WeaponSlot slot)
        {
            if (!input || (!input.IsWeaponMenuOpen && !IsOverwatchWeaponMenu && !IsReloadWeaponMenu)) return default;
            var slots = WeaponChoices;
            for (int i = 0; i < slots.Count; i++) if (slots[i] == slot) return AttackRow(i);
            return default;
        }
        public string GetWeaponButtonText(WeaponSlot slot)
        {
            if (!input || !input.SelectedUnit || !input.SelectedUnit.Combat.TryGetAttack(slot, out var stats, out _)) return string.Empty;
            var loadout = input.SelectedUnit.GetComponent<UnitWeaponLoadout>();
            var grip = slot == WeaponSlot.LeftHand ? loadout.LeftGrip : loadout.RightGrip;
            string hand = slot == WeaponSlot.Backpack ? "Backpack" :
                grip == GripMode.TwoHands ? "Both hands" : slot == WeaponSlot.LeftHand ? "Left" : "Right";
            var ammo = input.SelectedUnit.Ammo ? input.SelectedUnit.Ammo.GetStatus(slot) : default;
            string cost = IsReloadWeaponMenu ? $"Reload AP {ammo.ReloadAPCost}" : $"AP {stats.APCost}";
            string resource = !ammo.UsesAmmo ? string.Empty : slot == WeaponSlot.Backpack
                ? $"{ammo.Loaded}/{ammo.Capacity} · 재장전 불가 / "
                : $"{ammo.Loaded}/{ammo.Capacity} · 예비 {ammo.SpareMagazines} / ";
            return $"{hand}: {stats.DisplayName}{(stats.Weapon.IsIndirect ? " [곡사]" : "")}\n{resource}{cost}";
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
        public bool IsVisible => isActiveAndEnabled && input && !input.HasPendingOverwatchDecision && (input.CanManageSelectedUnit || input.IsFacingPanelVisible) &&
            battleCamera && (IsDirectionMenu || IsOverwatchWeaponMenu || IsReloadWeaponMenu || input.ActionMode == UnitActionMode.Menu ||
                input.ActionMode == UnitActionMode.Move || input.ActionMode == UnitActionMode.Attack) && InView();
        private bool InView()
        {
            var point = battleCamera.WorldToViewportPoint(SelectionAnchorWorldPoint());
            return point.z > 0 && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1;
        }
        public bool TryGetPanelRect(out Rect rect)
        {
            rect = default;
            if (!IsVisible) return false;
            var point = battleCamera.WorldToScreenPoint(SelectionAnchorWorldPoint());
            bool wheel = UsesCommandWheel;
            float width = (wheel ? 600 : 190) * Scale;
            bool target = input.ActionMode == UnitActionMode.Attack && input.TargetUnit;
            float height = (wheel ? 350 : IsDirectionMenu ? 224 : target ? 320 : 224) * Scale;
            // Additional action rows grow upward from the original three-action menu's lower edge,
            // rather than covering nearby unit clicks below that edge.
            float verticalOffset = wheel || IsDirectionMenu || target ? height / 2 : height - 80 * Scale;
            float rightEdge = bodyTargetUI && bodyTargetUI.IsVisible
                ? bodyTargetUI.PanelRect.xMin - 8 * Scale : Screen.width - 8 * Scale;
            var coverPanel = GetComponent<CoverTargetUI>(); var indirectPanel = GetComponent<IndirectTargetUI>();
            if (coverPanel && coverPanel.IsVisible) rightEdge = Mathf.Min(rightEdge, coverPanel.PanelRect.xMin - 8 * Scale);
            if (indirectPanel && indirectPanel.IsVisible) rightEdge = Mathf.Min(rightEdge, indirectPanel.PanelRect.xMin - 8 * Scale);
            float x = wheel ? point.x - width / 2 : point.x + 24 * Scale;
            if (!wheel && x + width > rightEdge) x = point.x - width - 24 * Scale;
            rect = new Rect(Mathf.Clamp(x, 8 * Scale, rightEdge - width),
                Mathf.Clamp(Screen.height - point.y - verticalOffset, 8 * Scale, Screen.height - height - 8 * Scale), width, height);
            AvoidInfoOverlays(ref rect, rightEdge);
            return true;
        }
        private void AvoidInfoOverlays(ref Rect rect, float rightEdge)
        {
            var debug = input ? input.GetComponent<BattleDebugUI>() : null;
            if (!debug) return;
            float gap = 8 * Scale;
            if (debug.TryGetUnitInfoRect(out var left) && rect.Overlaps(left))
            {
                if (left.xMax + gap + rect.width <= rightEdge) rect.x = left.xMax + gap;
                else if (left.yMax + gap + rect.height <= Screen.height - gap) rect.y = left.yMax + gap;
            }
            if (debug.TryGetCompactControlsRect(out var controls) && rect.Overlaps(controls))
            {
                float leftEdge = debug.TryGetUnitInfoRect(out left) ? left.xMax + gap : gap;
                if (controls.xMin - gap - rect.width >= leftEdge) rect.x = controls.xMin - gap - rect.width;
                else if (controls.yMax + gap + rect.height <= Screen.height - gap) rect.y = controls.yMax + gap;
            }
        }
        private Rect Row(int row)
        {
            if (!TryGetPanelRect(out var rect)) return default;
            return new Rect(rect.x + 8 * Scale, rect.y + (52 + row * 32) * Scale, rect.width - 16 * Scale, 28 * Scale);
        }
        private Rect CommandRect(UnitCommandAction action, int legacyRow) => UsesCommandWheel
            ? TryGetPanelRect(out var rect) ? UnitCommandWheel.ButtonRect(rect, action) : default : Row(legacyRow);
        public Rect MoveButtonRect => CommandRect(UnitCommandAction.Move, 0);
        public Rect AttackButtonRect => CommandRect(UnitCommandAction.Attack, 1);
        public Rect EjectButtonRect => CommandRect(UnitCommandAction.Eject, 2);
        public Rect OverwatchButtonRect => !UsesCommandWheel && input && input.ActionMode == UnitActionMode.Attack ? default : CommandRect(UnitCommandAction.Overwatch, 3);
        public Rect ReloadButtonRect => !UsesCommandWheel && input && input.ActionMode == UnitActionMode.Attack ? default : CommandRect(UnitCommandAction.Reload, 4);
        public Rect FireButtonRect => Row(7);
        public Rect GetFacingButtonRect(FacingDirection facing) => Row((int)facing);
        public Rect ConfirmFacingButtonRect => Row(4);
        public bool TryGetAttackMenuRect(out Rect rect)
        {
            rect = default;
            if (!input || (!input.IsAttackMenuOpen && !input.IsWeaponMenuOpen && !IsOverwatchWeaponMenu && !IsReloadWeaponMenu) || !TryGetPanelRect(out var main)) return false;
            float width = 220 * Scale, gap = 8 * Scale, height = (40 + 44 * Mathf.Max(2, WeaponChoices.Count)) * Scale;
            var exclusions = new System.Collections.Generic.List<Rect>();
            var debug = input.GetComponent<BattleDebugUI>();
            if (debug)
            {
                if (debug.TryGetUnitInfoRect(out var info)) exclusions.Add(info);
                if (debug.TryGetCompactControlsRect(out var controls)) exclusions.Add(controls);
            }
            return UnitCommandWheel.TryPlaceChoices(main, new Vector2(width, height), new Rect(0, 0, Screen.width, Screen.height), exclusions.ToArray(), gap, out rect);
        }
        private Rect AttackRow(int row)
        {
            if (!TryGetAttackMenuRect(out var rect)) return default;
            return new Rect(rect.x + 8 * Scale, rect.y + (32 + row * 44) * Scale, rect.width - 16 * Scale, 40 * Scale);
        }
        private Vector2 GuiPoint(Vector2 screenPoint) => new Vector2(screenPoint.x, Screen.height - screenPoint.y);
        public bool ContainsScreenPoint(Vector2 point)
        {
            var gui = GuiPoint(point);
            if (TryGetAttackMenuRect(out var choices) && choices.Contains(gui)) return true;
            if (!TryGetPanelRect(out var rect)) return false;
            return UsesCommandWheel ? MoveButtonRect.Contains(gui) || AttackButtonRect.Contains(gui) || EjectButtonRect.Contains(gui) ||
                OverwatchButtonRect.Contains(gui) || ReloadButtonRect.Contains(gui) : rect.Contains(gui);
        }
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
            if ((input.IsWeaponMenuOpen || IsOverwatchWeaponMenu || IsReloadWeaponMenu) && TryGetAttackMenuRect(out var weaponMenu) && weaponMenu.Contains(guiPoint))
            {
                foreach (var slot in WeaponChoices)
                    if (GetWeaponButtonRect(slot).Contains(guiPoint))
                    {
                        if (IsReloadWeaponMenu) input.TryReloadSelected(slot);
                        else if (IsOverwatchWeaponMenu) input.BeginOverwatch(slot); else input.BeginWeaponAttack(slot);
                        return;
                    }
                return;
            }
            if (MoveButtonRect.Contains(guiPoint)) input.BeginMove();
            else if (EjectButtonRect.Contains(guiPoint)) input.TryEjectSelected();
            else if (AttackButtonRect.Contains(guiPoint)) input.OpenAttackMenu();
            else if (OverwatchButtonRect.Contains(guiPoint)) input.OpenOverwatchMenu();
            else if (ReloadButtonRect.Contains(guiPoint)) input.OpenReloadMenu();
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
            var weaponButton = new GUIStyle(button) { fontSize = Mathf.RoundToInt(12 * Scale), wordWrap = true };
            if (UsesCommandWheel)
            {
                if (!commandFont) commandFont = Resources.Load<Font>("Oxanium-Regular");
                if (!phosphorRaster && Event.current.type == EventType.Repaint && phosphorTextureStrength > 0)
                    phosphorRaster = CommandWheelPhosphor.CreateRaster();
                UnitCommandWheel.Draw(rect, RingCenter, new CommandWheelAnimation(SelectionAnimationSeconds), SelectedCommand,
                    commandColor, selectedCommandFill, commandFont, CommandEnabled, textGlowIntensity, textGlowRadius,
                    phosphorPersistence, phosphorRaster, phosphorTextureStrength);
                DrawWeaponChoices(title, weaponButton);
                GUI.depth = oldDepth; return;
            }
            GUI.Box(rect, GUIContent.none);
            ResourceTypography.Label(new Rect(rect.x + 8 * Scale, rect.y + 5 * Scale, rect.width - 16 * Scale, 24 * Scale),
                $"{input.SelectedUnit.name} / AP {input.SelectedUnit.ActionPoints.CurrentPoints}", title);
            ResourceTypography.Label(new Rect(rect.x + 8 * Scale, rect.y + 27 * Scale, rect.width - 16 * Scale, 20 * Scale),
                UnitStateText, text);
            bool oldEnabled = GUI.enabled;
            if (IsDirectionMenu)
            {
                GUI.enabled = oldEnabled && (IsOverwatchFacing ? input.CanConfirmOverwatch : input.CanChooseFacing);
                string[] names = { "North (+Z)", "East (+X)", "South (-Z)", "West (-X)" };
                var selectedFacing = IsOverwatchFacing ? input.SelectedOverwatchFacing : input.SelectedFacing;
                for (int i = 0; i < 4; i++)
                    ResourceTypography.Button(GetFacingButtonRect((FacingDirection)i), names[i] + (selectedFacing == (FacingDirection)i ? " [selected]" : ""), button);
                ResourceTypography.Button(ConfirmFacingButtonRect, IsOverwatchFacing ? "Confirm / AP 2" :
                    input.SelectedUnit.IsConfirmingFacing ? "Turning..." : "Confirm / AP 0", button);
                GUI.enabled = oldEnabled;
                GUI.depth = oldDepth;
                return;
            }
            // Input System clicks are handled once by PlayerInputController before map raycasts.
            // IMGUI draws the controls here; its legacy-input return values are intentionally unused.
            GUI.enabled = oldEnabled && input.CanBeginMove;
            ResourceTypography.Button(MoveButtonRect, input.ActionMode == UnitActionMode.Move ? "Move [selected]" : "Move", button);
            GUI.enabled = oldEnabled && input.CanOpenAttackMenu;
            ResourceTypography.Button(AttackButtonRect, input.IsAttackMenuOpen ? "Attack [selected]" : "Attack", button);
            GUI.enabled = oldEnabled && input.CanEjectSelected;
            ResourceTypography.Button(EjectButtonRect, "Eject / AP 1", button);
            if (input.ActionMode != UnitActionMode.Attack)
            {
                GUI.enabled = oldEnabled && input.CanOpenOverwatchMenu;
                ResourceTypography.Button(OverwatchButtonRect, input.SelectedUnit.Overwatch && input.SelectedUnit.Overwatch.IsActive
                    ? "Overwatch [active]" : "Overwatch / AP 2", button);
                GUI.enabled = oldEnabled && input.CanOpenReloadMenu;
                ResourceTypography.Button(ReloadButtonRect, IsReloadWeaponMenu ? "Reload [selected]" : "Reload", button);
            }
            GUI.enabled = oldEnabled;
            DrawWeaponChoices(title, weaponButton);
            if (input.ActionMode == UnitActionMode.Attack && input.TargetUnit)
            {
                var preview = input.CurrentAttackPreview;
                ResourceTypography.Label(Row(3), $"Target: {input.TargetUnit.name}", text);
                ResourceTypography.Label(Row(4), WeaponInfoText, text);
                ResourceTypography.Label(Row(5), AttackInfoText, text);
                ResourceTypography.Label(Row(6), preview.CanAttack ? $"Cost: {preview.APCost} AP" : preview.FailureReason, text);
                GUI.enabled = oldEnabled && preview.CanAttack;
                ResourceTypography.Button(FireButtonRect, input.SelectedAttackType == AttackType.Melee ? "Strike" : "Fire", button);
                GUI.enabled = oldEnabled;
            }
            GUI.depth = oldDepth;
        }
        private bool CommandEnabled(UnitCommandAction action) => action == UnitCommandAction.Move ? input.CanBeginMove :
            action == UnitCommandAction.Attack ? input.CanOpenAttackMenu : action == UnitCommandAction.Overwatch ? input.CanOpenOverwatchMenu :
            action == UnitCommandAction.Eject ? input.CanEjectSelected : input.CanOpenReloadMenu;
        private void DrawWeaponChoices(GUIStyle title, GUIStyle weaponButton)
        {
            bool oldEnabled = GUI.enabled;
            if (TryGetAttackMenuRect(out var choices))
            {
                GUI.Box(choices, GUIContent.none);
                ResourceTypography.Label(new Rect(choices.x + 8 * Scale, choices.y + 5 * Scale, choices.width - 16 * Scale, 24 * Scale),
                    IsReloadWeaponMenu ? "Reload weapon" : IsOverwatchWeaponMenu ? "Overwatch weapon" : "Choose weapon", title);
                foreach (var slot in WeaponChoices)
                {
                    GUI.enabled = oldEnabled && (IsReloadWeaponMenu ? input.CanReloadWeapon(slot) : IsOverwatchWeaponMenu ? input.CanBeginOverwatch(slot) : input.CanBeginWeaponAttack(slot));
                    ResourceTypography.Button(GetWeaponButtonRect(slot), GetWeaponButtonText(slot), weaponButton);
                }
                GUI.enabled = oldEnabled;
            }
        }
    }
}

