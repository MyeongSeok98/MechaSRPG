using UnityEngine;

namespace MechaXCOM
{
    [CreateAssetMenu(fileName = "NewBackpack", menuName = "MechaXCOM/Backpack Data")]
    public class BackpackData : ScriptableObject
    {
        [SerializeField, InspectorName("백팩 ID")] private string backpackId;
        [SerializeField, InspectorName("이름")] private string displayName;
        [SerializeField, InspectorName("설명"), TextArea(3, 6)] private string description;
        [SerializeField, InspectorName("백팩 자체 중량 (톤)"), Min(0)] private float weightTonnes;
        [SerializeField, InspectorName("포함 무장 (없으면 일반 백팩)")] private WeaponData weapon;
        [SerializeField, InspectorName("모델 프리팹 (선택)")] private GameObject modelPrefab;

        public string BackpackId => backpackId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public float WeightTonnes => weightTonnes;
        public WeaponData Weapon => weapon;
        public GameObject ModelPrefab => modelPrefab;

        public bool IsValid(out string reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(backpackId)) reason = "백팩 ID를 입력하세요.";
            else if (float.IsNaN(weightTonnes) || float.IsInfinity(weightTonnes) || weightTonnes < 0)
                reason = "백팩 자체 중량은 유한한 0 이상의 값이어야 합니다.";
            else if (weapon && !weapon.IsValid(out var weaponReason))
                reason = "백팩 포함 무장: " + weaponReason;
            return reason == null;
        }
    }
}
