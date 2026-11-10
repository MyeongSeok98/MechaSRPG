using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace MechaXCOM.Tests
{
#if UNITY_EDITOR
    public class EliminationScenePlayTests
    {
        private const string Path = "Assets/_Game/Scenes/EliminationMissionTest.unity";
        private byte[] sceneBytes;
        private MissionController mission;
        private TurnManager turns;
        private EnemyUnitGroup group;
        [UnitySetUp] public IEnumerator Setup()
        { sceneBytes = File.ReadAllBytes(Path); yield return Load(); }
        private IEnumerator Load()
        {
            EditorSceneManager.LoadSceneInPlayMode(Path, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            turns = Object.FindFirstObjectByType<TurnManager>(); mission = turns.Mission;
            group = Object.FindFirstObjectByType<EnemyUnitGroup>();
            Assert.That(turns.IsInitialized, Is.True); yield return TurnBannerTestSupport.Finish(turns);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("Elimination cleanup"));
            yield return SceneManager.UnloadSceneAsync(old);
            Assert.That(File.ReadAllBytes(Path), Is.EqualTo(sceneBytes), "Tests must not save changes to the authored scene.");
        }
        [UnityTest] public IEnumerator SceneConnectsThreeVsThreeAndSharedMissionData()
        {
            var units = Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None);
            Assert.That(units.Count(u => u.Team == UnitTeam.Player), Is.EqualTo(3));
            Assert.That(units.Count(u => u.Team == UnitTeam.Enemy), Is.EqualTo(3));
            Assert.That(group.GroupId, Is.EqualTo(mission.Data.TargetEnemyGroupId));
            Assert.That(mission.Objective.TotalCount, Is.EqualTo(3)); Assert.That(mission.Objective.DestroyedCount, Is.Zero);
            Assert.That(mission.SucceedOnEnemyElimination, Is.False); yield return null;
        }
        [UnityTest] public IEnumerator LastCriticalTargetDoesNotWinUntilDestroyed()
        {
            string before = JsonUtility.ToJson(mission.Data); var targets = group.GetMembers();
            targets[0].Health.ApplyDamage(TargetBodyPart.Torso, int.MaxValue);
            targets[1].Health.ApplyDamage(TargetBodyPart.Torso, int.MaxValue);
            targets[2].Health.ConfigureDestructionChance(0);
            foreach (var part in new[] { TargetBodyPart.Head, TargetBodyPart.LeftArm, TargetBodyPart.RightArm }) targets[2].Health.ApplyDamage(part, int.MaxValue);
            Assert.That(targets[2].Health.State, Is.EqualTo(MechState.Critical));
            Assert.That(mission.Objective.DestroyedCount, Is.EqualTo(2)); Assert.That(turns.IsBattleOver, Is.False);
            int events = 0; mission.Completed += _ => events++;
            targets[2].Health.ApplyDamage(TargetBodyPart.Torso, int.MaxValue);
            Assert.That(turns.IsBattleOver && turns.IsPlayerVictory, Is.True); Assert.That(events, Is.EqualTo(1));
            Assert.That(mission.Objective.DestroyedCount, Is.EqualTo(3));
            Assert.That(JsonUtility.ToJson(mission.Data), Is.EqualTo(before)); yield return null;
        }
        [UnityTest] public IEnumerator PlayerAnnihilationStillFailsTheMission()
        {
            foreach (var unit in Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None).Where(u => u.Team == UnitTeam.Player))
                unit.Health.ApplyDamage(TargetBodyPart.Torso, int.MaxValue);
            Assert.That(mission.Result.Outcome, Is.EqualTo(MissionOutcome.Failure)); Assert.That(turns.IsBattleOver, Is.True);
            Assert.That(mission.Objective.DestroyedCount, Is.Zero); yield return null;
        }
        [UnityTest] public IEnumerator ReloadingSceneStartsFreshProgressWithoutChangingAsset()
        {
            string before = JsonUtility.ToJson(mission.Data);
            group.GetMembers()[0].Health.ApplyDamage(TargetBodyPart.Torso, int.MaxValue);
            Assert.That(mission.Objective.DestroyedCount, Is.EqualTo(1));
            yield return Load(); Assert.That(mission.Objective.DestroyedCount, Is.Zero);
            Assert.That(mission.Result.Outcome, Is.EqualTo(MissionOutcome.InProgress));
            Assert.That(JsonUtility.ToJson(mission.Data), Is.EqualTo(before));
        }
    }
#endif
    public class EliminationCombatPlayTests
    {
        private GameObject root;
        private MissionData data;
        private MissionController mission;
        private TurnManager turns;
        private GridManager grid;
        private BattleUnit player, enemy;
        private CombatSystem combat;
        [UnitySetUp] public IEnumerator Setup()
        {
            root = new GameObject("Elimination combat"); grid = root.AddComponent<GridManager>(); grid.Configure(12, 12, 2); grid.Generate();
            player = Unit(new Vector2Int(1, 1), UnitTeam.Player); enemy = Unit(new Vector2Int(3, 1), UnitTeam.Enemy);
            data = ScriptableObject.CreateInstance<MissionData>();
            JsonUtility.FromJsonOverwrite("{\"missionId\":\"combat\",\"targetEnemyGroupId\":\"Targets\"}", data);
            turns = root.AddComponent<TurnManager>(); mission = root.AddComponent<MissionController>();
            var group = root.AddComponent<EnemyUnitGroup>(); group.Configure("Targets", new[] { enemy });
            Assert.That(mission.Configure(data, new[] { group }), Is.True);
            var cover = root.AddComponent<CoverSystem>(); combat = root.AddTestCombatSystem(); combat.Initialize(grid, cover, turns);
            var visibility = root.AddComponent<VisibilitySystem>(); visibility.Initialize(grid, new[] { player, enemy }); turns.ConfigureVisibility(visibility);
            turns.Initialize(new[] { player, enemy });
            var camera = new GameObject("Feedback camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
            var feedback = root.AddComponent<CombatFeedbackPresenter>(); feedback.Initialize(combat, turns, camera);
            combat.SetRollSource(() => 0); combat.SetCriticalRollSource(() => 99); combat.SetDamageRollSource((min, max) => min);
            yield return null;
        }
        private BattleUnit Unit(Vector2Int cell, UnitTeam team)
        {
            var unit = new GameObject(team.ToString()).AddTestBattleUnit(); unit.transform.SetParent(root.transform);
            unit.Configure(cell, 5); unit.ConfigureTeam(team); unit.GetComponent<UnitCombat>().ConfigureTestRanged(40, 99, 7, 3);
            Assert.That(unit.Initialize(grid), Is.True); return unit;
        }
        [UnityTearDown] public IEnumerator Cleanup() { Object.Destroy(root); Object.Destroy(data); yield return null; }
        private void LastShot()
        {
            Assert.That(combat.TryAttack(player, enemy).WasExecuted, Is.True);
            Assert.That(enemy.Health.State, Is.EqualTo(MechState.Destroyed)); Assert.That(mission.Objective.DestroyedCount, Is.EqualTo(1));
            Assert.That(turns.IsPresentingAttack, Is.True); Assert.That(turns.IsBattleOver, Is.False);
        }
        private IEnumerator Finish()
        {
            float deadline = Time.realtimeSinceStartup + 3;
            while (!turns.IsBattleOver && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(turns.IsBattleOver, Is.True);
        }
        [UnityTest] public IEnumerator FinalShotWaitsForPopupThenCompletesOnce()
        {
            int events = 0; mission.Completed += _ => { events++; Assert.That(turns.IsPresentingAttack, Is.False); };
            LastShot(); Assert.That(events, Is.Zero); yield return Finish();
            Assert.That(mission.Result.Outcome, Is.EqualTo(MissionOutcome.Success)); Assert.That(events, Is.EqualTo(1));
            Assert.That(turns.CanAct(player) || turns.TryEndTurn(), Is.False);
        }
        [UnityTest] public IEnumerator CommonDefeatDuringPopupOverridesCompletedElimination()
        { LastShot(); player.Health.ApplyDamage(TargetBodyPart.Torso, int.MaxValue); yield return Finish(); Assert.That(mission.Result.Outcome, Is.EqualTo(MissionOutcome.Failure)); }
        [UnityTest] public IEnumerator ExplicitFailureDuringPopupOverridesObjectiveSuccess()
        { LastShot(); Assert.That(mission.TryFail("Protected target lost"), Is.True); yield return Finish(); Assert.That(mission.Result.Outcome, Is.EqualTo(MissionOutcome.Failure)); }
        [UnityTest] public IEnumerator InvalidSceneMissionStopsBeforeGeneratingGrid()
        {
            var other = new GameObject("Invalid mission");
            try
            {
                var otherGrid = other.AddComponent<GridManager>(); var cover = other.AddComponent<CoverSystem>();
                var otherTurns = other.AddComponent<TurnManager>(); var otherMission = other.AddComponent<MissionController>();
                otherMission.Configure(data, new EnemyUnitGroup[0]);
                var controller = other.AddComponent<BattleSceneController>(); controller.enabled = false;
                controller.Configure(otherGrid, cover, new[] { player, enemy }, new GridObstacle[0], new CoverObject[0]);
                controller.ConfigureCombat(combat, otherTurns);
                int ap = player.ActionPoints.CurrentPoints;
                LogAssert.Expect(LogType.Warning, new Regex(".*섬멸 대상 적 집단을 찾을 수 없습니다.*"));
                Assert.That(controller.InitializeBattle(), Is.False);
                Assert.That(otherGrid.TryGetCell(Vector2Int.zero, out _), Is.False); Assert.That(otherTurns.IsInitialized, Is.False);
                Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(ap));
            }
            finally { Object.Destroy(other); }
            yield return null;
        }
    }
}
