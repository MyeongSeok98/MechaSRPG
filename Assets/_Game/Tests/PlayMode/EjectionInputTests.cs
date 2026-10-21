using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class EjectionInputTests
    {
        private EnemyAITestBattle battle;
        private PlayerInputController input;
        private Camera camera;
        private Mouse mouse;
        private Keyboard keyboard;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private InputSettings.BackgroundBehavior oldBackground;
        private BattleUnit Player => battle.Players[0];
        private BattleUnit Enemy => battle.Enemies[0];
        private UnitActionUI Menu => battle.Root.GetComponent<UnitActionUI>();
        private BodyTargetUI Diagram => battle.Root.GetComponent<BodyTargetUI>();
        private Transform Ranges => battle.Root.transform.Find("Reachable Cells");
        private Transform Indicator => battle.Root.transform.Find("Selection Indicator");

        [UnityTest] public IEnumerator LegDestructionRefreshesAnAlreadyVisibleMoveRange()
        {
            input.SelectUnit(Player); Assert.That(input.BeginMove(), Is.True);
            yield return null;
            Assert.That(Ranges.childCount, Is.GreaterThan(12));
            Player.Health.ApplyDamage(TargetBodyPart.LeftLeg, 10);
            yield return null; yield return null;
            Assert.That(Ranges.childCount, Is.EqualTo(12));
            Player.Health.ApplyDamage(TargetBodyPart.RightLeg, 10);
            yield return null; yield return null;
            Assert.That(Ranges.gameObject.activeSelf, Is.False);
            Assert.That(input.CanBeginMove, Is.False);
            Assert.That(input.CanOpenAttackMenu, Is.True);
            Assert.That(input.CanEjectSelected, Is.True);
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            battle = new EnemyAITestBattle(new[] { new Vector2Int(3, 3) }, new[] { new Vector2Int(6, 3) });
            Player.gameObject.AddComponent<CapsuleCollider>(); Enemy.gameObject.AddComponent<CapsuleCollider>();
            var visualizer = battle.Root.AddComponent<GridVisualizer>(); visualizer.Initialize(battle.Grid, battle.Turns);
            var debug = battle.Root.AddComponent<BattleDebugUI>(); debug.Initialize(battle.Grid, battle.Root.GetComponent<CoverSystem>());
            camera = new GameObject("Ejection UI camera").AddComponent<Camera>(); camera.transform.SetParent(battle.Root.transform);
            camera.orthographic = true; camera.orthographicSize = 15;
            camera.transform.position = new Vector3(10, 28, -20); camera.transform.LookAt(new Vector3(10, 0, 10));
            input = battle.Root.AddComponent<PlayerInputController>(); input.Initialize(camera, battle.Grid, visualizer, debug);
            input.InitializeCombat(battle.Combat, battle.Turns); debug.InitializeCombat(input, battle.Turns);
            mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            Physics.SyncTransforms(); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            InputSystem.settings.backgroundBehavior = oldBackground;
            battle.Dispose(); yield return null;
        }
        private Rect EjectRect => Menu.EjectButtonRect;
        private IEnumerator Click(Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
        }
        private IEnumerator ClickRect(Rect rect) { yield return Click(new Vector2(rect.center.x, Screen.height - rect.center.y)); }
        private IEnumerator SelectCritical()
        {
            MechStateTestRules.MakeCritical(Player);
            yield return Click(camera.WorldToScreenPoint(Player.transform.position));
            Assert.That(input.SelectedUnit, Is.SameAs(Player));
        }
        private void AssertCleared()
        {
            Assert.That(input.SelectedUnit, Is.Null); Assert.That(input.TargetUnit, Is.Null);
            Assert.That(input.LastAttackResult.WasExecuted, Is.False);
            Assert.That(Menu.IsVisible, Is.False); Assert.That(Diagram.IsVisible, Is.False);
            Assert.That(Ranges.gameObject.activeSelf, Is.False); Assert.That(Indicator.gameObject.activeSelf, Is.False);
        }

        [UnityTest] public IEnumerator CriticalClickShowsEjectAndBlocksMoveAndAttack()
        {
            yield return SelectCritical();
            Assert.That(Menu.IsVisible, Is.True);
            Assert.That(input.CanManageSelectedUnit, Is.True);
            Assert.That(input.CanEjectSelected, Is.True);
            yield return ClickRect(Menu.MoveButtonRect); Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Menu));
            yield return ClickRect(Menu.AttackButtonRect); Assert.That(input.IsAttackMenuOpen, Is.False);
            yield return ClickRect(EjectRect);
            Assert.That(Player.Health.State, Is.EqualTo(MechState.Ejected));
            Assert.That(Player.Health.PilotEjected, Is.True); Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
            battle.Grid.TryGetCell(Player.CurrentGridPosition, out var cell); Assert.That(cell.Occupied, Is.False);
            AssertCleared(); StringAssert.Contains("EJECTED", input.InputMessage);
        }

        [UnityTest] public IEnumerator ZeroAPEjectionButtonCannotSpendOrRemoveUnit()
        {
            yield return SelectCritical(); Player.ActionPoints.TrySpend(5); yield return null;
            Assert.That(Menu.IsVisible, Is.True); Assert.That(input.CanEjectSelected, Is.False);
            yield return ClickRect(EjectRect);
            Assert.That(Player.Health.State, Is.EqualTo(MechState.Critical)); Assert.That(Player.ActionPoints.CurrentPoints, Is.Zero);
            Assert.That(input.SelectedUnit, Is.SameAs(Player));
        }

        [UnityTest] public IEnumerator EscapeClosesCriticalSelection()
        {
            yield return SelectCritical();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            AssertCleared(); Assert.That(Player.Health.State, Is.EqualTo(MechState.Critical));
        }

        [UnityTest] public IEnumerator EnemyTurnCannotEjectSelectedUnit()
        {
            input.SelectUnit(Player); var rect = EjectRect;
            battle.Combat.SetRollSource(() => 99); battle.SetActionDelay(.3f);
            Assert.That(input.TryEndTurn(), Is.True);
            Assert.That(input.CanEjectSelected, Is.False);
            yield return ClickRect(rect);
            Assert.That(Player.Health.State, Is.EqualTo(MechState.Active)); Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            yield return battle.WaitForPlayerTurn();
        }

        [UnityTest] public IEnumerator BodyTargetPanelReportsOnlySelectedPartHP()
        {
            input.SelectUnit(Player); input.BeginAttack(); input.TrySelectTarget(Enemy);
            yield return ClickRect(Diagram.GetPartRect(TargetBodyPart.Cockpit));
            StringAssert.Contains("6/6", Diagram.SelectedPartInfo);
            StringAssert.Contains("36%", Diagram.SelectedPartInfo);
            yield return ClickRect(Diagram.FireButtonRect);
            Assert.That(Enemy.Health.GetCurrentHP(TargetBodyPart.Cockpit), Is.EqualTo(1));
            Assert.That(Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Player.ActionPoints.ResetForTurn(); input.BeginAttack(); input.TrySelectTarget(Enemy);
            yield return ClickRect(Diagram.GetPartRect(TargetBodyPart.Cockpit));
            StringAssert.Contains("1/6", Diagram.SelectedPartInfo);
            StringAssert.DoesNotContain("20/20", Diagram.SelectedPartInfo);
            Assert.That(Diagram.PanelRect.yMin, Is.GreaterThanOrEqualTo(0));
            Assert.That(Diagram.PanelRect.yMax, Is.LessThanOrEqualTo(Screen.height));
        }

        [UnityTest] public IEnumerator RightEdgeTargetPanelKeepsMoveAttackEjectReachable()
        {
            camera.transform.position -= camera.transform.right * 11f; Physics.SyncTransforms();
            input.SelectUnit(Player); input.BeginAttack(); input.TrySelectTarget(Enemy);
            Assert.That(Menu.TryGetPanelRect(out var menuRect), Is.True);
            Assert.That(menuRect.Overlaps(Diagram.PanelRect), Is.False);
            Assert.That(menuRect.Contains(EjectRect.center), Is.True);
            yield return ClickRect(Menu.MoveButtonRect); Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Move));
            input.SelectUnit(Player);
            yield return ClickRect(Menu.AttackButtonRect); Assert.That(input.IsAttackMenuOpen, Is.True);
            yield return ClickRect(Menu.GetWeaponButtonRect(WeaponSlot.RightHand)); Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Attack));
            input.TrySelectTarget(Enemy);
            yield return ClickRect(EjectRect);
            Assert.That(Player.Health.State, Is.EqualTo(MechState.Ejected)); Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
            AssertCleared();
        }

        [UnityTest] public IEnumerator SelectionAndIndicatorClearAfterExternalDestruction()
        { yield return ExternalRemoval(false); }
        [UnityTest] public IEnumerator SelectionAndIndicatorClearAfterExternalEjection()
        { yield return ExternalRemoval(true); }
        private IEnumerator ExternalRemoval(bool eject)
        {
            input.SelectUnit(Player); input.BeginAttack(); input.TrySelectTarget(Enemy);
            Assert.That(input.TryAttackSelected().WasExecuted, Is.True);
            Player.ActionPoints.ResetForTurn(); input.BeginAttack(); input.TrySelectTarget(Enemy);
            if (eject) Player.Health.MarkEjected(); else Player.Health.ApplyDamage(TargetBodyPart.Torso, 20);
            yield return null; yield return null;
            AssertCleared();
        }
    }
}
