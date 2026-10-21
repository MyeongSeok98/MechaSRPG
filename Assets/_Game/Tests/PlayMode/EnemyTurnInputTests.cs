using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class EnemyTurnInputTests
    {
        private EnemyAITestBattle battle;
        private PlayerInputController input;
        private BattleDebugUI ui;
        private Camera camera;
        private Transform ranges;
        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private InputSettings.BackgroundBehavior oldBackground;
        // Exercise the same explicit action choice as the unit menu.
        private bool MoveSelected(Vector2Int destination)
        { input.BeginMove(); return input.TryMoveSelected(destination); }
        private bool SelectAttackTarget(BattleUnit target)
        { input.BeginAttack(); return input.TrySelectTarget(target); }
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            battle = new EnemyAITestBattle(new[] { new Vector2Int(3, 1) }, new[] { new Vector2Int(1, 1), new Vector2Int(1, 2) });
            battle.SetActionDelay(3);
            foreach (var unit in battle.Players) unit.gameObject.AddComponent<CapsuleCollider>();
            foreach (var unit in battle.Enemies) unit.gameObject.AddComponent<CapsuleCollider>();
            var visualizer = battle.Root.AddComponent<GridVisualizer>(); visualizer.Initialize(battle.Grid);
            ui = battle.Root.AddComponent<BattleDebugUI>(); ui.Initialize(battle.Grid, battle.Root.GetComponent<CoverSystem>());
            camera = new GameObject("Enemy Turn Camera").AddComponent<Camera>(); camera.transform.SetParent(battle.Root.transform);
            camera.orthographic = true; camera.orthographicSize = 15;
            camera.transform.position = new Vector3(10, 28, -20); camera.transform.LookAt(new Vector3(10, 0, 10));
            camera.gameObject.AddComponent<BattleCameraOrbit>().Initialize(battle.Grid);
            input = battle.Root.AddComponent<PlayerInputController>();
            input.Initialize(camera, battle.Grid, visualizer, ui); input.InitializeCombat(battle.Combat, battle.Turns);
            ui.InitializeCombat(input, battle.Turns);
            ranges = battle.Root.transform.Find("Reachable Cells");
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            Physics.SyncTransforms(); yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            InputSystem.settings.backgroundBehavior = oldBackground;
            battle?.Dispose(); yield return null;
        }
        private bool EndButtonAvailable() => ui.CanEndTurn;
        private IEnumerator EnemyPause()
        {
            float deadline = Time.realtimeSinceStartup + 2;
            while (battle.Enemies[0].ActionPoints.CurrentPoints == 5 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(battle.ActiveTeam, Is.EqualTo(UnitTeam.Enemy));
            Assert.That(battle.Turns.IsAnyUnitMoving, Is.False);
        }
        [UnityTest]
        public IEnumerator ExternalPhaseChangeClearsRangeTargetAndOldResult()
        {
            input.SelectUnit(battle.Players[0]); SelectAttackTarget(battle.Enemies[0]);
            Assert.That(input.TryAttackSelected().WasExecuted, Is.True);
            Assert.That(ranges.gameObject.activeSelf, Is.False);
            Assert.That(input.BeginMove(), Is.True);
            Assert.That(ranges.gameObject.activeSelf, Is.True);
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            yield return EnemyPause();
            Assert.That(input.SelectedUnit, Is.SameAs(battle.Players[0]));
            Assert.That(input.TargetUnit, Is.Null);
            Assert.That(input.LastAttackResult.WasExecuted, Is.False);
            Assert.That(ranges.gameObject.activeSelf, Is.False);
            int hp = battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso);
            Assert.That(MoveSelected(new Vector2Int(4, 1)), Is.False);
            Assert.That(SelectAttackTarget(battle.Enemies[1]), Is.False);
            Assert.That(input.TryAttackSelected().WasExecuted, Is.False);
            Assert.That(input.TryEndTurn(), Is.False);
            Assert.That(battle.Players[0].ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(hp));
            // Both enemies now retain their action marker during the configured 3-second pause.
            yield return battle.WaitForPlayerTurn(8); yield return null;
            Assert.That(ranges.gameObject.activeSelf, Is.False);
            Assert.That(input.InputMessage, Is.Null);
        }
        [UnityTest]
        public IEnumerator DisabledTurnManagerDisablesEndTurnButton()
        {
            Assert.That(EndButtonAvailable(), Is.True);
            battle.Turns.enabled = false;
            Assert.That(EndButtonAvailable(), Is.False);
            Assert.That(input.TryEndTurn(), Is.False);
            yield return null;
        }
        [UnityTest]
        public IEnumerator EnemyPauseDisablesEndTurnButtonEvenWhenNoUnitIsMoving()
        {
            Assert.That(EndButtonAvailable(), Is.True);
            Assert.That(input.TryEndTurn(), Is.True); yield return EnemyPause();
            Assert.That(EndButtonAvailable(), Is.False);
            yield return battle.WaitForPlayerTurn(8);
            Assert.That(EndButtonAvailable(), Is.True);
            battle.Enemies[0].Health.ApplyDamage(TargetBodyPart.Torso, 20); battle.Enemies[1].Health.ApplyDamage(TargetBodyPart.Torso, 20);
            Assert.That(EndButtonAvailable(), Is.False);
        }
        [UnityTest]
        public IEnumerator EscapeClearsSelectionDuringEnemyPhase()
        {
            input.SelectUnit(battle.Players[0]); battle.Turns.TryEndTurn(); yield return EnemyPause();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(input.SelectedUnit, Is.Null);
            Assert.That(ranges.gameObject.activeSelf, Is.False);
            Assert.That(battle.Players[0].ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(battle.ActiveTeam, Is.EqualTo(UnitTeam.Enemy));
        }
        [UnityTest]
        public IEnumerator CameraControlsAndDoubleClickWorkDuringEnemyPause()
        {
            ui.enabled = false;
            battle.Combat.SetRollSource(() => 99);
            battle.Turns.TryEndTurn(); yield return EnemyPause();
            var initial = camera.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSecondsRealtime(0.1f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(Vector3.Distance(initial, camera.transform.position), Is.GreaterThan(0.01f));
            var rotation = camera.transform.rotation;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSecondsRealtime(0.55f);
            Assert.That(Quaternion.Angle(camera.transform.rotation, Quaternion.AngleAxis(90, Vector3.up) * rotation), Is.LessThan(0.05f));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSecondsRealtime(0.55f);
            Assert.That(Quaternion.Angle(camera.transform.rotation, rotation), Is.LessThan(0.05f));
            for (int i = 0; i < 2; i++)
            {
                Vector2 point = camera.WorldToScreenPoint(battle.Players[0].transform.position);
                Assert.That(Physics.Raycast(camera.ScreenPointToRay(point), out var hit), Is.True);
                Assert.That(hit.collider.GetComponent<BattleUnit>(), Is.SameAs(battle.Players[0]));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
            }
            yield return new WaitForSecondsRealtime(0.45f);
            var viewport = camera.WorldToViewportPoint(battle.Players[0].transform.position);
            Assert.That(viewport.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(viewport.y, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(input.SelectedUnit, Is.SameAs(battle.Players[0]));
            Assert.That(battle.Players[0].ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(ranges.gameObject.activeSelf, Is.False);
            Assert.That(battle.ActiveTeam, Is.EqualTo(UnitTeam.Enemy));
        }
    }
}
