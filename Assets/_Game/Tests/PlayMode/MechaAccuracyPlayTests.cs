using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class MechaAccuracyPlayTests
    {
        private MechaAccuracyTestBattle battle;
        [UnityTearDown] public IEnumerator Cleanup() { battle?.Dispose(); yield return null; }
        [UnityTest] public IEnumerator NormalBodyPanelAndShotShowSameAdjustedChance()
        {
            battle = new MechaAccuracyTestBattle(enemyEvasion: 50);
            battle.Enemy.gameObject.AddComponent<CapsuleCollider>();
            var visual = battle.Root.AddComponent<GridVisualizer>(); visual.Initialize(battle.Grid, battle.Turns);
            var debug = battle.Root.AddComponent<BattleDebugUI>(); debug.Initialize(battle.Grid, battle.Root.GetComponent<CoverSystem>());
            var camera = new GameObject("Mecha accuracy UI").AddComponent<Camera>(); camera.transform.SetParent(battle.Root.transform);
            camera.transform.position = new Vector3(5, 20, -10); camera.transform.LookAt(new Vector3(5, 0, 5));
            var input = battle.Root.AddComponent<PlayerInputController>(); input.Initialize(camera, battle.Grid, visual, debug);
            input.InitializeCombat(battle.Combat, battle.Turns);
            Assert.That(input.SelectUnit(battle.Player), Is.True); Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand), Is.True);
            Assert.That(input.TrySelectTarget(battle.Enemy), Is.True);
            var body = battle.Root.GetComponent<BodyTargetUI>();
            Assert.That(body.IsVisible, Is.True); Assert.That(body.SelectedPartInfo, Does.Contain("48%"));
            Assert.That(input.TrySelectBodyPart(TargetBodyPart.Head), Is.True); Assert.That(body.SelectedPartInfo, Does.Contain("38%"));
            battle.Combat.SetRollSource(() => 38);
            var result = input.TryAttackSelected();
            Assert.That(result.WasExecuted, Is.True); Assert.That(result.HitChance, Is.EqualTo(38)); Assert.That(result.Hit, Is.False);
            Assert.That(battle.Enemy.Health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(6));
            yield return null;
        }
        [UnityTest] public IEnumerator EnemyAIUsesItsAccuracyAgainstPlayerEvasion()
        {
            battle = new MechaAccuracyTestBattle(enemyAccuracy: 50, withAI: true); battle.Combat.SetRollSource(() => 90);
            Assert.That(battle.Turns.TryEndTurn(), Is.True); yield return WaitForPlayerTurn();
            Assert.That(battle.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15), "Accuracy50 raises base80 to the99% cap; roll90 still hits.");
            Assert.That(battle.Enemy.ActionPoints.CurrentPoints, Is.EqualTo(2)); Assert.That(battle.Turns.IsAnyUnitBusy, Is.False);
        }
        [UnityTest] public IEnumerator EnemyAIMissesBecauseOfPlayerMechaEvasion()
        {
            battle = new MechaAccuracyTestBattle(playerEvasion: 50, withAI: true); battle.Combat.SetRollSource(() => 50);
            Assert.That(battle.Turns.TryEndTurn(), Is.True); yield return WaitForPlayerTurn();
            Assert.That(battle.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20), "Evasion50 lowers base80 to48.");
            Assert.That(battle.Enemy.ActionPoints.CurrentPoints, Is.EqualTo(2)); Assert.That(battle.Turns.IsAnyUnitBusy, Is.False);
        }
        private IEnumerator WaitForPlayerTurn()
        {
            float end = Time.realtimeSinceStartup + 4;
            while (!battle.Turns.IsPlayerTurn && !battle.Turns.IsBattleOver && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(battle.Turns.IsPlayerTurn, Is.True);
        }
        private IEnumerator BeginReaction()
        {
            battle.Overwatch.ConfigureDecisionReceiver(new OverwatchDecisionTestReceiver());
            Assert.That(battle.Overwatch.TryBegin(battle.Player, WeaponSlot.RightHand, FacingDirection.East), Is.True);
            battle.StartEnemyTeam(); Assert.That(battle.Enemy.Movement.TryMove(new Vector2Int(2, 1)), Is.True);
            float end = Time.realtimeSinceStartup + 3;
            while (!battle.Overwatch.PendingDecision.HasValue && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(battle.Overwatch.PendingDecision.HasValue, Is.True);
        }
        private IEnumerator FinishMovement()
        {
            float end = Time.realtimeSinceStartup + 3;
            while (battle.Enemy.Movement.IsMoving && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(battle.Enemy.Movement.IsMoving || battle.Overwatch.IsResolving, Is.False);
        }
        [UnityTest] public IEnumerator OverwatchCombinesMechaPartAndReactionWithoutIntermediateRounding()
        {
            battle = new MechaAccuracyTestBattle(enemyEvasion: 50); yield return BeginReaction();
            long id = battle.Overwatch.PendingDecision.Value.Id;
            Assert.That(battle.Overwatch.GetDecisionPreview(id, TargetBodyPart.Torso).HitChance, Is.EqualTo(36));
            Assert.That(battle.Overwatch.GetDecisionPreview(id, TargetBodyPart.Head).HitChance, Is.EqualTo(29));
            battle.Combat.SetRollSource(() => 28);
            var result = battle.Overwatch.TryFire(id, TargetBodyPart.Head);
            Assert.That(result.WasExecuted && result.Hit, Is.True); Assert.That(result.HitChance, Is.EqualTo(29));
            Assert.That(battle.Enemy.Health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(1));
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(3));
            yield return FinishMovement();
        }
        [UnityTest] public IEnumerator BoostedReactionDoesNotClampBeforeReactionPenalty() => BoostedReaction(89, true);
        [UnityTest] public IEnumerator BoostedReactionUsesSameMissThresholdAsPreview() => BoostedReaction(90, false);
        private IEnumerator BoostedReaction(int roll, bool hit)
        {
            battle = new MechaAccuracyTestBattle(playerAccuracy: 50); yield return BeginReaction();
            long id = battle.Overwatch.PendingDecision.Value.Id;
            Assert.That(battle.Overwatch.GetDecisionPreview(id, TargetBodyPart.Torso).HitChance, Is.EqualTo(90), "80x1.5x.75=90, not75.");
            battle.Combat.SetRollSource(() => roll);
            var result = battle.Overwatch.TryFire(id, TargetBodyPart.Torso);
            Assert.That(result.WasExecuted, Is.True); Assert.That(result.Hit, Is.EqualTo(hit)); Assert.That(result.HitChance, Is.EqualTo(90));
            yield return FinishMovement();
        }
    }
}
