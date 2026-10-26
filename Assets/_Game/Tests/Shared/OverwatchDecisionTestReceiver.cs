namespace MechaXCOM.Tests
{
    // Test-owned receiver: tests explicitly choose Fire or Skip; this never auto-fires.
    public sealed class OverwatchDecisionTestReceiver : IOverwatchDecisionReceiver
    {
        public bool IsReady { get; set; } = true;
    }
}
