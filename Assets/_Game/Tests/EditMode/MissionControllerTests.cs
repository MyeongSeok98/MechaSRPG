using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class MissionControllerTests
    {
        private GameObject root;
        private GridManager grid;
        private TurnManager turns;
        private BattleUnit player, enemy;
        private Component mission;
        private Type MissionType => typeof(TurnManager).Assembly.GetType("MechaXCOM.MissionController");
        [SetUp] public void Setup()
        {
            root = new GameObject("Mission tests"); grid = root.AddComponent<GridManager>(); grid.Generate();
            player = Unit(new Vector2Int(1, 1), UnitTeam.Player); enemy = Unit(new Vector2Int(3, 3), UnitTeam.Enemy);
            turns = root.AddComponent<TurnManager>();
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);
        private BattleUnit Unit(Vector2Int cell, UnitTeam team)
        {
            var unit = new GameObject(team.ToString()).AddTestBattleUnit(); unit.transform.SetParent(root.transform);
            unit.Configure(cell, 5); unit.ConfigureTeam(team); Assert.That(unit.Initialize(grid), Is.True); return unit;
        }
        private void Start(bool custom = true, BattleUnit[] roster = null)
        {
            Assert.That(MissionType, Is.Not.Null, "MissionController must own mission outcomes.");
            if (custom) mission = root.AddComponent(MissionType);
            turns.Initialize(roster ?? new[] { player, enemy });
            mission = root.GetComponent(MissionType);
            Assert.That(mission, Is.Not.Null);
        }
        private object Read(string name) => MissionType.GetProperty(name).GetValue(mission);
        private string Outcome => Read("Result").GetType().GetProperty("Outcome").GetValue(Read("Result")).ToString();
        private string Reason => (string)Read("Result").GetType().GetProperty("Reason").GetValue(Read("Result"));
        private bool Request(string method, string reason) => (bool)MissionType.GetMethod(method).Invoke(mission, new object[] { reason });
        private void DamageBoundary(string method) => typeof(TurnManager).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(turns, null);

        [Test] public void CustomMissionDoesNotAssumeEnemyEliminationIsSuccess()
        { Start(); enemy.Health.ApplyDamage(TargetBodyPart.Torso, int.MaxValue); Assert.That(turns.IsBattleOver, Is.False); Assert.That(Outcome, Is.EqualTo("InProgress")); }
        [Test] public void LegacyBattleKeepsEnemyEliminationVictory()
        { Start(false); enemy.Health.ApplyDamage(TargetBodyPart.Torso, int.MaxValue); Assert.That(Outcome, Is.EqualTo("Success")); Assert.That(turns.IsPlayerVictory, Is.True); }
        [Test] public void AllPlayerUnitsLostIsCommonFailure()
        { Start(); player.Health.ApplyDamage(TargetBodyPart.Torso, int.MaxValue); Assert.That(Outcome, Is.EqualTo("Failure")); Assert.That(Reason, Is.Not.Empty); Assert.That(turns.IsBattleOver, Is.True); }
        [Test] public void OneRemainingPlayerPreventsCommonFailure()
        {
            var second = Unit(new Vector2Int(2, 1), UnitTeam.Player); Start(roster: new[] { player, second, enemy });
            player.Health.ApplyDamage(TargetBodyPart.Torso, int.MaxValue); Assert.That(turns.IsBattleOver, Is.False);
            second.Health.ApplyDamage(TargetBodyPart.Torso, int.MaxValue); Assert.That(Outcome, Is.EqualTo("Failure"));
        }
        [Test] public void CriticalPlayerStillPresentAndCanEject()
        {
            Start(); player.Health.ConfigureDestructionChance(0);
            foreach (var part in new[] { TargetBodyPart.Head, TargetBodyPart.LeftArm, TargetBodyPart.RightArm }) player.Health.ApplyDamage(part, int.MaxValue);
            Assert.That(player.Health.State, Is.EqualTo(MechState.Critical)); Assert.That(turns.IsBattleOver, Is.False);
            Assert.That(player.TryEject(), Is.True); Assert.That(Outcome, Is.EqualTo("Failure"));
        }
        [Test] public void ExplicitSuccessStopsBattleAndCannotBeReplaced()
        {
            Start(); Assert.That(Request("TrySucceed", "Facility destroyed"), Is.True);
            Assert.That(Outcome, Is.EqualTo("Success")); Assert.That(Reason, Is.EqualTo("Facility destroyed"));
            Assert.That(turns.CanAct(player) || turns.TryEndTurn() || player.Movement.TryMove(new Vector2Int(2, 1)), Is.False);
            Assert.That(Request("TryFail", "Too late"), Is.False); Assert.That(Reason, Is.EqualTo("Facility destroyed"));
        }
        [Test] public void MissionSpecificFailureHasItsOwnReason()
        { Start(); Assert.That(Request("TryFail", "Escort destroyed"), Is.True); Assert.That(Outcome, Is.EqualTo("Failure")); Assert.That(Reason, Is.EqualTo("Escort destroyed")); Assert.That(turns.IsPlayerVictory, Is.False); }
        [Test] public void AtomicDamageWaitsAndCommonFailureOverridesPendingSuccess()
        {
            Start(); DamageBoundary("BeginDamageResolution");
            Assert.That(Request("TrySucceed", "Target destroyed"), Is.True);
            player.Health.ApplyDamage(TargetBodyPart.Torso, int.MaxValue); enemy.Health.ApplyDamage(TargetBodyPart.Torso, int.MaxValue);
            Assert.That(turns.IsBattleOver, Is.False); Assert.That(Outcome, Is.EqualTo("InProgress"));
            DamageBoundary("EndDamageResolution"); Assert.That(Outcome, Is.EqualTo("Failure"));
        }
        [Test] public void ReinitializationDoesNotEraseCompletedResult()
        { Start(); Request("TryFail", "Failed"); turns.Initialize(new[] { player, enemy }); Assert.That(Reason, Is.EqualTo("Failed")); Assert.That(turns.IsBattleOver, Is.True); }
        [Test] public void MissingPlayerRosterFailsAtInitialization()
        { Start(roster: new[] { enemy }); Assert.That(Outcome, Is.EqualTo("Failure")); Assert.That(turns.IsBattleOver, Is.True); }
        [Test] public void DisabledMissionRejectsExplicitRequests()
        { Start(); ((Behaviour)mission).enabled = false; Assert.That(Request("TrySucceed", "Invalid"), Is.False); Assert.That(turns.IsBattleOver, Is.False); }
    }
}
