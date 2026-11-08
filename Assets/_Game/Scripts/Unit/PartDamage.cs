namespace MechaXCOM
{
    public readonly struct PartDamage
    {
        public TargetBodyPart Part { get; }
        public int Damage { get; }
        public PartDamage(TargetBodyPart part, int damage) { Part = part; Damage = damage; }
    }
}
