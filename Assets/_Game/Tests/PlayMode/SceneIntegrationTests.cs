#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    // SavedScene means the generated, test-owned scene; never an authored gameplay scene.
    [PrebuildSetup(typeof(SceneIntegrationSetup)), PostBuildCleanup(typeof(SceneIntegrationSetup))]
    public class SceneIntegrationTests
    {
        private BattleUnit unit;
        private GridManager grid;
        private PlayerInputController input;
        private CoverSystem cover;
        private BattleUnit enemy;
        private TurnManager turns;
        private CombatSystem combat;
        private byte[] savedSceneBytes;
        private string loadedScenePath;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditor;
        private InputSettings.BackgroundBehavior previousBackground;
        // Exercise the same explicit action choice as the unit menu.
        private bool MoveSelected(Vector2Int destination)
        { input.BeginMove(); return input.TryMoveSelected(destination); }
        private bool SelectAttackTarget(BattleUnit target)
        { input.BeginAttack(); return input.TrySelectTarget(target); }
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            loadedScenePath = SceneIntegrationSetup.ScenePath;
            savedSceneBytes = File.ReadAllBytes(loadedScenePath);
            EditorSceneManager.LoadSceneInPlayMode(SceneIntegrationSetup.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            ReadBattle();
        }
        private void ReadBattle()
        {
            unit = Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None).Single(u => u.Team == UnitTeam.Player && u.StartGridPosition == new Vector2Int(1, 1));
            enemy = Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None).Single(u => u.Team == UnitTeam.Enemy && u.StartGridPosition == new Vector2Int(3, 3));
            turns = Object.FindFirstObjectByType<TurnManager>();
            combat = Object.FindFirstObjectByType<CombatSystem>();
            combat.SetRollSource(() => 99);
            grid = Object.FindFirstObjectByType<GridManager>();
            input = Object.FindFirstObjectByType<PlayerInputController>();
            cover = Object.FindFirstObjectByType<CoverSystem>();
            Assert.That(unit.IsInitialized, Is.True);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditor;
            InputSystem.settings.backgroundBehavior = previousBackground;
            var old = SceneManager.GetActiveScene();
            var empty = SceneManager.CreateScene("Integration Cleanup");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(old);
            Assert.That(File.ReadAllBytes(loadedScenePath), Is.EqualTo(savedSceneBytes), "Play must not save changes to the test scene.");
        }
        private IEnumerator LoadGeneratedSquad()
        {
            loadedScenePath = SceneIntegrationSetup.SquadScenePath;
            savedSceneBytes = File.ReadAllBytes(loadedScenePath);
            EditorSceneManager.LoadSceneInPlayMode(loadedScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            ReadBattle();
        }
        [UnityTest]
        public IEnumerator AuthoredScenesUseWeaponDataWithoutSaving()
        {
            foreach (var name in new[] { "1V1Test", "SqurdWeaponTest" })
            {
                loadedScenePath = "Assets/_Game/Scenes/" + name + ".unity";
                savedSceneBytes = File.ReadAllBytes(loadedScenePath);
                EditorSceneManager.LoadSceneInPlayMode(loadedScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                yield return null; yield return null;
                foreach (var loaded in Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None))
                {
                    var loadout = loaded.GetComponent<UnitWeaponLoadout>();
                    Assert.That(loadout, Is.Not.Null);
                    Assert.That(loadout.IsValid(out var reason), Is.True, reason);
                    foreach (var type in new[] { AttackType.Ranged, AttackType.Melee })
                    {
                        if (loaded.Combat.GetAttackSlots(type).Count == 0) continue;
                        var stats = loaded.Combat.GetAttack(type);
                        Assert.That(stats.Weapon, Is.Not.Null);
                        Assert.That(stats.MinDamage, Is.EqualTo(stats.Weapon.MinDamage));
                        Assert.That(stats.MaxDamage, Is.EqualTo(stats.Weapon.MaxDamage));
                    }
                }
                Assert.That(File.ReadAllBytes(loadedScenePath), Is.EqualTo(savedSceneBytes));
            }
        }
        [Test]
        public void SampleWeaponAssetsCanDriveTestSceneWithoutSaving()
        {
            var rifle = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Game/Data/Weapons/Ranged/StandardRifle.asset");
            var blade = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Game/Data/Weapons/Melee/StandardBlade.asset");
            Assert.That(rifle, Is.Not.Null); Assert.That(blade, Is.Not.Null);
            unit.gameObject.EnsureTestLoadout().Configure(blade, GripMode.OneHand, rifle, GripMode.OneHand);
            Assert.That(input.SelectUnit(unit), Is.True); Assert.That(input.BeginAttack(), Is.True);
            Assert.That(input.TrySelectTarget(enemy), Is.True);
            var preview = input.CurrentAttackPreview;
            Assert.That(preview.WeaponName, Is.EqualTo(rifle.DisplayName));
            Assert.That(preview.MinDamage, Is.EqualTo(4)); Assert.That(preview.MaxDamage, Is.EqualTo(6));
            combat.SetRollSource(() => 0); combat.SetDamageRollSource((min, max) => max);
            Assert.That(input.TryAttackSelected().DamageDealt, Is.EqualTo(6));
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(File.ReadAllBytes(loadedScenePath), Is.EqualTo(savedSceneBytes));
        }
        [UnityTest]
        public IEnumerator GeneratedSquadRunsMoveAttackEnemyCycleAndVictory()
        {
            yield return LoadGeneratedSquad();
            var players = Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None).Where(u => u.Team == UnitTeam.Player).ToArray();
            var enemies = Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None).Where(u => u.Team == UnitTeam.Enemy).ToArray();
            Assert.That(players.Length, Is.EqualTo(2)); Assert.That(enemies.Length, Is.EqualTo(2));
            var ai = Object.FindObjectsByType<EnemyAIController>(FindObjectsSortMode.None);
            Assert.That(ai.Length, Is.EqualTo(1)); Assert.That(ai[0].IsReady, Is.True);
            Object.FindFirstObjectByType<BattleSceneController>().InitializeBattle();
            Assert.That(Object.FindObjectsByType<EnemyAIController>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            input.SelectUnit(unit); Assert.That(MoveSelected(new Vector2Int(2, 1)), Is.True);
            yield return FinishMove(); SelectAttackTarget(enemy); combat.SetRollSource(() => 0);
            Assert.That(input.TryAttackSelected().Hit, Is.True);
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(1));
            Assert.That(input.TryEndTurn(), Is.True); yield return FinishEnemyTurn();
            Assert.That(turns.TurnNumber, Is.EqualTo(2));
            Assert.That(players.All(p => p.ActionPoints.CurrentPoints == 5), Is.True);
            Assert.That(unit.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(10));
            Assert.That(enemy.ActionPoints.CurrentPoints, Is.EqualTo(2));
            var secondEnemy = enemies.Single(e => e != enemy);
            Assert.That(secondEnemy.ActionPoints.CurrentPoints, Is.Zero);
            Assert.That(secondEnemy.Mobility.MovedCells, Is.EqualTo(2));
            foreach (var e in enemies) e.Health.ApplyDamage(TargetBodyPart.Torso, 20);
            Assert.That(turns.IsBattleOver, Is.True); Assert.That(turns.IsPlayerVictory, Is.True);
            Assert.That(input.TryEndTurn(), Is.False);
        }
        [UnityTest]
        public IEnumerator GeneratedSquadDefeatRequiresAllPlayersAndLocksActions()
        {
            yield return LoadGeneratedSquad();
            var players = Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None).Where(u => u.Team == UnitTeam.Player).ToArray();
            players[0].Health.ApplyDamage(TargetBodyPart.Torso, 20);
            Assert.That(turns.IsBattleOver, Is.False);
            players[1].Health.ApplyDamage(TargetBodyPart.Torso, 20);
            Assert.That(turns.IsBattleOver, Is.True); Assert.That(turns.IsPlayerVictory, Is.False);
            Assert.That(input.TryEndTurn(), Is.False); Assert.That(turns.CanAct(enemy), Is.False);
        }
        private IEnumerator FinishEnemyTurn()
        {
            float deadline = Time.realtimeSinceStartup + 6;
            while (!turns.IsPlayerTurn && !turns.IsBattleOver && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(turns.IsPlayerTurn || turns.IsBattleOver, Is.True, "Enemy phase timed out.");
            yield return null;
        }
        private IEnumerator FinishMove()
        {
            float deadline = Time.realtimeSinceStartup + 6;
            while (unit.Movement.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(unit.Movement.IsMoving, Is.False);
            yield return null;
            if (unit.HasPendingFacing)
                Assert.That(unit.Movement.TryKeepArrivalFacing(), Is.True);
            yield return null;
        }
        private IEnumerator Click(Mouse mouse, Vector3 worldPosition)
        {
            Vector2 point = Camera.main.WorldToScreenPoint(worldPosition);
            Assert.That(Camera.main.pixelRect.Contains(point), Is.True);
            Assert.That(Object.FindFirstObjectByType<BattleDebugUI>().ContainsScreenPoint(point), Is.False,
                $"World target {worldPosition} is covered by UI at {point}; screen {Screen.width}x{Screen.height}.");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
        }
        [UnityTest]
        public IEnumerator SavedCameraQEInputUsesCurrentGridWithoutChangingBattleState()
        {
            var camera = Camera.main;
            Assert.That(camera.GetComponent<BattleCameraOrbit>(), Is.Not.Null);
            var center = grid.transform.position + new Vector3(grid.Width * grid.CellSize / 2f, 0, grid.Height * grid.CellSize / 2f);
            var initial = camera.transform.position - center;
            var initialZoom = camera.orthographicSize;
            var initialRotation = camera.transform.rotation;
            var initialGridPosition = unit.CurrentGridPosition;
            var initialAP = unit.ActionPoints.CurrentPoints;
            input.SelectUnit(unit);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(0.6f);
                var expected = center + Quaternion.AngleAxis(90, Vector3.up) * initial;
                Assert.That(Vector3.Distance(camera.transform.position, expected), Is.LessThan(0.01f));
                Assert.That(Quaternion.Angle(camera.transform.rotation, Quaternion.AngleAxis(90, Vector3.up) * initialRotation), Is.LessThan(0.05f));
                Assert.That(camera.orthographicSize, Is.EqualTo(initialZoom));
                Assert.That(input.SelectedUnit, Is.SameAs(unit));
                Assert.That(unit.CurrentGridPosition, Is.EqualTo(initialGridPosition));
                Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(initialAP));
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }
        [UnityTest]
        public IEnumerator SavedSceneMouseMovementAndDirectionalCover()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return Click(mouse, unit.transform.position);
                Assert.That(input.BeginMove(), Is.True);
                Assert.That(input.SelectedUnit, Is.SameAs(unit));
                yield return Capture("combat-selected.png");
                // (3,3) is now occupied by the dummy; use nearby free (2,3).
                var visibleDestination = new Vector3(4.25f, 0, 7.75f);
                var targetRay = Camera.main.ScreenPointToRay(Camera.main.WorldToScreenPoint(visibleDestination));
                Assert.That(Physics.Raycast(targetRay, out var targetHit), Is.True);
                Assert.That(targetHit.collider.name, Is.EqualTo("Ground"), "Destination click must hit visible ground.");
                yield return Click(mouse, visibleDestination);
                yield return FinishMove();
                Assert.That(unit.CurrentGridPosition, Is.EqualTo(new Vector2Int(2, 3)));
                Assert.That(unit.Mobility.MovedCells, Is.EqualTo(3));
                Assert.That(unit.MovementEvasion, Is.EqualTo(10));
                Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(2));
                grid.TryGetCell(new Vector2Int(3, 3), out var cell);
                Assert.That(cell.GetCover(CoverDirection.North), Is.EqualTo(CoverType.Full));
                Assert.That(cell.GetCover(CoverDirection.South), Is.EqualTo(CoverType.Half));
                Assert.That(cover.GetCoverMultiplier(cell, CoverDirection.North), Is.EqualTo(.5f));
                Assert.That(cover.GetCoverMultiplier(cell, CoverDirection.South), Is.EqualTo(.75f));
                Assert.That(MoveSelected(new Vector2Int(3, 4)), Is.False);
                Assert.That(MoveSelected(new Vector2Int(3, 3)), Is.False);
                yield return Capture("combat-moved.png");
                LogAssert.NoUnexpectedReceived();
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }
        [UnityTest]
        public IEnumerator SavedSceneRepeatedMovesUseEachNewDistance()
        {
            input.SelectUnit(unit);
            var destinations = new[] { new Vector2Int(1, 0), new Vector2Int(3, 0),
                Vector2Int.zero, new Vector2Int(4, 0), new Vector2Int(9, 0) };
            var expected = new[] { 5, 5, 10, 10, 15 };
            for (int i = 0; i < destinations.Length; i++)
            {
                if (i > 0) { Assert.That(input.TryEndTurn(), Is.True); yield return FinishEnemyTurn(); }
                Assert.That(MoveSelected(destinations[i]), Is.True);
                yield return FinishMove();
                Assert.That(unit.Mobility.MovedCells, Is.EqualTo(i + 1));
                Assert.That(unit.MovementEvasion, Is.EqualTo(expected[i]));
            }
        }
        [UnityTest]
        public IEnumerator SavedSceneEnemyClickShowsStrongestDiagonalCoverChance()
        {
            input.SelectUnit(unit);
            Assert.That(input.BeginAttack(), Is.True);
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return Click(mouse, enemy.transform.position);
                Assert.That(input.SelectedUnit, Is.SameAs(unit)); Assert.That(input.TargetUnit, Is.SameAs(enemy));
                Assert.That(input.CurrentAttackPreview.Direction, Is.EqualTo(CoverDirection.SouthWest));
                Assert.That(input.CurrentAttackPreview.CoverMultiplier, Is.EqualTo(.75f));
                Assert.That(input.CurrentAttackPreview.HitChance, Is.EqualTo(60));
                Assert.That(input.CurrentAttackPreview.Distance, Is.EqualTo(4));
                yield return Capture("combat-target.png");
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }
        [UnityTest]
        public IEnumerator MoveTwoThenAttack()
        {
            input.SelectUnit(unit);
            Assert.That(MoveSelected(new Vector2Int(1, 3)), Is.True);
            yield return FinishMove();
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(3));
            Assert.That(SelectAttackTarget(enemy), Is.True); combat.SetRollSource(() => 0);
            Assert.That(input.TryAttackSelected().Hit, Is.True);
            Assert.That(unit.ActionPoints.CurrentPoints, Is.Zero);
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(MoveSelected(new Vector2Int(1, 4)), Is.False);
        }
        [UnityTest]
        public IEnumerator AttackThenMoveTwo()
        {
            input.SelectUnit(unit); SelectAttackTarget(enemy); combat.SetRollSource(() => 99);
            Assert.That(input.TryAttackSelected().Hit, Is.False);
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(MoveSelected(new Vector2Int(1, 3)), Is.True);
            yield return FinishMove(); Assert.That(unit.ActionPoints.CurrentPoints, Is.Zero);
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }
        [UnityTest]
        public IEnumerator KillDummyEndsBattle()
        {
            input.SelectUnit(unit); combat.SetRollSource(() => 0);
            for (int i = 0; i < 4; i++)
            {
                SelectAttackTarget(enemy); Assert.That(input.TryAttackSelected().Hit, Is.True);
                if (i < 3) { Assert.That(input.TryEndTurn(), Is.True); yield return FinishEnemyTurn(); }
            }
            Assert.That(input.LastAttackResult.TargetDestroyed, Is.True);
            grid.TryGetCell(new Vector2Int(3, 3), out var cell); Assert.That(cell.Occupied, Is.False);
            Assert.That(input.TargetUnit, Is.Null); Assert.That(turns.IsBattleOver, Is.True);
            Assert.That(input.TryEndTurn(), Is.False);
            Assert.That(MoveSelected(new Vector2Int(2, 1)), Is.False);
            yield return null;
            Assert.That(enemy != null, Is.True);
            Assert.That(enemy.Health.State, Is.EqualTo(MechState.Destroyed));
            Assert.That(enemy.gameObject.activeSelf, Is.False);
        }
        [UnityTest]
        public IEnumerator NextTurnRestoresBudget()
        {
            input.SelectUnit(unit); MoveSelected(new Vector2Int(1, 3)); yield return FinishMove();
            Assert.That(unit.MovementEvasion, Is.EqualTo(5));
            Assert.That(input.TryEndTurn(), Is.True);
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(3));
            Assert.That(turns.ActiveTeam, Is.EqualTo(UnitTeam.Enemy));
            yield return FinishEnemyTurn();
            Assert.That(turns.TurnNumber, Is.EqualTo(2)); Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(unit.MovementEvasion, Is.Zero); Assert.That(unit.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(unit.CurrentGridPosition, Is.EqualTo(new Vector2Int(1, 3)));
        }
        [UnityTest]
        public IEnumerator PhysicalCoverClickDoesNotMoveOrResetEvasion()
        {
            yield return SolidClickIsIgnored("HalfCover (3,2)");
        }
        [UnityTest]
        public IEnumerator PhysicalObstacleClickDoesNotMoveOrResetEvasion()
        {
            yield return SolidClickIsIgnored("GridObstacle (4,2)");
        }
        private IEnumerator SolidClickIsIgnored(string objectName)
        {
            input.SelectUnit(unit);
            Assert.That(input.BeginMove(), Is.True);
            unit.Mobility.RecordCellMoved();
            unit.Mobility.RecordCellMoved();
            unit.Mobility.RecordCellMoved();
            var collider = GameObject.Find(objectName).GetComponent<BoxCollider>();
            var topCenter = collider.bounds.center + Vector3.up * collider.bounds.extents.y;
            var point = Camera.main.WorldToScreenPoint(topCenter);
            Assert.That(Physics.Raycast(Camera.main.ScreenPointToRay(point), out var hit), Is.True);
            Assert.That(hit.collider, Is.SameAs(collider));
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return Click(mouse, topCenter);
                Assert.That(unit.Movement.IsMoving, Is.False, "Clicking a solid must not move to a cell behind it.");
                Assert.That(unit.CurrentGridPosition, Is.EqualTo(new Vector2Int(1, 1)));
                Assert.That(unit.MovementEvasion, Is.EqualTo(10));
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }
        [UnityTest]
        public IEnumerator SecondCapsuleBlocksOccupiedCell()
        {
            var second = GameObject.CreatePrimitive(PrimitiveType.Capsule).AddTestBattleUnit();
            second.Configure(new Vector2Int(2, 1), 5);
            second.Initialize(grid);
            input.SelectUnit(unit);
            Assert.That(MoveSelected(new Vector2Int(2, 1)), Is.False);
            Assert.That(Pathfinder.FindPath(grid, unit, new Vector2Int(3, 1)).Count, Is.EqualTo(4));
            Object.Destroy(second.gameObject);
            yield return null;
            Assert.That(Pathfinder.FindPath(grid, unit, new Vector2Int(3, 1)).Count, Is.EqualTo(2));
        }
        private IEnumerator Capture(string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
            Directory.CreateDirectory("Artifacts");
            string path = Path.GetFullPath("Artifacts/" + name);
            // Batch mode has no OS Game View to capture. Render the real camera and live scene state.
            var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            var previousTarget = RenderTexture.active;
            var previousRect = Camera.main.rect;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                Camera.main.rect = new Rect(0, 0, 1, 1);
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,
                    new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Assert.That(new FileInfo(path).Length, Is.GreaterThan(1000));
            }
            finally
            {
                Camera.main.rect = previousRect;
                RenderTexture.active = previousTarget;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(image);
            }
            yield return null;
        }
    }
}
#endif
