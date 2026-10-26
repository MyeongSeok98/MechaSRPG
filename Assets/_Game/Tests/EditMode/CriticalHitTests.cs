using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class CriticalHitTests
    {
        private static object Call(string name, params object[] args)
        {
            var method = typeof(CombatMath).GetMethod(name);
            Assert.That(method, Is.Not.Null, "Missing critical-hit rule: " + name);
            return method.Invoke(null, args);
        }
        [TestCase(0, 50, 0f)] [TestCase(0, 20, 0f)] [TestCase(0, 19, .25f)]
        [TestCase(0, 10, 2.5f)] [TestCase(0, 0, 5f)] [TestCase(25, 25, 5f)]
        [TestCase(10, 0, 14f)] [TestCase(20, 0, 23f)] [TestCase(30, 0, 32f)]
        [TestCase(40, 0, 41f)] [TestCase(50, 0, 50f)] [TestCase(100, -10, 50f)]
        public void TechniqueDifferencePreservesRequestedProbability(int attacker, int target, float expected)
            => Assert.That((float)Call("CalculateCriticalChance", attacker, target), Is.EqualTo(expected).Within(.00001f));

        [TestCase(0f, 0f, false)] [TestCase(2.5f, 2.49f, true)] [TestCase(2.5f, 2.5f, false)]
        [TestCase(50f, 49.999f, true)] [TestCase(50f, 50f, false)]
        [TestCase(50f, -1f, false)] [TestCase(50f, 100f, false)]
        public void FractionalProbabilityUsesExclusiveUpperBoundary(float chance, float roll, bool expected)
            => Assert.That((bool)Call("IsCritical", chance, roll), Is.EqualTo(expected));

        [TestCase(700, false, 700)] [TestCase(700, true, 875)]
        [TestCase(753, true, 941)] [TestCase(6, true, 8)]
        public void CriticalDamageRoundsOnlyFinalValue(int damage, bool critical, int expected)
            => Assert.That((int)Call("CalculateCriticalDamage", damage, critical), Is.EqualTo(expected));

        [TestCase(WeaponSlot.RightHand, true, 6)] [TestCase(WeaponSlot.LeftHand, true, 10)]
        [TestCase(WeaponSlot.RightHand, false, 5)]
        public void CommonAttackAppliesCriticalToChosenPart(WeaponSlot slot, bool critical, int expected)
        {
            using (var b = new MechaAccuracyTestBattle())
            {
                SetCriticalRoll(b.Combat, () => critical ? 0f : 99f);
                var preview = b.Combat.GetWeaponPreview(b.Player, b.Enemy, slot);
                Assert.That(Field<float>(preview, "CriticalChance"), Is.EqualTo(5f));
                var result = b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, slot);
                Assert.That(result.WasExecuted && result.Hit, Is.True);
                Assert.That(Field<bool>(result, "IsCritical"), Is.EqualTo(critical));
                Assert.That(result.DamageDealt, Is.EqualTo(expected));
                Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20 - expected));
                Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(2));
            }
        }
        [Test] public void PreviewAndMissDoNotConsumeCriticalRoll()
        {
            using (var b = new MechaAccuracyTestBattle())
            {
                int rolls = 0; SetCriticalRoll(b.Combat, () => { rolls++; return 0f; });
                b.Combat.GetWeaponPreview(b.Player, b.Enemy, WeaponSlot.RightHand);
                b.Combat.SetRollSource(() => 99);
                var result = b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.RightHand);
                Assert.That(result.Hit, Is.False); Assert.That(rolls, Is.Zero);
                Assert.That(Field<bool>(result, "IsCritical"), Is.False);
                Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            }
        }
        [TestCase(50, 0, 49.99f, true, 50f)] [TestCase(50, 0, 50f, false, 50f)]
        [TestCase(0, 10, 2.49f, true, 2.5f)] [TestCase(0, 10, 2.5f, false, 2.5f)]
        [TestCase(0, 20, 0f, false, 0f)] [TestCase(30, 30, 4.99f, true, 5f)]
        public void PilotDataControlsActualAttack(int attacker, int target, float roll, bool critical, float chance)
        {
            using (var b = new MechaAccuracyTestBattle(playerTechnique: attacker, enemyTechnique: target))
            {
                int rolls = 0; SetCriticalRoll(b.Combat, () => { rolls++; return roll; });
                var preview = b.Combat.GetWeaponPreview(b.Player, b.Enemy, WeaponSlot.RightHand);
                Assert.That(Field<float>(preview, "CriticalChance"), Is.EqualTo(chance).Within(.00001f));
                var result = b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.RightHand);
                Assert.That(Field<bool>(result, "IsCritical"), Is.EqualTo(critical));
                Assert.That(result.DamageDealt, Is.EqualTo(critical ? 6 : 5));
                Assert.That(rolls, Is.EqualTo(chance == 0f ? 0 : 1));
            }
        }
        [Test] public void PilotStatsAreCopiedAtInitializationWithoutMutatingSharedData()
        {
            using (var b = new MechaAccuracyTestBattle(playerTechnique: 30, enemyTechnique: 10))
            {
                string original = JsonUtility.ToJson(b.PlayerPilot);
                Assert.That(Field<float>(b.Combat.GetWeaponPreview(b.Player, b.Enemy, WeaponSlot.RightHand), "CriticalChance"), Is.EqualTo(23f));
                Assert.That(JsonUtility.ToJson(b.PlayerPilot), Is.EqualTo(original));
                JsonUtility.FromJsonOverwrite("{\"technique\":0}", b.PlayerPilot);
                Assert.That(b.Player.Initialize(b.Grid), Is.True);
                Assert.That(Field<float>(b.Combat.GetWeaponPreview(b.Player, b.Enemy, WeaponSlot.RightHand), "CriticalChance"), Is.EqualTo(23f));
                var later = b.Unit(new Vector2Int(5, 1), UnitTeam.Player, b.PlayerData, b.PlayerPilot);
                var property = typeof(BattleUnit).GetProperty("PilotTechnique"); Assert.That(property, Is.Not.Null);
                Assert.That((int)property.GetValue(later), Is.Zero);
            }
        }
        [Test] public void InvalidAttackNeverRollsCriticalOrSpendsAP()
        {
            using (var b = new MechaAccuracyTestBattle())
            {
                int rolls = 0; SetCriticalRoll(b.Combat, () => { rolls++; return 0f; });
                var result = b.Combat.TryAttackWithWeapon(b.Player, b.Player, WeaponSlot.RightHand);
                Assert.That(result.WasExecuted, Is.False); Assert.That(rolls, Is.Zero);
                Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            }
        }
        internal static T Field<T>(object value, string name)
        {
            var field = value.GetType().GetField(name); Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(value);
        }
        internal static void SetCriticalRoll(CombatSystem combat, Func<float> roll)
        {
            var method = typeof(CombatSystem).GetMethod("SetCriticalRollSource"); Assert.That(method, Is.Not.Null);
            method.Invoke(combat, new object[] { roll });
        }
    }
}
