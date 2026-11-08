using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class ExplosiveDamageStepTests
    {
        private BackpackTestBattle b;
        private WeaponData data;
        [TearDown] public void Cleanup() { b?.Dispose(); if (data) Object.DestroyImmediate(data); }
        private void Create(int count = 1, int step = 5, float min = 0, float max = .2f)
        {
            b = new BackpackTestBattle(); ExplosiveTestSupport.Configure(b, count, min, max);
            ExplosiveTestSupport.Set(b.Weapon, "additionalDamageStepPercent", step);
        }
        private void StepRoll(Func<int, int> source)
        {
            var method = typeof(CombatSystem).GetMethod("SetAdditionalDamageStepRollSource"); Assert.That(method, Is.Not.Null);
            method.Invoke(b.Combat, new object[] { source });
        }
        private AttackResult Shot() => b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.RightHand);
        [Test] public void StepDefaultsToFiveAndOldWeaponAssetsRemainValid()
        {
            data = ScriptableObject.CreateInstance<WeaponData>();
            Assert.That(ExplosiveTestSupport.Property<int>(data, "AdditionalDamageStepPercent"), Is.EqualTo(5));
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponData", new[] { "Assets/_Game/Data/Weapons" }))
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(ExplosiveTestSupport.Property<int>(weapon, "AdditionalDamageStepPercent"), Is.EqualTo(5));
                Assert.That(weapon.IsValid(out var reason), Is.True, reason);
            }
        }
        [TestCase(0)] [TestCase(-1)] [TestCase(101)] public void InvalidStepRejectedAndAuthoringClamps(int step)
        {
            Create(step: step); Assert.That(b.Weapon.IsValid(out _), Is.False);
            typeof(WeaponData).GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(b.Weapon, null);
            Assert.That(ExplosiveTestSupport.Property<int>(b.Weapon, "AdditionalDamageStepPercent"), Is.InRange(1, 100));
        }
        [TestCase(.03f, .2f)] [TestCase(0f, .18f)] public void MisalignedBoundsRejectedWithoutSilentlyChangingThem(float min, float max)
        {
            Create(min: min, max: max); Assert.That(b.Weapon.IsValid(out var reason), Is.False); StringAssert.Contains("간격", reason);
            typeof(WeaponData).GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(b.Weapon, null);
            Assert.That(b.Weapon.AdditionalDamageMinRatio, Is.EqualTo(min)); Assert.That(b.Weapon.AdditionalDamageMaxRatio, Is.EqualTo(max));
        }
        [Test] public void StepPersistsAndWeaponsCanUseDifferentIntervals()
        {
            Create(step: 10); string folder = "Assets/ExplosionStep_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            try
            {
                data = Object.Instantiate(b.Weapon); string path = folder + "/Weapon.asset";
                AssetDatabase.CreateAsset(data, path); AssetDatabase.SaveAssets(); Resources.UnloadAsset(data);
                data = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
                Assert.That(ExplosiveTestSupport.Property<int>(data, "AdditionalDamageStepPercent"), Is.EqualTo(10));
                ExplosiveTestSupport.Set(b.Weapon, "additionalDamageStepPercent", 5);
                Assert.That(ExplosiveTestSupport.Property<int>(data, "AdditionalDamageStepPercent"), Is.EqualTo(10));
            }
            finally { AssetDatabase.DeleteAsset(folder); data = null; }
        }
        [TestCase(0, 0)] [TestCase(1, 40)] [TestCase(2, 80)] [TestCase(3, 120)] [TestCase(4, 160)]
        public void FivePointStepOffersAllFiveUniformIntegerIndices(int index, int damage)
        {
            Create(); int calls = 0;
            StepRoll(count => { Assert.That(count, Is.EqualTo(5)); calls++; return index; });
            var result = Shot(); Assert.That(result.Hit, Is.True);
            Assert.That(result.AdditionalPartDamage[0].DamageDealt, Is.EqualTo(damage)); Assert.That(calls, Is.EqualTo(1));
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(2)); Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(5));
        }
        [TestCase(0, 0)] [TestCase(1, 80)] [TestCase(2, 160)]
        public void TenPointStepUsesOnlyThreeLevels(int index, int damage)
        {
            Create(step: 10); StepRoll(count => { Assert.That(count, Is.EqualTo(3)); return index; });
            Assert.That(Shot().AdditionalPartDamage[0].DamageDealt, Is.EqualTo(damage));
        }
        [Test] public void EachAdditionalPartDrawsItsOwnStep()
        {
            Create(count: 2); var indices = new Queue<int>(new[] { 0, 4 }); StepRoll(_ => indices.Dequeue());
            CollectionAssert.AreEquivalent(new[] { 0, 160 }, Shot().AdditionalPartDamage.Select(x => x.DamageDealt)); Assert.That(indices, Is.Empty);
        }
        [TestCase(0f, 0)] [TestCase(.1f, 80)] public void SingleLevelWorksIncludingZero(float ratio, int damage)
        {
            Create(min: ratio, max: ratio); StepRoll(count => { Assert.That(count, Is.EqualTo(1)); return 0; });
            Assert.That(Shot().AdditionalPartDamage[0].DamageDealt, Is.EqualTo(damage));
        }
        [Test] public void LegacyRatioOverrideIsQuantizedToAllowedLevel()
        {
            Create(); ExplosiveTestSupport.Rolls(b.Combat, max => 0, (min, max) => .123f);
            Assert.That(Shot().AdditionalPartDamage[0].DamageDealt, Is.EqualTo(80), "12.3% must become10%, not continuous98damage.");
        }
        [Test] public void PreviewAndMissNeverDrawStepsAndPreviewShowsInterval()
        {
            Create(); StepRoll(_ => { Assert.Fail("No step roll for preview or miss"); return 0; });
            var preview = b.Combat.GetWeaponPreview(b.Player, b.Enemy, WeaponSlot.RightHand);
            Assert.That(ExplosiveTestSupport.Read<int>(preview, "AdditionalDamageStepPercent"), Is.EqualTo(5));
            StringAssert.Contains("5%p", AttackPreviewText.AdditionalDamage(preview));
            b.Combat.SetRollSource(() => 99); Assert.That(Shot().Hit, Is.False);
        }
        [Test] public void NonzeroMinimumUsesGlobalMultiplesAndSafeLargeDamage()
        {
            Create(step: 5, min: .05f, max: .15f);
            ExplosiveTestSupport.Set(b.Weapon, "minDamage", int.MaxValue); ExplosiveTestSupport.Set(b.Weapon, "maxDamage", int.MaxValue);
            b.Enemy.Health.ConfigurePartHP(TargetBodyPart.Head, int.MaxValue); b.Enemy.Health.Initialize();
            ExplosiveTestSupport.Rolls(b.Combat, max => 0, null);
            StepRoll(count => { Assert.That(count, Is.EqualTo(3)); return 2; });
            Assert.That(Shot().AdditionalPartDamage[0].DamageDealt, Is.EqualTo(322122547));
        }
    }
}
