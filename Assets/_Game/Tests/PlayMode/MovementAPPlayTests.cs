using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class MovementAPPlayTests
    {
        private MechaAccuracyTestBattle b;
        private void Create(float player = 2, float enemy = 1) => b = new MechaAccuracyTestBattle(
            configureMechaData: (p,e) => {
                JsonUtility.FromJsonOverwrite("{\"movementCellsPerAP\":" + player.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}", p);
                JsonUtility.FromJsonOverwrite("{\"movementCellsPerAP\":" + enemy.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}", e);
            });
        [UnityTearDown] public IEnumerator Cleanup() { b?.Dispose(); yield return null; }
        private IEnumerator Finish(BattleUnit unit)
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (unit.Movement.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(unit.Movement.IsMoving, Is.False);
            if (unit.HasPendingFacing) Assert.That(unit.Movement.TryKeepArrivalFacing(), Is.True);
        }
        [UnityTest] public IEnumerator MoveShootMoveKeepsPaidCreditAcrossCommands()
        {
            Create(); Assert.That(b.Player.Movement.TryMove(new Vector2Int(1, 2)), Is.True); yield return Finish(b.Player);
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.RightHand).WasExecuted, Is.True);
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(1));
            Assert.That(b.Player.Movement.TryMove(new Vector2Int(1, 3)), Is.True); yield return Finish(b.Player);
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(1));
            Assert.That(b.Player.Movement.TryMove(new Vector2Int(1, 5)), Is.True); yield return Finish(b.Player);
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.Zero);
            Assert.That(b.Player.Movement.TryMove(new Vector2Int(1, 6)), Is.False);
        }
        [UnityTest] public IEnumerator ZeroAPStillEnablesMoveForPrepaidCells()
        {
            Create(3); Assert.That(b.Player.Movement.TryMove(new Vector2Int(1, 2)), Is.True); yield return Finish(b.Player);
            Assert.That(b.Player.ActionPoints.TrySpend(4), Is.True);
            Assert.That(b.Player.AvailableMovementRange, Is.EqualTo(2));
            var visual = b.Root.AddComponent<GridVisualizer>(); visual.Initialize(b.Grid, b.Turns);
            var debug = b.Root.AddComponent<BattleDebugUI>(); debug.Initialize(b.Grid, b.Root.AddComponent<CoverSystem>());
            var camera = new GameObject("Movement AP camera").AddComponent<Camera>(); camera.transform.SetParent(b.Root.transform);
            var input = b.Root.AddComponent<PlayerInputController>(); input.Initialize(camera, b.Grid, visual, debug); input.InitializeCombat(b.Combat, b.Turns);
            Assert.That(input.SelectUnit(b.Player), Is.True); Assert.That(input.CanBeginMove, Is.True);
            Assert.That(input.BeginMove(), Is.True); Assert.That(input.TryMoveSelected(new Vector2Int(1, 4)), Is.True);
            yield return Finish(b.Player);
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.Zero); Assert.That(b.Player.AvailableMovementRange, Is.Zero);
        }
        [UnityTest] public IEnumerator FractionalEfficiencyDoesNotPenalizeSplitMoves()
        {
            Create(1.5f);
            for (int z = 2; z <= 4; z++)
            { Assert.That(b.Player.Movement.TryMove(new Vector2Int(1, z)), Is.True); yield return Finish(b.Player);
                Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(z == 2 ? 4 : 3)); }
        }
        [UnityTest] public IEnumerator CancelAndReinitializeDoNotLoseOrRefillCredit()
        {
            Create(); Assert.That(b.Player.Movement.TryMove(new Vector2Int(1, 5)), Is.True);
            float deadline = Time.realtimeSinceStartup + 3;
            while (b.Player.CurrentGridPosition == new Vector2Int(1, 1) && Time.realtimeSinceStartup < deadline) yield return null;
            b.Player.Movement.CancelMove(); Assert.That(b.Player.CurrentGridPosition, Is.EqualTo(new Vector2Int(1, 2)));
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(b.Player.Initialize(b.Grid), Is.True); b.Player.Movement.enabled = false; b.Player.Movement.enabled = true;
            Assert.That(b.Player.Movement.TryMove(new Vector2Int(1, 3)), Is.True); yield return Finish(b.Player);
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
        }
        [UnityTest] public IEnumerator OnlyOwnNewTurnClearsPrepaidMovement()
        {
            Create(); Assert.That(b.Player.Movement.TryMove(new Vector2Int(1, 2)), Is.True); yield return Finish(b.Player);
            b.StartEnemyTeam(); Assert.That(b.Player.AvailableMovementRange, Is.EqualTo(9));
            typeof(TurnManager).GetMethod("StartTeamTurn", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(b.Turns, new object[] { UnitTeam.Player });
            Assert.That(b.Player.AvailableMovementRange, Is.EqualTo(10));
            Assert.That(b.Player.Movement.TryMove(new Vector2Int(1, 3)), Is.True); yield return Finish(b.Player);
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
        }
        [UnityTest] public IEnumerator OneLegDestroyedDuringMoveChangesFutureCostOnly() => LegDamage(false);
        [UnityTest] public IEnumerator BothLegsDestroyedStopRouteEvenWithPrepaidCredit() => LegDamage(true);
        private IEnumerator LegDamage(bool both)
        {
            Create(); bool damaged = false;
            b.Player.VisibilityChanged += () => {
                if (damaged || b.Player.CurrentGridPosition != new Vector2Int(1, 2)) return;
                damaged = true;
                b.Player.Health.ApplyDamage(TargetBodyPart.LeftLeg, b.Player.Health.GetCurrentHP(TargetBodyPart.LeftLeg));
                if (both) b.Player.Health.ApplyDamage(TargetBodyPart.RightLeg, b.Player.Health.GetCurrentHP(TargetBodyPart.RightLeg));
            };
            Assert.That(b.Player.Movement.TryMove(new Vector2Int(1, 5)), Is.True); yield return Finish(b.Player);
            Assert.That(b.Player.CurrentGridPosition, Is.EqualTo(new Vector2Int(1, both ? 2 : 5)));
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(both ? 4 : 1));
            if (both) Assert.That(b.Player.AvailableMovementRange, Is.Zero);
        }
        [UnityTest] public IEnumerator FastEnemyReservesActualMovementAPThenShoots()
        {
            Create(enemy: 2); Assert.That(b.Grid.TryMoveUnit(b.Player, new Vector2Int(7, 1)), Is.True);
            b.Player.transform.position = b.Grid.GetWorldPosition(b.Player.CurrentGridPosition) + Vector3.up * b.Player.HeightOffset;
            b.Enemy.Combat.ConfigureTestRanged(5, 80, 1, 3); b.Enemy.Combat.ConfigureTestMelee(0, 0, 0, 3);
            var ai = b.Root.AddComponent<EnemyAIController>(); ai.Initialize(b.Grid, b.Combat, b.Turns); b.Turns.ConfigureEnemyAI(ai);
            Assert.That(b.Turns.TryEndTurn(), Is.True);
            float deadline = Time.realtimeSinceStartup + 6;
            while (!b.Turns.IsPlayerTurn && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(b.Turns.IsPlayerTurn, Is.True); Assert.That(b.Enemy.CurrentGridPosition, Is.EqualTo(new Vector2Int(6, 1)));
            Assert.That(b.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(b.Enemy.ActionPoints.CurrentPoints, Is.Zero);
        }
        [UnityTest] public IEnumerator DisabledUnitResetsBudgetAtItsOwnTurn() => InactiveTurn(false);
        [UnityTest] public IEnumerator InactiveGameObjectResetsBudgetAtItsOwnTurn() => InactiveTurn(true);
        private IEnumerator InactiveTurn(bool gameObject)
        {
            Create(); Assert.That(b.Player.Movement.TryMove(new Vector2Int(1, 2)), Is.True); yield return Finish(b.Player);
            Assert.That(b.Player.ActionPoints.TrySpend(4), Is.True);
            if (gameObject) b.Player.gameObject.SetActive(false); else b.Player.enabled = false;
            b.StartEnemyTeam(); Assert.That(b.Player.Movement.PrepaidMovementAP, Is.EqualTo(.5));
            typeof(TurnManager).GetMethod("StartTeamTurn", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(b.Turns, new object[] { UnitTeam.Player });
            if (gameObject) b.Player.gameObject.SetActive(true); else b.Player.enabled = true;
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(b.Player.Movement.PrepaidMovementAP, Is.Zero);
            Assert.That(b.Player.ActionPoints.TrySpend(5), Is.True);
            Assert.That(b.Player.AvailableMovementRange, Is.Zero);
        }
        [UnityTest] public IEnumerator GridShrinkingDuringAnimationDoesNotChargeRejectedStep() => ChangedGrid(false);
        [UnityTest] public IEnumerator RegeneratedGridDuringAnimationDoesNotChargeStaleStep() => ChangedGrid(true);
        private IEnumerator ChangedGrid(bool regenerate)
        {
            Create();
            var visual = new GameObject("Visual").transform; visual.SetParent(b.Player.transform, false);
            visual.rotation = Quaternion.Euler(0, 180, 0); b.Player.Movement.Initialize(b.Grid, b.Player);
            Assert.That(b.Player.Movement.TryMove(new Vector2Int(1, 2)), Is.True);
            Assert.That(b.Player.CurrentGridPosition, Is.EqualTo(new Vector2Int(1, 1)));
            if (regenerate)
            {
                b.Grid.Generate(); b.Grid.SetWalkable(new Vector2Int(1, 2), false);
                Assert.That(b.Grid.TryPlaceUnit(b.Player, new Vector2Int(1, 1)), Is.True);
                Assert.That(b.Grid.TryPlaceUnit(b.Enemy, new Vector2Int(3, 1)), Is.True);
            }
            else b.Grid.Configure(12, 2, 2);
            yield return Finish(b.Player);
            Assert.That(b.Player.CurrentGridPosition, Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(b.Player.Movement.PaidMovementAP, Is.Zero);
        }
    }
}
