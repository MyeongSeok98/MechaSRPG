using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class EnemyAITests
    {
        private EnemyAITestBattle battle;
        private void Create(int playerX = 3, int playerY = 1) => battle = new EnemyAITestBattle(
            new[] { new Vector2Int(playerX, playerY) }, new[] { new Vector2Int(1, 1) });
        private void Building(int x, int z)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(battle.Root.transform);
            cube.transform.position = battle.Grid.GetWorldPosition(new Vector2Int(x, z)) + Vector3.up;
            cube.transform.localScale = new Vector3(2, 4, 2);
            battle.Grid.RegisterObstacle(cube.AddComponent<GridObstacle>().Collider);
        }
        [UnityTearDown] public IEnumerator Cleanup() { battle?.Dispose(); yield return null; }
        [UnityTest]
        public IEnumerator DiagonalEnemyMovesIntoDiamondRangeBeforeAttacking()
        {
            Create(4, 4);
            battle.Turns.TryEndTurn(); yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].CurrentGridPosition, Is.EqualTo(new Vector2Int(1, 2)));
            Assert.That(battle.Enemies[0].Mobility.MovedCells, Is.EqualTo(1));
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(1));
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
        }
        [UnityTest]
        public IEnumerator ImmediateTargetUsesAxisSumDistance()
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(4, 3), new Vector2Int(1, 5) },
                new[] { new Vector2Int(1, 1) });
            battle.Turns.TryEndTurn(); yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(battle.Players[1].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(battle.Enemies[0].Mobility.MovedCells, Is.Zero);
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [UnityTest]
        public IEnumerator OccludedEnemyMovesToClearFiringPositionThenAttacks()
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(4, 1) },
                new[] { new Vector2Int(1, 1), new Vector2Int(4, 4) });
            // A teammate observes the target while this shooter's own line is blocked.
            battle.Enemies[1].Combat.enabled = false;
            Building(2, 1);
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].CurrentGridPosition, Is.EqualTo(new Vector2Int(1, 3)));
            Assert.That(battle.Enemies[0].Mobility.MovedCells, Is.EqualTo(2));
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.Zero);
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
        }
        [UnityTest]
        public IEnumerator BuildingWallWithNoFiringPositionMakesEnemyWait()
        {
            Create();
            for (int z = 0; z < 16; z++) Building(2, z);
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(battle.Enemies[0].Mobility.MovedCells, Is.Zero);
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }
        [UnityTest]
        public IEnumerator BlockedNearestTargetDoesNotHideClearImmediateTarget()
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(3, 1), new Vector2Int(1, 4) },
                new[] { new Vector2Int(1, 1) });
            Building(2, 1);
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(battle.Players[1].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [UnityTest]
        public IEnumerator InRangeEnemyWithoutAttackAPWaitsDespiteDistantTarget()
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(3, 1), new Vector2Int(8, 1) }, new[] { new Vector2Int(1, 1) });
            battle.Enemies[0].ActionPoints.Configure(2);
            battle.Turns.TryEndTurn(); yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].Mobility.MovedCells, Is.Zero);
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(battle.Players.All(p => p.Health.GetCurrentHP(TargetBodyPart.Torso) == 20), Is.True);
        }
        [UnityTest]
        public IEnumerator NearestImmediateTargetWinsAndTiesUseRegistrationOrder()
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(4, 1), new Vector2Int(1, 3), new Vector2Int(3, 1) },
                new[] { new Vector2Int(1, 1) });
            battle.Enemies[0].ActionPoints.Configure(7);
            battle.Turns.TryEndTurn(); yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(battle.Players[1].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(battle.Players[2].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(4), "Do not repeat an attack despite extra AP.");
        }
        [UnityTest]
        public IEnumerator InactiveEnemyIsSkippedAndNextEnemyActs()
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(3, 1) }, new[] { new Vector2Int(1, 1), new Vector2Int(1, 2) });
            battle.Enemies[0].ActionPoints.TrySpend(4);
            battle.Enemies[0].gameObject.SetActive(false);
            battle.Turns.TryEndTurn(); yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(1));
            Assert.That(battle.Enemies[1].ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
        }
        [UnityTest]
        public IEnumerator InRangeEnemyAttacksOnce()
        {
            Create(); battle.Turns.TryEndTurn(); yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(battle.Enemies[0].Mobility.MovedCells, Is.Zero);
        }
        [UnityTest]
        public IEnumerator EnemyMovesTwoCellsThenAttacks()
        {
            Create(8); battle.Turns.TryEndTurn(); yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].Mobility.MovedCells, Is.EqualTo(2));
            Assert.That(battle.Enemies[0].CurrentGridPosition, Is.EqualTo(new Vector2Int(3, 1)));
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.Zero);
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
        }
        [UnityTest]
        public IEnumerator DistantEnemyApproachesWithoutAttacking()
        {
            Create(12); battle.Enemies[0].Vision.Configure(20);
            battle.Turns.TryEndTurn(); yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].Mobility.MovedCells, Is.EqualTo(5));
            Assert.That(battle.Enemies[0].CurrentGridPosition, Is.EqualTo(new Vector2Int(6, 1)));
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.Zero);
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }
        [UnityTest]
        public IEnumerator TrappedEnemyWaits()
        {
            Create(12);
            foreach (var step in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                battle.Grid.SetWalkable(new Vector2Int(1, 1) + step, false);
            battle.Turns.TryEndTurn(); yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(battle.Enemies[0].Mobility.MovedCells, Is.Zero);
        }
        [UnityTest]
        public IEnumerator UnreachableNearestTargetUsesReachablePlayer()
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(4, 1), new Vector2Int(1, 8) }, new[] { new Vector2Int(1, 1) });
            battle.Enemies[0].Combat.ConfigureTestRanged(5, 80, 1, 3);
            for (int z = 0; z < 16; z++) battle.Grid.SetWalkable(new Vector2Int(3, z), false);
            battle.Turns.TryEndTurn(); yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].CurrentGridPosition, Is.EqualTo(new Vector2Int(1, 6)));
            Assert.That(battle.Players.All(p => p.Health.GetCurrentHP(TargetBodyPart.Torso) == 20), Is.True);
        }
        [UnityTest]
        public IEnumerator DetourApproachCanInitiallyMoveAway()
        {
            Create(5);
            battle.Enemies[0].Combat.ConfigureTestRanged(5, 80, 1, 3);
            for (int z = 0; z < 7; z++) battle.Grid.SetWalkable(new Vector2Int(2, z), false);
            battle.Turns.TryEndTurn(); yield return battle.WaitForFirstArrival();
            Assert.That(battle.Enemies[0].CurrentGridPosition, Is.EqualTo(new Vector2Int(1, 2)));
            yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].CurrentGridPosition, Is.EqualTo(new Vector2Int(1, 6)));
        }
        [UnityTest]
        public IEnumerator EnemiesNeverMoveConcurrently()
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(14, 1) }, new[] { new Vector2Int(1, 1), new Vector2Int(2, 1) });
            foreach (var enemy in battle.Enemies) enemy.Vision.Configure(20);
            battle.Turns.TryEndTurn();
            float deadline = Time.realtimeSinceStartup + 6;
            while (battle.ActiveTeam == UnitTeam.Enemy && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(battle.Enemies.Count(e => e.Movement.IsMoving), Is.LessThanOrEqualTo(1));
                if (battle.Enemies[1].Movement.IsMoving) Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.Zero);
                yield return null;
            }
            Assert.That(battle.ActiveTeam, Is.EqualTo(UnitTeam.Player));
            Assert.That(battle.Enemies.All(e => e.Mobility.MovedCells == 5), Is.True);
            Assert.That(battle.Enemies[0].CurrentGridPosition, Is.Not.EqualTo(battle.Enemies[1].CurrentGridPosition));
        }
        [UnityTest]
        public IEnumerator EnemyDiagonalAttackUsesStrongestPlayerCover()
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(3, 3) }, new[] { new Vector2Int(5, 1) });
            var player = battle.Players[0];
            battle.Grid.TryGetCell(player.CurrentGridPosition, out var cell);
            cell.SetCover(CoverDirection.South, CoverType.Full);
            cell.SetCover(CoverDirection.SouthEast, CoverType.Full);
            cell.SetCover(CoverDirection.East, CoverType.Full);
            for (int i = 0; i < 3; i++) player.Mobility.RecordCellMoved();
            int chance = -1;
            battle.Combat.SetRollSource(() =>
            {
                chance = battle.Combat.GetPreview(battle.Enemies[0], player).HitChance;
                return 40;
            });
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            yield return battle.WaitForPlayerTurn();
            Assert.That(chance, Is.EqualTo(40));
            Assert.That(player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [UnityTest]
        public IEnumerator EnemyAttackUsesPlayerCoverButIgnoresMovement()
        {
            Create(); var player = battle.Players[0];
            for (int i = 0; i < 3; i++) player.Mobility.RecordCellMoved();
            battle.Grid.TryGetCell(player.CurrentGridPosition, out var cell);
            cell.SetCover(CoverDirection.West, CoverType.Half);
            int chance = -1;
            battle.Combat.SetRollSource(() => { chance = battle.Combat.GetPreview(battle.Enemies[0], player).HitChance; return 52; });
            battle.Turns.TryEndTurn(); yield return battle.WaitForPlayerTurn();
            Assert.That(chance, Is.EqualTo(60));
            Assert.That(player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [UnityTest]
        public IEnumerator EnemyMissStillSpendsAttackAP()
        {
            Create();
            for (int i = 0; i < 3; i++) battle.Players[0].Mobility.RecordCellMoved();
            battle.Grid.TryGetCell(battle.Players[0].CurrentGridPosition, out var cell);
            cell.SetCover(CoverDirection.West, CoverType.Half);
            battle.Combat.SetRollSource(() => 60);
            battle.Turns.TryEndTurn(); yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [UnityTest]
        public IEnumerator AIUsesConfiguredAttackCost()
        {
            Create(9); battle.Enemies[0].Combat.ConfigureTestRanged(5, 80, 5, 2);
            battle.Turns.TryEndTurn(); yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].Mobility.MovedCells, Is.EqualTo(3));
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.Zero);
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
        }
        [UnityTest]
        public IEnumerator RemovingTargetDuringMoveSkipsInvalidAttack()
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(8, 1), new Vector2Int(14, 14) }, new[] { new Vector2Int(1, 1) });
            battle.Turns.TryEndTurn(); yield return battle.WaitForFirstArrival();
            Object.Destroy(battle.Players[0].gameObject); yield return null;
            yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(battle.Enemies[0].Movement.IsMoving, Is.False);
            Assert.That(battle.Players[1].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }
        [UnityTest]
        public IEnumerator DisabledActionPointsCancelsMoveWithoutFurtherSpending()
        {
            Create(8); battle.Turns.TryEndTurn(); yield return battle.WaitForFirstArrival();
            battle.Enemies[0].ActionPoints.enabled = false;
            yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(battle.Enemies[0].Movement.IsMoving, Is.False);
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }
        [UnityTest]
        public IEnumerator DefeatStopsRemainingEnemiesAndTurnRefill()
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(3, 1) }, new[] { new Vector2Int(1, 1), new Vector2Int(1, 2) });
            var health = battle.Players[0].Health;
            health.ApplyDamage(TargetBodyPart.Torso, 15); battle.Players[0].ActionPoints.TrySpend(3);
            battle.Turns.TryEndTurn(); yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Turns.IsBattleOver, Is.True);
            Assert.That(battle.Turns.IsPlayerVictory, Is.False);
            Assert.That(battle.Turns.TurnNumber, Is.EqualTo(1));
            Assert.That(battle.Enemies[1].ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator VictoryStopsEnemyPhase()
        {
            Create(8); battle.Players[0].ActionPoints.TrySpend(3);
            battle.Turns.TryEndTurn(); yield return battle.WaitForFirstArrival();
            battle.Enemies[0].Health.ApplyDamage(TargetBodyPart.Torso, 20); yield return null;
            Assert.That(battle.Turns.IsBattleOver, Is.True);
            Assert.That(battle.Turns.IsPlayerVictory, Is.True);
            Assert.That(battle.Turns.TurnNumber, Is.EqualTo(1));
            Assert.That(battle.Players[0].ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(battle.ActiveEnemy, Is.Null);
        }
        [UnityTest]
        public IEnumerator DisabledAICancelsActiveMoveWithoutRefund()
        {
            Create(8); battle.Turns.TryEndTurn(); yield return battle.WaitForFirstArrival();
            battle.AI.enabled = false;
            Assert.That(battle.Enemies[0].Movement.IsMoving, Is.False);
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(battle.Enemies[0].transform.position, Is.EqualTo(new Vector3(5, 1, 3)));
            yield return new WaitForSeconds(0.1f);
            Assert.That(battle.ActiveTeam, Is.EqualTo(UnitTeam.Enemy));
            Assert.That(battle.Turns.TurnNumber, Is.EqualTo(1));
            battle.AI.enabled = true; yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }
        [UnityTest]
        public IEnumerator ReenabledTurnResumesNextEnemyWithoutRepeating()
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(8, 1) }, new[] { new Vector2Int(1, 1), new Vector2Int(7, 1) });
            battle.Turns.TryEndTurn(); yield return battle.WaitForFirstArrival();
            battle.Turns.enabled = false;
            Assert.That(battle.Enemies[0].Movement.IsMoving, Is.False);
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(4));
            yield return new WaitForSeconds(0.1f);
            Assert.That(battle.Enemies[1].ActionPoints.CurrentPoints, Is.EqualTo(5));
            battle.Turns.enabled = true; yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(battle.Enemies[1].ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
        }
    }
}
