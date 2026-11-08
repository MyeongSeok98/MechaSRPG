using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class BackpackAmmoTests
    {
        private BackpackTestBattle b;
        [SetUp] public void Setup() => b = new BackpackTestBattle(capacity: 2, fireAP: 1, enemyHasBackpack: true);
        [TearDown] public void Cleanup() => b.Dispose();
        private WeaponAmmoStatus Status => b.Player.Ammo.GetStatus(WeaponSlot.Backpack);
        private AttackResult Fire(WeaponSlot slot = WeaponSlot.Backpack) => b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, slot);

        [Test] public void BackpackStartsLoadedWithoutSparesOrReloadCost()
        {
            Assert.That(Status.Weapon, Is.SameAs(b.Weapon)); Assert.That(Status.UsesAmmo, Is.True);
            Assert.That(Status.Loaded, Is.EqualTo(2)); Assert.That(Status.Capacity, Is.EqualTo(2));
            Assert.That(Status.SpareMagazines, Is.Zero); Assert.That(Status.ReloadAPCost, Is.Zero);
            Assert.That(b.Weapon.SpareMagazineCount, Is.EqualTo(2)); Assert.That(b.Weapon.ReloadAPCost, Is.EqualTo(2));
        }
        [TestCase(false)] [TestCase(true)]
        public void ExecutedHitAndMissConsumeExactlyOneBackpackRound(bool hit)
        {
            b.Combat.SetRollSource(() => hit ? 0 : 99);
            var result = Fire(); Assert.That(result.WasExecuted, Is.True); Assert.That(result.Hit, Is.EqualTo(hit));
            Assert.That(Status.Loaded, Is.EqualTo(1)); Assert.That(Status.SpareMagazines, Is.Zero);
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
        }
        [Test] public void StartingMagazineIsFiniteAndEmptyShotSpendsNothing()
        {
            Assert.That(Fire().WasExecuted, Is.True); Assert.That(Fire().WasExecuted, Is.True);
            Assert.That(Status.Loaded, Is.Zero); Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(3));
            Assert.That(b.Player.Ammo.CanFire(WeaponSlot.Backpack), Is.False);
            Assert.That(b.Combat.GetWeaponRangeCells(b.Player, WeaponSlot.Backpack), Is.Empty);
            int rolls = 0; b.Combat.SetRollSource(() => { rolls++; return 0; });
            Assert.That(Fire().WasExecuted, Is.False); Assert.That(rolls, Is.Zero);
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(3)); Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(6));
        }
        [Test] public void PreviewAndInvalidAttackConsumeNoAmmo()
        {
            int rolls = 0; b.Combat.SetRollSource(() => { rolls++; return 0; });
            Assert.That(b.Combat.GetWeaponPreview(b.Player, b.Enemy, WeaponSlot.Backpack).CanAttack, Is.True);
            Assert.That(b.Combat.TryAttackWithWeapon(b.Player, b.Player, WeaponSlot.Backpack).WasExecuted, Is.False);
            Assert.That(Status.Loaded, Is.EqualTo(2)); Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(5)); Assert.That(rolls, Is.Zero);
        }
        [Test] public void BackpackReloadRejectedWithoutSpendingAPOrChangingAmmo()
        {
            Assert.That(Fire().WasExecuted, Is.True);
            Assert.That(b.Player.Ammo.CanReload(WeaponSlot.Backpack), Is.False);
            Assert.That(b.Combat.CanReloadWithWeapon(b.Player, WeaponSlot.Backpack, out var reason), Is.False); Assert.That(reason, Is.Not.Empty);
            Assert.That(b.Combat.TryReloadWithWeapon(b.Player, WeaponSlot.Backpack), Is.False);
            Assert.That(Status.Loaded, Is.EqualTo(1)); Assert.That(Status.SpareMagazines, Is.Zero);
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
        }
        [Test] public void SameDefinitionInHandAndBackpackHasIndependentAmmoAndHandCanReload()
        {
            string original = JsonUtility.ToJson(b.Weapon);
            Assert.That(Fire().WasExecuted, Is.True);
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.LeftHand).Loaded, Is.EqualTo(2));
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(2));
            Assert.That(Fire(WeaponSlot.RightHand).WasExecuted, Is.True);
            Assert.That(b.Combat.TryReloadWithWeapon(b.Player, WeaponSlot.RightHand), Is.True);
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(2));
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).SpareMagazines, Is.EqualTo(1));
            Assert.That(Status.Loaded, Is.EqualTo(1)); Assert.That(Status.SpareMagazines, Is.Zero);
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(1)); Assert.That(JsonUtility.ToJson(b.Weapon), Is.EqualTo(original));
        }
        [Test] public void SharedBackpackDataDoesNotShareRuntimeAmmo()
        {
            Assert.That(b.Enemy.GetComponent<UnitWeaponLoadout>().Backpack, Is.SameAs(b.Backpack));
            string original = JsonUtility.ToJson(b.Backpack);
            Assert.That(Fire().WasExecuted, Is.True);
            Assert.That(Status.Loaded, Is.EqualTo(1)); Assert.That(b.Enemy.Ammo.GetStatus(WeaponSlot.Backpack).Loaded, Is.EqualTo(2));
            Assert.That(JsonUtility.ToJson(b.Backpack), Is.EqualTo(original));
        }
        [Test] public void TurnReinitializeAndReenableDoNotRefill()
        {
            Assert.That(Fire().WasExecuted, Is.True); b.Player.ActionPoints.ResetForTurn();
            Assert.That(b.Player.Initialize(b.Grid), Is.True);
            b.Player.Ammo.enabled = false; b.Player.Ammo.enabled = true;
            var loadout = b.Player.GetComponent<UnitWeaponLoadout>();
            loadout.Configure(b.Weapon, GripMode.OneHand, b.Weapon, GripMode.OneHand);
            b.StartEnemyTeam();
            Assert.That(Status.Loaded, Is.EqualTo(1)); Assert.That(Status.SpareMagazines, Is.Zero);
        }
        [Test] public void HandTransferDoesNotStealBackpackAmmo()
        {
            var loadout = b.Player.GetComponent<UnitWeaponLoadout>();
            loadout.Configure(b.Weapon, GripMode.TwoHands, null, GripMode.OneHand);
            Assert.That(Fire().WasExecuted, Is.True); Assert.That(Fire(WeaponSlot.LeftHand).WasExecuted, Is.True);
            b.Player.Health.ApplyDamage(TargetBodyPart.LeftArm, 8);
            Assert.That(loadout.RightWeapon, Is.SameAs(b.Weapon));
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(1));
            Assert.That(Status.Loaded, Is.EqualTo(1)); Assert.That(Fire().WasExecuted, Is.True);
            Assert.That(Status.Loaded, Is.Zero); Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(1));
        }
        [Test] public void AmmoIsPaidBeforeDamageOrHitRollCallbacks()
        {
            int rolls = 0;
            b.Combat.SetRollSource(() => { rolls++; Assert.That(Status.Loaded, Is.EqualTo(1)); Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(4)); return 0; });
            Assert.That(Fire().WasExecuted, Is.True); Assert.That(rolls, Is.EqualTo(1));
        }
        [Test] public void ChangingBackpackDefinitionDuringBattleCannotReplenishAmmo()
        {
            Assert.That(Fire().WasExecuted, Is.True);
            var other = ScriptableObject.CreateInstance<BackpackData>();
            try
            {
                JsonUtility.FromJsonOverwrite("{\"backpackId\":\"other\"}", other);
                typeof(BackpackData).GetField("weapon", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(other, b.Weapon);
                var loadout = b.Player.GetComponent<UnitWeaponLoadout>();
                typeof(UnitWeaponLoadout).GetField("backpack", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(loadout, other);
                Assert.That(b.Player.Ammo.CanFire(WeaponSlot.Backpack), Is.False, "A serialized runtime replacement must not create a fresh magazine.");
                typeof(UnitWeaponLoadout).GetField("backpack", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(loadout, b.Backpack);
                Assert.That(Status.Loaded, Is.EqualTo(1)); Assert.That(b.Player.Ammo.CanFire(WeaponSlot.Backpack), Is.True);
            }
            finally { Object.DestroyImmediate(other); }
        }
    }
}
