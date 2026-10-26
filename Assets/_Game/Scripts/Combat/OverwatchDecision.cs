namespace MechaXCOM
{
    public readonly struct OverwatchDecision
    {
        public long Id { get; }
        public BattleUnit Watcher { get; }
        public BattleUnit Target { get; }
        public WeaponSlot Slot { get; }

        public OverwatchDecision(long id, BattleUnit watcher, BattleUnit target, WeaponSlot slot)
        { Id = id; Watcher = watcher; Target = target; Slot = slot; }
    }

    public interface IOverwatchDecisionReceiver
    {
        bool IsReady { get; }
    }
}
