using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class PathfinderTests
    {
        private GameObject root;
        private GridManager grid;
        private BattleUnit unit;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Path Tests");
            grid = root.AddComponent<GridManager>();
            grid.Generate();
            unit = new GameObject("Unit").AddTestBattleUnit();
            unit.transform.SetParent(root.transform);
            Place(Vector2Int.zero, 5);
        }
        private void Place(Vector2Int position, int range)
        {
            unit.Configure(position, range);
            grid.TryPlaceUnit(unit, position);
            unit.Initialize(grid);
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        private System.Collections.Generic.List<GridCell> FindPathToRange(Vector2Int target, int range)
            => Pathfinder.FindPathToRange(grid, unit, target, range);

        [Test]
        public void DiagonalTargetRequiresMovingIntoDiamondRange()
        {
            var path = FindPathToRange(new Vector2Int(3, 3), 5);
            Assert.That(path.Count, Is.EqualTo(1));
            Assert.That(path.Last().Position, Is.EqualTo(new Vector2Int(0, 1)));
        }
        [Test]
        public void InRangeButBuildingOccludedSearchesReachableFiringPosition()
        {
            Place(new Vector2Int(1, 1), 5);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform);
            cube.transform.position = grid.GetWorldPosition(new Vector2Int(2, 1)) + Vector3.up;
            cube.transform.localScale = new Vector3(2, 4, 2);
            grid.RegisterObstacle(cube.AddComponent<GridObstacle>().Collider);
            var path = FindPathToRange(new Vector2Int(4, 1), 5);
            Assert.That(path.Count, Is.EqualTo(2));
            Assert.That(path.Last().Position, Is.EqualTo(new Vector2Int(1, 3)));
        }
        [Test]
        public void FullPathExtendsBeyondThisTurnsMovementRange()
        {
            grid.Configure(8, 8, 2);
            grid.Generate();
            Place(new Vector2Int(1, 1), 5);
            unit.ActionPoints.TrySpend(4);
            for (int z = 0; z < 7; z++) grid.SetWalkable(new Vector2Int(2, z), false);

            var path = FindPathToRange(new Vector2Int(5, 1), 1);

            Assert.That(path.Count, Is.EqualTo(15));
            Assert.That(path.Last().Position, Is.EqualTo(new Vector2Int(5, 2)));
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(1));
            Assert.That(Pathfinder.FindPath(grid, unit, path.Last().Position), Is.Empty);
        }

        [Test]
        public void DetourCanInitiallyIncreaseDistance()
        {
            grid.Configure(8, 8, 2);
            grid.Generate();
            Place(new Vector2Int(1, 1), 5);
            for (int z = 0; z < 7; z++) grid.SetWalkable(new Vector2Int(2, z), false);

            var path = FindPathToRange(new Vector2Int(5, 1), 1);

            Assert.That(path[0].Position, Is.EqualTo(new Vector2Int(1, 2)));
            var previous = unit.CurrentGridPosition;
            foreach (var cell in path)
            {
                var delta = cell.Position - previous;
                Assert.That(Mathf.Abs(delta.x) + Mathf.Abs(delta.y), Is.EqualTo(1));
                previous = cell.Position;
            }
        }

        [Test]
        public void FullPathNeverEntersOccupiedOrBlockedCells()
        {
            grid.Configure(8, 8, 2);
            grid.Generate();
            Place(new Vector2Int(1, 1), 5);
            var other = new GameObject("Blocking Unit").AddTestBattleUnit();
            other.transform.SetParent(root.transform);
            other.Configure(new Vector2Int(2, 1), 5);
            Assert.That(other.Initialize(grid), Is.True);
            grid.SetWalkable(new Vector2Int(2, 2), false);

            var path = FindPathToRange(new Vector2Int(4, 1), 1);

            Assert.That(path.Count, Is.EqualTo(4));
            Assert.That(path.Last().Position, Is.EqualTo(new Vector2Int(3, 1)));
            Assert.That(path.All(c => c.Walkable && !c.Occupied), Is.True);
            Assert.That(unit.CurrentGridPosition, Is.EqualTo(new Vector2Int(1, 1)));
        }

        [Test]
        public void OccupiedTargetStopsAtEmptyAttackCell()
        {
            var target = new GameObject("Target").AddTestBattleUnit();
            target.transform.SetParent(root.transform);
            target.Configure(new Vector2Int(5, 0), 5);
            Assert.That(target.Initialize(grid), Is.True);

            var path = FindPathToRange(new Vector2Int(5, 0), 1);

            Assert.That(path.Count, Is.EqualTo(4));
            Assert.That(path.Last().Position, Is.EqualTo(new Vector2Int(4, 0)));
            Assert.That(path.All(c => !c.Occupied), Is.True);
            Assert.That(FindPathToRange(new Vector2Int(5, 0), 0), Is.Empty);
        }

        [Test]
        public void BlockedTargetHasNoPath()
        {
            grid.Configure(8, 8, 2);
            grid.Generate();
            Place(new Vector2Int(1, 1), 5);
            for (int z = 0; z < 8; z++) grid.SetWalkable(new Vector2Int(2, z), false);
            Assert.That(FindPathToRange(new Vector2Int(5, 1), 1), Is.Empty);
        }

        [Test]
        public void AlreadyInRangeOrInvalidSearchReturnsNoPath()
        {
            Assert.That(FindPathToRange(new Vector2Int(3, 2), 5), Is.Empty);
            Assert.That(FindPathToRange(new Vector2Int(3, 4), -1), Is.Empty);
            Assert.That(FindPathToRange(new Vector2Int(-1, 0), 1), Is.Empty);
            grid.ReleaseUnit(unit);
            Assert.That(FindPathToRange(new Vector2Int(5, 0), 1), Is.Empty);
        }

        [Test]
        public void RangeUsesRemainingActionPoints()
        {
            unit.ActionPoints.TrySpend(3);
            var cells = Pathfinder.GetReachableCells(grid, unit).Select(c => c.Position).ToList();
            Assert.That(cells.Contains(new Vector2Int(2, 0)), Is.True);
            Assert.That(cells.Contains(new Vector2Int(3, 0)), Is.False);
            Assert.That(Pathfinder.FindPath(grid, unit, new Vector2Int(3, 0)), Is.Empty);
            unit.ActionPoints.TrySpend(2);
            Assert.That(Pathfinder.GetReachableCells(grid, unit), Is.Empty);
        }
        [Test]
        public void EmptyGridRange()
        {
            var cells = Pathfinder.GetReachableCells(grid, unit).Select(c => c.Position).ToList();
            Assert.That(cells.Count, Is.EqualTo(20));
            Assert.That(cells.Contains(new Vector2Int(5, 0)), Is.True);
            Assert.That(cells.Contains(new Vector2Int(6, 0)), Is.False);
            Assert.That(cells.Contains(Vector2Int.zero), Is.False);
        }
        [Test]
        public void ObstacleDetour()
        {
            Place(new Vector2Int(1, 1), 5);
            grid.SetWalkable(new Vector2Int(2, 1), false);
            var path = Pathfinder.FindPath(grid, unit, new Vector2Int(3, 1));
            Assert.That(path.Count, Is.EqualTo(4));
            Assert.That(path.Last().Position, Is.EqualTo(new Vector2Int(3, 1)));
            Assert.That(path.Any(c => c.Position == new Vector2Int(2, 1)), Is.False);
            var previous = unit.CurrentGridPosition;
            foreach (var cell in path)
            {
                var delta = cell.Position - previous;
                Assert.That(Mathf.Abs(delta.x) + Mathf.Abs(delta.y), Is.EqualTo(1));
                previous = cell.Position;
            }
        }
        [Test]
        public void DetourExceedsRange()
        {
            Place(new Vector2Int(1, 1), 3);
            grid.SetWalkable(new Vector2Int(2, 1), false);
            Assert.That(Pathfinder.FindPath(grid, unit, new Vector2Int(3, 1)), Is.Empty);
        }
        [Test]
        public void OccupiedBlocksCorridor()
        {
            grid.Configure(3, 1, 2);
            grid.Generate();
            Place(Vector2Int.zero, 5);
            var other = new GameObject("Other").AddTestBattleUnit();
            other.transform.SetParent(root.transform);
            grid.TryPlaceUnit(other, Vector2Int.right);
            Assert.That(Pathfinder.FindPath(grid, unit, new Vector2Int(2, 0)), Is.Empty);
            Assert.That(Pathfinder.GetReachableCells(grid, unit), Is.Empty);
        }
        [Test]
        public void SurroundedAndOutside()
        {
            grid.SetWalkable(Vector2Int.right, false);
            grid.SetWalkable(Vector2Int.up, false);
            Assert.That(Pathfinder.GetReachableCells(grid, unit), Is.Empty);
            Assert.That(Pathfinder.FindPath(grid, unit, new Vector2Int(-1, 0)), Is.Empty);
            Assert.That(Pathfinder.FindPath(grid, unit, Vector2Int.zero), Is.Empty);
        }
        [Test]
        public void ZeroRangeAndUnregisteredUnitCannotMove()
        {
            unit.Configure(Vector2Int.zero, 0);
            Assert.That(Pathfinder.GetReachableCells(grid, unit), Is.Empty);
            grid.ReleaseUnit(unit);
            unit.Configure(Vector2Int.zero, 5);
            Assert.That(Pathfinder.GetReachableCells(grid, unit), Is.Empty);
        }
    }
}
