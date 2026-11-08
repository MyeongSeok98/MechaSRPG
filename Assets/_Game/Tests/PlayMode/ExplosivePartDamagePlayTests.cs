using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class ExplosivePartDamagePlayTests
    {
        private BackpackTestBattle b;
        private Mouse mouse;
        private bool changedInput;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private InputSettings.BackgroundBehavior oldBackground;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (changedInput)
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
                InputSystem.settings.backgroundBehavior = oldBackground;
            }
            b?.Dispose(); yield return null;
        }
        private void Create(int capacity = 6)
        {
            b = new BackpackTestBattle(capacity: capacity); ExplosiveTestSupport.Configure(b, 2, .1f, .1f);
            b.Combat.SetAdditionalPartIndexRollSource(max => 0);
            b.Combat.SetAdditionalDamageStepRollSource(count => 0);
        }
        [UnityTest] public IEnumerator PlayerPartSelectionAndFireShowAndApplyAdditionalDamage()
        {
            Create(); changedInput = true;
            b.Enemy.gameObject.AddComponent<CapsuleCollider>();
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode; oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var visual = b.Root.AddComponent<GridVisualizer>(); visual.Initialize(b.Grid, b.Turns);
            var debug = b.Root.AddComponent<BattleDebugUI>(); debug.Initialize(b.Grid, b.Cover);
            var camera = new GameObject("Explosion UI camera").AddComponent<Camera>(); camera.transform.SetParent(b.Root.transform);
            camera.transform.position = new Vector3(5, 20, -10); camera.transform.LookAt(new Vector3(5, 0, 5));
            var input = b.Root.AddComponent<PlayerInputController>(); input.Initialize(camera, b.Grid, visual, debug);
            input.InitializeCombat(b.Combat, b.Turns); debug.InitializeCombat(input, b.Turns);
            mouse = InputSystem.AddDevice<Mouse>();
            Assert.That(input.SelectUnit(b.Player), Is.True); Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand), Is.True);
            Assert.That(input.TrySelectTarget(b.Enemy), Is.True); yield return null;
            var body = b.Root.GetComponent<BodyTargetUI>();
            StringAssert.Contains("2", body.AdditionalDamageInfoText); StringAssert.Contains("10", body.AdditionalDamageInfoText);
            StringAssert.Contains("5%p", body.AdditionalDamageInfoText);
            Assert.That(body.PanelRect.yMin, Is.GreaterThanOrEqualTo(0)); Assert.That(body.PanelRect.yMax, Is.LessThanOrEqualTo(Screen.height));
            Assert.That(body.PanelRect.Contains(body.FireButtonRect.center), Is.True);
            yield return Click(body.GetPartRect(TargetBodyPart.LeftArm));
            Assert.That(input.SelectedBodyPart, Is.EqualTo(TargetBodyPart.LeftArm));
            yield return Click(body.FireButtonRect);
            var result = input.LastAttackResult;
            Assert.That(result.WasExecuted && result.Hit, Is.True); Assert.That(result.TargetPart, Is.EqualTo(TargetBodyPart.LeftArm));
            Assert.That(result.AdditionalPartDamage, Is.Not.Null);
            Assert.That(result.AdditionalPartDamage.Count, Is.EqualTo(2));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.LeftArm), Is.EqualTo(9200));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(9920));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(9920));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Cockpit), Is.EqualTo(10000));
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(2)); Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(5));
            StringAssert.Contains("80", AttackPreviewText.AdditionalDamageResult(result));
        }
        private IEnumerator Click(Rect rect)
        {
            var point = new Vector2(rect.center.x, Screen.height - rect.center.y);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
        }
        [UnityTest] public IEnumerator EnemyAIUsesSameAdditionalDamageAndSingleResourceCost()
        {
            Create();
            for (int part = 1; part <= 7; part++) b.Player.Health.ConfigurePartHP((TargetBodyPart)part, 10000);
            b.Player.Health.Initialize();
            var ai = b.Root.AddComponent<EnemyAIController>(); ai.Initialize(b.Grid, b.Combat, b.Turns); b.Turns.ConfigureEnemyAI(ai);
            Assert.That(b.Turns.TryEndTurn(), Is.True);
            float deadline = Time.realtimeSinceStartup + 5;
            while (!b.Turns.IsPlayerTurn && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(b.Turns.IsPlayerTurn, Is.True);
            Assert.That(b.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(9200));
            Assert.That(b.Player.Health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(9920));
            Assert.That(b.Player.Health.GetCurrentHP(TargetBodyPart.LeftArm), Is.EqualTo(9920));
            Assert.That(b.Enemy.ActionPoints.CurrentPoints, Is.EqualTo(2)); Assert.That(b.Enemy.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator BackpackReactionAppliesFragmentsAndLastRoundReleasesMovement()
        {
            Create(1); yield return BeginReaction();
            var preview = b.Overwatch.GetDecisionPreview(b.Overwatch.PendingDecision.Value.Id, TargetBodyPart.Torso);
            Assert.That(preview.AdditionalHitPartCount, Is.EqualTo(2));
            var result = b.Overwatch.TryFire(b.Overwatch.PendingDecision.Value.Id, TargetBodyPart.Torso);
            Assert.That(result.AdditionalPartDamage, Is.Not.Null);
            Assert.That(result.AdditionalPartDamage.Count, Is.EqualTo(2));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(9920));
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(3)); Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.Backpack).Loaded, Is.Zero);
            yield return FinishMovement(); Assert.That(b.Player.Overwatch.IsActive, Is.False);
            Assert.That(b.Overwatch.IsResolving || b.Overwatch.PendingDecision.HasValue, Is.False);
        }
        [UnityTest] public IEnumerator SkipDoesNotSelectPartsDamageOrConsumeAmmo()
        {
            Create(1); ExplosiveTestSupport.Rolls(b.Combat, max => { Assert.Fail("Skip cannot pick parts"); return 0; }, (min, max) => { Assert.Fail("Skip cannot roll damage"); return 0; });
            b.Combat.SetAdditionalDamageStepRollSource(count => { Assert.Fail("Skip cannot roll steps"); return 0; });
            yield return BeginReaction(); Assert.That(b.Overwatch.TrySkip(b.Overwatch.PendingDecision.Value.Id), Is.True);
            yield return FinishMovement();
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(10000));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(10000));
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.Backpack).Loaded, Is.EqualTo(1));
        }
        private IEnumerator BeginReaction()
        {
            b.Overwatch.ConfigureDecisionReceiver(new OverwatchDecisionTestReceiver());
            Assert.That(b.Overwatch.TryBegin(b.Player, WeaponSlot.Backpack, FacingDirection.East), Is.True);
            b.StartEnemyTeam(); Assert.That(b.Enemy.Movement.TryMove(new Vector2Int(2, 1)), Is.True);
            float deadline = Time.realtimeSinceStartup + 3;
            while (!b.Overwatch.PendingDecision.HasValue && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(b.Overwatch.PendingDecision.HasValue, Is.True);
        }
        private IEnumerator FinishMovement()
        {
            float deadline = Time.realtimeSinceStartup + 3;
            while (b.Enemy.Movement.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(b.Enemy.Movement.IsMoving, Is.False);
        }
    }
}
