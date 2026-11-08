using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class BackpackPlayTests
    {
        private BackpackTestBattle b;
        private PlayerInputController input;
        private UnitActionUI menu;
        private BodyTargetUI body;
        private Camera camera;
        private Mouse mouse;
        private Keyboard keyboard;
        private bool configured;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private InputSettings.BackgroundBehavior oldBackground;

        private void AttachInput()
        {
            configured = true;
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            b.Enemy.gameObject.AddComponent<CapsuleCollider>();
            var visual = b.Root.AddComponent<GridVisualizer>(); visual.Initialize(b.Grid, b.Turns);
            var debug = b.Root.AddComponent<BattleDebugUI>(); debug.Initialize(b.Grid, b.Cover);
            camera = new GameObject("Backpack UI test camera").AddComponent<Camera>(); camera.transform.SetParent(b.Root.transform);
            camera.transform.position = new Vector3(5, 20, -10); camera.transform.LookAt(new Vector3(5, 0, 5));
            input = b.Root.AddComponent<PlayerInputController>(); input.Initialize(camera, b.Grid, visual, debug);
            input.InitializeCombat(b.Combat, b.Turns); debug.InitializeCombat(input, b.Turns);
            menu = b.Root.GetComponent<UnitActionUI>(); body = b.Root.GetComponent<BodyTargetUI>();
            b.Root.AddComponent<OverwatchDecisionController>().Initialize(b.Overwatch, input, body);
            mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            Physics.SyncTransforms(); Assert.That(input.SelectUnit(b.Player), Is.True);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (configured)
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
                InputSystem.settings.backgroundBehavior = oldBackground;
            }
            b?.Dispose(); yield return null;
        }
        private IEnumerator Click(Rect rect)
        {
            Assert.That(rect.width, Is.GreaterThan(0));
            var point = new Vector2(rect.center.x, Screen.height - rect.center.y);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
        }
        private IEnumerator OpenAttack()
        { yield return Click(menu.AttackButtonRect); Assert.That(input.IsWeaponMenuOpen, Is.True); }
        private void AssertChoicesFit()
        {
            Assert.That(menu.TryGetAttackMenuRect(out var panel), Is.True);
            Assert.That(panel.xMin, Is.GreaterThanOrEqualTo(0)); Assert.That(panel.yMin, Is.GreaterThanOrEqualTo(0));
            Assert.That(panel.xMax, Is.LessThanOrEqualTo(Screen.width)); Assert.That(panel.yMax, Is.LessThanOrEqualTo(Screen.height));
            foreach (var slot in input.GetAvailableWeaponSlots())
            {
                var button = menu.GetWeaponButtonRect(slot);
                Assert.That(button.width, Is.GreaterThan(0));
                Assert.That(panel.Contains(button.min), Is.True); Assert.That(panel.Contains(button.max), Is.True);
            }
        }
        [UnityTest] public IEnumerator ThreeWeaponButtonsFitAndBackpackClickFiresChosenPart()
        {
            b = new BackpackTestBattle(capacity: 2, fireAP: 1); AttachInput(); yield return null;
            yield return OpenAttack();
            CollectionAssert.AreEqual(new[] { WeaponSlot.RightHand, WeaponSlot.LeftHand, WeaponSlot.Backpack }, input.GetAvailableWeaponSlots());
            AssertChoicesFit();
            Assert.That(menu.GetWeaponButtonText(WeaponSlot.Backpack), Does.Contain("Backpack").And.Contain(b.Weapon.DisplayName).And.Contain("재장전 불가"));
            yield return Click(menu.GetWeaponButtonRect(WeaponSlot.Backpack));
            Assert.That(input.SelectedWeaponSlot, Is.EqualTo(WeaponSlot.Backpack)); Assert.That(input.TrySelectTarget(b.Enemy), Is.True);
            Assert.That(body.AmmoInfoText, Does.Contain("2/2").And.Contain("재장전 불가"));
            yield return Click(body.GetPartRect(TargetBodyPart.LeftLeg));
            Assert.That(input.SelectedBodyPart, Is.EqualTo(TargetBodyPart.LeftLeg));
            yield return Click(body.FireButtonRect);
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.LeftLeg), Is.EqualTo(3));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.Backpack).Loaded, Is.EqualTo(1));
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(2));
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
        }
        [UnityTest] public IEnumerator BackpackOnlyUnitHasOneClickableAttackChoice()
        {
            b = new BackpackTestBattle(capacity: 2, fireAP: 1);
            b.Player.GetComponent<UnitWeaponLoadout>().Configure(null, GripMode.OneHand, null, GripMode.OneHand);
            AttachInput(); yield return null; yield return OpenAttack();
            CollectionAssert.AreEqual(new[] { WeaponSlot.Backpack }, input.GetAvailableWeaponSlots());
            yield return Click(menu.GetWeaponButtonRect(WeaponSlot.Backpack)); Assert.That(input.TrySelectTarget(b.Enemy), Is.True);
            yield return Click(body.FireButtonRect);
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(13));
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
        }
        [UnityTest] public IEnumerator UnarmedBackpackHasNoMenuEntry() => NoBackpackEntry(true);
        [UnityTest] public IEnumerator UnequippedBackpackHasNoMenuEntry() => NoBackpackEntry(false);
        private IEnumerator NoBackpackEntry(bool unarmed)
        {
            b = new BackpackTestBattle(withBackpack: unarmed, armed: !unarmed); AttachInput(); yield return null;
            yield return OpenAttack(); Assert.That(input.GetAvailableWeaponSlots().Count, Is.EqualTo(2));
            Assert.That(menu.GetWeaponButtonRect(WeaponSlot.Backpack).width, Is.Zero);
            Assert.That(menu.GetWeaponButtonText(WeaponSlot.Backpack), Is.Empty);
        }
        [UnityTest] public IEnumerator EmptyBackpackStaysNamedButCannotBeSelectedOrReloaded()
        {
            b = new BackpackTestBattle(capacity: 1, fireAP: 1);
            Assert.That(b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.Backpack).WasExecuted, Is.True);
            AttachInput(); yield return null; yield return OpenAttack();
            Assert.That(input.GetAvailableWeaponSlots(), Does.Contain(WeaponSlot.Backpack));
            Assert.That(menu.GetWeaponButtonText(WeaponSlot.Backpack), Does.Contain("0/1").And.Contain("재장전 불가"));
            Assert.That(input.CanBeginWeaponAttack(WeaponSlot.Backpack), Is.False);
            yield return Click(menu.GetWeaponButtonRect(WeaponSlot.Backpack));
            Assert.That(input.SelectedWeaponSlot, Is.Null); Assert.That(input.IsWeaponMenuOpen, Is.True);
            Assert.That(input.GetReloadWeaponSlots(), Has.No.Member(WeaponSlot.Backpack));
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
        }
        [UnityTest] public IEnumerator ReloadMenuExcludesBackpackAndHandReloadStillWorks()
        {
            b = new BackpackTestBattle(capacity: 2, fireAP: 1);
            b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.RightHand);
            AttachInput(); yield return null;
            CollectionAssert.AreEqual(new[] { WeaponSlot.RightHand, WeaponSlot.LeftHand }, input.GetReloadWeaponSlots());
            yield return Click(menu.ReloadButtonRect);
            Assert.That(input.IsReloadWeaponMenuOpen, Is.True); Assert.That(menu.GetWeaponButtonRect(WeaponSlot.Backpack).width, Is.Zero);
            yield return Click(menu.GetWeaponButtonRect(WeaponSlot.RightHand));
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(2));
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).SpareMagazines, Is.EqualTo(1));
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.Backpack).Loaded, Is.EqualTo(2));
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator ThirdChoiceRemainsClickableNearScreenCorner()
        {
            b = new BackpackTestBattle(capacity: 2, fireAP: 1); AttachInput();
            float depth = camera.WorldToViewportPoint(b.Player.transform.position).z;
            camera.transform.position += b.Player.transform.position - camera.ViewportToWorldPoint(new Vector3(.95f, .05f, depth));
            yield return null; yield return OpenAttack(); AssertChoicesFit();
            yield return Click(menu.GetWeaponButtonRect(WeaponSlot.Backpack));
            Assert.That(input.SelectedWeaponSlot, Is.EqualTo(WeaponSlot.Backpack));
        }
        [UnityTest] public IEnumerator EscapeClearsBackpackSelectionWithoutSpendingResources()
        {
            b = new BackpackTestBattle(capacity: 2, fireAP: 1); AttachInput(); yield return null;
            yield return OpenAttack(); yield return Click(menu.GetWeaponButtonRect(WeaponSlot.Backpack));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(input.SelectedUnit, Is.Null); Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.Backpack).Loaded, Is.EqualTo(2));
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator CriticalUnitCannotFireBackpackButCanEject()
        {
            b = new BackpackTestBattle(capacity: 2, fireAP: 1); AttachInput(); yield return null;
            b.Player.Health.ConfigureDestructionChance(0);
            foreach (var part in new[] { TargetBodyPart.LeftArm, TargetBodyPart.RightArm, TargetBodyPart.LeftLeg })
                b.Player.Health.ApplyDamage(part, b.Player.Health.GetCurrentHP(part));
            Assert.That(input.CanBeginWeaponAttack(WeaponSlot.Backpack), Is.False);
            Assert.That(input.BeginWeaponAttack(WeaponSlot.Backpack), Is.False);
            Assert.That(input.CanEjectSelected, Is.True); yield return Click(menu.EjectButtonRect);
            Assert.That(b.Player.Health.State, Is.EqualTo(MechState.Ejected));
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.Backpack).Loaded, Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator EnemyUsesBackpackThenWaitsWhenEmptyThenUsesHand()
        {
            b = new BackpackTestBattle(capacity: 1, fireAP: 1, enemyHasBackpack: true);
            var loadout = b.Enemy.GetComponent<UnitWeaponLoadout>(); loadout.Configure(null, GripMode.OneHand, null, GripMode.OneHand);
            var ai = b.Root.AddComponent<EnemyAIController>(); ai.Initialize(b.Grid, b.Combat, b.Turns); b.Turns.ConfigureEnemyAI(ai);
            typeof(TurnManager).GetField("enemyActionDelay", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(b.Turns, 0f);
            yield return EnemyPhase(); Assert.That(b.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(13));
            Assert.That(b.Enemy.Ammo.GetStatus(WeaponSlot.Backpack).Loaded, Is.Zero);
            Assert.That(b.Enemy.ActionPoints.CurrentPoints, Is.EqualTo(4));
            yield return EnemyPhase(); Assert.That(b.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(13));
            Assert.That(b.Enemy.ActionPoints.CurrentPoints, Is.EqualTo(5)); Assert.That(b.Turns.IsAnyUnitBusy, Is.False);
            loadout.Configure(null, GripMode.OneHand, b.Weapon, GripMode.OneHand);
            yield return EnemyPhase(); Assert.That(b.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(6));
            Assert.That(b.Enemy.Ammo.GetStatus(WeaponSlot.Backpack).Loaded, Is.Zero);
        }
        private IEnumerator EnemyPhase()
        {
            Assert.That(b.Turns.TryEndTurn(), Is.True);
            float end = Time.realtimeSinceStartup + 4;
            while (!b.Turns.IsPlayerTurn && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(b.Turns.IsPlayerTurn, Is.True);
        }
        [UnityTest] public IEnumerator NativeBackpackOverwatchFireUsesSelectedPartAndReleasesMovement()
        {
            b = new BackpackTestBattle(capacity: 1, fireAP: 1); AttachInput(); yield return null;
            yield return ArmBackpackThroughMenu(); yield return BeginReaction();
            Assert.That(body.AmmoInfoText, Does.Contain("1/1").And.Contain("재장전 불가"));
            yield return Click(body.GetPartRect(TargetBodyPart.LeftLeg)); yield return Click(body.FireButtonRect);
            yield return FinishMovement();
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.LeftLeg), Is.EqualTo(3));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.Backpack).Loaded, Is.Zero); Assert.That(b.Player.Overwatch.IsActive, Is.False);
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(3)); Assert.That(input.LastAttackResult.WasExecuted, Is.True);
        }
        [UnityTest] public IEnumerator NativeBackpackOverwatchSkipPreservesRound()
        {
            b = new BackpackTestBattle(capacity: 1, fireAP: 1); AttachInput(); yield return null;
            yield return ArmBackpackThroughMenu(); yield return BeginReaction();
            yield return Click(body.SkipButtonRect); yield return FinishMovement();
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.Backpack).Loaded, Is.EqualTo(1));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20)); Assert.That(b.Player.Overwatch.IsActive, Is.True);
        }
        private IEnumerator ArmBackpackThroughMenu()
        {
            yield return Click(menu.OverwatchButtonRect); Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.OverwatchWeapon));
            Assert.That(input.GetOverwatchWeaponSlots(), Does.Contain(WeaponSlot.Backpack));
            yield return Click(menu.GetWeaponButtonRect(WeaponSlot.Backpack));
            Assert.That(input.SelectedOverwatchSlot, Is.EqualTo(WeaponSlot.Backpack));
            yield return Click(menu.GetFacingButtonRect(FacingDirection.East)); yield return Click(menu.ConfirmFacingButtonRect);
            Assert.That(b.Player.Overwatch.IsActive, Is.True); Assert.That(b.Player.Overwatch.Slot, Is.EqualTo(WeaponSlot.Backpack));
        }
        private IEnumerator BeginReaction()
        {
            b.StartEnemyTeam(); Assert.That(b.Enemy.Movement.TryMove(new Vector2Int(2, 1)), Is.True);
            float end = Time.realtimeSinceStartup + 3;
            while (!b.Overwatch.PendingDecision.HasValue && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(b.Overwatch.PendingDecision.HasValue, Is.True); yield return null;
            Assert.That(body.IsVisible, Is.True);
        }
        private IEnumerator FinishMovement()
        {
            float end = Time.realtimeSinceStartup + 3;
            while (b.Enemy.Movement.IsMoving && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(b.Enemy.Movement.IsMoving || b.Overwatch.IsResolving, Is.False);
        }
    }
}
