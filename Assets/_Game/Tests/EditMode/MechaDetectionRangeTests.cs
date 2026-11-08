using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class MechaDetectionRangeTests
    {
        private GameObject root;
        private GridManager grid;
        private readonly List<MechaData> data = new List<MechaData>();
        [SetUp] public void Setup()
        {
            root = new GameObject("Mecha detection tests");
            grid = root.AddComponent<GridManager>(); grid.Configure(24, 24, 2); grid.Generate();
        }
        [TearDown] public void Cleanup()
        { Object.DestroyImmediate(root); foreach (var item in data) if (item) Object.DestroyImmediate(item); data.Clear(); }
        private MechaData Mecha(int range)
        {
            var value = ScriptableObject.CreateInstance<MechaData>(); data.Add(value);
            BackpackStageOneSupport.Set(value, "mechaId", "sensor_test");
            BackpackStageOneSupport.Set(value, "detectionRange", range); return value;
        }
        private BattleUnit Unit(MechaData mecha, Vector2Int position, int? previousRange = null, UnitTeam team = UnitTeam.Player)
        {
            var unit = new GameObject(team.ToString()).AddTestBattleUnit(); unit.transform.SetParent(root.transform);
            BackpackStageOneSupport.Set(unit, "mechaData", mecha); unit.Configure(position, 5); unit.ConfigureTeam(team);
            if (previousRange.HasValue) unit.gameObject.AddComponent<UnitVision>().Configure(previousRange.Value);
            return unit;
        }
        [TestCase(true)] [TestCase(false)] public void MechaRangeOverridesExistingUnitVision(bool existingVision)
        {
            var unit = Unit(Mecha(12), Vector2Int.one, existingVision ? 3 : (int?)null);
            Assert.That(unit.Initialize(grid), Is.True);
            Assert.That(unit.Vision.DetectionRange, Is.EqualTo(12));
            Assert.That(unit.GetComponents<UnitVision>(), Has.Length.EqualTo(1));
        }
        [TestCase(6)] [TestCase(-1)] public void MissingMechaKeepsLegacyRange(int previousRange)
        {
            var unit = Unit(null, Vector2Int.one, previousRange < 0 ? (int?)null : previousRange);
            Assert.That(unit.Initialize(grid), Is.True);
            Assert.That(unit.Vision.DetectionRange, Is.EqualTo(previousRange < 0 ? 10 : previousRange));
        }
        [Test] public void ZeroMechaRangeOnlyRevealsOwnCell()
        {
            var observer = Unit(Mecha(0), Vector2Int.one); var enemy = Unit(null, new Vector2Int(2, 1), team: UnitTeam.Enemy);
            Assert.That(observer.Initialize(grid) && enemy.Initialize(grid), Is.True);
            var visibility = root.AddComponent<VisibilitySystem>(); visibility.Initialize(grid, new[] { observer, enemy });
            Assert.That(observer.Vision.DetectionRange, Is.Zero);
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, Vector2Int.one), Is.EqualTo(CellVisibility.Visible));
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, enemy.CurrentGridPosition), Is.EqualTo(CellVisibility.Unexplored));
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.False);
        }
        [Test] public void NegativeMechaRangeRejectsInitializationWithoutOccupyingGrid()
        {
            var mecha = Mecha(-1); var unit = Unit(mecha, Vector2Int.one);
            LogAssert.Expect(LogType.Warning, new Regex("invalid MechaData.*탐지"));
            Assert.That(unit.Initialize(grid), Is.False); Assert.That(unit.IsInitialized, Is.False);
            Assert.That(grid.TryGetCell(Vector2Int.one, out var cell), Is.True); Assert.That(cell.OccupyingUnit, Is.Null);
            BackpackStageOneSupport.Set(mecha, "detectionRange", 0); Assert.That(unit.Initialize(grid), Is.True);
            Assert.That(unit.Vision.DetectionRange, Is.Zero);
        }
        [Test] public void SharedMechaProducesIndependentUnitVision()
        {
            var mecha = Mecha(12); string original = JsonUtility.ToJson(mecha);
            var first = Unit(mecha, Vector2Int.one); var second = Unit(mecha, new Vector2Int(2, 1));
            Assert.That(first.Initialize(grid) && second.Initialize(grid), Is.True);
            Assert.That(first.Vision, Is.Not.SameAs(second.Vision));
            Assert.That(second.Vision.DetectionRange, Is.EqualTo(12));
            first.Vision.Configure(4);
            Assert.That(first.Vision.DetectionRange, Is.EqualTo(4)); Assert.That(second.Vision.DetectionRange, Is.EqualTo(12));
            Assert.That(JsonUtility.ToJson(mecha), Is.EqualTo(original));
        }
        [Test] public void RepeatedInitializeDoesNotOverwriteAppliedRange()
        {
            var mecha = Mecha(12); var unit = Unit(mecha, Vector2Int.one); Assert.That(unit.Initialize(grid), Is.True);
            Assert.That(unit.Vision.DetectionRange, Is.EqualTo(12));
            unit.Vision.Configure(4); BackpackStageOneSupport.Set(mecha, "detectionRange", 7);
            Assert.That(unit.Vision.DetectionRange, Is.EqualTo(4));
            Assert.That(unit.Initialize(grid), Is.True); Assert.That(unit.Vision.DetectionRange, Is.EqualTo(4));
        }
        [Test] public void StartupDetectionUsesMechaRangeAndExistingLOS()
        {
            var observer = Unit(Mecha(5), Vector2Int.one);
            var boundary = Unit(null, new Vector2Int(4, 5), team: UnitTeam.Enemy);
            var outside = Unit(null, new Vector2Int(5, 5), team: UnitTeam.Enemy);
            var occluded = Unit(null, new Vector2Int(5, 1), team: UnitTeam.Enemy);
            foreach (var unit in new[] { observer, boundary, outside, occluded }) Assert.That(unit.Initialize(grid), Is.True);
            var building = new GameObject("Vision blocker"); building.transform.SetParent(root.transform);
            building.transform.position = grid.GetWorldPosition(new Vector2Int(3, 1));
            var obstacle = building.AddComponent<GridObstacle>(); obstacle.Collider.size = Vector3.one;
            grid.RegisterObstacle(obstacle.Collider);
            var visibility = root.AddComponent<VisibilitySystem>(); visibility.Initialize(grid, new[] { observer, boundary, outside, occluded });
            Assert.That(observer.Vision.DetectionRange, Is.EqualTo(5));
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, boundary), Is.True);
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, outside), Is.False);
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, occluded), Is.False);
        }
    }
}
