using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MechaXCOM.Editor
{
    public static class BattleFixtureSceneBuilder
    {
        public static bool BuildSceneAtPath(string scenePath, string assetFolder)
        {
            if (!ValidAssetPath(scenePath) || !ValidAssetPath(assetFolder) ||
                !scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || File.Exists(scenePath) ||
                string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) return false;
            EnsureFolder(Path.GetDirectoryName(scenePath).Replace('\\', '/'));
            EnsureFolder(assetFolder + "/Materials");
            EnsureFolder(assetFolder + "/Prefabs");
            var previousScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var groundMaterial = MaterialAsset(assetFolder, "Ground", new Color(0.38f, 0.34f, 0.25f), false);
                var unitMaterial = MaterialAsset(assetFolder, "Unit", new Color(0.75f, 0.83f, 0.78f), false);
                var enemyMaterial = MaterialAsset(assetFolder, "DummyEnemy", new Color(0.75f, 0.18f, 0.12f), false);
                var halfMaterial = MaterialAsset(assetFolder, "HalfCover", new Color(0.68f, 0.47f, 0.22f), false);
                var fullMaterial = MaterialAsset(assetFolder, "FullCover", new Color(0.32f, 0.4f, 0.29f), false);
                var obstacleMaterial = MaterialAsset(assetFolder, "Obstacle", new Color(0.27f, 0.25f, 0.23f), false);
                var lineMaterial = MaterialAsset(assetFolder, "GridLines", new Color(0.16f, 0.19f, 0.15f), true);
                var rangeMaterial = MaterialAsset(assetFolder, "MoveRange", new Color(0.24f, 0.52f, 0.48f), true);
                var selectionMaterial = MaterialAsset(assetFolder, "Selection", new Color(1f, 0.77f, 0.22f), true);

                var systems = SceneObject("Battle Systems", scene);
                var grid = systems.AddComponent<GridManager>();
                grid.Configure(10, 10, 2);
                var coverSystem = systems.AddComponent<CoverSystem>();
                var visualizer = systems.AddComponent<GridVisualizer>();
                visualizer.ConfigureMaterials(lineMaterial, rangeMaterial, selectionMaterial);
                var debugUI = systems.AddComponent<BattleDebugUI>();
                var input = systems.AddComponent<PlayerInputController>();
                var controller = systems.AddComponent<BattleSceneController>();
                var combat = systems.AddComponent<CombatSystem>();
                var turns = systems.AddComponent<TurnManager>();
                var ai = systems.AddComponent<EnemyAIController>();
                var ground = Primitive("Ground", PrimitiveType.Cube, groundMaterial, scene);
                ground.transform.position = new Vector3(10, -0.25f, 10);
                ground.transform.localScale = new Vector3(20, 0.5f, 20);

                var unitPrefab = PrefabAsset(assetFolder, "BattleUnit", PrimitiveType.Capsule, unitMaterial, Vector3.one, 1, scene);
                var enemyPrefab = PrefabAsset(assetFolder, "DummyEnemy", PrimitiveType.Capsule, enemyMaterial, Vector3.one, 1, scene);
                var halfPrefab = PrefabAsset(assetFolder, "HalfCover", PrimitiveType.Cube, halfMaterial, new Vector3(2, 1, 2), 0.5f, scene);
                var fullPrefab = PrefabAsset(assetFolder, "FullCover", PrimitiveType.Cube, fullMaterial, new Vector3(2, 2, 2), 1, scene);
                var obstaclePrefab = PrefabAsset(assetFolder, "GridObstacle", PrimitiveType.Cube, obstacleMaterial, new Vector3(2, 1.5f, 2), 0.75f, scene);
                var unitsRoot = SceneObject("Units", scene).transform;
                var coverRoot = SceneObject("Cover", scene).transform;
                var obstacleRoot = SceneObject("Obstacles", scene).transform;
                var unit = Place(unitPrefab, unitsRoot, new Vector2Int(1, 1), 1).GetComponent<BattleUnit>();
                unit.Configure(new Vector2Int(1, 1), 5);
                var enemy = Place(enemyPrefab, unitsRoot, new Vector2Int(3, 3), 1).GetComponent<BattleUnit>();
                enemy.Configure(new Vector2Int(3, 3), 5);
                enemy.ConfigureTeam(UnitTeam.Enemy);
                var covers = new[]
                {
                    Place(halfPrefab, coverRoot, new Vector2Int(3, 2), 0.5f).GetComponent<CoverObject>(),
                    Place(halfPrefab, coverRoot, new Vector2Int(6, 5), 0.5f).GetComponent<CoverObject>(),
                    Place(fullPrefab, coverRoot, new Vector2Int(3, 4), 1).GetComponent<CoverObject>(),
                    Place(fullPrefab, coverRoot, new Vector2Int(7, 7), 1).GetComponent<CoverObject>()
                };
                var obstacles = new[]
                {
                    Place(obstaclePrefab, obstacleRoot, new Vector2Int(4, 2), 0.75f).GetComponent<GridObstacle>(),
                    Place(obstaclePrefab, obstacleRoot, new Vector2Int(4, 3), 0.75f).GetComponent<GridObstacle>(),
                    Place(obstaclePrefab, obstacleRoot, new Vector2Int(4, 4), 0.75f).GetComponent<GridObstacle>()
                };
                var camera = SceneObject("Battle Camera", scene).AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true;
                camera.orthographicSize = 15;
                camera.rect = new Rect(0.23f, 0, 0.77f, 1);
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 150;
                camera.backgroundColor = new Color(0.11f, 0.14f, 0.16f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.transform.position = new Vector3(34, 28, -22);
                camera.transform.LookAt(new Vector3(10, 0, 10));
                camera.gameObject.AddComponent<AudioListener>();
                camera.gameObject.AddComponent<BattleCameraOrbit>();
                var light = SceneObject("Sun", scene).AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.4f;
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(50, -35, 0);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.61f);
                controller.Configure(grid, coverSystem, new[] { unit, enemy }, obstacles, covers);
                controller.ConfigurePresentation(camera, input, visualizer, debugUI);
                controller.ConfigureCombat(combat, turns);
                controller.ConfigureEnemyAI(ai);
                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                return EditorSceneManager.SaveScene(scene, scenePath);
            }
            finally
            {
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
        public static bool UpgradeSceneAtPath(string scenePath, string assetFolder)
        {
            if (!ValidAssetPath(scenePath) || !ValidAssetPath(assetFolder) || !File.Exists(scenePath) ||
                !scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) return false;
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                EnsureFolder(assetFolder + "/Materials"); EnsureFolder(assetFolder + "/Prefabs");
                var playerMaterial = MaterialAsset(assetFolder, "Unit", new Color(.75f, .83f, .78f), false);
                var enemyMaterial = MaterialAsset(assetFolder, "DummyEnemy", new Color(.75f, .18f, .12f), false);
                PrefabAsset(assetFolder, "BattleUnit", PrimitiveType.Capsule, playerMaterial, Vector3.one, 1, scene);
                var enemyPrefab = PrefabAsset(assetFolder, "DummyEnemy", PrimitiveType.Capsule, enemyMaterial, Vector3.one, 1, scene);
                var roots = scene.GetRootGameObjects();
                var controller = roots.SelectMany(r => r.GetComponentsInChildren<BattleSceneController>(true)).SingleOrDefault();
                var settings = controller ? new SerializedObject(controller) : null;
                var grid = ReferenceOr(settings, "grid", () => roots.SelectMany(r => r.GetComponentsInChildren<GridManager>(true)).Single());
                if (!controller) controller = Ensure<BattleSceneController>(grid.gameObject);
                var sceneUnits = roots.SelectMany(r => r.GetComponentsInChildren<BattleUnit>(true)).ToList();
                var units = ListOr(settings, "units", sceneUnits.ToArray()).ToList();
                foreach (var unit in sceneUnits) EnsureUnitComponents(unit.gameObject, unit.Team, assetFolder);
                foreach (var enemy in sceneUnits.Where(u => u.Team == UnitTeam.Enemy))
                    if (!units.Contains(enemy)) units.Add(enemy);
                if (!sceneUnits.Any(u => u.Team == UnitTeam.Enemy))
                {
                    var parent = roots.FirstOrDefault(r => r.name == "Units");
                    if (!parent) parent = SceneObject("Units", scene);
                    var enemy = Place(enemyPrefab, parent.transform, new Vector2Int(3, 3), 1).GetComponent<BattleUnit>();
                    enemy.Configure(new Vector2Int(3, 3), 5); enemy.ConfigureTeam(UnitTeam.Enemy); units.Add(enemy);
                }
                var systems = grid.gameObject;
                var combat = ReferenceOr(settings, "combatSystem", () => Ensure<CombatSystem>(systems));
                var turns = ReferenceOr(settings, "turnManager", () => Ensure<TurnManager>(systems));
                var covers = ListOr(settings, "covers", roots.SelectMany(r => r.GetComponentsInChildren<CoverObject>(true)).ToArray());
                var obstacles = ListOr(settings, "obstacles", roots.SelectMany(r => r.GetComponentsInChildren<GridObstacle>(true)).ToArray());
                controller.Configure(grid, ReferenceOr(settings, "coverSystem", () => Ensure<CoverSystem>(systems)), units.ToArray(), obstacles, covers);
                controller.ConfigurePresentation(
                    ReferenceOr(settings, "battleCamera", () => roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First()),
                    ReferenceOr(settings, "inputController", () => Ensure<PlayerInputController>(systems)),
                    ReferenceOr(settings, "visualizer", () => Ensure<GridVisualizer>(systems)),
                    ReferenceOr(settings, "debugUI", () => Ensure<BattleDebugUI>(systems)));
                controller.ConfigureCombat(combat, turns);
                EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
                return EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
        private static T ReferenceOr<T>(SerializedObject settings, string property, Func<T> fallback) where T : UnityEngine.Object
        {
            var assigned = settings?.FindProperty(property)?.objectReferenceValue as T;
            return assigned ? assigned : fallback();
        }
        private static T[] ListOr<T>(SerializedObject settings, string property, T[] fallback) where T : UnityEngine.Object
        {
            var list = settings?.FindProperty(property);
            if (list == null) return fallback;
            var result = new T[list.arraySize];
            for (int i = 0; i < result.Length; i++) result[i] = list.GetArrayElementAtIndex(i).objectReferenceValue as T;
            return result;
        }
        private static T Ensure<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? go.AddComponent<T>();
        private static void EnsureUnitComponents(GameObject go, UnitTeam team, string root)
        {
            Ensure<UnitHealth>(go); Ensure<UnitActionPoints>(go); Ensure<UnitCombat>(go);
            Ensure<BattleUnit>(go).ConfigureTeam(team);
            if (!go.GetComponent<UnitWeaponLoadout>())
                go.AddComponent<UnitWeaponLoadout>().Configure(WeaponAsset(root, true), GripMode.OneHand,
                    WeaponAsset(root, false), GripMode.OneHand);
        }
        private static WeaponData WeaponAsset(string root, bool melee)
        {
            EnsureFolder(root + "/Data");
            string path = root + "/Data/" + (melee ? "FixtureMelee" : "FixtureRanged") + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (existing) return existing;
            var weapon = ScriptableObject.CreateInstance<WeaponData>();
            string json = melee
                ? "{\"displayName\":\"Fixture Melee\",\"attackType\":1,\"minDamage\":8,\"maxDamage\":8,\"baseHitChance\":95,\"range\":2,\"apCost\":3,\"gripRequirement\":0,\"twoHandHitMultiplier\":1}"
                : "{\"displayName\":\"Fixture Ranged\",\"attackType\":0,\"minDamage\":5,\"maxDamage\":5,\"baseHitChance\":80,\"range\":7,\"apCost\":3,\"gripRequirement\":0,\"twoHandHitMultiplier\":1}";
            JsonUtility.FromJsonOverwrite(json, weapon);
            AssetDatabase.CreateAsset(weapon, path);
            return weapon;
        }
        private static bool ValidAssetPath(string path) => !string.IsNullOrEmpty(path) &&
            path.StartsWith("Assets/", StringComparison.Ordinal) && !path.Contains("..") && !path.Contains('\\');

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        private static Material MaterialAsset(string root, string name, Color color, bool unlit)
        {
            string path = root + "/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("Existing URP shader is missing.");
            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
        private static GameObject SceneObject(string name, Scene scene)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }
        private static GameObject Primitive(string name, PrimitiveType type, Material material, Scene scene)
        {
            var go = GameObject.CreatePrimitive(type);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.name = name;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
        private static GameObject PrefabAsset(string root, string name, PrimitiveType type,
            Material material, Vector3 scale, float height, Scene scene)
        {
            string path = root + "/Prefabs/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing)
            {
                if (name == "BattleUnit" || name == "DummyEnemy")
                {
                    var contents = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        EnsureUnitComponents(contents, name == "DummyEnemy" ? UnitTeam.Enemy : UnitTeam.Player, root);
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(contents); }
                    existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
                return existing;
            }
            var go = Primitive(name, type, material, scene);
            go.transform.localScale = scale;
            go.transform.position = Vector3.up * height;
            if (name == "BattleUnit" || name == "DummyEnemy")
                EnsureUnitComponents(go, name == "DummyEnemy" ? UnitTeam.Enemy : UnitTeam.Player, root);
            else if (name == "GridObstacle") go.AddComponent<GridObstacle>();
            else go.AddComponent<CoverObject>().Configure(name == "FullCover" ? CoverType.Full : CoverType.Half);
            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return saved;
        }
        private static GameObject Place(GameObject prefab, Transform parent, Vector2Int position, float height)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = prefab.name + $" ({position.x},{position.y})";
            go.transform.position = new Vector3((position.x + 0.5f) * 2, height, (position.y + 0.5f) * 2);
            return go;
        }
    }
}
