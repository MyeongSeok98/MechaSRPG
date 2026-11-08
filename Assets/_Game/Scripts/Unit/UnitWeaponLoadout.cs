using UnityEngine;

namespace MechaXCOM
{
    public enum WeaponSlot { RightHand = 0, LeftHand = 1, Backpack = 2 }
    [DisallowMultipleComponent]
    public class UnitWeaponLoadout : MonoBehaviour
    {
        [SerializeField] private WeaponData leftWeapon, rightWeapon;
        [SerializeField] private GripMode leftGrip, rightGrip;
        // -1 preserves existing authored weapons' recommended counts until battle starts.
        [SerializeField, Min(-1)] private int leftSpareMagazineCount = -1, rightSpareMagazineCount = -1;
        [SerializeField, InspectorName("백팩")] private BackpackData backpack;
        public WeaponData LeftWeapon => leftWeapon;
        public WeaponData RightWeapon => rightWeapon;
        public GripMode LeftGrip => leftGrip;
        public GripMode RightGrip => rightGrip;
        public BackpackData Backpack => backpack;
        public int LeftSpareMagazineSelection => leftSpareMagazineCount;
        public int RightSpareMagazineSelection => rightSpareMagazineCount;
        public float TotalEquipmentWeightTonnes => EquipmentWeight(leftWeapon, rightWeapon, backpack,
            leftSpareMagazineCount, rightSpareMagazineCount);
        public float EquipmentWeightLimitTonnes
        {
            get
            {
                var unit = GetComponent<BattleUnit>();
                return unit && unit.MechaData ? unit.MechaData.EquipmentWeightLimitTonnes : 0f;
            }
        }
        private static int SpareCount(WeaponData weapon, int selection) =>
            weapon && weapon.UsesAmmo ? (selection == -1 ? weapon.SpareMagazineCount : selection) : 0;
        private static double WeaponWeight(WeaponData weapon, int selection)
        {
            if (!weapon) return 0;
            if (float.IsNaN(weapon.WeightTonnes) || weapon.WeightTonnes < 0) return double.NaN;
            if (!weapon.UsesAmmo) return weapon.WeightTonnes;
            float magazine = weapon.MagazineWeightTonnes;
            int spares = SpareCount(weapon, selection);
            if (spares < 0 || float.IsNaN(magazine) || magazine < 0) return double.NaN;
            return weapon.WeightTonnes + (1L + spares) * (double)magazine;
        }
        private static float EquipmentWeight(WeaponData left, WeaponData right, BackpackData pack, int leftCount, int rightCount) =>
            (float)(WeaponWeight(left, leftCount) + WeaponWeight(right, rightCount) +
            (pack ? pack.WeightTonnes + WeaponWeight(pack.Weapon, 0) : 0));
        private int SelectionFor(WeaponData weapon, bool left, WeaponData other)
        {
            if (!weapon) return -1;
            if (left && weapon == leftWeapon) return leftSpareMagazineCount;
            if (!left && weapon == rightWeapon) return rightSpareMagazineCount;
            // A transfer keeps its initial reserves; a second copy gets its own defaults.
            if (left && weapon == rightWeapon && other != rightWeapon) return rightSpareMagazineCount;
            if (!left && weapon == leftWeapon && other != leftWeapon) return leftSpareMagazineCount;
            return -1;
        }
        public bool CanEquip(WeaponData left, WeaponData right, BackpackData pack, out string reason)
            => CanEquip(left, right, pack, SelectionFor(left, true, right), SelectionFor(right, false, left), out reason);
        private bool CanEquip(WeaponData left, WeaponData right, BackpackData pack, int leftCount, int rightCount, out string reason)
        {
            float weight = EquipmentWeight(left, right, pack, leftCount, rightCount), limit = EquipmentWeightLimitTonnes;
            reason = null;
            if (leftCount < -1 || rightCount < -1) reason = "예비 개수는 0 이상 또는 -1 (무기 기본값)이어야 합니다.";
            else if (float.IsNaN(weight) || float.IsInfinity(weight) || weight < 0)
                reason = "장착 중량이 유효하지 않습니다.";
            // Summed decimal weights can exceed an equal authored limit by float rounding.
            else if (limit > 0 && weight > limit && !Mathf.Approximately(weight, limit))
                reason = $"장착 중량 초과: {weight:0.###}톤 / 한도 {limit:0.###}톤. 장착 및 출격할 수 없습니다.";
            return reason == null;
        }
        public bool IsWithinWeightLimit(out string reason) => CanEquip(leftWeapon, rightWeapon, backpack,
            leftSpareMagazineCount, rightSpareMagazineCount, out reason);
        public bool CanChangeEquipment(WeaponData left, WeaponData right, BackpackData pack, out string reason)
            => CanChangeEquipment(left, right, pack, SelectionFor(left, true, right), SelectionFor(right, false, left), out reason);
        public bool CanChangeEquipment(WeaponData left, WeaponData right, BackpackData pack, int leftCount, int rightCount, out string reason)
        {
            if (leftCount < -1 || rightCount < -1)
            { reason = "예비 개수는 0 이상 또는 -1 (무기 기본값)이어야 합니다."; return false; }
            if (CanEquip(left, right, pack, leftCount, rightCount, out reason)) return true;
            // Authored data may become overweight after a balance edit. Allow removal only
            // so the player can repair it in stages, while deployment stays blocked.
            if ((!left || left == leftWeapon) && (!right || right == rightWeapon) && (!pack || pack == backpack) &&
                SpareCount(left, leftCount) <= GetSpareMagazineCount(WeaponSlot.LeftHand) &&
                SpareCount(right, rightCount) <= GetSpareMagazineCount(WeaponSlot.RightHand) &&
                (left != leftWeapon || right != rightWeapon || pack != backpack ||
                 SpareCount(left, leftCount) < GetSpareMagazineCount(WeaponSlot.LeftHand) ||
                 SpareCount(right, rightCount) < GetSpareMagazineCount(WeaponSlot.RightHand)))
            { reason = null; return true; }
            return false;
        }
        public int GetSpareMagazineCount(WeaponSlot slot) => slot == WeaponSlot.Backpack ? 0 :
            SpareCount(GetWeapon(slot), slot == WeaponSlot.LeftHand ? leftSpareMagazineCount : rightSpareMagazineCount);
        internal int CaptureSpareMagazineCount(WeaponSlot slot)
        {
            int count = GetSpareMagazineCount(slot);
            if (slot == WeaponSlot.LeftHand) leftSpareMagazineCount = count;
            else if (slot == WeaponSlot.RightHand) rightSpareMagazineCount = count;
            return count;
        }
        public bool TryConfigureSpareMagazines(int leftCount, int rightCount)
        {
            var unit = GetComponent<BattleUnit>();
            if (unit && unit.IsInitialized) return false;
            if (!CanChangeEquipment(leftWeapon, rightWeapon, backpack, leftCount, rightCount, out _)) return false;
            if (leftSpareMagazineCount == leftCount && rightSpareMagazineCount == rightCount) return true;
            leftSpareMagazineCount = leftCount; rightSpareMagazineCount = rightCount;
            Changed?.Invoke(); return true;
        }
        public WeaponData GetWeapon(WeaponSlot slot)
        {
            switch (slot)
            {
                case WeaponSlot.LeftHand: return leftWeapon;
                case WeaponSlot.RightHand: return rightWeapon;
                case WeaponSlot.Backpack: return backpack ? backpack.Weapon : null;
                default: return null;
            }
        }
        public bool TryConfigureBackpack(BackpackData value)
        {
            var unit = GetComponent<BattleUnit>();
            if ((unit && unit.IsInitialized) || (value && !value.IsValid(out _))) return false;
            if (!CanChangeEquipment(leftWeapon, rightWeapon, value, out _)) return false;
            if (backpack == value) return true;
            backpack = value;
            Changed?.Invoke();
            return true;
        }
        public event System.Action Changed;
        public void Configure(WeaponData left, GripMode leftGrip, WeaponData right, GripMode rightGrip)
            => TryConfigure(left, leftGrip, right, rightGrip);
        public bool TryConfigure(WeaponData left, GripMode leftGrip, WeaponData right, GripMode rightGrip)
            => ApplyHands(left, leftGrip, right, rightGrip, SelectionFor(left, true, right), SelectionFor(right, false, left));
        // Pre-battle atomic selection: a new weapon may fit only with fewer reserves
        // than its recommendation, so validating/equipping it first is insufficient.
        public bool TryConfigure(WeaponData left, GripMode leftGrip, WeaponData right, GripMode rightGrip,
            int leftSpareMagazines, int rightSpareMagazines)
        {
            var unit = GetComponent<BattleUnit>();
            if (unit && unit.IsInitialized) return false;
            return ApplyHands(left, leftGrip, right, rightGrip, leftSpareMagazines, rightSpareMagazines);
        }
        private bool ApplyHands(WeaponData left, GripMode leftGrip, WeaponData right, GripMode rightGrip, int leftCount, int rightCount)
        {
            if (!CanChangeEquipment(left, right, backpack, leftCount, rightCount, out _)) return false;
            leftWeapon = left; this.leftGrip = leftGrip; rightWeapon = right; this.rightGrip = rightGrip;
            leftSpareMagazineCount = leftCount; rightSpareMagazineCount = rightCount;
            var unit = GetComponent<BattleUnit>();
            if (unit && unit.IsInitialized) AdjustGripForArmDamage(unit.Health);
            Changed?.Invoke();
            return true;
        }
        internal void ResolveArmDamage(UnitHealth health)
        {
            if (AdjustGripForArmDamage(health)) Changed?.Invoke();
        }
        private bool AdjustGripForArmDamage(UnitHealth health)
        {
            if (!health || !health.IsPresent || (leftWeapon && rightWeapon)) return false;
            bool leftDestroyed = health.GetCurrentHP(TargetBodyPart.LeftArm) == 0;
            bool rightDestroyed = health.GetCurrentHP(TargetBodyPart.RightArm) == 0;
            if (leftDestroyed == rightDestroyed) return false;
            var weapon = leftWeapon && leftGrip == GripMode.TwoHands ? leftWeapon :
                rightWeapon && rightGrip == GripMode.TwoHands ? rightWeapon : null;
            if (!weapon || weapon.GripRequirement != GripRequirement.Either) return false;
            int count = leftWeapon ? leftSpareMagazineCount : rightSpareMagazineCount;
            // Runtime hand transfer only. Shared WeaponData and authored loadouts stay unchanged.
            leftWeapon = leftDestroyed ? null : weapon;
            rightWeapon = rightDestroyed ? null : weapon;
            leftSpareMagazineCount = leftWeapon ? count : -1;
            rightSpareMagazineCount = rightWeapon ? count : -1;
            leftGrip = rightGrip = GripMode.OneHand;
            return true;
        }
        private void OnValidate() => Changed?.Invoke();
        public bool IsValid(out string reason)
        {
            reason = null;
            if (!isActiveAndEnabled) { reason = "장착 설정이 비활성화되어 있습니다."; return false; }
            if (!IsWithinWeightLimit(out reason)) return false;
            if (backpack && !backpack.IsValid(out reason)) { reason = "백팩: " + reason; return false; }
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
