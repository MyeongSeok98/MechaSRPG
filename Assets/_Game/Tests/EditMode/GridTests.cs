using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class GridTests
    {
        private GameObject root;
        private GridManager grid;
        private BattleUnit unit;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Grid Test");
            grid = root.AddComponent<GridManager>();
            grid.Configure(10, 10, 2f);
            grid.Generate();
            unit = new GameObject("Unit").AddTestBattleUnit();
            unit.transform.SetParent(root.transform);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void FirstAndLastCellCenters()
        {
            Assert.That(grid.GetWorldPosition(Vector2Int.zero), Is.EqualTo(new Vector3(1, 0, 1)));
            Assert.That(grid.GetWorldPosition(new Vector2Int(9, 9)), Is.EqualTo(new Vector3(19, 0, 19)));
        }

        [Test]
        public void WorldBounds()
        {
            Assert.That(grid.TryWorldToGrid(new Vector3(-0.01f, 0, 1), out _), Is.False);
            Assert.That(grid.TryWorldToGrid(new Vector3(20, 0, 1), out _), Is.False);
            Assert.That(grid.TryWorldToGrid(new Vector3(19.99f, 0, 1), out var pos), Is.True);
            Assert.That(pos, Is.EqualTo(new Vector2Int(9, 0)));
        }

        [Test]
        public void OriginOffset()
        {
            grid.transform.position = new Vector3(10, 0, -4);
            grid.Generate();
            Assert.That(grid.GetWorldPosition(Vector2Int.zero), Is.EqualTo(new Vector3(11, 0, -3)));
            Assert.That(grid.TryWorldToGrid(new Vector3(11, 0, -3), out var pos), Is.True);
            Assert.That(pos, Is.EqualTo(Vector2Int.zero));
        }

        [Test]
        public void OccupiedCannotBeOverwritten()
        {
            Assert.That(grid.TryPlaceUnit(unit, Vector2Int.zero), Is.True);
            var other = new GameObject("Other").AddTestBattleUnit();
            other.transform.SetParent(root.transform);
            Assert.That(grid.TryPlaceUnit(other, Vector2Int.zero), Is.False);
        }

        [Test]
        public void MoveTransfersOccupancy()
        {
            grid.TryPlaceUnit(unit, Vector2Int.zero);
            Assert.That(grid.TryMoveUnit(unit, Vector2Int.right), Is.True);
            grid.TryGetCell(Vector2Int.zero, out var oldCell);
            grid.TryGetCell(Vector2Int.right, out var newCell);
            Assert.That(oldCell.Occupied, Is.False);
            Assert.That(newCell.OccupyingUnit, Is.SameAs(unit));
            Assert.That(unit.CurrentGridPosition, Is.EqualTo(Vector2Int.right));
            grid.ReleaseUnit(unit);
            Assert.That(newCell.Occupied, Is.False);
        }

        [Test]
        public void BlockedPlacementFails()
        {
            grid.SetWalkable(Vector2Int.zero, false);
            Assert.That(grid.TryPlaceUnit(unit, Vector2Int.zero), Is.False);
        }

        [Test]
        public void InvalidSizesAreClamped()
        {
            grid.Configure(0, -1, 0);
            grid.Generate();
            Assert.That(grid.TryGetCell(Vector2Int.zero, out var cell), Is.True);
            Assert.That(cell.WorldPosition, Is.EqualTo(new Vector3(0.05f, 0, 0.05f)));
        }
    }
}

