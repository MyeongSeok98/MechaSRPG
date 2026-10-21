using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class CharacterDataTests
    {
        private Type type;
        private ScriptableObject data;
        private string folder;

        [SetUp]
        public void Setup()
        {
            type = typeof(BattleUnit).Assembly.GetType("MechaXCOM.CharacterData");
            Assert.That(type, Is.Not.Null, "The requested CharacterData asset type must exist.");
            data = ScriptableObject.CreateInstance(type);
        }

        [TearDown]
        public void Cleanup()
        {
            if (folder != null && AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
            if (data && !AssetDatabase.Contains(data)) Object.DestroyImmediate(data);
        }

        private void Set(string field, object value) => type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(data, value);
        private object Read(string property) => type.GetProperty(property).GetValue(data);
        private bool Valid()
        {
            object[] args = { null };
            bool valid = (bool)type.GetMethod("IsValid").Invoke(data, args);
            if (!valid) Assert.That(args[0], Is.Not.Null.And.Not.Empty);
            return valid;
        }

        [Test]
        public void AuthoredIdentityAndInitialStatsSurviveAssetReload()
        {
            Set("characterId", "pilot_alpha"); Set("displayName", "알파"); Set("description", "파일럿 설명\n둘째 줄");
            Set("initialLevel", 3); Set("shooting", 12); Set("melee", 7); Set("technique", 9); Set("evasion", 11);
            folder = "Assets/CharacterDataTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            string path = folder + "/Pilot.asset";
            AssetDatabase.CreateAsset(data, path); AssetDatabase.SaveAssets();
            Resources.UnloadAsset(data); data = (ScriptableObject)AssetDatabase.LoadAssetAtPath(path, type);
            Assert.That(Read("CharacterId"), Is.EqualTo("pilot_alpha"));
            Assert.That(Read("DisplayName"), Is.EqualTo("알파")); Assert.That(Read("Description"), Is.EqualTo("파일럿 설명\n둘째 줄"));
            Assert.That(Read("InitialLevel"), Is.EqualTo(3)); Assert.That(Read("Shooting"), Is.EqualTo(12));
            Assert.That(Read("Melee"), Is.EqualTo(7)); Assert.That(Read("Technique"), Is.EqualTo(9)); Assert.That(Read("Evasion"), Is.EqualTo(11));
            Assert.That(Valid(), Is.True);
        }

        [TestCase(null)] [TestCase("")] [TestCase(" ")]
        public void MissingIdentityIsRejected(string id) { Set("characterId", id); Assert.That(Valid(), Is.False); }

        [TestCase("initialLevel", 0)] [TestCase("initialLevel", -1)]
        [TestCase("shooting", -1)] [TestCase("melee", -1)] [TestCase("technique", -1)] [TestCase("evasion", -1)]
        public void InvalidInitialValuesAreRejected(string field, int value)
        { Set("characterId", "valid"); Set(field, value); Assert.That(Valid(), Is.False); }

        [Test]
        public void ZeroStatsAreValidAndBlankNameFallsBackToAssetName()
        {
            Set("characterId", "valid"); data.name = "Fallback Pilot"; Set("displayName", " ");
            Assert.That(Valid(), Is.True); Assert.That(Read("DisplayName"), Is.EqualTo("Fallback Pilot"));
        }

        [Test]
        public void AuthoringNormalizesNegativeValuesWithoutLosingValidStats()
        {
            Set("initialLevel", -2); Set("shooting", -3); Set("melee", -4); Set("technique", 25); Set("evasion", -5);
            type.GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(data, null);
            Assert.That(Read("InitialLevel"), Is.EqualTo(1)); Assert.That(Read("Shooting"), Is.Zero);
            Assert.That(Read("Melee"), Is.Zero); Assert.That(Read("Technique"), Is.EqualTo(25)); Assert.That(Read("Evasion"), Is.Zero);
        }

        [Test]
        public void SampleAssetIsLoadableAndValid()
        {
            var sample = AssetDatabase.LoadAssetAtPath("Assets/_Game/Data/Characters/SampleCharacter.asset", type);
            Assert.That(sample, Is.Not.Null);
            Assert.That(type.GetProperty("CharacterId").GetValue(sample), Is.EqualTo("sample_character"));
            object[] args = { null };
            Assert.That(type.GetMethod("IsValid").Invoke(sample, args), Is.True, args[0] as string);
        }
    }
}
