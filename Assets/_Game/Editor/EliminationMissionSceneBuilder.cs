using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MechaXCOM.EditorTools
{
    public static class EliminationMissionSceneBuilder
    {
        public const string SourcePath = "Assets/_Game/Scenes/SqurdWeaponTest.unity";
        public const string ScenePath = "Assets/_Game/Scenes/EliminationMissionTest.unity";
        private const string DataPath = "Assets/_Game/Data/Missions/SampleEliminationMission.asset";
        [MenuItem("MechaXCOM/Create Elimination Mission Test Scene")]
        public static void Build()
        {
            var loadedSource = SceneManager.GetSceneByPath(SourcePath);
            if (loadedSource.IsValid() && loadedSource.isLoaded)
                throw new InvalidOperationException("The source scene is already loaded. Save it and open a different scene before generating a copy.");
            if (File.Exists(ScenePath) || File.Exists(ScenePath + ".meta"))
                throw new InvalidOperationException("Test scene already exists; refusing to overwrite: " + ScenePath);
            var data = AssetDatabase.LoadAssetAtPath<MissionData>(DataPath);
            if (!data || !data.IsValid(out _)) throw new InvalidOperationException("A valid sample MissionData is required.");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(SourcePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var controller = roots.SelectMany(r => r.GetComponentsInChildren<BattleSceneController>(true)).Single();
                var turns = roots.SelectMany(r => r.GetComponentsInChildren<TurnManager>(true)).Single();
                var settings = new SerializedObject(controller);
                var rosterProperty = settings.FindProperty("units");
                var roster = Enumerable.Range(0, rosterProperty.arraySize)
                    .Select(i => rosterProperty.GetArrayElementAtIndex(i).objectReferenceValue as BattleUnit).ToArray();
                var groupObject = new GameObject("Mission Targets - " + data.TargetEnemyGroupId);
                SceneManager.MoveGameObjectToScene(groupObject, scene);
                groupObject.transform.SetParent(turns.transform, false);
                var group = groupObject.AddComponent<EnemyUnitGroup>();
                group.Configure(data.TargetEnemyGroupId, roster.Where(u => u && u.Team == UnitTeam.Enemy).ToArray());
                var mission = turns.GetComponent<MissionController>() ?? turns.gameObject.AddComponent<MissionController>();
                if (!mission.Configure(data, new[] { group })) throw new InvalidOperationException("Cannot configure an initialized mission.");
                if (!mission.ValidateSetup(roster, out var reason))
                    throw new InvalidOperationException("Invalid sample mission configuration: " + reason);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Cannot save test scene.");
                Debug.Log("Created " + ScenePath + " with " + group.GetMembers().Length + " designated targets.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
