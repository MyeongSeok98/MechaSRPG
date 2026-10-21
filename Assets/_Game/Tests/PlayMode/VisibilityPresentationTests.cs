using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace MechaXCOM.Tests
{
    public class VisibilityPresentationTests
    {
        private GameObject root;
        private GridManager grid;
        private BattleUnit player, spotter, enemy;
        private BattleSceneController controller;
        private TurnManager turns;
        private PlayerInputController input;
        private Camera camera;
        private Renderer enemyRenderer, originallyDisabled;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private InputSettings.BackgroundBehavior oldBackground;

        [UnitySetUp] public IEnumerator Setup()
        {
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            root = new GameObject("Visibility presentation battle");
            grid = root.AddComponent<GridManager>(); grid.Configure(20, 12, 2);
            player = Unit(new Vector2Int(1, 1), UnitTeam.Player, 3);
            spotter = Unit(new Vector2Int(3, 3), UnitTeam.Player, 0);
            enemy = Unit(new Vector2Int(12, 1), UnitTeam.Enemy, 20);
            enemyRenderer = enemy.GetComponent<Renderer>();
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.transform.SetParent(enemy.transform, false); child.name = "Originally hidden model";
            child.GetComponent<Collider>().enabled = false;
            originallyDisabled = child.GetComponent<Renderer>(); originallyDisabled.enabled = false;
            turns = root.AddComponent<TurnManager>();
            var combat = root.AddComponent<CombatSystem>(); combat.SetRollSource(() => 0);
            var cover = root.AddComponent<CoverSystem>();
            var visualizer = root.AddComponent<GridVisualizer>();
            var debug = root.AddComponent<BattleDebugUI>(); debug.enabled = false;
            input = root.AddComponent<PlayerInputController>();
            camera = new GameObject("Camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
            camera.orthographic = true; camera.orthographicSize = 20;
            camera.rect = new Rect(.23f, 0, .77f, 1);
            camera.transform.position = new Vector3(25, 40, 3);
            camera.transform.LookAt(new Vector3(25, 0, 3), Vector3.forward);
            controller = root.AddComponent<BattleSceneController>();
            controller.Configure(grid, cover, new[] { player, spotter, enemy }, null, null);
            controller.ConfigureCombat(combat, turns);
            controller.ConfigurePresentation(camera, input, visualizer, debug);
            Assert.That(controller.InitializeBattle(), Is.True);
            Physics.SyncTransforms();
            yield return null;
        }
        private BattleUnit Unit(Vector2Int position, UnitTeam team, int range)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Capsule); obj.transform.SetParent(root.transform);
            obj.AddComponent<UnitVision>().Configure(range);
            var unit = obj.AddTestBattleUnit(); unit.Configure(position, 5); unit.ConfigureTeam(team);
            unit.GetComponent<UnitCombat>().ConfigureTestRanged(5, 80, 30, 3);
            return unit;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            InputSystem.settings.backgroundBehavior = oldBackground;
            Object.Destroy(root); yield return null;
        }
        private void Reveal() => spotter.Vision.Configure(10);
        private IEnumerator ClickEnemy()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                Physics.SyncTransforms();
                Vector2 point = camera.WorldToScreenPoint(enemy.transform.position);
                Assert.That(Physics.Raycast(camera.ScreenPointToRay(point), out var hit), Is.True);
                Assert.That(hit.collider.GetComponentInParent<BattleUnit>(), Is.SameAs(enemy));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
                yield return null;
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }
        [UnityTest] public IEnumerator InitiallyHiddenEnemyHasNoModelShadowSelectionOrIndicator()
        {
            Assert.That(enemy.gameObject.activeSelf, Is.True);
            Assert.That(enemyRenderer.enabled, Is.False, "Disabled model renderers cannot cast shadows either.");
            Assert.That(enemy.GetComponent<Collider>().enabled, Is.True);
            Assert.That(input.SelectUnit(player), Is.True); Assert.That(input.BeginAttack(), Is.True);
            Assert.That(input.TrySelectTarget(enemy), Is.False);
            yield return ClickEnemy();
            Assert.That(input.TargetUnit, Is.Null);
            Assert.That(input.SelectedUnit, Is.SameAs(player));
            Assert.That(input.GetComponent<BodyTargetUI>().IsVisible, Is.False);
            Assert.That(turns.TryEndTurn(), Is.True);
            float deadline = Time.realtimeSinceStartup + 3;
            while (!turns.ActiveEnemy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(turns.ActiveEnemy, Is.SameAs(enemy));
            yield return null;
            Assert.That(root.transform.Find("Enemy Action Indicator").gameObject.activeSelf, Is.False);
            Assert.That(root.GetComponent<BattleDebugUI>().VisibleActingEnemy, Is.Null);
        }
        [UnityTest] public IEnumerator SharedDiscoveryShowsEnemyAndAllowsTargeting()
        {
            Reveal();
            Assert.That(enemyRenderer.enabled, Is.True);
            Assert.That(input.SelectUnit(player), Is.True); Assert.That(input.BeginAttack(), Is.True);
            yield return ClickEnemy();
            Assert.That(input.TargetUnit, Is.SameAs(enemy));
            Assert.That(input.GetComponent<BodyTargetUI>().IsVisible, Is.True);
            Assert.That(turns.TryEndTurn(), Is.True);
            float deadline = Time.realtimeSinceStartup + 3;
            while (!turns.ActiveEnemy && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.That(root.transform.Find("Enemy Action Indicator").gameObject.activeSelf, Is.True);
            Assert.That(root.GetComponent<BattleDebugUI>().VisibleActingEnemy, Is.SameAs(enemy));
            spotter.Vision.Configure(0);
            Assert.That(root.transform.Find("Enemy Action Indicator").gameObject.activeSelf, Is.False);
        }
        [Test] public void LastSightLostClosesTargetPreview()
        {
            Reveal(); input.SelectUnit(player); input.BeginAttack(); input.TrySelectTarget(enemy);
            Assert.That(input.TryAttackSelected().WasExecuted, Is.True);
            player.ActionPoints.ResetForTurn();
            input.BeginAttack(); input.TrySelectTarget(enemy); input.TrySelectBodyPart(TargetBodyPart.Head);
            player.Combat.ConfigureTestRanged(5, 80, 3, 3);
            Assert.That(input.TryAttackSelected().WasExecuted, Is.False);
            Assert.That(input.InputMessage, Is.Not.Null);
            Assert.That(input.LastAttackResult.WasExecuted, Is.True);
            spotter.Vision.Configure(0);
            Assert.That(input.TargetUnit, Is.Null);
            Assert.That(input.SelectedBodyPart, Is.EqualTo(TargetBodyPart.Whole));
            Assert.That(input.LastAttackResult.WasExecuted, Is.False);
            Assert.That(input.InputMessage, Is.Null);
            Assert.That(input.SelectedUnit, Is.SameAs(player));
            Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Attack));
            Assert.That(input.CurrentAttackPreview.CanAttack, Is.False);
            Assert.That(input.GetComponent<BodyTargetUI>().IsVisible, Is.False);
            Assert.That(input.GetComponent<BodyTargetUI>().SelectedPartInfo, Is.Empty);
        }
        [Test] public void HiddenUnitStillOccupiesCellAndCountsForVictory()
        {
            grid.TryGetCell(enemy.CurrentGridPosition, out var cell);
            Assert.That(cell.OccupyingUnit, Is.SameAs(enemy));
            Assert.That(turns.IsBattleOver, Is.False);
            player.Health.ApplyDamage(TargetBodyPart.Torso, 20);
            spotter.Health.ApplyDamage(TargetBodyPart.Torso, 20);
            Assert.That(turns.IsBattleOver, Is.True);
            Assert.That(turns.IsPlayerVictory, Is.False);
        }
        [Test] public void RestorePreservesOriginallyDisabledRenderer()
        {
            Reveal();
            Assert.That(enemyRenderer.enabled, Is.True);
            Assert.That(originallyDisabled.enabled, Is.False);
            spotter.Vision.Configure(0); Reveal();
            Assert.That(enemyRenderer.enabled, Is.True);
            Assert.That(originallyDisabled.enabled, Is.False);
        }
        [TestCase(false)] [TestCase(true)] public void DestroyedOrEjectedEnemyNeverReappears(bool eject)
        {
            var presenter = enemy.GetComponent<UnitVisibilityPresenter>();
            Assert.That(presenter, Is.Not.Null);
            if (eject) enemy.Health.MarkEjected();
            else enemy.Health.ApplyDamage(TargetBodyPart.Torso, 20);
            Reveal(); turns.Visibility.Refresh();
            Assert.That(enemy.gameObject.activeSelf, Is.False);
            Assert.That(enemy.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled), Is.True);
        }
        [Test] public void RepeatedInitializationDoesNotDuplicatePresenters()
        {
            Assert.That(controller.InitializeBattle(), Is.True);
            foreach (var unit in new[] { player, spotter, enemy })
                Assert.That(unit.GetComponents<UnitVisibilityPresenter>(), Has.Length.EqualTo(1));
            var presenter = enemy.GetComponent<UnitVisibilityPresenter>();
            presenter.Initialize(enemy, turns.Visibility);
            Reveal();
            Assert.That(enemyRenderer.enabled, Is.True);
            Assert.That(originallyDisabled.enabled, Is.False);
        }
        [Test] public void DisabledVisibilityHidesEnemyAndClosesAim()
        {
            Reveal(); input.SelectUnit(player); input.BeginAttack(); input.TrySelectTarget(enemy);
            input.TrySelectBodyPart(TargetBodyPart.Head);
            turns.Visibility.enabled = false;
            Assert.That(enemyRenderer.enabled, Is.False);
            Assert.That(input.TargetUnit, Is.Null);
            Assert.That(input.SelectedBodyPart, Is.EqualTo(TargetBodyPart.Whole));
            Assert.That(player.GetComponent<Renderer>().enabled, Is.True);
            turns.Visibility.enabled = true;
            Assert.That(enemyRenderer.enabled, Is.True);
            Assert.That(input.TargetUnit, Is.Null, "Rediscovery must not auto-aim.");
        }
        [UnityTest] public IEnumerator SightLossKeepsPendingFacing()
        {
            Reveal(); input.SelectUnit(player); input.BeginMove();
            Assert.That(input.TryMoveSelected(new Vector2Int(2, 1)), Is.True);
            float deadline = Time.realtimeSinceStartup + 3;
            while (player.Movement.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Facing));
            spotter.Vision.Configure(0);
            Assert.That(player.HasPendingFacing, Is.True);
            Assert.That(input.SelectedUnit, Is.SameAs(player));
            Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Facing));
        }
        [Test] public void DisablingPresenterRestoresOriginalFlagsAndUnsubscribes()
        {
            var presenter = enemy.GetComponent<UnitVisibilityPresenter>();
            Assert.That(presenter, Is.Not.Null);
            presenter.enabled = false;
            Assert.That(enemyRenderer.enabled, Is.True);
            Assert.That(originallyDisabled.enabled, Is.False);
            Reveal(); spotter.Vision.Configure(0);
            Assert.That(enemyRenderer.enabled, Is.True);
            presenter.enabled = true;
            Assert.That(enemyRenderer.enabled, Is.False);
            Reveal(); Assert.That(enemyRenderer.enabled, Is.True);
            Assert.That(originallyDisabled.enabled, Is.False);
        }
        [Test] public void ReenabledHiddenUnitRetainsOriginalRendererFlags()
        {
            Assert.That(enemyRenderer.enabled, Is.False);
            enemy.gameObject.SetActive(false);
            enemy.gameObject.SetActive(true);
            Assert.That(enemyRenderer.enabled, Is.False);
            Reveal();
            Assert.That(enemyRenderer.enabled, Is.True);
            Assert.That(originallyDisabled.enabled, Is.False);
        }
        [Test] public void DisabledEnemyComponentHidesModelUntilReenabled()
        {
            Reveal(); Assert.That(enemyRenderer.enabled, Is.True);
            enemy.enabled = false;
            Assert.That(enemyRenderer.enabled, Is.False);
            enemy.enabled = true;
            Assert.That(enemyRenderer.enabled, Is.True);
            Assert.That(originallyDisabled.enabled, Is.False);
        }
        [Test] public void AimRemainsUntilLastSharedObserverLosesSight()
        {
            Reveal(); player.Vision.Configure(20);
            input.SelectUnit(player); input.BeginAttack(); input.TrySelectTarget(enemy);
            input.TrySelectBodyPart(TargetBodyPart.Head);
            spotter.Vision.Configure(0);
            Assert.That(enemyRenderer.enabled, Is.True);
            Assert.That(input.TargetUnit, Is.SameAs(enemy));
            Assert.That(input.SelectedBodyPart, Is.EqualTo(TargetBodyPart.Head));
            player.Vision.Configure(0);
            Assert.That(enemyRenderer.enabled, Is.False);
            Assert.That(input.TargetUnit, Is.Null);
            Assert.That(input.SelectedBodyPart, Is.EqualTo(TargetBodyPart.Whole));
        }
#if UNITY_EDITOR
        [UnityTest] public IEnumerator OneVOneUsesAuthoredLayoutForInitialVisibility()
        { yield return CheckSavedScene("1V1Test"); }
        [UnityTest] public IEnumerator SquadUsesAuthoredLayoutForInitialVisibility()
        { yield return CheckSavedScene("SqurdWeaponTest"); }
        private IEnumerator CheckSavedScene(string name)
        {
            string path = "Assets/_Game/Scenes/" + name + ".unity";
            byte[] before = File.ReadAllBytes(path);
            EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            try
            {
                yield return null; yield return null;
                var sceneTurns = Object.FindFirstObjectByType<TurnManager>();
                var sceneController = Object.FindFirstObjectByType<BattleSceneController>();
                Assert.That(sceneTurns.Visibility.IsReady, Is.True);
                Assert.That(sceneController.InitializeBattle(), Is.True);
                var units = Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None);
                Assert.That(units, Is.Not.Empty);
                foreach (var unit in units.Where(u => u.IsAlive))
                {
                    Assert.That(unit.GetComponents<UnitVisibilityPresenter>(), Has.Length.EqualTo(1));
                    var renderers = unit.GetComponentsInChildren<Renderer>(true);
                    if (unit.Team == UnitTeam.Enemy && !sceneTurns.Visibility.IsVisibleTo(UnitTeam.Player, unit))
                        Assert.That(renderers.All(r => !r.enabled), Is.True);
                    else
                        Assert.That(renderers.Any(r => r.enabled), Is.True, "Visible models retain their authored renderers.");
                    var sceneGrid = Object.FindFirstObjectByType<GridManager>();
                    Assert.That(sceneGrid.TryGetCell(unit.CurrentGridPosition, out var cell), Is.True);
                    Assert.That(cell.OccupyingUnit, Is.SameAs(unit));
                }
            }
            finally
            {
                var oldScene = SceneManager.GetActiveScene();
                SceneManager.SetActiveScene(SceneManager.CreateScene("Visibility scene cleanup"));
                SceneManager.UnloadSceneAsync(oldScene);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            }
            yield return null;
        }
#endif
    }
}
