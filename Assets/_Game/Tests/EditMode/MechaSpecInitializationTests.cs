using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class MechaSpecInitializationTests
    {
        private GameObject root;
        private GridManager grid;
        private MechaData data;
        [SetUp] public void Setup()
        {
            root = new GameObject("Spec initialization");
            grid = root.AddComponent<GridManager>(); grid.Configure(10, 10, 2); grid.Generate();
            data = ScriptableObject.CreateInstance<MechaData>();
            JsonUtility.FromJsonOverwrite("{\"mechaId\":\"test_mecha\",\"baseAP\":7,\"bodyMaxHP\":35,\"cockpitMaxHP\":12,\"headMaxHP\":9,\"leftArmMaxHP\":13,\"rightArmMaxHP\":14,\"leftLegMaxHP\":17,\"rightLegMaxHP\":18}", data);
        }
        [TearDown] public void Cleanup()
        { Object.DestroyImmediate(root); Object.DestroyImmediate(data); }
        private BattleUnit Unit(int x, bool bind = true)
        {
            var go = new GameObject("Unit " + x); go.transform.SetParent(root.transform);
            var unit = go.AddTestBattleUnit(); unit.Configure(new Vector2Int(x, 1), 5);
            if (bind) Bind(unit, data);
            return unit;
        }
        private static void Bind(BattleUnit unit, MechaData spec)
        {
            var settings = new SerializedObject(unit);
            var field = settings.FindProperty("mechaData");
            Assert.That(field, Is.Not.Null, "BattleUnit must own the shared MechaData reference.");
            field.objectReferenceValue = spec; settings.ApplyModifiedPropertiesWithoutUndo();
        }
        [Test] public void DataOverridesFallbackHPAndAPAtInitialization()
        {
            var unit = Unit(1);
            unit.GetComponent<UnitHealth>().ConfigurePartHP(TargetBodyPart.Torso, 2);
            unit.GetComponent<UnitActionPoints>().Configure(1);
            Assert.That(unit.Initialize(grid), Is.True);
            int[] expected = {35, 12, 9, 13, 14, 17, 18};
            for (int i = 0; i < expected.Length; i++)
            {
                var part = (TargetBodyPart)(i + 1);
                Assert.That(unit.Health.GetMaxHP(part), Is.EqualTo(expected[i]));
                Assert.That(unit.Health.GetCurrentHP(part), Is.EqualTo(expected[i]));
            }
            Assert.That(unit.ActionPoints.MaxPoints, Is.EqualTo(7));
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(7));
        }
        [Test] public void SharedDataDoesNotShareDamageOrSpentAP()
        {
            string before = EditorJsonUtility.ToJson(data);
            var first = Unit(1); var second = Unit(2);
            Assert.That(first.Initialize(grid), Is.True); Assert.That(second.Initialize(grid), Is.True);
            first.Health.ApplyDamage(TargetBodyPart.LeftArm, 3); first.ActionPoints.TrySpend(4);
            Assert.That(first.Health.GetCurrentHP(TargetBodyPart.LeftArm), Is.EqualTo(10));
            Assert.That(second.Health.GetCurrentHP(TargetBodyPart.LeftArm), Is.EqualTo(13));
            Assert.That(second.ActionPoints.CurrentPoints, Is.EqualTo(7));
            first.ActionPoints.ResetForTurn(); Assert.That(first.ActionPoints.CurrentPoints, Is.EqualTo(7));
            Assert.That(EditorJsonUtility.ToJson(data), Is.EqualTo(before));
        }
        [Test] public void ReinitializationDoesNotHealOrApplyEditedSpecsMidBattle()
        {
            var unit = Unit(1); Assert.That(unit.Initialize(grid), Is.True);
            unit.Health.ApplyDamage(TargetBodyPart.Torso, 5); unit.ActionPoints.TrySpend(3);
            JsonUtility.FromJsonOverwrite("{\"baseAP\":9,\"bodyMaxHP\":99}", data);
            Assert.That(unit.Initialize(grid), Is.True);
            Assert.That(unit.Health.GetMaxHP(TargetBodyPart.Torso), Is.EqualTo(35));
            Assert.That(unit.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(30));
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(4));
            unit.ActionPoints.ResetForTurn(); Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(7));
            var later = Unit(2); Assert.That(later.Initialize(grid), Is.True);
            Assert.That(later.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(99));
            Assert.That(later.ActionPoints.CurrentPoints, Is.EqualTo(9));
        }
        [Test] public void MissingDataKeepsExistingFallbackConfiguration()
        {
            var unit = Unit(1, false);
            unit.GetComponent<UnitHealth>().ConfigurePartHP(TargetBodyPart.Torso, 28);
            unit.GetComponent<UnitActionPoints>().Configure(4);
            Assert.That(unit.Initialize(grid), Is.True);
            Assert.That(unit.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(28));
            Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(4));
        }
        [Test] public void InvalidDataRejectsInitializationWithoutOccupyingTheGrid()
        {
            var unit = Unit(1); JsonUtility.FromJsonOverwrite("{\"mechaId\":\"\"}", data);
            LogAssert.Expect(LogType.Warning, new Regex("invalid MechaData"));
            Assert.That(unit.Initialize(grid), Is.False);
            Assert.That(unit.IsInitialized, Is.False);
            grid.TryGetCell(new Vector2Int(1, 1), out var cell); Assert.That(cell.Occupied, Is.False);
            JsonUtility.FromJsonOverwrite("{\"mechaId\":\"fixed\"}", data);
            Assert.That(unit.Initialize(grid), Is.True);
        }
        [Test] public void ModelIsPassedFromTheSameUnitData()
        {
            var unit = Unit(1);
            var settings = new SerializedObject(data);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Mecha/Samples/MechaReferenceWithRifle.prefab");
            settings.FindProperty("modelPrefab").objectReferenceValue = model; settings.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(unit.Initialize(grid), Is.True);
            var visual = unit.GetComponent<UnitMechaVisual>();
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.ModelInstance, Is.Not.Null);
            Assert.That(new SerializedObject(visual).FindProperty("mechaData"), Is.Null,
                "The visual must receive a model rather than own another data reference.");
        }
    }
}
