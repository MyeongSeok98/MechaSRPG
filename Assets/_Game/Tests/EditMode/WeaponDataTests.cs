using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class WeaponDataTests
    {
        private WeaponData weapon;
        private string folder;
        [SetUp] public void Setup()
        {
            weapon = ScriptableObject.CreateInstance<WeaponData>();
            folder = "Assets/WeaponDataTests_" + Guid.NewGuid().ToString("N");
        }
        [TearDown] public void Cleanup()
        {
            if (weapon && !AssetDatabase.Contains(weapon)) Object.DestroyImmediate(weapon);
            if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
        }
        private void Set(string field, object value) => typeof(WeaponData)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, value);
        private void Bands(params DistanceAccuracyBand[] bands) => Set("distanceBands", bands);
        private static DistanceAccuracyBand Band(int max, float multiplier) =>
            new DistanceAccuracyBand { MaxDistance = max, Multiplier = multiplier };

        [TestCase(AttackType.Ranged, 0)]
        [TestCase(AttackType.Ranged, 1)]
        [TestCase(AttackType.Ranged, 2)]
        [TestCase(AttackType.Melee, 0)]
        [TestCase(AttackType.Melee, 1)]
        public void CreateAndReloadAllFiveCategories(AttackType type, int category)
        {
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            var serialized = new SerializedObject(weapon);
            serialized.FindProperty("attackType").intValue = (int)type;
            serialized.FindProperty(type == AttackType.Ranged ? "rangedCategory" : "meleeCategory").intValue = category;
            serialized.FindProperty("displayName").stringValue = "Test weapon";
            serialized.FindProperty("minDamage").intValue = 4;
            serialized.FindProperty("maxDamage").intValue = 6;
            serialized.FindProperty("weightTonnes").floatValue = 2.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            string path = folder + "/Weapon.asset";
            AssetDatabase.CreateAsset(weapon, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var loaded = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            Assert.That(loaded.IsValid(out var reason), Is.True, reason);
            Assert.That(loaded.AttackType, Is.EqualTo(type));
            Assert.That(type == AttackType.Ranged ? (int)loaded.RangedCategory : (int)loaded.MeleeCategory, Is.EqualTo(category));
            Assert.That(loaded.DisplayName, Is.EqualTo("Test weapon"));
            Assert.That(loaded.MinDamage, Is.EqualTo(4));
            Assert.That(loaded.MaxDamage, Is.EqualTo(6));
            Assert.That(loaded.WeightTonnes, Is.EqualTo(2.5f));
            Assert.That(File.ReadAllText(path), Does.Contain("weightTonnes: 2.5"));
        }
        [Test] public void BandsAreInclusive()
        {
            Bands(Band(2, .9f), Band(5, 1), Band(7, .8f));
            foreach (long distance in new long[] { 1, 2 }) Assert.That(weapon.GetDistanceHitMultiplier(distance), Is.EqualTo(.9f));
            foreach (long distance in new long[] { 3, 5 }) Assert.That(weapon.GetDistanceHitMultiplier(distance), Is.EqualTo(1));
            foreach (long distance in new long[] { 6, 7 }) Assert.That(weapon.GetDistanceHitMultiplier(distance), Is.EqualTo(.8f));
            Assert.That(weapon.GetDistanceHitMultiplier(8), Is.EqualTo(1));
            Assert.That(weapon.GetDistanceHitMultiplier(long.MaxValue), Is.EqualTo(1));
            Assert.That(weapon.GetDistanceHitMultiplier(0), Is.EqualTo(1));
            Bands(Band(2, .9f));
            Assert.That(weapon.GetDistanceHitMultiplier(3), Is.EqualTo(1));
            Bands(); Assert.That(weapon.GetDistanceHitMultiplier(3), Is.EqualTo(1));
        }
        [TestCase("minDamage", 0)] [TestCase("maxDamage", 0)]
        [TestCase("baseHitChance", -1)] [TestCase("baseHitChance", 101)]
        [TestCase("range", 0)] [TestCase("apCost", 0)]
        public void InvalidNumbersRejectedAtRuntime(string field, int value)
        {
            Set(field, value);
            Assert.That(weapon.IsValid(out var reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
        }
        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidWeightRejected(float value)
        {
            Set("weightTonnes", value); Assert.That(weapon.IsValid(out _), Is.False);
        }
        [TestCase(.9f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidGripMultiplierRejected(float value)
        {
            Set("twoHandHitMultiplier", value); Assert.That(weapon.IsValid(out _), Is.False);
        }
        [Test] public void InvalidBandsRejectedWithoutReordering()
        {
            foreach (var bands in new[] {
                new[] { Band(2, 1), Band(2, .8f) }, new[] { Band(5, 1), Band(2, .8f) },
                new[] { Band(8, 1) }, new[] { Band(0, 1) }, new[] { Band(2, -1) },
                new[] { Band(2, float.NaN) }, new[] { Band(2, float.PositiveInfinity) } })
            {
                Bands(bands); Assert.That(weapon.IsValid(out var reason), Is.False);
                Assert.That(reason, Is.Not.Empty);
            }
            Bands(Band(2, 0)); Assert.That(weapon.IsValid(out _), Is.True);
        }
        [Test] public void InvalidEnumsRejected()
        {
            Set("attackType", (AttackType)99); Assert.That(weapon.IsValid(out _), Is.False);
            Set("attackType", AttackType.Ranged);
            Set("rangedCategory", (RangedWeaponCategory)99); Assert.That(weapon.IsValid(out _), Is.False);
            Set("rangedCategory", RangedWeaponCategory.Beam);
            Set("gripRequirement", (GripRequirement)99); Assert.That(weapon.IsValid(out _), Is.False);
            Set("gripRequirement", GripRequirement.Either);
            Set("attackType", AttackType.Melee);
            Set("meleeCategory", (MeleeWeaponCategory)99); Assert.That(weapon.IsValid(out _), Is.False);
        }
        [Test] public void MeleeIgnoresDistanceBands()
        {
            Set("attackType", AttackType.Melee); Bands(Band(999, float.NaN));
            Assert.That(weapon.IsValid(out var reason), Is.True, reason);
            Assert.That(weapon.GetDistanceHitMultiplier(1), Is.EqualTo(1));
        }
        [Test] public void NameFallbackAndFixedDamageAreSupported()
        {
            weapon.name = "Fallback"; Set("displayName", "  ");
            Set("minDamage", 5); Set("maxDamage", 5);
            Assert.That(weapon.DisplayName, Is.EqualTo("Fallback"));
            Assert.That(weapon.IsValid(out var reason), Is.True, reason);
            Set("minDamage", 6); Assert.That(weapon.IsValid(out _), Is.False);
        }
        [Test] public void DuplicateCanBeEditedIndependently()
        {
            var copy = Object.Instantiate(weapon);
            try
            {
                var serialized = new SerializedObject(copy);
                serialized.FindProperty("minDamage").intValue = 3;
                serialized.FindProperty("maxDamage").intValue = 9;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(copy.MinDamage, Is.EqualTo(3)); Assert.That(copy.MaxDamage, Is.EqualTo(9));
                Assert.That(weapon.MinDamage, Is.EqualTo(4)); Assert.That(weapon.MaxDamage, Is.EqualTo(6));
            }
            finally { Object.DestroyImmediate(copy); }
        }
        [Test] public void SampleAssetsCanBeDuplicatedWithoutChangingOriginal()
        {
            var rifle = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Game/Data/Weapons/Ranged/StandardRifle.asset");
            var blade = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Game/Data/Weapons/Melee/StandardBlade.asset");
            Assert.That(rifle, Is.Not.Null); Assert.That(blade, Is.Not.Null);
            Assert.That(rifle.IsValid(out var rifleReason), Is.True, rifleReason);
            Assert.That(blade.IsValid(out var bladeReason), Is.True, bladeReason);
            Assert.That(rifle.MinDamage, Is.EqualTo(4)); Assert.That(rifle.MaxDamage, Is.EqualTo(6));
            Assert.That(rifle.GripRequirement, Is.EqualTo(GripRequirement.Either));
            Assert.That(rifle.TwoHandHitMultiplier, Is.EqualTo(1.1f));
            Assert.That(rifle.GetDistanceHitMultiplier(2), Is.EqualTo(.9f));
            Assert.That(rifle.GetDistanceHitMultiplier(5), Is.EqualTo(1));
            Assert.That(rifle.GetDistanceHitMultiplier(7), Is.EqualTo(.8f));
            Assert.That(blade.AttackType, Is.EqualTo(AttackType.Melee));
            Assert.That(blade.MinDamage, Is.EqualTo(7)); Assert.That(blade.MaxDamage, Is.EqualTo(9));
            Assert.That(blade.GripRequirement, Is.EqualTo(GripRequirement.OneHandOnly));
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            string duplicate = folder + "/RifleCopy.asset";
            Assert.That(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(rifle), duplicate), Is.True);
            var copy = AssetDatabase.LoadAssetAtPath<WeaponData>(duplicate);
            var serialized = new SerializedObject(copy);
            serialized.FindProperty("displayName").stringValue = "Custom Rifle";
            serialized.FindProperty("maxDamage").intValue = 12;
            serialized.FindProperty("distanceBands").GetArrayElementAtIndex(0).FindPropertyRelative("Multiplier").floatValue = .5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(copy);
            Assert.That(copy.MaxDamage, Is.EqualTo(12));
            Assert.That(copy.GetDistanceHitMultiplier(1), Is.EqualTo(.5f));
            Assert.That(rifle.MaxDamage, Is.EqualTo(6));
            Assert.That(rifle.GetDistanceHitMultiplier(1), Is.EqualTo(.9f));
        }
        [Test] public void InspectorValidationClampsNumbersButKeepsInvalidBandsVisible()
        {
            Set("minDamage", -5); Set("maxDamage", -8); Set("baseHitChance", 150);
            Set("range", 0); Set("apCost", 0); Set("weightTonnes", float.NaN);
            Set("twoHandHitMultiplier", float.PositiveInfinity);
            Bands(Band(5, 1), Band(2, .8f));
            typeof(WeaponData).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(weapon, null);
            Assert.That(weapon.MinDamage, Is.EqualTo(1)); Assert.That(weapon.MaxDamage, Is.EqualTo(1));
            Assert.That(weapon.BaseHitChance, Is.EqualTo(100)); Assert.That(weapon.Range, Is.EqualTo(1));
            Assert.That(weapon.APCost, Is.EqualTo(1)); Assert.That(weapon.WeightTonnes, Is.EqualTo(0));
            Assert.That(weapon.TwoHandHitMultiplier, Is.EqualTo(1));
            Assert.That(weapon.IsValid(out _), Is.False);
        }
    }
}
