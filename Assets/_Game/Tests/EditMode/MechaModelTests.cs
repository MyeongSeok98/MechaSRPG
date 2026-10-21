using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class MechaModelTests
    {
        private const string ModelPath = "Assets/_Game/Prefabs/Mecha/Samples/MechaReferenceWithRifle.prefab";
        private const string DataPath = "Assets/_Game/Data/Mecha/SampleMecha.asset";
        private Type Presenter => typeof(BattleUnit).Assembly.GetType("MechaXCOM.UnitMechaVisual");
        [Test] public void SampleAndBothUnitPrefabsReferenceTheExistingModel()
        {
            var data = AssetDatabase.LoadAssetAtPath<MechaData>(DataPath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var property = typeof(MechaData).GetProperty("ModelPrefab");
            Assert.That(property, Is.Not.Null, "MechaData must expose an optional model prefab.");
            Assert.That(property.GetValue(data), Is.SameAs(model));
            Assert.That(Presenter, Is.Not.Null);
            foreach (var path in new[] { "Assets/_Game/Prefabs/BattleUnit.prefab", "Assets/_Game/Prefabs/DummyEnemy.prefab" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var presenter = prefab.GetComponent(Presenter);
                Assert.That(presenter, Is.Not.Null, path);
                var settings = new SerializedObject(presenter);
                Assert.That(new SerializedObject(prefab.GetComponent<BattleUnit>()).FindProperty("mechaData").objectReferenceValue, Is.SameAs(data));
                var instance = settings.FindProperty("modelInstance").objectReferenceValue as GameObject;
                Assert.That(instance, Is.Not.Null);
                Assert.That(instance.transform.parent, Is.SameAs(prefab.transform.Find("Visual")));
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(instance), Is.SameAs(model));
                Assert.That(prefab.GetComponent<MeshRenderer>().enabled, Is.False);
                Assert.That(prefab.GetComponent<CapsuleCollider>().enabled, Is.True);
            }
        }
        [Test] public void InitializationCreatesVisualBeforeMovementAndIsIdempotent()
        {
            Assert.That(Presenter, Is.Not.Null);
            var root = new GameObject("Visual unit");
            var gridObject = new GameObject("Grid");
            try
            {
                var unit = root.AddTestBattleUnit();
                var grid = gridObject.AddComponent<GridManager>(); grid.Generate();
                var presenter = root.AddComponent(Presenter);
                var settings = new SerializedObject(unit);
                settings.FindProperty("mechaData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MechaData>(DataPath);
                settings.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(unit.Initialize(grid), Is.True);
                var visual = root.transform.Find("Visual");
                Assert.That(visual, Is.Not.Null);
                Assert.That(visual.localPosition, Is.EqualTo(Vector3.down * unit.HeightOffset));
                Assert.That(visual.childCount, Is.EqualTo(1));
                var model = visual.GetChild(0);
                Presenter.GetMethod("Initialize").Invoke(presenter, new object[] { unit.MechaData ? unit.MechaData.ModelPrefab : null, unit.HeightOffset });
                Assert.That(visual.childCount, Is.EqualTo(1));
                Assert.That(visual.GetChild(0), Is.SameAs(model));
                Assert.That(unit.ActionPoints.CurrentPoints, Is.EqualTo(5));
                Assert.That(unit.Health.GetMaxHP(TargetBodyPart.Torso), Is.EqualTo(20));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(gridObject); }
        }
        [Test] public void MissingModelKeepsExistingVisualAndRootRenderer()
        {
            Assert.That(Presenter, Is.Not.Null);
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                var unit = root.AddTestBattleUnit();
                var visual = new GameObject("Visual"); visual.transform.SetParent(root.transform, false);
                var model = new GameObject("Authored model"); model.transform.SetParent(visual.transform, false);
                var presenter = root.AddComponent(Presenter);
                Presenter.GetMethod("Initialize").Invoke(presenter, new object[] { unit.MechaData ? unit.MechaData.ModelPrefab : null, unit.HeightOffset });
                Assert.That(model.transform.parent, Is.SameAs(visual.transform));
                Assert.That(root.GetComponent<MeshRenderer>().enabled, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test] public void ChangingModelOnlyReplacesItsOwnedInstance()
        {
            Assert.That(Presenter, Is.Not.Null);
            var root = new GameObject("Changing model");
            var data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<MechaData>(DataPath));
            try
            {
                var unit = root.AddTestBattleUnit();
                root.transform.position = new Vector3(8, 2, 6);
                var visual = new GameObject("Visual").transform; visual.SetParent(root.transform, false);
                visual.localPosition = new Vector3(0, -2, 0); visual.localScale = Vector3.one * 2;
                visual.localRotation = Quaternion.Euler(0, 90, 0);
                var marker = new GameObject("Preserved marker"); marker.transform.SetParent(visual, false);
                var presenter = root.AddComponent(Presenter);
                var settings = new SerializedObject(unit);
                settings.FindProperty("mechaData").objectReferenceValue = data;
                settings.ApplyModifiedPropertiesWithoutUndo();
                Presenter.GetMethod("Initialize").Invoke(presenter, new object[] { unit.MechaData ? unit.MechaData.ModelPrefab : null, unit.HeightOffset });
                var first = (GameObject)Presenter.GetProperty("ModelInstance").GetValue(presenter);
                var model = new SerializedObject(data);
                model.FindProperty("modelPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/HalfCover.prefab");
                model.ApplyModifiedPropertiesWithoutUndo();
                Presenter.GetMethod("Initialize").Invoke(presenter, new object[] { unit.MechaData ? unit.MechaData.ModelPrefab : null, unit.HeightOffset });
                Assert.That(first == null, Is.True);
                Assert.That(visual.childCount, Is.EqualTo(2));
                Assert.That(marker.transform.parent, Is.SameAs(visual));
                Assert.That(root.transform.position, Is.EqualTo(new Vector3(8, 2, 6)));
                Assert.That(visual.localPosition, Is.EqualTo(new Vector3(0, -2, 0)));
                Assert.That(visual.localScale, Is.EqualTo(Vector3.one * 2));
                Assert.That(Quaternion.Angle(visual.localRotation, Quaternion.Euler(0, 90, 0)), Is.LessThan(.01f));
                Assert.That(AssetDatabase.LoadAssetAtPath<MechaData>(DataPath).ModelPrefab.name, Is.EqualTo("MechaReferenceWithRifle"));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(data); }
        }
    }
}
