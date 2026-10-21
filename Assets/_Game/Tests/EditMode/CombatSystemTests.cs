using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class CombatSystemTests
    {
        protected GameObject root;
        protected GridManager grid;
        protected CoverSystem covers;
        protected TurnManager turns;
        protected CombatSystem combat;
        protected BattleUnit player, enemy;
        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Combat tests");
            grid = root.AddComponent<GridManager>(); grid.Generate();
            covers = root.AddComponent<CoverSystem>();
            player = MakeUnit("Player", new Vector2Int(1, 1), UnitTeam.Player);
            enemy = MakeUnit("Enemy", new Vector2Int(3, 3), UnitTeam.Enemy);
            turns = root.AddComponent<TurnManager>(); turns.Initialize(new[] { player, enemy });
            var visibility = root.AddComponent<VisibilitySystem>();
            visibility.Initialize(grid, new[] { player, enemy });
            turns.ConfigureVisibility(visibility);
            combat = root.AddComponent<CombatSystem>(); combat.Initialize(grid, covers, turns);
        }
        protected BattleUnit MakeUnit(string name, Vector2Int pos, UnitTeam team)
        {
            var unit = new GameObject(name).AddTestBattleUnit(); unit.transform.SetParent(root.transform);
            unit.GetComponent<UnitCombat>().ConfigureTestRanged(5, 80, 5, 3);
            unit.Configure(pos, 5); unit.ConfigureTeam(team); Assert.That(unit.Initialize(grid), Is.True);
            return unit;
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);
        [TestCase(0, 15, true)] [TestCase(99, 20, false)]
        public void HitAndMissBothCostThree(int roll, int hp, bool hit)
        {
            combat.SetRollSource(() => roll);
            var result = combat.TryAttack(player, enemy);
            Assert.That(result.WasExecuted, Is.True); Assert.That(result.Hit, Is.EqualTo(hit));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(hp));
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [Test]
        public void UnwalkableClearShotCellIsInAttackRangeAndDoesNotBlockAttack()
        {
            grid.SetWalkable(new Vector2Int(2, 2), false);
            grid.TryGetCell(new Vector2Int(2, 2), out var ravineCell);
            Assert.That(combat.GetAttackRangeCells(player), Has.Member(ravineCell));
            Assert.That(Pathfinder.GetReachableCells(grid, player), Has.No.Member(ravineCell));
            combat.SetRollSource(() => 0);
            Assert.That(combat.TryAttack(player, enemy).WasExecuted, Is.True);
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
        }
        [Test]
        public void DiamondRangeMatchesPreviewHighlightAndAttackValidation()
        {
            int rolls = 0;
            combat.SetRollSource(() => { rolls++; return 0; });
            Assert.That(grid.TryMoveUnit(enemy, new Vector2Int(4, 4)), Is.True);
            Assert.That(combat.GetPreview(player, enemy).Distance, Is.EqualTo(6));
            Assert.That(combat.GetPreview(player, enemy).CanAttack, Is.False);
            grid.TryGetCell(enemy.CurrentGridPosition, out var outside);
            Assert.That(combat.GetAttackRangeCells(player), Has.No.Member(outside));
            Assert.That(combat.TryAttack(player, enemy).WasExecuted, Is.False);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(rolls, Is.Zero);

            Assert.That(grid.TryMoveUnit(enemy, new Vector2Int(4, 3)), Is.True);
            Assert.That(combat.GetPreview(player, enemy).Distance, Is.EqualTo(5));
            Assert.That(combat.GetPreview(player, enemy).CanAttack, Is.True);
            grid.TryGetCell(enemy.CurrentGridPosition, out var boundary);
            Assert.That(combat.GetAttackRangeCells(player), Has.Member(boundary));
            Assert.That(combat.TryAttack(player, enemy).WasExecuted, Is.True);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(rolls, Is.EqualTo(1));
        }
        [Test]
        public void BuildingBlocksShotWithoutSpendingAPHealthOrRandomRoll()
        {
            var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.transform.SetParent(root.transform);
            building.transform.position = grid.GetWorldPosition(new Vector2Int(2, 2)) + Vector3.up;
            building.transform.localScale = new Vector3(2, 4, 2);
            grid.RegisterObstacle(building.AddComponent<GridObstacle>().Collider);
            int rolls = 0;
            combat.SetRollSource(() => { rolls++; return 0; });
            Assert.That(combat.GetPreview(player, enemy).CanAttack, Is.False);
            Assert.That(combat.TryAttack(player, enemy).WasExecuted, Is.False);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(rolls, Is.Zero);
        }
        [Test]
        public void BuildingCornerTouchBlocksShot()
        {
            var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.transform.SetParent(root.transform);
            building.transform.position = grid.GetWorldPosition(new Vector2Int(2, 1)) + Vector3.up;
            building.transform.localScale = new Vector3(2, 4, 2);
            grid.RegisterObstacle(building.AddComponent<GridObstacle>().Collider);
            Assert.That(combat.GetPreview(player, enemy).CanAttack, Is.False);
        }
        [Test]
        public void CoverOnShotPathStillAllowsAttackWithCoverBonus()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform);
            cube.transform.position = grid.GetWorldPosition(new Vector2Int(2, 2)) + Vector3.up;
            cube.transform.localScale = new Vector3(2, 2, 2);
            var cover = cube.AddComponent<CoverObject>(); cover.Configure(CoverType.Full);
            covers.Build(grid, new[] { cover });
            var preview = combat.GetPreview(player, enemy);
            Assert.That(preview.CanAttack, Is.True);
            Assert.That(preview.CoverMultiplier, Is.EqualTo(.5f));
            Assert.That(preview.HitChance, Is.EqualTo(40));
        }
        [Test]
        public void InvalidAttackDoesNotRollOrSpend()
        {
            int rolls = 0; combat.SetRollSource(() => { rolls++; return 0; });
            Assert.That(combat.TryAttack(player, player).WasExecuted, Is.False);
            Assert.That(combat.TryAttack(enemy, player).WasExecuted, Is.False);
            grid.TryMoveUnit(enemy, new Vector2Int(9, 9));
            Assert.That(combat.TryAttack(player, enemy).WasExecuted, Is.False);
            grid.TryMoveUnit(enemy, new Vector2Int(3, 3));
            grid.ReleaseUnit(enemy);
            Assert.That(combat.TryAttack(player, enemy).WasExecuted, Is.False);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20)); Assert.That(rolls, Is.Zero);
        }
        [Test]
        public void InsufficientAPRejectsSecondAttackInSameFrame()
        {
            int rolls = 0; combat.SetRollSource(() => { rolls++; return 0; });
            combat.TryAttack(player, enemy);
            Assert.That(combat.TryAttack(player, enemy).WasExecuted, Is.False);
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2)); Assert.That(rolls, Is.EqualTo(1));
        }
        [Test]
        public void DeadTargetCannotBeHitTwice()
        {
            enemy.Health.ApplyDamage(TargetBodyPart.Torso, 15); combat.SetRollSource(() => 0);
            var result = combat.TryAttack(player, enemy);
            Assert.That(result.TargetDestroyed, Is.True); Assert.That(result.TargetPartHPAfter, Is.Zero);
            Assert.That(combat.TryAttack(player, enemy).WasExecuted, Is.False);
            grid.TryGetCell(new Vector2Int(3, 3), out var cell); Assert.That(cell.Occupied, Is.False);
            Assert.That(turns.IsBattleOver, Is.True); Assert.That(turns.IsPlayerVictory, Is.True);
        }
        [Test]
        public void SoutheastAttackUsesStrongestCoverWithoutAdding()
        {
            grid.TryMoveUnit(player, new Vector2Int(5, 1));
            grid.TryGetCell(enemy.CurrentGridPosition, out var cell);
            cell.SetCover(CoverDirection.South, CoverType.Full);
            cell.SetCover(CoverDirection.East, CoverType.Full);
            var preview = combat.GetPreview(player, enemy);
            Assert.That(preview.CoverMultiplier, Is.EqualTo(.5f));
            Assert.That(preview.HitChance, Is.EqualTo(40));
            cell.SetCover(CoverDirection.SouthEast, CoverType.Full);
            preview = combat.GetPreview(player, enemy);
            Assert.That(preview.CoverMultiplier, Is.EqualTo(.5f));
            Assert.That(preview.HitChance, Is.EqualTo(40));
            combat.SetRollSource(() => 40);
            Assert.That(combat.TryAttack(player, enemy).Hit, Is.False);
        }
        [Test]
        public void DiagonalHalfFlankAppliesFullBonusAndIgnoresOtherDirections()
        {
            grid.TryMoveUnit(player, new Vector2Int(5, 1));
            grid.TryGetCell(enemy.CurrentGridPosition, out var cell);
            cell.SetCover(CoverDirection.South, CoverType.Half);
            var preview = combat.GetPreview(player, enemy);
            Assert.That(preview.CoverMultiplier, Is.EqualTo(.75f));
            Assert.That(preview.HitChance, Is.EqualTo(60));
            cell.SetCover(CoverDirection.East, CoverType.Half);
            cell.SetCover(CoverDirection.NorthWest, CoverType.Full);
            Assert.That(combat.GetPreview(player, enemy).HitChance, Is.EqualTo(60));
        }
        [TestCase(3, 5, CoverDirection.North)]
        [TestCase(5, 3, CoverDirection.East)]
        [TestCase(3, 1, CoverDirection.South)]
        [TestCase(1, 3, CoverDirection.West)]
        public void CardinalAttackIgnoresAllOtherCover(int x, int z, CoverDirection direction)
        {
            grid.TryMoveUnit(player, new Vector2Int(x, z));
            grid.TryGetCell(enemy.CurrentGridPosition, out var cell);
            foreach (CoverDirection other in System.Enum.GetValues(typeof(CoverDirection)))
                if (other != direction) cell.SetCover(other, CoverType.Full);
            Assert.That(combat.GetPreview(player, enemy).CoverMultiplier, Is.EqualTo(1f));
            cell.SetCover(direction, CoverType.Half);
            Assert.That(combat.GetPreview(player, enemy).CoverMultiplier, Is.EqualTo(.75f));
            Assert.That(combat.GetPreview(player, enemy).HitChance, Is.EqualTo(60));
        }
        [TestCase(5, 5, CoverDirection.NorthEast, CoverDirection.North)]
        [TestCase(5, 1, CoverDirection.SouthEast, CoverDirection.East)]
        [TestCase(1, 1, CoverDirection.SouthWest, CoverDirection.South)]
        [TestCase(1, 5, CoverDirection.NorthWest, CoverDirection.West)]
        public void DiagonalUsesFullFlankInsteadOfHalfFront(int x, int z, CoverDirection direction, CoverDirection flank)
        {
            grid.TryMoveUnit(player, new Vector2Int(x, z));
            grid.TryGetCell(enemy.CurrentGridPosition, out var cell);
            cell.SetCover(direction, CoverType.Half);
            cell.SetCover(flank, CoverType.Full);
            var preview = combat.GetPreview(player, enemy);
            Assert.That(preview.Direction, Is.EqualTo(direction));
            Assert.That(preview.CoverMultiplier, Is.EqualTo(.5f));
            Assert.That(preview.HitChance, Is.EqualTo(40));
        }
        [Test]
        public void CoverAffectsPreviewAndExecutionRegardlessOfMovement()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.transform.SetParent(root.transform);
            cube.transform.position = new Vector3(7, .5f, 5); cube.transform.localScale = new Vector3(2, 1, 2);
            var cover = cube.AddComponent<CoverObject>(); cover.Configure(CoverType.Half);
            covers.Build(grid, new[] { cover });
            for (int i = 0; i < 3; i++) enemy.Mobility.RecordCellMoved();
            var preview = combat.GetPreview(player, enemy);
            Assert.That(preview.Direction, Is.EqualTo(CoverDirection.SouthWest));
            Assert.That(preview.HitChance, Is.EqualTo(60));
            combat.SetRollSource(() => 60);
            var result = combat.TryAttack(player, enemy);
            Assert.That(result.HitChance, Is.EqualTo(60)); Assert.That(result.Hit, Is.False);
        }
    }
}
