using UnityEditor;
using UnityEngine;

namespace MechaXCOM.EditorTools
{
    [CustomEditor(typeof(UnitHealth))]
    public class UnitHealthEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var health = (UnitHealth)target;
            var unit = health.GetComponent<BattleUnit>();
            if (!unit || !unit.MechaData) { DrawDefaultInspector(); return; }
            serializedObject.Update();
            EditorGUILayout.HelpBox("최대 HP는 BattleUnit의 기체 데이터에서 전투 시작 시 적용됩니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("기체 데이터", unit.MechaData, typeof(MechaData), false);
                for (int index = (int)TargetBodyPart.Torso; index <= (int)TargetBodyPart.RightLeg; index++)
                {
                    var part = (TargetBodyPart)index;
                    int maximum = Application.isPlaying && unit.IsInitialized ? health.GetMaxHP(part) : unit.MechaData.GetMaxHP(part);
                    EditorGUILayout.IntField(BodyTargetRules.Label(part) + " 최대 HP", maximum);
                    if (Application.isPlaying && unit.IsInitialized)
                        EditorGUILayout.IntField("현재 HP", health.GetCurrentHP(part));
                }
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("destructionChance"), new GUIContent("Critical 완전파괴 확률 (%)"));
            serializedObject.ApplyModifiedProperties();
        }
    }
    [CustomEditor(typeof(UnitActionPoints))]
    public class UnitActionPointsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var points = (UnitActionPoints)target;
            var unit = points.GetComponent<BattleUnit>();
            if (!unit || !unit.MechaData) { DrawDefaultInspector(); return; }
            EditorGUILayout.HelpBox("최대 AP는 BattleUnit의 기체 데이터에서 전투 시작 시 적용됩니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("기체 데이터", unit.MechaData, typeof(MechaData), false);
                EditorGUILayout.IntField("최대 AP", Application.isPlaying && unit.IsInitialized ? points.MaxPoints : unit.MechaData.BaseAP);
                if (Application.isPlaying && unit.IsInitialized) EditorGUILayout.IntField("현재 AP", points.CurrentPoints);
            }
        }
    }
}
