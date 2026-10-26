using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class MechaDataTests
    {
        private ScriptableObject data;
        private Type type;
        private string folder;
        [SetUp] public void Setup()
        {
            type = typeof(BattleUnit).Assembly.GetType("MechaXCOM.MechaData");
            Assert.That(type, Is.Not.Null, "The requested MechaData asset type must exist.");
            data = ScriptableObject.CreateInstance(type);
            folder = "Assets/MechaDataTests_" + Guid.NewGuid().ToString("N");
        }
        [TearDown] public void Cleanup()
        {
            if (data && !AssetDatabase.Contains(data)) Object.DestroyImmediate(data);
            if (folder != null && AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
        }
        private object Read(string name) => type.GetProperty(name).GetValue(data);
        private void Set(string name, object value) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(data, value);
        private bool Valid()
        {
            object[] args = { null };
            bool valid = (bool)type.GetMethod("IsValid").Invoke(data, args);
            if (!valid) Assert.That(args[0], Is.Not.Null.And.Not.Empty);
            return valid;
        }
        private FieldInfo ArmorField()
        {
            var field = type.GetField("armorType", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "MechaData needs a serialized armor selection.");
            Assert.That(type.GetProperty("ArmorType"), Is.Not.Null);
            return field;
        }
        [Test] public void LegacyMechaDataWithoutArmorDefaultsToStandard()
        {
            var field = ArmorField();
            JsonUtility.FromJsonOverwrite("{\"mechaId\":\"legacy\",\"baseAP\":7}", data);
            Assert.That(Convert.ToInt32(field.GetValue(data)), Is.Zero);
            Assert.That(Read("ArmorType").ToString(), Is.EqualTo("Standard"));
            Assert.That(Valid(), Is.True);
            Assert.That(Read("BaseAP"), Is.EqualTo(7));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ArmorSelectionPersistsThroughAssetUnloadAndReload(int value)
        {
            var field = ArmorField(); Set("mechaId", "armor_test");
            field.SetValue(data, Enum.ToObject(field.FieldType, value));
            Set("bodyMaxHP", 3500);
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            string path = folder + "/Armor.asset";
            AssetDatabase.CreateAsset(data, path); AssetDatabase.SaveAssetIfDirty(data);
            Resources.UnloadAsset(data); data = null;
            data = AssetDatabase.LoadAssetAtPath<MechaData>(path);
            Assert.That(Convert.ToInt32(Read("ArmorType")), Is.EqualTo(value));
            Assert.That(((MechaData)data).GetMaxHP(TargetBodyPart.Torso), Is.EqualTo(3500));
            Assert.That(Valid(), Is.True);
        }
        [TestCase(-1)] [TestCase(3)] [TestCase(99)]
        public void UndefinedArmorTypeIsRejected(int value)
        {
            var field = ArmorField(); Set("mechaId", "invalid_armor");
            field.SetValue(data, Enum.ToObject(field.FieldType, value));
            Assert.That(Valid(), Is.False);
        }
        [Test] public void DuplicatedMechaCanChooseDifferentArmorWithoutChangingOriginal()
        {
            var field = ArmorField(); Set("mechaId", "original_armor");
            field.SetValue(data, Enum.ToObject(field.FieldType, 1));
            var copy = Object.Instantiate(data);
            try
            {
                var serialized = new SerializedObject(copy);
                serialized.FindProperty("armorType").enumValueIndex = 2;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(Convert.ToInt32(type.GetProperty("ArmorType").GetValue(copy)), Is.EqualTo(2));
                Assert.That(Convert.ToInt32(Read("ArmorType")), Is.EqualTo(1));
                Assert.That(((MechaData)copy).GetMaxHP(TargetBodyPart.Torso), Is.EqualTo(20));
            }
            finally { Object.DestroyImmediate(copy); }
        }
        [Test] public void AllRequestedFieldsSurviveAssetReload()
        {
            Set("mechaId", "test_mecha"); Set("displayName", "테스트 기체"); Set("description", "설명\n두 번째 줄");
            Set("accuracy", 35); Set("evasion", 20);
            Set("baseAP", 7); Set("movementCellsPerAP", .5f); Set("chassisWeightTonnes", 12.5f); Set("equipmentWeightLimitTonnes", 4.5f);
            string[] fields = { "bodyMaxHP", "cockpitMaxHP", "headMaxHP", "leftArmMaxHP", "rightArmMaxHP", "leftLegMaxHP", "rightLegMaxHP" };
            for (int i = 0; i < fields.Length; i++) Set(fields[i], 10 + i);
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            string path = folder + "/Mecha.asset";
            AssetDatabase.CreateAsset(data, path); AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            data = (ScriptableObject)AssetDatabase.LoadAssetAtPath(path, type);
            Assert.That(Valid(), Is.True);
            Assert.That(Read("MechaId"), Is.EqualTo("test_mecha"));
            Assert.That(Read("DisplayName"), Is.EqualTo("테스트 기체")); Assert.That(Read("Description"), Is.EqualTo("설명\n두 번째 줄"));
            Assert.That(Read("Accuracy"), Is.EqualTo(35)); Assert.That(Read("Evasion"), Is.EqualTo(20));
            Assert.That(Read("BaseAP"), Is.EqualTo(7)); Assert.That(Read("MovementCellsPerAP"), Is.EqualTo(.5f));
            Assert.That(Read("ChassisWeightTonnes"), Is.EqualTo(12.5f)); Assert.That(Read("EquipmentWeightLimitTonnes"), Is.EqualTo(4.5f));
            for (int i = 0; i < fields.Length; i++)
                Assert.That(type.GetMethod("GetMaxHP").Invoke(data, new object[] { (TargetBodyPart)(i + 1) }), Is.EqualTo(10 + i));
            Assert.That(type.GetMethod("GetMaxHP").Invoke(data, new object[] { TargetBodyPart.Whole }), Is.EqualTo(10));
            Assert.That(type.GetMethod("GetMaxHP").Invoke(data, new object[] { (TargetBodyPart)99 }), Is.Zero);
        }
        [TestCase("baseAP", 0)] [TestCase("bodyMaxHP", 0)] [TestCase("cockpitMaxHP", 0)]
        [TestCase("headMaxHP", 0)] [TestCase("leftArmMaxHP", 0)] [TestCase("rightArmMaxHP", 0)]
        [TestCase("leftLegMaxHP", 0)] [TestCase("rightLegMaxHP", 0)]
        public void NonpositiveAPAndPartHPRejected(string field, int value)
        { Set("mechaId", "valid"); Set(field, value); Assert.That(Valid(), Is.False); }
        [TestCase("movementCellsPerAP", 0f)] [TestCase("movementCellsPerAP", -1f)]
        [TestCase("movementCellsPerAP", float.NaN)] [TestCase("movementCellsPerAP", float.PositiveInfinity)]
        [TestCase("chassisWeightTonnes", -1f)] [TestCase("chassisWeightTonnes", float.NaN)]
        [TestCase("chassisWeightTonnes", float.PositiveInfinity)]
        [TestCase("equipmentWeightLimitTonnes", -1f)] [TestCase("equipmentWeightLimitTonnes", float.NaN)]
        [TestCase("equipmentWeightLimitTonnes", float.PositiveInfinity)]
        public void InvalidMovementAndWeightsRejected(string field, float value)
        { Set("mechaId", "valid"); Set(field, value); Assert.That(Valid(), Is.False); }
        [Test] public void IdIsRequiredAndNameFallsBackToAssetName()
        {
            Assert.That(Valid(), Is.False);
            Set("mechaId", "  "); Assert.That(Valid(), Is.False);
            Set("mechaId", "valid"); data.name = "Fallback"; Set("displayName", " ");
            Assert.That(Valid(), Is.True); Assert.That(Read("DisplayName"), Is.EqualTo("Fallback"));
        }
        [Test] public void AuthoringPreservesSmallPositiveMovementEfficiency()
        {
            Set("mechaId", "slow_mecha"); Set("movementCellsPerAP", .005f);
            type.GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(data, null);
            Assert.That(Read("MovementCellsPerAP"), Is.EqualTo(.005f));
            Assert.That(Valid(), Is.True);
        }
        [Test] public void DuplicateHasIndependentBaseSpecs()
        {
            Set("mechaId", "original");
            var copy = Object.Instantiate(data);
            try
            {
                type.GetField("baseAP", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(copy, 9);
                Assert.That(type.GetProperty("BaseAP").GetValue(copy), Is.EqualTo(9));
                Assert.That(Read("BaseAP"), Is.EqualTo(5));
                Assert.That(type.GetProperty("MechaId").GetValue(copy), Is.EqualTo("original"), "Duplication requires assigning a new ID manually.");
            }
            finally { Object.DestroyImmediate(copy); }
        }
        [Test] public void SampleAssetAndKoreanInspectorAreAvailable()
        {
            var sample = AssetDatabase.LoadAssetAtPath("Assets/_Game/Data/Mecha/SampleMecha.asset", type);
            Assert.That(sample, Is.Not.Null);
            Assert.That(type.GetProperty("MechaId").GetValue(sample), Is.EqualTo("sample_mecha"));
            object[] args = { null };
            Assert.That(type.GetMethod("IsValid").Invoke(sample, args), Is.True, args[0] as string);
            var editor = UnityEditor.Editor.CreateEditor(sample);
            try { Assert.That(editor.GetType().Name, Is.EqualTo("MechaDataEditor")); }
            finally { Object.DestroyImmediate(editor); }
        }
    }
}
