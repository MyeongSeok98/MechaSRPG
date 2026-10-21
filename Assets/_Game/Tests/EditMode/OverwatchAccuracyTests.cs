using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class OverwatchAccuracyTests
    {
        private static int Chance(int baseChance, float part = 1f, float cover = 1f,
            float head = 1f, float grip = 1f, float distance = 1f, float reaction = 1f)
        {
            var method = typeof(CombatMath).GetMethod("CalculateAttackHitChance", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "The common formula needs a reaction multiplier and no movement evasion.");
            return (int)method.Invoke(null, new object[] { baseChance, part, cover, head, grip, distance, reaction });
        }

        [Test] public void ReactionMultiplierAppliesBeforeSingleRounding() =>
            Assert.That(Chance(100, distance: .794f, reaction: .75f), Is.EqualTo(60));
        [Test] public void HighAccuracyIsNotClampedBeforeReaction() =>
            Assert.That(Chance(100, grip: 1.2f, reaction: .75f), Is.EqualTo(90));
        [Test] public void ZeroDistanceMultiplierRemainsZeroWithLargeGrip() =>
            Assert.That(Chance(80, grip: float.MaxValue, distance: 0), Is.Zero);
        [Test] public void LargeFiniteMultipliersClampAtOneHundred() =>
            Assert.That(Chance(80, grip: float.MaxValue, distance: float.MaxValue), Is.EqualTo(100));
        [Test] public void NegativeBaseChanceClampsAtZero() => Assert.That(Chance(-10), Is.Zero);
        [TestCase(10, .45f, 1f, 1f, 5)]
        [TestCase(80, .45f, .5f, .75f, 14)]
        [TestCase(100, .45f, .5f, 1f, 23)]
        public void FloatMultipliersKeepHalfUpRounding(int baseChance, float part, float cover, float head, int expected) =>
            Assert.That(Chance(baseChance, part, cover, head), Is.EqualTo(expected));
        [Test] public void CoverHeadAndBodyModifiersRemainMultiplicative() =>
            Assert.That(Chance(80, part: .45f, cover: .5f, head: .75f, grip: 1.1f, distance: .9f), Is.EqualTo(13));

        [TestCase(AttackType.Ranged, 80)] [TestCase(AttackType.Melee, 95)]
        public void MovementDoesNotChangeRangedOrMeleePreviewOrExecution(AttackType type, int expected)
        {
            var root = new GameObject("No movement evasion combat tests");
            try
            {
                var grid = root.AddComponent<GridManager>(); grid.Generate();
                var player = MakeUnit(root, grid, new Vector2Int(1, 1), UnitTeam.Player);
                var enemy = MakeUnit(root, grid, new Vector2Int(3, 1), UnitTeam.Enemy);
                var turns = root.AddComponent<TurnManager>(); turns.Initialize(new[] { player, enemy });
                var vision = root.AddComponent<VisibilitySystem>(); vision.Initialize(grid, new[] { player, enemy });
                turns.ConfigureVisibility(vision);
                var combat = root.AddComponent<CombatSystem>(); combat.Initialize(grid, root.AddComponent<CoverSystem>(), turns);
                Assert.That(combat.GetPreview(player, enemy, type).HitChance, Is.EqualTo(expected));
                for (int i = 0; i < 5; i++) enemy.Mobility.RecordCellMoved();
                var preview = combat.GetPreview(player, enemy, type);
                Assert.That(preview.CanAttack, Is.True);
                Assert.That(preview.HitChance, Is.EqualTo(expected), "Recorded movement must not reduce hit chance.");
                combat.SetRollSource(() => expected - 1);
                var result = combat.TryAttack(player, enemy, type);
                Assert.That(result.WasExecuted, Is.True); Assert.That(result.HitChance, Is.EqualTo(expected));
                Assert.That(result.Hit, Is.True); Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
                Assert.That(enemy.Mobility.MovedCells, Is.EqualTo(5), "Movement tracking remains available for future rules.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static BattleUnit MakeUnit(GameObject root, GridManager grid, Vector2Int position, UnitTeam team)
        {
            var unit = new GameObject(team.ToString()).AddTestBattleUnit(); unit.transform.SetParent(root.transform);
            unit.Configure(position, 5); unit.ConfigureTeam(team);
            Assert.That(unit.Initialize(grid), Is.True);
            return unit;
        }
    }
}
