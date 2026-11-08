using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public static class IndirectTestSupport
    {
        public static void Configure(BackpackTestBattle b, int radius = 1, int minParts = 1, int maxParts = 1)
        {
            ExplosiveTestSupport.Set(b.Weapon, "rangedCategory", RangedWeaponCategory.Explosive);
            ExplosiveTestSupport.Set(b.Weapon, "rangedAttackMode", RangedAttackMode.Indirect);
            ExplosiveTestSupport.Set(b.Weapon, "blastRadius", radius);
            ExplosiveTestSupport.Set(b.Weapon, "minHitParts", minParts); ExplosiveTestSupport.Set(b.Weapon, "maxHitParts", maxParts);
            ExplosiveTestSupport.Set(b.Weapon, "minDamage", 100); ExplosiveTestSupport.Set(b.Weapon, "maxDamage", 200);
            Heal(b.Player); Heal(b.Enemy);
        }
        public static void Heal(BattleUnit unit, int hp = 1000)
        { for (int i = 1; i <= 7; i++) unit.Health.ConfigurePartHP((TargetBodyPart)i, hp); unit.Health.Initialize(); }
        public static BattleUnit AddUnit(BackpackTestBattle b, Vector2Int position, UnitTeam team)
        {
            var unit = new GameObject("Area " + team).AddTestBattleUnit(); unit.transform.SetParent(b.Root.transform);
            unit.Configure(position, 5); unit.ConfigureTeam(team); Assert.That(unit.Initialize(b.Grid), Is.True); Heal(unit); return unit;
        }
        public static object Preview(BackpackTestBattle b, Vector2Int center, WeaponSlot slot = WeaponSlot.RightHand) => Call(b.Combat, "GetIndirectPreview", b.Player, center, slot);
        public static object Fire(BackpackTestBattle b, Vector2Int center, WeaponSlot slot = WeaponSlot.RightHand) => Call(b.Combat, "TryIndirectAttack", b.Player, center, slot);
        public static object Call(object value, string method, params object[] args)
        {
            var info = value.GetType().GetMethod(method); Assert.That(info, Is.Not.Null, method); return info.Invoke(value, args);
        }
        public static T Field<T>(object value, string name) => ExplosiveTestSupport.Read<T>(value, name);
        public static object[] Results(object result) => ((IEnumerable)Field<object>(result, "Targets")).Cast<object>().ToArray();
        public static void Rolls(CombatSystem combat, Func<int, int, int> count, Func<int, int> part)
        {
            Call(combat, "SetIndirectPartCountRollSource", count); Call(combat, "SetIndirectPartIndexRollSource", part);
        }
    }
}
