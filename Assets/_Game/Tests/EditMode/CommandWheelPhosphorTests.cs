using System;
using System.Reflection;
using NUnit.Framework;

namespace MechaXCOM.Tests
{
    public class CommandWheelPhosphorTests
    {
        private float Opacity(float elapsed, float age, float persistence)
        {
            var type = typeof(BattleUnit).Assembly.GetType("MechaXCOM.CommandWheelPhosphor");
            Assert.That(type, Is.Not.Null, "The HUD needs bounded, unit-relative phosphor persistence.");
            var method = type.GetMethod("TrailOpacity", BindingFlags.Static | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            return (float)method.Invoke(null, new object[] { elapsed, age, persistence });
        }

        [Test] public void NewSelectionCannotReplayBeforeItsStart()
        {
            Assert.That(Opacity(0, .03f, .12f), Is.Zero);
            Assert.That(Opacity(.02f, .03f, .12f), Is.Zero);
            Assert.That(Opacity(.2f, .03f, .12f), Is.GreaterThan(0));
        }
        [Test] public void OlderGeometryDecaysExponentiallyAndPersistenceIsAdjustable()
        {
            float young = Opacity(.5f, .03f, .12f), old = Opacity(.5f, .15f, .12f);
            Assert.That(old / young, Is.EqualTo((float)Math.Exp(-1)).Within(.001f));
            Assert.That(Opacity(.5f, .15f, .06f), Is.LessThan(old));
            Assert.That(Opacity(.5f, .03f, 0), Is.Zero);
        }
        [Test] public void SettledMenuHasNoPermanentGhostGeometry()
        {
            Assert.That(Opacity(1.5f, .03f, .12f), Is.LessThan(Opacity(1f, .03f, .12f)));
            Assert.That(Opacity(2.1f, .03f, .12f), Is.Zero);
        }
    }
}
