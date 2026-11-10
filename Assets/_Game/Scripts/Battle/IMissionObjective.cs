using System;

namespace MechaXCOM
{
    public interface IMissionObjective : IDisposable
    {
        int TotalCount { get; }
        int DestroyedCount { get; }
        bool IsComplete { get; }
        event Action Changed;
    }
}
