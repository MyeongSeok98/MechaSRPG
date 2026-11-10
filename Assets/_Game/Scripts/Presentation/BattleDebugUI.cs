using UnityEngine;

namespace MechaXCOM
{
    public class BattleDebugUI : MonoBehaviour
    {
        [SerializeField] private Rect panelRect = new Rect(16, 16, 270, 306);
        [SerializeField, InspectorName("기본 파일럿 초상화")] private Sprite fallbackPortrait;
        [SerializeField, InspectorName("디버그창 프레임")] private Sprite panelFrame;
        private GridManager grid;
        private CoverSystem coverSystem;
        private BattleUnit selected;
        private PlayerInputController input;
        private TurnManager turns;
        private SelectedUnitInfoPanel unitInfoPanel;
        public bool DetailedDebugVisible { get; private set; }
        public void ConfigureUnitInfoPanel(SelectedUnitInfoPanel panel) => unitInfoPanel = panel;
        public void ToggleDetailedDebug() => DetailedDebugVisible = !DetailedDebugVisible;
        public bool IsPointerOverUnitPanel(Vector2 point) => unitInfoPanel && !DetailedDebugVisible && unitInfoPanel.ContainsScreenPoint(point);
        public bool CompactControlsVisible => unitInfoPanel && !DetailedDebugVisible &&
            (!input || (turns && turns.IsBattleOver) || (!input.HasPendingOverwatchDecision && !input.TargetUnit && !input.TargetCover && !input.SelectedImpactCell.HasValue));
        public bool TryGetUnitInfoRect(out Rect rect)
        { rect = default; if (!unitInfoPanel || DetailedDebugVisible || !unitInfoPanel.IsVisible) return false; rect = unitInfoPanel.GUIRect; return true; }
        public bool TryGetCompactControlsRect(out Rect rect)
        {
            rect = default; if (!CompactControlsVisible) return false;
            var local = CompactControlsRect;
            rect = new Rect(local.x * DisplayScale, local.y * DisplayScale, local.width * DisplayScale, local.height * DisplayScale); return true;
        }
        public string CompactFeedbackText
        {
            get
            {
                if (turns && turns.IsBattleOver)
                    return (turns.IsPlayerVictory ? "MISSION SUCCESS" : "MISSION FAILED") + "\n" + (turns.Mission ? turns.Mission.Result.Reason : string.Empty);
                if (!input) return string.Empty;
                if (!string.IsNullOrEmpty(input.InputMessage)) return input.InputMessage;
                var result = input.LastAttackResult;
                return !result.WasExecuted ? string.Empty :
                    $"{(result.IsCritical ? "CRITICAL" : result.Hit ? "HIT" : "MISS")} / Damage {result.DamageDealt}\n{BodyTargetRules.Label(result.TargetPart)} HP {result.TargetPartHPAfter}";
            }
        }
        private void Update()
        {
            if (unitInfoPanel && UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.f3Key.wasPressedThisFrame) ToggleDetailedDebug();
        }
        public BattleUnit DisplayedUnit => selected && selected.isActiveAndEnabled && selected.IsAlive &&
            (selected.Team == UnitTeam.Player || (turns && turns.Visibility && turns.Visibility.IsVisibleTo(UnitTeam.Player, selected)))
            ? selected : null;
        public string MechaName => UnitInspectionText.Name(DisplayedUnit);
        public string EquipmentText => UnitInspectionText.Equipment(DisplayedUnit);
        public string PartHealthText => UnitInspectionText.Health(DisplayedUnit);
        public Sprite PilotPortrait
        {
            get
            {
                var unit = DisplayedUnit;
                if (!isActiveAndEnabled || !unit) return null;
                return unit.PilotData && unit.PilotData.DefaultPortrait ? unit.PilotData.DefaultPortrait : fallbackPortrait;
            }
        }
        public BattleUnit VisibleActingEnemy => turns && turns.Visibility &&
            turns.Visibility.IsVisibleTo(UnitTeam.Player, turns.ActiveEnemy) ? turns.ActiveEnemy : null;
        public bool CanEndTurn => isActiveAndEnabled && input && input.isActiveAndEnabled && turns &&
            turns.isActiveAndEnabled && turns.IsPlayerTurn && !turns.IsBattleOver && !turns.IsAnyUnitBusy;
        public void InitializeCombat(PlayerInputController input, TurnManager turns)
        { this.input = input; this.turns = turns; }
        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private Vector2 scrollPosition;

        public void Initialize(GridManager grid, CoverSystem coverSystem)
        {
            this.grid = grid;
            this.coverSystem = coverSystem;
        }
        public void SetSelectedUnit(BattleUnit unit)
        {
            if (selected != unit) scrollPosition = Vector2.zero;
            selected = unit;
        }
        public float GetIncomingCoverMultiplier(CoverDirection direction)
        {
            var selected = DisplayedUnit;
            if (!selected || !grid || !coverSystem ||
                !grid.TryGetCell(selected.CurrentGridPosition, out var cell)) return 1;
            return coverSystem.GetAttackCoverMultiplier(cell, direction);
        }
        public bool ContainsScreenPoint(Vector2 point)
        {
            if (!isActiveAndEnabled) return false;
            if (unitInfoPanel && !DetailedDebugVisible)
                return IsPointerOverUnitPanel(point) || (CompactControlsVisible && CompactControlsRect.Contains(new Vector2(point.x, Screen.height - point.y) / DisplayScale));
            return DisplayRect.Contains(new Vector2(point.x, Screen.height - point.y) / DisplayScale);
        }
        private Rect CompactControlsRect => new Rect(Screen.width / DisplayScale - 262, 16, 246, string.IsNullOrEmpty(CompactFeedbackText) ? 156 : 260);
        private Vector2 compactFeedbackScroll;
        private void DrawCompactControls()
        {
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(DisplayScale, DisplayScale, 1));
            GUILayout.BeginArea(CompactControlsRect, GUI.skin.box);
            GUILayout.Label(turns ? $"TURN {turns.TurnNumber} / {turns.ActiveTeam}" : "BATTLE");
            if (turns && turns.Mission && turns.Mission.Data) GUILayout.Label(turns.Mission.Data.DisplayName);
            if (turns && turns.Mission && turns.Mission.Objective != null)
                GUILayout.Label($"Objective {turns.Mission.Objective.DestroyedCount}/{turns.Mission.Objective.TotalCount}");
            bool enabled = GUI.enabled; GUI.enabled = enabled && CanEndTurn;
            if (GUILayout.Button("End Turn", GUILayout.Height(28))) input.TryEndTurn();
            GUI.enabled = enabled; GUILayout.Label("F3: Debug");
            if (!string.IsNullOrEmpty(CompactFeedbackText))
            {
                compactFeedbackScroll = GUILayout.BeginScrollView(compactFeedbackScroll, GUILayout.Height(94));
                GUILayout.Label(CompactFeedbackText, new GUIStyle(GUI.skin.label) { wordWrap = true });
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea(); GUI.matrix = oldMatrix;
        }
        private float DisplayScale => Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.25f, 1f);
        private Rect DisplayRect => turns ? new Rect(panelRect.x, panelRect.y, panelRect.width, 680) : selected ? new Rect(panelRect.x, panelRect.y, panelRect.width, 390) :
            new Rect(panelRect.x, panelRect.y, panelRect.width, 90);
        private void OnGUI()
        {
            if (!grid || !coverSystem) return;
            if (unitInfoPanel && !DetailedDebugVisible) { if (CompactControlsVisible) DrawCompactControls(); return; }
            bool originalGUIEnabled = GUI.enabled;
            bool turnLocked = turns && turns.IsPresentingTurn;
            GUI.enabled = originalGUIEnabled && !turnLocked;
            // Scroll views can process wheel events even with GUI.enabled=false.
            if (turnLocked && (Event.current.isMouse || Event.current.isKey || Event.current.type == EventType.ScrollWheel))
                Event.current.Use();
            var selected = DisplayedUnit;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, wordWrap = true };
                titleStyle.normal.textColor = new Color(1f, 0.8f, 0.35f);
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
                textStyle.normal.textColor = Color.white;
            }
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(DisplayScale, DisplayScale, 1));
            var oldColor = GUI.color;
            if (panelFrame)
            {
                GUI.color = Color.white;
                SlicedSpriteGUI.Draw(DisplayRect, panelFrame);
            }
            else
            {
                GUI.color = new Color32(25, 42, 70, 255);
                GUI.DrawTexture(DisplayRect, Texture2D.whiteTexture);
            }
            GUI.color = oldColor;
            float sideInset = panelFrame ? 18 : 14;
            float topInset = panelFrame ? Mathf.Min(48, DisplayRect.height / 3) : 10;
            float bottomInset = panelFrame ? 48 : 10;
            float contentWidth = DisplayRect.width - sideInset * 2;
            float columnWidth = Mathf.Max(1, (contentWidth - 32) / 2);
            GUILayout.BeginArea(new Rect(DisplayRect.x + sideInset, DisplayRect.y + topInset,
                contentWidth, DisplayRect.height - topInset - bottomInset));
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(1, contentWidth - 24)));
            ResourceTypography.LayoutLabel("GROUND TACTICS / LAB", titleStyle);
            if (turns)
            {
                ResourceTypography.LayoutLabel($"Turn: {turns.TurnNumber} / {turns.ActiveTeam}", textStyle);
                if (turns.Mission && turns.Mission.Data)
                {
                    ResourceTypography.LayoutLabel(turns.Mission.Data.DisplayName, titleStyle);
                    ResourceTypography.LayoutLabel(turns.Mission.Data.RegionName, textStyle);
                    var objective = turns.Mission.Objective;
                    if (objective != null) ResourceTypography.LayoutLabel($"Destroyed: {objective.DestroyedCount} / {objective.TotalCount}", textStyle);
                }
                if (VisibleActingEnemy) ResourceTypography.LayoutLabel($"Acting: {VisibleActingEnemy.name}", textStyle);
                var decision = turns.Overwatch ? turns.Overwatch.PendingDecision : null;
                if (decision.HasValue)
                {
                    ResourceTypography.LayoutLabel($"Overwatch: {decision.Value.Watcher.name}", textStyle);
                    ResourceTypography.LayoutLabel("Choose part / Fire or Skip", textStyle);
                }
            }
            if (!selected)
            {
                ResourceTypography.LayoutLabel("Click the capsule to select.", textStyle);
                ResourceTypography.LayoutLabel("North = +Z / East = +X", textStyle);
            }
            else
            {
                ResourceTypography.LayoutLabel(MechaName, new GUIStyle(titleStyle) { wordWrap = true });
                ResourceTypography.LayoutLabel(selected.Team == UnitTeam.Player ? "ALLY" : "ENEMY / INSPECT ONLY", textStyle);
                var portraitRect = GUILayoutUtility.GetRect(1, 160, GUILayout.ExpandWidth(true));
                var portrait = PilotPortrait;
                if (portrait)
                {
                    float aspect = portrait.rect.width / portrait.rect.height;
                    float height = Mathf.Min(portraitRect.height, portraitRect.width / aspect);
                    var fitted = new Rect(portraitRect.center.x - height * aspect / 2,
                        portraitRect.center.y - height / 2, height * aspect, height);
                    var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(portrait);
                    GUI.DrawTextureWithTexCoords(fitted, portrait.texture, new Rect(uv.x, uv.y, uv.z - uv.x, uv.w - uv.y));
                }
                else GUI.Label(portraitRect, "Portrait unavailable", textStyle);
                if (selected.Health)
                {
                    ResourceTypography.LayoutLabel($"{selected.Health.State.ToString().ToUpperInvariant()} / AP {selected.ActionPoints.CurrentPoints}/{selected.ActionPoints.MaxPoints}", textStyle);
                    var hpStyle = new GUIStyle(textStyle) { fontSize = 12 };
                    for (int i = 0; i < 7; i += 2)
                    {
                        GUILayout.BeginHorizontal();
                        for (int j = i; j < Mathf.Min(i + 2, 7); j++)
                        {
                            var part = (TargetBodyPart)(j + 1);
                            ResourceTypography.LayoutLabel(UnitInspectionText.PartHP(selected, part),
                                hpStyle, GUILayout.Width(columnWidth));
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                ResourceTypography.LayoutLabel(EquipmentText, new GUIStyle(textStyle) { wordWrap = true });
                ResourceTypography.LayoutLabel($"Cell: ({selected.CurrentGridPosition.x}, {selected.CurrentGridPosition.y})", textStyle);
                ResourceTypography.LayoutLabel($"Moved: {selected.Mobility.MovedCells} Cells", textStyle);
                ResourceTypography.LayoutLabel($"Facing: {selected.Facing}", textStyle);
                ResourceTypography.LayoutLabel($"Pilot: S {selected.PilotShooting} / M {selected.PilotMelee} / E {selected.PilotEvasion} / T {selected.PilotTechnique}", textStyle);
                if (selected.Ammo)
                    foreach (var slot in new[] { WeaponSlot.LeftHand, WeaponSlot.RightHand, WeaponSlot.Backpack })
                    {
                        var ammo = selected.Ammo.GetStatus(slot);
                        if (ammo.Weapon && ammo.UsesAmmo)
                            ResourceTypography.LayoutLabel($"{slot}: {WeaponAmmoText.Summary(ammo, slot)}", textStyle);
                    }
                if (selected.Overwatch && selected.Overwatch.IsActive)
                    ResourceTypography.LayoutLabel($"OVERWATCH: {selected.Overwatch.Facing} / {selected.Overwatch.Weapon.DisplayName} / Hit x0.75", textStyle);
                GUILayout.Space(10);
                ResourceTypography.LayoutLabel("INCOMING COVER MULTIPLIER", titleStyle);
                if (grid.TryGetCell(selected.CurrentGridPosition, out _))
                {
                    string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
                    var coverStyle = new GUIStyle(textStyle) { fontSize = 12 };
                    for (int i = 0; i < 8; i += 2)
                    {
                        GUILayout.BeginHorizontal();
                        for (int j = i; j < i + 2; j++)
                        {
                            var direction = CoverDirections.Clockwise[j];
                            ResourceTypography.LayoutLabel($"{names[j]}: ×{GetIncomingCoverMultiplier(direction):0.##}",
                                coverStyle, GUILayout.Width(columnWidth));
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.Space(10);
                ResourceTypography.LayoutLabel(turns && !turns.IsPlayerTurn ? "Waiting for enemy actions..." :
                    selected.Movement.IsMoving ? "Moving..." : $"Move: {selected.AvailableMovementRange} cells / {selected.GetMovementCellsPerAP():0.##} cells per AP", textStyle);
                ResourceTypography.LayoutLabel($"Movement AP paid: {selected.Movement.PaidMovementAP} / prepaid: {selected.Movement.PrepaidMovementAP:0.###} AP", textStyle);
            }
            if (input && turns)
            {
                GUILayout.Space(8);
                var target = input.TargetUnit;
                var preview = input.CurrentAttackPreview;
                if (target)
                {
                    ResourceTypography.LayoutLabel($"{preview.TargetState.ToString().ToUpperInvariant()} / {BodyTargetRules.Label(preview.TargetPart)} {preview.PartHP}/{preview.PartMaxHP}", titleStyle);
                    ResourceTypography.LayoutLabel($"Distance: {preview.Distance:F2} cells", textStyle);
                    ResourceTypography.LayoutLabel($"Weapon: {preview.WeaponName}", textStyle);
                    ResourceTypography.LayoutLabel($"Attack: {preview.Type} / Damage {AttackPreviewText.DamageRange(preview)}", textStyle);
                    if (preview.AdditionalHitPartCount > 0) ResourceTypography.LayoutLabel(AttackPreviewText.AdditionalDamage(preview), textStyle);
                    ResourceTypography.LayoutLabel($"Damage taken: {preview.ArmorMultiplier * 100f:0}% (×{preview.ArmorMultiplier:0.##})", textStyle);
                    ResourceTypography.LayoutLabel($"Cover: {preview.Direction} ×{preview.CoverMultiplier:0.##} / Part ×{preview.PartMultiplier:0.##}", textStyle);
                    ResourceTypography.LayoutLabel($"Attacker head: ×{preview.HeadMultiplier:0.##}", textStyle);
                    ResourceTypography.LayoutLabel($"Grip: ×{preview.GripMultiplier:0.##} / Range: ×{preview.DistanceMultiplier:0.##}", textStyle);
                    ResourceTypography.LayoutLabel($"Mecha hit: ×{preview.MechaMultiplier:0.###} / Pilot hit: ×{preview.PilotHitMultiplier:0.###}", textStyle);
                    ResourceTypography.LayoutLabel($"Pilot damage: ×{preview.PilotDamageMultiplier:0.###}", textStyle);
                    ResourceTypography.LayoutLabel(preview.BlockedByBuilding ? "Hit: BLOCKED BY BUILDING" :
                        $"Hit: {preview.HitChance}%   Damage: {AttackPreviewText.DamageRange(preview)}", textStyle);
                    if (!preview.CanAttack) ResourceTypography.LayoutLabel(preview.FailureReason, textStyle);
                    ResourceTypography.LayoutLabel($"Critical: {preview.CriticalChance:0.##}% / Damage ×{preview.CriticalDamageMultiplier:0.##}", textStyle);
                }
                else if (!turns.IsBattleOver && turns.IsPlayerTurn)
                    ResourceTypography.LayoutLabel(input.ActionMode == UnitActionMode.Move ? "Choose a destination in the teal range." :
                        input.ActionMode == UnitActionMode.Attack ? "Select an enemy in the red range." :
                        selected && selected.Health.State == MechState.Critical ? "CRITICAL: Eject costs 1 AP." :
                        selected && selected.Team == UnitTeam.Enemy ? "Enemy inspection only. Select an ally to act." :
                        "Choose Move / Attack / Overwatch / Eject beside the unit.", textStyle);
                bool oldEnabled = GUI.enabled;
                GUI.enabled = oldEnabled && CanEndTurn;
                if (GUILayout.Button("End Turn", GUILayout.Height(28))) input.TryEndTurn();
                GUI.enabled = oldEnabled;
                var result = input.LastAttackResult;
                if (result.WasExecuted)
                {
                    ResourceTypography.LayoutLabel($"{(result.IsCritical ? "CRITICAL HIT" : result.Hit ? "HIT" : "MISS")} / Damage {result.DamageDealt}\n{BodyTargetRules.Label(result.TargetPart)} HP: {result.TargetPartHPAfter} / {result.TargetStateAfter.ToString().ToUpperInvariant()}", textStyle);
                    if (result.AdditionalPartDamage != null && result.AdditionalPartDamage.Count > 0)
                        ResourceTypography.LayoutLabel(AttackPreviewText.AdditionalDamageResult(result), textStyle);
                }
                if (!string.IsNullOrEmpty(input.InputMessage)) ResourceTypography.LayoutLabel(input.InputMessage, textStyle);
                if (turns.IsBattleOver)
                {
                    ResourceTypography.LayoutLabel(turns.IsPlayerVictory ? "MISSION SUCCESS\nRestart Play to try again." : "MISSION FAILED", titleStyle);
                    if (turns.Mission) ResourceTypography.LayoutLabel(turns.Mission.Result.Reason, textStyle);
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = oldMatrix;
            GUI.enabled = originalGUIEnabled;
        }
    }
}

