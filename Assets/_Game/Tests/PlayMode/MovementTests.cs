using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class MovementTests
    {
        private GameObject root;
        private GridManager grid;
        private BattleUnit unit;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            root = new GameObject("Movement Tests");
            grid = root.AddComponent<GridManager>();
            grid.Generate();
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.transform.SetParent(root.transform);
            unit = capsule.AddTestBattleUnit();
            unit.Configure(Vector2Int.zero, 5);
            Assert.That(unit.Initialize(grid), Is.True);
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(root);
            yield return null;
        }
        private IEnumerator FinishMove()
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (unit.Movement.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(unit.Movement.IsMoving, Is.False, "Movement timed out.");
        }
        [UnityTest]
        public IEnumerator CancelMoveRestoresLastCompletedCellWithoutRefund()
        {
            Assert.That(grid.TryMoveUnit(unit, new Vector2Int(1, 1)), Is.True);
            unit.transform.position = new Vector3(3, 1, 3);
            Assert.That(unit.Movement.TryMove(new Vector2Int(5, 1)), Is.True);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (unit.CurrentGridPosition == new Vector2Int(1, 1) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(unit.CurrentGridPosition, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(unit.transform.position.x, Is.GreaterThan(5f), "Cancel during a partial next cell.");

            unit.Movement.CancelMove();

            Assert.That(unit.Movement.IsMoving, Is.False);
            Assert.That(unit.transform.position, Is.EqualTo(new Vector3(5, 1, 3)));
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(unit.Mobility.MovedCells, Is.EqualTo(1));
            Assert.That(unit.MovementEvasion, Is.EqualTo(5));
            grid.TryGetCell(new Vector2Int(2, 1), out var arrived);
            grid.TryGetCell(new Vector2Int(3, 1), out var next);
            Assert.That(arrived.OccupyingUnit, Is.SameAs(unit));
            Assert.That(next.Occupied, Is.False);
            yield return new WaitForSeconds(0.5f);
            Assert.That(unit.CurrentGridPosition, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(unit.Movement.TryMove(new Vector2Int(3, 1)), Is.True);
            yield return FinishMove();
            Assert.That(unit.CurrentGridPosition, Is.EqualTo(new Vector2Int(3, 1)));
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(3));
        }
        [UnityTest]
        public IEnumerator CancelBeforeArrivalSpendsNoAP()
        {
            Assert.That(unit.Movement.TryMove(new Vector2Int(4, 0)), Is.True);
            yield return null;
            unit.Movement.CancelMove();
            unit.Movement.CancelMove();
            Assert.That(unit.Movement.IsMoving, Is.False);
            Assert.That(unit.CurrentGridPosition, Is.EqualTo(Vector2Int.zero));
            Assert.That(unit.transform.position, Is.EqualTo(new Vector3(1, 1, 1)));
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(unit.Mobility.MovedCells, Is.Zero);
        }
        [UnityTest]
        public IEnumerator TwoMovesShareOneBudget()
        {
            Assert.That(unit.Movement.TryMove(new Vector2Int(2, 0)), Is.True);
            yield return FinishMove();
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(3));
            Assert.That(unit.Movement.TryMove(new Vector2Int(5, 0)), Is.True);
            yield return FinishMove();
            Assert.That(unit.ActionPoints.CurrentPoints, Is.Zero);
            Assert.That(unit.Movement.TryMove(new Vector2Int(6, 0)), Is.False);
            Assert.That(unit.CurrentGridPosition, Is.EqualTo(new Vector2Int(5, 0)));
        }
        [UnityTest]
        public IEnumerator BlockedMoveChargesOnlyArrivedCells()
        {
            unit.Movement.TryMove(new Vector2Int(4, 0));
            float deadline = Time.realtimeSinceStartup + 3f;
            while (unit.CurrentGridPosition == Vector2Int.zero && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(unit.CurrentGridPosition, Is.EqualTo(Vector2Int.right));
            grid.SetWalkable(new Vector2Int(2, 0), false);
            yield return FinishMove();
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(4));
        }
        [UnityTest]
        public IEnumerator DisabledMoveDoesNotChargePartialCell()
        {
            unit.Movement.TryMove(new Vector2Int(4, 0));
            yield return null;
            unit.Movement.enabled = false;
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator MovesAlongPathWithoutTeleport()
        {
            var destination = new Vector2Int(4, 0);
            Assert.That(unit.Movement.TryMove(destination), Is.True);
            Assert.That(unit.transform.position.x, Is.LessThan(9f));
            yield return FinishMove();
            Assert.That(unit.transform.position, Is.EqualTo(new Vector3(9, 1, 1)));
        }
        [UnityTest]
        public IEnumerator BlocksSecondOrder()
        {
            unit.Movement.TryMove(new Vector2Int(4, 0));
            Assert.That(unit.Movement.TryMove(Vector2Int.up), Is.False);
            yield return FinishMove();
            Assert.That(unit.CurrentGridPosition, Is.EqualTo(new Vector2Int(4, 0)));
        }
        [UnityTest]
        public IEnumerator ArrivalUpdatesOccupancyAndEvasion()
        {
            unit.Movement.TryMove(new Vector2Int(4, 0));
            yield return FinishMove();
            grid.TryGetCell(Vector2Int.zero, out var oldCell);
            grid.TryGetCell(new Vector2Int(4, 0), out var newCell);
            Assert.That(oldCell.Occupied, Is.False);
            Assert.That(newCell.OccupyingUnit, Is.SameAs(unit));
            Assert.That(unit.Mobility.MovedCells, Is.EqualTo(4));
            Assert.That(unit.MovementEvasion, Is.EqualTo(10));
        }
        [UnityTest]
        public IEnumerator NextMoveResetsDistance()
        {
            unit.Movement.TryMove(new Vector2Int(4, 0));
            yield return FinishMove();
            unit.Movement.TryMove(new Vector2Int(5, 0));
            Assert.That(unit.Mobility.MovedCells, Is.Zero);
            yield return FinishMove();
            Assert.That(unit.Mobility.MovedCells, Is.EqualTo(1));
            Assert.That(unit.MovementEvasion, Is.EqualTo(5));
            Assert.That(unit.Movement.TryMove(new Vector2Int(-1, 0)), Is.False);
            Assert.That(unit.MovementEvasion, Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator BlockedNextCellStopsAtLastArrival()
        {
            unit.Movement.TryMove(new Vector2Int(4, 0));
            float deadline = Time.realtimeSinceStartup + 3f;
            while (unit.CurrentGridPosition == Vector2Int.zero && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(unit.CurrentGridPosition, Is.EqualTo(Vector2Int.right));
            grid.SetWalkable(new Vector2Int(2, 0), false);
            yield return FinishMove();
            Assert.That(unit.CurrentGridPosition, Is.EqualTo(Vector2Int.right));
            Assert.That(unit.transform.position, Is.EqualTo(new Vector3(3, 1, 1)));
            Assert.That(unit.Mobility.MovedCells, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator DisableDuringMoveRestoresLastCell()
        {
            unit.Movement.TryMove(new Vector2Int(4, 0));
            yield return null;
            unit.Movement.enabled = false;
            Assert.That(unit.Movement.IsMoving, Is.False);
            Assert.That(unit.transform.position, Is.EqualTo(new Vector3(1, 1, 1)));
            Assert.That(unit.Movement.TryMove(Vector2Int.right), Is.False);
        }
        [UnityTest]
        public IEnumerator DestroyedUnitReleasesCell()
        {
            Object.Destroy(unit.gameObject);
            yield return null;
            grid.TryGetCell(Vector2Int.zero, out var cell);
            Assert.That(cell.Occupied, Is.False);
            Assert.That(cell.OccupyingUnit, Is.Null);
        }
    }
}
