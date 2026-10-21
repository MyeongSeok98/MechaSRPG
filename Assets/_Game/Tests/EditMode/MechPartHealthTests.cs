using System;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class MechPartHealthTests
    {
        private GameObject root;
        private UnitHealth health;
        [SetUp] public void SetUp() { root = new GameObject("PartHealth"); health = root.AddComponent<UnitHealth>(); health.Initialize(); }
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(root);

        [TestCase(TargetBodyPart.Whole, 20)]
        [TestCase(TargetBodyPart.Torso, 20)]
        [TestCase(TargetBodyPart.Cockpit, 6)]
        [TestCase(TargetBodyPart.Head, 6)]
        [TestCase(TargetBodyPart.LeftArm, 8)]
        [TestCase(TargetBodyPart.RightArm, 8)]
        [TestCase(TargetBodyPart.LeftLeg, 10)]
        [TestCase(TargetBodyPart.RightLeg, 10)]
        public void DefaultPartHP(TargetBodyPart part, int expected)
        {
            Assert.That(health.GetMaxHP(part), Is.EqualTo(expected));
            Assert.That(health.GetCurrentHP(part), Is.EqualTo(expected));
        }

        private PartDamageResult Hit(TargetBodyPart part, int damage) => health.ApplyDamage(part, damage);
        private void Roll(Func<int> source) => health.SetDestructionRollSource(source);
        private void BreakThree() { Hit(TargetBodyPart.Head, 6); Hit(TargetBodyPart.LeftArm, 8); Hit(TargetBodyPart.RightArm, 8); }

        [TestCase(24, MechState.Destroyed)]
        [TestCase(25, MechState.Critical)]
        public void ThreeDestroyedPartsCheckOnceAtThe25PercentBoundary(int roll, MechState expected)
        {
            int checks = 0; Roll(() => { checks++; return roll; });
            Hit(TargetBodyPart.Head, 6); Hit(TargetBodyPart.LeftArm, 8);
            Assert.That(health.State, Is.EqualTo(MechState.Active));
            Assert.That(health.MajorPartsDestroyed, Is.EqualTo(2));
            Assert.That(checks, Is.Zero);
            var result = Hit(TargetBodyPart.RightArm, 8);
            Assert.That(health.State, Is.EqualTo(expected));
            Assert.That(result.StateBefore, Is.EqualTo(MechState.Active));
            Assert.That(result.StateAfter, Is.EqualTo(expected));
            Assert.That(result.DestructionChecked, Is.True);
            Assert.That(health.IsPresent, Is.EqualTo(expected == MechState.Critical));
            Assert.That(health.CanFight, Is.False);
            Assert.That(health.PilotEjected, Is.False);
            Assert.That(checks, Is.EqualTo(1));
        }

        [Test]
        public void MajorPartCountNeverDoubleCountsAndCriticalHitChecksOnce()
        {
            int checks = 0; Roll(() => { checks++; return checks == 1 ? 99 : 24; });
            Hit(TargetBodyPart.Head, 6); Hit(TargetBodyPart.Head, 5);
            Assert.That(health.MajorPartsDestroyed, Is.EqualTo(1));
            Assert.That(checks, Is.Zero);
            Hit(TargetBodyPart.LeftArm, 8); Hit(TargetBodyPart.RightArm, 8);
            var result = Hit(TargetBodyPart.Head, 5);
            Assert.That(result.Applied, Is.True);
            Assert.That(result.DamageDealt, Is.Zero);
            Assert.That(health.MajorPartsDestroyed, Is.EqualTo(3));
            Assert.That(health.State, Is.EqualTo(MechState.Destroyed));
            Assert.That(checks, Is.EqualTo(2));
        }

        [TestCase(TargetBodyPart.LeftLeg, 10)]
        [TestCase(TargetBodyPart.Torso, 20)]
        [TestCase(TargetBodyPart.Cockpit, 6)]
        public void FourthPartAndVitalDestructionSkipRandomCheck(TargetBodyPart part, int damage)
        {
            int checks = 0; Roll(() => { checks++; return 99; }); BreakThree();
            var result = Hit(part, damage);
            Assert.That(health.State, Is.EqualTo(MechState.Destroyed));
            Assert.That(result.DestructionChecked, Is.False);
            Assert.That(checks, Is.EqualTo(1));
        }

        [Test]
        public void DamageStaysOnOnePartWithoutOverflow()
        {
            var result = Hit(TargetBodyPart.Head, 100);
            Assert.That(result.DamageDealt, Is.EqualTo(6));
            Assert.That(result.PartHPAfter, Is.Zero);
            foreach (TargetBodyPart part in new[] { TargetBodyPart.Torso, TargetBodyPart.Cockpit, TargetBodyPart.LeftArm, TargetBodyPart.RightArm, TargetBodyPart.LeftLeg, TargetBodyPart.RightLeg })
                Assert.That(health.GetCurrentHP(part), Is.EqualTo(health.GetMaxHP(part)));
        }

        [Test]
        public void InvalidDamageDoesNotChangePartsStateOrRandomness()
        {
            int checks = 0; Roll(() => { checks++; return 99; }); BreakThree();
            var before = health.State;
            int events = 0; health.StateChanged += () => events++;
            foreach (var result in new[] { Hit(TargetBodyPart.Torso, 0), Hit(TargetBodyPart.Torso, -5), Hit((TargetBodyPart)999, 5) })
            {
                Assert.That(result.Applied, Is.False);
                Assert.That(result.DestructionChecked, Is.False);
            }
            Assert.That(health.State, Is.EqualTo(before));
            Assert.That(health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(events, Is.Zero);
            Assert.That(checks, Is.EqualTo(1));
            Hit(TargetBodyPart.Torso, 20);
            Assert.That(Hit(TargetBodyPart.Cockpit, 6).Applied, Is.False);
            Assert.That(events, Is.EqualTo(1));
            Assert.That(checks, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EjectionRecordsPilotAndRejectsFurtherDamage(bool critical)
        {
            Roll(() => 99); if (critical) BreakThree();
            int events = 0; health.StateChanged += () => events++;
            Assert.That(health.MarkEjected(), Is.True);
            Assert.That(health.State, Is.EqualTo(MechState.Ejected));
            Assert.That(health.PilotEjected, Is.True);
            Assert.That(health.IsPresent, Is.False);
            Assert.That(health.CanFight, Is.False);
            Assert.That(health.MarkEjected(), Is.False);
            Assert.That(Hit(TargetBodyPart.Torso, 5).Applied, Is.False);
            Assert.That(health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(events, Is.EqualTo(1));
        }

        [TestCase(24, MechState.Destroyed)]
        [TestCase(25, MechState.Critical)]
        public void DamageEmitsOnlyTheFinalStateChange(int roll, MechState expected)
        {
            Roll(() => roll);
            var seen = new System.Collections.Generic.List<MechState>();
            health.StateChanged += () => seen.Add(health.State);
            BreakThree();
            Assert.That(seen, Is.EqualTo(new[] { expected }));
            if (expected == MechState.Critical)
            {
                Hit(TargetBodyPart.Head, 5);
                Assert.That(seen.Count, Is.EqualTo(1));
                Hit(TargetBodyPart.Cockpit, 6);
                Assert.That(seen, Is.EqualTo(new[] { MechState.Critical, MechState.Destroyed }));
            }
        }

        [TestCase(TargetBodyPart.Whole, 20)]
        [TestCase(TargetBodyPart.Cockpit, 6)]
        public void VitalDestructionFromActiveDoesNotUseRandomness(TargetBodyPart part, int damage)
        {
            Roll(() => { Assert.Fail("Guaranteed vital destruction cannot roll"); return 0; });
            var result = Hit(part, damage);
            Assert.That(result.Part, Is.EqualTo(BodyTargetRules.Normalize(part)));
            Assert.That(result.StateAfter, Is.EqualTo(MechState.Destroyed));
            Assert.That(result.DestructionChecked, Is.False);
            Assert.That(health.MajorPartsDestroyed, Is.Zero);
            Assert.That(health.MarkEjected(), Is.False);
            Assert.That(health.PilotEjected, Is.False);
        }

        [TestCase(-10, 0, MechState.Critical)]
        [TestCase(110, 99, MechState.Destroyed)]
        public void ConfiguredChanceClampsToProbabilityLimits(int percent, int roll, MechState expected)
        {
            health.ConfigureDestructionChance(percent); Roll(() => roll); BreakThree();
            Assert.That(health.State, Is.EqualTo(expected));
        }

        [Test]
        public void ConfigurationClampsHPAndWholeUsesBody()
        {
            health.ConfigurePartHP(TargetBodyPart.Whole, 37);
            health.ConfigurePartHP(TargetBodyPart.Head, -5);
            health.Initialize();
            Assert.That(health.CanFight, Is.True);
            Assert.That(health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(37));
            Assert.That(health.GetCurrentHP(TargetBodyPart.Whole), Is.EqualTo(37));
            Assert.That(health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(1));
            var result = Hit(TargetBodyPart.Whole, 5);
            Assert.That(result.Part, Is.EqualTo(TargetBodyPart.Torso));
            Assert.That(health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(32));
            Assert.That(health.GetCurrentHP((TargetBodyPart)999), Is.Zero);
            Assert.That(health.GetMaxHP((TargetBodyPart)999), Is.Zero);
        }
    }
}
