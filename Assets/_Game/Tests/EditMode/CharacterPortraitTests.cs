using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class CharacterPortraitTests
    {
        private string folder;
        private CharacterData data;

        [SetUp] public void Setup()
        {
            folder = "Assets/CharacterPortraitTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            data = ScriptableObject.CreateInstance<CharacterData>();
        }
        [TearDown] public void Cleanup()
        {
            if (data && !AssetDatabase.Contains(data)) Object.DestroyImmediate(data);
            if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
        }
        private string Image(string name, string path = null)
        {
            string file = (path ?? folder) + "/" + name + ".png";
            var texture = new Texture2D(4, 5);
            File.WriteAllBytes(file, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(file); return file;
        }
        private void SetFolder(Object value)
        {
            var serialized = new SerializedObject(data);
            var field = serialized.FindProperty("portraitFolder");
            Assert.That(field, Is.Not.Null, "CharacterData must store an editor portrait folder.");
            field.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private bool Refresh()
        {
            var type = typeof(EditorTools.CharacterDataEditor).Assembly.GetType("MechaXCOM.EditorTools.CharacterPortraitLoader");
            Assert.That(type, Is.Not.Null, "Editor folder portrait loader must exist.");
            object[] args = { data, null };
            return (bool)type.GetMethod("Refresh").Invoke(null, args);
        }
        private Sprite Portrait(string expression)
        {
            var method = typeof(CharacterData).GetMethod("GetPortrait"); Assert.That(method, Is.Not.Null);
            return (Sprite)method.Invoke(data, new[] { Enum.Parse(method.GetParameters()[0].ParameterType, expression) });
        }
        [Test] public void FindsThreeNamedPNGsAndImportsSingleSprites()
        {
            string standard = Image("Default"), laugh = Image("Laugh"), damaged = Image("Damaged");
            SetFolder(AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder)); Assert.That(Refresh(), Is.True);
            foreach (var pair in new[] { ("Default", standard), ("Laugh", laugh), ("Damaged", damaged) })
            {
                Assert.That(Portrait(pair.Item1), Is.SameAs(AssetDatabase.LoadAssetAtPath<Sprite>(pair.Item2)));
                Assert.That(Portrait(pair.Item1), Is.Not.Null);
                Assert.That(((TextureImporter)AssetImporter.GetAtPath(pair.Item2)).spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            }
        }
        [Test] public void MissingOptionalExpressionsUseDefault()
        {
            Image("Default"); SetFolder(AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder)); Assert.That(Refresh(), Is.True);
            Assert.That(Portrait("Default"), Is.Not.Null);
            Assert.That(Portrait("Laugh"), Is.SameAs(Portrait("Default")));
            Assert.That(Portrait("Damaged"), Is.SameAs(Portrait("Default")));
        }
        [Test] public void RefreshAfterDeletingExpressionFallsBackRatherThanKeepingStaleSprite()
        {
            Image("Default"); string laugh = Image("Laugh"); SetFolder(AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder));
            Assert.That(Refresh(), Is.True); Assert.That(Portrait("Laugh"), Is.Not.SameAs(Portrait("Default")));
            AssetDatabase.DeleteAsset(laugh); Assert.That(Refresh(), Is.True);
            Assert.That(Portrait("Laugh"), Is.SameAs(Portrait("Default")));
        }
        [Test] public void ChangingToEmptyFolderClearsOldPortraitsAndReportsMissingDefault()
        {
            Image("Default"); SetFolder(AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder)); Assert.That(Refresh(), Is.True);
            AssetDatabase.CreateFolder(folder, "Empty"); SetFolder(AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder + "/Empty"));
            Assert.That(Refresh(), Is.False); Assert.That(Portrait("Default"), Is.Null); Assert.That(Portrait("Laugh"), Is.Null);
        }
        [Test] public void ClearingFolderConnectsSamplePortraitFolderAndReplacesOldPortraits()
        {
            Image("Default"); SetFolder(AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder)); Assert.That(Refresh(), Is.True);
            var original = Portrait("Default");
            SetFolder(null); Assert.That(Refresh(), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(new SerializedObject(data).FindProperty("portraitFolder").objectReferenceValue),
                Is.EqualTo("Assets/_Game/Art/Characters/SampleAllyCharacter/Portraits"));
            Assert.That(Portrait("Default"), Is.Not.Null.And.Not.SameAs(original));
            Assert.That(Portrait("Laugh"), Is.SameAs(Portrait("Default")));
            Assert.That(Portrait("Damaged"), Is.SameAs(Portrait("Default")));
        }
        [Test] public void UnassignedPortraitFolderUsesSampleOnRefresh()
        {
            Assert.That(Refresh(), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(Portrait("Default")),
                Is.EqualTo("Assets/_Game/Art/Characters/SampleAllyCharacter/Portraits/Default.png"));
        }
        [Test] public void OpeningInspectorResolvesUnassignedPortraitFolder()
        {
            var editor = UnityEditor.Editor.CreateEditor(data, typeof(EditorTools.CharacterDataEditor));
            try
            {
                Assert.That(AssetDatabase.GetAssetPath(Portrait("Default")),
                    Is.EqualTo("Assets/_Game/Art/Characters/SampleAllyCharacter/Portraits/Default.png"));
            }
            finally { Object.DestroyImmediate(editor); }
        }
        [Test] public void DeletedPortraitFolderUsesSampleOnRefresh()
        {
            AssetDatabase.CreateFolder(folder, "Deleted"); Image("Default", folder + "/Deleted");
            SetFolder(AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder + "/Deleted")); Assert.That(Refresh(), Is.True);
            AssetDatabase.DeleteAsset(folder + "/Deleted"); Assert.That(Refresh(), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(Portrait("Default")),
                Is.EqualTo("Assets/_Game/Art/Characters/SampleAllyCharacter/Portraits/Default.png"));
        }
        [Test] public void TextureInsteadOfFolderIsRejected()
        {
            string path = Image("Default"); SetFolder(AssetDatabase.LoadAssetAtPath<Texture2D>(path)); Assert.That(Refresh(), Is.False);
        }
        [Test] public void SearchesOnlySelectedFolderNotSubfolders()
        {
            AssetDatabase.CreateFolder(folder, "Nested"); Image("Default", folder + "/Nested");
            SetFolder(AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder)); Assert.That(Refresh(), Is.False);
            Assert.That(Portrait("Default"), Is.Null);
        }
        [Test] public void SerializedSpritesSurviveAssetReloadWithoutSearchingFolder()
        {
            Image("Default"); Image("Damaged"); SetFolder(AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder)); Assert.That(Refresh(), Is.True);
            string path = folder + "/Pilot.asset"; AssetDatabase.CreateAsset(data, path); AssetDatabase.SaveAssets();
            string defaultPath = AssetDatabase.GetAssetPath(Portrait("Default")); string damagedPath = AssetDatabase.GetAssetPath(Portrait("Damaged"));
            Resources.UnloadAsset(data); data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            Assert.That(AssetDatabase.GetAssetPath(Portrait("Default")), Is.EqualTo(defaultPath));
            Assert.That(AssetDatabase.GetAssetPath(Portrait("Damaged")), Is.EqualTo(damagedPath));
            Assert.That(Portrait("Laugh"), Is.SameAs(Portrait("Default")));
        }
        [Test] public void ExistingMultipleSpriteImportIsPreservedAndOptionalExpressionFallsBack()
        {
            Image("Default"); string path = Image("Laugh");
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.SaveAndReimport();
            SetFolder(AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder)); Assert.That(Refresh(), Is.True);
            Assert.That(((TextureImporter)AssetImporter.GetAtPath(path)).spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
            Assert.That(Portrait("Laugh"), Is.SameAs(Portrait("Default")));
        }
        [Test] public void FolderReferenceSurvivesFolderRename()
        {
            AssetDatabase.CreateFolder(folder, "Original"); Image("Default", folder + "/Original");
            SetFolder(AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder + "/Original")); Assert.That(Refresh(), Is.True);
            Assert.That(AssetDatabase.MoveAsset(folder + "/Original", folder + "/Renamed"), Is.Empty);
            Assert.That(Refresh(), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(Portrait("Default")), Is.EqualTo(folder + "/Renamed/Default.png"));
        }
    }
}
