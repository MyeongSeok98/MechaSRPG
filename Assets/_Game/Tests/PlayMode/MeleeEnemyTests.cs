using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class MeleeEnemyTests
    {
        private EnemyAITestBattle battle;
        private void Create()
        {
            battle = new EnemyAITestBattle(new[] { new Vector2Int(3, 1) }, new[] { new Vector2Int(1, 1) });
            battle.Enemies[0].Combat.ConfigureTestMelee(8, 95, 2, 3);
        }
        [UnityTearDown] public IEnumerator Cleanup() { battle?.Dispose(); yield return null; }
        [UnityTest] public IEnumerator CloseEnemyUsesHighAccuracyMeleeAndIgnoresCover()
        {
            Create();
            battle.Grid.TryGetCell(new Vector2Int(3, 1), out var cell); cell.SetCover(CoverDirection.West, CoverType.Full);
            battle.Combat.SetRollSource(() => 94);
            battle.Turns.TryEndTurn(); yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(12));
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(battle.Enemies[0].Mobility.MovedCells, Is.Zero);
        }
        [UnityTest] public IEnumerator CloseEnemyAcrossRavineUsesRangedInstead()
        {
            Create(); battle.Grid.SetWalkable(new Vector2Int(2, 1), false);
            battle.Turns.TryEndTurn(); yield return battle.WaitForPlayerTurn();
            Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(battle.Enemies[0].ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(battle.Enemies[0].Mobility.MovedCells, Is.Zero);
        }
    }
}
