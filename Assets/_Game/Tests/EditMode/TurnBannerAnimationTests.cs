using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class TurnBannerAnimationTests
    {
        private object Frame(float elapsed)
        {
            var type = typeof(TurnManager).Assembly.GetType("MechaXCOM.TurnBannerAnimation");
            Assert.That(type, Is.Not.Null, "Turn banners need the shared right-center-left timeline.");
            return type.GetMethod("Evaluate", BindingFlags.Public | BindingFlags.Static).Invoke(null,
                new object[] { elapsed, .15f, .4f, .15f, new Rect(140, 250, 1000, 220), 1280f });
        }
        private T Read<T>(object frame, string property) => (T)frame.GetType().GetProperty(property).GetValue(frame);
        [Test] public void StartsFullyOutsideRightSide()
        { Assert.That(Read<Rect>(Frame(0), "Rect").xMin, Is.GreaterThanOrEqualTo(1280)); }
        [Test] public void EntersQuicklyWithoutOvershootingCenter()
        { Assert.That(Read<Rect>(Frame(.075f), "Rect").x, Is.InRange(140.01f, 1279.99f)); }
        [TestCase(.15f)] [TestCase(.3f)] [TestCase(.549f)]
        public void HoldsCenteredAndOpaqueForPointFourSeconds(float time)
        {
            var frame = Frame(time); Assert.That(Read<Rect>(frame, "Rect").center, Is.EqualTo(new Vector2(640, 360)));
            Assert.That(Read<float>(frame, "Alpha"), Is.EqualTo(1));
        }
        [Test] public void ExitsToLeftWhileFading()
        {
            var frame = Frame(.625f); Assert.That(Read<Rect>(frame, "Rect").x, Is.LessThan(140));
            Assert.That(Read<float>(frame, "Alpha"), Is.InRange(.01f, .99f));
        }
        [Test] public void FinishesAfterPointSevenSeconds()
        { Assert.That(Read<bool>(Frame(.701f), "Visible"), Is.False); Assert.That(Read<float>(Frame(.701f), "Alpha"), Is.Zero); }
    }
}
