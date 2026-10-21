using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class OverwatchRangeTests
    {
        [TestCase(FacingDirection.North, 0, 3, true)]
        [TestCase(FacingDirection.North, 3, 3, true)] [TestCase(FacingDirection.North, -3, 3, true)]
        [TestCase(FacingDirection.North, 4, 3, false)] [TestCase(FacingDirection.North, 3, 0, false)]
        [TestCase(FacingDirection.North, 0, -1, false)] [TestCase(FacingDirection.North, 0, 0, false)]
        [TestCase(FacingDirection.East, 3, 0, true)] [TestCase(FacingDirection.East, 3, 3, true)]
        [TestCase(FacingDirection.East, 3, -3, true)] [TestCase(FacingDirection.East, 0, 3, false)]
        [TestCase(FacingDirection.South, 0, -3, true)] [TestCase(FacingDirection.South, 3, -3, true)]
        [TestCase(FacingDirection.South, -3, -3, true)] [TestCase(FacingDirection.South, 0, 3, false)]
        [TestCase(FacingDirection.West, -3, 0, true)] [TestCase(FacingDirection.West, -3, 3, true)]
        [TestCase(FacingDirection.West, -3, -3, true)] [TestCase(FacingDirection.West, 3, 0, false)]
        [TestCase((FacingDirection)99, 0, 3, false)]
        public void ForwardNinetyDegreeConeIncludesItsEdges(FacingDirection facing, int x, int z, bool expected)
        {
            var type = typeof(BattleUnit).Assembly.GetType("MechaXCOM.OverwatchMath");
            Assert.That(type, Is.Not.Null, "Overwatch needs a forward-cone predicate.");
            Assert.That(type.GetMethod("IsInForwardCone").Invoke(null,
                new object[] { Vector2Int.zero, new Vector2Int(x, z), facing }), Is.EqualTo(expected));
        }
        [Test]
        public void CoverageUsesManhattanWeaponRangeAndDoesNotSpendAP()
        {
            using (var b = new OverwatchTestFixture())
            {
                b.Player.Combat.ConfigureTestRanged(5, 80, 5, 5);
                var cells = b.Coverage();
                b.Grid.TryGetCell(new Vector2Int(3, 8), out var boundary);
                b.Grid.TryGetCell(new Vector2Int(6, 6), out var outside);
                b.Grid.TryGetCell(new Vector2Int(5, 5), out var edge);
                Assert.That(cells, Has.Member(boundary)); Assert.That(cells, Has.Member(edge));
                Assert.That(cells, Has.No.Member(outside));
                Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(5)); Assert.That(b.Active, Is.False);
            }
        }
        [Test]
        public void TeammateDetectionDoesNotExtendPersonalCoverage()
        {
            using (var b = new OverwatchTestFixture())
            {
                b.Player.Vision.Configure(1);
                Assert.That(b.Turns.Visibility.IsVisibleTo(UnitTeam.Player, b.Enemy), Is.True);
                b.Grid.TryGetCell(b.Enemy.CurrentGridPosition, out var target);
                Assert.That(b.Coverage(), Has.No.Member(target));
            }
        }
        [Test]
        public void PersonalCircularVisionExcludesDiagonalBeyondRadius()
        {
            using (var b = new OverwatchTestFixture())
            {
                b.Player.Vision.Configure(3);
                b.Grid.TryGetCell(new Vector2Int(6, 6), out var diagonal);
                b.Grid.TryGetCell(new Vector2Int(3, 6), out var boundary);
                Assert.That(b.Coverage(), Has.No.Member(diagonal)); Assert.That(b.Coverage(), Has.Member(boundary));
            }
        }
        [Test]
        public void BuildingBlocksCoverageButRavineDoesNot()
        {
            using (var b = new OverwatchTestFixture())
            {
                b.Grid.TryGetCell(new Vector2Int(3, 6), out var target);
                b.Grid.SetWalkable(target.Position, false);
                Assert.That(b.Coverage(), Has.Member(target));
                b.Building(new Vector2Int(3, 4));
                Assert.That(b.Coverage(), Has.No.Member(target));
            }
        }
        [Test]
        public void DisabledVisionAndInvalidFacingHaveNoCoverage()
        {
            using (var b = new OverwatchTestFixture())
            {
                Assert.That(b.Coverage((FacingDirection)99), Is.Empty);
                b.Player.Vision.enabled = false; Assert.That(b.Coverage(), Is.Empty);
            }
        }
    }
}
