using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class InputPresentationTests
    {
        private GameObject root;
        private GridManager grid;
        private BattleUnit unit;
        private PlayerInputController input;
        private GridVisualizer visualizer;
        private BattleDebugUI debugUI;
        private Camera camera;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorBehavior;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;

        // Exercise the same explicit action choice as the unit menu.
        private bool MoveSelected(Vector2Int destination)
        { input.BeginMove(); return input.TryMoveSelected(destination); }
        private bool SelectAttackTarget(BattleUnit target)
        { input.BeginAttack(); return input.TrySelectTarget(target); }
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            root = new GameObject("Input Tests");
            grid = root.AddComponent<GridManager>();
            grid.Generate();
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.transform.SetParent(root.transform);
            unit = capsule.AddTestBattleUnit();
            unit.Configure(Vector2Int.zero, 5);
            unit.Initialize(grid);
            visualizer = root.AddComponent<GridVisualizer>();
            visualizer.Initialize(grid);
            debugUI = root.AddComponent<BattleDebugUI>();
            debugUI.Initialize(grid, root.AddComponent<CoverSystem>());
            camera = new GameObject("Test Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform);
            camera.orthographic = true;
            camera.orthographicSize = 15;
            camera.transform.position = new Vector3(10, 28, -20);
            camera.transform.LookAt(new Vector3(10, 0, 10));
            input = root.AddComponent<PlayerInputController>();
            input.Initialize(camera, grid, visualizer, debugUI);
            Physics.SyncTransforms();
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorBehavior;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            Object.Destroy(root);
            yield return null;
        }
        private IEnumerator FinishMove()
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (unit.Movement.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(unit.Movement.IsMoving, Is.False);
            yield return null;
        }
        [UnityTest]
        public IEnumerator DebugCoverUsesEffectiveIncomingBonusAndTracksSelectionCell()
        {
            input.SelectUnit(unit);
            grid.TryGetCell(unit.CurrentGridPosition, out var cell);
            cell.SetCover(CoverDirection.South, CoverType.Full);
            // Read the same presentation value consumed by the directional rows.
            Assert.That(debugUI.GetIncomingCoverMultiplier(CoverDirection.SouthEast), Is.EqualTo(.5f));
            Assert.That(debugUI.GetIncomingCoverMultiplier(CoverDirection.East), Is.EqualTo(1f));
            grid.TryMoveUnit(unit, new Vector2Int(1, 0));
            Assert.That(debugUI.GetIncomingCoverMultiplier(CoverDirection.SouthEast), Is.EqualTo(1f));
            grid.TryGetCell(unit.CurrentGridPosition, out var movedCell);
            movedCell.SetCover(CoverDirection.East, CoverType.Half);
            Assert.That(debugUI.GetIncomingCoverMultiplier(CoverDirection.SouthEast), Is.EqualTo(.75f));
            debugUI.SetSelectedUnit(null);
            Assert.That(debugUI.GetIncomingCoverMultiplier(CoverDirection.SouthEast), Is.EqualTo(1f));
            yield return null;
        }
        [UnityTest]
        public IEnumerator SelectionShowsRangeAndVisualsDoNotInterceptClicks()
        {
            Assert.That(input.SelectUnit(unit), Is.True);
            Assert.That(root.transform.Find("Reachable Cells").gameObject.activeSelf, Is.False);
            Assert.That(input.BeginMove(), Is.True);
            var ranges = root.transform.Find("Reachable Cells");
            Assert.That(ranges, Is.Not.Null);
            Assert.That(ranges.childCount, Is.EqualTo(20));
            yield return null;
            Assert.That(ranges.GetComponentsInChildren<Collider>(), Is.Empty);
            var indicator = root.transform.Find("Selection Indicator");
            Assert.That(indicator.GetComponentsInChildren<Collider>(), Is.Empty);
            Assert.That(indicator.gameObject.activeSelf, Is.True);
        }
        [UnityTest]
        public IEnumerator MovingPreventsSelectionAndRefreshesRange()
        {
            input.SelectUnit(unit);
            Assert.That(MoveSelected(Vector2Int.right), Is.True);
            Assert.That(root.transform.Find("Reachable Cells").gameObject.activeSelf, Is.False);
            var other = new GameObject("Other").AddTestBattleUnit();
            other.transform.SetParent(root.transform);
            other.Configure(new Vector2Int(9, 9), 5);
            other.Initialize(grid);
            Assert.That(input.SelectUnit(other), Is.False);
            yield return FinishMove();
            Assert.That(root.transform.Find("Reachable Cells").gameObject.activeSelf, Is.False);
            Assert.That(input.BeginMove(), Is.True);
            Assert.That(root.transform.Find("Reachable Cells").gameObject.activeSelf, Is.True);
            Assert.That(input.SelectedUnit, Is.SameAs(unit));
        }
        [UnityTest]
        public IEnumerator DestroyedSelectionClears()
        {
            input.SelectUnit(unit);
            Object.Destroy(unit.gameObject);
            yield return null;
            yield return null;
            Assert.That(input.SelectedUnit, Is.Null);
            Assert.That(root.transform.Find("Reachable Cells").gameObject.activeSelf, Is.False);
            Assert.That(root.transform.Find("Selection Indicator").gameObject.activeSelf, Is.False);
        }
        [UnityTest]
        public IEnumerator InvalidCellIgnored()
        {
            input.SelectUnit(unit);
            grid.SetWalkable(Vector2Int.right, false);
            Assert.That(MoveSelected(Vector2Int.right), Is.False);
            Assert.That(MoveSelected(new Vector2Int(-1, 0)), Is.False);
            Assert.That(unit.Mobility.MovedCells, Is.Zero);
            yield return null;
        }
        [UnityTest]
        public IEnumerator MouseClickSelectsThenMoves()
        {
            // Real Input System events exercise Update, raycasting and ground-plane picking.
            debugUI.enabled = false;
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                var point = (Vector2)camera.WorldToScreenPoint(unit.transform.position);
                Assert.That(camera.pixelRect.Contains(point), Is.True, "Unit must be inside the camera viewport.");
                Assert.That(Physics.Raycast(camera.ScreenPointToRay(point), out var hit), Is.True);
                Assert.That(hit.collider.GetComponent<BattleUnit>(), Is.SameAs(unit), "Ray must hit the capsule.");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
                yield return null;
                Assert.That(mouse.leftButton.isPressed, Is.True, "Injected mouse event must reach the game.");
                Assert.That(input.SelectedUnit, Is.SameAs(unit));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                yield return null;
                Assert.That(input.BeginMove(), Is.True);
                point = camera.WorldToScreenPoint(grid.GetWorldPosition(Vector2Int.right));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                yield return FinishMove();
                Assert.That(unit.CurrentGridPosition, Is.EqualTo(Vector2Int.right));
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }
        [UnityTest]
        public IEnumerator DebugPanelClickDoesNotIssueMovement()
        {
            input.SelectUnit(unit);
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(40, Screen.height - 40) }.WithButton(MouseButton.Left));
                yield return null;
                Assert.That(unit.Movement.IsMoving, Is.False);
                Assert.That(unit.Mobility.MovedCells, Is.Zero);
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }
    }
}
