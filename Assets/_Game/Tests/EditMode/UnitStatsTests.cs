using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class UnitStatsTests
    {
        private GameObject root;
        [SetUp] public void SetUp() => root = new GameObject("Stats");
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);
        [Test]
        public void BodyDamageDestroysAndNotifiesOnce()
        {
            var health = root.AddComponent<UnitHealth>();
            health.ConfigurePartHP(TargetBodyPart.Torso, 20); health.Initialize();
            int deaths = 0; health.StateChanged += () => deaths++;
            Assert.That(health.ApplyDamage(TargetBodyPart.Torso, 5).Applied, Is.True);
            Assert.That(health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(health.ApplyDamage(TargetBodyPart.Torso, 100).Applied, Is.True);
            Assert.That(health.GetCurrentHP(TargetBodyPart.Torso), Is.Zero);
            Assert.That(health.IsPresent, Is.False);
            Assert.That(health.ApplyDamage(TargetBodyPart.Torso, 1).Applied, Is.False);
            Assert.That(deaths, Is.EqualTo(1));
        }
        [Test]
        public void InvalidDamageDoesNotHeal()
        {
            var health = root.AddComponent<UnitHealth>(); health.Initialize();
            Assert.That(health.ApplyDamage(TargetBodyPart.Torso, -5).Applied, Is.False);
            Assert.That(health.ApplyDamage(TargetBodyPart.Torso, 0).Applied, Is.False);
            Assert.That(health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }
        [Test]
        public void ActionPointsRejectOverspend()
        {
            var ap = root.AddComponent<UnitActionPoints>(); ap.ResetForTurn();
            Assert.That(ap.TrySpend(3), Is.True);
            Assert.That(ap.CurrentPoints, Is.EqualTo(2));
            Assert.That(ap.TrySpend(3), Is.False);
            Assert.That(ap.TrySpend(-1), Is.False);
            Assert.That(ap.CurrentPoints, Is.EqualTo(2));
            ap.ResetForTurn(); Assert.That(ap.CurrentPoints, Is.EqualTo(5));
        }
        [Test]
        public void UnitInitializeDoesNotRefillTwice()
        {
            var grid = root.AddComponent<GridManager>(); grid.Generate();
            var unit = new GameObject("Unit").AddTestBattleUnit();
            unit.transform.SetParent(root.transform);
            Assert.That(unit.Initialize(grid), Is.True);
            unit.Health.ApplyDamage(TargetBodyPart.Torso, 5); unit.ActionPoints.TrySpend(3);
            Assert.That(unit.Initialize(grid), Is.True);
            Assert.That(unit.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(unit.AvailableMovementRange, Is.EqualTo(2));
        }
    }
}
