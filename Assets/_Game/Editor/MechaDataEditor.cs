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
            DamageTakenField("ballisticDamageTakenPercent", "실탄 받는 피해 (%)", false);
            DamageTakenField("beamDamageTakenPercent", "빔 받는 피해 (%)", true);
            EditorGUILayout.HelpBox("50~150%, 기본 100%. 80은 피해 20% 감소, 120은 피해 20% 증가입니다.\n" +
                "모든 부위에 적용하며 폭발물·근접에는 적용하지 않습니다. 명중률은 바뀌지 않습니다.\n" +
                "기존 장갑 설정은 같은 효과로 읽으며, 슬라이더를 변경하면 직접 지정한 값이 우선합니다.", MessageType.Info);
            EditorGUILayout.Space();
            Field("baseAP", "기본 AP");
            Field("accuracy", "명중력 (0~50)");
            Field("evasion", "회피력 (0~50)");
            Field("movementCellsPerAP", "AP당 이동거리 (칸/AP)");
            EditorGUILayout.HelpBox("이동 비용은 자기 턴 동안 누적합니다. 이동·사격·이동을 나눠도 이미 지불한 이동분을 유지합니다.\n" +
                "다리 1개 파괴 시 효율 절반, 2개 파괴 시 이동 불가입니다. 모델의 이동 애니메이션 속도와는 별개입니다.", MessageType.Info);
            Field("detectionRange", "기본 탐지 거리 (칸)");
            EditorGUILayout.HelpBox("탐지 거리는 전투 시작 시 UnitVision에 적용합니다. 기본값은 10칸이며 0은 반경 0입니다. 추가부품 보정은 아직 적용하지 않습니다.", MessageType.Info);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("부위별 최대 HP", EditorStyles.boldLabel);
            Field("bodyMaxHP", "몸통"); Field("cockpitMaxHP", "콕피트"); Field("headMaxHP", "머리");
            Field("leftArmMaxHP", "왼팔"); Field("rightArmMaxHP", "오른팔");
            Field("leftLegMaxHP", "왼다리"); Field("rightLegMaxHP", "오른다리");
            EditorGUILayout.Space();
            Field("chassisWeightTonnes", "기체 자체 중량 (t)");
            Field("equipmentWeightLimitTonnes", "장착 중량 한도 (t)");
            EditorGUILayout.HelpBox("BattleUnit이 전투 시작 시 최대 HP·기본 AP·명중력·회피력·이동 효율·모델을 적용합니다.\n" +
                "장착 중량 한도는 0이면 제한 미설정, 양수이면 초과 장착·전투 시작을 차단합니다. 이동력 감소는 없습니다.\n" +
                "명중력과 대상 회피력이 같으면 보정은 ×1입니다. 계수는 CombatMath에서 조정합니다.\n" +
                "모델은 +Z 전방, 발밑 원점 기준 프리팹을 사용하세요. 복제한 기종에는 새 ID를 지정하세요.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            foreach (var item in targets)
                if (item is MechaData data && !data.IsValid(out var reason))
                    EditorGUILayout.HelpBox(data.name + ": " + reason, MessageType.Error);
        }
        private void Field(string property, string label) =>
            EditorGUILayout.PropertyField(serializedObject.FindProperty(property), new GUIContent(label), true);
        private void DamageTakenField(string name, string label, bool beam)
        {
            var property = serializedObject.FindProperty(name);
            var first = (MechaData)target;
            int effective = beam ? first.BeamDamageTakenPercent : first.BallisticDamageTakenPercent;
            EditorGUI.showMixedValue = DamageTakenValuesMixed(beam);
            EditorGUI.BeginChangeCheck();
            int value = EditorGUILayout.IntSlider(label, property.intValue == -1 ? effective : property.intValue,
                MechaData.MinDamageTakenPercent, MechaData.MaxDamageTakenPercent);
            if (EditorGUI.EndChangeCheck()) property.intValue = value;
            EditorGUI.showMixedValue = false;
        }
        private bool DamageTakenValuesMixed(bool beam)
        {
            var first = (MechaData)target;
            int effective = beam ? first.BeamDamageTakenPercent : first.BallisticDamageTakenPercent;
            foreach (var item in targets)
            {
                var data = (MechaData)item;
                if ((beam ? data.BeamDamageTakenPercent : data.BallisticDamageTakenPercent) != effective) return true;
            }
            return false;
        }
    }
}
