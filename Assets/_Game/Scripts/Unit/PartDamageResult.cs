namespace MechaXCOM
{
    public struct PartDamageResult
    {
        public bool Applied;
        public TargetBodyPart Part;
        public int DamageDealt;
        public int PartHPAfter;
        public MechState StateBefore;
        public MechState StateAfter;
        public bool DestructionChecked;
    }
}
