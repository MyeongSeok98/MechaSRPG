using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace MechaXCOM.Tests
{
    public static class ExplosiveTestSupport
    {
        public static void Set(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(info, Is.Not.Null, field); info.SetValue(target, value);
        }
        public static T Read<T>(object target, string field)
        {
            var info = target.GetType().GetField(field);
            Assert.That(info, Is.Not.Null, field); return (T)info.GetValue(target);
        }
        public static T Property<T>(object target, string name)
        {
            var info = target.GetType().GetProperty(name);
            Assert.That(info, Is.Not.Null, name); return (T)info.GetValue(target);
        }
        public static void Configure(BackpackTestBattle b, int count = 2, float minimum = .05f, float maximum = .15f)
        {
            Set(b.Weapon, "rangedCategory", RangedWeaponCategory.Explosive);
            Set(b.Weapon, "minDamage", 800); Set(b.Weapon, "maxDamage", 800);
            Set(b.Weapon, "additionalHitPartCount", count);
            Set(b.Weapon, "additionalDamageMinRatio", minimum); Set(b.Weapon, "additionalDamageMaxRatio", maximum);
            for (int part = 1; part <= 7; part++) b.Enemy.Health.ConfigurePartHP((TargetBodyPart)part, 10000);
            b.Enemy.Health.Initialize();
        }
        public static void Rolls(CombatSystem combat, Func<int, int> index, Func<float, float, float> ratio)
        {
            var a = typeof(CombatSystem).GetMethod("SetAdditionalPartIndexRollSource");
            var b = typeof(CombatSystem).GetMethod("SetAdditionalDamageRatioRollSource");
            Assert.That(a, Is.Not.Null); Assert.That(b, Is.Not.Null);
            a.Invoke(combat, new object[] { index }); b.Invoke(combat, new object[] { ratio });
        }
        public static IReadOnlyList<PartDamageResult> Additional(AttackResult result) =>
            Read<IReadOnlyList<PartDamageResult>>(result, "AdditionalPartDamage");
        public static PartDamageResult[] Batch(UnitHealth health, params (TargetBodyPart part, int damage)[] hits)
        {
            var type = typeof(UnitHealth).Assembly.GetType("MechaXCOM.PartDamage");
            var method = typeof(UnitHealth).GetMethod("ApplyDamageBatch");
            Assert.That(type, Is.Not.Null); Assert.That(method, Is.Not.Null);
            var array = Array.CreateInstance(type, hits.Length);
            for (int i = 0; i < hits.Length; i++) array.SetValue(Activator.CreateInstance(type, hits[i].part, hits[i].damage), i);
            return (PartDamageResult[])method.Invoke(health, new object[] { array });
        }
    }
}
