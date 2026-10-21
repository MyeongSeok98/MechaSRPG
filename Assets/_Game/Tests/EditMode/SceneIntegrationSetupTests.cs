using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class SceneIntegrationSetupTests
    {
        private const string Folder = "Assets/_Game/Tests/GeneratedEnemyAIIntegration";
        private bool owned;
        [Test]
        public void RefusedExistingFolderSurvivesPostBuildCleanup()
        {
            Assert.That(Directory.Exists(Folder), Is.False, "Do not overwrite an existing folder during this safety test.");
            AssetDatabase.CreateFolder("Assets/_Game/Tests", "GeneratedEnemyAIIntegration"); owned = true;
            File.WriteAllText(Folder + "/.enemy-ai-tests-owned", "MechaXCOM enemy-ai integration tests");
            File.WriteAllText(Folder + "/Authored.txt", "keep this earlier run's data");
            var type = Type.GetType("MechaXCOM.Tests.SceneIntegrationSetup, MechaXCOM.PlayModeTests", true);
            var setup = Activator.CreateInstance(type);
            var error = Assert.Throws<TargetInvocationException>(() => type.GetMethod("Setup").Invoke(setup, null));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
            type.GetMethod("Cleanup").Invoke(setup, null);
            Assert.That(File.Exists(Folder + "/Authored.txt"), Is.True, "A refused folder must not be deleted by cleanup.");
        }
        [TearDown]
        public void CleanupTestOwnedFolder()
        {
            var testsRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "_Game/Tests")) + Path.DirectorySeparatorChar;
            Assert.That(Path.GetFullPath(Folder).StartsWith(testsRoot, StringComparison.OrdinalIgnoreCase), Is.True);
            if (owned && AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
        }
    }
}
