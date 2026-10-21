using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    internal static class MechStateTestRules
    {
        public static void MakeCritical(BattleUnit unit)
        {
            unit.Health.SetDestructionRollSource(() => 99);
            unit.Health.ApplyDamage(TargetBodyPart.Head, 6);
            unit.Health.ApplyDamage(TargetBodyPart.LeftArm, 8);
            unit.Health.ApplyDamage(TargetBodyPart.RightArm, 8);
            Assert.That(unit.Health.State, Is.EqualTo(MechState.Critical));
        }
        public static bool TryEject(BattleUnit unit) => unit.TryEject();
        public static bool CanEject(BattleUnit unit) => unit.CanEject;
        public static GameObject AddVisual(BattleUnit unit)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(unit.transform, false);
            return visual;
        }
    }

    public class MechStateTurnTests
    {
        private EnemyAITestBattle battle;
        [UnityTearDown] public IEnumerator Cleanup() { battle?.Dispose(); yield return null; }
        private void Create() => battle = new EnemyAITestBattle(new[] { new Vector2Int(3, 1) }, new[] { new Vector2Int(1, 1) });

        [UnityTest] public IEnumerator BothDestroyedLegsBlockMovementButPermitAttackAndEject()
        {
            Create(); var player = battle.Players[0];
            player.Health.ApplyDamage(TargetBodyPart.LeftLeg, 10); player.Health.ApplyDamage(TargetBodyPart.RightLeg, 10);
            Assert.That(player.Health.State, Is.EqualTo(MechState.Active));
            Assert.That(player.Movement.TryMove(new Vector2Int(4, 1)), Is.False);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(battle.Combat.TryAttack(player, battle.Enemies[0]).WasExecuted, Is.True);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(player.TryEject(), Is.True);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest] public IEnumerator DestroyedLegDuringMovementStopsWithoutRefundingArrivedCellAP()
        {
            Create(); var player = battle.Players[0];
            Assert.That(player.Movement.TryMove(new Vector2Int(7, 1)), Is.True);
            float deadline = Time.realtimeSinceStartup + 3;
            while (player.Mobility.MovedCells == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(player.Movement.IsMoving, Is.True);
            var arrived = player.CurrentGridPosition; int points = player.ActionPoints.CurrentPoints;
            player.Health.ApplyDamage(TargetBodyPart.LeftLeg, 10);
            Assert.That(player.Movement.IsMoving, Is.False);
            Assert.That(player.CurrentGridPosition, Is.EqualTo(arrived));
            Assert.That(player.transform.position, Is.EqualTo(battle.Grid.GetWorldPosition(arrived) + Vector3.up * player.HeightOffset));
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(points));
            yield return null;
        }

        [UnityTest] public IEnumerator CriticalUnitCanEjectButCannotMoveOrAttack()
        {
            Create(); var player = battle.Players[0]; var visual = MechStateTestRules.AddVisual(player);
            MechStateTestRules.MakeCritical(player);
            Assert.That(battle.Turns.IsBattleOver, Is.False);
            Assert.That(battle.Turns.CanAct(player), Is.False);
            Assert.That(player.CanTakeAction, Is.False);
            Assert.That(player.Movement.TryMove(new Vector2Int(4, 1)), Is.False);
            Assert.That(battle.Combat.TryAttack(player, battle.Enemies[0]).WasExecuted, Is.False);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            battle.Grid.TryGetCell(player.CurrentGridPosition, out var cell);
            Assert.That(cell.OccupyingUnit, Is.SameAs(player));
            Assert.That(visual.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(visual.GetComponent<Collider>().enabled, Is.True);
            Assert.That(MechStateTestRules.TryEject(player), Is.True);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(player.Health.State, Is.EqualTo(MechState.Ejected));
            Assert.That(player.Health.PilotEjected, Is.True);
            Assert.That(MechStateTestRules.TryEject(player), Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator CriticalWithoutBoundTurnManagerStillCannotMove()
        {
            Create(); var player = battle.Players[0]; MechStateTestRules.MakeCritical(player);
            player.BindTurnManager(null);
            Assert.That(player.CanTakeAction, Is.False);
            Assert.That(player.Movement.TryMove(new Vector2Int(4, 1)), Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator CriticalDuringMovementCancelsAtItsRegisteredCell()
        {
            Create(); var player = battle.Players[0];
            Assert.That(player.Movement.TryMove(new Vector2Int(7, 1)), Is.True);
            float deadline = Time.realtimeSinceStartup + 3;
            while (player.Mobility.MovedCells == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(player.Mobility.MovedCells, Is.EqualTo(1));
            Assert.That(player.Movement.IsMoving, Is.True);
            var arrived = player.CurrentGridPosition;
            MechStateTestRules.MakeCritical(player);
            Assert.That(player.Movement.IsMoving, Is.False);
            yield return new WaitForSeconds(.2f);
            Assert.That(player.CurrentGridPosition, Is.EqualTo(arrived));
            Assert.That(player.transform.position, Is.EqualTo(battle.Grid.GetWorldPosition(arrived) + Vector3.up * player.HeightOffset));
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(player.Mobility.MovedCells, Is.EqualTo(1));
            Assert.That(MechStateTestRules.CanEject(player), Is.True);
        }

        [UnityTest] public IEnumerator CriticalEnemyIsSkippedButCriticalPlayerCanBeAttacked()
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(3, 1) }, new[] { new Vector2Int(1, 1), new Vector2Int(1, 2) });
            MechStateTestRules.MakeCritical(battle.Enemies[0]); MechStateTestRules.MakeCritical(battle.Players[0]);
            int hits = 0; battle.Combat.SetRollSource(() => { hits++; return 0; });
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(battle.Enemies[1].ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(battle.Players[0].Health.State, Is.EqualTo(MechState.Critical));
            Assert.That(battle.Turns.IsBattleOver, Is.False);
            Assert.That(hits, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator CriticalDuringEnemyTurnDoesNotRepeatOrSkipNextActiveEnemy()
        { yield return StateChangeDuringEnemyMove(false); }
        [UnityTest] public IEnumerator DestructionDuringEnemyTurnDoesNotRepeatOrSkipNextActiveEnemy()
        { yield return StateChangeDuringEnemyMove(true); }
        private IEnumerator StateChangeDuringEnemyMove(bool destroy)
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(8, 1) }, new[] { new Vector2Int(1, 1), new Vector2Int(7, 1) });
            int hits = 0; battle.Combat.SetRollSource(() => { hits++; return 0; });
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            yield return battle.WaitForFirstArrival();
            var actor = battle.Enemies[0]; var arrived = actor.CurrentGridPosition;
            if (destroy) actor.Health.ApplyDamage(TargetBodyPart.Torso, 20); else MechStateTestRules.MakeCritical(actor);
            Assert.That(actor.Movement.IsMoving, Is.False);
            yield return battle.WaitForPlayerTurn();
            Assert.That(actor.CurrentGridPosition, Is.EqualTo(arrived));
            Assert.That(actor.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(battle.Enemies[1].ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(hits, Is.EqualTo(1));
            Assert.That(battle.Turns.TurnNumber, Is.EqualTo(2));
            Assert.That(battle.ActiveEnemy, Is.Null);
            Assert.That(battle.AI.ActingUnit, Is.Null);
        }
    }
}
