using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class InitializationTests
    {
        private GameObject root;
        private GridManager grid;
        private CoverSystem covers;
        private BattleSceneController controller;
        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Initialization Tests");
            grid = root.AddComponent<GridManager>();
            covers = root.AddComponent<CoverSystem>();
            controller = root.AddComponent<BattleSceneController>();
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);
        private BattleUnit Unit(string name)
        {
            var unit = new GameObject(name).AddTestBattleUnit();
            unit.transform.SetParent(root.transform);
            unit.Configure(Vector2Int.zero, 5);
            return unit;
        }
        [Test]
        public void LegacySceneInitializesAIExactlyOnceAndPreservesRegisteredUnits()
        {
            var player = Unit("Authored Player"); player.Configure(new Vector2Int(2, 2), 5);
            var enemy = Unit("Authored Enemy"); enemy.Configure(new Vector2Int(6, 2), 5); enemy.ConfigureTeam(UnitTeam.Enemy);
            var inactive = Unit("Inactive Enemy"); inactive.ConfigureTeam(UnitTeam.Enemy); inactive.gameObject.SetActive(false);
            var turns = new GameObject("Authored Turns").AddComponent<TurnManager>(); turns.transform.SetParent(root.transform);
            var combat = root.AddComponent<CombatSystem>();
            controller.Configure(grid, covers, new[] { player, enemy, inactive }, new GridObstacle[0], new CoverObject[0]);
            controller.ConfigureCombat(combat, turns);
            Assert.That(controller.InitializeBattle(), Is.True);
            player.ActionPoints.TrySpend(3);
            Assert.That(controller.InitializeBattle(), Is.True);
            Assert.That(turns.GetComponents<EnemyAIController>().Length, Is.EqualTo(1));
            Assert.That(turns.GetComponent<EnemyAIController>().IsReady, Is.True);
            Assert.That(root.GetComponents<EnemyAIController>(), Is.Empty, "Use the referenced TurnManager rather than another object.");
            Assert.That(turns.CanAct(player), Is.True);
            Assert.That(turns.CanAct(inactive), Is.False);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(enemy.StartGridPosition, Is.EqualTo(new Vector2Int(6, 2)));
            Assert.That(inactive.gameObject.activeSelf, Is.False);
            grid.TryGetCell(Vector2Int.zero, out var empty);
            Assert.That(empty.Walkable, Is.True);
            LogAssert.NoUnexpectedReceived();
        }
        [Test]
        public void ExistingAIOnReferencedTurnsIsReused()
        {
            var turns = root.AddComponent<TurnManager>();
            var combat = root.AddComponent<CombatSystem>();
            var ai = root.AddComponent<EnemyAIController>();
            controller.Configure(grid, covers, new BattleUnit[0], new GridObstacle[0], new CoverObject[0]);
            controller.ConfigureCombat(combat, turns);
            controller.InitializeBattle();
            Assert.That(ai.IsReady, Is.True);
            Assert.That(root.GetComponents<EnemyAIController>().Length, Is.EqualTo(1));
        }
        [Test]
        public void InitRegistersBeforeUnits()
        {
            var unit = Unit("Blocked Unit");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform);
            cube.transform.position = new Vector3(1, 1, 1);
            cube.transform.localScale = Vector3.one * 2;
            var obstacle = cube.AddComponent<GridObstacle>();
            controller.Configure(grid, covers, new[] { unit }, new[] { obstacle }, new CoverObject[0]);
            LogAssert.Expect(LogType.Warning, new Regex("Blocked Unit.*cannot initialize"));
            controller.InitializeBattle();
            Assert.That(unit.IsInitialized, Is.False);
        }
        [Test]
        public void DuplicateStartWarns()
        {
            var first = Unit("First");
            var second = Unit("Second");
            controller.Configure(grid, covers, new[] { first, second }, new GridObstacle[0], new CoverObject[0]);
            LogAssert.Expect(LogType.Warning, new Regex("Second.*cannot initialize"));
            controller.InitializeBattle();
            Assert.That(first.IsInitialized, Is.True);
            Assert.That(second.IsInitialized, Is.False);
        }
        [Test]
        public void MissingGridDoesNotThrow()
        {
            controller.Configure(null, covers, new BattleUnit[0], new GridObstacle[0], new CoverObject[0]);
            LogAssert.Expect(LogType.Warning, new Regex("missing GridManager or CoverSystem"));
            Assert.That(controller.InitializeBattle(), Is.False);
        }
        [Test]
        public void ValidFixtureNoWarningsAndRepeatedInitPreservesOccupancy()
        {
            var unit = Unit("Valid Unit");
            controller.Configure(grid, covers, new[] { unit }, new GridObstacle[0], new CoverObject[0]);
            Assert.That(controller.InitializeBattle(), Is.True);
            Assert.That(controller.InitializeBattle(), Is.True);
            grid.TryGetCell(Vector2Int.zero, out var cell);
            Assert.That(cell.OccupyingUnit, Is.SameAs(unit));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
