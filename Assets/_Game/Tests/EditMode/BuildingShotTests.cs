using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Linq;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class BuildingShotTests
    {
        private GameObject root;
        private GridManager grid;
        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Building shot tests");
            grid = root.AddComponent<GridManager>();
            grid.Configure(8, 8, 2); grid.Generate();
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);
        private GridObstacle Building(int x, int z)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform);
            cube.transform.position = grid.GetWorldPosition(new Vector2Int(x, z)) + Vector3.up;
            cube.transform.localScale = new Vector3(2, 4, 2);
            return cube.AddComponent<GridObstacle>();
        }
        private bool Clear(Vector2Int from, Vector2Int to)
        {
            return grid.HasClearShot(from, to);
        }
        [TestCase(1, 1, 5, 1, 3, 1, false)]
        [TestCase(2, 1, 2, 5, 2, 3, false)]
        [TestCase(1, 1, 4, 4, 2, 2, false)]
        [TestCase(1, 1, 4, 4, 2, 1, false)]
        [TestCase(1, 4, 4, 1, 2, 4, false)]
        [TestCase(1, 1, 4, 2, 2, 2, false)]
        [TestCase(1, 1, 2, 4, 2, 2, false)]
        [TestCase(1, 1, 5, 1, 3, 2, true)]
        [TestCase(1, 1, 4, 2, 2, 3, true)]
        public void BuildingFootprintBlocksIntersectionsIncludingCornersSymmetrically(
            int x1, int z1, int x2, int z2, int bx, int bz, bool expected)
        {
            grid.RegisterObstacle(Building(bx, bz).Collider);
            var from = new Vector2Int(x1, z1); var to = new Vector2Int(x2, z2);
            Assert.That(Clear(from, to), Is.EqualTo(expected));
            Assert.That(Clear(to, from), Is.EqualTo(expected));
        }
        [Test]
        public void RavineBlocksMovementButAllowsShotsAcrossItsFootprint()
        {
            var ravine = Building(3, 1);
            var serialized = new SerializedObject(ravine);
            var blocksShots = serialized.FindProperty("blocksShots");
            Assert.That(blocksShots, Is.Not.Null, "Terrain needs an Inspector shot-blocking setting.");
            blocksShots.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var controller = root.AddComponent<BattleSceneController>();
            controller.Configure(grid, root.AddComponent<CoverSystem>(), new BattleUnit[0],
                new[] { ravine }, new CoverObject[0]);
            Assert.That(controller.InitializeBattle(), Is.True);
            grid.TryGetCell(new Vector2Int(3, 1), out var cell);
            Assert.That(cell.Walkable, Is.False);
            Assert.That(Clear(new Vector2Int(1, 1), new Vector2Int(5, 1)), Is.True);
            var unit = new GameObject("Unit").AddTestBattleUnit();
            unit.transform.SetParent(root.transform);
            unit.Configure(new Vector2Int(1, 1), 5);
            Assert.That(unit.Initialize(grid), Is.True);
            Assert.That(grid.TryMoveUnit(unit, new Vector2Int(3, 1)), Is.False);
            var path = Pathfinder.FindPath(grid, unit, new Vector2Int(4, 1));
            Assert.That(path.Count, Is.EqualTo(5));
            Assert.That(path, Has.None.EqualTo(cell));
            Assert.That(Pathfinder.GetReachableCells(grid, unit), Has.No.Member(cell));
        }
        [Test]
        public void SampleMapRavinesRegisterWithoutOverlappingUnitsOrOtherTerrain()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/SqurdWeaponTest.unity", OpenSceneMode.Additive);
            try
            {
                var objects = scene.GetRootGameObjects();
                var ravines = objects.SelectMany(o => o.GetComponentsInChildren<GridObstacle>())
                    .Where(o => o.name.StartsWith("Ravine")).ToArray();
                Assert.That(ravines.Length, Is.EqualTo(3));
                var controller = objects.SelectMany(o => o.GetComponentsInChildren<BattleSceneController>()).Single();
                Assert.That(controller.InitializeBattle(), Is.True);
                var sampleGrid = controller.GetComponent<GridManager>();
                var otherTerrain = objects.SelectMany(o => o.GetComponentsInChildren<Collider>())
                    .Where(c => (c.GetComponent<GridObstacle>() && !c.name.StartsWith("Ravine")) ||
                        c.GetComponent<CoverObject>()).ToArray();
                Physics.SyncTransforms();
                int count = 0;
                foreach (var ravine in ravines)
                {
                    Assert.That(ravine.BlocksShots, Is.False);
                    foreach (var cell in sampleGrid.GetCells())
                    {
                        var bounds = ravine.Collider.bounds;
                        if (cell.WorldPosition.x < bounds.min.x || cell.WorldPosition.x > bounds.max.x ||
                            cell.WorldPosition.z < bounds.min.z || cell.WorldPosition.z > bounds.max.z) continue;
                        count++;
                        Assert.That(cell.Walkable, Is.False);
                        Assert.That(cell.BlocksShots, Is.False);
                        Assert.That(cell.Occupied, Is.False);
                        foreach (var collider in otherTerrain)
                            Assert.That(collider.bounds.Contains(new Vector3(cell.WorldPosition.x,
                                collider.bounds.center.y, cell.WorldPosition.z)), Is.False);
                    }
                }
                Assert.That(count, Is.GreaterThan(0));
                foreach (var unit in objects.SelectMany(o => o.GetComponentsInChildren<BattleUnit>()))
                {
                    Assert.That(sampleGrid.TryGetCell(unit.CurrentGridPosition, out var cell), Is.True);
                    Assert.That(cell.OccupyingUnit, Is.SameAs(unit));
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        [Test]
        public void CoverRebuildPreservesBuildingsAndGridRegenerationClearsThem()
        {
            grid.RegisterObstacle(Building(3, 1).Collider);
            root.AddComponent<CoverSystem>().Build(grid, new CoverObject[0]);
            Assert.That(Clear(new Vector2Int(1, 1), new Vector2Int(5, 1)), Is.False);
            grid.Generate();
            Assert.That(Clear(new Vector2Int(1, 1), new Vector2Int(5, 1)), Is.True);
        }
        [Test]
        public void ExistingSceneRegistrationMarksBuildingsWithoutSceneMigration()
        {
            var controller = root.AddComponent<BattleSceneController>();
            var covers = root.AddComponent<CoverSystem>();
            controller.Configure(grid, covers, new BattleUnit[0], new[] { Building(3, 1) }, new CoverObject[0]);
            Assert.That(controller.InitializeBattle(), Is.True);
            Assert.That(Clear(new Vector2Int(1, 1), new Vector2Int(5, 1)), Is.False);
        }
        [Test]
        public void InvalidEndpointsCannotProduceClearShot()
        {
            Assert.That(Clear(new Vector2Int(-1, 0), Vector2Int.zero), Is.False);
            Assert.That(Clear(Vector2Int.zero, new Vector2Int(8, 8)), Is.False);
        }
    }
}
