using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class UnitCameraFocusTests
    {
        private GameObject root;
        private Camera camera;
        private BattleCameraOrbit orbit;
        private PlayerInputController input;
        private BattleDebugUI ui;
        private BattleUnit first, second, enemy;
        private TurnManager turns;
        private CombatSystem combat;
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
            root = new GameObject("Temporary Squad");
            var grid = root.AddComponent<GridManager>(); grid.Generate();
            var covers = root.AddComponent<CoverSystem>();
            first = MakeUnit(grid, new Vector2Int(1, 1), UnitTeam.Player);
            second = MakeUnit(grid, new Vector2Int(1, 3), UnitTeam.Player);
            enemy = MakeUnit(grid, new Vector2Int(3, 3), UnitTeam.Enemy);
            camera = new GameObject("Camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
            camera.orthographic = true; camera.orthographicSize = 15;
            camera.rect = new Rect(0.23f, 0, 0.77f, 1);
            camera.transform.position = new Vector3(10, 28, -20);
            camera.transform.LookAt(new Vector3(10, 0, 10));
            orbit = camera.gameObject.AddComponent<BattleCameraOrbit>(); orbit.Initialize(grid);
            turns = root.AddComponent<TurnManager>(); turns.Initialize(new[] { first, second, enemy });
            var visibility = root.AddComponent<VisibilitySystem>();
            visibility.Initialize(grid, new[] { first, second, enemy });
            turns.ConfigureVisibility(visibility);
            combat = root.AddComponent<CombatSystem>(); combat.Initialize(grid, covers, turns); combat.SetRollSource(() => 0);
            var ai = root.AddComponent<EnemyAIController>(); ai.Initialize(grid, combat, turns); turns.ConfigureEnemyAI(ai);
            var visualizer = root.AddComponent<GridVisualizer>(); visualizer.Initialize(grid);
            ui = root.AddComponent<BattleDebugUI>(); ui.Initialize(grid, covers); ui.enabled = false;
            input = root.AddComponent<PlayerInputController>(); input.Initialize(camera, grid, visualizer, ui);
            input.InitializeCombat(combat, turns);
            mouse = InputSystem.AddDevice<Mouse>();
            Physics.SyncTransforms(); yield return null;
        }

        private BattleUnit MakeUnit(GridManager grid, Vector2Int position, UnitTeam team)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Capsule); obj.transform.SetParent(root.transform);
            var unit = obj.AddTestBattleUnit(); unit.Configure(position, 5); unit.ConfigureTeam(team);
            unit.Initialize(grid); return unit;
        }

        private IEnumerator Click(BattleUnit unit)
        {
            Vector2 point = camera.WorldToScreenPoint(unit.transform.position);
            Assert.That(camera.pixelRect.Contains(point), Is.True,
                $"Unit {unit.name} projects to {point}; viewport {camera.pixelRect}, screen {Screen.width}x{Screen.height}.");
            Assert.That(Physics.Raycast(camera.ScreenPointToRay(point), out var hit), Is.True);
            Assert.That(hit.collider.GetComponentInParent<BattleUnit>(), Is.SameAs(unit));
            yield return ClickPoint(point);
        }

        private IEnumerator ClickPoint(Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            InputSystem.settings.backgroundBehavior = oldBackground;
            Object.Destroy(root); yield return null;
        }

        [UnityTest]
        public IEnumerator DoubleClickCentersUnitSmoothlyWithoutChangingAngleZoomOrAP()
        {
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            first.ActionPoints.TrySpend(2);
            yield return Click(first);
            Assert.That(input.SelectedUnit, Is.SameAs(first));
            Assert.That(camera.transform.position, Is.EqualTo(position));
            yield return Click(first);
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(Vector3.Distance(camera.transform.position, position), Is.GreaterThan(0.01f));
            Assert.That(Vector3.Distance(camera.WorldToViewportPoint(first.transform.position), new Vector3(0.5f, 0.5f, camera.WorldToViewportPoint(first.transform.position).z)), Is.GreaterThan(0.001f));
            yield return new WaitForSecondsRealtime(0.5f);
            var point = camera.WorldToViewportPoint(first.transform.position);
            Assert.That(point.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(point.y, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(Quaternion.Angle(rotation, camera.transform.rotation), Is.LessThan(0.05f));
            Assert.That(camera.orthographicSize, Is.EqualTo(15));
            Assert.That(camera.transform.position.y, Is.EqualTo(position.y).Within(0.001f));
            Assert.That(first.ActionPoints.CurrentPoints, Is.EqualTo(3));
            Assert.That(second.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator SwitchingUnitsKeepsSeparateBudgetsAndDoesNotCountAsDoubleClick()
        {
            var position = camera.transform.position;
            input.SelectUnit(first);
            Assert.That(MoveSelected(new Vector2Int(2, 1)), Is.True);
            float deadline = Time.realtimeSinceStartup + 3;
            while (first.Movement.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(first.Movement.IsMoving, Is.False);
            Assert.That(first.CurrentGridPosition, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(first.Movement.TryKeepArrivalFacing(), Is.True);
            yield return null;
            yield return Click(first); yield return Click(second);
            Assert.That(input.SelectedUnit, Is.SameAs(second));
            Assert.That(SelectAttackTarget(enemy), Is.True);
            Assert.That(input.TryAttackSelected().WasExecuted, Is.True);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Click(first);
            Assert.That(input.SelectedUnit, Is.SameAs(first));
            Assert.That(camera.transform.position, Is.EqualTo(position));
            Assert.That(first.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(second.ActionPoints.CurrentPoints, Is.EqualTo(2));
            combat.SetRollSource(() => 99);
            Assert.That(input.TryEndTurn(), Is.True);
            Assert.That(first.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(second.ActionPoints.CurrentPoints, Is.EqualTo(2));
            deadline = Time.realtimeSinceStartup + 5;
            while (!turns.IsPlayerTurn && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(turns.IsPlayerTurn, Is.True);
            Assert.That(first.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(second.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(first.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(second.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
        }

        [UnityTest]
        public IEnumerator SlowClicksAndEnemyClicksDoNotFocusCamera()
        {
            var position = camera.transform.position;
            yield return Click(first); yield return new WaitForSecondsRealtime(0.4f); yield return Click(first);
            yield return Click(enemy); yield return Click(first);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(camera.transform.position, Is.EqualTo(position));
        }

        [UnityTest]
        public IEnumerator EscapeClearsSelectionTargetAndIndicatorsWithoutSpendingAP()
        {
            yield return Click(first);
            Assert.That(SelectAttackTarget(enemy), Is.True);
            first.ActionPoints.TrySpend(1);
            var position = camera.transform.position;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(input.SelectedUnit, Is.Null);
                Assert.That(input.TargetUnit, Is.Null);
                Assert.That(root.transform.Find("Reachable Cells").gameObject.activeSelf, Is.False);
                Assert.That(root.transform.Find("Selection Indicator").gameObject.activeSelf, Is.False);
                Assert.That(first.ActionPoints.CurrentPoints, Is.EqualTo(4));
                Assert.That(second.ActionPoints.CurrentPoints, Is.EqualTo(5));
                yield return Click(first);
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.That(camera.transform.position, Is.EqualTo(position), "Escape must interrupt the double-click sequence.");
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        [UnityTest]
        public IEnumerator EscapeDuringMovementDeselectsWithoutCancelingTheMove()
        {
            input.SelectUnit(first);
            Assert.That(MoveSelected(new Vector2Int(2, 1)), Is.True);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.That(input.SelectedUnit, Is.Null);
                float deadline = Time.realtimeSinceStartup + 3;
                while (first.Movement.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(first.Movement.IsMoving, Is.False);
                Assert.That(first.CurrentGridPosition, Is.EqualTo(new Vector2Int(2, 1)));
                Assert.That(first.ActionPoints.CurrentPoints, Is.EqualTo(4));
                Assert.That(second.ActionPoints.CurrentPoints, Is.EqualTo(5));
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        [UnityTest]
        public IEnumerator SPansCameraWithoutClearingSelectionOrChangingAPAndDoubleClickStillFocuses()
        {
            yield return Click(first);
            var start = camera.transform.position;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                yield return new WaitForSecondsRealtime(0.15f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(input.SelectedUnit, Is.SameAs(first));
                Assert.That(first.ActionPoints.CurrentPoints, Is.EqualTo(5));
                Assert.That(second.ActionPoints.CurrentPoints, Is.EqualTo(5));
                Assert.That(Vector3.Distance(camera.transform.position, start), Is.GreaterThan(0.1f));
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Click(first); yield return Click(first);
                yield return new WaitForSecondsRealtime(0.5f);
                var point = camera.WorldToViewportPoint(first.transform.position);
                Assert.That(point.x, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(point.y, Is.EqualTo(0.5f).Within(0.001f));
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        [UnityTest]
        public IEnumerator EmptySpaceClickClearsSelectionTargetAndRangeWithoutSpendingAP()
        {
            yield return Click(first);
            Assert.That(SelectAttackTarget(enemy), Is.True);
            Vector2 point = camera.ViewportToScreenPoint(new Vector3(0.95f, 0.95f, 0));
            Assert.That(camera.pixelRect.Contains(point), Is.True);
            var ray = camera.ScreenPointToRay(point);
            Assert.That(Physics.Raycast(ray, out _), Is.False);
            var grid = root.GetComponent<GridManager>();
            Assert.That(new Plane(Vector3.up, grid.transform.position).Raycast(ray, out var distance), Is.True);
            Assert.That(grid.TryWorldToGrid(ray.GetPoint(distance), out _), Is.False);
            yield return ClickPoint(point);
            Assert.That(input.SelectedUnit, Is.Null);
            Assert.That(input.TargetUnit, Is.Null);
            Assert.That(root.transform.Find("Reachable Cells").gameObject.activeSelf, Is.False);
            Assert.That(root.transform.Find("Selection Indicator").gameObject.activeSelf, Is.False);
            Assert.That(first.ActionPoints.CurrentPoints, Is.EqualTo(5));
            var position = camera.transform.position;
            yield return Click(first);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(camera.transform.position, Is.EqualTo(position), "An empty-space click must also interrupt double-click detection.");
        }

        [UnityTest]
        public IEnumerator GridClickStillMovesSelectedUnitAndKeepsSelection()
        {
            yield return Click(first);
            Assert.That(input.BeginMove(), Is.True);
            yield return ClickPoint(camera.WorldToScreenPoint(new Vector3(5, 0, 3)));
            float deadline = Time.realtimeSinceStartup + 3;
            while (first.Movement.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(first.CurrentGridPosition, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(input.SelectedUnit, Is.SameAs(first));
            Assert.That(first.ActionPoints.CurrentPoints, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator UIClickInterruptsDoubleClickSequence()
        {
            var position = camera.transform.position;
            yield return Click(first);
            ui.enabled = true;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.25f, 1);
            var point = new Vector2(30 * scale, Screen.height - 50 * scale);
            Assert.That(ui.ContainsScreenPoint(point), Is.True);
            yield return ClickPoint(point);
            Assert.That(input.SelectedUnit, Is.SameAs(first));
            ui.enabled = false;
            yield return Click(first); yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(camera.transform.position, Is.EqualTo(position));
        }

        [UnityTest]
        public IEnumerator FocusedOrbitKeepsUnitCenteredAndDoesNotFollowItsMovement()
        {
            Assert.That(orbit.TryFocus(first.transform.position), Is.True);
            yield return new WaitForSecondsRealtime(0.5f);
            var focusedPosition = camera.transform.position;
            var rotation = camera.transform.rotation;
            for (int i = 0; i < 4; i++)
            {
                orbit.RotateRight(); yield return new WaitForSecondsRealtime(0.6f);
                var point = camera.WorldToViewportPoint(first.transform.position);
                Assert.That(point.x, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(point.y, Is.EqualTo(0.5f).Within(0.001f));
            }
            Assert.That(Vector3.Distance(camera.transform.position, focusedPosition), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, rotation), Is.LessThan(0.05f));
            input.SelectUnit(first); Assert.That(MoveSelected(new Vector2Int(2, 1)), Is.True);
            float deadline = Time.realtimeSinceStartup + 3;
            while (first.Movement.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(first.Movement.IsMoving, Is.False);
            Assert.That(first.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(second.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(Vector3.Distance(camera.transform.position, focusedPosition), Is.LessThan(0.01f));
        }
    }
}
