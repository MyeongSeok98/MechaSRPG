using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class SquadTerrainTests
    {
        [Test]
        public void SavedSquadHasSixRegisteredUnitsAndTraversableMixedTerrain()
        {
            const string path = "Assets/_Game/Scenes/SqurdWeaponTest.unity";
            Assert.That(File.Exists(path), Is.True, "The retained squad weapon scene must exist.");
            var bytes = File.ReadAllBytes(path);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var controller = roots.SelectMany(r => r.GetComponentsInChildren<BattleSceneController>()).Single();
                Assert.That(controller.InitializeBattle(), Is.True);
                var grid = controller.GetComponent<GridManager>();
                Assert.That(grid.Width * grid.CellSize, Is.EqualTo(40));
                Assert.That(grid.Height * grid.CellSize, Is.EqualTo(40));
                var units = roots.SelectMany(r => r.GetComponentsInChildren<BattleUnit>()).ToArray();
                var players = units.Where(u => u.Team == UnitTeam.Player).ToArray();
                var enemies = units.Where(u => u.Team == UnitTeam.Enemy).ToArray();
                Assert.That(players.Length, Is.EqualTo(3));
                Assert.That(enemies.Length, Is.EqualTo(3));
                foreach (var unit in units)
                {
                    Assert.That(unit.IsAlive, Is.True);
                    Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(5));
                    grid.TryGetCell(unit.CurrentGridPosition, out var cell);
                    Assert.That(cell.OccupyingUnit, Is.SameAs(unit));
                    Assert.That(cell.Walkable, Is.True);
                }
                var obstacles = roots.SelectMany(r => r.GetComponentsInChildren<GridObstacle>()).ToArray();
                Assert.That(obstacles.Count(o => o.BlocksShots), Is.GreaterThanOrEqualTo(3));
                Assert.That(obstacles.Count(o => !o.BlocksShots), Is.GreaterThanOrEqualTo(3));
                var covers = roots.SelectMany(r => r.GetComponentsInChildren<CoverObject>()).ToArray();
                Assert.That(covers.Count(c => c.Type == CoverType.Half), Is.GreaterThanOrEqualTo(3));
                Assert.That(covers.Count(c => c.Type == CoverType.Full), Is.GreaterThanOrEqualTo(3));
                foreach (var player in players)
                    foreach (var enemy in enemies)
                        Assert.That(Pathfinder.FindPathToRange(grid, player, enemy.CurrentGridPosition, 5),
                            Is.Not.Empty, "Terrain must leave a route to a firing position.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
        }
    }
}
