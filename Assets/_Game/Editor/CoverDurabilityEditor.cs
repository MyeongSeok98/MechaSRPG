using UnityEditor;
using UnityEngine;

namespace MechaXCOM.EditorTools
{
    [CustomEditor(typeof(CoverDurability)), CanEditMultipleObjects]
    public class CoverDurabilityEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            bool runtime = Application.isPlaying && !EditorUtility.IsPersistent(target);
            using (new EditorGUI.DisabledScope(runtime))
            {
                var data = serializedObject.FindProperty("objectData");
                EditorGUILayout.PropertyField(data);
                if (data.hasMultipleDifferentValues || !data.objectReferenceValue)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("maxHP"), new GUIContent("최대 HP (데이터 미지정 시)"));
                else
                    EditorGUILayout.LabelField("데이터 최대 HP", ((DestructibleObjectData)data.objectReferenceValue).MaxHP.ToString());
            }
            serializedObject.ApplyModifiedProperties();
            if (runtime)
                using (new EditorGUI.DisabledScope(true))
                    foreach (var item in targets)
                    {
                        var health = (CoverDurability)item;
                        EditorGUILayout.IntField(item.name + " 개체 최대 HP", health.MaxHP);
                        EditorGUILayout.IntField(item.name + " 현재 HP", health.CurrentHP);
                        EditorGUILayout.Toggle("파괴됨", health.IsDestroyed);
                    }
            EditorGUILayout.HelpBox("개체별로 최대 HP에서 시작합니다. HP0이면 외형·충돌·엄폐·이동 차단 기능을 제거합니다.\n" +
                "다른 장애물의 차단은 유지합니다. Half/Full 종류와 내구도는 별개이며 전투 중 재활성화로 회복하지 않습니다.", MessageType.Info);
        }
    }
}
