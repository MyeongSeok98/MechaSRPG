using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class IndirectCombatTests
    {
        private BackpackTestBattle b;
        [TearDown] public void Cleanup() => b?.Dispose();
        private void Create(int radius = 1, int minParts = 1, int maxParts = 1)
        { b = new BackpackTestBattle(); IndirectTestSupport.Configure(b, radius, minParts, maxParts); }
        private void Block(int x, int z)
        {
            var obj = new GameObject("Blast wall"); obj.transform.SetParent(b.Root.transform); obj.transform.position = b.Grid.GetWorldPosition(new Vector2Int(x, z));
            var obstacle = obj.AddComponent<GridObstacle>(); obstacle.Collider.size = Vector3.one; b.Grid.RegisterObstacle(obstacle.Collider);
        }
        [Test] public void DiamondIncludesBoundaryButExcludesDiagonalOutsideAndUsesIndependentDamage()
        {
            Create(); var boundary = IndirectTestSupport.AddUnit(b, new Vector2Int(3, 2), UnitTeam.Enemy);
            var outside = IndirectTestSupport.AddUnit(b, new Vector2Int(4, 2), UnitTeam.Enemy);
            var damage = new Queue<int>(new[] { 100, 175 }); b.Combat.SetDamageRollSource((min, max) => damage.Dequeue());
            IndirectTestSupport.Rolls(b.Combat, (min, max) => min, count => 0);
            var result = IndirectTestSupport.Fire(b, new Vector2Int(3, 1));
            Assert.That(IndirectTestSupport.Field<bool>(result, "WasExecuted"), Is.True);
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(900));
            Assert.That(boundary.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(825));
            Assert.That(outside.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(1000)); Assert.That(damage, Is.Empty);
        }
        [Test] public void FriendlyAndSelfDamageIsGuaranteedWithoutHitCriticalOrArmorModifiers()
        {
            Create(2); var ally = IndirectTestSupport.AddUnit(b, new Vector2Int(2, 2), UnitTeam.Player);
            b.Combat.SetRollSource(() => { Assert.Fail("No hit roll"); return 99; });
            b.Combat.SetCriticalRollSource(() => { Assert.Fail("No crit roll"); return 0; });
            IndirectTestSupport.Rolls(b.Combat, (min, max) => min, count => 0);
            var result = IndirectTestSupport.Fire(b, new Vector2Int(2, 1));
            foreach (var unit in new[] { b.Player, b.Enemy, ally }) Assert.That(unit.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(900));
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(2)); Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(5));
            Assert.That(IndirectTestSupport.Results(result).Length, Is.EqualTo(3));
        }
        [Test] public void UnseenEnemiesAreHitWithoutPreviewOrResultIdentityLeaks()
        {
            Create(2); var hidden = IndirectTestSupport.AddUnit(b, new Vector2Int(6, 1), UnitTeam.Enemy);
            b.Player.Vision.Configure(1); b.Turns.Visibility.Initialize(b.Grid, new[] { b.Player, b.Enemy, hidden });
            IndirectTestSupport.Rolls(b.Combat, (min, max) => min, count => 0);
            var preview = IndirectTestSupport.Preview(b, new Vector2Int(5, 1));
            Assert.That(IndirectTestSupport.Field<bool>(preview, "CanAttack"), Is.True);
            Assert.That(IndirectTestSupport.Field<IReadOnlyList<BattleUnit>>(preview, "VisibleTargets"), Is.Empty);
            var result = IndirectTestSupport.Fire(b, new Vector2Int(5, 1));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(900)); Assert.That(hidden.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(900));
            Assert.That(IndirectTestSupport.Results(result), Is.Empty);
        }
        [Test] public void TraitedProjectileCrossesBuildingsButBlastDoesNot()
        {
            Create(2); var behind = IndirectTestSupport.AddUnit(b, new Vector2Int(5, 1), UnitTeam.Enemy); Block(2, 1); Block(4, 1);
            JsonUtility.FromJsonOverwrite("{\"traits\":[0]}", b.Weapon);
            IndirectTestSupport.Rolls(b.Combat, (min, max) => min, count => 0);
            Assert.That(b.Grid.HasClearShot(b.Player.CurrentGridPosition, new Vector2Int(3, 1)), Is.False);
            Assert.That(IndirectTestSupport.Field<bool>(IndirectTestSupport.Preview(b, new Vector2Int(3, 1)), "CanAttack"), Is.True);
            IndirectTestSupport.Fire(b, new Vector2Int(3, 1));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(900)); Assert.That(behind.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(1000));
        }
        [Test] public void EachTargetDrawsPartCountAndEachSelectedPartDrawsDamage()
        {
            Create(1, 1, 2); var second = IndirectTestSupport.AddUnit(b, new Vector2Int(3, 2), UnitTeam.Enemy);
            var counts = new Queue<int>(new[] { 2, 1 }); var damages = new Queue<int>(new[] { 100, 200, 150 });
            IndirectTestSupport.Rolls(b.Combat, (min, max) => counts.Dequeue(), count => 0); b.Combat.SetDamageRollSource((min, max) => damages.Dequeue());
            IndirectTestSupport.Fire(b, new Vector2Int(3, 1));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(900)); Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(800));
            Assert.That(second.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(850)); Assert.That(counts, Is.Empty); Assert.That(damages, Is.Empty);
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Cockpit), Is.EqualTo(1000));
        }
        [Test] public void BrokenPartsAndCockpitAreExcludedAndInsufficientCandidatesAreClipped()
        {
            Create(1, 6, 6); b.Enemy.Health.ApplyDamage(TargetBodyPart.Head, 1000); b.Enemy.Health.ApplyDamage(TargetBodyPart.LeftArm, 1000);
            IndirectTestSupport.Rolls(b.Combat, (min, max) => min, count => 0);
            var results = IndirectTestSupport.Results(IndirectTestSupport.Fire(b, new Vector2Int(3, 1)));
            var hits = IndirectTestSupport.Field<IReadOnlyList<PartDamageResult>>(results.Single(), "Parts");
            Assert.That(hits.Count, Is.EqualTo(4)); Assert.That(hits.Select(x => x.Part).Distinct().Count(), Is.EqualTo(4));
            Assert.That(hits.Any(x => x.Part == TargetBodyPart.Cockpit || x.Part == TargetBodyPart.Head || x.Part == TargetBodyPart.LeftArm), Is.False);
        }
        [Test] public void EmptyShotsAndUnexploredCentersAreAllowedWithoutDamageRolls()
        {
            Create(); IndirectTestSupport.Rolls(b.Combat, (min, max) => { Assert.Fail("No target"); return min; }, count => 0);
            b.Combat.SetDamageRollSource((min, max) => { Assert.Fail("No target"); return min; });
            b.Player.Vision.Configure(0);
            b.Turns.Visibility.Initialize(b.Grid, new[] { b.Player, b.Enemy });
            var center = new Vector2Int(1, 7); Assert.That(b.Turns.Visibility.GetCellVisibility(UnitTeam.Player, center), Is.EqualTo(CellVisibility.Unexplored));
            var result = IndirectTestSupport.Fire(b, center); Assert.That(IndirectTestSupport.Field<bool>(result, "WasExecuted"), Is.True);
            Assert.That(IndirectTestSupport.Results(result), Is.Empty); Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [TestCase(-1, 1)] [TestCase(11, 11)] public void InvalidCenterConsumesNothing(int x, int z)
        {
            Create(); var result = IndirectTestSupport.Fire(b, new Vector2Int(x, z));
            Assert.That(IndirectTestSupport.Field<bool>(result, "WasExecuted"), Is.False);
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(5)); Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(6));
        }
        [Test] public void PreviewNeverConsumesRandomnessAndDirectAndGuardAPIsRejectIndirect()
        {
            Create(); IndirectTestSupport.Rolls(b.Combat, (min, max) => { Assert.Fail("Preview RNG"); return min; }, count => { Assert.Fail("Preview RNG"); return 0; });
            Assert.That(IndirectTestSupport.Field<bool>(IndirectTestSupport.Preview(b, new Vector2Int(3, 1)), "CanAttack"), Is.True);
            Assert.That(b.Combat.GetWeaponPreview(b.Player, b.Enemy, WeaponSlot.RightHand).CanAttack, Is.False);
            Assert.That(b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.RightHand).WasExecuted, Is.False);
            Assert.That(b.Overwatch.TryBegin(b.Player, WeaponSlot.RightHand, FacingDirection.East), Is.False);
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [Test] public void AllUnitsReceiveOneShotBeforeBattleEndEvenIfShooterDiesFirst()
        {
            Create(2); IndirectTestSupport.Heal(b.Player, 1); IndirectTestSupport.Heal(b.Enemy, 1);
            b.Player.Health.StateChanged += () => Assert.That(b.Turns.IsBattleOver, Is.False, "A shot must finish before ending battle.");
            IndirectTestSupport.Rolls(b.Combat, (min, max) => min, count => 0);
            IndirectTestSupport.Fire(b, new Vector2Int(2, 1));
            Assert.That(b.Player.Health.State, Is.EqualTo(MechState.Destroyed)); Assert.That(b.Enemy.Health.State, Is.EqualTo(MechState.Destroyed));
            Assert.That(b.Turns.IsBattleOver, Is.True);
        }
        [Test] public void BackpackIndirectShotUsesOnlyItsOwnLoadedAmmoAndCannotReload()
        {
            b = new BackpackTestBattle(capacity: 1); IndirectTestSupport.Configure(b);
            IndirectTestSupport.Rolls(b.Combat, (min, max) => min, count => 0);
            var result = IndirectTestSupport.Fire(b, new Vector2Int(3, 1), WeaponSlot.Backpack);
            Assert.That(IndirectTestSupport.Field<bool>(result, "WasExecuted"), Is.True);
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(900));
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.Backpack).Loaded, Is.Zero);
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(1));
            Assert.That(b.Combat.TryReloadWithWeapon(b.Player, WeaponSlot.Backpack), Is.False);
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [Test] public void TraitedBuildingImpactCellCanStillBlastOutward()
        {
            Create(); Block(3, 2); IndirectTestSupport.Rolls(b.Combat, (min, max) => min, count => 0);
            JsonUtility.FromJsonOverwrite("{\"traits\":[0]}", b.Weapon);
            Assert.That(IndirectTestSupport.Field<bool>(IndirectTestSupport.Preview(b, new Vector2Int(3, 2)), "CanAttack"), Is.True);
            IndirectTestSupport.Fire(b, new Vector2Int(3, 2));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(900));
        }
        [Test] public void RadiusThreeDiamondPreviewAndDamageExcludeCircleOnlyDiagonal()
        {
            Create(3); var center = new Vector2Int(4, 4);
            var boundary = IndirectTestSupport.AddUnit(b, new Vector2Int(7, 4), UnitTeam.Enemy);
            var diagonalBoundary = IndirectTestSupport.AddUnit(b, new Vector2Int(5, 6), UnitTeam.Enemy);
            var outside = IndirectTestSupport.AddUnit(b, new Vector2Int(6, 6), UnitTeam.Enemy);
            b.Turns.Visibility.Initialize(b.Grid, new[] { b.Player, b.Enemy, boundary, diagonalBoundary, outside });
            Assert.That(b.Combat.GetIndirectBlastCells(center, 3), Has.Count.EqualTo(25));
            var preview = b.Combat.GetIndirectPreview(b.Player, center, WeaponSlot.RightHand);
            Assert.That(preview.VisibleTargets, Has.Member(boundary));
            Assert.That(preview.VisibleTargets, Has.Member(diagonalBoundary));
            Assert.That(preview.VisibleTargets, Has.No.Member(outside));
            IndirectTestSupport.Rolls(b.Combat, (min, max) => min, count => 0);
            Assert.That(b.Combat.TryIndirectAttack(b.Player, center, WeaponSlot.RightHand).WasExecuted, Is.True);
            Assert.That(boundary.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(900));
            Assert.That(diagonalBoundary.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(900));
            Assert.That(outside.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(1000));
        }
    }
}
