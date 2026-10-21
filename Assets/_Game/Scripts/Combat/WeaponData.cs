using System;
using UnityEngine;

namespace MechaXCOM
{
    public enum RangedWeaponCategory { Beam, Ballistic, Explosive }
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
        [SerializeField, Min(1)] private int minDamage = 4, maxDamage = 6;
        [SerializeField, Range(0, 100)] private int baseHitChance = 80;
        [SerializeField, Min(1)] private int range = 7, apCost = 3;
        [SerializeField, Min(0)] private float weightTonnes;
        [SerializeField] private GripRequirement gripRequirement = GripRequirement.Either;
        [SerializeField, Min(1)] private float twoHandHitMultiplier = 1.1f;
        [SerializeField] private DistanceAccuracyBand[] distanceBands = new DistanceAccuracyBand[0];
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public AttackType AttackType => attackType;
        public RangedWeaponCategory RangedCategory => rangedCategory;
        public MeleeWeaponCategory MeleeCategory => meleeCategory;
        public int MinDamage => minDamage;
        public int MaxDamage => maxDamage;
        public int BaseHitChance => baseHitChance;
        public int Range => range;
        public int APCost => apCost;
        public float WeightTonnes => weightTonnes;
        public GripRequirement GripRequirement => gripRequirement;
        public float TwoHandHitMultiplier => twoHandHitMultiplier;
        public bool IsValid(out string reason)
        {
            reason = null;
            if (!Enum.IsDefined(typeof(AttackType), attackType)) reason = "Invalid attack type.";
            else if ((attackType == AttackType.Ranged && !Enum.IsDefined(typeof(RangedWeaponCategory), rangedCategory)) ||
                (attackType == AttackType.Melee && !Enum.IsDefined(typeof(MeleeWeaponCategory), meleeCategory)))
                reason = "Invalid weapon category.";
            else if (!Enum.IsDefined(typeof(GripRequirement), gripRequirement)) reason = "Invalid grip requirement.";
            else if (minDamage < 1 || maxDamage < minDamage) reason = "Damage must satisfy 1 <= minimum <= maximum.";
            else if (baseHitChance < 0 || baseHitChance > 100) reason = "Base hit chance must be between 0 and 100.";
            else if (range < 1 || apCost < 1) reason = "Range and AP cost must be at least 1.";
            else if (!Finite(weightTonnes) || weightTonnes < 0) reason = "Weight must be finite and nonnegative.";
            else if (!Finite(twoHandHitMultiplier) || twoHandHitMultiplier < 1) reason = "Two-hand multiplier must be finite and at least 1.";
            if (reason != null) return false;
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
        private void OnValidate()
        {
            minDamage = Mathf.Max(1, minDamage);
            maxDamage = Mathf.Max(minDamage, maxDamage);
            baseHitChance = Mathf.Clamp(baseHitChance, 0, 100);
            range = Mathf.Max(1, range);
            apCost = Mathf.Max(1, apCost);
            weightTonnes = Finite(weightTonnes) ? Mathf.Max(0, weightTonnes) : 0;
            twoHandHitMultiplier = Finite(twoHandHitMultiplier) ? Mathf.Max(1, twoHandHitMultiplier) : 1;
        }
    }
}
