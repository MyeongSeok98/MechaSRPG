using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class IndirectAttackPlayTests
    {
        private BackpackTestBattle b;
        private PlayerInputController input;
        private GridVisualizer visual;
        private Camera camera;
        private Mouse mouse;
        private Keyboard keyboard;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private InputSettings.BackgroundBehavior oldBackground;
        private bool changedInput;
        private void Create(bool ui = false)
        {
            b = new BackpackTestBattle(); IndirectTestSupport.Configure(b);
            b.Combat.SetIndirectPartCountRollSource((min, max) => min); b.Combat.SetIndirectPartIndexRollSource(count => 0);
            if (!ui) return;
            changedInput = true; oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode; oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            visual = b.Root.AddComponent<GridVisualizer>(); visual.Initialize(b.Grid, b.Turns);
            var debug = b.Root.AddComponent<BattleDebugUI>(); debug.Initialize(b.Grid, b.Cover);
            camera = new GameObject("Indirect UI camera").AddComponent<Camera>(); camera.transform.SetParent(b.Root.transform);
            camera.orthographic = true; camera.orthographicSize = 16; camera.transform.position = new Vector3(12, 26, -18); camera.transform.LookAt(new Vector3(12, 0, 12));
            input = b.Root.AddComponent<PlayerInputController>(); input.Initialize(camera, b.Grid, visual, debug); input.InitializeCombat(b.Combat, b.Turns); debug.InitializeCombat(input, b.Turns);
            mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            Assert.That(input.SelectUnit(b.Player), Is.True); Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand), Is.True);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (mouse != null) InputSystem.RemoveDevice(mouse); if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (changedInput) { InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor; InputSystem.settings.backgroundBehavior = oldBackground; }
            b?.Dispose(); yield return null;
        }
        private Component AreaUI()
        {
            var type = typeof(PlayerInputController).Assembly.GetType("MechaXCOM.IndirectTargetUI"); Assert.That(type, Is.Not.Null);
            var component = b.Root.GetComponent(type); Assert.That(component, Is.Not.Null); return component;
        }
        private bool Select(Vector2Int center) => (bool)IndirectTestSupport.Call(input, "TrySelectImpactCell", center);
        private IEnumerator Click(Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
        }
        private IEnumerator ClickButton(Rect rect) => Click(new Vector2(rect.center.x, Screen.height - rect.center.y));
        [UnityTest] public IEnumerator MapClickSelectsAreaAndEscapeClearsAllIndicators()
        {
            Create(true); ExplosiveTestSupport.Set(b.Weapon, "blastRadius", 3); yield return null;
            // Use a map point in the wheel's open center, rather than underneath a command label.
            var cell = new Vector2Int(3, 3); var point = camera.WorldToScreenPoint(b.Grid.GetWorldPosition(cell));
            Assert.That(b.Root.GetComponent<UnitActionUI>().ContainsScreenPoint(point), Is.False);
            yield return Click(point);
            Assert.That(ExplosiveTestSupport.Property<Vector2Int?>(input, "SelectedImpactCell"), Is.EqualTo(cell));
            Assert.That(ExplosiveTestSupport.Property<bool>(AreaUI(), "IsVisible"), Is.True);
            Assert.That(b.Root.GetComponent<BodyTargetUI>().IsVisible, Is.False);
            var overlay = visual.transform.Find("Blast Preview Cells"); Assert.That(overlay, Is.Not.Null); Assert.That(overlay.gameObject.activeSelf, Is.True);
            Assert.That(overlay.childCount, Is.EqualTo(25), "Radius3 must render the diamond, not the29-cell circle.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(input.SelectedUnit, Is.Null); Assert.That(ExplosiveTestSupport.Property<Vector2Int?>(input, "SelectedImpactCell"), Is.Null);
            Assert.That(overlay.gameObject.activeSelf, Is.False); Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator FireButtonUsesCoordinatesAndShowsOnlyKnownResults()
        {
            Create(true); b.Enemy.name = "SECRET_TARGET"; b.Player.Vision.Configure(0);
            Assert.That(Select(new Vector2Int(3, 1)), Is.True); yield return null;
            var ui = AreaUI(); Assert.That(ExplosiveTestSupport.Property<string>(ui, "KnownTargetsText"), Does.Not.Contain("SECRET_TARGET"));
            var panel = ExplosiveTestSupport.Property<Rect>(ui, "PanelRect"); var fire = ExplosiveTestSupport.Property<Rect>(ui, "FireButtonRect");
            Assert.That(panel.Contains(fire.center), Is.True); Assert.That(panel.yMin, Is.GreaterThanOrEqualTo(0)); Assert.That(panel.yMax, Is.LessThanOrEqualTo(Screen.height));
            yield return ClickButton(fire);
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(900));
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(2)); Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(5));
            Assert.That(input.InputMessage, Does.Not.Contain("SECRET_TARGET"));
            var result = ExplosiveTestSupport.Property<IndirectAttackResult>(input, "LastIndirectAttackResult");
            Assert.That(result.WasExecuted, Is.True); Assert.That(result.Targets, Is.Empty);
        }
        [UnityTest] public IEnumerator FriendlyWarningAndWeaponSwitchClearPendingAim()
        {
            Create(true); ExplosiveTestSupport.Set(b.Weapon, "blastRadius", 2);
            Assert.That(Select(new Vector2Int(2, 1)), Is.True); yield return null;
            StringAssert.Contains("아군", ExplosiveTestSupport.Property<string>(AreaUI(), "FriendlyWarningText"));
            b.Player.Combat.ConfigureTestMelee(8, 95, 2, 3);
            Assert.That(input.BeginWeaponAttack(WeaponSlot.LeftHand), Is.True);
            Assert.That(ExplosiveTestSupport.Property<Vector2Int?>(input, "SelectedImpactCell"), Is.Null);
            Assert.That(ExplosiveTestSupport.Property<bool>(AreaUI(), "IsVisible"), Is.False);
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator ChangedResourcesAreRevalidatedAtFireAndIndirectIsNotGuardChoice()
        {
            Create(true); Assert.That(input.GetOverwatchWeaponSlots(), Is.Empty);
            Assert.That(Select(new Vector2Int(3, 1)), Is.True); b.Player.ActionPoints.TrySpend(5);
            var result = IndirectTestSupport.Call(input, "TryFireIndirectSelected");
            Assert.That(IndirectTestSupport.Field<bool>(result, "WasExecuted"), Is.False);
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(6)); yield return null;
        }
        [UnityTest] public IEnumerator EnemyAICanFireIndirectAndAvoidsFriendlyDamage()
        {
            Create(); var ai = b.Root.AddComponent<EnemyAIController>(); ai.Initialize(b.Grid, b.Combat, b.Turns); b.Turns.ConfigureEnemyAI(ai);
            Assert.That(b.Turns.TryEndTurn(), Is.True); yield return WaitForPlayer();
            Assert.That(b.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(900));
            Assert.That(b.Enemy.ActionPoints.CurrentPoints, Is.EqualTo(2)); Assert.That(b.Enemy.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator EnemyAIDoesNotFireWhenItsOwnBlastWouldHitItself()
        {
            Create(); ExplosiveTestSupport.Set(b.Weapon, "blastRadius", 3);
            var ai = b.Root.AddComponent<EnemyAIController>(); ai.Initialize(b.Grid, b.Combat, b.Turns); b.Turns.ConfigureEnemyAI(ai);
            Assert.That(b.Turns.TryEndTurn(), Is.True); yield return WaitForPlayer();
            Assert.That(b.Enemy.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(6));
            Assert.That(b.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(1000));
        }
        private void LaunchWall()
        {
            var obj = new GameObject("Indirect launch wall"); obj.transform.SetParent(b.Root.transform);
            obj.transform.position = b.Grid.GetWorldPosition(new Vector2Int(2, 1));
            var wall = obj.AddComponent<GridObstacle>(); wall.Collider.size = Vector3.one;
            b.Grid.RegisterObstacle(wall.Collider);
        }
        [UnityTest] public IEnumerator BuildingRejectsCoordinateSelectionUntilWeaponHasTrait()
        {
            Create(true); LaunchWall(); yield return null;
            var center = new Vector2Int(3, 1);
            Assert.That(Select(center), Is.False); Assert.That(input.SelectedImpactCell, Is.Null);
            Assert.That(input.InputMessage, Does.Contain("건물"));
            JsonUtility.FromJsonOverwrite("{\"traits\":[0]}", b.Weapon);
            Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand), Is.True);
            Assert.That(Select(center), Is.True); yield return null;
            Assert.That(input.CurrentIndirectPreview.CanAttack, Is.True);
            yield return ClickButton(ExplosiveTestSupport.Property<Rect>(AreaUI(), "FireButtonRect"));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(900));
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator NewBuildingAfterAimMakesFireFailWithoutSpendingResources()
        {
            Create(true); Assert.That(Select(new Vector2Int(3, 1)), Is.True); yield return null;
            LaunchWall();
            Assert.That(input.CurrentIndirectPreview.CanAttack, Is.False);
            Assert.That(input.TryFireIndirectSelected().WasExecuted, Is.False);
            Assert.That(input.InputMessage, Does.Contain("건물"));
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(6));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(1000));
        }
        [UnityTest] public IEnumerator EnemyAIHonorsBuildingRestrictionAndWeaponTrait()
        {
            Create(); LaunchWall();
            // A separate observer keeps the target team-visible behind the shooter's wall.
            var observer = IndirectTestSupport.AddUnit(b, new Vector2Int(1, 3), UnitTeam.Enemy);
            b.Turns.Visibility.Initialize(b.Grid, new[] { b.Player, b.Enemy, observer });
            Assert.That(b.Turns.Visibility.IsVisibleTo(UnitTeam.Enemy, b.Player), Is.True);
            b.Enemy.Health.ApplyDamage(TargetBodyPart.LeftLeg, 1000);
            b.Enemy.Health.ApplyDamage(TargetBodyPart.RightLeg, 1000);
            var ai = b.Root.AddComponent<EnemyAIController>(); ai.Initialize(b.Grid, b.Combat, b.Turns); b.Turns.ConfigureEnemyAI(ai);
            Assert.That(b.Turns.TryEndTurn(), Is.True); yield return WaitForPlayer();
            Assert.That(b.Enemy.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(6));
            Assert.That(b.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(1000));
            JsonUtility.FromJsonOverwrite("{\"traits\":[0]}", b.Weapon);
            Assert.That(b.Turns.TryEndTurn(), Is.True); yield return WaitForPlayer();
            Assert.That(b.Enemy.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(5));
            Assert.That(b.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(900));
        }
        [UnityTest] public IEnumerator EnemyAIMovesIntoIndirectRangeAcrossBuildingOnlyWithTrait()
        {
            Create(); b.Grid.Configure(5, 5, 2); b.Grid.Generate();
            Assert.That(b.Grid.TryPlaceUnit(b.Enemy, new Vector2Int(0, 1)), Is.True);
            Assert.That(b.Grid.TryPlaceUnit(b.Player, new Vector2Int(4, 1)), Is.True);
            foreach (var unit in new[] { b.Player, b.Enemy })
                unit.transform.position = b.Grid.GetWorldPosition(unit.CurrentGridPosition) + Vector3.up * unit.HeightOffset;
            ExplosiveTestSupport.Set(b.Weapon, "range", 3);
            for (int z = 0; z < 5; z++)
            {
                var obj = new GameObject("Building column"); obj.transform.SetParent(b.Root.transform);
                obj.transform.position = b.Grid.GetWorldPosition(new Vector2Int(3, z));
                var wall = obj.AddComponent<GridObstacle>(); wall.Collider.size = Vector3.one; b.Grid.RegisterObstacle(wall.Collider);
            }
            var observer = IndirectTestSupport.AddUnit(b, new Vector2Int(4, 4), UnitTeam.Enemy);
            b.Turns.Visibility.Initialize(b.Grid, new[] { b.Player, b.Enemy, observer });
            Assert.That(b.Turns.Visibility.IsVisibleTo(UnitTeam.Enemy, b.Player), Is.True);
            var ai = b.Root.AddComponent<EnemyAIController>(); ai.Initialize(b.Grid, b.Combat, b.Turns); b.Turns.ConfigureEnemyAI(ai);
            Assert.That(b.Turns.TryEndTurn(), Is.True); yield return WaitForPlayer();
            Assert.That(b.Enemy.CurrentGridPosition, Is.EqualTo(new Vector2Int(0, 1)));
            Assert.That(b.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(1000));
            JsonUtility.FromJsonOverwrite("{\"traits\":[0]}", b.Weapon);
            Assert.That(b.Turns.TryEndTurn(), Is.True); yield return WaitForPlayer();
            Assert.That(b.Enemy.CurrentGridPosition, Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(b.Enemy.ActionPoints.CurrentPoints, Is.EqualTo(1));
            Assert.That(b.Enemy.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(5));
            Assert.That(b.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(900));
        }
        private IEnumerator WaitForPlayer()
        {
            float deadline = Time.realtimeSinceStartup + 6;
            while (!b.Turns.IsPlayerTurn && !b.Turns.IsBattleOver && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(b.Turns.IsPlayerTurn, Is.True);
        }
    }
}
