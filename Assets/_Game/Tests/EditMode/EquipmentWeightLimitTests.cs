using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using MechaXCOM.EditorTools;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class EquipmentWeightLimitTests
    {
        private readonly List<Object> owned = new List<Object>();
        private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        [TearDown] public void Cleanup()
        { for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.DestroyImmediate(owned[i]); owned.Clear(); }
        private BattleUnit Unit(float limit, Vector2Int position = default)
        {
            var unit = Own(new GameObject("Weight test unit")).AddComponent<BattleUnit>();
            unit.gameObject.AddComponent<UnitWeaponLoadout>();
            var data = Own(ScriptableObject.CreateInstance<MechaData>());
            BackpackStageOneSupport.Set(data, "mechaId", "weight_test");
            BackpackStageOneSupport.Set(data, "equipmentWeightLimitTonnes", limit);
            BackpackStageOneSupport.Set(unit, "mechaData", data);
            unit.Configure(position, 5);
            return unit;
        }
        private WeaponData Weapon(float weight, GripRequirement grip = GripRequirement.OneHandOnly)
        {
            var weapon = Own(ScriptableObject.CreateInstance<WeaponData>());
            BackpackStageOneSupport.Set(weapon, "weightTonnes", weight);
            BackpackStageOneSupport.Set(weapon, "gripRequirement", grip);
            return weapon;
        }
        private BackpackData Pack(float weight, WeaponData weapon = null) =>
            (BackpackData)Own(BackpackStageOneSupport.Data("weight_pack", weight, weapon));
        private GridManager Grid()
        { var grid = Own(new GameObject("Weight test grid")).AddComponent<GridManager>(); grid.Configure(5, 5, 2); return grid; }

        [TestCase(0f, 100f)] [TestCase(5f, 5f)] [TestCase(5f, 4.9f)]
        public void ZeroLimitAndAtOrBelowLimitPermitEquipment(float limit, float weight)
        {
            var unit = Unit(limit); var loadout = unit.GetComponent<UnitWeaponLoadout>(); var weapon = Weapon(weight);
            loadout.Configure(null, GripMode.OneHand, weapon, GripMode.OneHand);
            Assert.That(loadout.RightWeapon, Is.SameAs(weapon));
            Assert.That(loadout.IsValid(out var reason), Is.True, reason);
            var grid = Grid(); grid.Generate(); Assert.That(unit.Initialize(grid), Is.True);
        }
        [Test] public void MissingMechaDataPreservesLegacyUnlimitedLoadout()
        {
            var unit = Unit(1); BackpackStageOneSupport.Set(unit, "mechaData", null);
            var loadout = unit.GetComponent<UnitWeaponLoadout>(); var weapon = Weapon(100);
            loadout.Configure(weapon, GripMode.OneHand, weapon, GripMode.OneHand);
            Assert.That(loadout.TotalEquipmentWeightTonnes, Is.EqualTo(200));
            Assert.That(loadout.IsValid(out _), Is.True);
        }
        [Test] public void OverweightHandChangeIsAtomicAndEmitsNoEvent()
        {
            var loadout = Unit(5).GetComponent<UnitWeaponLoadout>(); var old = Weapon(2); var heavy = Weapon(4);
            loadout.Configure(old, GripMode.OneHand, null, GripMode.OneHand);
            int changes = 0; loadout.Changed += () => changes++;
            loadout.Configure(old, GripMode.OneHand, heavy, GripMode.TwoHands);
            Assert.That(loadout.RightWeapon, Is.Null); Assert.That(loadout.LeftWeapon, Is.SameAs(old));
            Assert.That(loadout.RightGrip, Is.EqualTo(GripMode.OneHand)); Assert.That(changes, Is.Zero);
        }
        [Test] public void BackpackReplacementIncludesIntegratedWeaponAndPreservesOldPackOnReject()
        {
            var loadout = Unit(5).GetComponent<UnitWeaponLoadout>(); var hand = Weapon(2);
            loadout.Configure(null, GripMode.OneHand, hand, GripMode.OneHand);
            var old = Pack(1); Assert.That(loadout.TryConfigureBackpack(old), Is.True);
            var heavy = Pack(2, Weapon(2)); int changes = 0; loadout.Changed += () => changes++;
            Assert.That(loadout.TryConfigureBackpack(heavy), Is.False);
            Assert.That(loadout.Backpack, Is.SameAs(old)); Assert.That(changes, Is.Zero);
            Assert.That(loadout.TryConfigureBackpack(null), Is.True);
        }
        [Test] public void TwoHandWeaponCountsOnceAndSharedSlotsCountSeparately()
        {
            var loadout = Unit(5).GetComponent<UnitWeaponLoadout>(); var weapon = Weapon(3, GripRequirement.Either);
            loadout.Configure(null, GripMode.OneHand, weapon, GripMode.TwoHands);
            Assert.That(loadout.TryConfigureBackpack(Pack(1, Weapon(1))), Is.True);
            Assert.That(loadout.TotalEquipmentWeightTonnes, Is.EqualTo(5));
            loadout.Configure(weapon, GripMode.OneHand, weapon, GripMode.OneHand);
            Assert.That(loadout.LeftWeapon, Is.Null); Assert.That(loadout.RightGrip, Is.EqualTo(GripMode.TwoHands));
        }
        [Test] public void AssetWeightChangeBlocksDirectInitializationWithoutOccupyingGridThenCanRetry()
        {
            var unit = Unit(5); var loadout = unit.GetComponent<UnitWeaponLoadout>(); var weapon = Weapon(5);
            loadout.Configure(null, GripMode.OneHand, weapon, GripMode.OneHand);
            BackpackStageOneSupport.Set(weapon, "weightTonnes", 6f);
            Assert.That(loadout.IsValid(out var reason), Is.False); StringAssert.Contains("중량", reason);
            var grid = Grid(); grid.Generate();
            LogAssert.Expect(LogType.Warning, new Regex("중량")); Assert.That(unit.Initialize(grid), Is.False);
            Assert.That(unit.IsInitialized, Is.False); Assert.That(unit.GetComponent<UnitWeaponAmmo>(), Is.Null);
            Assert.That(grid.TryGetCell(Vector2Int.zero, out var cell), Is.True); Assert.That(cell.OccupyingUnit, Is.Null);
            BackpackStageOneSupport.Set(weapon, "weightTonnes", 5f); Assert.That(unit.Initialize(grid), Is.True);
        }
        [Test] public void OverweightUnitStopsWholeBattleBeforeGridAndOtherUnitsInitialize()
        {
            var grid = Grid(); var controller = grid.gameObject.AddComponent<BattleSceneController>();
            var cover = grid.gameObject.AddComponent<CoverSystem>(); var first = Unit(5); var second = Unit(5, Vector2Int.one);
            var loadout = second.GetComponent<UnitWeaponLoadout>(); var weapon = Weapon(5);
            loadout.Configure(null, GripMode.OneHand, weapon, GripMode.OneHand);
            BackpackStageOneSupport.Set(second.MechaData, "equipmentWeightLimitTonnes", 4f);
            controller.Configure(grid, cover, new[] { first, second }, null, null);
            LogAssert.Expect(LogType.Warning, new Regex("중량")); Assert.That(controller.InitializeBattle(), Is.False);
            Assert.That(first.IsInitialized, Is.False); Assert.That(second.IsInitialized, Is.False);
            Assert.That(grid.TryGetCell(Vector2Int.zero, out _), Is.False);
            BackpackStageOneSupport.Set(second.MechaData, "equipmentWeightLimitTonnes", 5f);
            Assert.That(controller.InitializeBattle(), Is.True);
            Assert.That(first.IsInitialized && second.IsInitialized, Is.True);
        }
        [Test] public void InspectorRejectsAllTargetsIfOneWouldExceedItsLimitAndSupportsUndo()
        {
            var first = Unit(10).GetComponent<UnitWeaponLoadout>(); var second = Unit(2).GetComponent<UnitWeaponLoadout>();
            var editor = Own(UnityEditor.Editor.CreateEditor(new Object[] { first, second }, typeof(UnitWeaponLoadoutEditor)));
            var method = typeof(UnitWeaponLoadoutEditor).GetMethod("TryApplyEquipmentChanges");
            Assert.That(method, Is.Not.Null);
            editor.serializedObject.Update();
            editor.serializedObject.FindProperty("rightWeapon").objectReferenceValue = Weapon(3);
            object[] args = { null };
            Assert.That((bool)method.Invoke(editor, args), Is.False); StringAssert.Contains("중량", (string)args[0]);
            Assert.That(first.RightWeapon, Is.Null); Assert.That(second.RightWeapon, Is.Null);
            editor.serializedObject.FindProperty("rightWeapon").objectReferenceValue = Weapon(2);
            Assert.That((bool)method.Invoke(editor, new object[] { null }), Is.True);
            Assert.That(first.RightWeapon, Is.SameAs(second.RightWeapon).And.Not.Null);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.That(first.RightWeapon, Is.Null); Assert.That(second.RightWeapon, Is.Null);
        }
        [Test] public void InspectorPreservesMixedUntouchedHandsWhenValidatingBackpackCandidate()
        {
            var first = Unit(5).GetComponent<UnitWeaponLoadout>(); var second = Unit(5).GetComponent<UnitWeaponLoadout>();
            var a = Weapon(4); var b = Weapon(1);
            first.Configure(a, GripMode.OneHand, null, GripMode.OneHand);
            second.Configure(b, GripMode.OneHand, null, GripMode.OneHand);
            var editor = Own(UnityEditor.Editor.CreateEditor(new Object[] { first, second }, typeof(UnitWeaponLoadoutEditor)));
            var method = typeof(UnitWeaponLoadoutEditor).GetMethod("TryApplyEquipmentChanges"); Assert.That(method, Is.Not.Null);
            editor.serializedObject.Update(); editor.serializedObject.FindProperty("backpack").objectReferenceValue = Pack(2);
            Assert.That((bool)method.Invoke(editor, new object[] { null }), Is.False);
            Assert.That(first.Backpack, Is.Null); Assert.That(second.Backpack, Is.Null);
            Assert.That(first.LeftWeapon, Is.SameAs(a)); Assert.That(second.LeftWeapon, Is.SameAs(b));
        }
        [Test] public void ExistingOverweightEquipmentCanBeRemovedInStagesButNotReplaced()
        {
            var unit = Unit(0); var loadout = unit.GetComponent<UnitWeaponLoadout>();
            var left = Weapon(6); var right = Weapon(7); var pack = Pack(5);
            loadout.Configure(left, GripMode.OneHand, right, GripMode.OneHand);
            Assert.That(loadout.TryConfigureBackpack(pack), Is.True);
            BackpackStageOneSupport.Set(unit.MechaData, "equipmentWeightLimitTonnes", 1f);
            Assert.That(loadout.TryConfigureBackpack(Pack(2)), Is.False, "Even a lighter replacement must respect the limit.");
            Assert.That(loadout.TryConfigureBackpack(null), Is.True, "Removing equipment must remain possible.");
            loadout.Configure(null, GripMode.OneHand, right, GripMode.OneHand);
            Assert.That(loadout.LeftWeapon, Is.Null); Assert.That(loadout.IsValid(out _), Is.False);
            var editor = Own(UnityEditor.Editor.CreateEditor(loadout, typeof(UnitWeaponLoadoutEditor)));
            editor.serializedObject.Update(); editor.serializedObject.FindProperty("rightWeapon").objectReferenceValue = null;
            var method = typeof(UnitWeaponLoadoutEditor).GetMethod("TryApplyEquipmentChanges");
            Assert.That((bool)method.Invoke(editor, new object[] { null }), Is.True);
            Assert.That(loadout.IsValid(out _), Is.True);
        }
        [TestCase(2.2f, true)] [TestCase(2.201f, false)]
        public void FractionalWeightSumAllowsEqualityButRejectsActualExcess(float rightWeight, bool allowed)
        {
            var unit = Unit(3.3f); var loadout = unit.GetComponent<UnitWeaponLoadout>();
            var left = Weapon(1.1f); var right = Weapon(rightWeight);
            Assert.That(loadout.TryConfigure(left, GripMode.OneHand, right, GripMode.OneHand), Is.EqualTo(allowed));
            // Also check authored/asset-edited configurations through the deployment gate.
            BackpackStageOneSupport.Set(loadout, "leftWeapon", left);
            BackpackStageOneSupport.Set(loadout, "rightWeapon", right);
            Assert.That(loadout.IsValid(out _), Is.EqualTo(allowed));
            var grid = Grid(); grid.Generate();
            if (!allowed) LogAssert.Expect(LogType.Warning, new Regex("중량"));
            Assert.That(unit.Initialize(grid), Is.EqualTo(allowed));
        }
    }
}
