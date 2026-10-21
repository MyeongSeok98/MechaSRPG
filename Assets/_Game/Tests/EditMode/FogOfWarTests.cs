using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class FogOfWarTests
    {
        [Test] public void FogShaderIsIncludedAsRuntimeResource()
        {
            var shader = Resources.Load<Shader>("Shaders/FogTerrain");
            Assert.That(shader, Is.Not.Null, "The runtime fog shader must be included in player builds through Resources.");
            Assert.That(shader.name, Is.EqualTo("MechaXCOM/FogTerrain"));
        }

        private GameObject root;
        private GridManager grid;
        private VisibilitySystem visibility;
        private BattleUnit observer;
        [SetUp] public void Setup()
        {
            root = new GameObject("Fog tests");
            grid = root.AddComponent<GridManager>(); grid.Configure(16, 12, 2); grid.Generate();
            observer = Unit(new Vector2Int(1, 1), UnitTeam.Player);
            observer.Vision.Configure(2);
            visibility = root.AddComponent<VisibilitySystem>(); visibility.Initialize(grid, new[] { observer });
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);
        private BattleUnit Unit(Vector2Int pos, UnitTeam team)
        {
            var unit = new GameObject(team.ToString()).AddTestBattleUnit(); unit.transform.SetParent(root.transform);
            unit.Configure(pos, 5); unit.ConfigureTeam(team); Assert.That(unit.Initialize(grid), Is.True); return unit;
        }
        [Test] public void UnexploredVisibleExploredAndRediscoveredCells()
        {
            var cell = new Vector2Int(8, 1);
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, cell), Is.EqualTo(CellVisibility.Unexplored));
            grid.TryMoveUnit(observer, new Vector2Int(7, 1));
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, cell), Is.EqualTo(CellVisibility.Visible));
            grid.TryMoveUnit(observer, new Vector2Int(1, 1));
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, cell), Is.EqualTo(CellVisibility.Explored));
            grid.TryMoveUnit(observer, new Vector2Int(7, 1));
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, cell), Is.EqualTo(CellVisibility.Visible));
        }
        [Test] public void NewBattleClearsExplorationButRefreshDoesNot()
        {
            var cell = new Vector2Int(2, 1);
            grid.TryMoveUnit(observer, new Vector2Int(9, 1)); visibility.Refresh();
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, cell), Is.EqualTo(CellVisibility.Explored));
            visibility.enabled = false; visibility.Refresh(); visibility.enabled = true; visibility.Refresh();
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, cell), Is.EqualTo(CellVisibility.Explored));
            visibility.Initialize(grid, new[] { observer });
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, cell), Is.EqualTo(CellVisibility.Unexplored));
        }
        [Test] public void FirstBlockingBuildingCellIsVisibleButCellsBehindAreNot()
        {
            observer.Vision.Configure(10);
            var building = new GameObject("Building"); building.transform.SetParent(root.transform);
            building.transform.position = grid.GetWorldPosition(new Vector2Int(3, 1));
            grid.RegisterObstacle(building.AddComponent<GridObstacle>().Collider);
            visibility.Initialize(grid, new[] { observer });
            Assert.That(grid.HasLineOfSightToCell(new Vector2Int(1, 1), new Vector2Int(3, 1)), Is.True);
            Assert.That(grid.HasLineOfSight(new Vector2Int(1, 1), new Vector2Int(3, 1)), Is.False);
            Assert.That(grid.HasClearShot(new Vector2Int(1, 1), new Vector2Int(3, 1)), Is.False);
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, new Vector2Int(3, 1)), Is.EqualTo(CellVisibility.Visible));
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, new Vector2Int(4, 1)), Is.EqualTo(CellVisibility.Unexplored));
            Assert.That(grid.HasLineOfSightToCell(new Vector2Int(1, 1), new Vector2Int(4, 1)), Is.False);
        }
        [Test] public void TerrainMemoryNeverRevealsEnemyAndTeamsHaveSeparateMemory()
        {
            var enemy = Unit(new Vector2Int(3, 1), UnitTeam.Enemy); enemy.Vision.Configure(1);
            visibility.Initialize(grid, new[] { observer, enemy });
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.True);
            grid.TryMoveUnit(observer, new Vector2Int(9, 1));
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, enemy.CurrentGridPosition), Is.EqualTo(CellVisibility.Explored));
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.False);
            Assert.That(visibility.GetCellVisibility(UnitTeam.Enemy, new Vector2Int(9, 1)), Is.EqualTo(CellVisibility.Unexplored));
        }
        [Test] public void InclusiveCircleAndInvalidQueries()
        {
            observer.Vision.Configure(5);
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, new Vector2Int(4, 5)), Is.EqualTo(CellVisibility.Visible));
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, new Vector2Int(5, 5)), Is.EqualTo(CellVisibility.Unexplored));
            Assert.That(visibility.GetCellVisibility((UnitTeam)99, Vector2Int.zero), Is.EqualTo(CellVisibility.Unexplored));
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, new Vector2Int(-1, 1)), Is.EqualTo(CellVisibility.Unexplored));
            Assert.That(grid.HasLineOfSightToCell(Vector2Int.zero, new Vector2Int(-1, 1)), Is.False);
        }
    }
}
