using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public sealed class OverwatchTestBattle : IDisposable
    {
        public GameObject Root { get; }
        public GridManager Grid { get; }
        public BattleUnit Watcher { get; }
        public BattleUnit Second { get; }
        public BattleUnit Mover { get; }
        public BattleUnit Spare { get; }
        public TurnManager Turns { get; }
        public CombatSystem Combat { get; }
        public OverwatchSystem Overwatch { get; }
        public OverwatchTestBattle(bool withMoverVisual = false)
        {
            Root = new GameObject("Overwatch reaction tests");
            Grid = Root.AddComponent<GridManager>(); Grid.Configure(12, 12, 2); Grid.Generate();
            Watcher = Unit(new Vector2Int(3, 1), UnitTeam.Player);
            Second = Unit(new Vector2Int(5, 1), UnitTeam.Player);
            Mover = Unit(new Vector2Int(3, 4), UnitTeam.Enemy, withMoverVisual);
            Spare = Unit(new Vector2Int(10, 10), UnitTeam.Enemy);
            var units = new[] { Watcher, Second, Mover, Spare };
            Turns = Root.AddComponent<TurnManager>(); Turns.Initialize(units);
            var visibility = Root.AddComponent<VisibilitySystem>(); visibility.Initialize(Grid, units);
            Turns.ConfigureVisibility(visibility);
            Combat = Root.AddComponent<CombatSystem>(); Combat.Initialize(Grid, Root.AddComponent<CoverSystem>(), Turns);
            Combat.SetRollSource(() => 0);
            Overwatch = Root.AddComponent<OverwatchSystem>(); Overwatch.Initialize(Grid, Combat, Turns, units);
        }
        private BattleUnit Unit(Vector2Int position, UnitTeam team, bool withVisual = false)
        {
            var unit = new GameObject(team.ToString()).AddTestBattleUnit(); unit.transform.SetParent(Root.transform);
            if (withVisual) new GameObject("Visual").transform.SetParent(unit.transform, false);
            unit.Configure(position, 5); unit.ConfigureTeam(team);
            Assert.That(unit.Initialize(Grid), Is.True); return unit;
        }
        public void Arm(BattleUnit watcher = null, FacingDirection facing = FacingDirection.North) =>
            Assert.That(Overwatch.TryBegin(watcher ? watcher : Watcher, WeaponSlot.RightHand, facing), Is.True);
        public void StartTeam(UnitTeam team) => typeof(TurnManager).GetMethod("StartTeamTurn", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(Turns, new object[] { team });
        public bool Resolving
        {
            get
            {
                var property = typeof(OverwatchSystem).GetProperty("IsResolving");
                Assert.That(property, Is.Not.Null, "Reaction processing must expose its busy state.");
                return (bool)property.GetValue(Overwatch);
            }
        }
        public void Building(Vector2Int position)
        {
            var building = GameObject.CreatePrimitive(PrimitiveType.Cube); building.transform.SetParent(Root.transform);
            building.transform.position = Grid.GetWorldPosition(position) + Vector3.up;
            building.transform.localScale = new Vector3(1.8f, 4, 1.8f);
            Grid.RegisterObstacle(building.AddComponent<GridObstacle>().Collider);
        }
        public void Dispose()
        {
            if (!Root) return;
            if (Application.isPlaying) Object.Destroy(Root); else Object.DestroyImmediate(Root);
        }
    }
}
