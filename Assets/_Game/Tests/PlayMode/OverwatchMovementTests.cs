using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class OverwatchMovementTests
    {
        private OverwatchTestBattle battle;
        [SetUp] public void Setup() => battle = new OverwatchTestBattle();
        [UnityTearDown] public IEnumerator Cleanup() { battle.Dispose(); yield return null; }
        private IEnumerator Finish()
        {
            float deadline = Time.realtimeSinceStartup + 6;
            while (battle.Mover.Movement.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(battle.Mover.Movement.IsMoving, Is.False, "Movement must complete or stop cleanly.");
        }
        private void BeginMove(Vector2Int target)
        {
            battle.StartTeam(UnitTeam.Enemy);
            Assert.That(battle.Mover.Movement.TryMove(target), Is.True);
        }
        private void AssertStoppedAtFirstCell()
        {
            Assert.That(battle.Mover.CurrentGridPosition, Is.EqualTo(new Vector2Int(3, 3)));
            Assert.That(battle.Mover.transform.position,
                Is.EqualTo(battle.Grid.GetWorldPosition(new Vector2Int(3, 3)) + Vector3.up * battle.Mover.HeightOffset));
            battle.Grid.TryGetCell(new Vector2Int(3, 3), out var cell);
            Assert.That(cell.OccupyingUnit, Is.SameAs(battle.Mover));
        }

        [UnityTest]
        public IEnumerator FirstCompletedCellFiresOnceThenSurvivorContinues()
        {
            battle.Arm(); int shots = 0;
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20), "Entry is not an attack.");
            battle.Combat.SetRollSource(() =>
            {
                shots++;
                Assert.That(battle.Mover.CurrentGridPosition, Is.EqualTo(new Vector2Int(3, 3)));
                Assert.That(battle.Mover.ActionPoints.CurrentPoints, Is.EqualTo(4));
                Assert.That(battle.Resolving, Is.True); Assert.That(battle.Turns.IsAnyUnitBusy, Is.True);
                Assert.That(battle.Turns.CanAct(battle.Spare), Is.False);
                Assert.That(battle.Combat.TryAttack(battle.Watcher, battle.Mover).WasExecuted, Is.False);
                var reaction = typeof(CombatSystem).GetMethod("TryOverwatchAttack", BindingFlags.Instance | BindingFlags.NonPublic);
                var duplicate = (AttackResult)reaction.Invoke(battle.Combat,
                    new object[] { battle.Watcher, battle.Mover, WeaponSlot.RightHand });
                Assert.That(duplicate.WasExecuted, Is.False, "A consumed guard cannot reenter the current reaction.");
                return 0;
            });
            BeginMove(new Vector2Int(3, 2)); yield return Finish();
            Assert.That(shots, Is.EqualTo(1)); Assert.That(battle.Watcher.Overwatch.IsActive, Is.False);
            Assert.That(battle.Mover.CurrentGridPosition, Is.EqualTo(new Vector2Int(3, 2)));
            Assert.That(battle.Mover.ActionPoints.CurrentPoints, Is.EqualTo(3));
            Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(3));
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(6));
            Assert.That(battle.Resolving, Is.False);
        }

        [UnityTest]
        public IEnumerator ReactionHitsAtFiftyNineBelowItsSixtyPercentBoundary()
        {
            battle.Arm(); battle.Combat.SetRollSource(() => 59);
            BeginMove(new Vector2Int(3, 3)); yield return Finish();
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.False);
        }

        [UnityTest]
        public IEnumerator ZeroRemainingAPCanReactAndMissConsumesGuard()
        {
            battle.Watcher.Combat.ConfigureTestRanged(5, 80, 7, 5);
            battle.Watcher.ActionPoints.TrySpend(3); battle.Arm();
            Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.Zero);
            int shots = 0; battle.Combat.SetRollSource(() => { shots++; return 60; });
            BeginMove(new Vector2Int(3, 2)); yield return Finish();
            Assert.That(shots, Is.EqualTo(1)); Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.False); Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ReactionSharesGripDistanceCoverHeadAndSingleRounding()
        {
            var loadout = battle.Watcher.GetComponent<UnitWeaponLoadout>(); var weapon = loadout.RightWeapon;
            JsonUtility.FromJsonOverwrite("{\"gripRequirement\":2,\"twoHandHitMultiplier\":1.1,\"distanceBands\":[{\"MaxDistance\":2,\"Multiplier\":0.9}]}", weapon);
            loadout.Configure(null, GripMode.OneHand, weapon, GripMode.TwoHands);
            battle.Watcher.Health.ApplyDamage(TargetBodyPart.Head, 6);
            battle.Grid.TryGetCell(new Vector2Int(3, 3), out var cell); cell.SetCover(CoverDirection.South, CoverType.Full);
            battle.Arm(); battle.Combat.SetRollSource(() => 22); // 80 * 1.1 * .9 * .75 * .5 * .75 = 22.275 -> 22, miss.
            BeginMove(new Vector2Int(3, 3)); yield return Finish();
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.False);
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }

        [UnityTest]
        public IEnumerator BlockedPersonalVisionRangeOrRearDoNotConsumeGuard()
        {
            battle.Watcher.Vision.Configure(1); battle.Arm();
            Assert.That(battle.Turns.Visibility.IsVisibleTo(UnitTeam.Player, battle.Mover), Is.True, "Teammate can detect the mover.");
            BeginMove(new Vector2Int(3, 3)); yield return Finish();
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.True);
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }
        [UnityTest]
        public IEnumerator BuildingOcclusionDoesNotConsumeGuard()
        {
            battle.Building(new Vector2Int(3, 2)); battle.Arm();
            BeginMove(new Vector2Int(3, 3)); yield return Finish();
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.True);
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }
        [UnityTest]
        public IEnumerator RearMovementDoesNotConsumeGuard()
        {
            battle.Arm(facing: FacingDirection.South); BeginMove(new Vector2Int(3, 3)); yield return Finish();
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.True);
        }
        [UnityTest]
        public IEnumerator OutOfWeaponRangeDoesNotConsumeGuard()
        {
            battle.Watcher.Combat.ConfigureTestRanged(5, 80, 1, 3); battle.Arm();
            BeginMove(new Vector2Int(3, 3)); yield return Finish();
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.True);
        }
        [UnityTest]
        public IEnumerator SameTeamMovementDoesNotConsumeGuard()
        {
            battle.Mover.ConfigureTeam(UnitTeam.Player); battle.Arm();
            Assert.That(battle.Mover.Movement.TryMove(new Vector2Int(3, 3)), Is.True); yield return Finish();
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.True);
        }
        [UnityTest]
        public IEnumerator StationaryEnemyAttackDoesNotTriggerReaction()
        {
            battle.Arm(); battle.StartTeam(UnitTeam.Enemy);
            Assert.That(battle.Combat.TryAttack(battle.Mover, battle.Watcher).WasExecuted, Is.True);
            yield return null;
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.True);
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }
        [UnityTest]
        public IEnumerator TurningThenCancellingBeforeCellEntryDoesNotTriggerReaction()
        {
            battle.Dispose(); yield return null;
            battle = new OverwatchTestBattle(withMoverVisual: true); battle.Arm();
            BeginMove(new Vector2Int(3, 3));
            // FollowPath first yields while turning the model from +Z toward -Z, before translation.
            Assert.That(battle.Mover.Movement.IsMoving, Is.True);
            Assert.That(battle.Mover.CurrentGridPosition, Is.EqualTo(new Vector2Int(3, 4)));
            Assert.That(battle.Mover.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.True);
            battle.Mover.Movement.CancelMove(); yield return null;
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.True); Assert.That(battle.Resolving, Is.False);
        }
        [UnityTest]
        public IEnumerator MultipleGuardsFireInRegistrationOrder()
        {
            battle.Arm(); battle.Arm(battle.Second);
            var order = new List<string>();
            battle.Combat.SetRollSource(() =>
            {
                order.Add(battle.Second.Overwatch.IsActive ? "first" : "second"); return 0;
            });
            BeginMove(new Vector2Int(3, 2)); yield return Finish();
            Assert.That(order, Is.EqualTo(new[] { "first", "second" }));
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(10));
        }
        [UnityTest]
        public IEnumerator DestroyedMoverStopsAndUnusedGuardRemains()
        {
            battle.Watcher.Combat.ConfigureTestRanged(20, 80, 7, 3);
            battle.Arm(); battle.Arm(battle.Second);
            BeginMove(new Vector2Int(3, 2)); yield return Finish();
            Assert.That(battle.Mover.Health.State, Is.EqualTo(MechState.Destroyed));
            Assert.That(battle.Mover.CurrentGridPosition, Is.EqualTo(new Vector2Int(3, 3)));
            Assert.That(battle.Mover.ActionPoints.CurrentPoints, Is.EqualTo(4));
            battle.Grid.TryGetCell(new Vector2Int(3, 3), out var cell); Assert.That(cell.Occupied, Is.False);
            Assert.That(battle.Second.Overwatch.IsActive, Is.True); Assert.That(battle.Resolving, Is.False);
        }
        [UnityTest]
        public IEnumerator CriticalMoverCanReceiveRemainingShotsThenStops()
        {
            battle.Arm(); battle.Arm(battle.Second); int damageRolls = 0;
            battle.Mover.Health.SetDestructionRollSource(() => 99);
            battle.Combat.SetDamageRollSource((min, max) =>
            {
                if (++damageRolls == 1)
                {
                    // Fixture changes status at the interruption point; body fire does not destroy these parts.
                    battle.Mover.Health.ApplyDamage(TargetBodyPart.Head, 6);
                    battle.Mover.Health.ApplyDamage(TargetBodyPart.LeftArm, 8);
                    battle.Mover.Health.ApplyDamage(TargetBodyPart.RightArm, 8);
                }
                return min;
            });
            BeginMove(new Vector2Int(3, 2)); yield return Finish();
            Assert.That(damageRolls, Is.EqualTo(2)); Assert.That(battle.Mover.Health.State, Is.EqualTo(MechState.Critical));
            AssertStoppedAtFirstCell();
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(10));
            Assert.That(battle.Resolving, Is.False);
        }
        [UnityTest]
        public IEnumerator BothLegsDestroyedAtInterruptionStopFurtherMovement()
        {
            battle.Arm();
            battle.Combat.SetDamageRollSource((min, max) =>
            {
                battle.Mover.Health.ApplyDamage(TargetBodyPart.LeftLeg, 10);
                battle.Mover.Health.ApplyDamage(TargetBodyPart.RightLeg, 10); return min;
            });
            BeginMove(new Vector2Int(3, 2)); yield return Finish();
            Assert.That(battle.Mover.Health.State, Is.EqualTo(MechState.Active));
            AssertStoppedAtFirstCell();
            Assert.That(battle.Mover.ActionPoints.CurrentPoints, Is.EqualTo(4)); Assert.That(battle.Resolving, Is.False);
        }
        [UnityTest]
        public IEnumerator DisableMovementDuringReactionReleasesBusyState()
        {
            battle.Arm();
            battle.Combat.SetRollSource(() => { battle.Mover.Movement.enabled = false; return 99; });
            BeginMove(new Vector2Int(3, 2)); yield return Finish();
            Assert.That(battle.Mover.CurrentGridPosition, Is.EqualTo(new Vector2Int(3, 3)));
            Assert.That(battle.Resolving, Is.False); Assert.That(battle.Turns.IsAnyUnitBusy, Is.False);
            Assert.That(battle.Turns.CanAct(battle.Spare), Is.True);
        }
        [UnityTest]
        public IEnumerator DisablingOverwatchDuringReactionClearsGateAndMovementCanFinish()
        {
            battle.Arm(); battle.Arm(battle.Second);
            battle.Combat.SetRollSource(() => { battle.Overwatch.enabled = false; return 99; });
            BeginMove(new Vector2Int(3, 2)); yield return Finish();
            Assert.That(battle.Mover.CurrentGridPosition, Is.EqualTo(new Vector2Int(3, 2)));
            Assert.That(battle.Resolving, Is.False); Assert.That(battle.Turns.IsAnyUnitBusy, Is.False);
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.False); Assert.That(battle.Second.Overwatch.IsActive, Is.False);
        }
        [UnityTest]
        public IEnumerator BattleEndDuringReactionReleasesAllGuardsAndBusyState()
        {
            battle.Spare.Health.ApplyDamage(TargetBodyPart.Torso, 20);
            battle.Watcher.Combat.ConfigureTestRanged(20, 80, 7, 3); battle.Arm(); battle.Arm(battle.Second);
            BeginMove(new Vector2Int(3, 2)); yield return Finish();
            Assert.That(battle.Turns.IsBattleOver, Is.True); Assert.That(battle.Turns.IsPlayerVictory, Is.True);
            Assert.That(battle.Resolving, Is.False); Assert.That(battle.Turns.IsAnyUnitBusy, Is.False);
            Assert.That(battle.Second.Overwatch.IsActive, Is.False);
        }
    }
}
