using UnityEditor;
using UnityEngine;

namespace MechaXCOM.EditorTools
{
    [CustomEditor(typeof(WeaponData)), CanEditMultipleObjects]
    public class WeaponDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            Field("displayName", "무기 이름");
            Field("description", "설명");
            Field("attackType", "공격 종류 (Ranged / Melee)");
            var type = serializedObject.FindProperty("attackType");
            if (type.hasMultipleDifferentValues || type.intValue == (int)AttackType.Ranged)
                Field("rangedCategory", "원거리 분류 (빔 / 실탄 / 폭발물)");
            if (type.hasMultipleDifferentValues || type.intValue == (int)AttackType.Melee)
                Field("meleeCategory", "근거리 분류 (날붙이 / 둔기)");
            EditorGUILayout.Space();
            Field("minDamage", "최소 피해"); Field("maxDamage", "최대 피해");
            Field("baseHitChance", "기본 명중률 (%)"); Field("range", "사거리 (칸)");
            Field("apCost", "공격 AP 비용"); Field("weightTonnes", "중량 (t)");
            EditorGUILayout.HelpBox("중량과 무기 분류는 현재 추가 효과를 주지 않습니다.", MessageType.Info);
            Field("gripRequirement", "파지 조건");
            var grip = serializedObject.FindProperty("gripRequirement");
            if (grip.hasMultipleDifferentValues || grip.intValue == (int)GripRequirement.Either)
                Field("twoHandHitMultiplier", "양손 명중 배율");
            if (type.hasMultipleDifferentValues || type.intValue == (int)AttackType.Ranged)
            {
                EditorGUILayout.Space();
                Field("distanceBands", "거리별 명중률 구간");
                EditorGUILayout.HelpBox("Max Distance는 구간의 마지막 칸입니다. 오름차순으로 입력하세요.\n" +
                    "예: 2 / 0.9, 5 / 1, 7 / 0.8. 빈 목록과 마지막 구간 이후는 ×1.0입니다.", MessageType.Info);
            }
            serializedObject.ApplyModifiedProperties();
            foreach (var item in targets)
                if (item is WeaponData data && !data.IsValid(out var reason))
                    EditorGUILayout.HelpBox(data.name + ": " + reason, MessageType.Error);
        }
        private void Field(string name, string label) =>
            EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label), true);
    }
}
