using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class UnitWeaponInfoTests
    {
        private BackpackTestBattle battle;
        [TearDown] public void Cleanup() => battle?.Dispose();
        private IList Read()
        {
            var type = typeof(BattleUnit).Assembly.GetType("MechaXCOM.UnitWeaponInfo"); Assert.That(type, Is.Not.Null);
            var rows = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(type));
            type.GetMethod("Read").Invoke(null, new object[] { battle.Player, rows }); return rows;
        }
        private T Get<T>(object row, string property) => (T)row.GetType().GetProperty(property).GetValue(row);
        private object Slot(IList rows, WeaponSlot slot)
        { foreach (var row in rows) if (Get<WeaponSlot>(row, "Slot") == slot) return row; Assert.Fail("Missing slot " + slot); return null; }
        [Test] public void ListsEquippedHandsAndBackpackWithLiveAmmo()
        {
            battle = new BackpackTestBattle(); var rows = Read(); Assert.That(rows.Count, Is.EqualTo(3));
            Assert.That(Get<string>(Slot(rows, WeaponSlot.LeftHand), "SlotLabel"), Is.EqualTo("L"));
            Assert.That(Get<string>(Slot(rows, WeaponSlot.RightHand), "ResourceText"), Does.Contain("6/6").And.Contain("RESERVE"));
            Assert.That(Get<string>(Slot(rows, WeaponSlot.Backpack), "ResourceText"), Does.Contain("NO RELOAD"));
        }
        [Test] public void DestroyedHandStaysListedAsDisabledWhileOtherEquipmentRemainsAvailable()
        {
            battle = new BackpackTestBattle(); battle.Player.Health.ApplyDamage(TargetBodyPart.RightArm, int.MaxValue);
            var rows = Read(); Assert.That(rows.Count, Is.EqualTo(3));
            Assert.That(Get<bool>(Slot(rows, WeaponSlot.RightHand), "IsDisabled"), Is.True);
            Assert.That(Get<string>(Slot(rows, WeaponSlot.RightHand), "StatusText"), Is.EqualTo("사용 불가"));
            Assert.That(Get<bool>(Slot(rows, WeaponSlot.LeftHand), "IsDisabled"), Is.False);
            Assert.That(Get<bool>(Slot(rows, WeaponSlot.Backpack), "IsDisabled"), Is.False);
            int ap = battle.Player.ActionPoints.CurrentPoints;
            Assert.That(battle.Combat.TryAttack(battle.Player, battle.Enemy, AttackType.Ranged).WasExecuted,
                Is.True, "The surviving hand/backpack may still attack; slot-specific rejection is checked below.");
            Assert.That(battle.Player.Combat.TryGetAttack(WeaponSlot.RightHand, out _, out _), Is.False);
        }
        [Test] public void NoAPOrAmmoIsDistinctFromArmRestriction()
        {
            battle = new BackpackTestBattle(); battle.Player.ActionPoints.TrySpend(5);
            var info = Slot(Read(), WeaponSlot.RightHand); Assert.That(Get<bool>(info, "IsDisabled"), Is.False);
            Assert.That(Get<string>(info, "StatusText"), Is.EqualTo("AP 부족"));
            battle.Player.ActionPoints.ResetForTurn();
            var consume = typeof(UnitWeaponAmmo).GetMethod("TryConsumeShot", BindingFlags.Instance | BindingFlags.NonPublic);
            while (battle.Player.Ammo.CanFire(WeaponSlot.RightHand)) consume.Invoke(battle.Player.Ammo, new object[] { WeaponSlot.RightHand });
            info = Slot(Read(), WeaponSlot.RightHand); Assert.That(Get<bool>(info, "IsDisabled"), Is.False);
            Assert.That(Get<string>(info, "StatusText"), Is.EqualTo("탄약 없음"));
        }
        [Test] public void EitherGripWeaponUsesOneRowAndTransfersToSurvivingHand()
        {
            battle = new BackpackTestBattle(withBackpack: false);
            Assert.That(battle.Player.GetComponent<UnitWeaponLoadout>().TryConfigure(null, GripMode.OneHand, battle.Weapon, GripMode.TwoHands), Is.True);
            var rows = Read(); Assert.That(rows.Count, Is.EqualTo(1)); Assert.That(Get<string>(rows[0], "SlotLabel"), Is.EqualTo("LR"));
            battle.Player.Health.ApplyDamage(TargetBodyPart.RightArm, int.MaxValue);
            rows = Read(); Assert.That(rows.Count, Is.EqualTo(1)); Assert.That(Get<string>(rows[0], "SlotLabel"), Is.EqualTo("L"));
            Assert.That(Get<bool>(rows[0], "IsDisabled"), Is.False);
        }
    }
}

