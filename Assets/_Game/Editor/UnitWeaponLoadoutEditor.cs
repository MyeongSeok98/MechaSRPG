using UnityEditor;
using UnityEngine;

namespace MechaXCOM.EditorTools
{
    [CustomEditor(typeof(UnitWeaponLoadout)), CanEditMultipleObjects]
    public class UnitWeaponLoadoutEditor : UnityEditor.Editor
    {
        private string changeError;
        // Validate the pending serialized values before ApplyModifiedProperties so Undo and
        // multi-object editing cannot leave a partially accepted equipment change.
        public bool TryApplyEquipmentChanges(out string reason)
        {
            reason = null;
            if (!serializedObject.hasModifiedProperties) return true;
            var left = serializedObject.FindProperty("leftWeapon");
            var right = serializedObject.FindProperty("rightWeapon");
            var pack = serializedObject.FindProperty("backpack");
            var leftCount = serializedObject.FindProperty("leftSpareMagazineCount");
            var rightCount = serializedObject.FindProperty("rightSpareMagazineCount");
            foreach (var item in targets)
            {
                var loadout = (UnitWeaponLoadout)item;
                int pendingLeft = leftCount.hasMultipleDifferentValues ? loadout.LeftSpareMagazineSelection : leftCount.intValue;
                int pendingRight = rightCount.hasMultipleDifferentValues ? loadout.RightSpareMagazineSelection : rightCount.intValue;
                var unit = loadout.GetComponent<BattleUnit>();
                if (unit && unit.IsInitialized && (pendingLeft != loadout.LeftSpareMagazineSelection || pendingRight != loadout.RightSpareMagazineSelection))
                {
                    reason = loadout.name + ": 예비 탄약 개수는 전투 시작 전에만 변경할 수 있습니다.";
                    serializedObject.Update(); return false;
                }
                if (!loadout.CanChangeEquipment(left.hasMultipleDifferentValues ? loadout.LeftWeapon : (WeaponData)left.objectReferenceValue,
                    right.hasMultipleDifferentValues ? loadout.RightWeapon : (WeaponData)right.objectReferenceValue,
                    pack.hasMultipleDifferentValues ? loadout.Backpack : (BackpackData)pack.objectReferenceValue,
                    pendingLeft, pendingRight, out reason))
                {
                    reason = loadout.name + ": " + reason;
                    serializedObject.Update();
                    return false;
                }
            }
            serializedObject.ApplyModifiedProperties();
            return true;
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            EditorGUILayout.HelpBox("전투 시작 장착 설정입니다. 양손 무기는 한쪽에만 지정하고 반대 손은 비워 두세요.\n" +
                "빈 장착 설정은 비무장이며, 기존 UnitCombat 수치를 대신 사용하지 않습니다.", MessageType.Info);
            DrawHand("leftWeapon", "leftGrip", "왼손");
            DrawHand("rightWeapon", "rightGrip", "오른손");
            EditorGUILayout.HelpBox("예비 개수 -1은 무기의 권장 기본값을 사용합니다. 0 이상을 입력하면 기체별 개수로 고정됩니다.\n" +
                "장전 탄창·팩 1개는 별도이며, 예비 개수에 포함하지 않습니다.", MessageType.Info);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("백팩 (손을 점유하지 않음)", EditorStyles.boldLabel);
            bool initialized = false;
            foreach (var item in targets)
            {
                var unit = ((UnitWeaponLoadout)item).GetComponent<BattleUnit>();
                if (unit && unit.IsInitialized) initialized = true;
            }
            using (new EditorGUI.DisabledScope(initialized))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("backpack"), new GUIContent("장착 백팩"));
            if (initialized) EditorGUILayout.HelpBox("백팩 교체는 전투 시작 전에 설정하세요.", MessageType.Info);
            if (!TryApplyEquipmentChanges(out var error)) changeError = error;
            else if (GUI.changed) changeError = null;
            if (!string.IsNullOrEmpty(changeError)) EditorGUILayout.HelpBox(changeError, MessageType.Error);
            foreach (var item in targets)
            {
                var loadout = (UnitWeaponLoadout)item;
                string limit = loadout.EquipmentWeightLimitTonnes > 0
                    ? $"{loadout.EquipmentWeightLimitTonnes:0.###}톤" : "제한 미설정 (0)";
                EditorGUILayout.HelpBox($"총 장착 중량: {loadout.TotalEquipmentWeightTonnes:0.###}톤 / 한도: {limit}", MessageType.Info);
                if (!loadout.IsValid(out var reason))
                { EditorGUILayout.HelpBox(loadout.name + ": " + reason, MessageType.Error); continue; }
                string left = Describe(loadout.LeftWeapon, loadout.LeftGrip);
                string right = Describe(loadout.RightWeapon, loadout.RightGrip);
                if (loadout.RightWeapon && loadout.RightGrip == GripMode.TwoHands) left = "오른손 무기가 점유";
                if (loadout.LeftWeapon && loadout.LeftGrip == GripMode.TwoHands) right = "왼손 무기가 점유";
                string pack = loadout.Backpack ? loadout.Backpack.DisplayName : "미장착";
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("백팩 포함 무장", loadout.GetWeapon(WeaponSlot.Backpack), typeof(WeaponData), false);
                EditorGUILayout.HelpBox($"{loadout.name}\n왼손: {left}\n오른손: {right}\n백팩: {pack}\n" +
                    $"예비: 왼손 {loadout.GetSpareMagazineCount(WeaponSlot.LeftHand)}개 / 오른손 {loadout.GetSpareMagazineCount(WeaponSlot.RightHand)}개\n" +
                    $"총 장착 중량: {loadout.TotalEquipmentWeightTonnes:0.###}톤 (빈 무기 + 장전·예비 탄약 + 백팩)", MessageType.Info);
            }
            EditorGUILayout.HelpBox("이 설정은 모델이나 손잡이 위치를 자동으로 변경하지 않습니다.", MessageType.Info);
            EditorGUILayout.HelpBox("한도가 양수이면 초과 장착과 전투 시작을 차단합니다. 한도 0은 제한 미설정입니다. 이동력에는 영향을 주지 않습니다.", MessageType.Info);
        }
        private void DrawHand(string weapon, string grip, string hand)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(hand, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(weapon), new GUIContent(hand + " 무기"));
            var property = serializedObject.FindProperty(grip);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int value = EditorGUILayout.Popup("파지 방식", property.intValue, new[] { "한손", "양손" });
            if (EditorGUI.EndChangeCheck()) property.intValue = value;
            EditorGUI.showMixedValue = false;
            bool initialized = false;
            foreach (var item in targets)
            {
                var unit = ((UnitWeaponLoadout)item).GetComponent<BattleUnit>();
                if (unit && unit.IsInitialized) initialized = true;
            }
            using (new EditorGUI.DisabledScope(initialized))
                EditorGUILayout.PropertyField(serializedObject.FindProperty(weapon == "leftWeapon" ?
                    "leftSpareMagazineCount" : "rightSpareMagazineCount"), new GUIContent("예비 탄창·팩 개수 (-1: 기본값)"));
        }
        private static string Describe(WeaponData weapon, GripMode grip) =>
            weapon ? weapon.DisplayName + (grip == GripMode.TwoHands ? " (양손)" : " (한손)") : "비어 있음";
    }
}
