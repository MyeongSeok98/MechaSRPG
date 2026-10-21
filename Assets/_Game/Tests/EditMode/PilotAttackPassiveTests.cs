using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class PilotAttackPassiveTests
    {
        private WeaponData weapon;
        [SetUp] public void Setup() => weapon = ScriptableObject.CreateInstance<WeaponData>();
        [TearDown] public void Cleanup() => Object.DestroyImmediate(weapon);

        // 실제 계약을 사용하는 예시. 게임에 등록하는 패시브는 아니다.
        private sealed class StationaryRangedPassive : IPilotAttackPassive
        {
            public string Id { get; }
            public StationaryRangedPassive(string id) => Id = id;
            public bool CanApply(PilotAttackContext context) =>
                context.AttackType == AttackType.Ranged && context.MovedCells == 0;
            public PilotAttackModifiers GetModifiers(PilotAttackContext context) => new PilotAttackModifiers(1, 1.1f);
        }

        [Test]
        public void MatchingPassivesReturnSeparateEffectsInInputOrderWithoutChangingWeapon()
        {
            string before = JsonUtility.ToJson(weapon);
            var result = PilotAttackPassiveEvaluator.Evaluate(new IPilotAttackPassive[] {
                new StationaryRangedPassive("steady"), null, new StationaryRangedPassive("training") },
                new PilotAttackContext(weapon, 0));
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].PassiveId, Is.EqualTo("steady"));
            Assert.That(result[1].PassiveId, Is.EqualTo("training"));
            Assert.That(result[0].Modifiers.AttackAPReduction, Is.EqualTo(1));
            Assert.That(result[1].Modifiers.AccuracyMultiplier, Is.EqualTo(1.1f));
            Assert.That(JsonUtility.ToJson(weapon), Is.EqualTo(before));
        }

        [TestCase(0, 1)] [TestCase(1, 0)] [TestCase(4, 0)]
        public void StationaryConditionUsesProvidedMovement(int moved, int count)
        {
            var result = PilotAttackPassiveEvaluator.Evaluate(new[] { new StationaryRangedPassive("steady") },
                new PilotAttackContext(weapon, moved));
            Assert.That(result.Count, Is.EqualTo(count));
        }

        [Test]
        public void RangedPassiveDoesNotApplyToMelee()
        {
            JsonUtility.FromJsonOverwrite("{\"attackType\":1}", weapon);
            Assert.That(PilotAttackPassiveEvaluator.Evaluate(new[] { new StationaryRangedPassive("steady") },
                new PilotAttackContext(weapon, 0)), Is.Empty);
        }

        [Test]
        public void NoPassivesReturnNoEffects()
        {
            var context = new PilotAttackContext(weapon, 0);
            Assert.That(PilotAttackPassiveEvaluator.Evaluate(null, context), Is.Empty);
            Assert.That(PilotAttackPassiveEvaluator.Evaluate(new IPilotAttackPassive[] { null }, context), Is.Empty);
        }

        [TestCase(null)] [TestCase("")] [TestCase(" ")]
        public void MissingIdsAreConfigurationErrors(string id) => Assert.Throws<ArgumentException>(() =>
            PilotAttackPassiveEvaluator.Evaluate(new[] { new StationaryRangedPassive(id) }, new PilotAttackContext(weapon, 1)));

        [Test]
        public void DuplicateIdsAreRejectedEvenWhenNotApplicable() => Assert.Throws<ArgumentException>(() =>
            PilotAttackPassiveEvaluator.Evaluate(new[] { new StationaryRangedPassive("same"), new StationaryRangedPassive("same") },
                new PilotAttackContext(weapon, 1)));

        [Test]
        public void MissingContextIsRejected() => Assert.Throws<ArgumentNullException>(() =>
            PilotAttackPassiveEvaluator.Evaluate(null, null));

        [Test]
        public void DefaultModifiersAreNeutral()
        {
            var modifiers = default(PilotAttackModifiers);
            Assert.That(modifiers.AttackAPReduction, Is.Zero);
            Assert.That(modifiers.AccuracyMultiplier, Is.EqualTo(1f));
        }

        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidMultipliersAreRejected(float value) => Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PilotAttackModifiers(0, value));

        [Test] public void NegativeAPReductionIsRejected() => Assert.Throws<ArgumentOutOfRangeException>(() => new PilotAttackModifiers(-1));
        [Test] public void MissingWeaponIsRejected() => Assert.Throws<ArgumentNullException>(() => new PilotAttackContext(null, 0));
        [Test] public void NegativeMovementIsRejected() => Assert.Throws<ArgumentOutOfRangeException>(() => new PilotAttackContext(weapon, -1));
        [Test] public void NegativeDistanceIsRejected() => Assert.Throws<ArgumentOutOfRangeException>(() => new PilotAttackContext(weapon, 0, -1));
        [Test] public void InvalidPartIsRejected() => Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PilotAttackContext(weapon, 0, bodyPart: (TargetBodyPart)999));
        [Test] public void InvalidGripIsRejected() => Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PilotAttackContext(weapon, 0, grip: (GripMode)999));
    }
}
