using System;
using System.Linq;
using MechaXCOM.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MechaXCOM.Tests
{
    public class SceneBuilderTests
    {
        private string output;
        private string scenePath;
        private Scene opened;

        [SetUp]
        public void SetUp()
        {
            output = "Assets/_Game/Tests/GeneratedSceneTest_" + Guid.NewGuid().ToString("N");
            scenePath = output + "/Scenes/Test.unity";
            AssetDatabase.CreateFolder("Assets/_Game/Tests", System.IO.Path.GetFileName(output));
            var context = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(context, output + "/Context.unity");
        }
        [TearDown]
        public void TearDown()
        {
            if (opened.IsValid() && opened.isLoaded) EditorSceneManager.CloseScene(opened, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (AssetDatabase.IsValidFolder(output)) AssetDatabase.DeleteAsset(output);
        }
        [Test]
        public void DefaultSceneHasExpectedObjects()
        {
            Assert.That(BattleFixtureSceneBuilder.BuildSceneAtPath(scenePath, output), Is.True);
            opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            var roots = opened.GetRootGameObjects();
            var grid = roots.SelectMany(r => r.GetComponentsInChildren<GridManager>()).Single();
            var units = roots.SelectMany(r => r.GetComponentsInChildren<BattleUnit>()).ToArray();
            var covers = roots.SelectMany(r => r.GetComponentsInChildren<CoverObject>()).ToArray();
            Assert.That(grid.Width, Is.EqualTo(10));
            Assert.That(grid.Height, Is.EqualTo(10));
            Assert.That(grid.CellSize, Is.EqualTo(2));
            Assert.That(units.Length, Is.EqualTo(2));
            Assert.That(units.Single(u => u.Team == UnitTeam.Player).StartGridPosition, Is.EqualTo(new Vector2Int(1, 1)));
            var enemy = units.Single(u => u.Team == UnitTeam.Enemy);
            Assert.That(enemy.StartGridPosition, Is.EqualTo(new Vector2Int(3, 3)));
            Assert.That(enemy.GetComponent<UnitHealth>().GetMaxHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(enemy.GetComponent<UnitHealth>().GetMaxHP(TargetBodyPart.Cockpit), Is.EqualTo(6));
            Assert.That(covers.Count(c => c.Type == CoverType.Half), Is.EqualTo(2));
            Assert.That(covers.Count(c => c.Type == CoverType.Full), Is.EqualTo(2));
            Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<GridObstacle>()).Count(), Is.EqualTo(3));
            var controller = roots.SelectMany(r => r.GetComponentsInChildren<BattleSceneController>()).Single();
            var serialized = new SerializedObject(controller);
            Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<EnemyAIController>()).Count(), Is.EqualTo(1));
            Assert.That(serialized.FindProperty("enemyAI"), Is.Not.Null);
            Assert.That(serialized.FindProperty("enemyAI").objectReferenceValue, Is.Not.Null);
            var battleCamera = (Camera)serialized.FindProperty("battleCamera").objectReferenceValue;
            Assert.That(battleCamera.GetComponent<BattleCameraOrbit>(), Is.Not.Null);
            foreach (var name in new[] { "grid", "coverSystem", "battleCamera", "inputController", "visualizer", "debugUI", "combatSystem", "turnManager" })
                Assert.That(serialized.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
        }
        [Test]
        public void UpgradeIsIdempotent()
        {
            Assert.That(BattleFixtureSceneBuilder.BuildSceneAtPath(scenePath, output), Is.True);
            var guid = AssetDatabase.AssetPathToGUID(scenePath);
            Assert.That(BattleFixtureSceneBuilder.UpgradeSceneAtPath(scenePath, output), Is.True);
            Assert.That(BattleFixtureSceneBuilder.UpgradeSceneAtPath(scenePath, output), Is.True);
            Assert.That(AssetDatabase.AssetPathToGUID(scenePath), Is.EqualTo(guid));
            opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            var roots = opened.GetRootGameObjects();
            Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<BattleUnit>()).Count(u => u.Team == UnitTeam.Enemy), Is.EqualTo(1));
            Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<CombatSystem>()).Count(), Is.EqualTo(1));
        }
        [Test]
        public void UpgradePreservesAuthoredReferencesAndInactiveUnits()
        {
            Assert.That(BattleFixtureSceneBuilder.BuildSceneAtPath(scenePath, output), Is.True);
            opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(opened);
            var roots = opened.GetRootGameObjects();
            var controller = roots.SelectMany(r => r.GetComponentsInChildren<BattleSceneController>()).Single();
            var enemy = roots.SelectMany(r => r.GetComponentsInChildren<BattleUnit>()).Single(u => u.Team == UnitTeam.Enemy);
            enemy.gameObject.SetActive(false);
            var authored = new GameObject("Authored Presentation");
            var camera = authored.AddComponent<Camera>(); camera.orthographicSize = 9;
            var input = authored.AddComponent<PlayerInputController>();
            var visualizer = authored.AddComponent<GridVisualizer>(); var ui = authored.AddComponent<BattleDebugUI>();
            controller.ConfigurePresentation(camera, input, visualizer, ui);
            var inactiveCover = roots.SelectMany(r => r.GetComponentsInChildren<CoverObject>()).First();
            inactiveCover.gameObject.SetActive(false);
            EditorSceneManager.SaveScene(opened); EditorSceneManager.CloseScene(opened, true);
            Assert.That(BattleFixtureSceneBuilder.UpgradeSceneAtPath(scenePath, output), Is.True);
            opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            roots = opened.GetRootGameObjects();
            var units = roots.SelectMany(r => r.GetComponentsInChildren<BattleUnit>(true)).ToArray();
            Assert.That(units.Count(u => u.Team == UnitTeam.Enemy), Is.EqualTo(1));
            Assert.That(units.Single(u => u.Team == UnitTeam.Enemy).gameObject.activeSelf, Is.False);
            controller = roots.SelectMany(r => r.GetComponentsInChildren<BattleSceneController>()).Single();
            var serialized = new SerializedObject(controller);
            foreach (var property in new[] { "battleCamera", "inputController", "visualizer", "debugUI" })
                Assert.That(serialized.FindProperty(property).objectReferenceValue.name, Is.EqualTo("Authored Presentation"), property);
            Assert.That(serialized.FindProperty("units").arraySize, Is.EqualTo(2));
            Assert.That(serialized.FindProperty("covers").arraySize, Is.EqualTo(4));
        }
        [Test]
        public void UpgradePreservesExplicitEmptyObstacleLists()
        {
            Assert.That(BattleFixtureSceneBuilder.BuildSceneAtPath(scenePath, output), Is.True);
            opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            var controller = opened.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleSceneController>()).Single();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("covers").ClearArray(); serialized.FindProperty("obstacles").ClearArray();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(opened); EditorSceneManager.CloseScene(opened, true);
            Assert.That(BattleFixtureSceneBuilder.UpgradeSceneAtPath(scenePath, output), Is.True);
            opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            controller = opened.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleSceneController>()).Single();
            serialized = new SerializedObject(controller);
            Assert.That(serialized.FindProperty("covers").arraySize, Is.Zero);
            Assert.That(serialized.FindProperty("obstacles").arraySize, Is.Zero);
        }
        [Test]
        public void ExistingPrefabGetsCombatComponents()
        {
            AssetDatabase.CreateFolder(output, "Prefabs");
            var old = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            old.AddComponent<UnitMovement>(); old.AddComponent<UnitMobility>();
            var oldPrefab = output + "/Prefabs/BattleUnit.prefab";
            PrefabUtility.SaveAsPrefabAsset(old, oldPrefab); UnityEngine.Object.DestroyImmediate(old);
            var guid = AssetDatabase.AssetPathToGUID(oldPrefab);
            Assert.That(BattleFixtureSceneBuilder.BuildSceneAtPath(scenePath, output), Is.True);
            var upgraded = AssetDatabase.LoadAssetAtPath<GameObject>(oldPrefab);
            Assert.That(upgraded.GetComponent<UnitHealth>(), Is.Not.Null);
            Assert.That(upgraded.GetComponent<UnitActionPoints>(), Is.Not.Null);
            Assert.That(upgraded.GetComponent<UnitWeaponLoadout>().RightWeapon.APCost, Is.EqualTo(3));
            var instance = UnityEngine.Object.Instantiate(upgraded);
            try { Assert.That(instance.GetComponent<UnitCombat>().GetAttack(AttackType.Ranged).APCost, Is.EqualTo(3)); }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
            Assert.That(AssetDatabase.AssetPathToGUID(oldPrefab), Is.EqualTo(guid));
        }
        [Test]
        public void ExistingOutputNotOverwritten()
        {
            Assert.That(BattleFixtureSceneBuilder.BuildSceneAtPath(scenePath, output), Is.True);
            var guid = AssetDatabase.AssetPathToGUID(scenePath);
            Assert.That(BattleFixtureSceneBuilder.BuildSceneAtPath(scenePath, output), Is.False);
            Assert.That(AssetDatabase.AssetPathToGUID(scenePath), Is.EqualTo(guid));
        }
        [Test]
        public void UnsavedScenePreserved()
        {
            var unsaved = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var userObject = new GameObject("Unsaved User Object");
            Assert.That(BattleFixtureSceneBuilder.BuildSceneAtPath(scenePath, output), Is.False);
            Assert.That(userObject, Is.Not.Null);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(unsaved));
            Assert.That(System.IO.File.Exists(scenePath), Is.False);
        }
        [Test]
        public void GeneratedPrefabsHaveComponents()
        {
            BattleFixtureSceneBuilder.BuildSceneAtPath(scenePath, output);
            var unit = AssetDatabase.LoadAssetAtPath<GameObject>(output + "/Prefabs/BattleUnit.prefab");
            Assert.That(unit.GetComponent<BattleUnit>(), Is.Not.Null);
            Assert.That(unit.GetComponent<UnitMovement>(), Is.Not.Null);
            Assert.That(unit.GetComponent<UnitMobility>(), Is.Not.Null);
            Assert.That(unit.GetComponent<CapsuleCollider>(), Is.Not.Null);
            var half = AssetDatabase.LoadAssetAtPath<GameObject>(output + "/Prefabs/HalfCover.prefab");
            var full = AssetDatabase.LoadAssetAtPath<GameObject>(output + "/Prefabs/FullCover.prefab");
            Assert.That(half.GetComponent<CoverObject>().Type, Is.EqualTo(CoverType.Half));
            Assert.That(full.GetComponent<CoverObject>().Type, Is.EqualTo(CoverType.Full));
            Assert.That(half.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(output + "/Prefabs/GridObstacle.prefab")
                .GetComponent<GridObstacle>(), Is.Not.Null);
        }
    }
}
