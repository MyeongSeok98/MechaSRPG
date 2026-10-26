using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class WeaponAmmoPlayTests
    {
        private MechaAccuracyTestBattle battle;
        private OverwatchTestBattle reaction;
        private PlayerInputController input;
        private UnitActionUI menu;
        private Mouse mouse;
        private Keyboard keyboard;
        private bool inputConfigured;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private InputSettings.BackgroundBehavior oldBackground;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (inputConfigured)
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
                InputSystem.settings.backgroundBehavior = oldBackground;
            }
            battle?.Dispose(); reaction?.Dispose(); yield return null;
        }
        private void AttachInput()
        {
            inputConfigured = true;
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            battle.Enemy.gameObject.AddComponent<CapsuleCollider>();
            var visual = battle.Root.AddComponent<GridVisualizer>(); visual.Initialize(battle.Grid, battle.Turns);
            var debug = battle.Root.AddComponent<BattleDebugUI>(); debug.Initialize(battle.Grid, battle.Root.GetComponent<CoverSystem>());
            var camera = new GameObject("Ammo UI camera").AddComponent<Camera>(); camera.transform.SetParent(battle.Root.transform);
            camera.transform.position = new Vector3(5, 20, -10); camera.transform.LookAt(new Vector3(5, 0, 5));
            input = battle.Root.AddComponent<PlayerInputController>(); input.Initialize(camera, battle.Grid, visual, debug);
            input.InitializeCombat(battle.Combat, battle.Turns); debug.InitializeCombat(input, battle.Turns);
            menu = battle.Root.GetComponent<UnitActionUI>();
            mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            Physics.SyncTransforms();
        }
        private IEnumerator Click(Rect rect)
        {
            Assert.That(rect.width, Is.GreaterThan(0));
            var point = new Vector2(rect.center.x, Screen.height - rect.center.y);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
        }
        [UnityTest] public IEnumerator ReloadButtonAndWeaponClickSpendAPAndReplacePartialPack()
        {
            battle = new MechaAccuracyTestBattle(); AmmoTestWeapons.Equip(battle.Player, capacity: 3);
            Assert.That(battle.Combat.TryAttackWithWeapon(battle.Player, battle.Enemy, WeaponSlot.RightHand).WasExecuted, Is.True);
            AttachInput(); yield return null; Assert.That(input.SelectUnit(battle.Player), Is.True);
            yield return Click(menu.ReloadButtonRect);
            Assert.That(input.IsReloadWeaponMenuOpen, Is.True);
            Assert.That(menu.GetWeaponButtonText(WeaponSlot.RightHand), Does.Contain("2/3").And.Contain("Reload AP 2"));
            yield return Click(menu.GetWeaponButtonRect(WeaponSlot.RightHand));
            Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Menu));
            var status = battle.Player.Ammo.GetStatus(WeaponSlot.RightHand);
            Assert.That(status.Loaded, Is.EqualTo(3)); Assert.That(status.SpareMagazines, Is.EqualTo(1));
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator EscapeCancelsReloadSelectionWithoutChangingResources()
        {
            battle = new MechaAccuracyTestBattle(); AmmoTestWeapons.Equip(battle.Player);
            battle.Combat.TryAttackWithWeapon(battle.Player, battle.Enemy, WeaponSlot.RightHand);
            AttachInput(); yield return null; input.SelectUnit(battle.Player); Assert.That(input.OpenReloadMenu(), Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(input.SelectedUnit, Is.Null);
            Assert.That(input.IsReloadWeaponMenuOpen, Is.False);
            Assert.That(battle.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(1));
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
        }
        [UnityTest] public IEnumerator BodyPanelShowsAmmoAndEmptyWeaponRequiresReload()
        {
            battle = new MechaAccuracyTestBattle(); AmmoTestWeapons.Equip(battle.Player, capacity: 1);
            AttachInput(); yield return null; input.SelectUnit(battle.Player);
            Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand), Is.True);
            Assert.That(input.TrySelectTarget(battle.Enemy), Is.True);
            var body = battle.Root.GetComponent<BodyTargetUI>();
            Assert.That(body.AmmoInfoText, Does.Contain("에너지 1/1").And.Contain("예비 팩 2개"));
            Assert.That(input.TryAttackSelected().WasExecuted, Is.True);
            Assert.That(input.CanBeginWeaponAttack(WeaponSlot.RightHand), Is.False);
            Assert.That(input.CanOpenReloadMenu, Is.True);
            Assert.That(input.OpenReloadMenu(), Is.True);
            Assert.That(input.TryReloadSelected(WeaponSlot.RightHand), Is.True);
            Assert.That(input.CanBeginWeaponAttack(WeaponSlot.RightHand), Is.True);
        }
        [UnityTest] public IEnumerator TwoHandedWeaponHasOneReloadChoice()
        {
            battle = new MechaAccuracyTestBattle(); var weapon = AmmoTestWeapons.Equip(battle.Player);
            JsonUtility.FromJsonOverwrite("{\"gripRequirement\":1}", weapon);
            battle.Player.GetComponent<UnitWeaponLoadout>().Configure(null, GripMode.OneHand, weapon, GripMode.TwoHands);
            battle.Combat.TryAttackWithWeapon(battle.Player, battle.Enemy, WeaponSlot.RightHand);
            AttachInput(); yield return null; input.SelectUnit(battle.Player);
            Assert.That(input.OpenReloadMenu(), Is.True);
            Assert.That(input.GetReloadWeaponSlots().Count, Is.EqualTo(1));
            Assert.That(menu.GetWeaponButtonText(WeaponSlot.RightHand), Does.Contain("Both hands").And.Contain("1/2"));
            Assert.That(input.TryReloadSelected(WeaponSlot.RightHand), Is.True);
            Assert.That(battle.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator ReloadCannotOpenWhenAPIsInsufficient()
        {
            battle = new MechaAccuracyTestBattle(); AmmoTestWeapons.Equip(battle.Player, reload: 5);
            battle.Combat.TryAttackWithWeapon(battle.Player, battle.Enemy, WeaponSlot.RightHand);
            AttachInput(); yield return null; input.SelectUnit(battle.Player);
            Assert.That(input.CanOpenReloadMenu, Is.False); yield return Click(menu.ReloadButtonRect);
            Assert.That(input.IsReloadWeaponMenuOpen, Is.False);
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(battle.Player.Ammo.GetStatus(WeaponSlot.RightHand).SpareMagazines, Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator EnemyReloadsThenFiresWhenRemainingAPAllowsAndEventuallyRunsDry()
        {
            battle = new MechaAccuracyTestBattle(withAI: true);
            AmmoTestWeapons.Equip(battle.Enemy, capacity: 1, spares: 1, reload: 2, fire: 3);
            yield return EnemyPhase();
            Assert.That(battle.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(19));
            yield return EnemyPhase();
            Assert.That(battle.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(18));
            Assert.That(battle.Enemy.ActionPoints.CurrentPoints, Is.Zero);
            Assert.That(battle.Enemy.Ammo.GetStatus(WeaponSlot.RightHand).SpareMagazines, Is.Zero);
            yield return EnemyPhase();
            Assert.That(battle.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(18));
            Assert.That(battle.Enemy.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator EnemyWaitsToFireIfReloadLeavesTooLittleAP()
        {
            battle = new MechaAccuracyTestBattle(withAI: true);
            AmmoTestWeapons.Equip(battle.Enemy, capacity: 1, spares: 1, reload: 3, fire: 3);
            yield return EnemyPhase(); yield return EnemyPhase();
            Assert.That(battle.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(19));
            Assert.That(battle.Enemy.ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(battle.Enemy.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(1));
            yield return EnemyPhase();
            Assert.That(battle.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(18));
        }
        [UnityTest] public IEnumerator UndetectedEnemyDoesNotReloadOrSpendAP()
        {
            battle = new MechaAccuracyTestBattle(withAI: true);
            AmmoTestWeapons.Equip(battle.Enemy, capacity: 1);
            yield return EnemyPhase(); battle.Enemy.Vision.Configure(0);
            yield return EnemyPhase();
            Assert.That(battle.Enemy.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.Zero);
            Assert.That(battle.Enemy.Ammo.GetStatus(WeaponSlot.RightHand).SpareMagazines, Is.EqualTo(2));
            Assert.That(battle.Enemy.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        private IEnumerator EnemyPhase()
        {
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            float end = Time.realtimeSinceStartup + 4;
            while (!battle.Turns.IsPlayerTurn && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(battle.Turns.IsPlayerTurn, Is.True);
        }
        private IEnumerator BeginReaction(int capacity)
        {
            reaction = new OverwatchTestBattle(); AmmoTestWeapons.Equip(reaction.Watcher, capacity: capacity);
            Assert.That(reaction.Grid.TryMoveUnit(reaction.Spare, new Vector2Int(4, 4)), Is.True);
            reaction.Spare.transform.position = reaction.Grid.GetWorldPosition(new Vector2Int(4, 4)) + Vector3.up;
            reaction.AttachDecisionReceiver(); reaction.Arm(); reaction.StartTeam(UnitTeam.Enemy);
            Assert.That(reaction.Mover.Movement.TryMove(new Vector2Int(3, 2)), Is.True);
            yield return reaction.WaitForDecision();
        }
        private IEnumerator Finish(BattleUnit unit)
        {
            float end = Time.realtimeSinceStartup + 4;
            while (unit.Movement.IsMoving && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(unit.Movement.IsMoving, Is.False);
        }
        [UnityTest] public IEnumerator LastOverwatchRoundFiresThenReleasesMovementAndFutureEnemies()
        {
            yield return BeginReaction(1);
            long id = reaction.Overwatch.PendingDecision.Value.Id;
            Assert.That(reaction.Overwatch.TryFire(id, TargetBodyPart.Torso).WasExecuted, Is.True);
            Assert.That(reaction.Watcher.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.Zero);
            Assert.That(reaction.Watcher.Overwatch.IsActive, Is.False);
            Assert.That(reaction.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(3));
            Assert.That(reaction.Overwatch.TryFire(id, TargetBodyPart.Torso).WasExecuted, Is.False);
            yield return Finish(reaction.Mover);
            Assert.That(reaction.Spare.Movement.TryMove(new Vector2Int(4, 3)), Is.True);
            yield return Finish(reaction.Spare);
            Assert.That(reaction.Overwatch.PendingDecision.HasValue, Is.False);
            Assert.That(reaction.Turns.IsAnyUnitBusy, Is.False);
        }
        [UnityTest] public IEnumerator SkippingOverwatchKeepsLastRoundForAnotherEnemy()
        {
            yield return BeginReaction(1);
            Assert.That(reaction.Overwatch.TrySkip(reaction.Overwatch.PendingDecision.Value.Id), Is.True);
            yield return Finish(reaction.Mover);
            Assert.That(reaction.Watcher.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(1));
            Assert.That(reaction.Spare.Movement.TryMove(new Vector2Int(4, 3)), Is.True);
            yield return reaction.WaitForDecision();
            Assert.That(reaction.Overwatch.TryFire(reaction.Overwatch.PendingDecision.Value.Id, TargetBodyPart.Torso).WasExecuted, Is.True);
            yield return Finish(reaction.Spare);
            Assert.That(reaction.Watcher.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.Zero);
        }
        [UnityTest] public IEnumerator ReloadIsDeniedDuringGuardAndReactionDecision()
        {
            reaction = new OverwatchTestBattle(); AmmoTestWeapons.Equip(reaction.Watcher, capacity: 2, reload: 1);
            Assert.That(reaction.Combat.TryAttackWithWeapon(reaction.Watcher, reaction.Mover, WeaponSlot.RightHand).WasExecuted, Is.True);
            reaction.AttachDecisionReceiver(); reaction.Arm();
            Assert.That(reaction.Combat.TryReloadWithWeapon(reaction.Watcher, WeaponSlot.RightHand), Is.False);
            reaction.StartTeam(UnitTeam.Enemy);
            Assert.That(reaction.Mover.Movement.TryMove(new Vector2Int(3, 2)), Is.True);
            yield return reaction.WaitForDecision();
            Assert.That(reaction.Combat.TryReloadWithWeapon(reaction.Watcher, WeaponSlot.RightHand), Is.False);
            Assert.That(reaction.Watcher.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(1));
            Assert.That(reaction.Overwatch.TrySkip(reaction.Overwatch.PendingDecision.Value.Id), Is.True);
            yield return Finish(reaction.Mover);
        }
    }
}
