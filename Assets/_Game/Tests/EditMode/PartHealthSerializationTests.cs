using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class PartHealthSerializationTests
    {
        [TestCase("Assets/_Game/Prefabs/BattleUnit.prefab")]
        [TestCase("Assets/_Game/Prefabs/DummyEnemy.prefab")]
        public void AuthoredPrefabKeepsHealthReferenceAndMigratesPartHPWithoutSaving(string path)
        {
            byte[] before = File.ReadAllBytes(path);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var health = contents.GetComponent<UnitHealth>();
                Assert.That(health, Is.Not.Null);
                Assert.That(health.GetMaxHP(TargetBodyPart.Torso), Is.EqualTo(20));
                Assert.That(health.GetMaxHP(TargetBodyPart.Cockpit), Is.EqualTo(6));
                health.Initialize();
                Assert.That(health.GetCurrentHP(TargetBodyPart.Cockpit), Is.EqualTo(6));
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
        }

        [Test]
        public void LegacyMaxHealthBecomesBodyMaximum()
        {
            string folder = "Assets/_Game/Tests/GeneratedPartHealth_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/_Game/Tests", Path.GetFileName(folder));
            string path = folder + "/Legacy.prefab";
            GameObject source = null, loaded = null;
            try
            {
                source = new GameObject("LegacyHealth");
                source.AddComponent<UnitHealth>().ConfigurePartHP(TargetBodyPart.Torso, 37);
                PrefabUtility.SaveAsPrefabAsset(source, path);
                File.WriteAllText(path, File.ReadAllText(path).Replace("bodyMaxHP: 37", "maxHealth: 37"));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                loaded = PrefabUtility.LoadPrefabContents(path);
                var health = loaded.GetComponent<UnitHealth>();
                Assert.That(health.GetMaxHP(TargetBodyPart.Torso), Is.EqualTo(37));
                Assert.That(health.GetMaxHP(TargetBodyPart.Cockpit), Is.EqualTo(6));
                health.Initialize();
                Assert.That(health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(37));
                Assert.That(health.GetCurrentHP(TargetBodyPart.Cockpit), Is.EqualTo(6));
            }
            finally
            {
                if (loaded != null) PrefabUtility.UnloadPrefabContents(loaded);
                if (source != null) UnityEngine.Object.DestroyImmediate(source);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
