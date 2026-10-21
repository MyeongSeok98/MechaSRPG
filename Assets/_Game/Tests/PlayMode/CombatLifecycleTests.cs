using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class CombatLifecycleTests
    {
        private GameObject root;
        private GridManager grid;
        private TurnManager turns;
        private CombatSystem combat;
        private BattleUnit player, enemy;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            root = new GameObject("Lifecycle"); grid = root.AddComponent<GridManager>(); grid.Generate();
            var cover = root.AddComponent<CoverSystem>();
            player = MakeUnit(Vector2Int.zero, UnitTeam.Player);
            enemy = MakeUnit(new Vector2Int(3, 3), UnitTeam.Enemy);
            turns = root.AddComponent<TurnManager>(); turns.Initialize(new[] { player, enemy });
            var visibility = root.AddComponent<VisibilitySystem>();
            visibility.Initialize(grid, new[] { player, enemy });
            turns.ConfigureVisibility(visibility);
            player.BindTurnManager(turns); enemy.BindTurnManager(turns);
            combat = root.AddComponent<CombatSystem>(); combat.Initialize(grid, cover, turns);
            var ai = root.AddComponent<EnemyAIController>(); ai.Initialize(grid, combat, turns); turns.ConfigureEnemyAI(ai);
            yield return null;
        }
        private BattleUnit MakeUnit(Vector2Int pos, UnitTeam team)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Capsule); obj.transform.SetParent(root.transform);
            var unit = obj.AddTestBattleUnit(); unit.Configure(pos, 5); unit.ConfigureTeam(team);
            Assert.That(unit.Initialize(grid), Is.True); return unit;
        }
        [UnityTearDown] public IEnumerator TearDown() { Object.Destroy(root); yield return null; }
        [UnityTest]
        public IEnumerator DirectEnemyRemovalEndsBattle()
        {
            Object.Destroy(enemy.gameObject); yield return null;
            grid.TryGetCell(new Vector2Int(3, 3), out var cell); Assert.That(cell.Occupied, Is.False);
            Assert.That(turns.IsBattleOver, Is.True); Assert.That(turns.IsPlayerVictory, Is.True);
            Assert.That(turns.TryEndTurn(), Is.False);
            Assert.That(player.Movement.TryMove(Vector2Int.right), Is.False);
        }
        [UnityTest]
        public IEnumerator DirectPlayerRemovalEndsBattle()
        {
            Object.Destroy(player.gameObject); yield return null;
            Assert.That(turns.IsBattleOver, Is.True); Assert.That(turns.IsPlayerVictory, Is.False);
            Assert.That(turns.TryEndTurn(), Is.False);
        }
        [UnityTest]
        public IEnumerator DeathReleasesOccupancyImmediately()
        {
            enemy.Health.ApplyDamage(TargetBodyPart.Torso, 20);
            grid.TryGetCell(new Vector2Int(3, 3), out var cell);
            Assert.That(cell.Occupied, Is.False);
            Assert.That(enemy.GetComponent<Collider>().enabled, Is.False);
            Assert.That(turns.IsBattleOver, Is.True);
            yield return null;
            Assert.That(enemy != null, Is.True);
            Assert.That(enemy.Health.State, Is.EqualTo(MechState.Destroyed));
            Assert.That(enemy.gameObject.activeSelf, Is.False);
        }
        [UnityTest]
        public IEnumerator MovingBlocksAttackAndEndTurn()
        {
            Assert.That(player.Movement.TryMove(new Vector2Int(2, 0)), Is.True);
            Assert.That(combat.TryAttack(player, enemy).WasExecuted, Is.False);
            Assert.That(turns.TryEndTurn(), Is.False);
            yield return null;
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator VictoryLocksMovementAndTurns()
        {
            enemy.Health.ApplyDamage(TargetBodyPart.Torso, 20);
            Assert.That(player.Movement.TryMove(Vector2Int.right), Is.False);
            Assert.That(turns.TryEndTurn(), Is.False);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            yield return null;
        }
    }
}
