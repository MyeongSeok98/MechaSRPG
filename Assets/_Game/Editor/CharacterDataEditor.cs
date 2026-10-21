using UnityEditor;
using UnityEngine;

namespace MechaXCOM.EditorTools
{
    [CustomEditor(typeof(CharacterData)), CanEditMultipleObjects]
    public class CharacterDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("파일럿의 초기 정의입니다. 현재 레벨·경험치·성장 능력치와 패시브 목록은 아직 연결하지 않았습니다.\n" +
                "복제한 캐릭터에는 새 ID를 지정하세요. 능력치는 아직 전투에 적용되지 않습니다.", MessageType.Info);
            foreach (var item in targets)
                if (item is CharacterData data && !data.IsValid(out var reason))
                    EditorGUILayout.HelpBox(data.name + ": " + reason, MessageType.Error);
        }
    }
}
