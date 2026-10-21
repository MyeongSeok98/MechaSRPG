#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class SceneIntegrationSetup : IPrebuildSetup, IPostBuildCleanup
    {
        public const string Folder = "Assets/_Game/Tests/GeneratedEnemyAIIntegration";
        public const string ScenePath = Folder + "/BattleFixture.unity";
        public const string SquadScenePath = Folder + "/GeneratedSquad.unity";
        private const string Marker = Folder + "/.enemy-ai-tests-owned";
        private const string Owner = "MechaXCOM enemy-ai integration tests";
        private const string SessionOwnerKey = "MechaXCOM.EnemyAIIntegrationOwner";
        public void Setup()
        {
            SessionState.EraseString(SessionOwnerKey);
            if (Directory.Exists(Folder)) throw new InvalidOperationException("Reserved integration folder already exists; refusing to overwrite it: " + Folder);
            AssetDatabase.CreateFolder("Assets/_Game/Tests", "GeneratedEnemyAIIntegration");
            var token = Guid.NewGuid().ToString("N");
            File.WriteAllText(Marker, Owner + " " + token);
            SessionState.SetString(SessionOwnerKey, token);
            var previous = SceneManager.GetActiveScene();
            var context = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(context);
            try
            {
                if (!EditorSceneManager.SaveScene(context, Folder + "/Context.unity")) throw new InvalidOperationException("Cannot save test context.");
                // Keep the PlayMode assembly usable without a dependency on an Editor-only assembly.
                var builder = Type.GetType("MechaXCOM.Editor.BattleFixtureSceneBuilder, MechaXCOM.Editor", true);
                if (!(bool)builder.GetMethod("BuildSceneAtPath").Invoke(null, new object[] { ScenePath, Folder }))
                    throw new InvalidOperationException("Cannot build integration scene.");
                CreateGeneratedSquadScene();
            }
            catch
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(context, true);
                Cleanup();
                throw;
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (context.IsValid() && context.isLoaded) EditorSceneManager.CloseScene(context, true);
            }
        }
        private static void CreateGeneratedSquadScene()
        {
            var rifle = ScriptableObject.CreateInstance<WeaponData>();
            JsonUtility.FromJsonOverwrite("{\"displayName\":\"Integration Rifle\",\"attackType\":0,\"minDamage\":5,\"maxDamage\":5,\"baseHitChance\":80,\"range\":5,\"apCost\":3,\"gripRequirement\":0,\"twoHandHitMultiplier\":1}", rifle);
            AssetDatabase.CreateAsset(rifle, Folder + "/IntegrationRifle.asset");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                // Preserve the original saved-scene regression fixture's ranged-only configuration.
                foreach (var unit in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleUnit>()))
                {
                    var stats = unit.GetComponent<UnitCombat>();
                    var loadout = stats.gameObject.EnsureTestLoadout();
                    loadout.Configure(null, GripMode.OneHand, rifle, GripMode.OneHand);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(loadout);
                }
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Cannot save integration weapon configuration.");
                var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleSceneController>()).Single();
                var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Prefabs/BattleUnit.prefab"), scene);
                var enemy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Prefabs/DummyEnemy.prefab"), scene);
                player.GetComponent<BattleUnit>().Configure(new Vector2Int(1, 0), 5);
                enemy.GetComponent<BattleUnit>().Configure(new Vector2Int(8, 0), 5);
                enemy.GetComponent<BattleUnit>().ConfigureTeam(UnitTeam.Enemy);
                foreach (var unitObject in new[] { player, enemy })
                {
                    var stats = unitObject.GetComponent<UnitCombat>();
                    var loadout = stats.gameObject.EnsureTestLoadout();
                    loadout.Configure(null, GripMode.OneHand, rifle, GripMode.OneHand);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(loadout);
                }
                var settings = new SerializedObject(controller);
                var units = settings.FindProperty("units"); units.arraySize = 4;
                units.GetArrayElementAtIndex(2).objectReferenceValue = player.GetComponent<BattleUnit>();
                units.GetArrayElementAtIndex(3).objectReferenceValue = enemy.GetComponent<BattleUnit>();
                settings.FindProperty("enemyAI").objectReferenceValue = null;
                settings.ApplyModifiedPropertiesWithoutUndo();
                UnityEngine.Object.DestroyImmediate(controller.GetComponent<EnemyAIController>());
                if (!EditorSceneManager.SaveScene(scene, SquadScenePath, true)) throw new InvalidOperationException("Cannot save test squad scene.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        public void Cleanup()
        {
            var resolved = Path.GetFullPath(Folder);
            var testsRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "_Game/Tests")) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(testsRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cleanup path is outside the test directory.");
            var token = SessionState.GetString(SessionOwnerKey, "");
            if (!string.IsNullOrEmpty(token) && File.Exists(Marker) && File.ReadAllText(Marker) == Owner + " " + token)
                AssetDatabase.DeleteAsset(Folder);
            SessionState.EraseString(SessionOwnerKey);
        }
    }
}
#endif
