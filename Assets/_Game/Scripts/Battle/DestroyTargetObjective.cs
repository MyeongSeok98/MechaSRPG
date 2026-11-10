using System;

namespace MechaXCOM
{
    public sealed class DestroyTargetObjective : IMissionObjective
    {
        private readonly UnitHealth unitHealth;
        private readonly CoverDurability objectHealth;
        private bool destroyed;
        public int TotalCount => 1;
        public int DestroyedCount => destroyed ? 1 : 0;
        public bool IsComplete => destroyed;
        public event Action Changed;
        internal DestroyTargetObjective(MissionTarget target)
        {
            unitHealth = target.UnitHealth; objectHealth = target.ObjectHealth;
            if (unitHealth) unitHealth.StateChanged += RecordDestroyed;
            if (objectHealth) objectHealth.Destroyed += RecordDestroyed;
            RecordDestroyed();
        }
        private void RecordDestroyed()
        {
            if (destroyed || !((unitHealth && unitHealth.State == MechState.Destroyed) || (objectHealth && objectHealth.IsDestroyed))) return;
            destroyed = true; Changed?.Invoke();
        }
        public void Dispose()
        {
            if (unitHealth) unitHealth.StateChanged -= RecordDestroyed;
            if (objectHealth) objectHealth.Destroyed -= RecordDestroyed;
        }
    }
}
