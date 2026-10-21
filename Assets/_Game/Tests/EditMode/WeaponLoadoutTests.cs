using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class WeaponLoadoutTests
    {
        private GameObject root;
        private UnitCombat combat;
        private readonly List<WeaponData> weapons = new List<WeaponData>();
        [SetUp] public void Setup() { root = new GameObject("Loadout tests"); combat = root.AddComponent<UnitCombat>(); }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(root);
            foreach (var weapon in weapons) Object.DestroyImmediate(weapon);
            weapons.Clear();
        }
        private WeaponData Weapon(AttackType type = AttackType.Ranged, GripRequirement grip = GripRequirement.Either)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponData>(); weapons.Add(weapon);
            var data = new SerializedObject(weapon);
            data.FindProperty("attackType").intValue = (int)type;
            data.FindProperty("gripRequirement").intValue = (int)grip;
            data.FindProperty("displayName").stringValue = "Test rifle";
            data.FindProperty("weightTonnes").floatValue = 2;
            data.ApplyModifiedPropertiesWithoutUndo();
            return weapon;
        }
        private UnitWeaponLoadout Equip(WeaponData left, GripMode leftGrip, WeaponData right, GripMode rightGrip)
        {
            var loadout = root.EnsureTestLoadout(); loadout.Configure(left, leftGrip, right, rightGrip); return loadout;
        }
        [Test] public void SeparateHandsCanUseSameAsset()
        {
            var weapon = Weapon(); Equip(weapon, GripMode.OneHand, weapon, GripMode.OneHand);
            CollectionAssert.AreEqual(new[] { WeaponSlot.RightHand, WeaponSlot.LeftHand }, combat.GetAttackSlots(AttackType.Ranged));
            Assert.That(combat.TryGetAttack(WeaponSlot.LeftHand, out var left, out var reason), Is.True, reason);
            Assert.That(combat.TryGetAttack(WeaponSlot.RightHand, out var right, out reason), Is.True, reason);
            Assert.That(left.Weapon, Is.SameAs(weapon)); Assert.That(right.Weapon, Is.SameAs(weapon));
            Assert.That(left.Slot, Is.EqualTo(WeaponSlot.LeftHand)); Assert.That(right.Slot, Is.EqualTo(WeaponSlot.RightHand));
        }
        [TestCase(WeaponSlot.LeftHand)] [TestCase(WeaponSlot.RightHand)]
        public void TwoHandWeaponEnumeratedOnce(WeaponSlot slot)
        {
            var weapon = Weapon(AttackType.Ranged, GripRequirement.TwoHandsRequired);
            Equip(slot == WeaponSlot.LeftHand ? weapon : null, GripMode.TwoHands,
                slot == WeaponSlot.RightHand ? weapon : null, GripMode.TwoHands);
            CollectionAssert.AreEqual(new[] { slot }, combat.GetAttackSlots(AttackType.Ranged));
            Assert.That(combat.TryGetAttack(slot, out var stats, out _), Is.True);
            Assert.That(stats.GripMultiplier, Is.EqualTo(1));
            Assert.That(combat.TryGetAttack(slot == WeaponSlot.LeftHand ? WeaponSlot.RightHand : WeaponSlot.LeftHand, out _, out _), Is.False);
        }
        [TestCase(GripRequirement.OneHandOnly, GripMode.TwoHands)]
        [TestCase(GripRequirement.TwoHandsRequired, GripMode.OneHand)]
        public void IncorrectGripFailsClosed(GripRequirement requirement, GripMode grip)
        {
            var loadout = Equip(null, GripMode.OneHand, Weapon(AttackType.Ranged, requirement), grip);
            Assert.That(loadout.IsValid(out var reason), Is.False); Assert.That(reason, Is.Not.Empty);
            Assert.That(combat.GetAttackSlots(AttackType.Ranged), Is.Empty);
            Assert.That(combat.TryGetAttack(WeaponSlot.RightHand, out _, out _), Is.False);
        }
        [TestCase(WeaponSlot.LeftHand)] [TestCase(WeaponSlot.RightHand)]
        public void ConflictingHandsFailClosed(WeaponSlot twoHandSlot)
        {
            var loadout = Equip(Weapon(), twoHandSlot == WeaponSlot.LeftHand ? GripMode.TwoHands : GripMode.OneHand,
                Weapon(), twoHandSlot == WeaponSlot.RightHand ? GripMode.TwoHands : GripMode.OneHand);
            Assert.That(loadout.IsValid(out var reason), Is.False); Assert.That(reason, Is.Not.Empty);
            Assert.That(combat.GetAttackSlots(AttackType.Ranged), Is.Empty);
            Assert.That(combat.TryGetAttack(WeaponSlot.RightHand, out _, out _), Is.False);
            Assert.That(combat.TryGetAttack(WeaponSlot.LeftHand, out _, out _), Is.False);
        }
        [Test] public void EmptyDisabledInvalidLoadoutNeverFallsBack()
        {
            var loadout = Equip(null, GripMode.OneHand, null, GripMode.OneHand);
            Assert.That(loadout.IsValid(out _), Is.True, "An empty loadout is valid but unarmed.");
            foreach (var type in new[] { AttackType.Ranged, AttackType.Melee }) Assert.That(combat.GetAttackSlots(type), Is.Empty);
            Assert.That(combat.TryGetAttack((WeaponSlot)99, out _, out _), Is.False);
            Assert.That(combat.TryGetAttack((WeaponSlot)98, out _, out _), Is.False);
            loadout.Configure(null, GripMode.OneHand, Weapon(), GripMode.OneHand);
            loadout.enabled = false;
            Assert.That(combat.TryGetAttack(WeaponSlot.RightHand, out _, out _), Is.False);
            Assert.That(combat.GetAttackSlots(AttackType.Ranged), Is.Empty);
            Assert.That(combat.GetAttack(AttackType.Ranged).MinDamage, Is.Zero);
            loadout.enabled = true; Assert.That(combat.TryGetAttack(WeaponSlot.RightHand, out _, out _), Is.True);
            loadout.Configure(null, GripMode.OneHand, Weapon(), (GripMode)99);
            Assert.That(combat.GetAttackSlots(AttackType.Ranged), Is.Empty);
            Assert.That(combat.TryGetAttack((WeaponSlot)99, out _, out _), Is.False);
        }
        [Test] public void FixedDamageWeaponDataSuppliesAttackStats()
        {
            combat.ConfigureTestRanged(5, 80, 7, 3); combat.ConfigureTestMelee(8, 95, 2, 3);
            Assert.That(combat.TryGetAttack(WeaponSlot.RightHand, out var ranged, out _), Is.True);
            Assert.That(ranged.MinDamage, Is.EqualTo(5)); Assert.That(ranged.MaxDamage, Is.EqualTo(5));
            Assert.That(ranged.BaseHitChance, Is.EqualTo(80)); Assert.That(ranged.Range, Is.EqualTo(7));
            Assert.That(ranged.APCost, Is.EqualTo(3)); Assert.That(ranged.GripMultiplier, Is.EqualTo(1));
            Assert.That(ranged.Weapon, Is.Not.Null);
            Assert.That(combat.TryGetAttack(WeaponSlot.LeftHand, out var melee, out _), Is.True);
            Assert.That(melee.MinDamage, Is.EqualTo(8)); Assert.That(melee.MaxDamage, Is.EqualTo(8));
            Assert.That(melee.Type, Is.EqualTo(AttackType.Melee));
            Assert.That(combat.TryGetAttack((WeaponSlot)99, out _, out _), Is.False);
            Assert.That(combat.GetAttackSlots((AttackType)99), Is.Empty);
        }
        [TestCase(GripRequirement.Either, GripMode.OneHand, 1f)]
        [TestCase(GripRequirement.Either, GripMode.TwoHands, 1.1f)]
        [TestCase(GripRequirement.TwoHandsRequired, GripMode.TwoHands, 1f)]
        [TestCase(GripRequirement.OneHandOnly, GripMode.OneHand, 1f)]
        public void GripBonusOnlyForEitherTwoHands(GripRequirement requirement, GripMode mode, float multiplier)
        {
            Equip(null, GripMode.OneHand, Weapon(AttackType.Ranged, requirement), mode);
            Assert.That(combat.TryGetAttack(WeaponSlot.RightHand, out var stats, out _), Is.True);
            Assert.That(stats.GripMultiplier, Is.EqualTo(multiplier));
        }
        [Test] public void SharedAssetDoesNotChangeOtherUnitsOrLoadout()
        {
            var weapon = Weapon(); string before = EditorJsonUtility.ToJson(weapon);
            Equip(null, GripMode.OneHand, weapon, GripMode.OneHand);
            var other = new GameObject("Other"); other.transform.SetParent(root.transform);
            var otherCombat = other.AddComponent<UnitCombat>();
            other.EnsureTestLoadout().Configure(weapon, GripMode.TwoHands, null, GripMode.OneHand);
            Assert.That(combat.TryGetAttack(WeaponSlot.RightHand, out var first, out _), Is.True);
            Assert.That(otherCombat.TryGetAttack(WeaponSlot.LeftHand, out var second, out _), Is.True);
            Assert.That(first.MinDamage, Is.EqualTo(4)); Assert.That(second.MinDamage, Is.EqualTo(4));
            Assert.That(first.GripMultiplier, Is.EqualTo(1)); Assert.That(second.GripMultiplier, Is.EqualTo(1.1f));
            Assert.That(EditorJsonUtility.ToJson(weapon), Is.EqualTo(before));
        }
        [Test] public void InvalidWeaponInvalidatesWholeLoadout()
        {
            var bad = Weapon();
            typeof(WeaponData).GetField("range", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bad, 0);
            var loadout = Equip(bad, GripMode.OneHand, Weapon(), GripMode.OneHand);
            Assert.That(loadout.IsValid(out var reason), Is.False); Assert.That(reason, Is.Not.Empty);
            Assert.That(combat.GetAttackSlots(AttackType.Ranged), Is.Empty);
        }
        [Test] public void AdapterPropertiesFollowFirstMatchingWeapon()
        {
            var melee = Weapon(AttackType.Melee); var ranged = Weapon();
            Equip(ranged, GripMode.OneHand, melee, GripMode.OneHand);
            Assert.That(combat.TryGetAttack(WeaponSlot.LeftHand, out var stats, out _), Is.True);
            Assert.That(stats.DisplayName, Is.EqualTo(ranged.DisplayName));
            Assert.That(stats.MinDamage, Is.EqualTo(ranged.MinDamage)); Assert.That(stats.MaxDamage, Is.EqualTo(ranged.MaxDamage));
            Assert.That(combat.GetAttack(AttackType.Ranged).Slot, Is.EqualTo(WeaponSlot.LeftHand));
            Assert.That(combat.GetAttack(AttackType.Melee).Slot, Is.EqualTo(WeaponSlot.RightHand));
            Assert.That(combat.GetAttack(AttackType.Ranged).Range, Is.EqualTo(ranged.Range)); Assert.That(combat.GetAttack(AttackType.Ranged).APCost, Is.EqualTo(ranged.APCost));
            Assert.That(combat.GetAttack(AttackType.Ranged).BaseHitChance, Is.EqualTo(ranged.BaseHitChance));
        }
    }
}
