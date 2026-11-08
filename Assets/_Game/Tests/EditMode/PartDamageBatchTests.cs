using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class PartDamageBatchTests
    {
        private GameObject root;
        private UnitHealth health;
        [SetUp] public void Setup() { root = new GameObject("Batch damage test"); health = root.AddComponent<UnitHealth>(); health.Initialize(); }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);
        [Test] public void VitalDestructionDoesNotDiscardOtherPartDamageAndCallbacksSeeFinalHP()
        {
            int states = 0, parts = 0;
            health.StateChanged += () => { states++; Assert.That(health.GetCurrentHP(TargetBodyPart.LeftArm), Is.EqualTo(5)); };
            health.PartDestroyed += _ => { parts++; Assert.That(health.State, Is.EqualTo(MechState.Destroyed)); };
            var result = ExplosiveTestSupport.Batch(health, (TargetBodyPart.Torso, 800), (TargetBodyPart.LeftArm, 3));
            Assert.That(result[0].DamageDealt, Is.EqualTo(20)); Assert.That(result[1].DamageDealt, Is.EqualTo(3));
            Assert.That(result[1].StateAfter, Is.EqualTo(MechState.Destroyed)); Assert.That(states, Is.EqualTo(1)); Assert.That(parts, Is.EqualTo(1));
        }
        [Test] public void FourDestroyedPartsUseFinalGuaranteedDestructionWithoutIntermediateCriticalRoll()
        {
            health.ApplyDamage(TargetBodyPart.Head, 6); health.ApplyDamage(TargetBodyPart.LeftArm, 8);
            health.SetDestructionRollSource(() => { Assert.Fail("Four parts must not roll a prior intermediate Critical state."); return 99; });
            var result = ExplosiveTestSupport.Batch(health, (TargetBodyPart.RightArm, 8), (TargetBodyPart.LeftLeg, 10));
            Assert.That(health.State, Is.EqualTo(MechState.Destroyed)); Assert.That(health.MajorPartsDestroyed, Is.EqualTo(4));
            Assert.That(result[0].DestructionChecked || result[1].DestructionChecked, Is.False);
        }
        [Test] public void EnteringCriticalAndFurtherMultiPartHitEachRollOnlyOnce()
        {
            health.ApplyDamage(TargetBodyPart.Head, 6); health.ApplyDamage(TargetBodyPart.LeftArm, 8);
            int rolls = 0; health.SetDestructionRollSource(() => { rolls++; return 99; });
            var result = ExplosiveTestSupport.Batch(health, (TargetBodyPart.RightArm, 8), (TargetBodyPart.Torso, 1));
            Assert.That(health.State, Is.EqualTo(MechState.Critical)); Assert.That(rolls, Is.EqualTo(1)); Assert.That(result[0].DestructionChecked, Is.True);
            ExplosiveTestSupport.Batch(health, (TargetBodyPart.Torso, 1), (TargetBodyPart.RightLeg, 1));
            Assert.That(health.State, Is.EqualTo(MechState.Critical)); Assert.That(rolls, Is.EqualTo(2));
        }
        [Test] public void DestructionCallbacksObserveAllFinalHPAndDuplicatePartsNotifyOnce()
        {
            int events = 0;
            health.PartDestroyed += _ => { events++; Assert.That(health.GetCurrentHP(TargetBodyPart.Head), Is.Zero); Assert.That(health.GetCurrentHP(TargetBodyPart.LeftArm), Is.Zero); };
            ExplosiveTestSupport.Batch(health, (TargetBodyPart.Head, 6), (TargetBodyPart.LeftArm, 4), (TargetBodyPart.LeftArm, 4));
            Assert.That(events, Is.EqualTo(2)); Assert.That(health.MajorPartsDestroyed, Is.EqualTo(2));
        }
        [Test] public void EmptyInvalidAndZeroDamageDoNotMutateOrRoll()
        {
            health.SetDestructionRollSource(() => { Assert.Fail("Invalid damage cannot roll."); return 0; });
            Assert.That(ExplosiveTestSupport.Batch(health), Is.Empty);
            var result = ExplosiveTestSupport.Batch(health, ((TargetBodyPart)99, 5), (TargetBodyPart.Torso, 0), (TargetBodyPart.Head, -1));
            foreach (var item in result) Assert.That(item.Applied, Is.False);
            Assert.That(health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }
        [Test] public void EjectedUnitCannotReceiveBatchDamage()
        {
            Assert.That(health.MarkEjected(), Is.True);
            var result = ExplosiveTestSupport.Batch(health, (TargetBodyPart.Torso, 10), (TargetBodyPart.Head, 5));
            foreach (var item in result) Assert.That(item.Applied, Is.False);
            Assert.That(health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }
    }
}
