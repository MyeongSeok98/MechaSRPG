using NUnit.Framework;

namespace MechaXCOM.Tests
{
    public class PilotPassiveContractTests
    {
        [Test]
        public void AttackPassivesHaveAnAttackSpecificContract()
        {
            var assembly = typeof(BattleUnit).Assembly;
            Assert.That(assembly.GetType("MechaXCOM.IPilotAttackPassive"), Is.Not.Null,
                "Attack passives need a dedicated contract, separate from future turn or damage events.");
            Assert.That(assembly.GetType("MechaXCOM.PilotAttackContext"), Is.Not.Null);
        }
    }
}
