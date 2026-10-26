using UnityEditor;
using UnityEngine;

namespace MechaXCOM.EditorTools
{
    [CustomEditor(typeof(MechaData)), CanEditMultipleObjects]
    public class MechaDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            Field("mechaId", "기체 ID");
            Field("displayName", "기체 이름");
            Field("description", "설명");
            Field("modelPrefab", "모델 프리팹");
            Field("armorType", "장갑 타입");
            EditorGUILayout.HelpBox("장갑은 모든 부위에 적용됩니다. 대빔: 빔 ×0.8 / 실탄 ×1.1, 대실탄: 빔 ×1.1 / 실탄 ×0.8.\n" +
                "일반 장갑·폭발물·근접 공격은 ×1입니다. 명중률에는 영향을 주지 않습니다.", MessageType.Info);
            EditorGUILayout.Space();
            Field("baseAP", "기본 AP");
            Field("accuracy", "명중력 (0~50)");
            Field("evasion", "회피력 (0~50)");
            Field("movementCellsPerAP", "AP당 이동거리 (칸/AP)");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("부위별 최대 HP", EditorStyles.boldLabel);
            Field("bodyMaxHP", "몸통"); Field("cockpitMaxHP", "콕피트"); Field("headMaxHP", "머리");
            Field("leftArmMaxHP", "왼팔"); Field("rightArmMaxHP", "오른팔");
            Field("leftLegMaxHP", "왼다리"); Field("rightLegMaxHP", "오른다리");
            EditorGUILayout.Space();
            Field("chassisWeightTonnes", "기체 자체 중량 (t)");
            Field("equipmentWeightLimitTonnes", "장착 중량 한도 (t)");
            EditorGUILayout.HelpBox("BattleUnit이 전투 시작 시 최대 HP·기본 AP·명중력·회피력·모델을 적용합니다. 이동 효율·중량 제한은 아직 데이터만 저장합니다.\n" +
                "명중력과 대상 회피력이 같으면 보정은 ×1입니다. 계수는 CombatMath에서 조정합니다.\n" +
                "모델은 +Z 전방, 발밑 원점 기준 프리팹을 사용하세요. 복제한 기종에는 새 ID를 지정하세요.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            foreach (var item in targets)
                if (item is MechaData data && !data.IsValid(out var reason))
                    EditorGUILayout.HelpBox(data.name + ": " + reason, MessageType.Error);
        }
        private void Field(string property, string label) =>
            EditorGUILayout.PropertyField(serializedObject.FindProperty(property), new GUIContent(label), true);
    }
}
