using UnityEditor;
using UnityEngine;

namespace MechaXCOM.EditorTools
{
    public static class BodyTargetArtSetup
    {
        [MenuItem("MechaXCOM/Configure Body Target Art")]
        public static void CreateAsset()
        {
            const string folder = "Assets/_Game/Resources";
            const string path = folder + "/BodyTargetArt.asset";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Game", "Resources");
            var art = AssetDatabase.LoadAssetAtPath<BodyTargetArt>(path);
            if (!art) { art = ScriptableObject.CreateInstance<BodyTargetArt>(); AssetDatabase.CreateAsset(art, path); }
            var names = new[] { "Mecha", "MechaTorso", "MechaCockpit", "MechaHead", "MechaLeftarm", "MechaRightarm", "MechaLeftleg", "MechaRightleg" };
            var serialized = new SerializedObject(art);
            var images = serialized.FindProperty("images"); images.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/UI/Targeting/" + names[i] + ".png");
                if (!texture) throw new System.InvalidOperationException("Missing targeting image: " + names[i]);
                images.GetArrayElementAtIndex(i).objectReferenceValue = texture;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }
}
