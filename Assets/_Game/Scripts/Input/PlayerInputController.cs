using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace MechaXCOM
{
    public enum UnitActionMode { Menu, Move, Attack, Facing, OverwatchWeapon, OverwatchFacing }
    public class PlayerInputController : MonoBehaviour
    {
        public WeaponSlot? SelectedWeaponSlot { get; private set; }
        public bool IsWeaponMenuOpen { get; private set; }
        public bool CanBeginWeaponAttack(WeaponSlot slot) => CanChooseAction && combat && combat.isActiveAndEnabled &&
            selected.Combat && selected.Combat.isActiveAndEnabled && selected.ActionPoints && selected.ActionPoints.isActiveAndEnabled &&
            selected.Combat.TryGetAttack(slot, out var stats, out _) && stats.Range > 0 && stats.MinDamage > 0 &&
            selected.ActionPoints.CanSpend(stats.APCost);
        public bool BeginWeaponAttack(WeaponSlot slot)
        {
            if (!CanBeginWeaponAttack(slot) || !selected.Combat.TryGetAttack(slot, out var stats, out _)) return false;
            ClearOverwatchSelection();
            if (SelectedWeaponSlot != slot) LastAttackResult = default;
            SelectedWeaponSlot = slot; SelectedAttackType = stats.Type;
            IsWeaponMenuOpen = false; IsAttackMenuOpen = false;
            SelectedBodyPart = TargetBodyPart.Whole;
            ActionMode = UnitActionMode.Attack;
            target = null; InputMessage = null; ResetClickSequence(); RefreshRange();
            return true;
        }
        public IReadOnlyList<WeaponSlot> GetAvailableWeaponSlots(AttackType? type = null)
        {
            var result = new List<WeaponSlot>(2);
            if (!selected || !selected.Combat || !selected.Combat.isActiveAndEnabled) return result;
            foreach (var slot in new[] { WeaponSlot.RightHand, WeaponSlot.LeftHand })
                if (selected.Combat.TryGetAttack(slot, out var stats, out _) &&
                    (!type.HasValue || stats.Type == type.Value) && stats.Range > 0 && stats.MinDamage > 0) result.Add(slot);
            return result;
        }
        private void ClearWeaponSelection() { SelectedWeaponSlot = null; IsWeaponMenuOpen = false; ClearOverwatchSelection(); }
        public WeaponSlot? SelectedOverwatchSlot { get; private set; }
        public FacingDirection SelectedOverwatchFacing { get; private set; }
        private WeaponData preparedOverwatchWeapon;
        private OverwatchSystem Overwatch => turns ? turns.Overwatch : null;
        public IReadOnlyList<WeaponSlot> GetOverwatchWeaponSlots() => GetAvailableWeaponSlots(AttackType.Ranged);
        public bool CanBeginOverwatch(WeaponSlot slot) => CanChooseAction && Overwatch && Overwatch.CanBegin(selected, slot);
        public bool CanOpenOverwatchMenu
        {
            get
            {
                if (!CanChooseAction) return false;
                foreach (var slot in GetOverwatchWeaponSlots()) if (CanBeginOverwatch(slot)) return true;
                return false;
            }
        }
        public bool CanConfirmOverwatch => ActionMode == UnitActionMode.OverwatchFacing && SelectedOverwatchSlot.HasValue &&
            CanBeginOverwatch(SelectedOverwatchSlot.Value) &&
            selected.Combat.TryGetAttack(SelectedOverwatchSlot.Value, out var attack, out _) && attack.Weapon == preparedOverwatchWeapon;
        public bool OpenOverwatchMenu()
        {
            if (!CanOpenOverwatchMenu) return false;
            ClearWeaponSelection(); IsAttackMenuOpen = false;
            ActionMode = UnitActionMode.OverwatchWeapon;
            LastAttackResult = default; SelectedBodyPart = TargetBodyPart.Whole;
            target = null; InputMessage = null; ResetClickSequence(); RefreshRange();
            return true;
        }
        public bool BeginOverwatch(WeaponSlot slot)
        {
            if (ActionMode != UnitActionMode.OverwatchWeapon || !CanBeginOverwatch(slot) ||
                !selected.Combat.TryGetAttack(slot, out var attack, out _)) return false;
            SelectedOverwatchSlot = slot; preparedOverwatchWeapon = attack.Weapon;
            SelectedOverwatchFacing = selected.Facing;
            ActionMode = UnitActionMode.OverwatchFacing;
            ResetClickSequence(); RefreshRange(); return true;
        }
        public bool TryChooseOverwatchFacing(FacingDirection facing)
        {
            if (!CanConfirmOverwatch || !FacingDirections.IsValid(facing)) return false;
            SelectedOverwatchFacing = facing; RefreshRange(); return true;
        }
        public bool TryConfirmOverwatch()
        {
            if (!CanConfirmOverwatch || !Overwatch.TryBegin(selected, SelectedOverwatchSlot.Value, SelectedOverwatchFacing)) return false;
            ClearWeaponSelection(); IsAttackMenuOpen = false;
            ActionMode = UnitActionMode.Menu; target = null; InputMessage = null;
            ResetClickSequence(); RefreshRange(); return true;
        }
        private void ClearOverwatchSelection() { SelectedOverwatchSlot = null; preparedOverwatchWeapon = null; }
        [SerializeField, Min(0.05f)] private float doubleClickInterval = 0.3f;
        private BattleCameraOrbit cameraOrbit;
        private BattleUnit lastClickedUnit;
        private float lastUnitClickTime;
        private Camera battleCamera;
        private GridManager grid;
        private GridVisualizer visualizer;
        private BattleDebugUI debugUI;
        private UnitActionUI actionUI;
        private BodyTargetUI bodyTargetUI;
        private BattleUnit selected;
        private BattleUnit target;
        private CombatSystem combat;
        private TurnManager turns;
        private VisibilitySystem visibility;
        private int observedTurn;
        private UnitTeam observedTeam;
        private int observedAP = -1;
        private int observedMovementRange = -1;
        private bool wasMoving;
        public BattleUnit SelectedUnit => selected ? selected : null;
        public BattleUnit TargetUnit => IsTargetSelectable(target) ? target : null;
        public AttackType SelectedAttackType { get; private set; } = AttackType.Ranged;
        public TargetBodyPart SelectedBodyPart { get; private set; } = TargetBodyPart.Whole;
        public AttackPreview CurrentAttackPreview => combat && SelectedWeaponSlot.HasValue
            ? combat.GetWeaponPreview(selected, TargetUnit, SelectedWeaponSlot.Value, SelectedBodyPart) : default;
        public AttackResult LastAttackResult { get; private set; }
        public string InputMessage { get; private set; }
        public UnitActionMode ActionMode { get; private set; } = UnitActionMode.Menu;
        public bool IsAttackMenuOpen { get; private set; }
        public FacingDirection SelectedFacing { get; private set; }
        public bool CanChooseFacing => isActiveAndEnabled && PlayerActionsAllowed && selected &&
            ActionMode == UnitActionMode.Facing && selected.CanResolveFacing;
        public bool IsFacingPanelVisible => isActiveAndEnabled && PlayerActionsAllowed && selected &&
            selected.isActiveAndEnabled && selected.HasPendingFacing && ActionMode == UnitActionMode.Facing;
        public bool TryChooseFacing(FacingDirection facing)
        {
            if (!CanChooseFacing || !FacingDirections.IsValid(facing)) return false;
            SelectedFacing = facing;
            return true;
        }
        public bool TryConfirmFacing() => CanChooseFacing && selected.Movement.TryConfirmFacing(SelectedFacing);
        public bool CanManageSelectedUnit => isActiveAndEnabled && PlayerActionsAllowed && !Busy &&
            (!turns || turns.isActiveAndEnabled) && selected && selected.isActiveAndEnabled && selected.IsAlive &&
            grid && grid.TryGetCell(selected.CurrentGridPosition, out var cell) && cell.OccupyingUnit == selected;
        public bool CanChooseAction => CanManageSelectedUnit && selected.CanTakeAction;
        public bool CanEjectSelected => CanManageSelectedUnit && selected.CanEject;
        public bool TryEjectSelected()
        {
            if (!CanEjectSelected) return false;
            string unitName = selected.name;
            if (!selected.TryEject()) return false;
            ClearSelection();
            InputMessage = $"{unitName}: EJECTED / 파일럿 탈출";
            return true;
        }
        public bool CanBeginMove => CanChooseAction && selected.Movement && selected.Movement.isActiveAndEnabled &&
            selected.ActionPoints && selected.ActionPoints.isActiveAndEnabled && selected.AvailableMovementRange > 0;
        public bool CanBeginAttack => SelectedWeaponSlot.HasValue && CanBeginWeaponAttack(SelectedWeaponSlot.Value);
        public bool CanBeginMelee => CanBeginAttackType(AttackType.Melee);
        public bool CanBeginRanged => CanBeginAttackType(AttackType.Ranged);
        public bool CanOpenAttackMenu => CanBeginMelee || CanBeginRanged;
        private bool CanBeginAttackType(AttackType type)
        {
            if (!CanChooseAction) return false;
            foreach (var slot in GetAvailableWeaponSlots(type)) if (CanBeginWeaponAttack(slot)) return true;
            return false;
        }
        private bool Busy => turns ? turns.IsAnyUnitBusy : selected && selected.Movement && (selected.Movement.IsMoving || selected.HasPendingFacing);
        private bool PlayerActionsAllowed => !turns || (turns.IsPlayerTurn && !turns.IsBattleOver);
        public void InitializeCombat(CombatSystem combat, TurnManager turns)
        {
            UnsubscribeVisibility();
            this.combat = combat; this.turns = turns;
            visibility = turns ? turns.Visibility : null;
            if (isActiveAndEnabled && visibility) visibility.Changed += HandleVisibilityChanged;
            HandleVisibilityChanged();
            observedTurn = turns ? turns.TurnNumber : 0;
            observedTeam = turns ? turns.ActiveTeam : UnitTeam.Player;
        }

        public void Initialize(Camera camera, GridManager grid, GridVisualizer visualizer, BattleDebugUI debugUI)
        {
            battleCamera = camera;
            cameraOrbit = camera ? camera.GetComponent<BattleCameraOrbit>() : null;
            ResetClickSequence();
            this.grid = grid;
            this.visualizer = visualizer;
            this.debugUI = debugUI;
            bodyTargetUI = GetComponent<BodyTargetUI>() ?? gameObject.AddComponent<BodyTargetUI>();
            bodyTargetUI.Initialize(this);
            actionUI = GetComponent<UnitActionUI>() ?? gameObject.AddComponent<UnitActionUI>();
            actionUI.Initialize(this, battleCamera);
            if (!battleCamera || !grid || !visualizer || !debugUI)
                Debug.LogWarning($"{name}: input requires Camera, GridManager, GridVisualizer and BattleDebugUI.", this);
        }
        private void Update()
        {
            if (!battleCamera || !grid || !visualizer || !debugUI) return;
            var pending = turns ? turns.PendingFacingUnit : selected && selected.HasPendingFacing ? selected : null;
            if (pending && (selected != pending || ActionMode != UnitActionMode.Facing))
            {
                selected = pending;
                SelectedFacing = pending.Facing;
                ActionMode = UnitActionMode.Facing;
                IsAttackMenuOpen = false;
                ClearWeaponSelection();
                target = null;
                ResetClickSequence();
                visualizer.ClearReachableCells();
                visualizer.SetSelectedUnit(pending);
                debugUI.SetSelectedUnit(pending);
            }
            else if (!pending && ActionMode == UnitActionMode.Facing)
            {
                ActionMode = UnitActionMode.Menu;
                RefreshRange();
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (pending && pending.IsConfirmingFacing) return;
                if (pending) pending.Movement.TryKeepArrivalFacing();
                ClearSelection();
                return;
            }
            if (!ReferenceEquals(selected, null) && (!selected || !selected.isActiveAndEnabled ||
                !selected.IsAlive || !selected.Movement || !selected.Movement.isActiveAndEnabled))
                ClearSelection();
            if (!ReferenceEquals(target, null) && !IsTargetSelectable(target))
            {
                target = null; SelectedBodyPart = TargetBodyPart.Whole;
                LastAttackResult = default; InputMessage = null;
            }
            if (turns && (observedTurn != turns.TurnNumber || observedTeam != turns.ActiveTeam))
            {
                observedTurn = turns.TurnNumber; observedTeam = turns.ActiveTeam;
                target = null; LastAttackResult = default; InputMessage = null;
                ActionMode = UnitActionMode.Menu;
                IsAttackMenuOpen = false;
                ClearWeaponSelection();
                ResetClickSequence();
                if (selected) RefreshRange();
            }
            bool moving = selected && selected.Movement && selected.Movement.IsMoving;
            if (wasMoving && !moving && selected) RefreshRange();
            wasMoving = moving;
            if (IsAttackMenuOpen && !CanOpenAttackMenu) IsAttackMenuOpen = false;
            if (IsWeaponMenuOpen && !CanOpenAttackMenu) ClearWeaponSelection();
            if ((ActionMode == UnitActionMode.OverwatchWeapon && !CanOpenOverwatchMenu) ||
                (ActionMode == UnitActionMode.OverwatchFacing && !CanConfirmOverwatch))
            {
                ActionMode = UnitActionMode.Menu; ClearOverwatchSelection();
                visualizer.ClearReachableCells(); ResetClickSequence();
            }
            if (selected && !moving && (observedAP != selected.ActionPoints.CurrentPoints ||
                observedMovementRange != selected.AvailableMovementRange)) RefreshRange();
            if (selected && !moving && ((ActionMode == UnitActionMode.Move && !CanBeginMove) ||
                (ActionMode == UnitActionMode.Attack && !CanBeginAttack)))
            {
                ActionMode = UnitActionMode.Menu; target = null; SelectedBodyPart = TargetBodyPart.Whole;
                ClearWeaponSelection();
                visualizer.ClearReachableCells();
            }
            if (turns && turns.IsBattleOver) { target = null; ActionMode = UnitActionMode.Menu; IsAttackMenuOpen = false; ClearWeaponSelection(); visualizer.ClearReachableCells(); return; }
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            if (pending)
            {
                ResetClickSequence();
                actionUI.HandleScreenClick(Mouse.current.position.ReadValue());
                return;
            }
            if (Busy) { ResetClickSequence(); return; }
            var point = Mouse.current.position.ReadValue();
            if (bodyTargetUI && bodyTargetUI.ContainsScreenPoint(point))
            {
                ResetClickSequence(); bodyTargetUI.HandleScreenClick(point); return;
            }
            if (actionUI && actionUI.ContainsScreenPoint(point))
            {
                ResetClickSequence(); actionUI.HandleScreenClick(point); return;
            }
            if (debugUI.ContainsScreenPoint(point)) { ResetClickSequence(); return; }
            var ray = battleCamera.ScreenPointToRay(point);
            if (Physics.Raycast(ray, out var hit, 1000f))
            {
                var unit = hit.collider.GetComponentInParent<BattleUnit>();
                if (unit)
                {
                    if (unit.Team == UnitTeam.Enemy) { ResetClickSequence(); TrySelectTarget(unit); }
                    else if (SelectUnit(unit))
                    {
                        float now = Time.unscaledTime;
                        if (lastClickedUnit == unit && now - lastUnitClickTime <= Mathf.Max(0.05f, doubleClickInterval))
                        {
                            if (cameraOrbit) cameraOrbit.TryFocus(unit.transform.position);
                            ResetClickSequence();
                        }
                        else { lastClickedUnit = unit; lastUnitClickTime = now; }
                    }
                    else ResetClickSequence();
                    return;
                }
                if (hit.collider.GetComponentInParent<CoverObject>() ||
                    hit.collider.GetComponentInParent<GridObstacle>()) { ResetClickSequence(); return; }
            }
            ResetClickSequence();
            var plane = new Plane(Vector3.up, grid.transform.position);
            if (plane.Raycast(ray, out var distance) &&
                grid.TryWorldToGrid(ray.GetPoint(distance), out var position))
            {
                if (ActionMode == UnitActionMode.Move) TryMoveSelected(position);
                else if (ActionMode == UnitActionMode.Menu) ClearSelection();
                else target = null;
            }
            else
                ClearSelection();
        }
        public bool SelectUnit(BattleUnit unit)
        {
            if (!unit || !unit.isActiveAndEnabled || !unit.IsAlive || unit.Team != UnitTeam.Player ||
                !unit.Movement || !unit.Movement.isActiveAndEnabled ||
                Busy || (turns && turns.IsBattleOver) || !grid || !visualizer || !debugUI) return false;
            if (!grid.TryGetCell(unit.CurrentGridPosition, out var cell) || cell.OccupyingUnit != unit) return false;
            selected = unit;
            SelectedBodyPart = TargetBodyPart.Whole;
            LastAttackResult = default;
            SelectedAttackType = AttackType.Ranged;
            ClearWeaponSelection();
            ActionMode = UnitActionMode.Menu;
            IsAttackMenuOpen = false;
            target = null; InputMessage = null;
            visualizer.SetSelectedUnit(unit);
            debugUI.SetSelectedUnit(unit);
            RefreshRange();
            return true;
        }
        public bool BeginMove()
        {
            if (!CanBeginMove) return false;
            ActionMode = UnitActionMode.Move;
            IsAttackMenuOpen = false;
            ClearWeaponSelection();
            target = null; InputMessage = null; ResetClickSequence(); RefreshRange();
            return true;
        }
        public bool OpenAttackMenu()
        {
            if (!CanOpenAttackMenu) return false;
            ActionMode = UnitActionMode.Menu;
            IsAttackMenuOpen = true;
            ClearWeaponSelection();
            IsWeaponMenuOpen = true;
            LastAttackResult = default; SelectedBodyPart = TargetBodyPart.Whole;
            target = null; InputMessage = null; ResetClickSequence(); RefreshRange();
            return true;
        }
        public bool BeginMelee() => BeginAttack(AttackType.Melee);
        public bool BeginAttack(AttackType type = AttackType.Ranged)
        {
            if (!CanBeginAttackType(type)) return false;
            ClearOverwatchSelection();
            var slots = GetAvailableWeaponSlots(type);
            if (slots.Count == 1) return BeginWeaponAttack(slots[0]);
            SelectedAttackType = type;
            SelectedBodyPart = TargetBodyPart.Whole;
            LastAttackResult = default; SelectedWeaponSlot = null;
            IsWeaponMenuOpen = true;
            ActionMode = UnitActionMode.Menu;
            IsAttackMenuOpen = false;
            target = null; InputMessage = null; ResetClickSequence(); RefreshRange();
            return true;
        }
        public bool TryMoveSelected(Vector2Int destination)
        {
            if (ActionMode != UnitActionMode.Move || !isActiveAndEnabled || !PlayerActionsAllowed || Busy || !selected || !selected.Movement ||
                !selected.Movement.TryMove(destination)) return false;
            ActionMode = UnitActionMode.Menu;
            target = null; InputMessage = null;
            visualizer.ClearReachableCells();
            wasMoving = true;
            return true;
        }
        public bool TrySelectTarget(BattleUnit unit)
        {
            if (!selected) { InputMessage = "Select the player first."; return false; }
            if (ActionMode != UnitActionMode.Attack || !SelectedWeaponSlot.HasValue || !isActiveAndEnabled || !PlayerActionsAllowed || Busy || !combat || !IsTargetSelectable(unit)) return false;
            if (target != unit) SelectedBodyPart = TargetBodyPart.Whole;
            target = unit; InputMessage = null;
            return true;
        }
        public bool TrySelectBodyPart(TargetBodyPart part)
        {
            if (!CanChooseAction || ActionMode != UnitActionMode.Attack || SelectedAttackType != AttackType.Ranged ||
                !TargetUnit || !System.Enum.IsDefined(typeof(TargetBodyPart), part)) return false;
            SelectedBodyPart = part; InputMessage = null;
            return true;
        }
        private bool IsTargetSelectable(BattleUnit unit)
        {
            if (!unit || !unit.isActiveAndEnabled || !unit.IsAlive || unit.Team != UnitTeam.Enemy || !grid ||
                !grid.TryGetCell(unit.CurrentGridPosition, out var cell) || cell.OccupyingUnit != unit) return false;
            if (!turns || !turns.Visibility || !turns.Visibility.IsVisibleTo(UnitTeam.Player, unit)) return false;
            foreach (var collider in unit.GetComponentsInChildren<Collider>())
                if (collider.enabled && collider.gameObject.activeInHierarchy) return true;
            return false;
        }
        public AttackResult TryAttackSelected()
        {
            if (!PlayerActionsAllowed)
                return new AttackResult { FailureReason = "Wait for the player turn." };
            if (ActionMode != UnitActionMode.Attack || !SelectedWeaponSlot.HasValue || !isActiveAndEnabled || !combat || !TargetUnit)
                return new AttackResult { FailureReason = "Select a living enemy." };
            var result = combat.TryAttackWithWeapon(selected, target, SelectedWeaponSlot.Value, SelectedBodyPart);
            if (result.WasExecuted)
            {
                LastAttackResult = result; InputMessage = null;
                ActionMode = UnitActionMode.Menu;
                target = null; ResetClickSequence();
                if (selected) RefreshRange();
            }
            else InputMessage = result.FailureReason;
            return result;
        }
        public bool TryEndTurn()
        {
            if (!isActiveAndEnabled || !turns || !turns.TryEndTurn()) return false;
            observedTurn = turns.TurnNumber; observedTeam = turns.ActiveTeam;
            target = null; LastAttackResult = default; InputMessage = null;
            ActionMode = UnitActionMode.Menu;
            IsAttackMenuOpen = false;
            ClearWeaponSelection();
            ResetClickSequence();
            if (selected) RefreshRange();
            return true;
        }
        private void RefreshRange()
        {
            observedAP = selected ? selected.ActionPoints.CurrentPoints : -1;
            observedMovementRange = selected ? selected.AvailableMovementRange : -1;
            if (!selected || !selected.CanTakeAction) { visualizer.ClearReachableCells(); return; }
            if (ActionMode == UnitActionMode.Move && CanBeginMove)
                visualizer.ShowReachableCells(Pathfinder.GetReachableCells(grid, selected));
            else if (ActionMode == UnitActionMode.Attack && CanBeginAttack)
                visualizer.ShowAttackCells(combat.GetWeaponRangeCells(selected, SelectedWeaponSlot.Value));
            else if (ActionMode == UnitActionMode.OverwatchFacing && CanConfirmOverwatch)
                visualizer.ShowOverwatchCells(Overwatch.GetCoverageCells(selected, SelectedOverwatchSlot.Value, SelectedOverwatchFacing));
            else visualizer.ClearReachableCells();
        }
        private void ClearSelection()
        {
            ResetClickSequence();
            selected = null;
            LastAttackResult = default;
            SelectedBodyPart = TargetBodyPart.Whole;
            ActionMode = UnitActionMode.Menu;
            IsAttackMenuOpen = false;
            ClearWeaponSelection();
            target = null; observedAP = -1;
            InputMessage = null;
            wasMoving = false;
            visualizer.ClearReachableCells();
            visualizer.SetSelectedUnit(null);
            debugUI.SetSelectedUnit(null);
        }
        private void OnDisable()
        {
            UnsubscribeVisibility();
            if (visualizer && debugUI) ClearSelection();
        }
        private void HandleVisibilityChanged()
        {
            if (ActionMode == UnitActionMode.OverwatchFacing && visualizer) RefreshRange();
            if (ReferenceEquals(target, null) || IsTargetSelectable(target)) return;
            target = null;
            SelectedBodyPart = TargetBodyPart.Whole;
            LastAttackResult = default;
            InputMessage = null;
        }
        private void UnsubscribeVisibility()
        {
            if (visibility) visibility.Changed -= HandleVisibilityChanged;
        }
        private void OnEnable()
        {
            if (visibility) visibility.Changed += HandleVisibilityChanged;
            HandleVisibilityChanged();
        }
        private void OnDestroy() => UnsubscribeVisibility();
        private void ResetClickSequence() { lastClickedUnit = null; lastUnitClickTime = float.NegativeInfinity; }
    }
}
