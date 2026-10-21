using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class WeaponCombatTests
    {
        private GameObject root;
        private GridManager grid;
        private TurnManager turns;
        private CombatSystem combat;
        private BattleUnit player, enemy;
        private UnitWeaponLoadout loadout;
        private WeaponData rifle;
        private readonly List<WeaponData> weapons = new List<WeaponData>();
        private int hitRolls, damageRolls;
        [SetUp] public void Setup()
        {
            hitRolls = damageRolls = 0;
            root = new GameObject("Weapon combat tests");
            grid = root.AddComponent<GridManager>(); grid.Configure(16, 12, 2); grid.Generate();
            player = Unit(new Vector2Int(1, 1), UnitTeam.Player);
            enemy = Unit(new Vector2Int(3, 1), UnitTeam.Enemy);
            turns = root.AddComponent<TurnManager>(); turns.Initialize(new[] { player, enemy });
            var visibility = root.AddComponent<VisibilitySystem>(); visibility.Initialize(grid, new[] { player, enemy });
            turns.ConfigureVisibility(visibility);
            combat = root.AddComponent<CombatSystem>(); combat.Initialize(grid, root.AddComponent<CoverSystem>(), turns);
            rifle = Weapon(); loadout = player.gameObject.EnsureTestLoadout();
            loadout.Configure(null, GripMode.OneHand, rifle, GripMode.OneHand);
            combat.SetRollSource(() => { hitRolls++; return 0; });
            combat.SetDamageRollSource((min, max) => { damageRolls++; return max; });
        }
        private BattleUnit Unit(Vector2Int position, UnitTeam team)
        {
            var unit = new GameObject(team.ToString()).AddTestBattleUnit(); unit.transform.SetParent(root.transform);
            unit.Configure(position, 5); unit.ConfigureTeam(team); Assert.That(unit.Initialize(grid), Is.True);
            unit.Vision.Configure(20); return unit;
        }
        private WeaponData Weapon(AttackType type = AttackType.Ranged, int min = 4, int max = 6, int range = 7, int cost = 3)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponData>(); weapons.Add(weapon);
            var data = new SerializedObject(weapon);
            data.FindProperty("attackType").intValue = (int)type;
            data.FindProperty("displayName").stringValue = type + " weapon";
            data.FindProperty("minDamage").intValue = min; data.FindProperty("maxDamage").intValue = max;
            data.FindProperty("range").intValue = range; data.FindProperty("apCost").intValue = cost;
            data.ApplyModifiedPropertiesWithoutUndo(); return weapon;
        }
        private static void Set(WeaponData weapon, string field, object value) => typeof(WeaponData)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, value);
        private void Bands() => Set(rifle, "distanceBands", new[] {
            new DistanceAccuracyBand { MaxDistance = 2, Multiplier = .9f },
            new DistanceAccuracyBand { MaxDistance = 5, Multiplier = 1f },
            new DistanceAccuracyBand { MaxDistance = 7, Multiplier = .8f } });
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(root); foreach (var weapon in weapons) Object.DestroyImmediate(weapon); weapons.Clear();
        }
        [Test] public void PreviewNeverConsumesRandom()
        {
            for (int i = 0; i < 10; i++)
            {
                var preview = combat.GetWeaponPreview(player, enemy, WeaponSlot.RightHand, TargetBodyPart.Cockpit);
                Assert.That(preview.CanAttack, Is.True, preview.FailureReason);
                Assert.That(preview.WeaponSlot, Is.EqualTo(WeaponSlot.RightHand));
                Assert.That(preview.WeaponName, Is.EqualTo(rifle.DisplayName));
                Assert.That(preview.MinDamage, Is.EqualTo(4)); Assert.That(preview.MaxDamage, Is.EqualTo(6));
                Assert.That(combat.GetWeaponRangeCells(player, WeaponSlot.RightHand), Is.Not.Empty);
            }
            Assert.That(hitRolls, Is.Zero); Assert.That(damageRolls, Is.Zero);
        }
        [TestCase(false, 4)] [TestCase(true, 6)]
        public void HitRollsInclusiveDamageOnce(bool maximum, int expected)
        {
            combat.SetDamageRollSource((min, max) => {
                damageRolls++; Assert.That(min, Is.EqualTo(4)); Assert.That(max, Is.EqualTo(6)); return maximum ? max : min;
            });
            var preview = combat.GetWeaponPreview(player, enemy, WeaponSlot.RightHand);
            var result = combat.TryAttackWithWeapon(player, enemy, WeaponSlot.RightHand);
            Assert.That(result.WasExecuted, Is.True, result.FailureReason); Assert.That(result.Hit, Is.True);
            Assert.That(result.HitChance, Is.EqualTo(preview.HitChance));
            Assert.That(result.DamageDealt, Is.EqualTo(expected));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20 - expected));
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(hitRolls, Is.EqualTo(1)); Assert.That(damageRolls, Is.EqualTo(1));
        }
        [Test] public void MissNeverRollsDamageButCostsAP()
        {
            combat.SetRollSource(() => { hitRolls++; return 99; });
            var result = combat.TryAttackWithWeapon(player, enemy, WeaponSlot.RightHand);
            Assert.That(result.WasExecuted, Is.True); Assert.That(result.Hit, Is.False);
            Assert.That(result.DamageDealt, Is.Zero); Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2)); Assert.That(hitRolls, Is.EqualTo(1)); Assert.That(damageRolls, Is.Zero);
        }
        [TestCase("slot")] [TestCase("ap")] [TestCase("sight")] [TestCase("range")]
        [TestCase("building")] [TestCase("disabled")] [TestCase("turn")] [TestCase("self")]
        [TestCase("invalidData")] [TestCase("loadoutDisabled")]
        public void InvalidAttackNeverSpendsAPOrRolls(string condition)
        {
            WeaponSlot slot = WeaponSlot.RightHand;
            if (condition == "slot") slot = WeaponSlot.LeftHand;
            if (condition == "ap") player.ActionPoints.TrySpend(5);
            if (condition == "sight") turns.Visibility.enabled = false;
            if (condition == "range") grid.TryMoveUnit(enemy, new Vector2Int(10, 1));
            if (condition == "building")
            {
                var wall = new GameObject("Wall"); wall.transform.SetParent(root.transform);
                wall.transform.position = grid.GetWorldPosition(new Vector2Int(2, 1));
                grid.RegisterObstacle(wall.AddComponent<GridObstacle>().Collider);
            }
            if (condition == "disabled") player.Combat.enabled = false;
            if (condition == "turn") turns.enabled = false;
            if (condition == "invalidData") Set(rifle, "range", 0);
            if (condition == "loadoutDisabled") loadout.enabled = false;
            int before = player.ActionPoints.CurrentPoints;
            var result = combat.TryAttackWithWeapon(player, condition == "self" ? player : enemy, slot);
            Assert.That(result.WasExecuted, Is.False); Assert.That(result.FailureReason, Is.Not.Empty);
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(before));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(hitRolls, Is.Zero); Assert.That(damageRolls, Is.Zero);
        }
        [Test] public void FixedDamageAndRemainingPartHP()
        {
            Set(rifle, "minDamage", 5); Set(rifle, "maxDamage", 5);
            enemy.Health.ApplyDamage(TargetBodyPart.Torso, 19);
            var result = combat.TryAttackWithWeapon(player, enemy, WeaponSlot.RightHand);
            Assert.That(result.WasExecuted, Is.True); Assert.That(result.DamageDealt, Is.EqualTo(1));
            Assert.That(damageRolls, Is.EqualTo(1));
        }
        [TestCase(2, .9f, 72)] [TestCase(3, 1f, 80)] [TestCase(5, 1f, 80)]
        [TestCase(6, .8f, 64)] [TestCase(7, .8f, 64)]
        public void DistanceBandBoundariesMatchPreviewRangeAndAttack(int distance, float multiplier, int chance)
        {
            Bands(); grid.TryMoveUnit(enemy, new Vector2Int(1 + distance, 1));
            var preview = combat.GetWeaponPreview(player, enemy, WeaponSlot.RightHand);
            Assert.That(preview.CanAttack, Is.True, preview.FailureReason);
            Assert.That(preview.DistanceMultiplier, Is.EqualTo(multiplier)); Assert.That(preview.HitChance, Is.EqualTo(chance));
            grid.TryGetCell(enemy.CurrentGridPosition, out var cell);
            Assert.That(combat.GetWeaponRangeCells(player, WeaponSlot.RightHand), Has.Member(cell));
            combat.SetRollSource(() => chance);
            var result = combat.TryAttackWithWeapon(player, enemy, WeaponSlot.RightHand);
            Assert.That(result.HitChance, Is.EqualTo(chance)); Assert.That(result.Hit, Is.False);
        }
        [Test] public void GripDistanceAndOtherMultipliersWithFinalRoundingIgnoreMovement()
        {
            Assert.That(CombatMath.CalculateAttackHitChance(80, 1, 1, 1, 1.1f, .8f), Is.EqualTo(70));
            Assert.That(CombatMath.CalculateAttackHitChance(80, .5f, .5f, .5f, 1.1f, .8f), Is.EqualTo(9));
            Assert.That(CombatMath.CalculateAttackHitChance(100, 1, 1, 1, 1.2f, 1), Is.EqualTo(100));
            Bands(); loadout.Configure(null, GripMode.OneHand, rifle, GripMode.TwoHands);
            grid.TryMoveUnit(enemy, new Vector2Int(7, 1));
            for (int i = 0; i < 3; i++) enemy.Mobility.RecordCellMoved();
            var preview = combat.GetWeaponPreview(player, enemy, WeaponSlot.RightHand);
            Assert.That(preview.GripMultiplier, Is.EqualTo(1.1f)); Assert.That(preview.HitChance, Is.EqualTo(70));
        }
        [Test] public void LargeFiniteGripAndZeroDistanceMultiplierRemainValid()
        {
            Assert.That(CombatMath.CalculateAttackHitChance(80, 1, 1, 1, float.MaxValue, 0), Is.Zero);
            Assert.That(CombatMath.CalculateAttackHitChance(80, 1, 1, 1, float.MaxValue, float.MaxValue), Is.EqualTo(100));
        }
        [Test] public void ExistingHeadCoverAndPartMultipliersStillApply()
        {
            Bands(); loadout.Configure(null, GripMode.OneHand, rifle, GripMode.TwoHands);
            player.Health.ApplyDamage(TargetBodyPart.Head, 6);
            grid.TryGetCell(enemy.CurrentGridPosition, out var cell); cell.SetCover(CoverDirection.West, CoverType.Full);
            var preview = combat.GetWeaponPreview(player, enemy, WeaponSlot.RightHand, TargetBodyPart.Cockpit);
            Assert.That(preview.HeadMultiplier, Is.EqualTo(player.HeadHitMultiplier));
            int expected = Mathf.FloorToInt(80 * 1.1f * .9f * player.HeadHitMultiplier * .45f * .5f + .5f);
            Assert.That(preview.HitChance, Is.EqualTo(expected));
        }
        [Test] public void MeleeIgnoresCoverPartsAndDistanceButUsesGripAndRandomDamage()
        {
            var blade = Weapon(AttackType.Melee, 7, 9, 2);
            Set(blade, "distanceBands", new[] { new DistanceAccuracyBand { MaxDistance = 2, Multiplier = 0 } });
            loadout.Configure(blade, GripMode.TwoHands, null, GripMode.OneHand);
            grid.TryGetCell(enemy.CurrentGridPosition, out var cell); cell.SetCover(CoverDirection.West, CoverType.Full);
            var preview = combat.GetWeaponPreview(player, enemy, WeaponSlot.LeftHand, TargetBodyPart.Cockpit);
            Assert.That(preview.Type, Is.EqualTo(AttackType.Melee)); Assert.That(preview.TargetPart, Is.EqualTo(TargetBodyPart.Torso));
            Assert.That(preview.DistanceMultiplier, Is.EqualTo(1)); Assert.That(preview.CoverMultiplier, Is.EqualTo(1));
            Assert.That(preview.PartMultiplier, Is.EqualTo(1)); Assert.That(preview.HitChance, Is.EqualTo(88));
            var result = combat.TryAttackWithWeapon(player, enemy, WeaponSlot.LeftHand, TargetBodyPart.Cockpit);
            Assert.That(result.DamageDealt, Is.EqualTo(9)); Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Cockpit), Is.EqualTo(6));
        }
        [Test] public void ExplicitSlotNeverFallsBackAndLegacyWrappersUseMatchingFirstSlot()
        {
            var weak = Weapon(AttackType.Ranged, 1, 2, 2, 1);
            loadout.Configure(weak, GripMode.OneHand, rifle, GripMode.OneHand);
            Assert.That(combat.GetWeaponPreview(player, enemy, WeaponSlot.LeftHand).MaxDamage, Is.EqualTo(2));
            Assert.That(combat.GetPreview(player, enemy).WeaponSlot, Is.EqualTo(WeaponSlot.RightHand));
            Assert.That(combat.GetWeaponPreview(player, enemy, (WeaponSlot)99).CanAttack, Is.False);
            Assert.That(combat.GetWeaponRangeCells(player, (WeaponSlot)99), Is.Empty);
            grid.TryMoveUnit(enemy, new Vector2Int(5, 1));
            Assert.That(combat.TryAttackWithWeapon(player, enemy, WeaponSlot.LeftHand).WasExecuted, Is.False);
            Assert.That(hitRolls, Is.Zero); Assert.That(damageRolls, Is.Zero);
        }
        [Test] public void FireRevalidatesWeaponAndDoesNotModifySharedAsset()
        {
            string before = EditorJsonUtility.ToJson(rifle);
            Assert.That(combat.GetWeaponPreview(player, enemy, WeaponSlot.RightHand).CanAttack, Is.True);
            loadout.Configure(null, GripMode.OneHand, null, GripMode.OneHand);
            Assert.That(combat.TryAttackWithWeapon(player, enemy, WeaponSlot.RightHand).WasExecuted, Is.False);
            Assert.That(hitRolls, Is.Zero); Assert.That(damageRolls, Is.Zero);
            loadout.Configure(null, GripMode.OneHand, rifle, GripMode.OneHand);
            combat.TryAttackWithWeapon(player, enemy, WeaponSlot.RightHand);
            Assert.That(EditorJsonUtility.ToJson(rifle), Is.EqualTo(before));
        }
        [Test] public void DefaultRollIncludesEndpointsAndDoesNotOverflowMaximum()
        {
            combat.SetDamageRollSource(null); var seen = new HashSet<int>();
            var oldRandom = Random.state;
            try
            {
                Random.InitState(42);
                for (int i = 0; i < 40; i++)
                {
                    player.ActionPoints.ResetForTurn();
                    var result = combat.TryAttackWithWeapon(player, enemy, WeaponSlot.RightHand, TargetBodyPart.LeftLeg);
                    Assert.That(result.WasExecuted, Is.True); seen.Add(result.DamageDealt);
                    // Reset target health for independent rolls, avoiding a destroyed part's HP cap.
                    enemy.Health.Initialize();
                }
                Assert.That(seen, Has.Member(4)); Assert.That(seen, Has.Member(6));
                Set(rifle, "minDamage", int.MaxValue); Set(rifle, "maxDamage", int.MaxValue);
                player.ActionPoints.ResetForTurn();
                Assert.That(combat.TryAttackWithWeapon(player, enemy, WeaponSlot.RightHand).DamageDealt, Is.EqualTo(20));
            }
            finally { Random.state = oldRandom; }
        }
    }
}
