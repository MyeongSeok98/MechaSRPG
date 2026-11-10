using System;
using System.Reflection;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class EliminationMissionTests
    {
        private GameObject root;
        private GridManager grid;
        private TurnManager turns;
        private MissionController mission;
        private MissionData data;
        private BattleUnit player, first, second, outsider;
        private Component group;
        private Type groupType;
        private BattleUnit[] roster;
        [SetUp] public void Setup()
        {
            root = new GameObject("Elimination tests"); grid = root.AddComponent<GridManager>(); grid.Configure(12, 12, 2); grid.Generate();
            player = Unit(new Vector2Int(1, 1), UnitTeam.Player); first = Unit(new Vector2Int(3, 1), UnitTeam.Enemy);
            second = Unit(new Vector2Int(5, 1), UnitTeam.Enemy); outsider = Unit(new Vector2Int(7, 1), UnitTeam.Enemy);
            roster = new[] { player, first, second, outsider };
            turns = root.AddComponent<TurnManager>(); mission = root.AddComponent<MissionController>();
            data = ScriptableObject.CreateInstance<MissionData>();
            JsonUtility.FromJsonOverwrite("{\"missionId\":\"test\",\"targetEnemyGroupId\":\"EnemySquad_A\"}", data);
            groupType = typeof(TurnManager).Assembly.GetType("MechaXCOM.EnemyUnitGroup");
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(root); Object.DestroyImmediate(data); }
        private BattleUnit Unit(Vector2Int cell, UnitTeam team)
        {
            var unit = new GameObject(team.ToString()).AddTestBattleUnit(); unit.transform.SetParent(root.transform);
            unit.Configure(cell, 5); unit.ConfigureTeam(team); Assert.That(unit.Initialize(grid), Is.True); return unit;
        }
        private Component Group(string id, BattleUnit[] members)
        {
            Assert.That(groupType, Is.Not.Null, "EnemyUnitGroup must register actual mission targets.");
            var obj = new GameObject(id ?? "Missing ID"); obj.transform.SetParent(root.transform);
            var result = obj.AddComponent(groupType);
            groupType.GetMethod("Configure").Invoke(result, new object[] { id, members }); return result;
        }
        private void Configure(params Component[] groups)
        {
            var array = Array.CreateInstance(groupType, groups.Length);
            for (int i = 0; i < groups.Length; i++) array.SetValue(groups[i], i);
            var method = typeof(MissionController).GetMethod("Configure"); Assert.That(method, Is.Not.Null);
            Assert.That(method.Invoke(mission, new object[] { data, array }), Is.EqualTo(true));
        }
        private bool Valid()
        {
            var method = typeof(MissionController).GetMethod("ValidateSetup"); Assert.That(method, Is.Not.Null);
            object[] args = { roster, null }; bool result = (bool)method.Invoke(mission, args);
            Assert.That(args[1], result ? Is.Null : Is.Not.Null.And.Not.Empty); return result;
        }
        private void Start()
        { group = Group("EnemySquad_A", new[] { first, second }); Configure(group); Assert.That(Valid(), Is.True); turns.Initialize(roster); }
        private object Objective => typeof(MissionController).GetProperty("Objective").GetValue(mission);
        private int Count(string property) => (int)Objective.GetType().GetProperty(property).GetValue(Objective);
        private void Destroyed(BattleUnit unit) => unit.Health.ApplyDamage(TargetBodyPart.Torso, int.MaxValue);
        private void DamageBoundary(string method) => typeof(TurnManager).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(turns, null);
        [Test] public void OnlyRegisteredDestroyedTargetsCompleteMissionAndDoNotMutateData()
        {
            string original = JsonUtility.ToJson(data); Start();
            Assert.That(Count("TotalCount"), Is.EqualTo(2)); Assert.That(Count("DestroyedCount"), Is.Zero);
            Destroyed(first); Assert.That(Count("DestroyedCount"), Is.EqualTo(1)); Assert.That(turns.IsBattleOver, Is.False);
            Destroyed(second); Assert.That(mission.Result.Outcome, Is.EqualTo(MissionOutcome.Success));
            Assert.That(outsider.Health.State, Is.EqualTo(MechState.Active)); Assert.That(JsonUtility.ToJson(data), Is.EqualTo(original));
        }
        [Test] public void CriticalIsNotDestroyed()
        {
            Start(); Destroyed(first); second.Health.ConfigureDestructionChance(0);
            foreach (var part in new[] { TargetBodyPart.Head, TargetBodyPart.LeftArm, TargetBodyPart.RightArm }) second.Health.ApplyDamage(part, int.MaxValue);
            Assert.That(second.Health.State, Is.EqualTo(MechState.Critical)); Assert.That(Count("DestroyedCount"), Is.EqualTo(1));
            Assert.That(turns.IsBattleOver, Is.False); Destroyed(second); Assert.That(turns.IsPlayerVictory, Is.True);
        }
        [Test] public void EjectedTargetDoesNotGrantVictoryEvenWhenEveryEnemyIsAbsent()
        { JsonUtility.FromJsonOverwrite("{\"succeedOnEnemyElimination\":true}", mission); Start(); first.Health.MarkEjected(); Destroyed(second); Destroyed(outsider); Assert.That(Count("DestroyedCount"), Is.EqualTo(1)); Assert.That(turns.IsBattleOver, Is.False); }
        [Test] public void RemovedHealthyTargetDoesNotGrantVictory()
        { Start(); Object.DestroyImmediate(first.gameObject); Destroyed(second); Destroyed(outsider); Assert.That(Count("DestroyedCount"), Is.EqualTo(1)); Assert.That(turns.IsBattleOver, Is.False); }
        [Test] public void DestroyedTargetRecordSurvivesObjectRemoval()
        { Start(); Destroyed(first); Object.DestroyImmediate(first.gameObject); Destroyed(second); Assert.That(Count("DestroyedCount"), Is.EqualTo(2)); Assert.That(turns.IsPlayerVictory, Is.True); }
        [Test] public void RuntimeGroupEditsDoNotChangeStartedObjective()
        { Start(); groupType.GetMethod("Configure").Invoke(group, new object[] { "EnemySquad_A", new[] { first } }); Destroyed(first); Assert.That(Count("TotalCount"), Is.EqualTo(2)); Assert.That(turns.IsBattleOver, Is.False); }
        [Test] public void FinishedMissionFreezesObjectiveProgress()
        { Start(); Assert.That(mission.TryFail("Mission ended"), Is.True); Destroyed(first); Assert.That(Count("DestroyedCount"), Is.Zero); }
        [Test] public void CommonDefeatWinsOverEliminationInAtomicDamage()
        {
            Start(); DamageBoundary("BeginDamageResolution"); Destroyed(first); Destroyed(second); Destroyed(player);
            Assert.That(turns.IsBattleOver, Is.False); DamageBoundary("EndDamageResolution");
            Assert.That(mission.Result.Outcome, Is.EqualTo(MissionOutcome.Failure));
        }
        [TestCase("empty")] [TestCase("null")] [TestCase("duplicate")] [TestCase("ally")] [TestCase("outside")] [TestCase("inactive")]
        public void InvalidGroupMembersAreRejected(string kind)
        {
            BattleUnit[] members = new[] { first };
            switch (kind)
            {
                case "empty": members = Array.Empty<BattleUnit>(); break;
                case "null": members = new BattleUnit[] { null }; break;
                case "duplicate": members = new[] { first, first }; break;
                case "ally": members = new[] { player }; break;
                case "outside": members = new[] { Unit(new Vector2Int(9, 1), UnitTeam.Enemy) }; break;
                case "inactive": first.gameObject.SetActive(false); break;
            }
            Configure(Group("EnemySquad_A", members)); Assert.That(Valid(), Is.False);
        }
        [Test] public void MissingTargetGroupIsRejected()
        { Configure(Group("DifferentGroup", new[] { first })); Assert.That(Valid(), Is.False); }
        [Test] public void DuplicateGroupIdIsRejected()
        { Configure(Group("EnemySquad_A", new[] { first }), Group("EnemySquad_A", new[] { second })); Assert.That(Valid(), Is.False); }
        [Test] public void MissingGroupIdIsRejected()
        { Configure(Group(" ", new[] { first })); Assert.That(Valid(), Is.False); }
        [Test] public void InvalidMissionDataIsRejected()
        { Configure(Group("EnemySquad_A", new[] { first })); JsonUtility.FromJsonOverwrite("{\"missionId\":\"\"}", data); Assert.That(Valid(), Is.False); }
        [Test] public void DestroyObjectiveCannotUseEnemyGroupInsteadOfTargetId()
        { Configure(Group("EnemySquad_A", new[] { first })); JsonUtility.FromJsonOverwrite("{\"objectiveType\":1}", data); Assert.That(Valid(), Is.False); }
        [Test] public void InvalidDirectTurnInitializationDoesNotRefillAP()
        {
            Configure(Group("WrongGroup", new[] { first })); player.ActionPoints.TrySpend(2);
            LogAssert.Expect(LogType.Warning, new Regex(".*섬멸 대상 적 집단을 찾을 수 없습니다.*"));
            turns.Initialize(roster); Assert.That(turns.IsInitialized, Is.False);
            Assert.That(turns.TurnNumber, Is.Zero); Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(3));
        }
        [Test] public void SceneBuilderRefusesToOverwriteExistingScene()
        {
            const string path = "Assets/_Game/Scenes/EliminationMissionTest.unity";
            var before = File.ReadAllBytes(path);
            var builder = Type.GetType("MechaXCOM.EditorTools.EliminationMissionSceneBuilder, MechaXCOM.Editor", true);
            var error = Assert.Throws<TargetInvocationException>(() => builder.GetMethod("Build").Invoke(null, null));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
        }
        [Test] public void SceneBuilderPreservesAnAlreadyLoadedDirtySourceScene()
        {
            const string sourcePath = "Assets/_Game/Scenes/SqurdWeaponTest.unity";
            var bytes = File.ReadAllBytes(sourcePath);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(sourcePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            try
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                var builder = Type.GetType("MechaXCOM.EditorTools.EliminationMissionSceneBuilder, MechaXCOM.Editor", true);
                var error = Assert.Throws<TargetInvocationException>(() => builder.GetMethod("Build").Invoke(null, null));
                Assert.That(error.InnerException.Message, Does.Contain("source scene is already loaded"));
                Assert.That(scene.isLoaded && scene.isDirty, Is.True); Assert.That(scene.path, Is.EqualTo(sourcePath));
                Assert.That(File.ReadAllBytes(sourcePath), Is.EqualTo(bytes));
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
