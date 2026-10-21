using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class CombatInputTests
    {
        private GameObject root;
        private BattleUnit player, enemy;
        private PlayerInputController input;
        private CombatSystem combat;
        private TurnManager turns;
        private BattleDebugUI ui;
        private Camera camera;
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
            root = new GameObject("Combat Input");
            var grid = root.AddComponent<GridManager>(); grid.Generate();
            var covers = root.AddComponent<CoverSystem>();
            player = MakeUnit(grid, new Vector2Int(1, 1), UnitTeam.Player);
            enemy = MakeUnit(grid, new Vector2Int(3, 3), UnitTeam.Enemy);
            var visualizer = root.AddComponent<GridVisualizer>(); visualizer.Initialize(grid);
            ui = root.AddComponent<BattleDebugUI>(); ui.Initialize(grid, covers);
            camera = new GameObject("Camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
            camera.orthographic = true; camera.orthographicSize = 15;
            camera.transform.position = new Vector3(10, 28, -20); camera.transform.LookAt(new Vector3(10, 0, 10));
            turns = root.AddComponent<TurnManager>(); turns.Initialize(new[] { player, enemy });
            var visibility = root.AddComponent<VisibilitySystem>();
            visibility.Initialize(grid, new[] { player, enemy });
            turns.ConfigureVisibility(visibility);
            combat = root.AddComponent<CombatSystem>(); combat.Initialize(grid, covers, turns);
            var ai = root.AddComponent<EnemyAIController>(); ai.Initialize(grid, combat, turns); turns.ConfigureEnemyAI(ai);
            input = root.AddComponent<PlayerInputController>(); input.Initialize(camera, grid, visualizer, ui);
            input.InitializeCombat(combat, turns); ui.InitializeCombat(input, turns);
            Physics.SyncTransforms(); yield return null;
        }
        private BattleUnit MakeUnit(GridManager grid, Vector2Int pos, UnitTeam team)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Capsule); obj.transform.SetParent(root.transform);
            var unit = obj.AddTestBattleUnit(); unit.Configure(pos, 5); unit.ConfigureTeam(team);
            unit.Initialize(grid); return unit;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            InputSystem.settings.backgroundBehavior = oldBackground;
            Object.Destroy(root); yield return null;
        }
        [UnityTest]
        public IEnumerator BuildingBlockedPreviewDisablesAttackAndRefreshesAfterRepositioning()
        {
            var grid = root.GetComponent<GridManager>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform);
            cube.transform.position = grid.GetWorldPosition(new Vector2Int(2, 2)) + Vector3.up;
            cube.transform.localScale = new Vector3(2, 4, 2);
            grid.RegisterObstacle(cube.AddComponent<GridObstacle>().Collider);
            input.SelectUnit(player);
            Assert.That(SelectAttackTarget(enemy), Is.True);
            Assert.That(input.CurrentAttackPreview.BlockedByBuilding, Is.True);
            Assert.That(input.CurrentAttackPreview.CanAttack, Is.False);
            int rolls = 0; combat.SetRollSource(() => { rolls++; return 0; });
            Assert.That(input.TryAttackSelected().WasExecuted, Is.False);
            Assert.That(input.InputMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(rolls, Is.Zero);
            Assert.That(grid.TryMoveUnit(player, new Vector2Int(1, 4)), Is.True);
            Assert.That(input.CurrentAttackPreview.BlockedByBuilding, Is.False);
            Assert.That(input.CurrentAttackPreview.CanAttack, Is.True);
            yield return null;
        }
        [UnityTest]
        public IEnumerator EnemyClickKeepsPlayerSelection()
        {
            Assert.That(input.SelectUnit(enemy), Is.False);
            Assert.That(SelectAttackTarget(enemy), Is.False);
            Assert.That(input.SelectUnit(player), Is.True); ui.enabled = false;
            Assert.That(input.BeginAttack(), Is.True);
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                Vector2 point = camera.WorldToScreenPoint(enemy.transform.position);
                Assert.That(Physics.Raycast(camera.ScreenPointToRay(point), out var hit), Is.True);
                Assert.That(hit.collider.GetComponent<BattleUnit>(), Is.SameAs(enemy));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
                yield return null;
                Assert.That(input.SelectedUnit, Is.SameAs(player)); Assert.That(input.TargetUnit, Is.SameAs(enemy));
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }
        [UnityTest]
        public IEnumerator AttackPreviewMatchesExecution()
        {
            input.SelectUnit(player); SelectAttackTarget(enemy); combat.SetRollSource(() => 79);
            Assert.That(input.CurrentAttackPreview.HitChance, Is.EqualTo(80));
            var result = input.TryAttackSelected();
            Assert.That(result.Hit, Is.True); Assert.That(result.HitChance, Is.EqualTo(80));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(input.CurrentAttackPreview.CanAttack, Is.False);
            Assert.That(input.LastAttackResult.WasExecuted, Is.True);
            Assert.That(input.TryEndTurn(), Is.True);
            Assert.That(input.TargetUnit, Is.Null); Assert.That(input.LastAttackResult.WasExecuted, Is.False);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
            float deadline = Time.realtimeSinceStartup + 5;
            while (!turns.IsPlayerTurn && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(turns.IsPlayerTurn, Is.True);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            yield return null;
        }
        [UnityTest]
        public IEnumerator DisabledUnitClearsRangeAndTarget()
        {
            input.SelectUnit(player); SelectAttackTarget(enemy);
            player.Movement.enabled = false; yield return null;
            Assert.That(input.SelectedUnit, Is.Null); Assert.That(input.TargetUnit, Is.Null);
            Assert.That(root.transform.Find("Reachable Cells").gameObject.activeSelf, Is.False);
        }
        [UnityTest]
        public IEnumerator DisabledTargetColliderCannotBeSelected()
        {
            input.SelectUnit(player); enemy.GetComponent<Collider>().enabled = false;
            Assert.That(SelectAttackTarget(enemy), Is.False);
            yield return null;
        }
        [UnityTest]
        public IEnumerator UIRectBlocksMapInput()
        {
            input.SelectUnit(player);
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                var point = new Vector2(30 * Mathf.Min(Screen.width / 1280f, Screen.height / 720f), Screen.height - 100 * Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
                Assert.That(ui.ContainsScreenPoint(point), Is.True);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
                yield return null;
                Assert.That(player.Movement.IsMoving, Is.False); Assert.That(input.TargetUnit, Is.Null);
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }
    }
}
