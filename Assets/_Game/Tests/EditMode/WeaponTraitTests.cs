using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class WeaponTraitTests
    {
        private BackpackTestBattle b;
        private static readonly Vector2Int Center = new Vector2Int(3, 1);
        [SetUp] public void Setup()
        { b = new BackpackTestBattle(); IndirectTestSupport.Configure(b, radius: 2); }
        [TearDown] public void Cleanup() => b.Dispose();
        private static bool HasTrait(WeaponData weapon, int value = 0)
        {
            var type = typeof(WeaponData).Assembly.GetType("MechaXCOM.WeaponTrait");
            Assert.That(type, Is.Not.Null, "WeaponTrait enum is required.");
            var method = typeof(WeaponData).GetMethod("HasTrait", new[] { type });
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(weapon, new[] { Enum.ToObject(type, value) });
        }
        private void Grant() => JsonUtility.FromJsonOverwrite("{\"traits\":[0]}", b.Weapon);
        private void Building(int x, int z, bool blocksShots = true)
        {
            var obj = new GameObject("Launch path obstacle"); obj.transform.SetParent(b.Root.transform);
            obj.transform.position = b.Grid.GetWorldPosition(new Vector2Int(x, z));
            var obstacle = obj.AddComponent<GridObstacle>();
            ExplosiveTestSupport.Set(obstacle, "blocksShots", blocksShots);
            obstacle.Collider.size = Vector3.one; b.Grid.RegisterObstacle(obstacle.Collider);
        }
        private bool CanAim(Vector2Int center) => b.Combat.GetIndirectPreview(b.Player, center, WeaponSlot.RightHand).CanAttack;
        private bool InRange(Vector2Int center) => b.Combat.GetWeaponRangeCells(b.Player, WeaponSlot.RightHand).Any(c => c.Position == center);

        [Test] public void LegacyEmptyAndNullListsHaveNoTraits()
        {
            Assert.That(HasTrait(b.Weapon), Is.False);
            JsonUtility.FromJsonOverwrite("{\"traits\":null}", b.Weapon);
            Assert.That(HasTrait(b.Weapon), Is.False); Assert.That(b.Weapon.IsValid(out _), Is.True);
            JsonUtility.FromJsonOverwrite("{\"traits\":[]}", b.Weapon); Assert.That(HasTrait(b.Weapon), Is.False);
        }
        [Test] public void TraitListSurvivesSerializationAndDoesNotGrantUnknownTraits()
        {
            Grant(); Assert.That(HasTrait(b.Weapon), Is.True); Assert.That(HasTrait(b.Weapon, 999), Is.False);
            var copy = ScriptableObject.CreateInstance<WeaponData>();
            try { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(b.Weapon), copy); Assert.That(HasTrait(copy), Is.True); }
            finally { Object.DestroyImmediate(copy); }
        }
        [TestCase("[999]")] [TestCase("[0,0]")]
        public void UnknownOrDuplicateTraitsAreInvalid(string list)
        {
            JsonUtility.FromJsonOverwrite("{\"traits\":" + list + "}", b.Weapon);
            Assert.That(b.Weapon.IsValid(out _), Is.False);
        }
        [Test] public void BuildingBlocksDefaultIndirectAimRangeAndFireWithoutResourceOrRandomUse()
        {
            Building(2, 1);
            b.Combat.SetDamageRollSource((min, max) => { Assert.Fail("Rejected shot must not roll."); return min; });
            var preview = b.Combat.GetIndirectPreview(b.Player, Center, WeaponSlot.RightHand);
            Assert.That(preview.CanAttack, Is.False); Assert.That(preview.FailureReason, Does.Contain("건물"));
            Assert.That(InRange(Center), Is.False);
            Assert.That(b.Combat.TryIndirectAttack(b.Player, Center, WeaponSlot.RightHand).WasExecuted, Is.False);
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(6));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(1000));
        }
        [Test] public void TraitAllowsBuildingCrossingButDoesNotBypassBlastWall()
        {
            Grant(); Building(2, 1); Building(4, 1);
            var behind = IndirectTestSupport.AddUnit(b, new Vector2Int(5, 1), UnitTeam.Enemy);
            b.Combat.SetIndirectPartIndexRollSource(count => 0);
            Assert.That(HasTrait(b.Weapon), Is.True); Assert.That(CanAim(Center), Is.True); Assert.That(InRange(Center), Is.True);
            Assert.That(b.Combat.TryIndirectAttack(b.Player, Center, WeaponSlot.RightHand).WasExecuted, Is.True);
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(900));
            Assert.That(behind.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(1000));
        }
        [TestCase(false)] [TestCase(true)] public void TraitNeverBypassesRangeOrMapBounds(bool trait)
        {
            if (trait) Grant();
            foreach (var cell in new[] { new Vector2Int(11, 11), new Vector2Int(-1, 1) })
            { Assert.That(CanAim(cell), Is.False); Assert.That(InRange(cell), Is.False); }
        }
        [TestCase(false)] [TestCase(true)] public void CornerTouchingBuildingBlocksDefaultAndTraitCanCross(bool trait)
        {
            if (trait) Grant(); Building(2, 1); var center = new Vector2Int(3, 3);
            Assert.That(CanAim(center), Is.EqualTo(trait)); Assert.That(InRange(center), Is.EqualTo(trait));
        }
        [TestCase(CoverType.Half)] [TestCase(CoverType.Full)] public void NormalCoverDoesNotBlockIndirectLaunch(CoverType type)
        {
            var obj = new GameObject("Normal cover"); obj.transform.SetParent(b.Root.transform);
            obj.transform.position = b.Grid.GetWorldPosition(new Vector2Int(2, 1));
            var cover = obj.AddComponent<CoverObject>(); cover.Configure(type); cover.Collider.size = Vector3.one;
            b.Cover.Build(b.Grid, new[] { cover });
            Assert.That(CanAim(Center), Is.True); Assert.That(InRange(Center), Is.True);
        }
        [Test] public void RavinesAndUnitsDoNotBlockIndirectLaunch()
        {
            Building(2, 1, false); var ally = IndirectTestSupport.AddUnit(b, new Vector2Int(2, 2), UnitTeam.Player);
            Assert.That(CanAim(Center), Is.True); Assert.That(InRange(Center), Is.True);
            Assert.That(CanAim(ally.CurrentGridPosition), Is.True);
        }
        [Test] public void TraitCannotGiveDirectWeaponBuildingPenetration()
        {
            Grant(); ExplosiveTestSupport.Set(b.Weapon, "rangedAttackMode", RangedAttackMode.Direct); Building(2, 1);
            Assert.That(b.Combat.GetWeaponPreview(b.Player, b.Enemy, WeaponSlot.RightHand).CanAttack, Is.False);
            Assert.That(InRange(Center), Is.False);
        }
        [TestCase(false)] [TestCase(true)] public void BuildingDestinationRequiresTrait(bool trait)
        {
            if (trait) Grant(); Building(3, 2); var center = new Vector2Int(3, 2);
            Assert.That(CanAim(center), Is.EqualTo(trait)); Assert.That(InRange(center), Is.EqualTo(trait));
        }
        [Test] public void FireRevalidatesLaunchPathAfterPreview()
        {
            Assert.That(CanAim(Center), Is.True); Building(2, 1);
            Assert.That(b.Combat.TryIndirectAttack(b.Player, Center, WeaponSlot.RightHand).WasExecuted, Is.False);
            Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(6));
        }
        [Test] public void BothHandAndBackpackSlotsUseTheSameLaunchRestriction()
        {
            Building(2, 1);
            foreach (var slot in new[] { WeaponSlot.LeftHand, WeaponSlot.RightHand, WeaponSlot.Backpack })
                Assert.That(b.Combat.GetIndirectPreview(b.Player, Center, slot).CanAttack, Is.False);
            Grant();
            foreach (var slot in new[] { WeaponSlot.LeftHand, WeaponSlot.RightHand, WeaponSlot.Backpack })
                Assert.That(b.Combat.GetIndirectPreview(b.Player, Center, slot).CanAttack, Is.True);
        }
    }
}
