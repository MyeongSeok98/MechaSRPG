using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using MechaXCOM.EditorTools;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class AmmoLoadoutTests
    {
        private readonly List<Object> owned = new List<Object>();
        private T Own<T>(T obj) where T : Object { owned.Add(obj); return obj; }
        [TearDown] public void Cleanup()
        { for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.DestroyImmediate(owned[i]); owned.Clear(); }
        private UnitWeaponLoadout Loadout(float limit = 0)
        {
            var unit = Own(new GameObject("Ammo loadout")).AddComponent<BattleUnit>();
            var data = Own(ScriptableObject.CreateInstance<MechaData>());
            BackpackStageOneSupport.Set(data, "mechaId", "ammo_loadout_test");
            BackpackStageOneSupport.Set(data, "equipmentWeightLimitTonnes", limit);
            BackpackStageOneSupport.Set(unit, "mechaData", data);
            return unit.gameObject.AddComponent<UnitWeaponLoadout>();
        }
        private WeaponData Weapon(float body = 2, float magazine = .5f, int recommended = 2)
        {
            var weapon = Own(ScriptableObject.CreateInstance<WeaponData>());
            JsonUtility.FromJsonOverwrite("{\"weightTonnes\":" + body.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                ",\"magazineWeightTonnes\":" + magazine.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                ",\"spareMagazineCount\":" + recommended + ",\"gripRequirement\":0}", weapon);
            return weapon;
        }
        private bool SetCounts(UnitWeaponLoadout loadout, int left, int right)
        {
            var method = typeof(UnitWeaponLoadout).GetMethod("TryConfigureSpareMagazines");
            Assert.That(method, Is.Not.Null, "Per-unit reserve selection API is required.");
            return (bool)method.Invoke(loadout, new object[] { left, right });
        }
        [Test] public void BodyLoadedMagazineAndSelectedSparesAreCountedSeparately()
        {
            var loadout = Loadout(); var weapon = Weapon();
            loadout.Configure(weapon, GripMode.OneHand, weapon, GripMode.OneHand);
            Assert.That(SetCounts(loadout, 0, 4), Is.True);
            Assert.That(loadout.TotalEquipmentWeightTonnes, Is.EqualTo(7));
            Assert.That(weapon.SpareMagazineCount, Is.EqualTo(2));
        }
        [Test] public void SharedWeaponAllowsDifferentCountsOnDifferentUnits()
        {
            var a = Loadout(); var b = Loadout(); var weapon = Weapon();
            a.Configure(null, GripMode.OneHand, weapon, GripMode.OneHand);
            b.Configure(null, GripMode.OneHand, weapon, GripMode.OneHand);
            Assert.That(SetCounts(a, 0, 0), Is.True); Assert.That(SetCounts(b, 0, 3), Is.True);
            Assert.That(a.TotalEquipmentWeightTonnes, Is.EqualTo(2.5f));
            Assert.That(b.TotalEquipmentWeightTonnes, Is.EqualTo(4));
        }
        [Test] public void OverweightReserveSelectionIsRejectedAtomically()
        {
            var loadout = Loadout(3.5f); var weapon = Weapon();
            loadout.Configure(null, GripMode.OneHand, weapon, GripMode.OneHand);
            int events = 0; loadout.Changed += () => events++;
            Assert.That(SetCounts(loadout, 0, 3), Is.False);
            Assert.That(loadout.TotalEquipmentWeightTonnes, Is.EqualTo(3.5f)); Assert.That(events, Is.Zero);
            Assert.That(SetCounts(loadout, 0, 0), Is.True);
            Assert.That(loadout.TotalEquipmentWeightTonnes, Is.EqualTo(2.5f));
        }
        [Test] public void LegacyRecommendationAndBackpackLoadedMagazineContributeToWeight()
        {
            var loadout = Loadout(); var weapon = Weapon();
            loadout.Configure(null, GripMode.OneHand, weapon, GripMode.OneHand);
            var pack = Own((BackpackData)BackpackStageOneSupport.Data("ammo_pack", 1, weapon));
            Assert.That(loadout.TryConfigureBackpack(pack), Is.True);
            Assert.That(loadout.TotalEquipmentWeightTonnes, Is.EqualTo(7));
        }
        [Test] public void InspectorRejectsSpareCountThatExceedsWeightLimit()
        {
            var loadout = Loadout(3.5f); var weapon = Weapon();
            loadout.Configure(null, GripMode.OneHand, weapon, GripMode.OneHand);
            var editor = Own(UnityEditor.Editor.CreateEditor(loadout)) as UnitWeaponLoadoutEditor;
            editor.serializedObject.Update();
            var property = editor.serializedObject.FindProperty("rightSpareMagazineCount");
            Assert.That(property, Is.Not.Null);
            property.intValue = 3;
            Assert.That(editor.TryApplyEquipmentChanges(out _), Is.False);
            Assert.That(loadout.TotalEquipmentWeightTonnes, Is.EqualTo(3.5f));
        }
        [Test] public void NegativeOrNonfiniteMagazineWeightIsInvalid()
        {
            var weapon = Weapon();
            var field = typeof(WeaponData).GetField("magazineWeightTonnes", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            foreach (float value in new[] { -1f, float.NaN, float.PositiveInfinity })
            { field.SetValue(weapon, value); Assert.That(weapon.IsValid(out _), Is.False); }
        }
        private void Initialize(UnitWeaponLoadout loadout)
        {
            var grid = Own(new GameObject("Ammo grid")).AddComponent<GridManager>();
            grid.Configure(4, 4, 2); grid.Generate();
            Assert.That(loadout.GetComponent<BattleUnit>().Initialize(grid), Is.True);
        }
        [Test] public void BattleAmmoUsesSelectedCountsAndNeverRefillsFromDefinitionEdits()
        {
            var loadout = Loadout(); var weapon = Weapon();
            loadout.Configure(weapon, GripMode.OneHand, weapon, GripMode.OneHand);
            Assert.That(SetCounts(loadout, 0, 4), Is.True); Initialize(loadout);
            var unit = loadout.GetComponent<BattleUnit>();
            Assert.That(unit.Ammo.GetStatus(WeaponSlot.LeftHand).SpareMagazines, Is.Zero);
            Assert.That(unit.Ammo.GetStatus(WeaponSlot.RightHand).SpareMagazines, Is.EqualTo(4));
            Assert.That(SetCounts(loadout, 3, 3), Is.False);
            BackpackStageOneSupport.Set(weapon, "spareMagazineCount", 12);
            loadout.Configure(weapon, GripMode.OneHand, weapon, GripMode.OneHand);
            Assert.That(unit.Ammo.GetStatus(WeaponSlot.RightHand).SpareMagazines, Is.EqualTo(4));
            Assert.That(loadout.TotalEquipmentWeightTonnes, Is.EqualTo(7));
        }
        [Test] public void LegacyCountIsCapturedAtBattleStartWithoutMutatingWeapon()
        {
            var loadout = Loadout(); var weapon = Weapon(recommended: 10);
            loadout.Configure(null, GripMode.OneHand, weapon, GripMode.OneHand); Initialize(loadout);
            Assert.That(loadout.GetComponent<BattleUnit>().Ammo.GetStatus(WeaponSlot.RightHand).SpareMagazines, Is.EqualTo(10));
            BackpackStageOneSupport.Set(weapon, "spareMagazineCount", 2);
            Assert.That(loadout.TotalEquipmentWeightTonnes, Is.EqualTo(7.5f));
        }
        [Test] public void ReserveReductionRepairsOverweightInStages()
        {
            var loadout = Loadout(10); var weapon = Weapon();
            loadout.Configure(null, GripMode.OneHand, weapon, GripMode.OneHand);
            Assert.That(SetCounts(loadout, 0, 8), Is.True);
            BackpackStageOneSupport.Set(loadout.GetComponent<BattleUnit>().MechaData, "equipmentWeightLimitTonnes", 3f);
            Assert.That(SetCounts(loadout, 0, 7), Is.True);
            Assert.That(loadout.IsWithinWeightLimit(out _), Is.False);
            Assert.That(SetCounts(loadout, 0, 8), Is.False);
            Assert.That(SetCounts(loadout, 0, 1), Is.True);
            Assert.That(loadout.IsWithinWeightLimit(out _), Is.True);
        }
        [Test] public void MaximumReserveCountDoesNotOverflowIntoNegativeMass()
        {
            var loadout = Loadout(); var weapon = Weapon(body: 0, magazine: 1);
            loadout.Configure(null, GripMode.OneHand, weapon, GripMode.OneHand);
            Assert.That(SetCounts(loadout, 0, int.MaxValue), Is.True);
            Assert.That(loadout.TotalEquipmentWeightTonnes, Is.GreaterThan(2e9f));
        }
        [Test] public void SurvivingHandTransferKeepsChosenReservesAndDeploymentMass()
        {
            var loadout = Loadout(); var weapon = Weapon();
            BackpackStageOneSupport.Set(weapon, "gripRequirement", GripRequirement.Either);
            loadout.Configure(weapon, GripMode.TwoHands, null, GripMode.OneHand);
            Assert.That(SetCounts(loadout, 4, 0), Is.True); Initialize(loadout);
            var unit = loadout.GetComponent<BattleUnit>();
            unit.Health.ApplyDamage(TargetBodyPart.LeftArm, unit.Health.GetCurrentHP(TargetBodyPart.LeftArm));
            Assert.That(loadout.RightWeapon, Is.SameAs(weapon));
            Assert.That(loadout.GetSpareMagazineCount(WeaponSlot.RightHand), Is.EqualTo(4));
            Assert.That(unit.Ammo.GetStatus(WeaponSlot.RightHand).SpareMagazines, Is.EqualTo(4));
            Assert.That(loadout.TotalEquipmentWeightTonnes, Is.EqualTo(4.5f));
        }
        [Test] public void InspectorReserveEditsAreAtomicAcrossUnitsAndUndoable()
        {
            var a = Loadout(8); var b = Loadout(3.5f); var weapon = Weapon();
            a.Configure(null, GripMode.OneHand, weapon, GripMode.OneHand);
            b.Configure(null, GripMode.OneHand, weapon, GripMode.OneHand);
            var editor = (UnitWeaponLoadoutEditor)Own(UnityEditor.Editor.CreateEditor(new Object[] { a, b }, typeof(UnitWeaponLoadoutEditor)));
            editor.serializedObject.Update(); editor.serializedObject.FindProperty("rightSpareMagazineCount").intValue = 3;
            Assert.That(editor.TryApplyEquipmentChanges(out _), Is.False);
            Assert.That(a.RightSpareMagazineSelection, Is.EqualTo(-1));
            Assert.That(b.RightSpareMagazineSelection, Is.EqualTo(-1));
            editor.serializedObject.FindProperty("rightSpareMagazineCount").intValue = 0;
            Assert.That(editor.TryApplyEquipmentChanges(out _), Is.True);
            Assert.That(a.GetSpareMagazineCount(WeaponSlot.RightHand), Is.Zero);
            Assert.That(b.GetSpareMagazineCount(WeaponSlot.RightHand), Is.Zero);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.That(a.RightSpareMagazineSelection, Is.EqualTo(-1));
            Assert.That(b.RightSpareMagazineSelection, Is.EqualTo(-1));
        }
        [Test] public void InspectorPreservesMixedReserveCountsWhenEditingBackpack()
        {
            var a = Loadout(4); var b = Loadout(4); var weapon = Weapon();
            a.Configure(null, GripMode.OneHand, weapon, GripMode.OneHand);
            b.Configure(null, GripMode.OneHand, weapon, GripMode.OneHand);
            Assert.That(SetCounts(a, 0, 0), Is.True); Assert.That(SetCounts(b, 0, 2), Is.True);
            var pack = Own((BackpackData)BackpackStageOneSupport.Data("mixed_pack", 1, null));
            var editor = (UnitWeaponLoadoutEditor)Own(UnityEditor.Editor.CreateEditor(new Object[] { a, b }, typeof(UnitWeaponLoadoutEditor)));
            editor.serializedObject.Update(); editor.serializedObject.FindProperty("backpack").objectReferenceValue = pack;
            Assert.That(editor.TryApplyEquipmentChanges(out _), Is.False, "Second unit is overweight with its own reserves.");
            Assert.That(a.Backpack, Is.Null); Assert.That(b.Backpack, Is.Null);
            Assert.That(a.GetSpareMagazineCount(WeaponSlot.RightHand), Is.Zero);
            Assert.That(b.GetSpareMagazineCount(WeaponSlot.RightHand), Is.EqualTo(2));
        }
        [Test] public void InspectorCanEquipWeaponWithFewerReservesInOneAtomicChange()
        {
            var loadout = Loadout(2.5f); var weapon = Weapon();
            var editor = (UnitWeaponLoadoutEditor)Own(UnityEditor.Editor.CreateEditor(loadout));
            editor.serializedObject.Update();
            editor.serializedObject.FindProperty("rightWeapon").objectReferenceValue = weapon;
            editor.serializedObject.FindProperty("rightSpareMagazineCount").intValue = 0;
            Assert.That(editor.TryApplyEquipmentChanges(out _), Is.True);
            Assert.That(loadout.TotalEquipmentWeightTonnes, Is.EqualTo(2.5f));
        }
        [Test] public void InspectorCannotChangeReserveCountAfterBattleStarts()
        {
            var loadout = Loadout(); loadout.Configure(null, GripMode.OneHand, Weapon(), GripMode.OneHand); Initialize(loadout);
            var editor = (UnitWeaponLoadoutEditor)Own(UnityEditor.Editor.CreateEditor(loadout));
            editor.serializedObject.Update(); editor.serializedObject.FindProperty("rightSpareMagazineCount").intValue = 9;
            Assert.That(editor.TryApplyEquipmentChanges(out _), Is.False);
            Assert.That(loadout.GetComponent<BattleUnit>().Ammo.GetStatus(WeaponSlot.RightHand).SpareMagazines, Is.EqualTo(2));
        }
        [Test] public void MeleeIgnoresAmmoMassAndReserveCount()
        {
            var loadout = Loadout(); var weapon = Weapon(magazine: 100);
            BackpackStageOneSupport.Set(weapon, "attackType", AttackType.Melee);
            loadout.Configure(null, GripMode.OneHand, weapon, GripMode.OneHand);
            Assert.That(SetCounts(loadout, 0, 100), Is.True);
            Assert.That(loadout.TotalEquipmentWeightTonnes, Is.EqualTo(2)); Initialize(loadout);
            Assert.That(loadout.GetComponent<BattleUnit>().Ammo.GetStatus(WeaponSlot.RightHand).UsesAmmo, Is.False);
        }
        [Test] public void AuthoredAmmoWeightChangeBlocksDeploymentBeforeOccupyingCell()
        {
            var loadout = Loadout(3.5f); var weapon = Weapon();
            loadout.Configure(null, GripMode.OneHand, weapon, GripMode.OneHand);
            BackpackStageOneSupport.Set(weapon, "magazineWeightTonnes", 1f);
            var grid = Own(new GameObject("Weight grid")).AddComponent<GridManager>();
            grid.Configure(4, 4, 2); grid.Generate();
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("중량"));
            Assert.That(loadout.GetComponent<BattleUnit>().Initialize(grid), Is.False);
            Assert.That(loadout.GetComponent<BattleUnit>().IsInitialized, Is.False);
            Assert.That(SetCounts(loadout, 0, 0), Is.True);
            Assert.That(loadout.GetComponent<BattleUnit>().Initialize(grid), Is.True);
        }
        [Test] public void MagazineMassAndChosenReservesSurviveSerialization()
        {
            var loadout = Loadout(); var copy = Loadout(); var weapon = Weapon(magazine: .25f);
            loadout.Configure(null, GripMode.OneHand, weapon, GripMode.OneHand);
            Assert.That(SetCounts(loadout, 0, 7), Is.True);
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(loadout), copy);
            Assert.That(copy.GetSpareMagazineCount(WeaponSlot.RightHand), Is.EqualTo(7));
            var definitionCopy = Own(ScriptableObject.CreateInstance<WeaponData>());
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(weapon), definitionCopy);
            Assert.That(definitionCopy.MagazineWeightTonnes, Is.EqualTo(.25f));
            Assert.That(copy.TotalEquipmentWeightTonnes, Is.EqualTo(4));
        }
        [Test] public void APIEquipsWeaponAndSelectedReservesAtomicallyUnderWeightCap()
        {
            var loadout = Loadout(2.5f); var weapon = Weapon();
            var method = typeof(UnitWeaponLoadout).GetMethod("TryConfigure", new[] {
                typeof(WeaponData), typeof(GripMode), typeof(WeaponData), typeof(GripMode), typeof(int), typeof(int) });
            Assert.That(method, Is.Not.Null, "An atomic weapon-and-reserves API is required when defaults would exceed the cap.");
            Assert.That((bool)method.Invoke(loadout, new object[] { null, GripMode.OneHand, weapon, GripMode.OneHand, 0, 0 }), Is.True);
            Assert.That(loadout.RightWeapon, Is.SameAs(weapon));
            Assert.That(loadout.TotalEquipmentWeightTonnes, Is.EqualTo(2.5f));
            int events = 0; loadout.Changed += () => events++;
            Assert.That((bool)method.Invoke(loadout, new object[] { weapon, GripMode.OneHand, weapon, GripMode.OneHand, 0, 0 }), Is.False);
            Assert.That(loadout.LeftWeapon, Is.Null); Assert.That(events, Is.Zero);
            Initialize(loadout);
            Assert.That((bool)method.Invoke(loadout, new object[] { null, GripMode.OneHand, weapon, GripMode.OneHand, 0, 3 }), Is.False);
            Assert.That(loadout.GetComponent<BattleUnit>().Ammo.GetStatus(WeaponSlot.RightHand).SpareMagazines, Is.Zero);
        }
    }
}
