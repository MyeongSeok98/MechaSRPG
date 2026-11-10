namespace MechaXCOM
{
    public enum MissionOutcome { InProgress, Success, Failure }

    public readonly struct MissionResult
    {
        public MissionOutcome Outcome { get; }
        public string Reason { get; }
        public MissionResult(MissionOutcome outcome, string reason)
        { Outcome = outcome; Reason = reason ?? string.Empty; }
    }
}
