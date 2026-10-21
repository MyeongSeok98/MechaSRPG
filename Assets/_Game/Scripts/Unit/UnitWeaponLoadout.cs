using UnityEngine;

namespace MechaXCOM
{
    public enum WeaponSlot { RightHand, LeftHand }
    [DisallowMultipleComponent]
    public class UnitWeaponLoadout : MonoBehaviour
    {
        [SerializeField] private WeaponData leftWeapon, rightWeapon;
        [SerializeField] private GripMode leftGrip, rightGrip;
        public WeaponData LeftWeapon => leftWeapon;
        public WeaponData RightWeapon => rightWeapon;
        public GripMode LeftGrip => leftGrip;
        public GripMode RightGrip => rightGrip;
        public event System.Action Changed;
        public void Configure(WeaponData left, GripMode leftGrip, WeaponData right, GripMode rightGrip)
        {
            leftWeapon = left; this.leftGrip = leftGrip; rightWeapon = right; this.rightGrip = rightGrip;
            Changed?.Invoke();
        }
        private void OnValidate() => Changed?.Invoke();
        public bool IsValid(out string reason)
        {
            reason = null;
            if (!isActiveAndEnabled) { reason = "장착 설정이 비활성화되어 있습니다."; return false; }
            if (!ValidateHand(leftWeapon, leftGrip, "왼손", out reason) ||
                !ValidateHand(rightWeapon, rightGrip, "오른손", out reason)) return false;
            if ((leftWeapon && leftGrip == GripMode.TwoHands && rightWeapon) ||
                (rightWeapon && rightGrip == GripMode.TwoHands && leftWeapon))
            { reason = "양손 무기를 사용하려면 반대 손을 비워야 합니다."; return false; }
            return true;
        }
        private static bool ValidateHand(WeaponData weapon, GripMode grip, string hand, out string reason)
        {
            reason = null;
            if (grip != GripMode.OneHand && grip != GripMode.TwoHands)
            { reason = hand + ": 잘못된 파지 방식입니다."; return false; }
            if (!weapon) return true;
            if (!weapon.IsValid(out reason)) { reason = hand + ": " + reason; return false; }
            if ((weapon.GripRequirement == GripRequirement.OneHandOnly && grip != GripMode.OneHand) ||
                (weapon.GripRequirement == GripRequirement.TwoHandsRequired && grip != GripMode.TwoHands))
            { reason = hand + ": 무기의 파지 조건과 설정이 일치하지 않습니다."; return false; }
            return true;
        }
    }
}
