using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class MissionDataTests
    {
        private Type type;
        private ScriptableObject data;
        private string folder;
        [SetUp] public void Setup()
        {
            type = typeof(TurnManager).Assembly.GetType("MechaXCOM.MissionData");
            Assert.That(type, Is.Not.Null, "MissionData must be an editable asset independent of battle state.");
            data = ScriptableObject.CreateInstance(type);
            JsonUtility.FromJsonOverwrite("{\"missionId\":\"mission_a\",\"targetEnemyGroupId\":\"EnemySquad_A\"}", data);
        }
        [TearDown] public void Cleanup()
        {
            if (folder != null && AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
            if (data && !AssetDatabase.Contains(data)) Object.DestroyImmediate(data);
            folder = null;
        }
        private object Read(string property) => type.GetProperty(property).GetValue(data);
        private bool Valid()
        {
            object[] args = { null };
            bool valid = (bool)type.GetMethod("IsValid").Invoke(data, args);
            Assert.That(args[0], valid ? Is.Null : Is.Not.Null.And.Not.Empty);
            return valid;
        }
        [Test] public void IdentityDescriptionRegionAndTargetSurviveAssetReload()
        {
            JsonUtility.FromJsonOverwrite("{\"missionId\":\"elimination_01\",\"displayName\":\"폐허 지역 소탕\",\"description\":\"적 소대를 섬멸한다.\\n전원 파괴 필요.\",\"regionName\":\"남부 폐허\",\"targetEnemyGroupId\":\"EnemySquad_A\"}", data);
            folder = "Assets/MissionDataTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            string path = folder + "/Mission.asset";
            AssetDatabase.CreateAsset(data, path); AssetDatabase.SaveAssets();
            Resources.UnloadAsset(data); data = (ScriptableObject)AssetDatabase.LoadAssetAtPath(path, type);
            Assert.That(Read("MissionId"), Is.EqualTo("elimination_01"));
            Assert.That(Read("DisplayName"), Is.EqualTo("폐허 지역 소탕"));
            Assert.That(Read("Description"), Is.EqualTo("적 소대를 섬멸한다.\n전원 파괴 필요."));
            Assert.That(Read("RegionName"), Is.EqualTo("남부 폐허"));
            Assert.That(Read("TargetEnemyGroupId"), Is.EqualTo("EnemySquad_A")); Assert.That(Valid(), Is.True);
        }
        [TestCase("missionId", null)] [TestCase("missionId", "")] [TestCase("missionId", " ")]
        [TestCase("targetEnemyGroupId", null)] [TestCase("targetEnemyGroupId", "")] [TestCase("targetEnemyGroupId", " ")]
        public void MissingRequiredIdIsRejected(string field, string value)
        {
            type.GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(data, value);
            Assert.That(Valid(), Is.False);
        }
        [Test] public void UnnamedMissionFallsBackToAssetName()
        { data.name = "NewMission"; Assert.That(Read("DisplayName"), Is.EqualTo("NewMission")); Assert.That(Valid(), Is.True); }
        private int ObjectiveTypeValue(ScriptableObject value)
        {
            var property = type.GetProperty("ObjectiveType"); Assert.That(property, Is.Not.Null, "MissionData must expose the Inspector objective choice.");
            return Convert.ToInt32(property.GetValue(value));
        }
        [Test] public void LegacyAndSampleAssetsDefaultToEnemyElimination()
        {
            Assert.That(ObjectiveTypeValue(data), Is.Zero);
            var sample = AssetDatabase.LoadAssetAtPath("Assets/_Game/Data/Missions/SampleEliminationMission.asset", type) as ScriptableObject;
            Assert.That(sample, Is.Not.Null); Assert.That(ObjectiveTypeValue(sample), Is.Zero);
        }
        [TestCase(0)] [TestCase(1)] public void ObjectiveChoiceSurvivesAssetReload(int choice)
        {
            JsonUtility.FromJsonOverwrite("{\"objectiveType\":" + choice + "}", data);
            folder = "Assets/MissionDataTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            string path = folder + "/Mission.asset";
            AssetDatabase.CreateAsset(data, path); AssetDatabase.SaveAssets();
            Resources.UnloadAsset(data); data = (ScriptableObject)AssetDatabase.LoadAssetAtPath(path, type);
            Assert.That(ObjectiveTypeValue(data), Is.EqualTo(choice));
        }
        [Test] public void DestroyTargetChoiceDoesNotRequireAnEnemyGroup()
        { JsonUtility.FromJsonOverwrite("{\"objectiveType\":1,\"targetEnemyGroupId\":\"\",\"targetObjectId\":\"Station_A\"}", data); Assert.That(Valid(), Is.True); }
        [Test] public void UnknownObjectiveChoiceIsRejected()
        { JsonUtility.FromJsonOverwrite("{\"objectiveType\":99}", data); Assert.That(Valid(), Is.False); }
        [Test] public void SampleAssetLoadsAndValidatesWithoutStartingAMission()
        {
            var sample = AssetDatabase.LoadAssetAtPath("Assets/_Game/Data/Missions/SampleEliminationMission.asset", type) as ScriptableObject;
            Assert.That(sample, Is.Not.Null); Object.DestroyImmediate(data); data = sample;
            Assert.That(Valid(), Is.True); Assert.That(Read("MissionId"), Is.EqualTo("sample_elimination"));
            Assert.That(Read("DisplayName"), Is.Not.Empty); Assert.That(Read("Description"), Is.Not.Empty);
            Assert.That(Read("RegionName"), Is.Not.Empty); Assert.That(Read("TargetEnemyGroupId"), Is.EqualTo("EnemySquad_A"));
        }
    }
}
