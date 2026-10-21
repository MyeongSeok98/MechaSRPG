using UnityEditor;
using UnityEngine;

namespace MechaXCOM.EditorTools
{
    [CustomEditor(typeof(UnitWeaponLoadout)), CanEditMultipleObjects]
    public class UnitWeaponLoadoutEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            EditorGUILayout.HelpBox("전투 시작 장착 설정입니다. 양손 무기는 한쪽에만 지정하고 반대 손은 비워 두세요.\n" +
                "빈 장착 설정은 비무장이며, 기존 UnitCombat 수치를 대신 사용하지 않습니다.", MessageType.Info);
            DrawHand("leftWeapon", "leftGrip", "왼손");
            DrawHand("rightWeapon", "rightGrip", "오른손");
            serializedObject.ApplyModifiedProperties();
            foreach (var item in targets)
            {
                var loadout = (UnitWeaponLoadout)item;
                if (!loadout.IsValid(out var reason))
                { EditorGUILayout.HelpBox(loadout.name + ": " + reason, MessageType.Error); continue; }
                string left = Describe(loadout.LeftWeapon, loadout.LeftGrip);
                string right = Describe(loadout.RightWeapon, loadout.RightGrip);
                if (loadout.RightWeapon && loadout.RightGrip == GripMode.TwoHands) left = "오른손 무기가 점유";
                if (loadout.LeftWeapon && loadout.LeftGrip == GripMode.TwoHands) right = "왼손 무기가 점유";
                EditorGUILayout.HelpBox($"{loadout.name}\n왼손: {left}\n오른손: {right}", MessageType.Info);
            }
            EditorGUILayout.HelpBox("이 설정은 모델이나 손잡이 위치를 자동으로 변경하지 않습니다.", MessageType.Info);
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
        }
        private static string Describe(WeaponData weapon, GripMode grip) =>
            weapon ? weapon.DisplayName + (grip == GripMode.TwoHands ? " (양손)" : " (한손)") : "비어 있음";
    }
}
