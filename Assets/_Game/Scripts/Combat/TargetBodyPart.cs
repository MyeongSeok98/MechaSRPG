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
                case TargetBodyPart.Torso: return "몸통";
                case TargetBodyPart.Cockpit: return "콕피트";
                case TargetBodyPart.Head: return "머리";
                case TargetBodyPart.LeftArm: return "왼팔";
                case TargetBodyPart.RightArm: return "오른팔";
                case TargetBodyPart.LeftLeg: return "왼다리";
                case TargetBodyPart.RightLeg: return "오른다리";
                default: return "전체";
            }
        }
    }
}
