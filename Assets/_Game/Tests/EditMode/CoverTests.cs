using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class CoverTests
    {
        private GameObject root;
        private GridManager grid;
        private CoverSystem system;
        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Cover Tests");
            grid = root.AddComponent<GridManager>();
            grid.Generate();
            system = root.AddComponent<CoverSystem>();
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);
        private CoverObject Cover(Vector2Int position, CoverType type)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Test Cover";
            cube.transform.SetParent(root.transform);
            cube.transform.position = grid.GetWorldPosition(position) + Vector3.up;
            cube.transform.localScale = new Vector3(2, 2, 2);
            var cover = cube.AddComponent<CoverObject>();
            cover.Configure(type);
            Physics.SyncTransforms();
            return cover;
        }
        private GridCell Cell(int x, int z)
        {
            grid.TryGetCell(new Vector2Int(x, z), out var cell);
            return cell;
        }
        [Test]
        public void DiagonalCoverFootprintProtectsDiagonalNeighbor()
        {
            system.Build(grid, new[] { Cover(new Vector2Int(4, 2), CoverType.Full) });
            Assert.That(Cell(3, 3).GetCover(CoverDirection.SouthEast), Is.EqualTo(CoverType.Full));
        }
        [Test]
        public void FullNorth()
        {
            system.Build(grid, new[] { Cover(new Vector2Int(3, 4), CoverType.Full) });
            Assert.That(Cell(3, 4).Walkable, Is.False);
            Assert.That(Cell(3, 3).GetCover(CoverDirection.North), Is.EqualTo(CoverType.Full));
            Assert.That(system.GetCoverMultiplier(Cell(3, 3), CoverDirection.North), Is.EqualTo(.5f));
            Assert.That(Cell(3, 3).GetCover(CoverDirection.East), Is.EqualTo(CoverType.None));
            Assert.That(Cell(3, 3).GetCover(CoverDirection.South), Is.EqualTo(CoverType.None));
            Assert.That(Cell(3, 3).GetCover(CoverDirection.West), Is.EqualTo(CoverType.None));
        }
        [Test]
        public void HalfFourDirections()
        {
            system.Build(grid, new[] { Cover(new Vector2Int(3, 4), CoverType.Half) });
            Assert.That(system.GetCoverMultiplier(Cell(3, 3), CoverDirection.North), Is.EqualTo(.75f));
            Assert.That(system.GetCoverMultiplier(Cell(2, 4), CoverDirection.East), Is.EqualTo(.75f));
            Assert.That(system.GetCoverMultiplier(Cell(3, 5), CoverDirection.South), Is.EqualTo(.75f));
            Assert.That(system.GetCoverMultiplier(Cell(4, 4), CoverDirection.West), Is.EqualTo(.75f));
        }
        [Test]
        public void OrdinaryObstacleHasNoCover()
        {
            var cover = Cover(new Vector2Int(3, 4), CoverType.Full);
            grid.RegisterObstacle(cover.Collider);
            system.Build(grid, new CoverObject[0]);
            Assert.That(Cell(3, 4).Walkable, Is.False);
            foreach (CoverDirection direction in System.Enum.GetValues(typeof(CoverDirection)))
                Assert.That(Cell(3, 3).GetCover(direction), Is.EqualTo(CoverType.None));
        }
        [Test]
        public void LargeCubeOuterCells()
        {
            var cover = Cover(new Vector2Int(1, 1), CoverType.Full);
            cover.transform.position = new Vector3(4, 1, 3);
            cover.transform.localScale = new Vector3(4, 2, 2);
            Physics.SyncTransforms();
            system.Build(grid, new[] { cover });
            Assert.That(grid.GetCells().Count(c => !c.Walkable), Is.EqualTo(2));
            Assert.That(Cell(1, 0).GetCover(CoverDirection.North), Is.EqualTo(CoverType.Full));
            Assert.That(Cell(2, 0).GetCover(CoverDirection.North), Is.EqualTo(CoverType.Full));
            Assert.That(Cell(0, 1).GetCover(CoverDirection.East), Is.EqualTo(CoverType.Full));
            Assert.That(Cell(3, 1).GetCover(CoverDirection.West), Is.EqualTo(CoverType.Full));
        }
        [TestCase(false)] [TestCase(true)]
        public void OverlapPrefersFull(bool reverse)
        {
            var half = Cover(new Vector2Int(3, 4), CoverType.Half);
            var full = Cover(new Vector2Int(3, 4), CoverType.Full);
            system.Build(grid, reverse ? new[] { full, half } : new[] { half, full });
            Assert.That(Cell(3, 3).GetCover(CoverDirection.North), Is.EqualTo(CoverType.Full));
        }
        [Test]
        public void SmallPlacementOffset()
        {
            var cover = Cover(new Vector2Int(3, 4), CoverType.Half);
            cover.transform.position += new Vector3(0.05f, 0, 0.05f);
            Physics.SyncTransforms();
            system.Build(grid, new[] { cover });
            Assert.That(grid.GetCells().Count(c => !c.Walkable), Is.EqualTo(1));
        }
        [Test]
        public void EmptyFootprintWarns()
        {
            var cover = Cover(Vector2Int.zero, CoverType.Half);
            cover.transform.position = new Vector3(2, 1, 2);
            cover.transform.localScale = Vector3.one * 0.2f;
            Physics.SyncTransforms();
            LogAssert.Expect(LogType.Warning, new Regex("Test Cover.*no cell centers"));
            Assert.That(grid.RegisterObstacle(cover.Collider), Is.Empty);
        }
        [Test]
        public void RotatedCubeIsRejected()
        {
            var cover = Cover(Vector2Int.zero, CoverType.Full);
            cover.transform.rotation = Quaternion.Euler(0, 45, 0);
            LogAssert.Expect(LogType.Warning, new Regex("Test Cover.*axis-aligned"));
            Assert.That(grid.RegisterObstacle(cover.Collider), Is.Empty);
        }
    }
}
