namespace MechaXCOM
{
    public struct AttackResult
    {
        public bool WasExecuted;
        public string FailureReason;
        public bool Hit;
        public int HitChance;
        public int DamageDealt;
        public TargetBodyPart TargetPart;
        public int TargetPartHPAfter;
        public MechState TargetStateAfter;
        public bool DestructionChecked;
        public bool TargetDestroyed => TargetStateAfter == MechState.Destroyed;
    }
}
