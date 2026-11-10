using UnityEditor;
using UnityEngine;

namespace MechaXCOM.EditorTools
{
    [CustomEditor(typeof(CharacterData)), CanEditMultipleObjects]
    public class CharacterDataEditor : UnityEditor.Editor
    {
        private string portraitMessage;
        private void OnEnable()
        {
            foreach (var item in targets)
            {
                var individual = new SerializedObject(item);
                if (!individual.FindProperty("portraitFolder").objectReferenceValue)
                {
                    CharacterPortraitLoader.Refresh((CharacterData)item, out var message);
                    if (!string.IsNullOrEmpty(message)) portraitMessage = message;
                }
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            DrawPortraits();
            EditorGUILayout.HelpBox("파일럿의 초기 정의입니다. 현재 레벨·경험치·성장 능력치와 패시브 목록은 아직 연결하지 않았습니다.\n" +
                "복제한 캐릭터에는 새 ID를 지정하세요. BattleUnit에 연결하면 기량(0~50)이 치명타 확률에 적용됩니다. 다른 능력치는 아직 전투에 적용되지 않습니다.", MessageType.Info);
            foreach (var item in targets)
                if (item is CharacterData data && !data.IsValid(out var reason))
                    EditorGUILayout.HelpBox(data.name + ": " + reason, MessageType.Error);
        }

        private void DrawPortraits()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("초상화", EditorStyles.boldLabel);
            serializedObject.Update();
            var folder = serializedObject.FindProperty("portraitFolder");
            EditorGUI.showMixedValue = folder.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var selected = EditorGUILayout.ObjectField("초상화 폴더", folder.objectReferenceValue, typeof(DefaultAsset), false);
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;
            if (changed)
            {
                string path = selected ? AssetDatabase.GetAssetPath(selected) : null;
                if (selected && (!AssetDatabase.IsValidFolder(path) || !path.StartsWith("Assets/")))
                    portraitMessage = "Assets 아래의 폴더를 지정하세요. 파일은 지정할 수 없습니다.";
                else
                {
                    folder.objectReferenceValue = selected;
                    serializedObject.ApplyModifiedProperties();
                    RefreshPortraits();
                }
            }
            if (GUILayout.Button("초상화 새로고침")) RefreshPortraits();
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                foreach (var item in targets)
                {
                    var data = (CharacterData)item;
                    if (targets.Length > 1) EditorGUILayout.LabelField(data.name);
                    EditorGUILayout.ObjectField("Default", data.DefaultPortrait, typeof(Sprite), false);
                    EditorGUILayout.ObjectField("Laugh (없으면 Default)", data.GetPortrait(PortraitExpression.Laugh), typeof(Sprite), false);
                    EditorGUILayout.ObjectField("Damaged (없으면 Default)", data.GetPortrait(PortraitExpression.Damaged), typeof(Sprite), false);
                    if (!data.DefaultPortrait)
                        EditorGUILayout.HelpBox(data.name + ": Default.png를 준비하고 초상화를 새로고침하세요.", MessageType.Warning);
                }
            if (!string.IsNullOrEmpty(portraitMessage)) EditorGUILayout.HelpBox(portraitMessage, MessageType.Warning);
            EditorGUILayout.HelpBox("선택 폴더 바로 아래의 Default.png / Laugh.png / Damaged.png를 연결합니다.\n" +
                "폴더 미지정·연결 해제 시 SampleAllyCharacter/Portraits를 기본으로 연결합니다.\n" +
                "일반 PNG는 Sprite (Single)로 가져옵니다. 파일 추가·삭제 후 새로고침하세요.\n" +
                "Laugh·Damaged가 없으면 Default를 사용합니다. 표정 전환 UI는 별도 연결이 필요합니다.", MessageType.Info);
        }

        private void RefreshPortraits()
        {
            var messages = new System.Collections.Generic.List<string>();
            foreach (var item in targets)
            {
                CharacterPortraitLoader.Refresh((CharacterData)item, out var message);
                if (!string.IsNullOrEmpty(message)) messages.Add(item.name + ": " + message);
            }
            portraitMessage = string.Join("\n", messages);
        }
    }
}
