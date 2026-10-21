using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class MobilityTests
    {
        [TestCase(0, 0)] [TestCase(1, 5)] [TestCase(2, 5)] [TestCase(3, 10)]
        [TestCase(4, 10)] [TestCase(5, 15)] [TestCase(6, 15)]
        public void MovementThresholds(int cells, int expected)
        {
            var go = new GameObject("Mobility Test");
            try
            {
                var mobility = go.AddComponent<UnitMobility>();
                for (int i = 0; i < cells; i++) mobility.RecordCellMoved();
                Assert.That(mobility.MovedCells, Is.EqualTo(cells));
                Assert.That(mobility.MovementEvasion, Is.EqualTo(expected));
                mobility.ResetMovement();
                Assert.That(mobility.MovedCells, Is.Zero);
                Assert.That(mobility.MovementEvasion, Is.Zero);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
