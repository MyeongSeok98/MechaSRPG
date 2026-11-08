namespace MechaXCOM
{
    public struct AttackPreview
    {
        public AttackType Type;
        public TargetBodyPart TargetPart;
        public int PartHP;
        public int PartMaxHP;
        public MechState TargetState;
        public bool CanAttack;
        public bool BlockedByBuilding;
        public string FailureReason;
        public float Distance;
        public CoverDirection Direction;
        public CoverType Cover;
        public float CoverMultiplier;
        public float PartMultiplier;
        public float HeadMultiplier;
        public int HitChance;
        public float CriticalChance;
        public float CriticalDamageMultiplier;
        public ArmorType TargetArmorType;
        public float ArmorMultiplier;
        public WeaponSlot WeaponSlot;
        public string WeaponName;
        public int MinDamage, MaxDamage;
        public float GripMultiplier, DistanceMultiplier;
        public float ReactionMultiplier;
        public float MechaMultiplier;
        public float PilotHitMultiplier, PilotDamageMultiplier;
        public int APCost;
        public bool UsesAmmo;
        public bool UsesEnergyPack;
        public int AmmoLoaded, MagazineCapacity, SpareMagazines;
        public int AdditionalHitPartCount;
        public float AdditionalDamageMinRatio, AdditionalDamageMaxRatio;
        public int AdditionalDamageStepPercent;
    }
}
