using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class MovementFacingTests
    {
        private GameObject root;
        private GridManager grid;
        private BattleUnit unit;
        private Transform visual;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            root = new GameObject("Movement facing test");
            grid = root.AddComponent<GridManager>();
            grid.Generate();
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.transform.SetParent(root.transform);
            visual = new GameObject("Visual").transform;
            visual.SetParent(capsule.transform, false);
            visual.localPosition = Vector3.down;
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
            float deadline = Time.realtimeSinceStartup + 5;
            while (unit.Movement.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(unit.Movement.IsMoving, Is.False, "Movement timed out.");
        }

        [UnityTest]
        public IEnumerator TurnsVisualSmoothlyBeforeAdvancingAndKeepsRootUnrotated()
        {
            var start = unit.transform.position;
            Assert.That(unit.Movement.TryMove(Vector2Int.right), Is.True);
            Assert.That(unit.transform.position, Is.EqualTo(start), "Do not slide sideways while turning.");
            bool sawPartialTurn = false;
            float deadline = Time.realtimeSinceStartup + 5;
            while (unit.Movement.IsMoving && Time.realtimeSinceStartup < deadline)
            {
                float angle = Vector3.Angle(visual.forward, Vector3.right);
                if (angle > .1f)
                {
                    Assert.That(unit.transform.position, Is.EqualTo(start));
                    Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(5));
                    if (angle < 89.9f) sawPartialTurn = true;
                }
                Assert.That(Quaternion.Angle(unit.transform.rotation, Quaternion.identity), Is.LessThan(.01f));
                yield return null;
            }
            Assert.That(unit.Movement.IsMoving, Is.False);
            Assert.That(sawPartialTurn, Is.True, "Rotation must be gradual.");
            Assert.That(Vector3.Angle(visual.forward, Vector3.right), Is.LessThan(.01f));
            Assert.That(unit.CurrentGridPosition, Is.EqualTo(Vector2Int.right));
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(unit.GetComponent<CapsuleCollider>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator TurnsAtPathCornerAndKeepsLastFacingOnArrival()
        {
            grid.SetWalkable(Vector2Int.up, false);
            Assert.That(unit.Movement.TryMove(Vector2Int.one), Is.True);
            float deadline = Time.realtimeSinceStartup + 5;
            while (unit.CurrentGridPosition == Vector2Int.zero && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(unit.CurrentGridPosition, Is.EqualTo(Vector2Int.right));
            var corner = unit.transform.position;
            bool sawCornerTurn = false;
            while (unit.Movement.IsMoving && Time.realtimeSinceStartup < deadline)
            {
                if (Vector3.Angle(visual.forward, Vector3.forward) > .1f)
                {
                    sawCornerTurn = true;
                    Assert.That(unit.transform.position, Is.EqualTo(corner));
                    Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(4));
                }
                yield return null;
            }
            Assert.That(sawCornerTurn, Is.True);
            Assert.That(unit.Movement.IsMoving, Is.False);
            Assert.That(Vector3.Angle(visual.forward, Vector3.forward), Is.LessThan(.01f));
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(3));
            Assert.That(unit.Mobility.MovedCells, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ReturningAlongSamePathTurnsThroughHalfCircle()
        {
            Assert.That(unit.Movement.TryMove(Vector2Int.up), Is.True);
            yield return FinishMove();
            Assert.That(unit.Movement.TryMove(Vector2Int.zero), Is.True);
            yield return FinishMove();
            Assert.That(Vector3.Angle(visual.forward, Vector3.back), Is.LessThan(.01f));
            Assert.That(unit.CurrentGridPosition, Is.EqualTo(Vector2Int.zero));
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator CancellationStopsTurningWithoutChargingAPAndAllowsNextMove()
        {
            var start = unit.transform.position;
            Assert.That(unit.Movement.TryMove(Vector2Int.right), Is.True);
            unit.Movement.CancelMove();
            var cancelledRotation = visual.rotation;
            yield return new WaitForSeconds(.4f);
            Assert.That(unit.Movement.IsMoving, Is.False);
            Assert.That(unit.transform.position, Is.EqualTo(start));
            Assert.That(Quaternion.Angle(visual.rotation, cancelledRotation), Is.LessThan(.01f));
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(unit.Mobility.MovedCells, Is.Zero);
            Assert.That(unit.Movement.TryMove(Vector2Int.up), Is.True);
            yield return FinishMove();
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator BlockedDestinationDuringTurnCancelsWithoutChargingAP()
        {
            var start = unit.transform.position;
            Assert.That(unit.Movement.TryMove(Vector2Int.right), Is.True);
            grid.SetWalkable(Vector2Int.right, false);
            yield return FinishMove();
            Assert.That(unit.transform.position, Is.EqualTo(start));
            Assert.That(unit.CurrentGridPosition, Is.EqualTo(Vector2Int.zero));
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
    }
}
