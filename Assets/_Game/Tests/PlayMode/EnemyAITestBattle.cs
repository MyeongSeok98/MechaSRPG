using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public sealed class EnemyAITestBattle : IDisposable
    {
        public GameObject Root { get; }
        public GridManager Grid { get; }
        public TurnManager Turns { get; }
        public CombatSystem Combat { get; }
        public EnemyAIController AI { get; }
        public BattleUnit[] Players { get; }
        public BattleUnit[] Enemies { get; }
        public UnitTeam ActiveTeam => Turns.ActiveTeam;
        public BattleUnit ActiveEnemy => Turns.ActiveEnemy;

        public EnemyAITestBattle(Vector2Int[] playerPositions, Vector2Int[] enemyPositions, int width = 16, int height = 16)
        {
            Root = new GameObject("Enemy AI Test Battle");
            Grid = Root.AddComponent<GridManager>();
            Grid.Configure(width, height, 2);
            Grid.Generate();
            Players = MakeUnits(playerPositions, UnitTeam.Player);
            Enemies = MakeUnits(enemyPositions, UnitTeam.Enemy);
            Turns = Root.AddComponent<TurnManager>();
            Combat = Root.AddComponent<CombatSystem>();
            Combat.Initialize(Grid, Root.AddComponent<CoverSystem>(), Turns);
            Combat.SetRollSource(() => 0);
            AI = Root.AddComponent<EnemyAIController>();
            AI.Initialize(Grid, Combat, Turns);
            Turns.ConfigureEnemyAI(AI);
            var visibility = Root.AddComponent<VisibilitySystem>();
            visibility.Initialize(Grid, Players.Concat(Enemies).ToArray());
            Turns.ConfigureVisibility(visibility);
            Turns.Initialize(Players.Concat(Enemies).ToArray());
            SetActionDelay(0);
        }
        private BattleUnit[] MakeUnits(Vector2Int[] positions, UnitTeam team)
        {
            return positions.Select((pos, index) =>
            {
                var unit = new GameObject(team + " " + index).AddTestBattleUnit();
                // Legacy AI fixtures explicitly retain their original ranged-only rules.
                unit.GetComponent<UnitCombat>().ConfigureTestRanged(5, 80, 5, 3);
                unit.GetComponent<UnitCombat>().ConfigureTestMelee(8, 95, 0, 3);
                unit.transform.SetParent(Root.transform);
                unit.Configure(pos, 5);
                unit.ConfigureTeam(team);
                Assert.That(unit.Initialize(Grid), Is.True);
                return unit;
            }).ToArray();
        }
        public void SetActionDelay(float seconds) => typeof(TurnManager)
            .GetField("enemyActionDelay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(Turns, seconds);
        public IEnumerator WaitForPlayerTurn(float timeoutSeconds = 5f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (ActiveTeam != UnitTeam.Player && !Turns.IsBattleOver && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(ActiveTeam == UnitTeam.Player || Turns.IsBattleOver, Is.True, "Enemy phase timed out.");
        }
        public IEnumerator WaitForFirstArrival()
        {
            float deadline = Time.realtimeSinceStartup + 3;
            while (Enemies[0] && Enemies[0].Mobility.MovedCells == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(Enemies[0].Mobility.MovedCells, Is.EqualTo(1));
            Assert.That(Enemies[0].Movement.IsMoving, Is.True);
        }
        public void Dispose() => UnityEngine.Object.Destroy(Root);
    }
}
