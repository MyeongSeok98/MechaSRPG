using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class DestroyTargetMissionTests
    {
        private GameObject root;
        private GridManager grid;
        private BattleUnit player, enemy;
        private CoverDurability health;
        private DestructibleObjectData objectData;
        private MissionData data;
        private MissionController mission;
        private TurnManager turns;
        private Component marker;
        private Type markerType;
        [SetUp] public void Setup()
        {
            root = new GameObject("Destroy target tests"); grid = root.AddComponent<GridManager>(); grid.Generate();
            player = Unit(new Vector2Int(1, 1), UnitTeam.Player); enemy = Unit(new Vector2Int(3, 1), UnitTeam.Enemy);
            turns = root.AddComponent<TurnManager>(); mission = root.AddComponent<MissionController>();
            var station = new GameObject("Station"); station.transform.SetParent(root.transform); health = station.AddComponent<CoverDurability>();
            objectData = ScriptableObject.CreateInstance<DestructibleObjectData>();
            JsonUtility.FromJsonOverwrite("{\"objectId\":\"station\",\"maxHP\":2000}", objectData);
            data = ScriptableObject.CreateInstance<MissionData>();
            JsonUtility.FromJsonOverwrite("{\"missionId\":\"destroy\",\"objectiveType\":1,\"targetObjectId\":\"Station_A\"}", data);
            markerType = typeof(MissionController).Assembly.GetType("MechaXCOM.MissionTarget");
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(root); Object.DestroyImmediate(data); Object.DestroyImmediate(objectData); }
        private BattleUnit Unit(Vector2Int position, UnitTeam team)
        {
            var unit = new GameObject(team.ToString()).AddTestBattleUnit(); unit.transform.SetParent(root.transform);
            unit.Configure(position, 5); unit.ConfigureTeam(team); Assert.That(unit.Initialize(grid), Is.True); return unit;
        }
        private Component Marker(string id, bool mech = false)
        {
            Assert.That(markerType, Is.Not.Null, "MissionTarget must bind a named scene target to destruction state.");
            var obj = new GameObject(id); obj.transform.SetParent(root.transform); var result = obj.AddComponent(markerType);
            var method = markerType.GetMethod(mech ? "ConfigureUnit" : "ConfigureObject"); Assert.That(method, Is.Not.Null);
            method.Invoke(result, new object[] { id, mech ? (object)enemy.Health : health }); return result;
        }
        private void Configure(params Component[] markers)
        {
            var array = Array.CreateInstance(markerType, markers.Length);
            for(int i=0;i<markers.Length;i++) array.SetValue(markers[i], i);
            var method=typeof(MissionController).GetMethod("ConfigureDestruction"); Assert.That(method, Is.Not.Null);
            Assert.That(method.Invoke(mission,new object[]{data,array}),Is.EqualTo(true));
        }
        private bool Valid() => mission.ValidateSetup(new[]{player,enemy}, out _);
        private void Start(bool mech = false)
        {
            if (!mech)
            {
                var method=typeof(CoverDurability).GetMethod("ConfigureData"); Assert.That(method,Is.Not.Null);
                Assert.That(method.Invoke(health,new object[]{objectData}),Is.EqualTo(true));
            }
            marker=Marker("Station_A",mech); Configure(marker); Assert.That(Valid(),Is.True); turns.Initialize(new[]{player,enemy});
        }
        private int Count => (int)typeof(MissionController).GetProperty("Objective").GetValue(mission).GetType().GetProperty("DestroyedCount").GetValue(typeof(MissionController).GetProperty("Objective").GetValue(mission));
        private void Boundary(string name) => typeof(TurnManager).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(turns,null);
        [Test] public void FacilityReadsDataHPAndWinsWhileEnemySurvives()
        {
            Start(); string original=JsonUtility.ToJson(objectData); Assert.That(health.MaxHP,Is.EqualTo(2000));
            health.ApplyDamage(1000); Assert.That(Count,Is.Zero); Assert.That(turns.IsBattleOver,Is.False);
            health.ApplyDamage(1000); Assert.That(Count,Is.EqualTo(1)); Assert.That(mission.Result.Outcome,Is.EqualTo(MissionOutcome.Success));
            Assert.That(enemy.IsAlive,Is.True); Assert.That(JsonUtility.ToJson(objectData),Is.EqualTo(original));
        }
        [Test] public void KillingAllEnemiesDoesNotCompleteFacilityMission()
        { Start(); enemy.Health.ApplyDamage(TargetBodyPart.Torso,int.MaxValue); Assert.That(turns.IsBattleOver,Is.False); }
        [Test] public void DisablingOrRemovingIntactFacilityDoesNotWin()
        { Start(); health.gameObject.SetActive(false); turns.CheckBattleEnd(); Assert.That(Count,Is.Zero); Object.DestroyImmediate(health.gameObject); turns.CheckBattleEnd(); Assert.That(turns.IsBattleOver,Is.False); }
        [Test] public void RecordedDestructionSurvivesRemovalDuringAtomicDamage()
        { Start(); Boundary("BeginDamageResolution"); health.ApplyDamage(2000); Object.DestroyImmediate(health.gameObject); Assert.That(turns.IsBattleOver,Is.False); Boundary("EndDamageResolution"); Assert.That(mission.Result.Outcome,Is.EqualTo(MissionOutcome.Success)); }
        [Test] public void CommonDefeatWinsWhenTargetAndPlayerDieTogether()
        { Start(); Boundary("BeginDamageResolution"); health.ApplyDamage(2000); player.Health.ApplyDamage(TargetBodyPart.Torso,int.MaxValue); Boundary("EndDamageResolution"); Assert.That(mission.Result.Outcome,Is.EqualTo(MissionOutcome.Failure)); }
        [Test] public void CriticalMechaTargetMustBecomeDestroyed()
        {
            Start(true); enemy.Health.ConfigureDestructionChance(0);
            foreach(var part in new[]{TargetBodyPart.Head,TargetBodyPart.LeftArm,TargetBodyPart.RightArm}) enemy.Health.ApplyDamage(part,int.MaxValue);
            Assert.That(enemy.Health.State,Is.EqualTo(MechState.Critical)); Assert.That(turns.IsBattleOver,Is.False);
            enemy.Health.ApplyDamage(TargetBodyPart.Torso,int.MaxValue); Assert.That(mission.Result.Outcome,Is.EqualTo(MissionOutcome.Success));
        }
        [Test] public void FinishedObjectiveProgressIsFrozen()
        { Start(); mission.TryFail("Aborted"); health.ApplyDamage(2000); Assert.That(Count,Is.Zero); }
        [Test] public void MissingTargetIdDoesNotUseEnemyGroup()
        { marker=Marker("Station_A"); Configure(marker); JsonUtility.FromJsonOverwrite("{\"targetObjectId\":\"\",\"targetEnemyGroupId\":\"Station_A\"}",data); Assert.That(Valid(),Is.False); }
        [Test] public void DuplicateTargetIdsAreRejected()
        { Configure(Marker("Station_A"),Marker("Station_A")); Assert.That(Valid(),Is.False); }
        [Test] public void WrongTargetIdIsRejected()
        { Configure(Marker("Different")); Assert.That(Valid(),Is.False); }
        [TestCase("none")] [TestCase("both")]
        public void MissingOrAmbiguousHealthSourceIsRejected(string kind)
        {
            marker=Marker("Station_A"); Configure(marker);
            markerType.GetField(kind=="none"?"objectHealth":"unitHealth",BindingFlags.NonPublic|BindingFlags.Instance)
                .SetValue(marker,kind=="none"?null:enemy.Health); Assert.That(Valid(),Is.False);
        }
        [Test] public void ObjectDataHPIsSnapshotAndCannotBeRebound()
        {
            Start(); Assert.That(health.MaxHP,Is.EqualTo(2000)); JsonUtility.FromJsonOverwrite("{\"maxHP\":3000}",objectData);
            Assert.That(health.MaxHP,Is.EqualTo(2000));
            Assert.That(typeof(CoverDurability).GetMethod("ConfigureData").Invoke(health,new object[]{objectData}),Is.EqualTo(false));
        }
    }
}
