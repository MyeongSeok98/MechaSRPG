using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class VisibilityAITests
    {
        private EnemyAITestBattle battle;
        private GameObject sceneRoot;
        private VisibilitySystem Create(int playerX = 1, int enemyX = 3)
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(playerX, 1) },
                new[] { new Vector2Int(enemyX, 1) }, 20, 10);
            var visibility = battle.Root.AddComponent<VisibilitySystem>();
            visibility.Initialize(battle.Grid, battle.Players.Concat(battle.Enemies).ToArray());
            battle.Turns.ConfigureVisibility(visibility);
            return visibility;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            battle?.Dispose();
            if (sceneRoot) Object.Destroy(sceneRoot);
            yield return null;
        }
        [Test] public void BattleStartsWithNearbyEnemyAlreadyDetected()
        {
            sceneRoot = new GameObject("Visibility scene");
            var grid = sceneRoot.AddComponent<GridManager>();
            grid.Configure(20, 10, 2);
            var player = new GameObject("Player").AddTestBattleUnit();
            player.transform.SetParent(sceneRoot.transform);
            player.Configure(new Vector2Int(1, 1), 5);
            var enemy = new GameObject("Enemy").AddTestBattleUnit();
            enemy.transform.SetParent(sceneRoot.transform);
            enemy.Configure(new Vector2Int(3, 1), 5); enemy.ConfigureTeam(UnitTeam.Enemy);
            var turns = sceneRoot.AddComponent<TurnManager>();
            var controller = sceneRoot.AddComponent<BattleSceneController>();
            controller.Configure(grid, sceneRoot.AddComponent<CoverSystem>(), new[] { player, enemy }, null, null);
            controller.ConfigureCombat(sceneRoot.AddComponent<CombatSystem>(), turns);
            Assert.That(controller.InitializeBattle(), Is.True);
            Assert.That(turns.Visibility, Is.Not.Null);
            Assert.That(turns.Visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.True);
            Assert.That(controller.InitializeBattle(), Is.True);
            Assert.That(sceneRoot.GetComponents<VisibilitySystem>(), Has.Length.EqualTo(1));
        }
        [UnityTest] public IEnumerator UndetectedEnemyWaitsWithoutSpendingAPAndTurnReturns()
        {
            Create(1, 12);
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(battle.Enemies[0].CurrentGridPosition, Is.EqualTo(new Vector2Int(12, 1)));
            Assert.That(battle.Turns.TurnNumber, Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator BuildingOccludedEnemyWaits()
        {
            var visibility = Create();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(battle.Root.transform);
            cube.transform.position = battle.Grid.GetWorldPosition(new Vector2Int(2, 1)) + Vector3.up;
            cube.transform.localScale = new Vector3(2, 4, 2);
            battle.Grid.RegisterObstacle(cube.AddComponent<GridObstacle>().Collider);
            visibility.Refresh();
            Assert.That(visibility.IsVisibleTo(UnitTeam.Enemy, battle.Players[0]), Is.False);
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(battle.Enemies[0].Mobility.MovedCells, Is.Zero);
        }
        [UnityTest] public IEnumerator DetectedEnemyUsesExistingAttack()
        {
            Create();
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator LostTargetIsNotPursuedNextTurn()
        {
            Create();
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            yield return battle.WaitForPlayerTurn();
            battle.Enemies[0].Vision.Configure(0);
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
        }
        [TestCase(AttackType.Melee)] [TestCase(AttackType.Ranged)]
        public void HiddenTargetCannotBeAttackedDirectly(AttackType type)
        {
            Create(1, 2);
            battle.Players[0].Combat.ConfigureTestMelee(8, 95, 2, 3);
            battle.Players[0].Vision.Configure(0);
            AssertRejected(type);
        }
        [Test] public void DisabledVisibilityDoesNotPermitAttack()
        { Create().enabled = false; AssertRejected(AttackType.Ranged); }
        [Test] public void MissingVisibilityDoesNotPermitAttack()
        { Create(); battle.Turns.ConfigureVisibility(null); AssertRejected(AttackType.Ranged); }
        [Test] public void UninitializedVisibilityDoesNotPermitAttack()
        { Create(); battle.Turns.ConfigureVisibility(battle.Root.AddComponent<VisibilitySystem>()); AssertRejected(AttackType.Ranged); }
        private void AssertRejected(AttackType type)
        {
            int rolls = 0;
            battle.Combat.SetRollSource(() => { rolls++; return 0; });
            Assert.That(battle.Combat.GetPreview(battle.Players[0], battle.Enemies[0], type).CanAttack, Is.False);
            Assert.That(battle.Combat.TryAttack(battle.Players[0], battle.Enemies[0], type).WasExecuted, Is.False);
            Assert.That(battle.Players[0].ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(battle.Enemies[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(rolls, Is.Zero);
        }
        [UnityTest] public IEnumerator TargetLostDuringMoveStopsAtCommittedCell()
        {
            Create(1, 9);
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            yield return battle.WaitForFirstArrival();
            var enemy = battle.Enemies[0];
            var committed = enemy.CurrentGridPosition;
            enemy.Vision.Configure(0);
            yield return battle.WaitForPlayerTurn();
            Assert.That(enemy.Movement.IsMoving, Is.False);
            Assert.That(enemy.CurrentGridPosition, Is.EqualTo(committed));
            Assert.That(enemy.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(enemy.Mobility.MovedCells, Is.EqualTo(1));
            battle.Grid.TryGetCell(committed, out var cell);
            Assert.That(cell.OccupyingUnit, Is.SameAs(enemy));
            Assert.That(enemy.transform.position, Is.EqualTo(cell.WorldPosition + Vector3.up * enemy.HeightOffset));
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            yield return battle.WaitForPlayerTurn();
            Assert.That(enemy.CurrentGridPosition, Is.EqualTo(committed));
            Assert.That(enemy.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [Test] public void SharedSpotterAllowsAttackButDoesNotBypassShooterBuildingOrRange()
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(1, 1), new Vector2Int(3, 4) },
                new[] { new Vector2Int(3, 1) }, 20, 10);
            var shooter = battle.Players[0];
            var target = battle.Enemies[0];
            shooter.Vision.Configure(0);
            Assert.That(battle.Turns.Visibility.IsVisibleTo(UnitTeam.Player, target), Is.True);
            Assert.That(battle.Combat.GetPreview(shooter, target).CanAttack, Is.True);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(battle.Root.transform);
            cube.transform.position = battle.Grid.GetWorldPosition(new Vector2Int(2, 1)) + Vector3.up;
            cube.transform.localScale = new Vector3(2, 4, 2);
            battle.Grid.RegisterObstacle(cube.AddComponent<GridObstacle>().Collider);
            battle.Turns.Visibility.Refresh();
            Assert.That(battle.Turns.Visibility.IsVisibleTo(UnitTeam.Player, target), Is.True);
            Assert.That(battle.Combat.GetPreview(shooter, target).BlockedByBuilding, Is.True);
            Assert.That(battle.Combat.TryAttack(shooter, target).WasExecuted, Is.False);
            Assert.That(battle.Grid.TryMoveUnit(target, new Vector2Int(3, 7)), Is.True);
            Assert.That(battle.Turns.Visibility.IsVisibleTo(UnitTeam.Player, target), Is.True);
            Assert.That(battle.Combat.GetPreview(shooter, target).CanAttack, Is.False);
            Assert.That(shooter.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
    }
}
