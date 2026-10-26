using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class ArmWeaponDamageTests
    {
        private OverwatchTestBattle battle;
        private readonly List<WeaponData> weapons = new List<WeaponData>();
        private BattleUnit Unit => battle.Watcher;
        private UnitWeaponLoadout Loadout => Unit.GetComponent<UnitWeaponLoadout>();
        [SetUp] public void Setup() { battle = new OverwatchTestBattle(); }
        [TearDown] public void Cleanup()
        {
            battle.Dispose(); foreach (var weapon in weapons) Object.DestroyImmediate(weapon); weapons.Clear();
        }
        private WeaponData Weapon(AttackType type, GripRequirement requirement = GripRequirement.Either)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponData>(); weapons.Add(weapon);
            JsonUtility.FromJsonOverwrite($"{{\"attackType\":{(int)type},\"gripRequirement\":{(int)requirement},\"twoHandHitMultiplier\":1.2}}", weapon);
            return weapon;
        }
        private void Equip(WeaponData weapon, WeaponSlot slot, GripMode mode) => Loadout.Configure(
            slot == WeaponSlot.LeftHand ? weapon : null, mode, slot == WeaponSlot.RightHand ? weapon : null, mode);
        private static WeaponSlot SurvivingSlot(TargetBodyPart broken) => broken == TargetBodyPart.LeftArm ? WeaponSlot.RightHand : WeaponSlot.LeftHand;
        private void Break(TargetBodyPart part) => Unit.Health.ApplyDamage(part, Unit.Health.GetMaxHP(part));

        [TestCase(AttackType.Ranged, TargetBodyPart.LeftArm)]
        [TestCase(AttackType.Ranged, TargetBodyPart.RightArm)]
        [TestCase(AttackType.Melee, TargetBodyPart.LeftArm)]
        [TestCase(AttackType.Melee, TargetBodyPart.RightArm)]
        public void OnlyBrokenHandsOneHandWeaponIsUnavailable(AttackType type, TargetBodyPart broken)
        {
            var weapon = Weapon(type, GripRequirement.OneHandOnly);
            Loadout.Configure(weapon, GripMode.OneHand, weapon, GripMode.OneHand);
            int ap = Unit.ActionPoints.CurrentPoints;
            Break(broken);
            var healthy = SurvivingSlot(broken);
            var damaged = healthy == WeaponSlot.LeftHand ? WeaponSlot.RightHand : WeaponSlot.LeftHand;
            Assert.That(Loadout.IsValid(out _), Is.True, "Damage does not invalidate equipment in the other hand.");
            Assert.That(Unit.Combat.TryGetAttack(damaged, out _, out var reason), Is.False);
            Assert.That(reason, Does.Contain("팔"));
            Assert.That(Unit.Combat.TryGetAttack(healthy, out _, out _), Is.True);
            CollectionAssert.AreEqual(new[] { healthy }, Unit.Combat.GetAttackSlots(type));
            Assert.That(Loadout.LeftWeapon, Is.SameAs(weapon)); Assert.That(Loadout.RightWeapon, Is.SameAs(weapon));
            Assert.That(Unit.ActionPoints.CurrentPoints, Is.EqualTo(ap));
        }

        [TestCase(AttackType.Ranged, WeaponSlot.LeftHand, TargetBodyPart.LeftArm)]
        [TestCase(AttackType.Ranged, WeaponSlot.LeftHand, TargetBodyPart.RightArm)]
        [TestCase(AttackType.Ranged, WeaponSlot.RightHand, TargetBodyPart.LeftArm)]
        [TestCase(AttackType.Ranged, WeaponSlot.RightHand, TargetBodyPart.RightArm)]
        [TestCase(AttackType.Melee, WeaponSlot.LeftHand, TargetBodyPart.LeftArm)]
        [TestCase(AttackType.Melee, WeaponSlot.LeftHand, TargetBodyPart.RightArm)]
        [TestCase(AttackType.Melee, WeaponSlot.RightHand, TargetBodyPart.LeftArm)]
        [TestCase(AttackType.Melee, WeaponSlot.RightHand, TargetBodyPart.RightArm)]
        public void EitherTwoHandsFallsBackToActualSurvivingSlot(AttackType type, WeaponSlot original, TargetBodyPart broken)
        {
            var weapon = Weapon(type); string assetBefore = JsonUtility.ToJson(weapon);
            Equip(weapon, original, GripMode.TwoHands);
            Assert.That(Unit.Combat.TryGetAttack(original, out var before, out _), Is.True);
            Assert.That(before.GripMultiplier, Is.EqualTo(1.2f));
            int changes = 0; Loadout.Changed += () => changes++;
            int ap = Unit.ActionPoints.CurrentPoints;
            Break(broken);
            var healthy = SurvivingSlot(broken);
            Assert.That(healthy == WeaponSlot.LeftHand ? Loadout.LeftWeapon : Loadout.RightWeapon, Is.SameAs(weapon));
            Assert.That(healthy == WeaponSlot.LeftHand ? Loadout.RightWeapon : Loadout.LeftWeapon, Is.Null);
            Assert.That(healthy == WeaponSlot.LeftHand ? Loadout.LeftGrip : Loadout.RightGrip, Is.EqualTo(GripMode.OneHand));
            CollectionAssert.AreEqual(new[] { healthy }, Unit.Combat.GetAttackSlots(type));
            Assert.That(Unit.Combat.TryGetAttack(healthy, out var after, out _), Is.True);
            Assert.That(after.GripMultiplier, Is.EqualTo(1f));
            Assert.That(after.APCost, Is.EqualTo(before.APCost)); Assert.That(after.MinDamage, Is.EqualTo(before.MinDamage));
            Assert.That(Unit.ActionPoints.CurrentPoints, Is.EqualTo(ap));
            Assert.That(changes, Is.EqualTo(1)); Assert.That(JsonUtility.ToJson(weapon), Is.EqualTo(assetBefore));
        }

        [TestCase(WeaponSlot.LeftHand, TargetBodyPart.LeftArm)]
        [TestCase(WeaponSlot.LeftHand, TargetBodyPart.RightArm)]
        [TestCase(WeaponSlot.RightHand, TargetBodyPart.LeftArm)]
        [TestCase(WeaponSlot.RightHand, TargetBodyPart.RightArm)]
        public void RequiredTwoHandsCannotAttackWithEitherArmDestroyed(WeaponSlot slot, TargetBodyPart broken)
        {
            var weapon = Weapon(AttackType.Ranged, GripRequirement.TwoHandsRequired);
            Equip(weapon, slot, GripMode.TwoHands); Break(broken);
            Assert.That(Unit.Combat.TryGetAttack(slot, out _, out _), Is.False);
            Assert.That(Unit.Combat.GetAttackSlots(AttackType.Ranged), Is.Empty);
            Assert.That(slot == WeaponSlot.LeftHand ? Loadout.LeftWeapon : Loadout.RightWeapon, Is.SameAs(weapon));
        }
        [Test] public void BothArmsDestroyedAfterFallbackLeavesNoUsableWeapon()
        {
            Equip(Weapon(AttackType.Ranged), WeaponSlot.RightHand, GripMode.TwoHands);
            Break(TargetBodyPart.RightArm); Break(TargetBodyPart.LeftArm);
            Assert.That(Unit.Health.CanFight, Is.True, "Two major parts alone do not cause Critical.");
            Assert.That(Unit.Combat.GetAttackSlots(AttackType.Ranged), Is.Empty);
            Assert.That(Unit.Combat.TryGetAttack(WeaponSlot.LeftHand, out _, out _), Is.False);
            Assert.That(Unit.Combat.TryGetAttack(WeaponSlot.RightHand, out _, out _), Is.False);
        }
        [TestCase(TargetBodyPart.LeftArm)] [TestCase(TargetBodyPart.RightArm)]
        public void ConfiguringTwoHandsAfterArmLossAlsoUsesSurvivingHand(TargetBodyPart broken)
        {
            Loadout.Configure(null, GripMode.OneHand, null, GripMode.OneHand); Break(broken);
            var weapon = Weapon(AttackType.Ranged); int changes = 0; Loadout.Changed += () => changes++;
            Equip(weapon, WeaponSlot.RightHand, GripMode.TwoHands);
            Assert.That(Unit.Combat.TryGetAttack(SurvivingSlot(broken), out var stats, out _), Is.True);
            Assert.That(stats.GripMultiplier, Is.EqualTo(1f)); Assert.That(changes, Is.EqualTo(1));
        }
        [Test] public void PartialArmDamageAndUnrelatedPartDoNotChangeGrip()
        {
            Equip(Weapon(AttackType.Ranged), WeaponSlot.RightHand, GripMode.TwoHands);
            Unit.Health.ApplyDamage(TargetBodyPart.RightArm, 7); Break(TargetBodyPart.Head);
            Assert.That(Loadout.RightGrip, Is.EqualTo(GripMode.TwoHands));
            Assert.That(Unit.Combat.TryGetAttack(WeaponSlot.RightHand, out var stats, out _), Is.True);
            Assert.That(stats.GripMultiplier, Is.EqualTo(1.2f));
        }
        [Test] public void BrokenWeaponCannotSpendAPOrRandomOnDirectAttack()
        {
            int rolls = 0; battle.Combat.SetRollSource(() => { rolls++; return 0; });
            Break(TargetBodyPart.RightArm);
            int ap = Unit.ActionPoints.CurrentPoints;
            Assert.That(battle.Combat.GetWeaponPreview(Unit, battle.Mover, WeaponSlot.RightHand).CanAttack, Is.False);
            Assert.That(battle.Combat.TryAttackWithWeapon(Unit, battle.Mover, WeaponSlot.RightHand).WasExecuted, Is.False);
            Assert.That(Unit.ActionPoints.CurrentPoints, Is.EqualTo(ap)); Assert.That(rolls, Is.Zero);
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }
    }
}
