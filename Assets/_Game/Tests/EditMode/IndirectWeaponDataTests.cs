using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class IndirectWeaponDataTests
    {
        private WeaponData data;
        [SetUp] public void Setup() => data = ScriptableObject.CreateInstance<WeaponData>();
        [TearDown] public void Cleanup() { if (data) Object.DestroyImmediate(data); }
        private void Mode(int mode)
        {
            var field = typeof(WeaponData).GetField("rangedAttackMode", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null); field.SetValue(data, Enum.ToObject(field.FieldType, mode));
        }
        private void Indirect()
        {
            ExplosiveTestSupport.Set(data, "rangedCategory", RangedWeaponCategory.Explosive); Mode(1);
        }
        [Test] public void LegacyDefinitionsDefaultToDirectAndAuthoredWeaponAssetsRemainValid()
        {
            Assert.That(ExplosiveTestSupport.Property<bool>(data, "IsIndirect"), Is.False);
            JsonUtility.FromJsonOverwrite("{\"attackType\":0,\"rangedCategory\":2,\"range\":7}", data);
            Assert.That(data.IsIndirect, Is.False, "Definitions without an attack mode must stay direct.");
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponData", new[] { "Assets/_Game/Data/Weapons" }))
            {
                var existing = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(existing.IsValid(out var reason), Is.True, reason);
            }
        }
        [Test] public void IndirectDefaultsHaveRadiusAndFixedOnePart()
        {
            Indirect(); Assert.That(data.IsValid(out var reason), Is.True, reason);
            Assert.That(ExplosiveTestSupport.Property<bool>(data, "IsIndirect"), Is.True);
            Assert.That(ExplosiveTestSupport.Property<int>(data, "BlastRadius"), Is.EqualTo(1));
            Assert.That(ExplosiveTestSupport.Property<int>(data, "MinHitParts"), Is.EqualTo(1));
            Assert.That(ExplosiveTestSupport.Property<int>(data, "MaxHitParts"), Is.EqualTo(1));
        }
        [TestCase("blastRadius", 0)] [TestCase("blastRadius", -1)]
        [TestCase("minHitParts", 0)] [TestCase("maxHitParts", 7)] [TestCase("minHitParts", 2)]
        public void InvalidIndirectSettingsAreRejected(string field, int value)
        { Indirect(); ExplosiveTestSupport.Set(data, field, value); Assert.That(data.IsValid(out _), Is.False); }
        [TestCase(0)] [TestCase(1)] public void IndirectRejectsNonExplosiveCategories(int category)
        {
            Mode(1); ExplosiveTestSupport.Set(data, "rangedCategory", (RangedWeaponCategory)category);
            Assert.That(data.IsValid(out _), Is.False);
        }
        [Test] public void InvalidModeIsRejected()
        { Mode(99); Assert.That(data.IsValid(out _), Is.False); }
        [Test] public void IndirectIgnoresDirectFragmentSettings()
        {
            Indirect(); ExplosiveTestSupport.Set(data, "additionalHitPartCount", 3);
            ExplosiveTestSupport.Set(data, "additionalDamageMinRatio", .03f);
            ExplosiveTestSupport.Set(data, "additionalDamageStepPercent", 5);
            Assert.That(data.IsValid(out var reason), Is.True, reason);
            Assert.That(data.HasAdditionalPartDamage, Is.False);
        }
        [Test] public void IndirectFieldsSurviveAssetReload()
        {
            string folder = "Assets/IndirectData_" + Guid.NewGuid().ToString("N"); AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            try
            {
                Indirect(); ExplosiveTestSupport.Set(data, "blastRadius", 3);
                ExplosiveTestSupport.Set(data, "minHitParts", 1); ExplosiveTestSupport.Set(data, "maxHitParts", 4);
                string path = folder + "/Weapon.asset"; AssetDatabase.CreateAsset(data, path); AssetDatabase.SaveAssets(); Resources.UnloadAsset(data);
                data = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
                Assert.That(ExplosiveTestSupport.Property<bool>(data, "IsIndirect"), Is.True);
                Assert.That(ExplosiveTestSupport.Property<int>(data, "BlastRadius"), Is.EqualTo(3));
                Assert.That(ExplosiveTestSupport.Property<int>(data, "MaxHitParts"), Is.EqualTo(4));
            }
            finally { AssetDatabase.DeleteAsset(folder); data = null; }
        }
    }
}
