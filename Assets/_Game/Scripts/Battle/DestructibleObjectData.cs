using UnityEngine;

namespace MechaXCOM
{
    // Shared single-HP object specifications. Current HP belongs to a future scene instance.
    [CreateAssetMenu(fileName = "NewDestructibleObject", menuName = "MechaXCOM/Destructible Object Data")]
    public class DestructibleObjectData : ScriptableObject
    {
        [SerializeField, InspectorName("오브젝트 ID")] private string objectId;
        [SerializeField, InspectorName("이름")] private string displayName;
        [SerializeField, InspectorName("설명"), TextArea(3, 6)] private string description;
        [SerializeField, InspectorName("최대 HP"), Min(1)] private int maxHP = 2000;
        [SerializeField, InspectorName("모델 프리팹 (선택)")] private GameObject modelPrefab;

        public string ObjectId => objectId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public int MaxHP => maxHP;
        public GameObject ModelPrefab => modelPrefab;

        public virtual bool IsValid(out string reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(objectId)) reason = "오브젝트 ID를 입력하세요.";
            else if (maxHP < 1) reason = "최대 HP는 1 이상이어야 합니다.";
            return reason == null;
        }
    }
}
