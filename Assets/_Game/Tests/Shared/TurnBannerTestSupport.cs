using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public static class TurnBannerTestSupport
    {
        public static IEnumerator Finish(TurnManager turns)
        {
            float end = Time.realtimeSinceStartup + 3;
            while (turns && turns.IsPresentingTurn && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(turns && turns.IsPresentingTurn, Is.False, "Turn presentation must finish before the next test action.");
        }
    }
}
