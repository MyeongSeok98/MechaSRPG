using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class MeleeCombatTests
    {
        private GameObject root;
        private GridManager grid;
        private CombatSystem combat;
        private BattleUnit player, enemy;
        [SetUp] public void Setup()
        {
            root = new GameObject("Melee rules");
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
        private AttackPreview Preview() => combat.GetPreview(player, enemy, AttackType.Melee);
        [TearDown] public void Cleanup() => UnityEngine.Object.DestroyImmediate(root);
        [Test] public void MeleeIgnoresCoverAndMovementAndDealsEightForThreeAP()
        {
            grid.TryGetCell(enemy.CurrentGridPosition, out var cell); cell.SetCover(CoverDirection.West, CoverType.Full);
            for (int i = 0; i < 3; i++) enemy.Mobility.RecordCellMoved();
            var preview = Preview();
            Assert.That(preview.CanAttack, Is.True); Assert.That(preview.HitChance, Is.EqualTo(95));
            Assert.That(preview.CoverMultiplier, Is.EqualTo(1f)); Assert.That(preview.MinDamage, Is.EqualTo(8));
            Assert.That(combat.GetPreview(player, enemy).HitChance, Is.EqualTo(40));
            combat.SetRollSource(() => 0);
            Assert.That(combat.TryAttack(player, enemy, AttackType.Melee).WasExecuted, Is.True);
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(12)); Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(combat.TryAttack(player, enemy, AttackType.Melee).WasExecuted, Is.False);
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(12));
        }
        [Test] public void MeleeMissStillCostsThreeWithoutDamage()
        {
            combat.SetRollSource(() => 95);
            var result = combat.TryAttack(player, enemy, AttackType.Melee);
            Assert.That(result.WasExecuted, Is.True); Assert.That(result.HitChance, Is.EqualTo(95));
            Assert.That(result.Hit, Is.False); Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [Test] public void MeleeCannotReachAcrossRavineOrBuildingWithoutSpending()
        {
            grid.SetWalkable(new Vector2Int(2, 1), false);
            int rolls = 0; combat.SetRollSource(() => { rolls++; return 0; });
            Assert.That(Preview().CanAttack, Is.False);
            Assert.That(combat.TryAttack(player, enemy, AttackType.Melee).WasExecuted, Is.False);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(5)); Assert.That(rolls, Is.Zero);
            Assert.That(combat.GetPreview(player, enemy).CanAttack, Is.True, "Ranged shots can cross the ravine.");
        }
        [Test] public void MeleeDiagonalNeedsAtLeastOneOpenTwoStepRoute()
        {
            grid.TryMoveUnit(enemy, new Vector2Int(2, 2));
            grid.SetWalkable(new Vector2Int(2, 1), false); grid.SetWalkable(new Vector2Int(1, 2), false);
            Assert.That(Preview().CanAttack, Is.False);
            grid.SetWalkable(new Vector2Int(1, 2), true);
            Assert.That(Preview().CanAttack, Is.True);
            grid.TryGetCell(enemy.CurrentGridPosition, out var destination);
            Assert.That(combat.GetAttackRangeCells(player, AttackType.Melee), Has.Member(destination));
        }
        [Test] public void OccupiedIntermediateCellBlocksMeleeReachAndHighlight()
        {
            Unit(new Vector2Int(2, 1), UnitTeam.Player);
            Assert.That(Preview().CanAttack, Is.False);
            grid.TryGetCell(enemy.CurrentGridPosition, out var destination);
            Assert.That(combat.GetAttackRangeCells(player, AttackType.Melee), Has.No.Member(destination));
        }
        [Test] public void MeleeRangeEndsAtTwoAndRangedRangeEndsAtSeven()
        {
            grid.TryMoveUnit(enemy, new Vector2Int(4, 1)); Assert.That(Preview().CanAttack, Is.False);
            grid.TryMoveUnit(enemy, new Vector2Int(8, 1)); Assert.That(combat.GetPreview(player, enemy).CanAttack, Is.True);
            grid.TryMoveUnit(enemy, new Vector2Int(9, 1)); Assert.That(combat.GetPreview(player, enemy).CanAttack, Is.False);
        }
    }
}
