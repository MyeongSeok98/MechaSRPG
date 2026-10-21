using UnityEngine;

namespace MechaXCOM
{
    // Shared type specifications only. Current HP, AP and equipment belong to each unit.
    [CreateAssetMenu(fileName = "NewMecha", menuName = "MechaXCOM/Mecha Data")]
    public class MechaData : ScriptableObject
    {
        [SerializeField] private string mechaId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(3, 6)] private string description;
        [SerializeField] private GameObject modelPrefab;
        [SerializeField, Min(1)] private int baseAP = 5;
        [SerializeField, Min(0)] private float movementCellsPerAP = 1f;
        [SerializeField, Min(1)] private int bodyMaxHP = 20;
        [SerializeField, Min(1)] private int cockpitMaxHP = 6;
        [SerializeField, Min(1)] private int headMaxHP = 6;
        [SerializeField, Min(1)] private int leftArmMaxHP = 8;
        [SerializeField, Min(1)] private int rightArmMaxHP = 8;
        [SerializeField, Min(1)] private int leftLegMaxHP = 10;
        [SerializeField, Min(1)] private int rightLegMaxHP = 10;
        [SerializeField, Min(0)] private float chassisWeightTonnes;
        [SerializeField, Min(0)] private float equipmentWeightLimitTonnes;

        public string MechaId => mechaId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public GameObject ModelPrefab => modelPrefab;
        public int BaseAP => baseAP;
        public float MovementCellsPerAP => movementCellsPerAP;
        public float ChassisWeightTonnes => chassisWeightTonnes;
        public float EquipmentWeightLimitTonnes => equipmentWeightLimitTonnes;
        public int GetMaxHP(TargetBodyPart part)
        {
            switch (BodyTargetRules.Normalize(part))
            {
                case TargetBodyPart.Torso: return bodyMaxHP;
                case TargetBodyPart.Cockpit: return cockpitMaxHP;
                case TargetBodyPart.Head: return headMaxHP;
                case TargetBodyPart.LeftArm: return leftArmMaxHP;
                case TargetBodyPart.RightArm: return rightArmMaxHP;
                case TargetBodyPart.LeftLeg: return leftLegMaxHP;
                case TargetBodyPart.RightLeg: return rightLegMaxHP;
                default: return 0;
            }
        }
        public bool IsValid(out string reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(mechaId)) reason = "기체 ID를 입력하세요.";
            else if (baseAP < 1) reason = "기본 AP는 1 이상이어야 합니다.";
            else if (!Finite(movementCellsPerAP) || movementCellsPerAP <= 0)
                reason = "AP당 이동거리는 0보다 큰 유한한 값이어야 합니다.";
            else if (bodyMaxHP < 1 || cockpitMaxHP < 1 || headMaxHP < 1 || leftArmMaxHP < 1 ||
                rightArmMaxHP < 1 || leftLegMaxHP < 1 || rightLegMaxHP < 1)
                reason = "모든 부위의 최대 HP는 1 이상이어야 합니다.";
            else if (!Finite(chassisWeightTonnes) || chassisWeightTonnes < 0 ||
                !Finite(equipmentWeightLimitTonnes) || equipmentWeightLimitTonnes < 0)
                reason = "기체 자체 중량과 장착 중량 한도는 0 이상의 유한한 값이어야 합니다.";
            return reason == null;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private void OnValidate()
        {
            baseAP = Mathf.Max(1, baseAP);
            if (!Finite(movementCellsPerAP) || movementCellsPerAP <= 0) movementCellsPerAP = 1f;
            bodyMaxHP = Mathf.Max(1, bodyMaxHP); cockpitMaxHP = Mathf.Max(1, cockpitMaxHP);
            headMaxHP = Mathf.Max(1, headMaxHP);
            leftArmMaxHP = Mathf.Max(1, leftArmMaxHP); rightArmMaxHP = Mathf.Max(1, rightArmMaxHP);
            leftLegMaxHP = Mathf.Max(1, leftLegMaxHP); rightLegMaxHP = Mathf.Max(1, rightLegMaxHP);
            chassisWeightTonnes = Finite(chassisWeightTonnes) ? Mathf.Max(0, chassisWeightTonnes) : 0;
            equipmentWeightLimitTonnes = Finite(equipmentWeightLimitTonnes) ? Mathf.Max(0, equipmentWeightLimitTonnes) : 0;
        }
    }
}
