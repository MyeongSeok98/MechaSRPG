using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class VisibilityTests
    {
        private GameObject root;
        private GridManager grid;
        private VisibilitySystem visibility;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Visibility test");
            grid = root.AddComponent<GridManager>();
            grid.Configure(24, 24, 2);
            grid.Generate();
            visibility = root.AddComponent<VisibilitySystem>();
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        private BattleUnit Unit(int x, int z, UnitTeam team, int? range = null)
        {
            var unit = new GameObject(team.ToString()).AddTestBattleUnit();
            unit.transform.SetParent(root.transform);
            unit.Configure(new Vector2Int(x, z), 5);
            unit.ConfigureTeam(team);
            Assert.That(unit.Initialize(grid), Is.True);
            if (range.HasValue) unit.Vision.Configure(range.Value);
            return unit;
        }

        private void Obstacle(int x, int z, bool blocksShots = true)
        {
            var obj = new GameObject("Terrain"); obj.transform.SetParent(root.transform);
            obj.transform.position = grid.GetWorldPosition(new Vector2Int(x, z));
            var terrain = obj.AddComponent<GridObstacle>();
            terrain.Collider.size = Vector3.one;
            var settings = new SerializedObject(terrain);
            settings.FindProperty("blocksShots").boolValue = blocksShots;
            settings.ApplyModifiedPropertiesWithoutUndo();
            grid.RegisterObstacle(terrain.Collider);
        }

        [Test]
        public void StartupUsesInclusiveCircleAndBuildingLine()
        {
            var observer = Unit(1, 1, UnitTeam.Player);
            var boundary = Unit(7, 9, UnitTeam.Enemy);
            var outside = Unit(9, 9, UnitTeam.Enemy);
            var occluded = Unit(5, 1, UnitTeam.Enemy);
            Obstacle(3, 1);
            visibility.Initialize(grid, new[] { observer, boundary, outside, occluded });
            Assert.That(visibility.IsReady, Is.True);
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, boundary), Is.True);
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, outside), Is.False);
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, occluded), Is.False);
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, observer), Is.True);
        }

        [Test]
        public void SharedDetectionEndsOnlyAfterLastObserverLosesSight()
        {
            var first = Unit(1, 1, UnitTeam.Player, 3);
            var second = Unit(5, 1, UnitTeam.Player, 3);
            var target = Unit(3, 1, UnitTeam.Enemy);
            visibility.Initialize(grid, new[] { first, second, target });
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, target), Is.True);
            Assert.That(grid.TryMoveUnit(first, new Vector2Int(1, 8)), Is.True);
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, target), Is.True);
            Assert.That(grid.TryMoveUnit(second, new Vector2Int(5, 8)), Is.True);
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, target), Is.False);
            Assert.That(grid.TryMoveUnit(first, new Vector2Int(1, 1)), Is.True);
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, target), Is.True);
        }

        [TestCase("Critical")]
        [TestCase("Disabled")]
        [TestCase("Inactive")]
        [TestCase("Removed")]
        [TestCase("Ejected")]
        [TestCase("Destroyed")]
        public void CriticalDisabledRemovedObserversCannotRevealTargets(string change)
        {
            var observer = Unit(1, 1, UnitTeam.Player);
            var target = Unit(3, 1, UnitTeam.Enemy);
            visibility.Initialize(grid, new[] { observer, target });
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, target), Is.True);
            switch (change)
            {
                case "Critical": MakeCritical(observer); break;
                case "Disabled": observer.enabled = false; break;
                case "Inactive": observer.gameObject.SetActive(false); break;
                case "Removed": Object.DestroyImmediate(observer.gameObject); break;
                case "Ejected": observer.Health.MarkEjected(); break;
                case "Destroyed": observer.Health.ApplyDamage(TargetBodyPart.Torso, 20); break;
            }
            // EditMode exercises the calculation; lifecycle callbacks are tested in PlayMode.
            visibility.Refresh();
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, target), Is.False);
        }

        private void MakeCritical(BattleUnit unit)
        {
            unit.Health.ConfigureDestructionChance(0);
            unit.Health.ApplyDamage(TargetBodyPart.Head, 6);
            unit.Health.ApplyDamage(TargetBodyPart.LeftArm, 8);
            unit.Health.ApplyDamage(TargetBodyPart.RightArm, 8);
            Assert.That(unit.Health.State, Is.EqualTo(MechState.Critical));
        }

        [Test]
        public void CriticalTargetRemainsVisibleButEjectedTargetDoesNot()
        {
            var observer = Unit(1, 1, UnitTeam.Player);
            var target = Unit(3, 1, UnitTeam.Enemy);
            visibility.Initialize(grid, new[] { observer, target });
            MakeCritical(target);
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, target), Is.True);
            target.Health.MarkEjected();
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, target), Is.False);
        }

        [Test]
        public void CoverRavinesAndUnitsDoNotBlockSight()
        {
            var observer = Unit(1, 1, UnitTeam.Player);
            var between = Unit(4, 1, UnitTeam.Player, 0);
            var target = Unit(6, 1, UnitTeam.Enemy);
            Obstacle(3, 1, false);
            grid.TryGetCell(new Vector2Int(2, 1), out var half);
            grid.TryGetCell(new Vector2Int(5, 1), out var full);
            half.SetCover(CoverDirection.West, CoverType.Half);
            full.SetCover(CoverDirection.West, CoverType.Full);
            visibility.Initialize(grid, new[] { observer, between, target });
            Assert.That(grid.HasLineOfSight(observer.CurrentGridPosition, target.CurrentGridPosition), Is.True);
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, target), Is.True);
        }

        [TestCase(2, 1)] [TestCase(1, 2)]
        public void CornerBuildingsBlockSightLikeShots(int x, int z)
        {
            Obstacle(x, z);
            var from = new Vector2Int(1, 1); var to = new Vector2Int(3, 3);
            Assert.That(grid.HasLineOfSight(from, to), Is.False);
            Assert.That(grid.HasLineOfSight(to, from), Is.False);
            Assert.That(grid.HasClearShot(from, to), Is.False);
        }

        [Test]
        public void SightAndShotsUseIndependentBlockingProperties()
        {
            grid.TryGetCell(new Vector2Int(2, 1), out var cell);
            typeof(GridCell).GetProperty("BlocksVision").SetValue(cell, true);
            Assert.That(grid.HasLineOfSight(new Vector2Int(1, 1), new Vector2Int(3, 1)), Is.False);
            Assert.That(grid.HasClearShot(new Vector2Int(1, 1), new Vector2Int(3, 1)), Is.True);
        }

        [Test]
        public void TeamsUseTheirOwnObserversAndConfiguredRange()
        {
            var player = Unit(1, 1, UnitTeam.Player, 2);
            var enemy = Unit(5, 1, UnitTeam.Enemy, 5);
            visibility.Initialize(grid, new[] { player, enemy });
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.False);
            Assert.That(visibility.IsVisibleTo(UnitTeam.Enemy, player), Is.True);
            player.Vision.Configure(4);
            visibility.Refresh();
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.True);
            player.Vision.Configure(-1);
            visibility.Refresh();
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.False);
        }

        [Test]
        public void ReinitializationUsesOnlyNewRegisteredUnits()
        {
            var player = Unit(1, 1, UnitTeam.Player);
            var enemy = Unit(3, 1, UnitTeam.Enemy);
            visibility.Initialize(grid, new[] { player, enemy, player });
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.True);
            visibility.Initialize(grid, new[] { enemy });
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.False);
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, player), Is.False);
        }

        [Test]
        public void UnregisteredAndForeignGridUnitsDoNotParticipate()
        {
            var player = Unit(1, 1, UnitTeam.Player);
            var enemy = Unit(3, 1, UnitTeam.Enemy);
            var unregistered = Unit(1, 2, UnitTeam.Enemy);
            visibility.Initialize(grid, new[] { player, enemy });
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, unregistered), Is.False);
            var otherGrid = new GameObject("Other grid").AddComponent<GridManager>();
            otherGrid.transform.SetParent(root.transform); otherGrid.Generate();
            var foreign = new GameObject("Foreign observer").AddTestBattleUnit();
            foreign.transform.SetParent(root.transform);
            foreign.Configure(new Vector2Int(1, 3), 5); foreign.Initialize(otherGrid);
            visibility.Initialize(grid, new[] { foreign, enemy });
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.False);
        }
    }
}
