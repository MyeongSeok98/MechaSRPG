using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class ArmorDamageTests
    {
        private static object MathCall(string name, params object[] args)
        {
            var method = typeof(CombatMath).GetMethod(name);
            Assert.That(method, Is.Not.Null, "Missing armor damage rule: " + name);
            return method.Invoke(null, args);
        }
        [TestCase(ArmorType.Standard, RangedWeaponCategory.Beam, 1f)]
        [TestCase(ArmorType.Standard, RangedWeaponCategory.Ballistic, 1f)]
        [TestCase(ArmorType.Standard, RangedWeaponCategory.Explosive, 1f)]
        [TestCase(ArmorType.BeamResistant, RangedWeaponCategory.Beam, .8f)]
        [TestCase(ArmorType.BeamResistant, RangedWeaponCategory.Ballistic, 1.1f)]
        [TestCase(ArmorType.BeamResistant, RangedWeaponCategory.Explosive, 1f)]
        [TestCase(ArmorType.BallisticResistant, RangedWeaponCategory.Beam, 1.1f)]
        [TestCase(ArmorType.BallisticResistant, RangedWeaponCategory.Ballistic, .8f)]
        [TestCase(ArmorType.BallisticResistant, RangedWeaponCategory.Explosive, 1f)]
        public void RequestedRangedArmorTable(ArmorType armor, RangedWeaponCategory category, float expected)
            => Assert.That((float)MathCall("CalculateArmorMultiplier", armor, AttackType.Ranged, category), Is.EqualTo(expected));

        [TestCase(ArmorType.Standard)] [TestCase(ArmorType.BeamResistant)] [TestCase(ArmorType.BallisticResistant)]
        public void MeleeIgnoresRangedCategoryAndArmor(ArmorType armor)
            => Assert.That((float)MathCall("CalculateArmorMultiplier", armor, AttackType.Melee, RangedWeaponCategory.Beam), Is.EqualTo(1f));

        [TestCase(7, false, .8f, 6)] [TestCase(7, true, .8f, 7)]
        [TestCase(5, false, 1.1f, 6)] [TestCase(5, true, 1.1f, 7)]
        [TestCase(1000, false, .8f, 800)] [TestCase(1000, true, .8f, 1000)]
        [TestCase(1000, true, 1.1f, 1375)] [TestCase(int.MaxValue, true, 1.1f, int.MaxValue)]
        [TestCase(33554432, true, .8f, 33554432)]
        [TestCase(100000000, false, 1.1f, 110000000)]
        public void ArmorAndCriticalRoundOnceAtTheEnd(int raw, bool critical, float multiplier, int expected)
            => Assert.That((int)MathCall("CalculateAttackDamage", raw, critical, multiplier), Is.EqualTo(expected));

        [TestCase(ArmorType.BeamResistant, RangedWeaponCategory.Beam, false, 6)]
        [TestCase(ArmorType.BeamResistant, RangedWeaponCategory.Beam, true, 7)]
        [TestCase(ArmorType.BeamResistant, RangedWeaponCategory.Ballistic, false, 8)]
        [TestCase(ArmorType.BeamResistant, RangedWeaponCategory.Ballistic, true, 10)]
        [TestCase(ArmorType.BallisticResistant, RangedWeaponCategory.Ballistic, false, 6)]
        [TestCase(ArmorType.BallisticResistant, RangedWeaponCategory.Beam, false, 8)]
        [TestCase(ArmorType.BeamResistant, RangedWeaponCategory.Explosive, false, 7)]
        [TestCase(ArmorType.Standard, RangedWeaponCategory.Beam, false, 7)]
        public void AttackDamagesChosenPartWithTargetArmor(ArmorType armor, RangedWeaponCategory category, bool critical, int expected)
        {
            using (var b = new MechaAccuracyTestBattle(playerArmor: ArmorType.BallisticResistant, enemyArmor: armor))
            {
                b.Player.Combat.ConfigureTestRanged(7, 80, 7, 3);
                var weapon = b.Player.GetComponent<UnitWeaponLoadout>().RightWeapon;
                JsonUtility.FromJsonOverwrite("{\"rangedCategory\":" + (int)category + "}", weapon);
                string dataBefore = JsonUtility.ToJson(b.EnemyData);
                b.Combat.SetCriticalRollSource(() => critical ? 0f : 99f);
                var preview = b.Combat.GetWeaponPreview(b.Player, b.Enemy, WeaponSlot.RightHand, TargetBodyPart.RightLeg);
                Assert.That(preview.CanAttack, Is.True);
                Assert.That(preview.MinDamage, Is.EqualTo(7), "Keep raw weapon bounds for RNG.");
                Assert.That(preview.HitChance, Is.EqualTo(72), "Armor must not change accuracy.");
                var result = b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.RightHand, TargetBodyPart.RightLeg);
                Assert.That(result.Hit, Is.True); Assert.That(result.IsCritical, Is.EqualTo(critical));
                Assert.That(result.DamageDealt, Is.EqualTo(expected));
                Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.RightLeg), Is.EqualTo(10 - expected));
                Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
                Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(2));
                Assert.That(JsonUtility.ToJson(b.EnemyData), Is.EqualTo(dataBefore));
            }
        }
        [TestCase(TargetBodyPart.Torso)] [TestCase(TargetBodyPart.Cockpit)] [TestCase(TargetBodyPart.Head)]
        [TestCase(TargetBodyPart.LeftArm)] [TestCase(TargetBodyPart.RightArm)]
        [TestCase(TargetBodyPart.LeftLeg)] [TestCase(TargetBodyPart.RightLeg)]
        public void OneChassisArmorAppliesToEveryPart(TargetBodyPart part)
        {
            using (var b = new MechaAccuracyTestBattle(enemyArmor: ArmorType.BeamResistant))
            {
                b.Player.Combat.ConfigureTestRanged(5, 80, 7, 3);
                int before = b.Enemy.Health.GetCurrentHP(part);
                var result = b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.RightHand, part);
                Assert.That(result.DamageDealt, Is.EqualTo(4));
                Assert.That(b.Enemy.Health.GetCurrentHP(part), Is.EqualTo(before - 4));
            }
        }
        [Test] public void PreviewShowsAdjustedRangeWithoutChangingRawDamageRollBounds()
        {
            using (var b = new MechaAccuracyTestBattle(enemyArmor: ArmorType.BeamResistant))
            {
                var weapon = b.Player.GetComponent<UnitWeaponLoadout>().RightWeapon;
                JsonUtility.FromJsonOverwrite("{\"minDamage\":7,\"maxDamage\":9}", weapon);
                int rolls = 0;
                b.Combat.SetDamageRollSource((min, max) => { rolls++; Assert.That(min, Is.EqualTo(7)); Assert.That(max, Is.EqualTo(9)); return max; });
                var preview = b.Combat.GetWeaponPreview(b.Player, b.Enemy, WeaponSlot.RightHand);
                Assert.That(AttackPreviewText.DamageRange(preview), Is.EqualTo("6-7"));
                Assert.That(rolls, Is.Zero);
                Assert.That(b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.RightHand).DamageDealt, Is.EqualTo(7));
                Assert.That(rolls, Is.EqualTo(1));
            }
        }
        [Test] public void MeleeDamageIsUnchangedByAntiBeamArmor()
        {
            using (var b = new MechaAccuracyTestBattle(enemyArmor: ArmorType.BeamResistant))
            {
                Assert.That(b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.LeftHand).DamageDealt, Is.EqualTo(8));
            }
        }
        [Test] public void ArmorIsSnapshottedAtInitializationAndMissingDataIsStandard()
        {
            using (var b = new MechaAccuracyTestBattle(enemyArmor: ArmorType.BeamResistant))
            {
                var property = typeof(BattleUnit).GetProperty("ArmorType"); Assert.That(property, Is.Not.Null);
                JsonUtility.FromJsonOverwrite("{\"armorType\":2}", b.EnemyData);
                Assert.That(b.Enemy.Initialize(b.Grid), Is.True);
                Assert.That(property.GetValue(b.Enemy), Is.EqualTo(ArmorType.BeamResistant));
                var later = b.Unit(new Vector2Int(5, 1), UnitTeam.Enemy, b.EnemyData);
                Assert.That(property.GetValue(later), Is.EqualTo(ArmorType.BallisticResistant));
                var legacy = b.Unit(new Vector2Int(7, 1), UnitTeam.Player, null);
                Assert.That(property.GetValue(legacy), Is.EqualTo(ArmorType.Standard));
            }
        }
    }
}
