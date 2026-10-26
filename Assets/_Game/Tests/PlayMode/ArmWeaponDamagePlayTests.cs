using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class ArmWeaponDamagePlayTests
    {
        private OverwatchTestBattle battle;
        private EnemyAITestBattle ai;
        private readonly List<WeaponData> weapons = new List<WeaponData>();
        private WeaponData Weapon(GripRequirement requirement = GripRequirement.Either, int range = 7, int damage = 5)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponData>(); weapons.Add(weapon);
            JsonUtility.FromJsonOverwrite($"{{\"displayName\":\"Arm test rifle\",\"gripRequirement\":{(int)requirement},\"range\":{range},\"minDamage\":{damage},\"maxDamage\":{damage},\"apCost\":3,\"baseHitChance\":80,\"twoHandHitMultiplier\":1.2}}", weapon);
            return weapon;
        }
        private void EquipTwoHands(BattleUnit unit, GripRequirement requirement = GripRequirement.Either, int range = 7)
            => unit.GetComponent<UnitWeaponLoadout>().Configure(null, GripMode.OneHand, Weapon(requirement, range), GripMode.TwoHands);
        private static void Break(BattleUnit unit, TargetBodyPart part) => unit.Health.ApplyDamage(part, unit.Health.GetMaxHP(part));
        [UnityTearDown] public IEnumerator Cleanup()
        {
            battle?.Dispose(); ai?.Dispose(); foreach (var weapon in weapons) Object.Destroy(weapon); weapons.Clear(); yield return null;
        }
        private PlayerInputController Input()
        {
            foreach (var unit in new[] { battle.Watcher, battle.Second, battle.Mover, battle.Spare }) unit.gameObject.AddComponent<CapsuleCollider>();
            var visualizer = battle.Root.AddComponent<GridVisualizer>(); visualizer.Initialize(battle.Grid, battle.Turns);
            var debug = battle.Root.AddComponent<BattleDebugUI>(); debug.Initialize(battle.Grid, battle.Root.GetComponent<CoverSystem>());
            var camera = new GameObject("Arm test camera").AddComponent<Camera>(); camera.transform.SetParent(battle.Root.transform);
            camera.transform.position = new Vector3(8, 20, -10); camera.transform.LookAt(new Vector3(8, 0, 8));
            var input = battle.Root.AddComponent<PlayerInputController>(); input.Initialize(camera, battle.Grid, visualizer, debug);
            input.InitializeCombat(battle.Combat, battle.Turns);
            Assert.That(input.SelectUnit(battle.Watcher), Is.True); return input;
        }
        [UnityTest] public IEnumerator MenuAndPreviewFollowSurvivingHandWithoutTwoHandBonus()
        {
            battle = new OverwatchTestBattle(); EquipTwoHands(battle.Watcher); var input = Input();
            Break(battle.Watcher, TargetBodyPart.RightArm);
            Assert.That(input.OpenAttackMenu(), Is.True);
            CollectionAssert.AreEqual(new[] { WeaponSlot.LeftHand }, input.GetAvailableWeaponSlots());
            var ui = battle.Root.GetComponent<UnitActionUI>();
            Assert.That(ui.GetWeaponButtonText(WeaponSlot.LeftHand), Does.StartWith("Left:"));
            Assert.That(input.BeginWeaponAttack(WeaponSlot.LeftHand), Is.True);
            Assert.That(input.TrySelectTarget(battle.Mover), Is.True);
            Assert.That(input.CurrentAttackPreview.CanAttack, Is.True, input.CurrentAttackPreview.FailureReason);
            Assert.That(input.CurrentAttackPreview.GripMultiplier, Is.EqualTo(1f));
            Assert.That(input.CurrentAttackPreview.HitChance, Is.EqualTo(80));
            Assert.That(input.TryAttackSelected().WasExecuted, Is.True);
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            yield return null;
        }
        [UnityTest] public IEnumerator SelectedBrokenHandCannotFireAndSelectionClears()
        {
            battle = new OverwatchTestBattle(); var input = Input();
            Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand), Is.True);
            Assert.That(input.TrySelectTarget(battle.Mover), Is.True);
            Break(battle.Watcher, TargetBodyPart.RightArm);
            int ap = battle.Watcher.ActionPoints.CurrentPoints;
            Assert.That(input.TryAttackSelected().WasExecuted, Is.False);
            yield return null;
            Assert.That(input.SelectedWeaponSlot.HasValue, Is.False);
            Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(ap));
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }
        [UnityTest] public IEnumerator BrokenFiringArmCancelsGuard()
        {
            battle = new OverwatchTestBattle(); battle.Arm(); Break(battle.Watcher, TargetBodyPart.RightArm);
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.False); yield return null;
        }
        [UnityTest] public IEnumerator BrokenOtherArmKeepsOneHandGuard()
        {
            battle = new OverwatchTestBattle(); battle.Arm(); Break(battle.Watcher, TargetBodyPart.LeftArm);
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.True); yield return null;
        }
        [UnityTest] public IEnumerator EitherFallbackCancelsGuardButCanRearmWithSurvivingHand()
        {
            battle = new OverwatchTestBattle(); EquipTwoHands(battle.Watcher); battle.Arm();
            Break(battle.Watcher, TargetBodyPart.RightArm);
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.False);
            Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(3));
            Assert.That(battle.Overwatch.TryBegin(battle.Watcher, WeaponSlot.LeftHand, FacingDirection.North), Is.True);
            Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(1)); yield return null;
        }
        [UnityTest] public IEnumerator RequiredTwoHandGuardCancelsOnSupportArmLoss()
        {
            battle = new OverwatchTestBattle(); EquipTwoHands(battle.Watcher, GripRequirement.TwoHandsRequired); battle.Arm();
            Break(battle.Watcher, TargetBodyPart.LeftArm);
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.False); yield return null;
        }
        [UnityTest] public IEnumerator PendingBrokenHandRejectsLateFireAndReleasesMover() => LosePendingArm(false);
        [UnityTest] public IEnumerator PendingTwoHandFallbackRejectsLateFireAndReleasesMover() => LosePendingArm(true);
        private IEnumerator LosePendingArm(bool twoHands)
        {
            battle = new OverwatchTestBattle(); if (twoHands) EquipTwoHands(battle.Watcher);
            battle.AttachDecisionReceiver(); battle.Arm(); battle.StartTeam(UnitTeam.Enemy);
            Assert.That(battle.Mover.Movement.TryMove(new Vector2Int(3, 2)), Is.True);
            yield return battle.WaitForDecision(); long id = battle.Overwatch.PendingDecision.Value.Id;
            Break(battle.Watcher, TargetBodyPart.RightArm);
            Assert.That(battle.Overwatch.TryFire(id, TargetBodyPart.Torso).WasExecuted, Is.False);
            float end = Time.realtimeSinceStartup + 3;
            while (battle.Mover.Movement.IsMoving && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(battle.Mover.Movement.IsMoving || battle.Overwatch.IsResolving, Is.False);
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(3));
        }
        [UnityTest] public IEnumerator PendingOneHandDecisionSurvivesOtherArmLoss()
        {
            battle = new OverwatchTestBattle(); battle.AttachDecisionReceiver(); battle.Arm(); battle.StartTeam(UnitTeam.Enemy);
            Assert.That(battle.Mover.Movement.TryMove(new Vector2Int(3, 2)), Is.True);
            yield return battle.WaitForDecision(); long id = battle.Overwatch.PendingDecision.Value.Id;
            Break(battle.Watcher, TargetBodyPart.LeftArm);
            Assert.That(battle.Overwatch.PendingDecision.Value.Id, Is.EqualTo(id));
            Assert.That(battle.Overwatch.TryFire(id, TargetBodyPart.Torso).WasExecuted, Is.True);
            yield return null;
        }
        private void MakeAI()
        {
            ai = new EnemyAITestBattle(new[] { new Vector2Int(3, 1) }, new[] { new Vector2Int(3, 4) });
            ai.Combat.SetDamageRollSource((min, max) => min);
        }
        [UnityTest] public IEnumerator AIUsesHealthyHandInsteadOfDestroyedHand()
        {
            MakeAI(); var unit = ai.Enemies[0];
            unit.GetComponent<UnitWeaponLoadout>().Configure(Weapon(damage: 6), GripMode.OneHand, Weapon(damage: 10), GripMode.OneHand);
            Break(unit, TargetBodyPart.RightArm);
            Assert.That(ai.Turns.TryEndTurn(), Is.True); yield return ai.WaitForPlayerTurn();
            Assert.That(ai.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(14));
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator AIWithBothArmsDestroyedWaitsWithoutSpendingAP()
        {
            MakeAI(); var unit = ai.Enemies[0]; Break(unit, TargetBodyPart.RightArm); Break(unit, TargetBodyPart.LeftArm);
            Assert.That(ai.Turns.TryEndTurn(), Is.True); yield return ai.WaitForPlayerTurn();
            Assert.That(ai.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(5)); Assert.That(unit.Mobility.MovedCells, Is.Zero);
        }
        [UnityTest] public IEnumerator AIMidMoveFallbackUsesNewHandForPlannedAttack() => ShootMovingAIArm(true);
        [UnityTest] public IEnumerator AIMidMoveLostOnlyWeaponCannotAttack() => ShootMovingAIArm(false);
        private IEnumerator ShootMovingAIArm(bool fallback)
        {
            MakeAI(); var unit = ai.Enemies[0];
            if (fallback) EquipTwoHands(unit, range: 1); else unit.Combat.ConfigureTestRanged(5, 80, 1, 3);
            ai.Players[0].Combat.ConfigureTestRanged(8, 80, 7, 3);
            var service = ai.Root.AddComponent<OverwatchSystem>();
            service.Initialize(ai.Grid, ai.Combat, ai.Turns, ai.Players.Concat(ai.Enemies).ToArray());
            service.ConfigureDecisionReceiver(new OverwatchDecisionTestReceiver());
            Assert.That(service.TryBegin(ai.Players[0], WeaponSlot.RightHand, FacingDirection.North), Is.True);
            Assert.That(ai.Turns.TryEndTurn(), Is.True);
            float end = Time.realtimeSinceStartup + 3;
            while (!service.PendingDecision.HasValue && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(service.PendingDecision.HasValue, Is.True);
            Assert.That(service.TryFire(service.PendingDecision.Value.Id, TargetBodyPart.RightArm).WasExecuted, Is.True);
            Assert.That(unit.Health.GetCurrentHP(TargetBodyPart.RightArm), Is.Zero);
            if (fallback)
            {
                Assert.That(unit.GetComponent<UnitWeaponLoadout>().RightWeapon, Is.Null);
                Assert.That(unit.Combat.TryGetAttack(WeaponSlot.LeftHand, out var attack, out _), Is.True);
                Assert.That(attack.GripMultiplier, Is.EqualTo(1f));
            }
            yield return ai.WaitForPlayerTurn();
            Assert.That(ai.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(fallback ? 15 : 20));
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(fallback ? 0 : 3));
            Assert.That(unit.Mobility.MovedCells, Is.EqualTo(2));
            Assert.That(service.IsResolving || ai.Turns.IsAnyUnitBusy, Is.False);
        }
    }
}
