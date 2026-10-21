using UnityEngine;

namespace MechaXCOM
{
    public class BattleDebugUI : MonoBehaviour
    {
        [SerializeField] private Rect panelRect = new Rect(16, 16, 270, 306);
        private GridManager grid;
        private CoverSystem coverSystem;
        private BattleUnit selected;
        private PlayerInputController input;
        private TurnManager turns;
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
        public void SetSelectedUnit(BattleUnit unit) => selected = unit;
        public float GetIncomingCoverMultiplier(CoverDirection direction)
        {
            if (!selected || !grid || !coverSystem ||
                !grid.TryGetCell(selected.CurrentGridPosition, out var cell)) return 1;
            return coverSystem.GetAttackCoverMultiplier(cell, direction);
        }
        public bool ContainsScreenPoint(Vector2 point)
        {
            if (!isActiveAndEnabled) return false;
            return DisplayRect.Contains(new Vector2(point.x, Screen.height - point.y) / DisplayScale);
        }
        private float DisplayScale => Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.25f, 1f);
        private Rect DisplayRect => turns ? new Rect(panelRect.x, panelRect.y, panelRect.width, 680) : selected ? new Rect(panelRect.x, panelRect.y, panelRect.width, 390) :
            new Rect(panelRect.x, panelRect.y, panelRect.width, 90);
        private void OnGUI()
        {
            if (!grid || !coverSystem) return;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold };
                titleStyle.normal.textColor = new Color(1f, 0.8f, 0.35f);
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
                textStyle.normal.textColor = Color.white;
            }
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(DisplayScale, DisplayScale, 1));
            var oldBackground = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.1f, 0.14f, 0.14f, 0.95f);
            GUI.Box(DisplayRect, GUIContent.none);
            GUI.backgroundColor = oldBackground;
            GUILayout.BeginArea(new Rect(DisplayRect.x + 14, DisplayRect.y + 10, DisplayRect.width - 28, DisplayRect.height - 20));
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.Label("GROUND TACTICS / LAB", titleStyle);
            if (turns)
            {
                GUILayout.Label($"Turn: {turns.TurnNumber} / {turns.ActiveTeam}", textStyle);
                if (VisibleActingEnemy) GUILayout.Label($"Acting: {VisibleActingEnemy.name}", textStyle);
            }
            if (!selected)
            {
                GUILayout.Label("Click the capsule to select.", textStyle);
                GUILayout.Label("North = +Z / East = +X", textStyle);
            }
            else
            {
                if (selected.Health)
                {
                    GUILayout.Label($"{selected.Health.State.ToString().ToUpperInvariant()} / AP {selected.ActionPoints.CurrentPoints}/{selected.ActionPoints.MaxPoints}", textStyle);
                    var hpStyle = new GUIStyle(textStyle) { fontSize = 12 };
                    for (int i = 0; i < 7; i += 2)
                    {
                        GUILayout.BeginHorizontal();
                        for (int j = i; j < Mathf.Min(i + 2, 7); j++)
                        {
                            var part = (TargetBodyPart)(j + 1);
                            GUILayout.Label($"{BodyTargetRules.Label(part)} {selected.Health.GetCurrentHP(part)}/{selected.Health.GetMaxHP(part)}",
                                hpStyle, GUILayout.Width((DisplayRect.width - 28) / 2));
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.Label($"Cell: ({selected.CurrentGridPosition.x}, {selected.CurrentGridPosition.y})", textStyle);
                GUILayout.Label($"Moved: {selected.Mobility.MovedCells} Cells", textStyle);
                GUILayout.Label($"Facing: {selected.Facing}", textStyle);
                if (selected.Overwatch && selected.Overwatch.IsActive)
                    GUILayout.Label($"OVERWATCH: {selected.Overwatch.Facing} / {selected.Overwatch.Weapon.DisplayName} / Hit x0.75", textStyle);
                GUILayout.Space(10);
                GUILayout.Label("INCOMING COVER MULTIPLIER", titleStyle);
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
                            GUILayout.Label($"{names[j]}: ×{GetIncomingCoverMultiplier(direction):0.##}",
                                coverStyle, GUILayout.Width((DisplayRect.width - 28) / 2));
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.Space(10);
                GUILayout.Label(turns && !turns.IsPlayerTurn ? "Waiting for enemy actions..." :
                    selected.Movement.IsMoving ? "Moving..." : $"Move: {selected.AvailableMovementRange} cells / 1 AP each", textStyle);
            }
            if (input && turns)
            {
                GUILayout.Space(8);
                var target = input.TargetUnit;
                var preview = input.CurrentAttackPreview;
                if (target)
                {
                    GUILayout.Label($"{preview.TargetState.ToString().ToUpperInvariant()} / {BodyTargetRules.Label(preview.TargetPart)} {preview.PartHP}/{preview.PartMaxHP}", titleStyle);
                    GUILayout.Label($"Distance: {preview.Distance:F2} cells", textStyle);
                    GUILayout.Label($"Weapon: {preview.WeaponName}", textStyle);
                    GUILayout.Label($"Attack: {preview.Type} / Damage {AttackPreviewText.DamageRange(preview)}", textStyle);
                    GUILayout.Label($"Cover: {preview.Direction} ×{preview.CoverMultiplier:0.##} / Part ×{preview.PartMultiplier:0.##}", textStyle);
                    GUILayout.Label($"Attacker head: ×{preview.HeadMultiplier:0.##}", textStyle);
                    GUILayout.Label($"Grip: ×{preview.GripMultiplier:0.##} / Range: ×{preview.DistanceMultiplier:0.##}", textStyle);
                    GUILayout.Label(preview.BlockedByBuilding ? "Hit: BLOCKED BY BUILDING" :
                        $"Hit: {preview.HitChance}%   Damage: {AttackPreviewText.DamageRange(preview)}", textStyle);
                    if (!preview.CanAttack) GUILayout.Label(preview.FailureReason, textStyle);
                }
                else if (!turns.IsBattleOver && turns.IsPlayerTurn)
                    GUILayout.Label(input.ActionMode == UnitActionMode.Move ? "Choose a destination in the teal range." :
                        input.ActionMode == UnitActionMode.Attack ? "Select an enemy in the red range." :
                        selected && selected.Health.State == MechState.Critical ? "CRITICAL: Eject costs 1 AP." :
                        "Choose Move / Attack / Overwatch / Eject beside the unit.", textStyle);
                bool oldEnabled = GUI.enabled;
                GUI.enabled = oldEnabled && CanEndTurn;
                if (GUILayout.Button("End Turn", GUILayout.Height(28))) input.TryEndTurn();
                GUI.enabled = oldEnabled;
                var result = input.LastAttackResult;
                if (result.WasExecuted)
                    GUILayout.Label($"{(result.Hit ? "HIT" : "MISS")} / Damage {result.DamageDealt}\n{BodyTargetRules.Label(result.TargetPart)} HP: {result.TargetPartHPAfter} / {result.TargetStateAfter.ToString().ToUpperInvariant()}", textStyle);
                if (!string.IsNullOrEmpty(input.InputMessage)) GUILayout.Label(input.InputMessage, textStyle);
                if (turns.IsBattleOver)
                    GUILayout.Label(turns.IsPlayerVictory ? "ENEMY DEFEATED\nRestart Play to try again." : "PLAYER DEFEATED", titleStyle);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = oldMatrix;
        }
    }
}
