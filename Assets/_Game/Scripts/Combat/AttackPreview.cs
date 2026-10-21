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
        public WeaponSlot WeaponSlot;
        public string WeaponName;
        public int MinDamage, MaxDamage;
        public float GripMultiplier, DistanceMultiplier;
        public float ReactionMultiplier;
        public int APCost;
    }
}
