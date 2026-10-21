using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class FactionTurnTests
    {
        private EnemyAITestBattle battle;
        [UnityTearDown] public IEnumerator Cleanup() { battle?.Dispose(); yield return null; }
        [UnityTest]
        public IEnumerator OnlyCurrentFactionCanActAndEndTurnWaitsForEnemyPhase()
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(3, 1), new Vector2Int(3, 2) },
                new[] { new Vector2Int(1, 1), new Vector2Int(1, 2) });
            var player = battle.Players[0];
            player.ActionPoints.TrySpend(3);
            player.Mobility.RecordCellMoved();
            battle.Enemies[0].ActionPoints.TrySpend(4);
            battle.Enemies[0].Mobility.RecordCellMoved();
            Assert.That(battle.Turns.CanAct(player), Is.True);
            Assert.That(battle.Turns.CanAct(battle.Enemies[0]), Is.False);
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            Assert.That(battle.ActiveTeam, Is.EqualTo(UnitTeam.Enemy));
            Assert.That(battle.Turns.TurnNumber, Is.EqualTo(1));
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(player.MovementEvasion, Is.EqualTo(5));
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(battle.Enemies[0].MovementEvasion, Is.Zero);
            Assert.That(battle.Turns.CanAct(player), Is.False);
            Assert.That(battle.Turns.CanAct(battle.Enemies[0]), Is.True);
            Assert.That(battle.Turns.TryEndTurn(), Is.False);
            Assert.That(player.Movement.TryMove(new Vector2Int(4, 1)), Is.False);
            int rolls = 0;
            battle.Combat.SetRollSource(() => { rolls++; return 99; });
            Assert.That(battle.Combat.TryAttack(player, battle.Enemies[0]).WasExecuted, Is.False);
            Assert.That(battle.Combat.TryAttack(battle.Enemies[0], battle.Enemies[1]).WasExecuted, Is.False);
            Assert.That(rolls, Is.Zero);
            yield return battle.WaitForPlayerTurn();
            Assert.That(rolls, Is.EqualTo(2));
            Assert.That(battle.Turns.TurnNumber, Is.EqualTo(2));
            foreach (var ally in battle.Players) Assert.That(ally.ActionPoints.CurrentPoints, Is.EqualTo(5));
            foreach (var foe in battle.Enemies) Assert.That(foe.ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(player.MovementEvasion, Is.Zero);
            Assert.That(player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }
        [UnityTest]
        public IEnumerator EndTurnRefillsAPAndResetsEvasionAfterEnemyActions()
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(3, 1) }, new[] { new Vector2Int(1, 1) });
            var player = battle.Players[0];
            player.ActionPoints.TrySpend(3); player.Mobility.RecordCellMoved(); player.Health.ApplyDamage(TargetBodyPart.Torso, 5);
            battle.Combat.SetRollSource(() => 99);
            Assert.That(battle.Turns.TryEndTurn(), Is.True);
            battle.Turns.Initialize(new[] { player });
            Assert.That(battle.ActiveTeam, Is.EqualTo(UnitTeam.Enemy));
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
            yield return battle.WaitForPlayerTurn();
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(player.Mobility.MovedCells, Is.Zero);
            Assert.That(player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(player.CurrentGridPosition, Is.EqualTo(new Vector2Int(3, 1)));
        }
        [UnityTest]
        public IEnumerator MissingOrDisabledAIRejectsTurnWithoutResettingAP()
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(3, 1) }, new[] { new Vector2Int(1, 1) });
            battle.Players[0].ActionPoints.TrySpend(3);
            battle.AI.enabled = false;
            LogAssert.Expect(LogType.Warning, "Enemy AI is unavailable; cannot end the player turn.");
            Assert.That(battle.Turns.TryEndTurn(), Is.False);
            Assert.That(battle.ActiveTeam, Is.EqualTo(UnitTeam.Player));
            Assert.That(battle.Players[0].ActionPoints.CurrentPoints, Is.EqualTo(2));
            battle.AI.enabled = true;
            battle.Turns.ConfigureEnemyAI(null);
            LogAssert.Expect(LogType.Warning, "Enemy AI is unavailable; cannot end the player turn.");
            Assert.That(battle.Turns.TryEndTurn(), Is.False);
            Assert.That(battle.Turns.TurnNumber, Is.EqualTo(1));
            yield return null;
        }
    }
}
