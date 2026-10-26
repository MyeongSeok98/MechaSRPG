using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class OverwatchDecisionAITests
    {
        private EnemyAITestBattle battle;
        private OverwatchSystem overwatch;
        [UnityTearDown] public IEnumerator Cleanup() { battle?.Dispose(); yield return null; }

        private void Make(bool secondWatcher = false, bool secondEnemy = false, Vector2Int? start = null)
        {
            battle = new EnemyAITestBattle(secondWatcher
                ? new[] { new Vector2Int(3, 1), new Vector2Int(5, 1) } : new[] { new Vector2Int(3, 1) },
                secondEnemy ? new[] { new Vector2Int(3, 4), new Vector2Int(4, 4) }
                    : new[] { start ?? new Vector2Int(3, 4) });
            foreach (var enemy in battle.Enemies) enemy.Combat.ConfigureTestRanged(5, 80, 1, 3);
            overwatch = battle.Root.AddComponent<OverwatchSystem>();
            overwatch.Initialize(battle.Grid, battle.Combat, battle.Turns, battle.Players.Concat(battle.Enemies).ToArray());
            overwatch.ConfigureDecisionReceiver(new OverwatchDecisionTestReceiver());
        }
        private void Begin(bool secondWatcher = false)
        {
            Assert.That(overwatch.TryBegin(battle.Players[0], WeaponSlot.RightHand, FacingDirection.North), Is.True);
            if (secondWatcher)
                Assert.That(overwatch.TryBegin(battle.Players[1], WeaponSlot.RightHand, FacingDirection.North), Is.True);
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
        }
        private IEnumerator WaitForDecision()
        {
            float deadline = Time.realtimeSinceStartup + 4;
            while (!overwatch.PendingDecision.HasValue && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(overwatch.PendingDecision.HasValue, Is.True, "AI movement must offer a decision.");
        }
        private IEnumerator AssertPausedForThreeFrames(OverwatchDecision decision)
        {
            var position = decision.Target.CurrentGridPosition;
            int ap = decision.Target.ActionPoints.CurrentPoints;
            for (int i = 0; i < 3; i++)
            {
                yield return null;
                Assert.That(overwatch.PendingDecision.HasValue, Is.True, "AI must not cancel the waiting reaction.");
                Assert.That(overwatch.PendingDecision.Value.Id, Is.EqualTo(decision.Id));
                Assert.That(decision.Target.CurrentGridPosition, Is.EqualTo(position));
                Assert.That(decision.Target.ActionPoints.CurrentPoints, Is.EqualTo(ap));
                Assert.That(battle.Turns.ActiveTeam, Is.EqualTo(UnitTeam.Enemy));
                Assert.That(battle.AI.ActingUnit, Is.SameAs(decision.Target));
                Assert.That(battle.Turns.ActiveEnemy, Is.SameAs(decision.Target));
                Assert.That(battle.Turns.IsAnyUnitBusy, Is.True);
            }
        }
        private void AssertOccupancy(BattleUnit unit)
        {
            Assert.That(battle.Grid.TryGetCell(unit.CurrentGridPosition, out var cell), Is.True);
            Assert.That(cell.OccupyingUnit, Is.SameAs(unit));
            Assert.That(unit.transform.position, Is.EqualTo(cell.WorldPosition + Vector3.up * unit.HeightOffset));
        }

        [UnityTest] public IEnumerator EnemyStaysOnCommittedCellUntilDecisionThenCompletesTurn()
        {
            Make(); Begin(); yield return WaitForDecision();
            var decision = overwatch.PendingDecision.Value;
            Assert.That(decision.Target.Mobility.MovedCells, Is.EqualTo(1));
            Assert.That(decision.Target.ActionPoints.CurrentPoints, Is.EqualTo(4));
            yield return AssertPausedForThreeFrames(decision);
            Assert.That(overwatch.TryFire(decision.Id, TargetBodyPart.Torso).WasExecuted, Is.True);
            yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(battle.Enemies[0].Mobility.MovedCells, Is.EqualTo(2));
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.Zero);
            Assert.That(battle.Turns.TurnNumber, Is.EqualTo(2));
            Assert.That(overwatch.PendingDecision.HasValue || battle.Turns.IsAnyUnitBusy, Is.False);
            AssertOccupancy(battle.Enemies[0]);
        }

        [UnityTest] public IEnumerator CriticalMoverWaitsForSecondWatcherBeforeStopping()
        {
            Make(secondWatcher: true);
            var enemy = battle.Enemies[0];
            enemy.Health.SetDestructionRollSource(() => 99);
            enemy.Health.ApplyDamage(TargetBodyPart.LeftArm, 8);
            enemy.Health.ApplyDamage(TargetBodyPart.LeftLeg, 10);
            enemy.Health.ApplyDamage(TargetBodyPart.Head, 5);
            Assert.That(enemy.Health.MajorPartsDestroyed, Is.EqualTo(2));
            Assert.That(enemy.Combat.GetAttackSlots(AttackType.Ranged), Is.Not.Empty, "Keep a working weapon so the real AI starts its movement.");
            Assert.That(enemy.AvailableMovementRange, Is.GreaterThanOrEqualTo(2));
            Begin(secondWatcher: true); yield return WaitForDecision();
            Assert.That(overwatch.TryFire(overwatch.PendingDecision.Value.Id, TargetBodyPart.Head).WasExecuted, Is.True);
            Assert.That(enemy.Health.State, Is.EqualTo(MechState.Critical));
            yield return WaitForDecision();
            var second = overwatch.PendingDecision.Value;
            Assert.That(second.Watcher, Is.SameAs(battle.Players[1]));
            yield return AssertPausedForThreeFrames(second);
            Assert.That(overwatch.TryFire(second.Id, TargetBodyPart.Torso).WasExecuted, Is.True);
            yield return battle.WaitForPlayerTurn();
            Assert.That(enemy.Mobility.MovedCells, Is.EqualTo(1));
            Assert.That(enemy.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(overwatch.PendingDecision.HasValue || battle.Turns.IsAnyUnitBusy, Is.False);
            AssertOccupancy(enemy);
        }

        [UnityTest] public IEnumerator FirstWatcherSkipsSecondWatcherFires()
        {
            Make(secondWatcher: true); Begin(secondWatcher: true); yield return WaitForDecision();
            Assert.That(overwatch.PendingDecision.Value.Watcher, Is.SameAs(battle.Players[0]));
            Assert.That(overwatch.TrySkip(overwatch.PendingDecision.Value.Id), Is.True);
            yield return WaitForDecision();
            var second = overwatch.PendingDecision.Value;
            Assert.That(second.Watcher, Is.SameAs(battle.Players[1]));
            yield return AssertPausedForThreeFrames(second);
            Assert.That(overwatch.TryFire(second.Id, TargetBodyPart.Torso).WasExecuted, Is.True);
            yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(battle.Enemies[0].Mobility.MovedCells, Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator SecondEnemyGetsItsOwnDecision()
        {
            Make(secondEnemy: true); Begin(); yield return WaitForDecision();
            var first = overwatch.PendingDecision.Value;
            Assert.That(first.Target, Is.SameAs(battle.Enemies[0]));
            yield return AssertPausedForThreeFrames(first);
            Assert.That(battle.Enemies[1].Mobility.MovedCells, Is.Zero);
            Assert.That(overwatch.TryFire(first.Id, TargetBodyPart.Torso).WasExecuted, Is.True);
            yield return WaitForDecision();
            var second = overwatch.PendingDecision.Value;
            Assert.That(second.Target, Is.SameAs(battle.Enemies[1]));
            Assert.That(second.Id, Is.GreaterThan(first.Id));
            Assert.That(overwatch.TryFire(second.Id, TargetBodyPart.Torso).WasExecuted, Is.True);
            yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies.All(e => e.Health.GetCurrentHP(TargetBodyPart.Torso) == 15), Is.True);
            Assert.That(battle.Turns.TurnNumber, Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator SkipAllowsAIToContinueWithoutAnotherPromptOnFollowingCells()
        {
            Make(); Begin(); yield return WaitForDecision();
            Assert.That(overwatch.TrySkip(overwatch.PendingDecision.Value.Id), Is.True);
            yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(battle.Enemies[0].Mobility.MovedCells, Is.EqualTo(2));
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
        }

        [UnityTest] public IEnumerator HeadDamageUsesExistingPenaltyOnSurvivingEnemyAttack()
        {
            Make(); battle.Enemies[0].Health.ApplyDamage(TargetBodyPart.Head, 5);
            int rolls = 0; battle.Combat.SetRollSource(() => ++rolls == 1 ? 0 : 60);
            Begin(); yield return WaitForDecision();
            Assert.That(overwatch.TryFire(overwatch.PendingDecision.Value.Id, TargetBodyPart.Head).WasExecuted, Is.True);
            yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].Health.GetCurrentHP(TargetBodyPart.Head), Is.Zero);
            Assert.That(rolls, Is.EqualTo(2), "Enemy still attacks with its destroyed-head penalty.");
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20), "60 misses at 80 * .75.");
        }

        [UnityTest] public IEnumerator BothLegsDestroyedStopAtCommittedCell()
        {
            Make(); var enemy = battle.Enemies[0];
            enemy.Health.ApplyDamage(TargetBodyPart.LeftLeg, 10);
            enemy.Health.ApplyDamage(TargetBodyPart.RightLeg, 6);
            Begin(); yield return WaitForDecision();
            Assert.That(overwatch.TryFire(overwatch.PendingDecision.Value.Id, TargetBodyPart.RightLeg).WasExecuted, Is.True);
            yield return battle.WaitForPlayerTurn();
            Assert.That(enemy.Health.State, Is.EqualTo(MechState.Active));
            Assert.That(enemy.Mobility.MovedCells, Is.EqualTo(1));
            Assert.That(enemy.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            AssertOccupancy(enemy);
        }

        [UnityTest] public IEnumerator OneLegDestroyedRechecksReducedMovementRange()
        {
            Make(start: new Vector2Int(3, 6)); var enemy = battle.Enemies[0];
            enemy.Health.ApplyDamage(TargetBodyPart.LeftLeg, 6);
            Begin(); yield return WaitForDecision();
            Assert.That(overwatch.TryFire(overwatch.PendingDecision.Value.Id, TargetBodyPart.LeftLeg).WasExecuted, Is.True);
            yield return battle.WaitForPlayerTurn();
            Assert.That(enemy.Mobility.MovedCells, Is.EqualTo(2));
            Assert.That(enemy.ActionPoints.CurrentPoints, Is.EqualTo(3));
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            AssertOccupancy(enemy);
        }

        [UnityTest] public IEnumerator DestroyedTargetClosesRemainingRequests()
        {
            Make(secondWatcher: true); battle.Enemies[0].Health.ApplyDamage(TargetBodyPart.Torso, 15);
            Begin(secondWatcher: true); yield return WaitForDecision(); long id = overwatch.PendingDecision.Value.Id;
            Assert.That(overwatch.TryFire(id, TargetBodyPart.Torso).WasExecuted, Is.True);
            yield return battle.WaitForPlayerTurn(); yield return null;
            Assert.That(battle.Turns.IsBattleOver && battle.Turns.IsPlayerVictory, Is.True);
            Assert.That(overwatch.PendingDecision.HasValue || battle.Turns.IsAnyUnitBusy, Is.False);
            Assert.That(battle.Turns.ActiveEnemy, Is.Null);
            Assert.That(battle.AI.ActingUnit, Is.Null);
            Assert.That(overwatch.TryFire(id, TargetBodyPart.Torso).WasExecuted, Is.False);
        }

        [UnityTest] public IEnumerator DisabledAIReleasesPending() => DisableDuringPending("ai");
        [UnityTest] public IEnumerator DisabledMovementReleasesPending() => DisableDuringPending("movement");
        [UnityTest] public IEnumerator DisabledTurnsReleasePending() => DisableDuringPending("turns");
        [UnityTest] public IEnumerator DisabledServiceReleasesPending() => DisableDuringPending("system");
        private IEnumerator DisableDuringPending(string component)
        {
            Make(); Begin(); yield return WaitForDecision(); long id = overwatch.PendingDecision.Value.Id;
            switch (component)
            {
                case "ai": battle.AI.enabled = false; break;
                case "movement": battle.Enemies[0].Movement.enabled = false; break;
                case "turns": battle.Turns.enabled = false; break;
                case "system": overwatch.enabled = false; break;
            }
            float deadline = Time.realtimeSinceStartup + 4;
            while (battle.Enemies[0].Movement.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.That(overwatch.PendingDecision.HasValue || battle.Turns.IsAnyUnitBusy, Is.False);
            Assert.That(overwatch.TryFire(id, TargetBodyPart.Torso).WasExecuted || overwatch.TrySkip(id), Is.False);
            AssertOccupancy(battle.Enemies[0]);
            switch (component)
            {
                case "ai": battle.AI.enabled = true; break;
                case "movement": battle.Enemies[0].Movement.enabled = true; break;
                case "turns": battle.Turns.enabled = true; break;
                case "system": overwatch.enabled = true; break;
            }
            yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Turns.CanAct(battle.Players[0]), Is.True);
            Assert.That(overwatch.PendingDecision.HasValue || battle.Turns.IsAnyUnitBusy, Is.False);
            Assert.That(battle.AI.ActingUnit, Is.Null);
            Assert.That(overwatch.TryFire(id, TargetBodyPart.Torso).WasExecuted, Is.False);
        }
    }
}
