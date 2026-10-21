using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class CombatMathTests
    {
        [TestCase(3, 3, false)] [TestCase(3, 4, false)] [TestCase(4, 4, false)]
        [TestCase(5, 0, true)] [TestCase(6, 0, false)]
        [TestCase(3, 2, true)] [TestCase(-3, 2, true)] [TestCase(3, -2, true)]
        [TestCase(-3, -2, true)] [TestCase(0, -5, true)]
        public void GridRangeAddsAxisDistancesIncludingBoundary(int x, int z, bool expected) =>
            Assert.That(CombatMath.IsInRange(Vector2Int.zero, new Vector2Int(x, z), 5), Is.EqualTo(expected));
        [TestCase(1, 1, CoverDirection.NorthEast)] [TestCase(-1, -1, CoverDirection.SouthWest)]
        [TestCase(2, 1, CoverDirection.NorthEast)] [TestCase(-2, 1, CoverDirection.NorthWest)]
        [TestCase(3, -1, CoverDirection.East)] [TestCase(1, -3, CoverDirection.South)]
        [TestCase(5, -2, CoverDirection.East)] [TestCase(5, -3, CoverDirection.SouthEast)]
        [TestCase(-1, 0, CoverDirection.West)] [TestCase(1, -1, CoverDirection.SouthEast)]
        [TestCase(0, 2, CoverDirection.North)] [TestCase(0, -2, CoverDirection.South)]
        public void IncomingDirectionUsesEightAngularSectors(int x, int z, CoverDirection direction) =>
            Assert.That(CombatMath.GetIncomingDirection(new Vector2Int(x, z), Vector2Int.zero), Is.EqualTo(direction));
        [TestCase(80, .75f, 60)] [TestCase(80, .5f, 40)]
        [TestCase(10, 0f, 0)] [TestCase(120, 1f, 100)]
        public void HitChanceUsesMultiplicativeCover(int chance, float coverMultiplier, int expected) =>
            Assert.That(CombatMath.CalculateAttackHitChance(chance, 1f, coverMultiplier), Is.EqualTo(expected));
        [TestCase(55, 54, true)] [TestCase(55, 55, false)]
        [TestCase(0, 0, false)] [TestCase(100, 99, true)]
        public void RollBoundaryIsExclusive(int chance, int roll, bool expected) =>
            Assert.That(CombatMath.IsHit(chance, roll), Is.EqualTo(expected));
    }
}
