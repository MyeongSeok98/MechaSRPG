using System;

namespace MechaXCOM
{
    // Per-battle progress, independent of shared MissionData and later Inspector list edits.
    public sealed class EliminationObjective : IMissionObjective
    {
        private readonly UnitHealth[] targets;
        private readonly Action[] handlers;
        private readonly bool[] destroyed;
        public int TotalCount => targets.Length;
        public int DestroyedCount { get; private set; }
        public bool IsComplete => TotalCount > 0 && DestroyedCount == TotalCount;
        public event Action Changed;
        internal EliminationObjective(BattleUnit[] units)
        {
            targets = new UnitHealth[units.Length]; handlers = new Action[units.Length]; destroyed = new bool[units.Length];
            for (int i = 0; i < units.Length; i++)
            {
                int index = i;
                targets[i] = units[i].Health;
                handlers[i] = () => RecordDestroyed(index);
                targets[i].StateChanged += handlers[i];
                RecordDestroyed(i);
            }
        }
        private void RecordDestroyed(int index)
        {
            if (destroyed[index] || !targets[index] || targets[index].State != MechState.Destroyed) return;
            destroyed[index] = true; DestroyedCount++;
            Changed?.Invoke();
        }
        public void Dispose()
        {
            for (int i = 0; i < targets.Length; i++)
                if (targets[i]) targets[i].StateChanged -= handlers[i];
        }
    }
}
