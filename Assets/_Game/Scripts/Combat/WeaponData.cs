using System;
using UnityEngine;

namespace MechaXCOM
{
    public enum RangedWeaponCategory { Beam, Ballistic, Explosive }
    public enum RangedAttackMode { [InspectorName("직사 · 적 지정")] Direct = 0, [InspectorName("곡사 · 좌표 지정")] Indirect = 1 }
    public enum MeleeWeaponCategory { Edged, Blunt }
    public enum GripRequirement { OneHandOnly, TwoHandsRequired, Either }
    public enum GripMode { OneHand, TwoHands }
    [Serializable] public struct DistanceAccuracyBand
    {
        [Min(1)] public int MaxDistance;
        [Min(0)] public float Multiplier;
    }
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "MechaXCOM/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField, TextArea(3, 6)] private string description;
        [SerializeField] private AttackType attackType;
        [SerializeField] private RangedWeaponCategory rangedCategory;
        [SerializeField] private MeleeWeaponCategory meleeCategory;
        [SerializeField] private WeaponTrait[] traits = new WeaponTrait[0];
        [SerializeField, Min(1)] private int minDamage = 4, maxDamage = 6;
        [SerializeField, Range(0, 100)] private int baseHitChance = 80;
        [SerializeField, Min(1)] private int range = 7, apCost = 3;
        [SerializeField, Min(1)] private int magazineCapacity = 6;
        [SerializeField, Min(0)] private int spareMagazineCount = 2;
        [SerializeField, Min(1)] private int reloadAPCost = 3;
        [SerializeField, Min(0)] private float weightTonnes;
        [SerializeField, Min(0)] private float magazineWeightTonnes;
        [SerializeField, Min(0)] private float coverDamageMultiplier = 1f;
        [SerializeField] private GripRequirement gripRequirement = GripRequirement.Either;
        [SerializeField, Min(1)] private float twoHandHitMultiplier = 1.1f;
        [SerializeField] private DistanceAccuracyBand[] distanceBands = new DistanceAccuracyBand[0];
        [SerializeField, Range(0, 6)] private int additionalHitPartCount;
        [SerializeField, Range(0, 1)] private float additionalDamageMinRatio = .05f;
        [SerializeField, Range(0, 1)] private float additionalDamageMaxRatio = .15f;
        [SerializeField, Range(1, 100)] private int additionalDamageStepPercent = 5;
        [SerializeField] private RangedAttackMode rangedAttackMode;
        [SerializeField, Min(1)] private int blastRadius = 1;
        [SerializeField, Range(1, 6)] private int minHitParts = 1, maxHitParts = 1;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public AttackType AttackType => attackType;
        public RangedWeaponCategory RangedCategory => rangedCategory;
        public MeleeWeaponCategory MeleeCategory => meleeCategory;
        public bool HasTrait(WeaponTrait trait) => traits != null && Array.IndexOf(traits, trait) >= 0;
        public int MinDamage => minDamage;
        public int MaxDamage => maxDamage;
        public int BaseHitChance => baseHitChance;
        public int Range => range;
        public int APCost => apCost;
        public bool UsesAmmo => attackType == AttackType.Ranged;
        public int MagazineCapacity => magazineCapacity;
        public int SpareMagazineCount => spareMagazineCount;
        public int ReloadAPCost => reloadAPCost;
        public float WeightTonnes => weightTonnes;
        public float MagazineWeightTonnes => magazineWeightTonnes;
        public float CoverDamageMultiplier => coverDamageMultiplier;
        public GripRequirement GripRequirement => gripRequirement;
        public float TwoHandHitMultiplier => twoHandHitMultiplier;
        public int AdditionalHitPartCount => additionalHitPartCount;
        public float AdditionalDamageMinRatio => additionalDamageMinRatio;
        public float AdditionalDamageMaxRatio => additionalDamageMaxRatio;
        public int AdditionalDamageStepPercent => additionalDamageStepPercent;
        public bool IsIndirect => attackType == AttackType.Ranged && rangedCategory == RangedWeaponCategory.Explosive && rangedAttackMode == RangedAttackMode.Indirect;
        public int BlastRadius => blastRadius;
        public int MinHitParts => minHitParts;
        public int MaxHitParts => maxHitParts;
        public bool HasAdditionalPartDamage => attackType == AttackType.Ranged &&
            rangedCategory == RangedWeaponCategory.Explosive && !IsIndirect && additionalHitPartCount > 0;
        public bool IsValid(out string reason)
        {
            reason = null;
            if (!Enum.IsDefined(typeof(AttackType), attackType)) reason = "Invalid attack type.";
            else if ((attackType == AttackType.Ranged && !Enum.IsDefined(typeof(RangedWeaponCategory), rangedCategory)) ||
                (attackType == AttackType.Melee && !Enum.IsDefined(typeof(MeleeWeaponCategory), meleeCategory)))
                reason = "Invalid weapon category.";
            else if (!Enum.IsDefined(typeof(GripRequirement), gripRequirement)) reason = "Invalid grip requirement.";
            else if (attackType == AttackType.Ranged && (!Enum.IsDefined(typeof(RangedAttackMode), rangedAttackMode) ||
                (rangedAttackMode == RangedAttackMode.Indirect && rangedCategory != RangedWeaponCategory.Explosive)))
                reason = "곡사 공격은 원거리 폭발물 무기에서만 사용할 수 있습니다.";
            else if (IsIndirect && (blastRadius < 1 || minHitParts < 1 || maxHitParts > 6 || maxHitParts < minHitParts))
                reason = "곡사 폭발 반경은 1 이상, 피격 부위 수는 1 ≤ 최소 ≤ 최대 ≤ 6이어야 합니다.";
            else if (minDamage < 1 || maxDamage < minDamage) reason = "Damage must satisfy 1 <= minimum <= maximum.";
            else if (baseHitChance < 0 || baseHitChance > 100) reason = "Base hit chance must be between 0 and 100.";
            else if (range < 1 || apCost < 1) reason = "Range and AP cost must be at least 1.";
            else if (UsesAmmo && (magazineCapacity < 1 || spareMagazineCount < 0 || reloadAPCost < 1))
                reason = "장탄 수와 재장전 AP는 1 이상, 예비 탄창·팩 개수는 0 이상이어야 합니다.";
            else if (!Finite(weightTonnes) || weightTonnes < 0) reason = "Weight must be finite and nonnegative.";
            else if (UsesAmmo && (!Finite(magazineWeightTonnes) || magazineWeightTonnes < 0))
                reason = "탄창·팩 중량은 유한한 0 이상의 값이어야 합니다.";
            else if (!Finite(twoHandHitMultiplier) || twoHandHitMultiplier < 1) reason = "Two-hand multiplier must be finite and at least 1.";
            else if (attackType == AttackType.Ranged && rangedCategory == RangedWeaponCategory.Explosive && !IsIndirect &&
                (additionalHitPartCount < 0 || additionalHitPartCount > 6 ||
                 !Finite(additionalDamageMinRatio) || !Finite(additionalDamageMaxRatio) ||
                 additionalDamageMinRatio < 0 || additionalDamageMaxRatio > 1 || additionalDamageMinRatio > additionalDamageMaxRatio ||
                 additionalDamageStepPercent < 1 || additionalDamageStepPercent > 100))
                reason = "폭발 추가 부위 수는 0~6, 부가 피해 배율은 0 ≤ 최소 ≤ 최대 ≤ 1, 추첨 간격은 1~100%p여야 합니다.";
            else if (attackType == AttackType.Ranged && rangedCategory == RangedWeaponCategory.Explosive && !IsIndirect &&
                (!MatchesAdditionalDamageStep(additionalDamageMinRatio) || !MatchesAdditionalDamageStep(additionalDamageMaxRatio)))
                reason = "부가 피해 최소·최대 퍼센트는 추첨 간격의 배수여야 합니다. 예: 간격 5%p, 최소 0%, 최대 20%.";
            if (reason != null) return false;
            if (!Finite(coverDamageMultiplier) || coverDamageMultiplier < 0)
            { reason = "엄폐물 피해 배율은 유한한 0 이상의 값이어야 합니다."; return false; }
            if (traits != null)
                for (int i = 0; i < traits.Length; i++)
                {
                    if (!Enum.IsDefined(typeof(WeaponTrait), traits[i]))
                    { reason = "알 수 없는 무기 특성입니다."; return false; }
                    for (int j = 0; j < i; j++)
                        if (traits[j] == traits[i])
                        { reason = "같은 무기 특성을 중복 지정할 수 없습니다."; return false; }
                }
            if (attackType == AttackType.Ranged && distanceBands != null)
            {
                int previous = 0;
                foreach (var band in distanceBands)
                {
                    if (band.MaxDistance <= previous || band.MaxDistance > range || !Finite(band.Multiplier) || band.Multiplier < 0)
                    {
                        reason = "Distance bands need increasing unique limits within range and finite nonnegative multipliers.";
                        return false;
                    }
                    previous = band.MaxDistance;
                }
            }
            return true;
        }
        public float GetDistanceHitMultiplier(long distance)
        {
            if (attackType != AttackType.Ranged || distance < 1 || distanceBands == null) return 1f;
            foreach (var band in distanceBands)
                if (distance <= band.MaxDistance) return band.Multiplier;
            return 1f;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private bool MatchesAdditionalDamageStep(float ratio)
        {
            float units = ratio * 100f / additionalDamageStepPercent;
            return Mathf.Approximately(units, Mathf.Round(units));
        }
        private void OnValidate()
        {
            minDamage = Mathf.Max(1, minDamage);
            maxDamage = Mathf.Max(minDamage, maxDamage);
            baseHitChance = Mathf.Clamp(baseHitChance, 0, 100);
            range = Mathf.Max(1, range);
            apCost = Mathf.Max(1, apCost);
            magazineCapacity = Mathf.Max(1, magazineCapacity);
            spareMagazineCount = Mathf.Max(0, spareMagazineCount);
            reloadAPCost = Mathf.Max(1, reloadAPCost);
            weightTonnes = Finite(weightTonnes) ? Mathf.Max(0, weightTonnes) : 0;
            magazineWeightTonnes = Finite(magazineWeightTonnes) ? Mathf.Max(0, magazineWeightTonnes) : 0;
            coverDamageMultiplier = Finite(coverDamageMultiplier) ? Mathf.Max(0, coverDamageMultiplier) : 0;
            twoHandHitMultiplier = Finite(twoHandHitMultiplier) ? Mathf.Max(1, twoHandHitMultiplier) : 1;
            additionalHitPartCount = Mathf.Clamp(additionalHitPartCount, 0, 6);
            additionalDamageMinRatio = Finite(additionalDamageMinRatio) ? Mathf.Clamp01(additionalDamageMinRatio) : 0;
            additionalDamageMaxRatio = Finite(additionalDamageMaxRatio)
                ? Mathf.Clamp(additionalDamageMaxRatio, additionalDamageMinRatio, 1) : additionalDamageMinRatio;
            additionalDamageStepPercent = Mathf.Clamp(additionalDamageStepPercent, 1, 100);
            blastRadius = Mathf.Max(1, blastRadius);
            minHitParts = Mathf.Clamp(minHitParts, 1, 6); maxHitParts = Mathf.Clamp(maxHitParts, minHitParts, 6);
        }
    }
}
