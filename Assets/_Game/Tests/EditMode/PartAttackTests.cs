using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class PartAttackTests
    {
        private GameObject root;
        private GridManager grid;
        private CombatSystem combat;
        private BattleUnit player, enemy;

        [SetUp] public void Setup()
        {
            root = new GameObject("Part attack tests");
            grid = root.AddComponent<GridManager>(); grid.Generate();
            player = Unit(new Vector2Int(1, 1), UnitTeam.Player);
            enemy = Unit(new Vector2Int(3, 1), UnitTeam.Enemy);
            var turns = root.AddComponent<TurnManager>(); turns.Initialize(new[] { player, enemy });
            var visibility = root.AddComponent<VisibilitySystem>();
            visibility.Initialize(grid, new[] { player, enemy });
            turns.ConfigureVisibility(visibility);
            combat = root.AddComponent<CombatSystem>(); combat.Initialize(grid, root.AddComponent<CoverSystem>(), turns);
        }
        private BattleUnit Unit(Vector2Int position, UnitTeam team)
        {
            var unit = new GameObject(team.ToString()).AddTestBattleUnit(); unit.transform.SetParent(root.transform);
            unit.Configure(position, 5); unit.ConfigureTeam(team); Assert.That(unit.Initialize(grid), Is.True); return unit;
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);
        private void MakeCritical()
        {
            enemy.Health.SetDestructionRollSource(() => 99);
            enemy.Health.ApplyDamage(TargetBodyPart.Head, 6);
            enemy.Health.ApplyDamage(TargetBodyPart.LeftArm, 8);
            enemy.Health.ApplyDamage(TargetBodyPart.RightArm, 8);
            Assert.That(enemy.Health.State, Is.EqualTo(MechState.Critical));
        }

        [TestCase(80, TargetBodyPart.Torso, CoverType.None, 0, 80)]
        [TestCase(80, TargetBodyPart.Torso, CoverType.Half, 0, 60)]
        [TestCase(80, TargetBodyPart.Torso, CoverType.Full, 0, 40)]
        [TestCase(80, TargetBodyPart.Cockpit, CoverType.None, 0, 36)]
        [TestCase(80, TargetBodyPart.Cockpit, CoverType.Half, 0, 27)]
        [TestCase(80, TargetBodyPart.Cockpit, CoverType.Full, 0, 18)]
        [TestCase(80, TargetBodyPart.Head, CoverType.Full, 0, 32)]
        [TestCase(80, TargetBodyPart.LeftArm, CoverType.Half, 0, 54)]
        [TestCase(80, TargetBodyPart.RightLeg, CoverType.None, 0, 72)]
        [TestCase(80, TargetBodyPart.Cockpit, CoverType.Full, 1, 18)]
        [TestCase(100, TargetBodyPart.Cockpit, CoverType.Full, 0, 23)]
        [TestCase(0, TargetBodyPart.Cockpit, CoverType.Half, 1, 0)]
        [TestCase(71, TargetBodyPart.Cockpit, CoverType.Half, 0, 24)]
        public void RangedMultipliersCombineBeforeSingleRounding(int accuracy, TargetBodyPart part,
            CoverType cover, int movedCells, int expected)
        {
            player.Combat.ConfigureTestRanged(5, accuracy, 7, 3);
            grid.TryGetCell(enemy.CurrentGridPosition, out var cell);
            cell.SetCover(CoverDirection.West, cover);
            for (int i = 0; i < movedCells; i++) enemy.Mobility.RecordCellMoved();
            var preview = combat.GetPreview(player, enemy, AttackType.Ranged, part);
            Assert.That(preview.HitChance, Is.EqualTo(expected));
            combat.SetRollSource(() => expected);
            var result = combat.TryAttack(player, enemy, AttackType.Ranged, part);
            Assert.That(result.HitChance, Is.EqualTo(expected));
            Assert.That(result.Hit, Is.False);
            Assert.That(enemy.Health.GetCurrentHP(part), Is.EqualTo(enemy.Health.GetMaxHP(part)));
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }

        [Test] public void CockpitTwoRangedHitsDestroyItWithoutDamagingBody()
        {
            var preview = combat.GetPreview(player, enemy, AttackType.Ranged, TargetBodyPart.Cockpit);
            Assert.That(preview.HitChance, Is.EqualTo(36));
            Assert.That(preview.PartHP, Is.EqualTo(6));
            Assert.That(preview.PartMaxHP, Is.EqualTo(6));
            combat.SetRollSource(() => 0);
            int destructionRolls = 0; enemy.Health.SetDestructionRollSource(() => { destructionRolls++; return 99; });
            var first = combat.TryAttack(player, enemy, AttackType.Ranged, TargetBodyPart.Cockpit);
            Assert.That(first.DamageDealt, Is.EqualTo(5));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Cockpit), Is.EqualTo(1));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
            player.ActionPoints.ResetForTurn();
            var second = combat.TryAttack(player, enemy, AttackType.Ranged, TargetBodyPart.Cockpit);
            Assert.That(second.DamageDealt, Is.EqualTo(1));
            Assert.That(second.TargetPartHPAfter, Is.Zero);
            Assert.That(second.TargetStateAfter, Is.EqualTo(MechState.Destroyed));
            Assert.That(second.DestructionChecked, Is.False);
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(destructionRolls, Is.Zero);
        }

        [TestCase(5, 0, 5)] [TestCase(5, 1, 2)] [TestCase(5, 2, 0)]
        [TestCase(1, 1, 1)] [TestCase(0, 1, 0)] [TestCase(6, 1, 3)]
        public void DestroyedLegsLimitReachablePaths(int baseRange, int brokenLegs, int expectedRange)
        {
            player.Configure(player.StartGridPosition, baseRange);
            if (brokenLegs > 0) player.Health.ApplyDamage(TargetBodyPart.LeftLeg, 10);
            if (brokenLegs > 1) player.Health.ApplyDamage(TargetBodyPart.RightLeg, 10);
            Assert.That(player.AvailableMovementRange, Is.EqualTo(expectedRange));
            var destination = player.CurrentGridPosition + Vector2Int.up * (expectedRange + 1);
            Assert.That(Pathfinder.FindPath(grid, player, destination), Is.Empty);
            if (expectedRange > 0)
                Assert.That(Pathfinder.FindPath(grid, player, player.CurrentGridPosition + Vector2Int.up * expectedRange).Count,
                    Is.EqualTo(expectedRange));
            else Assert.That(Pathfinder.GetReachableCells(grid, player), Is.Empty);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }

        [TestCase(AttackType.Ranged, 80, TargetBodyPart.Torso, CoverType.None, 0, 60)]
        [TestCase(AttackType.Ranged, 80, TargetBodyPart.Cockpit, CoverType.Full, 0, 14)]
        [TestCase(AttackType.Ranged, 80, TargetBodyPart.Cockpit, CoverType.Half, 1, 20)]
        [TestCase(AttackType.Ranged, 100, TargetBodyPart.Torso, CoverType.None, 0, 75)]
        [TestCase(AttackType.Melee, 95, TargetBodyPart.Cockpit, CoverType.Full, 1, 71)]
        public void DestroyedAttackerHeadScalesAccuracyBeforeFinalRounding(AttackType type, int accuracy,
            TargetBodyPart part, CoverType cover, int movedCells, int expected)
        {
            player.Combat.ConfigureTestRanged(5, accuracy, 7, 3); player.Combat.ConfigureTestMelee(8, accuracy, 2, 3);
            player.Health.ApplyDamage(TargetBodyPart.Head, 6);
            grid.TryGetCell(enemy.CurrentGridPosition, out var cell); cell.SetCover(CoverDirection.West, cover);
            for (int i = 0; i < movedCells; i++) enemy.Mobility.RecordCellMoved();
            Assert.That(combat.GetPreview(player, enemy, type, part).HitChance, Is.EqualTo(expected));
            combat.SetRollSource(() => expected);
            var result = combat.TryAttack(player, enemy, type, part);
            Assert.That(result.HitChance, Is.EqualTo(expected)); Assert.That(result.Hit, Is.False);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }

        [TestCase(TargetBodyPart.Whole, TargetBodyPart.Torso, 15)]
        [TestCase(TargetBodyPart.Torso, TargetBodyPart.Torso, 15)]
        [TestCase(TargetBodyPart.Head, TargetBodyPart.Head, 1)]
        [TestCase(TargetBodyPart.LeftArm, TargetBodyPart.LeftArm, 3)]
        [TestCase(TargetBodyPart.RightArm, TargetBodyPart.RightArm, 3)]
        [TestCase(TargetBodyPart.LeftLeg, TargetBodyPart.LeftLeg, 5)]
        [TestCase(TargetBodyPart.RightLeg, TargetBodyPart.RightLeg, 5)]
        public void RangedDamagesOnlySelectedPart(TargetBodyPart requested, TargetBodyPart actual, int hpAfter)
        {
            combat.SetRollSource(() => 0);
            var preview = combat.GetPreview(player, enemy, AttackType.Ranged, requested);
            var result = combat.TryAttack(player, enemy, AttackType.Ranged, requested);
            Assert.That(preview.TargetPart, Is.EqualTo(actual));
            Assert.That(result.TargetPart, Is.EqualTo(actual));
            Assert.That(result.TargetPartHPAfter, Is.EqualTo(hpAfter));
            Assert.That(enemy.Health.GetCurrentHP(actual), Is.EqualTo(hpAfter));
            if (actual != TargetBodyPart.Torso) Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            if (actual != TargetBodyPart.Head) Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(6));
            Assert.That(result.DamageDealt, Is.EqualTo(5));
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }

        [TestCase(TargetBodyPart.Head)]
        [TestCase((TargetBodyPart)999)]
        public void MeleeAlwaysDamagesBodyDespiteRequestedPart(TargetBodyPart requested)
        {
            var preview = combat.GetPreview(player, enemy, AttackType.Melee, requested);
            Assert.That(preview.HitChance, Is.EqualTo(95));
            Assert.That(preview.TargetPart, Is.EqualTo(TargetBodyPart.Torso));
            combat.SetRollSource(() => 0);
            var result = combat.TryAttack(player, enemy, AttackType.Melee, requested);
            Assert.That(result.TargetPart, Is.EqualTo(TargetBodyPart.Torso));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(12));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(6));
            Assert.That(result.DamageDealt, Is.EqualTo(8));
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }

        [Test] public void CockpitAndCoverRetainSameChanceAfterMovement()
        {
            grid.TryGetCell(enemy.CurrentGridPosition, out var cell); cell.SetCover(CoverDirection.West, CoverType.Full);
            Assert.That(combat.GetPreview(player, enemy, AttackType.Ranged, TargetBodyPart.Cockpit).HitChance, Is.EqualTo(18));
            enemy.Mobility.RecordCellMoved();
            Assert.That(combat.GetPreview(player, enemy, AttackType.Ranged, TargetBodyPart.Cockpit).HitChance, Is.EqualTo(18));
            combat.SetRollSource(() => 18);
            Assert.That(combat.TryAttack(player, enemy, AttackType.Ranged, TargetBodyPart.Cockpit).Hit, Is.False);
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Cockpit), Is.EqualTo(6));
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }

        [TestCase(35, true, 1)]
        [TestCase(36, false, 6)]
        public void CockpitRollBoundaryMatchesPreview(int roll, bool hit, int hpAfter)
        {
            combat.SetRollSource(() => roll);
            var result = combat.TryAttack(player, enemy, AttackType.Ranged, TargetBodyPart.Cockpit);
            Assert.That(result.HitChance, Is.EqualTo(36));
            Assert.That(result.Hit, Is.EqualTo(hit));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Cockpit), Is.EqualTo(hpAfter));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }

        [TestCase(99, 0, MechState.Critical)]
        [TestCase(0, 1, MechState.Destroyed)]
        public void CriticalMissSkipsDestructionAndHitToBrokenPartChecksOnce(int hitRoll, int checksExpected, MechState expected)
        {
            MakeCritical();
            int checks = 0; enemy.Health.SetDestructionRollSource(() => { checks++; return 24; });
            combat.SetRollSource(() => hitRoll);
            var preview = combat.GetPreview(player, enemy, AttackType.Ranged, TargetBodyPart.Head);
            Assert.That(preview.CanAttack, Is.True);
            Assert.That(preview.TargetState, Is.EqualTo(MechState.Critical));
            var result = combat.TryAttack(player, enemy, AttackType.Ranged, TargetBodyPart.Head);
            Assert.That(result.WasExecuted, Is.True);
            Assert.That(result.DamageDealt, Is.Zero);
            Assert.That(result.DestructionChecked, Is.EqualTo(checksExpected == 1));
            Assert.That(result.TargetStateAfter, Is.EqualTo(expected));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(enemy.Health.MajorPartsDestroyed, Is.EqualTo(3));
            Assert.That(checks, Is.EqualTo(checksExpected));
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }

        [Test] public void InvalidPartAttackDoesNotSpendAPOrRoll()
        {
            int hitRolls = 0, destructionRolls = 0;
            combat.SetRollSource(() => { hitRolls++; return 0; });
            enemy.Health.SetDestructionRollSource(() => { destructionRolls++; return 0; });
            Assert.That(combat.GetPreview(player, enemy, AttackType.Ranged, (TargetBodyPart)999).CanAttack, Is.False);
            Assert.That(combat.TryAttack(player, enemy, AttackType.Ranged, (TargetBodyPart)999).WasExecuted, Is.False);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(hitRolls, Is.Zero); Assert.That(destructionRolls, Is.Zero);
        }

        [TestCase(AttackType.Ranged, 0)]
        [TestCase(AttackType.Ranged, -5)]
        [TestCase(AttackType.Melee, 0)]
        [TestCase(AttackType.Melee, -5)]
        public void NonpositiveDamageAttackDoesNotSpendAPOrRoll(AttackType type, int damage)
        {
            MakeCritical();
            if (type == AttackType.Ranged) player.Combat.ConfigureTestRanged(damage, 80, 7, 3);
            else player.Combat.ConfigureTestMelee(damage, 95, 2, 3);
            int hitRolls = 0, destructionRolls = 0;
            combat.SetRollSource(() => { hitRolls++; return 0; });
            enemy.Health.SetDestructionRollSource(() => { destructionRolls++; return 0; });
            Assert.That(combat.GetPreview(player, enemy, type).CanAttack, Is.False);
            Assert.That(combat.TryAttack(player, enemy, type).WasExecuted, Is.False);
            Assert.That(combat.GetAttackRangeCells(player, type), Is.Empty);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(enemy.Health.State, Is.EqualTo(MechState.Critical));
            Assert.That(hitRolls, Is.Zero); Assert.That(destructionRolls, Is.Zero);
        }
    }
}
