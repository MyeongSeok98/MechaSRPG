using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class UnitInspectionTextTests
    {
        private BackpackTestBattle b;
        [SetUp] public void Setup() => b = new BackpackTestBattle();
        [TearDown] public void Cleanup() => b.Dispose();
        private string Read(string method, params object[] args)
        {
            var type = typeof(BattleUnit).Assembly.GetType("MechaXCOM.UnitInspectionText");
            Assert.That(type, Is.Not.Null, "Inspection must read current unit data.");
            return (string)type.GetMethod(method).Invoke(null, args);
        }
        [Test] public void NameUsesMechaDataInsteadOfSceneObjectName()
        {
            JsonUtility.FromJsonOverwrite("{\"displayName\":\"Enemy Scout\"}", b.Enemy.MechaData);
            Assert.That(Read("Name", b.Enemy), Is.EqualTo("Enemy Scout"));
        }
        [Test] public void PartHPReadsCurrentHealthAndEnglishPartLabel()
        {
            b.Enemy.Health.ApplyDamage(TargetBodyPart.LeftArm, 1);
            Assert.That(Read("PartHP", b.Enemy, TargetBodyPart.LeftArm), Is.EqualTo(
                $"L.Arm {b.Enemy.Health.GetCurrentHP(TargetBodyPart.LeftArm)}/{b.Enemy.Health.GetMaxHP(TargetBodyPart.LeftArm)}"));
        }
        [Test] public void EquipmentListsBothHandsIncludingMeleeAndEmptyBackpack()
        {
            string text = Read("Equipment", b.Enemy);
            Assert.That(text, Does.Contain("L.Hand: " + b.Weapon.DisplayName));
            Assert.That(text, Does.Contain("R.Hand: " + b.Weapon.DisplayName));
            Assert.That(text, Does.Contain("Backpack: None"));
        }
        [Test] public void TwoHandWeaponIsListedOnceAndBackpackWeaponIsIncluded()
        {
            b.Player.GetComponent<UnitWeaponLoadout>().Configure(null, GripMode.OneHand, b.Weapon, GripMode.TwoHands);
            string text = Read("Equipment", b.Player);
            Assert.That(text, Does.Contain("Both hands: " + b.Weapon.DisplayName));
            Assert.That(text, Does.Not.Contain("L.Hand:")); Assert.That(text, Does.Not.Contain("R.Hand:"));
            Assert.That(text, Does.Contain("Backpack: " + b.Backpack.DisplayName));
            Assert.That(text, Does.Contain("Backpack weapon: " + b.Weapon.DisplayName));
        }
    }
}
