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
    public class ExplosivePartDamageTests
    {
        private BackpackTestBattle b;
        private WeaponData data;
        [TearDown] public void Cleanup() { b?.Dispose(); if (data) Object.DestroyImmediate(data); }
        private void Battle(int count = 2, float min = .05f, float max = .15f)
        { b = new BackpackTestBattle(); ExplosiveTestSupport.Configure(b, count, min, max); }
        private AttackResult Shot(TargetBodyPart part = TargetBodyPart.Torso) => b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.RightHand, part);
        [Test] public void LegacyDefinitionWithoutAdditionalDamageFieldsDefaultsToNoAdditionalDamage()
        {
            data = ScriptableObject.CreateInstance<WeaponData>();
            Assert.That(ExplosiveTestSupport.Property<int>(data, "AdditionalHitPartCount"), Is.Zero);
            Assert.That(ExplosiveTestSupport.Property<bool>(data, "HasAdditionalPartDamage"), Is.False);
            // Legacy definitions omit the new fields. Authored explosive sample assets
            // may explicitly enable them and are not fixed-default test fixtures.
            JsonUtility.FromJsonOverwrite("{\"attackType\":0,\"rangedCategory\":2,\"minDamage\":4,\"maxDamage\":6}", data);
            Assert.That(data.AdditionalHitPartCount, Is.Zero);
            Assert.That(data.HasAdditionalPartDamage, Is.False);
        }
        [TestCase("additionalHitPartCount", -1)] [TestCase("additionalHitPartCount", 7)]
        [TestCase("additionalDamageMinRatio", -.1f)] [TestCase("additionalDamageMaxRatio", 1.1f)]
        [TestCase("additionalDamageMinRatio", float.NaN)] [TestCase("additionalDamageMaxRatio", float.PositiveInfinity)]
        public void InvalidExplosionSettingsAreRejected(string field, object value)
        {
            data = ScriptableObject.CreateInstance<WeaponData>(); ExplosiveTestSupport.Set(data, "rangedCategory", RangedWeaponCategory.Explosive);
            ExplosiveTestSupport.Set(data, field, value); Assert.That(data.IsValid(out _), Is.False);
        }
        [Test] public void InvertedRatiosAreRejectedAndAuthoringClamps()
        {
            data = ScriptableObject.CreateInstance<WeaponData>(); ExplosiveTestSupport.Set(data, "rangedCategory", RangedWeaponCategory.Explosive);
            ExplosiveTestSupport.Set(data, "additionalDamageMinRatio", .8f); ExplosiveTestSupport.Set(data, "additionalDamageMaxRatio", .2f);
            Assert.That(data.IsValid(out _), Is.False);
            typeof(WeaponData).GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(data, null);
            Assert.That(data.IsValid(out var reason), Is.True, reason);
        }
        [Test] public void ExplosionSettingsSurviveAssetReload()
        {
            string folder = "Assets/ExplosionPersistence_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            try
            {
                data = ScriptableObject.CreateInstance<WeaponData>();
                ExplosiveTestSupport.Set(data, "rangedCategory", RangedWeaponCategory.Explosive);
                ExplosiveTestSupport.Set(data, "additionalHitPartCount", 2);
                ExplosiveTestSupport.Set(data, "additionalDamageMinRatio", .05f); ExplosiveTestSupport.Set(data, "additionalDamageMaxRatio", .15f);
                string path = folder + "/Explosion.asset"; AssetDatabase.CreateAsset(data, path); AssetDatabase.SaveAssets(); Resources.UnloadAsset(data);
                data = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
                Assert.That(ExplosiveTestSupport.Property<int>(data, "AdditionalHitPartCount"), Is.EqualTo(2));
                Assert.That(ExplosiveTestSupport.Property<float>(data, "AdditionalDamageMinRatio"), Is.EqualTo(.05f));
                Assert.That(ExplosiveTestSupport.Property<float>(data, "AdditionalDamageMaxRatio"), Is.EqualTo(.15f));
                Assert.That(data.IsValid(out _), Is.True);
            }
            finally { AssetDatabase.DeleteAsset(folder); data = null; }
        }
        [Test] public void SelectedPartsAreUniqueExcludeDirectCockpitAndReceiveIndependentRatios()
        {
            Battle(); int ratioCalls = 0, indexCalls = 0;
            ExplosiveTestSupport.Rolls(b.Combat, max => { indexCalls++; return 0; }, (min, max) => ++ratioCalls == 1 ? min : max);
            var result = Shot(); var extras = ExplosiveTestSupport.Additional(result);
            Assert.That(result.DamageDealt, Is.EqualTo(800)); Assert.That(extras.Count, Is.EqualTo(2));
            CollectionAssert.AreEquivalent(new[] { 40, 120 }, extras.Select(x => x.DamageDealt));
            Assert.That(extras.Select(x => x.Part).Distinct().Count(), Is.EqualTo(2));
            Assert.That(extras.Any(x => x.Part == TargetBodyPart.Cockpit || x.Part == TargetBodyPart.Torso), Is.False);
            Assert.That(indexCalls, Is.EqualTo(2)); Assert.That(ratioCalls, Is.EqualTo(2));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Cockpit), Is.EqualTo(10000));
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(2)); Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(5));
        }
        [Test] public void TooFewCandidatesExcludeDestroyedPartsAndOnlyAffectSelectedMecha()
        {
            Battle(6, .1f, .1f); b.Enemy.Health.ApplyDamage(TargetBodyPart.Head, 10000);
            ExplosiveTestSupport.Rolls(b.Combat, max => max - 1, (min, max) => min);
            var result = Shot(TargetBodyPart.LeftArm); var extra = ExplosiveTestSupport.Additional(result);
            Assert.That(extra.Count, Is.EqualTo(4));
            Assert.That(extra.Any(x => x.Part == TargetBodyPart.Head || x.Part == TargetBodyPart.LeftArm || x.Part == TargetBodyPart.Cockpit), Is.False);
            Assert.That(b.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }
        [TestCase(false, 40)] [TestCase(true, 50)] public void SecondaryDamageUsesUnclampedModifiedDirectDamageExactlyOnce(bool critical, int secondary)
        {
            Battle(1, .05f, .05f); b.Enemy.Health.ConfigurePartHP(TargetBodyPart.Torso, 20); b.Enemy.Health.Initialize();
            b.Combat.SetCriticalRollSource(() => critical ? 0 : 99);
            ExplosiveTestSupport.Rolls(b.Combat, max => 0, (min, max) => min);
            var result = Shot(); Assert.That(result.DamageDealt, Is.EqualTo(20));
            Assert.That(ExplosiveTestSupport.Additional(result)[0].DamageDealt, Is.EqualTo(secondary));
            Assert.That(result.TargetStateAfter, Is.EqualTo(MechState.Destroyed));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(10000 - secondary));
        }
        [TestCase(false)] [TestCase(true)] public void MissOrRejectedAttackDoesNotRollAdditionalDamage(bool rejected)
        {
            Battle(); int beforeAmmo = b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded;
            ExplosiveTestSupport.Rolls(b.Combat, max => { Assert.Fail("No part roll"); return 0; }, (min, max) => { Assert.Fail("No ratio roll"); return 0; });
            if (rejected) b.Player.ActionPoints.TrySpend(5); else b.Combat.SetRollSource(() => 99);
            var result = Shot(); Assert.That(result.WasExecuted, Is.EqualTo(!rejected)); Assert.That(result.Hit, Is.False);
            Assert.That(ExplosiveTestSupport.Additional(result), Is.Null.Or.Empty);
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(beforeAmmo - (rejected ? 0 : 1)));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(10000));
        }
        [Test] public void PreviewDescribesPotentialDamageWithoutConsumingRandomnessOrResources()
        {
            Battle(); ExplosiveTestSupport.Rolls(b.Combat, max => { Assert.Fail("Preview cannot pick parts"); return 0; }, (min, max) => { Assert.Fail("Preview cannot roll ratios"); return 0; });
            var preview = b.Combat.GetWeaponPreview(b.Player, b.Enemy, WeaponSlot.RightHand);
            Assert.That(ExplosiveTestSupport.Read<int>(preview, "AdditionalHitPartCount"), Is.EqualTo(2));
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(5)); Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(6));
            var method = typeof(AttackPreviewText).GetMethod("AdditionalDamage"); Assert.That(method, Is.Not.Null);
            string text = (string)method.Invoke(null, new object[] { preview }); StringAssert.Contains("2", text); StringAssert.Contains("5", text); StringAssert.Contains("15", text);
        }
        [TestCase(RangedWeaponCategory.Beam)] [TestCase(RangedWeaponCategory.Ballistic)]
        public void NonExplosiveWeaponsIgnoreExtraSettings(RangedWeaponCategory category)
        {
            Battle(); ExplosiveTestSupport.Set(b.Weapon, "rangedCategory", category);
            ExplosiveTestSupport.Rolls(b.Combat, max => { Assert.Fail("Not explosive"); return 0; }, (min, max) => 0);
            var result = Shot(); Assert.That(ExplosiveTestSupport.Additional(result), Is.Null.Or.Empty);
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(10000));
        }
        [Test] public void MeleeAndDisabledExplosionDoNotAddDamage()
        {
            b = new BackpackTestBattle(AttackType.Melee); ExplosiveTestSupport.Configure(b);
            ExplosiveTestSupport.Rolls(b.Combat, max => { Assert.Fail("Melee cannot fragment"); return 0; }, (min, max) => 0);
            Assert.That(ExplosiveTestSupport.Additional(Shot()), Is.Null.Or.Empty);
            b.Dispose(); b = null; Battle(0);
            ExplosiveTestSupport.Rolls(b.Combat, max => { Assert.Fail("Disabled"); return 0; }, (min, max) => 0);
            Assert.That(ExplosiveTestSupport.Additional(Shot()), Is.Null.Or.Empty);
        }
        [Test] public void ZeroPercentSelectionDoesNotDamageExtraPart()
        {
            Battle(1, 0, 0); ExplosiveTestSupport.Rolls(b.Combat, max => 0, (min, max) => 0);
            var result = Shot(); var extra = ExplosiveTestSupport.Additional(result);
            Assert.That(extra.Count, Is.EqualTo(1)); Assert.That(extra[0].DamageDealt, Is.Zero); Assert.That(extra[0].Applied, Is.False);
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(10000));
        }
        [Test] public void CockpitDirectHitCanSelectAllSixOtherPartsWithoutRepeatingCockpit()
        {
            Battle(6, .1f, .1f); ExplosiveTestSupport.Rolls(b.Combat, max => 0, (min, max) => min);
            var result = Shot(TargetBodyPart.Cockpit); var extra = ExplosiveTestSupport.Additional(result);
            Assert.That(result.DamageDealt, Is.EqualTo(800)); Assert.That(extra.Count, Is.EqualTo(6));
            Assert.That(extra.Select(x => x.Part).Distinct().Count(), Is.EqualTo(6));
            Assert.That(extra.Any(x => x.Part == TargetBodyPart.Cockpit), Is.False);
        }
    }
}
