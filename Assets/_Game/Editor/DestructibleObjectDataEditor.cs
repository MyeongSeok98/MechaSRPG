using UnityEditor;
using UnityEngine;

namespace MechaXCOM.EditorTools
{
    [CustomEditor(typeof(DestructibleObjectData), true), CanEditMultipleObjects]
    public class DestructibleObjectDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("공유 오브젝트 정의입니다. 통신소 같은 시설은 단일 HP를 사용하고, TurretData는 AP·탐지·원거리 무장을 추가합니다.\n" +
                "현재 HP·AP·잔탄·진영·배치 위치는 개별 인스턴스에서 관리할 항목입니다.\n" +
                "시설은 CoverDurability의 오브젝트 데이터에 연결할 수 있습니다. 고정포대의 행동·AI는 아직 데이터만 제공하며 모델은 나중에 지정할 수 있습니다.", MessageType.Info);
            foreach (var item in targets)
                if (item is DestructibleObjectData data && !data.IsValid(out var reason))
                    EditorGUILayout.HelpBox(data.name + ": " + reason, MessageType.Error);
        }
    }
}
