using UnityEngine;

namespace MechaXCOM
{
    [CreateAssetMenu(fileName = "NewTurret", menuName = "MechaXCOM/Turret Data")]
    public class TurretData : DestructibleObjectData
    {
        [SerializeField, InspectorName("기본 AP"), Min(1)] private int baseAP = 5;
        [SerializeField, InspectorName("탐지 거리 (칸)"), Min(0)] private int detectionRange = 10;
        [SerializeField, InspectorName("고정 원거리 무장")]
        [Tooltip("피해·명중률·사거리·발사 AP·장탄 설정은 연결한 WeaponData를 사용합니다.")]
        private WeaponData weapon;

        public int BaseAP => baseAP;
        public int DetectionRange => detectionRange;
        public WeaponData Weapon => weapon;

        public override bool IsValid(out string reason)
        {
            if (!base.IsValid(out reason)) return false;
            if (baseAP < 1) reason = "기본 AP는 1 이상이어야 합니다.";
            else if (detectionRange < 0) reason = "탐지 거리는 0 이상이어야 합니다.";
            else if (!weapon) reason = "포대의 원거리 무장을 지정하세요.";
            else if (weapon.AttackType != AttackType.Ranged) reason = "포대에는 원거리 무장을 지정하세요.";
            else if (!weapon.IsValid(out var weaponReason)) reason = "포대 무장: " + weaponReason;
            else if (weapon.APCost > baseAP) reason = "무기의 발사 AP가 포대의 기본 AP를 초과합니다.";
            return reason == null;
        }
    }
}
