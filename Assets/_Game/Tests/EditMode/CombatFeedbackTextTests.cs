using System.Reflection;
using NUnit.Framework;

namespace MechaXCOM.Tests
{
    public class CombatFeedbackTextTests
    {
        [TestCase(TargetBodyPart.Torso, "Body")]
        [TestCase(TargetBodyPart.Head, "Head")]
        [TestCase(TargetBodyPart.Cockpit, "C.Pit")]
        [TestCase(TargetBodyPart.LeftArm, "L.Arm")]
        [TestCase(TargetBodyPart.RightArm, "R.arm")]
        [TestCase(TargetBodyPart.LeftLeg, "L.Leg")]
        [TestCase(TargetBodyPart.RightLeg, "R.Leg")]
        public void PartNamesAreEnglish(TargetBodyPart part, string label) => Assert.That(BodyTargetRules.Label(part), Is.EqualTo(label));

        private string Text(AttackResult result)
        {
            var type = typeof(CombatSystem).Assembly.GetType("MechaXCOM.CombatFeedbackText");
            Assert.That(type, Is.Not.Null, "A common result formatter is required.");
            return (string)type.GetMethod("ForAttack", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { result });
        }
        [Test] public void MissDoesNotListDamageOrDestroyedParts()
        {
            Assert.That(Text(new AttackResult { WasExecuted = true, Hit = false, TargetPart = TargetBodyPart.Torso }), Is.EqualTo("MISS"));
        }
        [Test] public void CriticalMultiPartAttackShowsPartNamesAndDamageWithoutHitOrDestructionText()
        {
            string text = Text(new AttackResult { WasExecuted = true, Hit = true, IsCritical = true,
                TargetPart = TargetBodyPart.Torso, DamageDealt = 20, TargetPartHPAfter = 500,
                AdditionalPartDamage = new[] { new PartDamageResult { Part = TargetBodyPart.LeftArm, DamageDealt = 3, PartHPAfter = 0 } } });
            Assert.That(text, Is.EqualTo("CRITICAL!\nBody -20\nL.Arm -3"));
        }
        [Test] public void ZeroDamageDoesNotAnnouncePartDestruction()
        {
            string text = Text(new AttackResult { WasExecuted = true, Hit = true, TargetPart = TargetBodyPart.Head,
                DamageDealt = 0, TargetPartHPAfter = 0 });
            Assert.That(text, Is.EqualTo("Head -0"));
        }
        [Test] public void RejectedAttackHasNoFeedback() => Assert.That(Text(default), Is.Empty);
    }
}
