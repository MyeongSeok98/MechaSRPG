using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    internal static class BackpackStageOneSupport
    {
        internal static ScriptableObject Data(string id = "test_backpack", float weight = 1f, WeaponData weapon = null)
        {
            var type = typeof(WeaponData).Assembly.GetType("MechaXCOM.BackpackData");
            Assert.That(type, Is.Not.Null, "BackpackData must define integrated backpack equipment.");
            var data = ScriptableObject.CreateInstance(type);
            Set(data, "backpackId", id); Set(data, "weightTonnes", weight); Set(data, "weapon", weapon);
            return data;
        }
        internal static void Set(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(info, Is.Not.Null, field); info.SetValue(target, value);
        }
        internal static T Property<T>(object target, string name)
        {
            var property = target.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, name); return (T)property.GetValue(target);
        }
        internal static object Call(object target, string name, params object[] args)
        {
            var method = target.GetType().GetMethod(name); Assert.That(method, Is.Not.Null, name);
            return method.Invoke(target, args);
        }
        internal static bool Valid(object target, out string reason)
        {
            object[] args = { null }; bool valid = (bool)Call(target, "IsValid", args); reason = (string)args[0]; return valid;
        }
    }

    public class BackpackDataTests
    {
        [TestCase("SampleUnarmedBackpack", false)] [TestCase("SampleWeaponBackpack", true)]
        public void SavedSampleBackpacksLoadValidateAndKeepReferencesAfterReload(string name, bool armed)
        {
            string path = "Assets/_Game/Data/Backpacks/" + name + ".asset";
            var pack = AssetDatabase.LoadAssetAtPath<BackpackData>(path);
            Assert.That(pack, Is.Not.Null, "Sample backpack asset must be provided: " + path);
            Assert.That(pack.IsValid(out var reason), Is.True, reason);
            Assert.That(pack.DisplayName, Is.Not.Empty); Assert.That(pack.Description, Is.Not.Empty);
            string id = pack.BackpackId;
            Assert.That(pack.WeightTonnes, Is.GreaterThanOrEqualTo(0));
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Game/Data/Weapons/Defaults/FixedRanged_5_80_7_3.asset");
            Assert.That(pack.Weapon, armed ? Is.SameAs(weapon) : Is.Null);
            string before = JsonUtility.ToJson(pack);
            Resources.UnloadAsset(pack);
            var loaded = AssetDatabase.LoadAssetAtPath<BackpackData>(path);
            Assert.That(loaded.BackpackId, Is.EqualTo(id)); Assert.That(loaded.IsValid(out reason), Is.True, reason);
            Assert.That(loaded.Weapon, armed ? Is.SameAs(weapon) : Is.Null);
            Assert.That(JsonUtility.ToJson(loaded), Is.EqualTo(before));
            var other = AssetDatabase.LoadAssetAtPath<BackpackData>("Assets/_Game/Data/Backpacks/" +
                (armed ? "SampleUnarmedBackpack" : "SampleWeaponBackpack") + ".asset");
            Assert.That(other, Is.Not.Null); Assert.That(loaded.BackpackId, Is.Not.EqualTo(other.BackpackId));
        }
        [Test] public void LegacyHandOnlyBattleStillInitializesAndFiresWithoutBackpack()
        {
            using (var b = new MechaAccuracyTestBattle())
            {
                var loadout = b.Player.GetComponent<UnitWeaponLoadout>();
                Assert.That(loadout.Backpack, Is.Null); Assert.That(loadout.GetWeapon(WeaponSlot.Backpack), Is.Null);
                Assert.That(loadout.IsValid(out var reason), Is.True, reason);
                Assert.That(b.Player.Combat.GetAttackSlots(AttackType.Ranged), Has.No.Member(WeaponSlot.Backpack));
                var result = b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.RightHand);
                Assert.That(result.WasExecuted && result.Hit, Is.True);
                Assert.That(result.DamageDealt, Is.EqualTo(5)); Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(2));
            }
        }
        private readonly List<Object> owned = new List<Object>();
        private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        [TearDown] public void Cleanup() { foreach (var value in owned) if (value) Object.DestroyImmediate(value); owned.Clear(); }

        [Test] public void UnarmedBackpackIsValidAndKeepsMetadata()
        {
            var data = Own(BackpackStageOneSupport.Data());
            BackpackStageOneSupport.Set(data, "displayName", "일반 백팩");
            BackpackStageOneSupport.Set(data, "description", "무장이 없는 백팩");
            Assert.That(BackpackStageOneSupport.Valid(data, out var reason), Is.True, reason);
            Assert.That(BackpackStageOneSupport.Property<string>(data, "BackpackId"), Is.EqualTo("test_backpack"));
            Assert.That(BackpackStageOneSupport.Property<string>(data, "DisplayName"), Is.EqualTo("일반 백팩"));
            Assert.That(BackpackStageOneSupport.Property<string>(data, "Description"), Is.EqualTo("무장이 없는 백팩"));
            Assert.That(BackpackStageOneSupport.Property<WeaponData>(data, "Weapon"), Is.Null);
            Assert.That(BackpackStageOneSupport.Property<GameObject>(data, "ModelPrefab"), Is.Null);
            Assert.That(BackpackStageOneSupport.Property<float>(data, "WeightTonnes"), Is.EqualTo(1f));
        }
        [TestCase("")] [TestCase(" ")] [TestCase(null)]
        public void BackpackRequiresId(string id)
        {
            var data = Own(BackpackStageOneSupport.Data(id));
            Assert.That(BackpackStageOneSupport.Valid(data, out var reason), Is.False); Assert.That(reason, Is.Not.Empty);
        }
        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void BackpackRequiresFiniteNonnegativeWeight(float weight)
        {
            var data = Own(BackpackStageOneSupport.Data(weight: weight));
            Assert.That(BackpackStageOneSupport.Valid(data, out var reason), Is.False); Assert.That(reason, Is.Not.Empty);
        }
        [Test] public void IncludedWeaponMustBeValidAndBlankDisplayNameFallsBackToAssetName()
        {
            var weapon = Own(ScriptableObject.CreateInstance<WeaponData>());
            var data = Own(BackpackStageOneSupport.Data(weapon: weapon)); data.name = "Backpack asset";
            Assert.That(BackpackStageOneSupport.Valid(data, out _), Is.True);
            Assert.That(BackpackStageOneSupport.Property<string>(data, "DisplayName"), Is.EqualTo("Backpack asset"));
            BackpackStageOneSupport.Set(weapon, "minDamage", 0);
            Assert.That(BackpackStageOneSupport.Valid(data, out var reason), Is.False); Assert.That(reason, Is.Not.Empty);
        }
        [Test] public void BackpackAssetRoundTripPreservesWeaponAndModelReferences()
        {
            var data = Own(BackpackStageOneSupport.Data(weight: 1.5f));
            string folder = "Assets/_Game/Tests/GeneratedBackpack_" + Guid.NewGuid().ToString("N");
            var weapon = ScriptableObject.CreateInstance<WeaponData>();
            var modelSource = new GameObject("Backpack model");
            try
            {
                AssetDatabase.CreateFolder("Assets/_Game/Tests", folder.Substring(folder.LastIndexOf('/') + 1));
                AssetDatabase.CreateAsset(weapon, folder + "/Weapon.asset");
                var model = PrefabUtility.SaveAsPrefabAsset(modelSource, folder + "/Model.prefab");
                BackpackStageOneSupport.Set(data, "weapon", weapon); BackpackStageOneSupport.Set(data, "modelPrefab", model);
                BackpackStageOneSupport.Set(data, "description", "백팩 설명");
                string path = folder + "/Backpack.asset";
                AssetDatabase.CreateAsset(data, path); EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
                Resources.UnloadAsset(data);
                var loaded = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                Assert.That(BackpackStageOneSupport.Valid(loaded, out var reason), Is.True, reason);
                Assert.That(BackpackStageOneSupport.Property<string>(loaded, "BackpackId"), Is.EqualTo("test_backpack"));
                Assert.That(BackpackStageOneSupport.Property<string>(loaded, "Description"), Is.EqualTo("백팩 설명"));
                Assert.That(BackpackStageOneSupport.Property<float>(loaded, "WeightTonnes"), Is.EqualTo(1.5f));
                Assert.That(AssetDatabase.GetAssetPath(BackpackStageOneSupport.Property<WeaponData>(loaded, "Weapon")), Is.EqualTo(folder + "/Weapon.asset"));
                Assert.That(AssetDatabase.GetAssetPath(BackpackStageOneSupport.Property<GameObject>(loaded, "ModelPrefab")), Is.EqualTo(folder + "/Model.prefab"));
            }
            finally
            {
                Object.DestroyImmediate(modelSource);
                AssetDatabase.DeleteAsset(folder);
                if (weapon && !AssetDatabase.Contains(weapon)) Object.DestroyImmediate(weapon);
            }
        }
    }
}
