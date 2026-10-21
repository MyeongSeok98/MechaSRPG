namespace MechaXCOM
{
    /// <summary>공격 시점의 패시브 계약. 조건 확인과 효과 반환은 전투 상태를 변경하지 않는다.</summary>
    public interface IPilotAttackPassive
    {
        string Id { get; }
        bool CanApply(PilotAttackContext context);
        PilotAttackModifiers GetModifiers(PilotAttackContext context);
    }
}
