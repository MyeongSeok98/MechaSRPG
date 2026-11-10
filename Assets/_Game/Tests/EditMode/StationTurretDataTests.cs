using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class StationTurretDataTests
    {
        private readonly List<Object> owned = new List<Object>();
        private string folder;
        private Type DataType(string name)
        {
            var type = typeof(TurnManager).Assembly.GetType("MechaXCOM." + name);
            Assert.That(type, Is.Not.Null, name + " must be an editable data asset."); return type;
        }
        private ScriptableObject Data(bool turret)
        {
            var data = ScriptableObject.CreateInstance(DataType(turret ? "TurretData" : "DestructibleObjectData")); owned.Add(data);
            JsonUtility.FromJsonOverwrite("{\"objectId\":\"test\",\"maxHP\":2000,\"baseAP\":5,\"detectionRange\":10}", data);
            if (turret)
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Game/Data/Weapons/Ranged/StandardRifle.asset");
                var settings = new SerializedObject(data); settings.FindProperty("weapon").objectReferenceValue = weapon; settings.ApplyModifiedPropertiesWithoutUndo();
            }
            return data;
        }
        private object Read(ScriptableObject data, string name) => data.GetType().GetProperty(name).GetValue(data);
        private bool Valid(ScriptableObject data)
        {
            object[] args = { null }; bool valid = (bool)data.GetType().GetMethod("IsValid").Invoke(data, args);
            Assert.That(args[0], valid ? Is.Null : Is.Not.Null.And.Not.Empty); return valid;
        }
        [TearDown] public void Cleanup()
        {
            if (folder != null && AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
            foreach (var item in owned) if (item && !AssetDatabase.Contains(item)) Object.DestroyImmediate(item);
            owned.Clear(); folder = null;
        }
        [TestCase(false)] [TestCase(true)] public void AuthoredDefinitionSurvivesAssetReload(bool turret)
        {
            var data = Data(turret); data.name = "Sample";
            JsonUtility.FromJsonOverwrite("{\"displayName\":\"테스트 시설\",\"description\":\"설명\\n두 번째 줄\",\"maxHP\":2500}", data);
            var weapon = turret ? Read(data, "Weapon") : null;
            folder = "Assets/StationTurretTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            string path = folder + "/Data.asset"; var type = data.GetType();
            AssetDatabase.CreateAsset(data, path); AssetDatabase.SaveAssets(); Resources.UnloadAsset(data);
            data = AssetDatabase.LoadAssetAtPath(path, type) as ScriptableObject;
            Assert.That(Read(data, "ObjectId"), Is.EqualTo("test")); Assert.That(Read(data, "DisplayName"), Is.EqualTo("테스트 시설"));
            Assert.That(Read(data, "Description"), Is.EqualTo("설명\n두 번째 줄")); Assert.That(Read(data, "MaxHP"), Is.EqualTo(2500));
            Assert.That(Read(data, "ModelPrefab"), Is.Null); Assert.That(Valid(data), Is.True);
            if (turret)
            {
                Assert.That(Read(data, "BaseAP"), Is.EqualTo(5)); Assert.That(Read(data, "DetectionRange"), Is.EqualTo(10));
                Assert.That(Read(data, "Weapon"), Is.SameAs(weapon));
                Assert.That(DataType("DestructibleObjectData").IsInstanceOfType(data), Is.True);
            }
        }
        [TestCase(false)] [TestCase(true)] public void NameFallsBackToAssetName(bool turret)
        { var data = Data(turret); data.name = "UnnamedObject"; Assert.That(Read(data, "DisplayName"), Is.EqualTo("UnnamedObject")); Assert.That(Valid(data), Is.True); }
        [TestCase(false, "objectId", "")] [TestCase(true, "objectId", " ")]
        [TestCase(false, "maxHP", 0)] [TestCase(true, "maxHP", -1)]
        [TestCase(true, "baseAP", 0)] [TestCase(true, "detectionRange", -1)]
        public void InvalidDefinitionsAreRejected(bool turret, string field, object value)
        {
            var data = Data(turret); var settings = new SerializedObject(data); var property = settings.FindProperty(field);
            if (value is string text) property.stringValue = text; else property.intValue = (int)value;
            settings.ApplyModifiedPropertiesWithoutUndo(); Assert.That(Valid(data), Is.False);
        }
        [Test] public void MissingMeleeInvalidOrUnaffordableWeaponIsRejected()
        {
            var data = Data(true); var settings = new SerializedObject(data);
            settings.FindProperty("weapon").objectReferenceValue = null; settings.ApplyModifiedPropertiesWithoutUndo(); Assert.That(Valid(data), Is.False);
            var weapon = ScriptableObject.CreateInstance<WeaponData>(); owned.Add(weapon);
            settings.FindProperty("weapon").objectReferenceValue = weapon; settings.ApplyModifiedPropertiesWithoutUndo();
            JsonUtility.FromJsonOverwrite("{\"attackType\":1}", weapon); Assert.That(Valid(data), Is.False);
            JsonUtility.FromJsonOverwrite("{\"attackType\":0,\"apCost\":6}", weapon); Assert.That(Valid(data), Is.False);
            JsonUtility.FromJsonOverwrite("{\"apCost\":3,\"minDamage\":0}", weapon); Assert.That(Valid(data), Is.False);
            JsonUtility.FromJsonOverwrite("{\"minDamage\":4}", weapon); Assert.That(Valid(data), Is.True);
        }
        [TestCase(false, "SampleCommunicationStation", 2000)] [TestCase(true, "SampleFixedTurret", 2500)]
        public void SavedSamplesLoadWithIndependentValidIdentity(bool turret, string name, int hp)
        {
            var data = AssetDatabase.LoadAssetAtPath("Assets/_Game/Data/Objects/" + name + ".asset", DataType(turret ? "TurretData" : "DestructibleObjectData")) as ScriptableObject;
            Assert.That(data, Is.Not.Null); Assert.That(Valid(data), Is.True); Assert.That(Read(data, "MaxHP"), Is.EqualTo(hp));
            Assert.That(Read(data, "ObjectId"), Is.EqualTo(turret ? "sample_fixed_turret" : "sample_communication_station"));
            Assert.That(Read(data, "DisplayName"), Is.Not.Empty); Assert.That(Read(data, "Description"), Is.Not.Empty);
            if (turret) Assert.That(Read(data, "Weapon"), Is.SameAs(AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Game/Data/Weapons/Ranged/StandardRifle.asset")));
        }
    }
}
