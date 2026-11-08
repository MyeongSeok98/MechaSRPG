using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class PilotCombatPlayTests
    {
        private MechaAccuracyTestBattle battle;
        [UnityTearDown] public IEnumerator Cleanup() { battle?.Dispose(); yield return null; }

        [UnityTest] public IEnumerator EnemyAIShootingRaisesActualHitChance()
        {
            battle = new MechaAccuracyTestBattle(withAI: true, enemyShooting: 50);
            battle.Combat.SetRollSource(() => 90); battle.Combat.SetCriticalRollSource(() => 99f);
            Assert.That(battle.Turns.TryEndTurn(), Is.True); yield return WaitForPlayerTurn();
            Assert.That(battle.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(battle.Enemy.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator EnemyAIMissesBecauseOfTargetPilotEvasion()
        {
            battle = new MechaAccuracyTestBattle(withAI: true, playerPilotEvasion: 50);
            battle.Combat.SetRollSource(() => 50); battle.Combat.SetCriticalRollSource(() => 99f);
            Assert.That(battle.Turns.TryEndTurn(), Is.True); yield return WaitForPlayerTurn();
            Assert.That(battle.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(battle.Enemy.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator EnemyAIMeleeUsesPilotDamageAndExistingCriticalMultiplier()
        {
            battle = new MechaAccuracyTestBattle(withAI: true, enemyMelee: 50);
            battle.Enemy.Combat.ConfigureTestMelee(8, 95, 2, 3);
            var loadout = battle.Enemy.GetComponent<UnitWeaponLoadout>();
            loadout.Configure(loadout.LeftWeapon, GripMode.OneHand, null, GripMode.OneHand);
            battle.Combat.SetRollSource(() => 98); battle.Combat.SetCriticalRollSource(() => 0f);
            Assert.That(battle.Turns.TryEndTurn(), Is.True); yield return WaitForPlayerTurn();
            Assert.That(battle.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(7), "8 x 1.25 x 1.25 = 12.5 rounds13.");
            Assert.That(battle.Enemy.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator BodyPanelAndFireUseSamePilotAdjustedPartChance()
        {
            battle = new MechaAccuracyTestBattle(playerShooting: 30, enemyPilotEvasion: 10);
            battle.Enemy.gameObject.AddComponent<CapsuleCollider>();
            var visual = battle.Root.AddComponent<GridVisualizer>(); visual.Initialize(battle.Grid, battle.Turns);
            var debug = battle.Root.AddComponent<BattleDebugUI>(); debug.Initialize(battle.Grid, battle.Root.GetComponent<CoverSystem>());
            var camera = new GameObject("Pilot combat UI").AddComponent<Camera>(); camera.transform.SetParent(battle.Root.transform);
            var input = battle.Root.AddComponent<PlayerInputController>(); input.Initialize(camera, battle.Grid, visual, debug);
            input.InitializeCombat(battle.Combat, battle.Turns);
            Assert.That(input.SelectUnit(battle.Player), Is.True);
            Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand), Is.True);
            Assert.That(input.TrySelectTarget(battle.Enemy), Is.True);
            Assert.That(input.TrySelectBodyPart(TargetBodyPart.Head), Is.True);
            var body = battle.Root.GetComponent<BodyTargetUI>();
            Assert.That(body.SelectedPartInfo, Does.Contain("77%"), "80 x1.2 x.8 =76.8.");
            battle.Combat.SetRollSource(() => 77);
            var result = input.TryAttackSelected();
            Assert.That(result.WasExecuted, Is.True); Assert.That(result.Hit, Is.False);
            Assert.That(result.HitChance, Is.EqualTo(77));
            Assert.That(battle.Enemy.Health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(6));
            yield return null;
        }
        [UnityTest] public IEnumerator OverwatchUsesPilotDifferenceAlongsideReactionAndPart()
        {
            battle = new MechaAccuracyTestBattle(playerShooting: 50, enemyPilotEvasion: 10);
            battle.Overwatch.ConfigureDecisionReceiver(new OverwatchDecisionTestReceiver());
            Assert.That(battle.Overwatch.TryBegin(battle.Player, WeaponSlot.RightHand, FacingDirection.East), Is.True);
            battle.StartEnemyTeam(); Assert.That(battle.Enemy.Movement.TryMove(new Vector2Int(2, 1)), Is.True);
            float end = Time.realtimeSinceStartup + 3;
            while (!battle.Overwatch.PendingDecision.HasValue && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(battle.Overwatch.PendingDecision.HasValue, Is.True);
            long id = battle.Overwatch.PendingDecision.Value.Id;
            var preview = battle.Overwatch.GetDecisionPreview(id, TargetBodyPart.Head);
            Assert.That(preview.HitChance, Is.EqualTo(67), "80 x1.4 x.75 x.8 =67.2.");
            battle.Combat.SetRollSource(() => 66); battle.Combat.SetCriticalRollSource(() => 99f);
            var result = battle.Overwatch.TryFire(id, TargetBodyPart.Head);
            Assert.That(result.WasExecuted && result.Hit, Is.True); Assert.That(result.HitChance, Is.EqualTo(67));
            Assert.That(result.DamageDealt, Is.EqualTo(5), "Pilot shooting must not increase damage.");
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(3));
            Assert.That(battle.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(5));
            end = Time.realtimeSinceStartup + 3;
            while (battle.Enemy.Movement.IsMoving && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(battle.Enemy.Movement.IsMoving || battle.Overwatch.IsResolving, Is.False);
        }
        private IEnumerator WaitForPlayerTurn()
        {
            float end = Time.realtimeSinceStartup + 4;
            while (!battle.Turns.IsPlayerTurn && !battle.Turns.IsBattleOver && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(battle.Turns.IsPlayerTurn, Is.True);
        }
    }
}
