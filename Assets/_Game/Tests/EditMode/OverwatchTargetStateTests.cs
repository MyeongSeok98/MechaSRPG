using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class OverwatchTargetStateTests
    {
        private OverwatchTestBattle battle;
        [SetUp] public void Setup() => battle = new OverwatchTestBattle();
        [TearDown] public void Cleanup() => battle?.Dispose();

        private bool Call(BattleUnit watcher, string name, BattleUnit target)
        {
            var method = typeof(UnitOverwatch).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name + " must manage opportunities per target.");
            return (bool)method.Invoke(watcher.Overwatch, new object[] { target });
        }
        private bool CanReact(BattleUnit watcher, BattleUnit target) => Call(watcher, "CanReactTo", target);
        private bool Consume(BattleUnit watcher, BattleUnit target) => Call(watcher, "TryConsumeTarget", target);

        [Test]
        public void DifferentTargetsHaveIndependentOpportunities()
        {
            battle.Arm();
            Assert.That(CanReact(battle.Watcher, battle.Mover), Is.True);
            Assert.That(Consume(battle.Watcher, battle.Mover), Is.True);
            Assert.That(CanReact(battle.Watcher, battle.Mover), Is.False);
            Assert.That(CanReact(battle.Watcher, battle.Spare), Is.True);
            Assert.That(Consume(battle.Watcher, battle.Spare), Is.True);
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.True);
            Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(3));
        }

        [Test]
        public void DifferentWatchersHaveIndependentRecords()
        {
            battle.Arm(); battle.Arm(battle.Second);
            Assert.That(Consume(battle.Watcher, battle.Mover), Is.True);
            Assert.That(CanReact(battle.Second, battle.Mover), Is.True);
            Assert.That(Consume(battle.Second, battle.Mover), Is.True);
            Assert.That(CanReact(battle.Watcher, battle.Mover), Is.False);
            Assert.That(CanReact(battle.Second, battle.Mover), Is.False);
            Assert.That(battle.Second.Overwatch.IsActive, Is.True);
        }

        [Test]
        public void UsedTargetCannotReactAgainUntilRearmed()
        {
            battle.Arm();
            Assert.That(Consume(battle.Watcher, battle.Mover), Is.True);
            Assert.That(Consume(battle.Watcher, battle.Mover), Is.False);
            battle.StartTeam(UnitTeam.Enemy);
            Assert.That(CanReact(battle.Watcher, battle.Mover), Is.False);
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.True);
            battle.StartTeam(UnitTeam.Player);
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.False);
            battle.Arm();
            Assert.That(CanReact(battle.Watcher, battle.Mover), Is.True);
            Assert.That(Consume(battle.Watcher, battle.Mover), Is.True);
        }

        [Test]
        public void CancelAndRearmClearPreviouslyUsedTarget()
        {
            battle.Arm();
            Assert.That(Consume(battle.Watcher, battle.Mover), Is.True);
            battle.Overwatch.enabled = false;
            Assert.That(CanReact(battle.Watcher, battle.Mover), Is.False);
            battle.Overwatch.enabled = true;
            battle.Arm(); // Only AP 2 more for entering; no AP is spent by consuming a target.
            Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(1));
            Assert.That(Consume(battle.Watcher, battle.Mover), Is.True);
        }

        [Test]
        public void EqualNamesDoNotShareTargetRecords()
        {
            battle.Spare.name = battle.Mover.name;
            battle.Arm();
            Assert.That(Consume(battle.Watcher, battle.Mover), Is.True);
            Assert.That(CanReact(battle.Watcher, battle.Spare), Is.True);
        }

        [Test]
        public void InvalidTargetsAndInactiveGuardCannotConsumeOpportunity()
        {
            Assert.That(Consume(battle.Watcher, battle.Mover), Is.False);
            battle.Arm();
            Assert.That(Consume(battle.Watcher, null), Is.False);
            Assert.That(Consume(battle.Watcher, battle.Watcher), Is.False);
            Assert.That(Consume(battle.Watcher, battle.Second), Is.False);
            battle.Mover.enabled = false;
            Assert.That(Consume(battle.Watcher, battle.Mover), Is.False);
            battle.Mover.enabled = true;
            Assert.That(CanReact(battle.Watcher, battle.Mover), Is.True);
            battle.Mover.Health.ApplyDamage(TargetBodyPart.Torso, 20);
            Assert.That(Consume(battle.Watcher, battle.Mover), Is.False);
            Object.DestroyImmediate(battle.Mover.gameObject);
            Assert.That(Consume(battle.Watcher, battle.Mover), Is.False);
            Assert.That(CanReact(battle.Watcher, battle.Spare), Is.True);
        }

        [Test]
        public void CriticalTargetKeepsItsOwnOpportunityWhilePresent()
        {
            battle.Arm();
            battle.Mover.Health.SetDestructionRollSource(() => 99);
            battle.Mover.Health.ApplyDamage(TargetBodyPart.Head, 6);
            battle.Mover.Health.ApplyDamage(TargetBodyPart.LeftArm, 8);
            battle.Mover.Health.ApplyDamage(TargetBodyPart.RightArm, 8);
            Assert.That(battle.Mover.Health.State, Is.EqualTo(MechState.Critical));
            Assert.That(CanReact(battle.Watcher, battle.Mover), Is.True);
            Assert.That(Consume(battle.Watcher, battle.Mover), Is.True);
            Assert.That(CanReact(battle.Watcher, battle.Mover), Is.False);
        }
    }
}
