using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class MovementAPTests
    {
        private object Ledger()
        {
            var type = typeof(BattleUnit).Assembly.GetType("MechaXCOM.MovementCostLedger");
            Assert.That(type, Is.Not.Null, "Turn movement ledger is required."); return Activator.CreateInstance(type);
        }
        private static object Call(object ledger, string method, params object[] args)
        { var m = ledger.GetType().GetMethod(method); Assert.That(m, Is.Not.Null, method); return m.Invoke(ledger, args); }
        private static bool Commit(object ledger, float efficiency, int ap, out int cost)
        {
            object[] args = { efficiency, ap, 0 }; bool ok = (bool)Call(ledger, "TryCommitStep", args);
            cost = (int)args[2]; return ok;
        }
        [TestCase(1f, new[] {1,1,1,1})]
        [TestCase(1.5f, new[] {1,1,0,1,1,0})]
        [TestCase(2f, new[] {1,0,1,0})]
        [TestCase(3f, new[] {1,0,0,1,0,0})]
        [TestCase(.5f, new[] {2,2,2})]
        public void EachCommittedCellChargesOnlyNewAP(float efficiency, int[] expected)
        {
            var ledger = Ledger(); int remaining = 20;
            foreach (int amount in expected) { Assert.That(Commit(ledger, efficiency, remaining, out int charge), Is.True);
                Assert.That(charge, Is.EqualTo(amount)); remaining -= charge; }
        }
        [Test] public void PaidCreditAllowsOneCellAtZeroAPButNotAnother()
        {
            var ledger = Ledger(); Assert.That(Commit(ledger, 2, 1, out int cost), Is.True); Assert.That(cost, Is.EqualTo(1));
            Assert.That((int)Call(ledger, "GetAffordableCells", 2f, 0), Is.EqualTo(1));
            Assert.That(Commit(ledger, 2, 0, out cost), Is.True); Assert.That(cost, Is.Zero);
            Assert.That(Commit(ledger, 2, 0, out _), Is.False);
        }
        [Test] public void EfficiencyChangeUsesCreditWithoutRepricingEarlierSteps()
        {
            var ledger = Ledger(); Assert.That(Commit(ledger, 2, 5, out _), Is.True);
            Assert.That(Commit(ledger, 1, 4, out int cost), Is.True); Assert.That(cost, Is.EqualTo(1));
            Assert.That((int)Call(ledger, "GetAffordableCells", 1f, 0), Is.Zero);
            Assert.That(Commit(ledger, 0, 3, out _), Is.False);
        }
        [Test] public void PreviewAndResetDoNotGrantFreeMovement()
        {
            var ledger = Ledger();
            Assert.That((double)Call(ledger, "GetAdditionalAPCost", 3, 1.5f), Is.EqualTo(2));
            Assert.That((int)Call(ledger, "GetAffordableCells", 1.5f, 2), Is.EqualTo(3));
            Assert.That(Commit(ledger, 2, 1, out _), Is.True); Call(ledger, "ResetForTurn");
            Assert.That(Commit(ledger, 2, 0, out _), Is.False);
        }
        [TestCase(float.Epsilon)] [TestCase(float.MaxValue)]
        public void ExtremeFiniteRatesHaveSafeBoundsAndNeverStartFree(float rate)
        {
            var ledger = Ledger(); Assert.That(Commit(ledger, rate, 0, out _), Is.False);
            int cells = (int)Call(ledger, "GetAffordableCells", rate, 5);
            Assert.That(cells, Is.GreaterThanOrEqualTo(0));
            Assert.That(Commit(ledger, rate, 5, out int cost), Is.EqualTo(rate == float.MaxValue));
            if (rate == float.MaxValue) Assert.That(cost, Is.EqualTo(1));
        }
        [TestCase(0)] [TestCase(-1)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidRatesHaveNoAffordableCells(float rate)
        { var ledger = Ledger(); Assert.That((int)Call(ledger, "GetAffordableCells", rate, 5), Is.Zero);
            Assert.That(Commit(ledger, rate, 5, out _), Is.False); }
        [Test] public void MechaEfficiencyReplacesFixedRangeAndIsSnapshotted()
        {
            using (var b = new MechaAccuracyTestBattle(configureMechaData: (p,e) => JsonUtility.FromJsonOverwrite("{\"movementCellsPerAP\":2}", p)))
            {
                Assert.That(b.Player.AvailableMovementRange, Is.EqualTo(10));
                JsonUtility.FromJsonOverwrite("{\"movementCellsPerAP\":1}", b.PlayerData);
                b.Player.Initialize(b.Grid); Assert.That(b.Player.AvailableMovementRange, Is.EqualTo(10));
                b.Player.Health.ApplyDamage(TargetBodyPart.LeftLeg, b.Player.Health.GetCurrentHP(TargetBodyPart.LeftLeg));
                Assert.That(b.Player.AvailableMovementRange, Is.EqualTo(5));
                b.Player.Health.ApplyDamage(TargetBodyPart.RightLeg, b.Player.Health.GetCurrentHP(TargetBodyPart.RightLeg));
                Assert.That(b.Player.AvailableMovementRange, Is.Zero);
            }
        }
        [TestCase(1.4f, 7)] [TestCase(1.8f, 9)] [TestCase(1.2f, 6)]
        public void AuthoredDecimalBoundaryMatchesRangePreviewAndSplitCharges(float rate, int cells)
        {
            var ledger = Ledger(); Assert.That((int)Call(ledger, "GetAffordableCells", rate, 5), Is.EqualTo(cells));
            Assert.That((double)Call(ledger, "GetAdditionalAPCost", cells, rate), Is.EqualTo(5));
            int ap = 5;
            for (int i = 0; i < cells; i++)
            { Assert.That(Commit(ledger, rate, ap, out int cost), Is.True, "Step " + i); ap -= cost; }
            Assert.That(ap, Is.Zero); Assert.That(Commit(ledger, rate, 0, out _), Is.False);
        }
    }
}
