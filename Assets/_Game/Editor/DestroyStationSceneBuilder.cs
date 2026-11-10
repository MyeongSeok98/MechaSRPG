using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MechaXCOM.EditorTools
{
    public static class DestroyStationSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/DestroyCommunicationStationTest.unity";
        public const string MissionPath = "Assets/_Game/Data/Missions/SampleDestroyStationMission.asset";
        public const string WeaponPath = "Assets/_Game/Data/Weapons/Ranged/MissionDemolitionLauncher.asset";
        private const string SourcePath = "Assets/_Game/Scenes/SqurdWeaponTest.unity";
        [MenuItem("MechaXCOM/Create Destroy Station Test Scene")]
        public static void Build()
        {
            var loaded = SceneManager.GetSceneByPath(SourcePath);
            if (loaded.IsValid() && loaded.isLoaded) throw new InvalidOperationException("The source scene is already loaded. Save it and open another scene first.");
            if (File.Exists(ScenePath) || File.Exists(ScenePath + ".meta")) throw new InvalidOperationException("Refusing to overwrite " + ScenePath);
            var data = AssetDatabase.LoadAssetAtPath<MissionData>(MissionPath);
            var stationData = AssetDatabase.LoadAssetAtPath<DestructibleObjectData>("Assets/_Game/Data/Objects/SampleCommunicationStation.asset");
            var launcher = AssetDatabase.LoadAssetAtPath<WeaponData>(WeaponPath);
            var rifle = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Game/Data/Weapons/Ranged/StandardRifle.asset");
            if (!data || !data.IsValid(out _) || data.ObjectiveType != MissionObjectiveType.DestroyTarget ||
                !stationData || !stationData.IsValid(out _) || !launcher || !launcher.IsValid(out _))
                throw new InvalidOperationException("Valid station mission, object and launcher assets are required.");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(SourcePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var controller = roots.SelectMany(r => r.GetComponentsInChildren<BattleSceneController>(true)).Single();
                var turns = roots.SelectMany(r => r.GetComponentsInChildren<TurnManager>(true)).Single();
                var settings = new SerializedObject(controller);
                var rosterProperty = settings.FindProperty("units");
                var original = Enumerable.Range(0, rosterProperty.arraySize).Select(i => rosterProperty.GetArrayElementAtIndex(i).objectReferenceValue as BattleUnit).ToArray();
                var allies = original.Where(u => u && u.Team == UnitTeam.Player).Take(2).ToArray();
                var enemies = original.Where(u => u && u.Team == UnitTeam.Enemy).Take(2).ToArray();
                if (allies.Length != 2 || enemies.Length != 2) throw new InvalidOperationException("Source must contain at least two units per team.");
                var roster = allies.Concat(enemies).ToArray();
                foreach (var unused in original.Where(u => u && !roster.Contains(u))) UnityEngine.Object.DestroyImmediate(unused.gameObject);
                // Clear only this new scene's old cover/obstacles to make the demolition lane obvious.
                foreach (var old in roots.SelectMany(r => r.GetComponentsInChildren<CoverObject>(true)).Select(c => c.gameObject)
                    .Concat(roots.SelectMany(r => r.GetComponentsInChildren<GridObstacle>(true)).Select(o => o.gameObject)).Distinct().ToArray())
                    if (old) UnityEngine.Object.DestroyImmediate(old);
                var grid = settings.FindProperty("grid").objectReferenceValue as GridManager;
                var positions = new[] { new Vector2Int(2, 1), new Vector2Int(2, 3), new Vector2Int(10, 1), new Vector2Int(10, 5) };
                for (int i = 0; i < roster.Length; i++)
                {
                    roster[i].Configure(positions[i], roster[i].MovementRange);
                    roster[i].transform.position = grid.GetWorldPosition(positions[i]) + Vector3.up;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(roster[i]);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(roster[i].transform);
                }
                foreach (var ally in allies)
                {
                    var loadout = ally.GetComponent<UnitWeaponLoadout>();
                    if (!loadout.TryConfigureBackpack(null) || !loadout.TryConfigure(rifle, GripMode.OneHand, launcher, GripMode.OneHand, 0, 1))
                        throw new InvalidOperationException("Cannot equip test launcher: " + ally.name);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(loadout);
                }
                var station = new GameObject("Communication Station [TARGET]"); SceneManager.MoveGameObjectToScene(station, scene);
                station.transform.position = grid.GetWorldPosition(new Vector2Int(6, 2));
                var cover = station.AddComponent<CoverObject>(); cover.Configure(CoverType.Full);
                cover.Collider.center = new Vector3(0, 1.5f, 0); cover.Collider.size = new Vector3(1.6f, 3, 1.6f);
                var health = station.AddComponent<CoverDurability>();
                if (!health.ConfigureData(stationData)) throw new InvalidOperationException("Cannot configure station HP.");
                var bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/FullCover.mat");
                var signalMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Unit.mat");
                Visual(station.transform, "Station building", PrimitiveType.Cube, new Vector3(0, 1.5f, 0), new Vector3(1.6f, 3, 1.6f), bodyMaterial);
                Visual(station.transform, "Antenna", PrimitiveType.Cylinder, new Vector3(0, 3.8f, 0), new Vector3(.12f, .8f, .12f), bodyMaterial);
                Visual(station.transform, "Signal head", PrimitiveType.Sphere, new Vector3(0, 4.5f, 0), new Vector3(.7f, .4f, .7f), signalMaterial);
                var target = station.AddComponent<MissionTarget>(); target.ConfigureObject(data.TargetObjectId, health);
                var mission = turns.GetComponent<MissionController>() ?? turns.gameObject.AddComponent<MissionController>();
                if (!mission.ConfigureDestruction(data, new[] { target }) || !mission.ValidateSetup(roster, out _))
                    throw new InvalidOperationException("Invalid destruction mission setup.");
                settings.Update();
                var list = settings.FindProperty("units"); list.arraySize = roster.Length;
                for(int i=0;i<roster.Length;i++) list.GetArrayElementAtIndex(i).objectReferenceValue=roster[i];
                settings.FindProperty("obstacles").arraySize=0;
                var covers=settings.FindProperty("covers"); covers.arraySize=1; covers.GetArrayElementAtIndex(0).objectReferenceValue=cover;
                settings.ApplyModifiedPropertiesWithoutUndo();
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Cannot save station mission scene.");
                Debug.Log("Created " + ScenePath + ": two allies, two enemies, one data-driven station.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
        private static void Visual(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var node=GameObject.CreatePrimitive(type); node.name=name; node.transform.SetParent(parent,false);
            node.transform.localPosition=position; node.transform.localScale=scale;
            UnityEngine.Object.DestroyImmediate(node.GetComponent<Collider>());
            if(material) node.GetComponent<Renderer>().sharedMaterial=material;
        }
    }
}
