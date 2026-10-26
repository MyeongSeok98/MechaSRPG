using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class CriticalHitPlayTests
    {
        private MechaAccuracyTestBattle battle;
        [UnityTearDown] public IEnumerator Cleanup() { battle?.Dispose(); yield return null; }

        [UnityTest] public IEnumerator EnemyAIUsesPilotTechniqueAndCriticalDamage()
        {
            battle = new MechaAccuracyTestBattle(withAI: true, playerTechnique: 0, enemyTechnique: 50);
            battle.Combat.SetCriticalRollSource(() => 49.99f);
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            float end = Time.realtimeSinceStartup + 4;
            while (!battle.Turns.IsPlayerTurn && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(battle.Turns.IsPlayerTurn, Is.True);
            Assert.That(battle.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(14));
            Assert.That(battle.Enemy.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }

        private IEnumerator BeginReaction()
        {
            battle = new MechaAccuracyTestBattle(playerTechnique: 50, enemyTechnique: 0);
            battle.Overwatch.ConfigureDecisionReceiver(new OverwatchDecisionTestReceiver());
            Assert.That(battle.Overwatch.TryBegin(battle.Player, WeaponSlot.RightHand, FacingDirection.East), Is.True);
            battle.StartEnemyTeam();
            Assert.That(battle.Enemy.Movement.TryMove(new Vector2Int(2, 1)), Is.True);
            float end = Time.realtimeSinceStartup + 3;
            while (!battle.Overwatch.PendingDecision.HasValue && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(battle.Overwatch.PendingDecision.HasValue, Is.True);
        }
        [UnityTest] public IEnumerator OverwatchCriticalDamagesSelectedPartWithoutExtraAP()
        {
            yield return BeginReaction();
            int rolls = 0; battle.Combat.SetCriticalRollSource(() => { rolls++; return 0f; });
            long id = battle.Overwatch.PendingDecision.Value.Id;
            var preview = battle.Overwatch.GetDecisionPreview(id, TargetBodyPart.Head);
            Assert.That(preview.CriticalChance, Is.EqualTo(50f));
            Assert.That(preview.CriticalDamageMultiplier, Is.EqualTo(1.25f));
            Assert.That(preview.HitChance, Is.EqualTo(48)); Assert.That(rolls, Is.Zero);
            var result = battle.Overwatch.TryFire(id, TargetBodyPart.Head);
            Assert.That(result.Hit && result.IsCritical, Is.True);
            Assert.That(result.DamageDealt, Is.EqualTo(6));
            Assert.That(battle.Enemy.Health.GetCurrentHP(TargetBodyPart.Head), Is.Zero);
            Assert.That(battle.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(3));
            Assert.That(rolls, Is.EqualTo(1));
            Assert.That(battle.Overwatch.TryFire(id, TargetBodyPart.Head).WasExecuted, Is.False);
            Assert.That(rolls, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator OverwatchSkipDoesNotRollCritical()
        {
            yield return BeginReaction();
            int rolls = 0; battle.Combat.SetCriticalRollSource(() => { rolls++; return 0f; });
            Assert.That(battle.Overwatch.TrySkip(battle.Overwatch.PendingDecision.Value.Id), Is.True);
            Assert.That(rolls, Is.Zero);
            Assert.That(battle.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }
        [UnityTest] public IEnumerator TargetDiagramShowsTechniqueBasedCriticalProbability()
        {
            battle = new MechaAccuracyTestBattle(playerTechnique: 10, enemyTechnique: 20);
            battle.Enemy.gameObject.AddComponent<CapsuleCollider>();
            var visual = battle.Root.AddComponent<GridVisualizer>(); visual.Initialize(battle.Grid, battle.Turns);
            var debug = battle.Root.AddComponent<BattleDebugUI>(); debug.Initialize(battle.Grid, battle.Root.GetComponent<CoverSystem>());
            var camera = new GameObject("Critical UI").AddComponent<Camera>(); camera.transform.SetParent(battle.Root.transform);
            var input = battle.Root.AddComponent<PlayerInputController>();
            input.Initialize(camera, battle.Grid, visual, debug);
            input.InitializeCombat(battle.Combat, battle.Turns);
            input.SelectUnit(battle.Player);
            Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand), Is.True);
            Assert.That(input.TrySelectTarget(battle.Enemy), Is.True);
            var body = battle.Root.GetComponent<BodyTargetUI>();
            Assert.That(body.SelectedPartInfo, Does.Contain("치명타 2.5%").And.Contain("×1.25"));
            yield return null;
        }
    }
}
