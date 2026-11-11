using System;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class CommandWheelAnimationTests
    {
        private object State(float seconds)
        {
            var type = typeof(BattleUnit).Assembly.GetType("MechaXCOM.CommandWheelAnimation");
            Assert.That(type, Is.Not.Null, "A display-only timeline must drive the wheel.");
            return Activator.CreateInstance(type, seconds);
        }
        private float Value(object state, string property)
        {
            var member = state.GetType().GetProperty(property); Assert.That(member, Is.Not.Null, property);
            return (float)member.GetValue(state);
        }
        private bool Complete(object state)
        {
            var member = state.GetType().GetProperty("IsComplete"); Assert.That(member, Is.Not.Null);
            return (bool)member.GetValue(state);
        }
        private float Channel(object state, int index)
        {
            var method = state.GetType().GetMethod("ChannelLockProgress"); Assert.That(method, Is.Not.Null);
            return (float)method.Invoke(state, new object[] { index });
        }
        [Test] public void AcquisitionDetailsStartBeforeActionChannelLocking()
        {
            var state = State(.2f);
            Assert.That(Value(state, "AlignmentProgress"), Is.GreaterThan(0));
            Assert.That(Value(state, "ScanOpacity"), Is.GreaterThan(0));
            Assert.That(Channel(state, 0), Is.Zero); Assert.That(Complete(state), Is.False);
        }
        [Test] public void ActionChannelsVerifyInOrderAfterTheLines()
        {
            var state = State(.9f);
            Assert.That(Channel(state, 0), Is.GreaterThan(0));
            Assert.That(Channel(state, 3), Is.Zero);
            Assert.That(Complete(state), Is.False);
        }
        [Test] public void FinalHudLockFinishesAfterWordsAndThenSettles()
        {
            var words = State(1f); Assert.That(Characters(words, 16), Is.EqualTo(16)); Assert.That(Complete(words), Is.False);
            var settled = State(1.5f);
            Assert.That(Complete(settled), Is.True); Assert.That(Value(settled, "SectorRotation"), Is.Zero);
            Assert.That(Value(settled, "FinalLockProgress"), Is.EqualTo(1));
            for (int i = 0; i < 4; i++) Assert.That(Channel(settled, i), Is.EqualTo(1));
        }
        private int Characters(object state, int count) => (int)state.GetType().GetMethod("CharacterCount").Invoke(state, new object[] { count });
        [Test] public void RingAppearsBeforeConnectionsAndWords()
        {
            var state = State(.2f);
            Assert.That(Value(state, "RingProgress"), Is.GreaterThan(0));
            Assert.That(Value(state, "LineProgress"), Is.Zero);
            Assert.That(Characters(state, 16), Is.Zero);
        }
        [Test] public void LinesAndCharactersAppearTogether()
        {
            var state = State(.5f);
            Assert.That(Value(state, "LineProgress"), Is.InRange(.01f, .99f));
            Assert.That(Characters(state, 16), Is.InRange(1, 15));
        }
        [Test] public void AllLabelsIncludingAPCostsFinishWithoutTruncation()
        {
            var state = State(1f);
            Assert.That(Value(state, "RingProgress"), Is.EqualTo(1));
            Assert.That(Value(state, "LineProgress"), Is.EqualTo(1));
            Assert.That(Characters(state, 16), Is.EqualTo(16));
            Assert.That(Characters(state, 4), Is.EqualTo(4));
        }
        [Test] public void WeaponChoicesFindFreeSpaceWhenOriginalFourPlacementsAreBlocked()
        {
            var type = typeof(BattleUnit).Assembly.GetType("MechaXCOM.UnitCommandWheel");
            var method = type.GetMethod("TryPlaceChoices"); Assert.That(method, Is.Not.Null);
            var main = new Rect(468, 200, 600, 350);
            var info = new Rect(16, 16, 390, 650); var turn = new Rect(1018, 16, 246, 156);
            object[] args = { main, new Vector2(220, 172), new Rect(0, 0, 1280, 720), new[] { info, turn }, 8f, default(Rect) };
            Assert.That((bool)method.Invoke(null, args), Is.True);
            var choices = (Rect)args[5]; Assert.That(choices.Overlaps(main), Is.False);
            Assert.That(choices.Overlaps(info), Is.False); Assert.That(choices.Overlaps(turn), Is.False);
        }
    }
}
