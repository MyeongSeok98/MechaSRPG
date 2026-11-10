using UnityEditor;
using UnityEngine;

namespace MechaXCOM.EditorTools
{
    [CustomEditor(typeof(MissionData)), CanEditMultipleObjects]
    public class MissionDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            foreach (var field in new[] { "missionId", "displayName", "description", "regionName", "objectiveType" })
                EditorGUILayout.PropertyField(serializedObject.FindProperty(field));
            var choice = serializedObject.FindProperty("objectiveType");
            if (choice.hasMultipleDifferentValues || choice.intValue == (int)MissionObjectiveType.EliminateEnemies)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("targetEnemyGroupId"));
            if (choice.hasMultipleDifferentValues || choice.intValue == (int)MissionObjectiveType.DestroyTarget)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("targetObjectId"));
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("공유 임무 설정입니다. 현재 진행도나 성공·실패 상태는 이 에셋에 저장하지 않습니다.\n" +
                "적 전멸은 대상 ID와 같은 EnemyUnitGroup을 등록하세요. 적 전원이 Destroyed여야 섬멸 완료입니다.\n" +
                "지역은 표시용 이름이며 씬을 자동으로 불러오지 않습니다. 복제한 임무에는 새 임무 ID를 지정하세요.", MessageType.Info);
            bool hasDestroyTarget = false;
            foreach (var item in targets)
                if (item is MissionData data)
                {
                    hasDestroyTarget |= data.ObjectiveType == MissionObjectiveType.DestroyTarget;
                    if (!data.IsValid(out var reason)) EditorGUILayout.HelpBox(data.name + ": " + reason, MessageType.Error);
                }
            if (hasDestroyTarget)
                EditorGUILayout.HelpBox("MissionController의 씬의 파괴 대상에 같은 ID의 MissionTarget을 등록하세요. 시설 내구도 또는 메카 내구도가 실제 파괴되어야 완료됩니다.", MessageType.Info);
        }
    }
}
