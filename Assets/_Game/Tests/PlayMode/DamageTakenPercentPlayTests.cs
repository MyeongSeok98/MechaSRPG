using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class DamageTakenPercentPlayTests
    {
        private MechaAccuracyTestBattle b;
        private BattleUnit target;
        private void Create(UnitTeam team)
        {
            b = new MechaAccuracyTestBattle(configureMechaData: (player, enemy) =>
                JsonUtility.FromJsonOverwrite("{\"armorType\":1,\"beamDamageTakenPercent\":50,\"ballisticDamageTakenPercent\":150,\"bodyMaxHP\":10000}",
                    team == UnitTeam.Enemy ? enemy : player));
            target = team == UnitTeam.Enemy ? b.Enemy : b.Player;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { b?.Dispose(); yield return null; }
        [UnityTest] public IEnumerator BodyUIAndFireUseExplicitPercentInsteadOfLegacyArmor()
        {
            Create(UnitTeam.Enemy); b.Player.Combat.ConfigureTestRanged(100, 80, 7, 3);
            target.gameObject.AddComponent<CapsuleCollider>();
            var visual = b.Root.AddComponent<GridVisualizer>(); visual.Initialize(b.Grid, b.Turns);
            var debug = b.Root.AddComponent<BattleDebugUI>(); debug.Initialize(b.Grid, b.Root.AddComponent<CoverSystem>());
            var camera = new GameObject("Percent camera").AddComponent<Camera>(); camera.transform.SetParent(b.Root.transform);
            var input = b.Root.AddComponent<PlayerInputController>();
            input.Initialize(camera, b.Grid, visual, debug); input.InitializeCombat(b.Combat, b.Turns);
            Assert.That(input.SelectUnit(b.Player), Is.True); Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand), Is.True);
            Assert.That(input.TrySelectTarget(target), Is.True);
            var ui = b.Root.GetComponent<BodyTargetUI>();
            Assert.That(ui.SelectedPartInfo, Does.Contain("장갑 ×0.5"));
            Assert.That(ui.AttackInfoText, Is.EqualTo("Damage 50 / AP 3"));
            Assert.That(input.TryAttackSelected().DamageDealt, Is.EqualTo(50)); yield return null;
        }
        [UnityTest] public IEnumerator EnemyAIUsesTargetsExplicitBallisticWeakness()
        {
            Create(UnitTeam.Player);
            b.Enemy.Combat.ConfigureTestRanged(100, 80, 7, 3); b.Enemy.Combat.ConfigureTestMelee(0, 0, 0, 3);
            JsonUtility.FromJsonOverwrite("{\"rangedCategory\":1}", b.Enemy.GetComponent<UnitWeaponLoadout>().RightWeapon);
            var ai = b.Root.AddComponent<EnemyAIController>(); ai.Initialize(b.Grid, b.Combat, b.Turns); b.Turns.ConfigureEnemyAI(ai);
            Assert.That(b.Turns.TryEndTurn(), Is.True);
            float end = Time.realtimeSinceStartup + 5;
            while (!b.Turns.IsPlayerTurn && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(b.Turns.IsPlayerTurn, Is.True);
            Assert.That(target.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(9850));
            Assert.That(b.Enemy.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator GuardBeamResistanceAndCriticalRoundOnce() => Guard(RangedWeaponCategory.Beam, 63);
        [UnityTest] public IEnumerator GuardBallisticWeaknessAndCriticalRoundOnce() => Guard(RangedWeaponCategory.Ballistic, 188);
        private IEnumerator Guard(RangedWeaponCategory category, int expected)
        {
            Create(UnitTeam.Enemy); b.Player.Combat.ConfigureTestRanged(100, 80, 7, 3);
            JsonUtility.FromJsonOverwrite("{\"rangedCategory\":" + (int)category + "}", b.Player.GetComponent<UnitWeaponLoadout>().RightWeapon);
            b.Combat.SetCriticalRollSource(() => 0f);
            b.Overwatch.ConfigureDecisionReceiver(new OverwatchDecisionTestReceiver());
            Assert.That(b.Overwatch.TryBegin(b.Player, WeaponSlot.RightHand, FacingDirection.East), Is.True);
            b.StartEnemyTeam(); Assert.That(target.Movement.TryMove(new Vector2Int(4, 1)), Is.True);
            float end = Time.realtimeSinceStartup + 3;
            while (!b.Overwatch.PendingDecision.HasValue && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(b.Overwatch.PendingDecision.HasValue, Is.True);
            long id = b.Overwatch.PendingDecision.Value.Id;
            var preview = b.Overwatch.GetDecisionPreview(id, TargetBodyPart.Torso);
            Assert.That(preview.ArmorMultiplier, Is.EqualTo(category == RangedWeaponCategory.Beam ? .5f : 1.5f));
            var result = b.Overwatch.TryFire(id, TargetBodyPart.Torso);
            Assert.That(result.IsCritical, Is.True); Assert.That(result.DamageDealt, Is.EqualTo(expected));
        }
    }
}
