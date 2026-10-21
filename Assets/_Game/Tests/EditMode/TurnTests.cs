using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class TurnTests : CombatSystemTests
    {
        [Test]
        public void RepeatedInitializeDoesNotResetTurnOrRefill()
        {
            player.ActionPoints.TrySpend(3);
            turns.Initialize(new[] { player, enemy });
            Assert.That(turns.TurnNumber, Is.EqualTo(1)); Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [Test]
        public void PlayerDeathEndsBattleAndBlocksTurn()
        {
            player.Health.ApplyDamage(TargetBodyPart.Torso, 20);
            Assert.That(turns.IsBattleOver, Is.True); Assert.That(turns.IsPlayerVictory, Is.False);
            Assert.That(turns.TryEndTurn(), Is.False); Assert.That(turns.CanAct(enemy), Is.False);
        }
    }
}
