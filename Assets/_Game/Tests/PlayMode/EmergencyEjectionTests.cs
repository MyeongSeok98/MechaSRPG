using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class EmergencyEjectionTests
    {
        private EnemyAITestBattle battle;
        [UnityTearDown] public IEnumerator Cleanup() { battle?.Dispose(); yield return null; }
        private void Create() => battle = new EnemyAITestBattle(new[] { new Vector2Int(3, 1), new Vector2Int(3, 3) }, new[] { new Vector2Int(1, 1) });

        [UnityTest] public IEnumerator ZeroAPLastCriticalUnitCanEjectNextPlayerTurn()
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(3, 1) }, new[] { new Vector2Int(1, 1) });
            var player = battle.Players[0]; var enemy = battle.Enemies[0];
            MechStateTestRules.MakeCritical(player); MechStateTestRules.MakeCritical(enemy);
            player.ActionPoints.TrySpend(5); player.Mobility.RecordCellMoved();
            Assert.That(MechStateTestRules.CanEject(player), Is.False);
            Assert.That(MechStateTestRules.TryEject(player), Is.False);
            Assert.That(player.Health.State, Is.EqualTo(MechState.Critical));
            Assert.That(battle.Turns.IsBattleOver, Is.False);
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            yield return battle.WaitForPlayerTurn();
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(player.MovementEvasion, Is.Zero);
            Assert.That(enemy.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(battle.Turns.TurnNumber, Is.EqualTo(2));
            Assert.That(MechStateTestRules.TryEject(player), Is.True);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(battle.Turns.IsBattleOver, Is.True);
            Assert.That(battle.Turns.IsPlayerVictory, Is.False);
        }

        [UnityTest] public IEnumerator ActiveUnitCanEjectAndCannotBeRetargeted()
        {
            Create(); var player = battle.Players[0]; var visual = MechStateTestRules.AddVisual(player);
            var cellPosition = player.CurrentGridPosition;
            Assert.That(MechStateTestRules.CanEject(player), Is.True);
            Assert.That(MechStateTestRules.TryEject(player), Is.True);
            Assert.That(player.Health.State, Is.EqualTo(MechState.Ejected));
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(MechStateTestRules.TryEject(player), Is.False);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(4));
            battle.Grid.TryGetCell(cellPosition, out var cell); Assert.That(cell.Occupied, Is.False);
            Assert.That(visual.GetComponent<Collider>().enabled, Is.False);
            Assert.That(visual.GetComponent<Renderer>().enabled, Is.False);
            Assert.That(battle.Turns.IsBattleOver, Is.False);
            Assert.That(battle.Turns.CanAct(player), Is.False);
            Assert.That(player.Movement.TryMove(new Vector2Int(4, 1)), Is.False);
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            Assert.That(battle.Combat.GetPreview(battle.Enemies[0], player).CanAttack, Is.False);
            yield return battle.WaitForPlayerTurn();
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(4), "Ejected units never refill AP.");
            Assert.That(player.Initialize(battle.Grid), Is.True);
            Assert.That(player.Health.State, Is.EqualTo(MechState.Ejected), "Repeated initialization cannot rejoin battle.");
        }

        [UnityTest] public IEnumerator DestroyedAndEjectedStateRemainQueryableAfterRemoval()
        {
            Create(); var destroyed = battle.Players[0]; var ejected = battle.Players[1];
            Assert.That(MechStateTestRules.TryEject(ejected), Is.True);
            destroyed.Health.ApplyDamage(TargetBodyPart.Torso, 20);
            yield return null; yield return null;
            Assert.That(destroyed != null, Is.True);
            Assert.That(ejected != null, Is.True);
            Assert.That(destroyed.Health.State, Is.EqualTo(MechState.Destroyed));
            Assert.That(destroyed.Health.PilotEjected, Is.False);
            Assert.That(ejected.Health.State, Is.EqualTo(MechState.Ejected));
            Assert.That(ejected.Health.PilotEjected, Is.True);
            Assert.That(destroyed.gameObject.activeSelf, Is.False);
            Assert.That(ejected.gameObject.activeSelf, Is.False);
            Assert.That(battle.Turns.IsBattleOver, Is.True);
            Assert.That(MechStateTestRules.TryEject(destroyed), Is.False);
        }

        [UnityTest] public IEnumerator WrongTurnMovingUnregisteredAndUnavailableUnitsCannotEject()
        {
            Create(); var player = battle.Players[0]; var other = battle.Players[1];
            Assert.That(MechStateTestRules.TryEject(battle.Enemies[0]), Is.False);
            Assert.That(player.Movement.TryMove(new Vector2Int(5, 1)), Is.True);
            Assert.That(MechStateTestRules.TryEject(player), Is.False);
            Assert.That(MechStateTestRules.TryEject(other), Is.False);
            player.Movement.CancelMove();
            player.ActionPoints.enabled = false;
            Assert.That(MechStateTestRules.TryEject(player), Is.False); player.ActionPoints.enabled = true;
            battle.Grid.ReleaseUnit(player);
            Assert.That(MechStateTestRules.TryEject(player), Is.False);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(player.Health.State, Is.EqualTo(MechState.Active));
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            Assert.That(MechStateTestRules.TryEject(other), Is.False);
            yield return battle.WaitForPlayerTurn();
        }
    }
}
