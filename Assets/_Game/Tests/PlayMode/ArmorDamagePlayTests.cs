using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class ArmorDamagePlayTests
    {
        private MechaAccuracyTestBattle battle;
        [UnityTearDown] public IEnumerator Cleanup() { battle?.Dispose(); yield return null; }

        [UnityTest] public IEnumerator EnemyAIRespectsResistance() => EnemyAttack(ArmorType.BeamResistant, false, 6);
        [UnityTest] public IEnumerator EnemyAICombinesResistanceAndCriticalOnce() => EnemyAttack(ArmorType.BeamResistant, true, 7);
        [UnityTest] public IEnumerator EnemyAIExploitsWeakness() => EnemyAttack(ArmorType.BallisticResistant, false, 8);
        [UnityTest] public IEnumerator EnemyAICombinesWeaknessAndCriticalOnce() => EnemyAttack(ArmorType.BallisticResistant, true, 10);
        private IEnumerator EnemyAttack(ArmorType armor, bool critical, int expected)
        {
            battle = new MechaAccuracyTestBattle(withAI: true, playerArmor: armor);
            battle.Enemy.Combat.ConfigureTestRanged(7, 80, 7, 3);
            battle.Combat.SetCriticalRollSource(() => critical ? 0f : 99f);
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            float end = Time.realtimeSinceStartup + 4;
            while (!battle.Turns.IsPlayerTurn && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(battle.Turns.IsPlayerTurn, Is.True);
            Assert.That(battle.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20 - expected));
            Assert.That(battle.Enemy.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator OverwatchCombinesResistanceAndCriticalWithoutChangingHitChance() => Reaction(RangedWeaponCategory.Beam, TargetBodyPart.RightLeg, 54, 7);
        [UnityTest] public IEnumerator OverwatchCombinesWeaknessAndCriticalWithoutExtraAP() => Reaction(RangedWeaponCategory.Ballistic, TargetBodyPart.Torso, 60, 10);
        private IEnumerator Reaction(RangedWeaponCategory category, TargetBodyPart part, int hitChance, int damage)
        {
            battle = new MechaAccuracyTestBattle(enemyArmor: ArmorType.BeamResistant);
            battle.Player.Combat.ConfigureTestRanged(7, 80, 7, 3);
            JsonUtility.FromJsonOverwrite("{\"rangedCategory\":" + (int)category + "}", battle.Player.GetComponent<UnitWeaponLoadout>().RightWeapon);
            battle.Combat.SetCriticalRollSource(() => 0f);
            battle.Overwatch.ConfigureDecisionReceiver(new OverwatchDecisionTestReceiver());
            Assert.That(battle.Overwatch.TryBegin(battle.Player, WeaponSlot.RightHand, FacingDirection.East), Is.True);
            battle.StartEnemyTeam();
            Assert.That(battle.Enemy.Movement.TryMove(new Vector2Int(2, 1)), Is.True);
            float end = Time.realtimeSinceStartup + 3;
            while (!battle.Overwatch.PendingDecision.HasValue && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(battle.Overwatch.PendingDecision.HasValue, Is.True);
            long id = battle.Overwatch.PendingDecision.Value.Id;
            var preview = battle.Overwatch.GetDecisionPreview(id, part);
            Assert.That(preview.HitChance, Is.EqualTo(hitChance));
            Assert.That(preview.ArmorMultiplier, Is.EqualTo(category == RangedWeaponCategory.Beam ? .8f : 1.1f));
            var result = battle.Overwatch.TryFire(id, part);
            Assert.That(result.Hit && result.IsCritical, Is.True);
            Assert.That(result.DamageDealt, Is.EqualTo(damage));
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(3));
            Assert.That(battle.Overwatch.TryFire(id, part).WasExecuted, Is.False);
        }
        [UnityTest] public IEnumerator TargetUIShowsArmorAdjustedDamageAndMatchesActualFire()
        {
            battle = new MechaAccuracyTestBattle(enemyArmor: ArmorType.BeamResistant);
            battle.Player.Combat.ConfigureTestRanged(7, 80, 7, 3);
            battle.Enemy.gameObject.AddComponent<CapsuleCollider>();
            var visual = battle.Root.AddComponent<GridVisualizer>(); visual.Initialize(battle.Grid, battle.Turns);
            var debug = battle.Root.AddComponent<BattleDebugUI>(); debug.Initialize(battle.Grid, battle.Root.GetComponent<CoverSystem>());
            var camera = new GameObject("Armor UI").AddComponent<Camera>(); camera.transform.SetParent(battle.Root.transform);
            var input = battle.Root.AddComponent<PlayerInputController>();
            input.Initialize(camera, battle.Grid, visual, debug); input.InitializeCombat(battle.Combat, battle.Turns);
            Assert.That(input.SelectUnit(battle.Player), Is.True);
            Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand), Is.True);
            Assert.That(input.TrySelectTarget(battle.Enemy), Is.True);
            var body = battle.Root.GetComponent<BodyTargetUI>();
            Assert.That(body.SelectedPartInfo, Does.Contain("장갑 ×0.8").And.Contain("명중률 80%"));
            Assert.That(body.AttackInfoText, Is.EqualTo("Damage 6 / AP 3"));
            Assert.That(input.TryAttackSelected().DamageDealt, Is.EqualTo(6));
            yield return null;
        }
    }
}
