using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class DamageTakenPercentTests
    {
        private MechaData data;
        [SetUp] public void Setup()
        { data = ScriptableObject.CreateInstance<MechaData>(); JsonUtility.FromJsonOverwrite("{\"mechaId\":\"percent_test\"}", data); }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(data);
        private static int Read(object value, string name)
        {
            var property = value.GetType().GetProperty(name); Assert.That(property, Is.Not.Null, name);
            return (int)property.GetValue(value);
        }
        private void Set(int beam, int ballistic) => JsonUtility.FromJsonOverwrite(
            "{\"beamDamageTakenPercent\":" + beam + ",\"ballisticDamageTakenPercent\":" + ballistic + "}", data);
        [Test] public void NewDefaultsAre100AndExplicitValuesReplaceLegacyType()
        {
            Assert.That(Read(data, "BeamDamageTakenPercent"), Is.EqualTo(100));
            Assert.That(Read(data, "BallisticDamageTakenPercent"), Is.EqualTo(100));
            JsonUtility.FromJsonOverwrite("{\"armorType\":1}", data);
            Assert.That(Read(data, "BeamDamageTakenPercent"), Is.EqualTo(80));
            Assert.That(Read(data, "BallisticDamageTakenPercent"), Is.EqualTo(110));
            Set(100, 150);
            Assert.That(Read(data, "BeamDamageTakenPercent"), Is.EqualTo(100));
            Assert.That(Read(data, "BallisticDamageTakenPercent"), Is.EqualTo(150));
        }
        [TestCase(50, 150)] [TestCase(100, 100)] [TestCase(150, 50)]
        public void ValidRatesSurviveSerialization(int beam, int ballistic)
        {
            Set(beam, ballistic); Assert.That(data.IsValid(out var reason), Is.True, reason);
            var copy = ScriptableObject.CreateInstance<MechaData>();
            try { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(data), copy);
                Assert.That(Read(copy, "BeamDamageTakenPercent"), Is.EqualTo(beam));
                Assert.That(Read(copy, "BallisticDamageTakenPercent"), Is.EqualTo(ballistic)); }
            finally { Object.DestroyImmediate(copy); }
        }
        [TestCase(0, 100)] [TestCase(49, 100)] [TestCase(151, 100)]
        [TestCase(100, 0)] [TestCase(100, 49)] [TestCase(100, 151)]
        public void InvalidRatesAreRejected(int beam, int ballistic)
        { Set(beam, ballistic); Assert.That(data.IsValid(out _), Is.False); }
        [TestCase(50, 50)] [TestCase(100, 100)] [TestCase(150, 150)]
        public void ExplicitPercentControlsPreviewAndDamageOnce(int percent, int damage)
        {
            using (var b = new MechaAccuracyTestBattle())
            {
                Set(percent, 100); JsonUtility.FromJsonOverwrite("{\"armorType\":1,\"bodyMaxHP\":10000}", data);
                var target = b.Unit(new Vector2Int(5, 1), UnitTeam.Enemy, data);
                b.Turns.Visibility.Initialize(b.Grid, new[] { b.Player, b.Enemy, target });
                b.Player.Combat.ConfigureTestRanged(100, 80, 7, 3); b.Combat.SetCriticalRollSource(() => 99f);
                var preview = b.Combat.GetWeaponPreview(b.Player, target, WeaponSlot.RightHand);
                Assert.That(preview.CanAttack, Is.True, preview.FailureReason);
                Assert.That(preview.ArmorMultiplier, Is.EqualTo(percent / 100f));
                Assert.That(AttackPreviewText.DamageRange(preview), Is.EqualTo(damage.ToString()));
                Assert.That(b.Combat.TryAttackWithWeapon(b.Player, target, WeaponSlot.RightHand).DamageDealt, Is.EqualTo(damage));
            }
        }
        [Test] public void EachCategoryUsesItsOwnPercentAndExplosivesIgnoreBoth()
        {
            using (var b = new MechaAccuracyTestBattle())
            {
                Set(50, 150); JsonUtility.FromJsonOverwrite("{\"bodyMaxHP\":10000}", data);
                var target = b.Unit(new Vector2Int(5, 1), UnitTeam.Enemy, data);
                b.Player.Combat.ConfigureTestRanged(100, 80, 7, 3);
                var weapon = b.Player.GetComponent<UnitWeaponLoadout>().RightWeapon;
                foreach (var pair in new[] { (RangedWeaponCategory.Beam, .5f), (RangedWeaponCategory.Ballistic, 1.5f), (RangedWeaponCategory.Explosive, 1f) })
                {
                    JsonUtility.FromJsonOverwrite("{\"rangedCategory\":" + (int)pair.Item1 + "}", weapon);
                    Assert.That(b.Combat.GetWeaponPreview(b.Player, target, WeaponSlot.RightHand).ArmorMultiplier, Is.EqualTo(pair.Item2));
                }
            }
        }
        [Test] public void UnitSnapshotsBasicsAndNullDataIsNeutral()
        {
            using (var b = new MechaAccuracyTestBattle())
            {
                Set(60, 140); var target = b.Unit(new Vector2Int(5, 1), UnitTeam.Enemy, data);
                Set(150, 50); Assert.That(target.Initialize(b.Grid), Is.True);
                Assert.That(Read(target, "BeamDamageTakenPercent"), Is.EqualTo(60));
                Assert.That(Read(target, "BallisticDamageTakenPercent"), Is.EqualTo(140));
                var neutral = b.Unit(new Vector2Int(7, 1), UnitTeam.Enemy, null);
                Assert.That(Read(neutral, "BeamDamageTakenPercent"), Is.EqualTo(100));
                Assert.That(Read(neutral, "BallisticDamageTakenPercent"), Is.EqualTo(100));
            }
        }
        [Test] public void InspectorMixedIndicatorComparesEffectiveRatherThanLegacyStorage()
        {
            JsonUtility.FromJsonOverwrite("{\"armorType\":1}", data);
            var other = ScriptableObject.CreateInstance<MechaData>();
            JsonUtility.FromJsonOverwrite("{\"mechaId\":\"explicit\",\"beamDamageTakenPercent\":80,\"ballisticDamageTakenPercent\":100}", other);
            var editor = UnityEditor.Editor.CreateEditor(new Object[] { data, other }, typeof(EditorTools.MechaDataEditor));
            try
            {
                var method = editor.GetType().GetMethod("DamageTakenValuesMixed", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null, "Mixed display must compare resolved percentages.");
                Assert.That((bool)method.Invoke(editor, new object[] { true }), Is.False);
                Assert.That((bool)method.Invoke(editor, new object[] { false }), Is.True);
            }
            finally { Object.DestroyImmediate(editor); Object.DestroyImmediate(other); }
        }
    }
}
