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
            Field("traits", "무기 특성");
            EditorGUILayout.HelpBox("건물 너머 곡사 가능: 곡사 폭발무기의 발사 경로만 건물을 넘습니다.\n" +
                "특성이 없으면 큰 건물에 발사가 막힙니다. 착탄 후 폭발과 직사 공격은 건물에 막힙니다.", MessageType.Info);
            Field("attackType", "공격 종류 (Ranged / Melee)");
            var type = serializedObject.FindProperty("attackType");
            if (type.hasMultipleDifferentValues || type.intValue == (int)AttackType.Ranged)
                Field("rangedCategory", "원거리 분류 (빔 / 실탄 / 폭발물)");
            if (type.hasMultipleDifferentValues || type.intValue == (int)AttackType.Melee)
                Field("meleeCategory", "근거리 분류 (날붙이 / 둔기)");
            var category = serializedObject.FindProperty("rangedCategory");
            if ((type.hasMultipleDifferentValues || type.intValue == (int)AttackType.Ranged) &&
                (category.hasMultipleDifferentValues || category.intValue == (int)RangedWeaponCategory.Explosive))
            {
                EditorGUILayout.Space();
                Field("coverDamageMultiplier", "엄폐물 피해 배율 (0: 피해 없음)");
                EditorGUILayout.HelpBox("기본 피해를 엄폐물마다 한 번 추첨해 배율을 적용합니다. 직사는 엄폐물을 직접 선택하고, 곡사는 범위 내 엄폐물을 타격합니다.\n" +
                    "부위 수·치명타·장갑·파일럿 보정을 엄폐물 피해에 적용하지 않습니다.", MessageType.Info);
                Field("rangedAttackMode", "공격 방식");
                var mode = serializedObject.FindProperty("rangedAttackMode");
                if (mode.hasMultipleDifferentValues || mode.intValue == (int)RangedAttackMode.Indirect)
                {
                    EditorGUILayout.LabelField("곡사 폭발 · 좌표 범위 공격", EditorStyles.boldLabel);
                    Field("blastRadius", "폭발 반경 (칸, 마름모)");
                    Field("minHitParts", "최소 피격 부위 수"); Field("maxHitParts", "최대 피격 부위 수");
                    EditorGUILayout.HelpBox("기체마다 부위 수를 추첨하고 각 부위의 피해를 별도로 추첨합니다. 콕피트·파괴된 부위는 제외합니다. 직사 부가 피해 배율은 사용하지 않습니다.", MessageType.Info);
                }
                if (mode.hasMultipleDifferentValues || mode.intValue == (int)RangedAttackMode.Direct)
                {
                EditorGUILayout.LabelField("직사 폭발 · 추가 부위 피해", EditorStyles.boldLabel);
                Field("additionalHitPartCount", "추가 피해 부위 수 (0: 비활성)");
                Field("additionalDamageMinRatio", "부가 피해 최소 배율 (0~1)");
                Field("additionalDamageMaxRatio", "부가 피해 최대 배율 (0~1)");
                Field("additionalDamageStepPercent", "부가 피해 추첨 간격 (%p)");
                EditorGUILayout.HelpBox("한 적의 직격 부위·콕피트·파괴된 부위를 제외하고 중복 없이 선택합니다.\n" +
                    "배율 0.05는 5%, 간격 5는 5%p입니다. 최소·최대 퍼센트는 간격의 배수로 설정하세요.\n" +
                    "예: 0~0.2, 간격 5 → 0/5/10/15/20%에서 균등 추첨합니다. 부위마다 별도로 추첨하며 곡사·주변 기체 범위 피해는 없습니다.", MessageType.Info);
                }
            }
            EditorGUILayout.Space();
            Field("minDamage", "최소 피해"); Field("maxDamage", "최대 피해");
            Field("baseHitChance", "기본 명중률 (%)"); Field("range", "사거리 (칸)");
            Field("apCost", "공격 AP 비용"); Field("weightTonnes", "빈 무기 본체 중량 (t)");
            if (type.hasMultipleDifferentValues || type.intValue == (int)AttackType.Ranged)
            {
                Field("magazineCapacity", "장탄 수 (발/탄창·팩)");
                Field("magazineWeightTonnes", "만탄 탄창·팩 1개 중량 (t)");
                Field("spareMagazineCount", "권장 예비 개수 (기체별 설정의 기본값)");
                Field("reloadAPCost", "재장전 AP 비용");
                EditorGUILayout.HelpBox("실제 예비 개수는 기체의 UnitWeaponLoadout에서 선택합니다. -1이면 이 권장값을 사용합니다.\n" +
                    "장착 중량 = 빈 무기 + (장전 1개 + 예비 개수) × 만탄 탄창·팩 중량. 백팩은 장전 1개, 예비 0개입니다.\n" +
                    "사격마다 1발 소모하며, 재장전하면 잔탄을 버리고 예비 1개로 교체합니다.", MessageType.Info);
            }
            EditorGUILayout.HelpBox("빔·실탄 분류는 대상 장갑에 따라 피해량이 보정됩니다. 폭발물·근접은 장갑 상성이 없습니다. 무기 중량은 기체의 장착 중량 한도 검사에 포함합니다.", MessageType.Info);
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
