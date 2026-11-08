namespace MechaXCOM
{
    public enum TargetBodyPart { Whole, Torso, Cockpit, Head, LeftArm, RightArm, LeftLeg, RightLeg }

    public static class BodyTargetRules
    {
        public static TargetBodyPart Normalize(TargetBodyPart part) =>
            part == TargetBodyPart.Whole ? TargetBodyPart.Torso : part;

        public static float HitMultiplier(TargetBodyPart part)
        {
            switch (part)
            {
                case TargetBodyPart.Cockpit: return .45f;
                case TargetBodyPart.Head: return .8f;
                case TargetBodyPart.LeftArm:
                case TargetBodyPart.RightArm:
                case TargetBodyPart.LeftLeg:
                case TargetBodyPart.RightLeg: return .9f;
                default: return 1f;
            }
        }
        public static string Label(TargetBodyPart part)
        {
            switch (part)
            {
                case TargetBodyPart.Torso: return "Body";
                case TargetBodyPart.Cockpit: return "C.Pit";
                case TargetBodyPart.Head: return "Head";
                case TargetBodyPart.LeftArm: return "L.Arm";
                case TargetBodyPart.RightArm: return "R.arm";
                case TargetBodyPart.LeftLeg: return "L.Leg";
                case TargetBodyPart.RightLeg: return "R.Leg";
                default: return "Body";
            }
        }
    }
}
