using System;
using UnityEditor;
using UnityEngine;

namespace MechaXCOM.Editor
{
    public static class SampleMechaModelSetup
    {
        [MenuItem("MechaXCOM/Apply Sample Mecha Models")]
        public static void Apply()
        {
            var data = AssetDatabase.LoadAssetAtPath<MechaData>("Assets/_Game/Data/Mecha/SampleMecha.asset");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponSamplePrefabBuilder.MechaPath);
            if (!data || !model) throw new InvalidOperationException("Sample mecha data or model is missing.");
            var settings = new SerializedObject(data);
            settings.FindProperty("modelPrefab").objectReferenceValue = model;
            settings.ApplyModifiedPropertiesWithoutUndo();
            foreach (var path in new[] { "Assets/_Game/Prefabs/BattleUnit.prefab", "Assets/_Game/Prefabs/DummyEnemy.prefab" })
            {
                var unit = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var visual = unit.transform.Find("Visual");
                    if (!visual)
                    {
                        visual = new GameObject("Visual").transform;
                        visual.SetParent(unit.transform, false);
                        visual.localPosition = Vector3.down * unit.GetComponent<BattleUnit>().HeightOffset;
                    }
                    GameObject instance;
                    if (visual.childCount == 0)
                        instance = (GameObject)PrefabUtility.InstantiatePrefab(model, visual);
                    else
                    {
                        if (visual.childCount != 1 ||
                            PrefabUtility.GetCorrespondingObjectFromSource(visual.GetChild(0).gameObject) != model)
                            throw new InvalidOperationException(path + ": preserve the authored Visual; expected the existing sample model.");
                        instance = visual.GetChild(0).gameObject;
                    }
                    var presenter = unit.GetComponent<UnitMechaVisual>() ?? unit.AddComponent<UnitMechaVisual>();
                    var unitSettings = new SerializedObject(unit.GetComponent<BattleUnit>());
                    unitSettings.FindProperty("mechaData").objectReferenceValue = data;
                    unitSettings.ApplyModifiedPropertiesWithoutUndo();
                    var component = new SerializedObject(presenter);
                    component.FindProperty("modelInstance").objectReferenceValue = instance;
                    component.FindProperty("appliedModelPrefab").objectReferenceValue = model;
                    component.ApplyModifiedPropertiesWithoutUndo();
                    var primitive = unit.GetComponent<Renderer>();
                    if (primitive) primitive.enabled = false;
                    PrefabUtility.SaveAsPrefabAsset(unit, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(unit); }
            }
            AssetDatabase.SaveAssets();
        }
        public static void RenderPreview()
        {
            WeaponSamplePrefabBuilder.RenderPreview("Assets/_Game/Prefabs/BattleUnit.prefab", "Artifacts/MechaModelPlayer.png");
            WeaponSamplePrefabBuilder.RenderPreview("Assets/_Game/Prefabs/DummyEnemy.prefab", "Artifacts/MechaModelEnemy.png");
        }
    }
}
