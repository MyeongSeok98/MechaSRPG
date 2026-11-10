using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MechaXCOM.EditorTools
{
    // Resolves authoring folder names into direct Sprite references usable in player builds.
    public static class CharacterPortraitLoader
    {
        public const string DefaultFolderPath = "Assets/_Game/Art/Characters/SampleAllyCharacter/Portraits";

        public static bool Refresh(CharacterData data, out string message)
        {
            message = null;
            if (!data) { message = "캐릭터 데이터를 선택하세요."; return false; }
            var serialized = new SerializedObject(data);
            var folderProperty = serialized.FindProperty("portraitFolder");
            var folder = folderProperty.objectReferenceValue;
            if (!folder)
            {
                folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultFolderPath);
                folderProperty.objectReferenceValue = folder;
            }
            string path = folder ? AssetDatabase.GetAssetPath(folder) : null;
            if (folder && (!AssetDatabase.IsValidFolder(path) || !path.StartsWith("Assets/")))
            { message = "Assets 아래의 초상화 폴더를 지정하세요."; return false; }

            var notes = new List<string>();
            Sprite standard = folder ? Load(path, "Default", notes) : null;
            Sprite laugh = folder ? Load(path, "Laugh", notes) : null;
            Sprite damaged = folder ? Load(path, "Damaged", notes) : null;
            serialized.FindProperty("defaultPortrait").objectReferenceValue = standard;
            serialized.FindProperty("laughPortrait").objectReferenceValue = laugh;
            serialized.FindProperty("damagedPortrait").objectReferenceValue = damaged;
            serialized.ApplyModifiedProperties();
            if (!standard) notes.Insert(0, folder
                ? "Default.png를 찾을 수 없거나 단일 Sprite로 가져오지 못했습니다."
                : "기본 초상화 폴더를 찾을 수 없습니다: " + DefaultFolderPath);
            message = string.Join("\n", notes);
            return standard;
        }

        private static Sprite Load(string folder, string name, List<string> notes)
        {
            string path = folder + "/" + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) return null;
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Multiple)
            {
                notes.Add(name + ".png는 Multiple Sprite입니다. 단일 초상화 파일로 지정하세요.");
                return null;
            }
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
