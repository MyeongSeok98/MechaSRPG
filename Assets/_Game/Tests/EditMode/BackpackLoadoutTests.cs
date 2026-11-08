using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class BackpackLoadoutTests
    {
        private readonly List<Object> owned = new List<Object>();
        private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        [TearDown] public void Cleanup() { foreach (var value in owned) if (value) Object.DestroyImmediate(value); owned.Clear(); }
        private UnitWeaponLoadout Loadout() => Own(new GameObject("Backpack loadout test")).AddComponent<UnitWeaponLoadout>();
        private WeaponData Weapon(float weight, GripRequirement grip = GripRequirement.OneHandOnly)
        {
            var weapon = Own(ScriptableObject.CreateInstance<WeaponData>());
            BackpackStageOneSupport.Set(weapon, "weightTonnes", weight); BackpackStageOneSupport.Set(weapon, "gripRequirement", grip); return weapon;
        }
        private bool Equip(UnitWeaponLoadout loadout, ScriptableObject data) =>
            (bool)BackpackStageOneSupport.Call(loadout, "TryConfigureBackpack", data);
        private float Weight(UnitWeaponLoadout loadout) => BackpackStageOneSupport.Property<float>(loadout, "TotalEquipmentWeightTonnes");
        private WeaponData Lookup(UnitWeaponLoadout loadout, int slot) =>
            (WeaponData)BackpackStageOneSupport.Call(loadout, "GetWeapon", (WeaponSlot)slot);

        [Test] public void EmptyBackpackAndUnarmedBackpackAreValid()
        {
            var loadout = Loadout();
            Assert.That(BackpackStageOneSupport.Property<ScriptableObject>(loadout, "Backpack"), Is.Null);
            Assert.That(loadout.IsValid(out _), Is.True); Assert.That(Weight(loadout), Is.Zero);
            Assert.That(Lookup(loadout, 2), Is.Null);
            var data = Own(BackpackStageOneSupport.Data()); Assert.That(Equip(loadout, data), Is.True);
            Assert.That(loadout.IsValid(out _), Is.True); Assert.That(Weight(loadout), Is.EqualTo(1f));
            Assert.That(Lookup(loadout, 2), Is.Null); Assert.That(Equip(loadout, null), Is.True);
            Assert.That(Weight(loadout), Is.Zero);
        }
        [Test] public void TwoHandWeaponCanCoexistWithBackpackAndCountsOnce()
        {
            var loadout = Loadout(); var hand = Weapon(3, GripRequirement.TwoHandsRequired); var integrated = Weapon(2);
            var data = Own(BackpackStageOneSupport.Data(weapon: integrated));
            loadout.Configure(null, GripMode.OneHand, hand, GripMode.TwoHands);
            Assert.That(Equip(loadout, data), Is.True); Assert.That(loadout.IsValid(out var reason), Is.True, reason);
            Assert.That(loadout.LeftWeapon, Is.Null); Assert.That(loadout.RightWeapon, Is.SameAs(hand));
            Assert.That(Weight(loadout), Is.EqualTo(6f));
            Assert.That(Lookup(loadout, 0), Is.SameAs(hand)); Assert.That(Lookup(loadout, 1), Is.Null);
            Assert.That(Lookup(loadout, 2), Is.SameAs(integrated)); Assert.That(Lookup(loadout, 99), Is.Null);
            Assert.That((int)WeaponSlot.RightHand, Is.Zero); Assert.That((int)WeaponSlot.LeftHand, Is.EqualTo(1));
            Assert.That(System.Enum.GetName(typeof(WeaponSlot), 2), Is.EqualTo("Backpack"));
        }
        [Test] public void BackpackWeightCountsIntegratedWeaponOnceAndSharedHandsAsTwoItems()
        {
            var loadout = Loadout(); var weapon = Weapon(2);
            var data = Own(BackpackStageOneSupport.Data(weapon: weapon));
            string original = JsonUtility.ToJson(data), originalWeapon = JsonUtility.ToJson(weapon);
            loadout.Configure(weapon, GripMode.OneHand, weapon, GripMode.OneHand);
            Assert.That(Equip(loadout, data), Is.True);
            Assert.That(Weight(loadout), Is.EqualTo(7f), "2+2+1+2: shared definition represents3 physically distinct weapons.");
            loadout.Configure(null, GripMode.OneHand, null, GripMode.OneHand);
            Assert.That(BackpackStageOneSupport.Property<ScriptableObject>(loadout, "Backpack"), Is.SameAs(data));
            Assert.That(Weight(loadout), Is.EqualTo(3f));
            Assert.That(JsonUtility.ToJson(data), Is.EqualTo(original)); Assert.That(JsonUtility.ToJson(weapon), Is.EqualTo(originalWeapon));
        }
        [Test] public void ReplacingBackpackReplacesIntegratedWeaponWithoutTouchingHands()
        {
            var loadout = Loadout(); var hand = Weapon(3); var a = Weapon(2); var b = Weapon(4);
            var first = Own(BackpackStageOneSupport.Data("first", 1, a));
            var second = Own(BackpackStageOneSupport.Data("second", 2, b));
            loadout.Configure(hand, GripMode.OneHand, null, GripMode.OneHand);
            Assert.That(Equip(loadout, first), Is.True); Assert.That(Lookup(loadout, 2), Is.SameAs(a));
            Assert.That(Equip(loadout, second), Is.True); Assert.That(Lookup(loadout, 2), Is.SameAs(b));
            Assert.That(loadout.LeftWeapon, Is.SameAs(hand)); Assert.That(Weight(loadout), Is.EqualTo(9f));
        }
        [Test] public void InvalidBackpackIsRejectedAndSerializedInvalidBackpackInvalidatesLoadout()
        {
            var loadout = Loadout(); var valid = Own(BackpackStageOneSupport.Data());
            var invalid = Own(BackpackStageOneSupport.Data("", -1));
            Assert.That(Equip(loadout, valid), Is.True); int changes = 0; loadout.Changed += () => changes++;
            Assert.That(Equip(loadout, invalid), Is.False); Assert.That(changes, Is.Zero);
            Assert.That(BackpackStageOneSupport.Property<ScriptableObject>(loadout, "Backpack"), Is.SameAs(valid));
            BackpackStageOneSupport.Set(loadout, "backpack", invalid);
            Assert.That(loadout.IsValid(out var reason), Is.False); Assert.That(reason, Is.Not.Empty);
        }
        [Test] public void BackpackCannotBeChangedAfterInitialization()
        {
            var root = Own(new GameObject("Initialized backpack unit"));
            var grid = root.AddComponent<GridManager>(); grid.Configure(8, 8, 2); grid.Generate();
            var unit = Own(new GameObject("Unit")).AddTestBattleUnit(); unit.Configure(new Vector2Int(1, 1), 5);
            var loadout = unit.GetComponent<UnitWeaponLoadout>(); var data = Own(BackpackStageOneSupport.Data());
            Assert.That(Equip(loadout, data), Is.True); Assert.That(unit.Initialize(grid), Is.True);
            int changes = 0; loadout.Changed += () => changes++;
            Assert.That(Equip(loadout, null), Is.False); Assert.That(changes, Is.Zero);
            Assert.That(BackpackStageOneSupport.Property<ScriptableObject>(loadout, "Backpack"), Is.SameAs(data));
        }
    }
}
