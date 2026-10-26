using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class MechaAccuracyTests
    {
        private MechaAccuracyTestBattle battle;
        [TearDown] public void Cleanup() => battle?.Dispose();
        private static float Multiplier(int accuracy, int evasion)
        {
            var method = typeof(CombatMath).GetMethod("CalculateMechaHitMultiplier");
            Assert.That(method, Is.Not.Null, "Mecha hit correction needs a common balancing formula.");
            return (float)method.Invoke(null, new object[] { accuracy, evasion });
        }
        private static int Stat(BattleUnit unit, string name)
        {
            var property = typeof(BattleUnit).GetProperty(name); Assert.That(property, Is.Not.Null, name);
            return (int)property.GetValue(unit);
        }
        [TestCase(0, 0, 1f)] [TestCase(50, 0, 1.5f)] [TestCase(0, 50, .6f)] [TestCase(50, 50, 1f)]
        [TestCase(25, 25, 1f)] [TestCase(25, 0, 1.25f)] [TestCase(0, 25, .8f)]
        [TestCase(40, 10, 1.3f)] [TestCase(10, 40, .76f)]
        public void RequestedTableAndIntermediateRatings(int accuracy, int evasion, float expected)
            => Assert.That(Multiplier(accuracy, evasion), Is.EqualTo(expected).Within(.00001f));
        [TestCase(-10, 100, .6f)] [TestCase(100, -10, 1.5f)]
        public void FormulaClampsRatingsToAllowedRange(int accuracy, int evasion, float expected)
            => Assert.That(Multiplier(accuracy, evasion), Is.EqualTo(expected).Within(.00001f));

        [TestCase(0, 0, 80)] [TestCase(50, 0, 100)] [TestCase(0, 50, 48)] [TestCase(50, 50, 80)]
        public void RangedPreviewAppliesAttackerAccuracyAndTargetEvasion(int accuracy, int evasion, int expected)
        {
            battle = new MechaAccuracyTestBattle(playerAccuracy: accuracy, enemyEvasion: evasion);
            var preview = battle.Combat.GetWeaponPreview(battle.Player, battle.Enemy, WeaponSlot.RightHand);
            Assert.That(preview.CanAttack, Is.True, preview.FailureReason);
            Assert.That(preview.HitChance, Is.EqualTo(expected));
            var field = typeof(AttackPreview).GetField("MechaMultiplier"); Assert.That(field, Is.Not.Null);
            Assert.That((float)field.GetValue(preview), Is.EqualTo(Multiplier(accuracy, evasion)).Within(.00001f));
        }
        [TestCase(0, 50, 57)] [TestCase(50, 0, 100)]
        public void MeleeAlsoUsesMechaRatings(int accuracy, int evasion, int expected)
        {
            battle = new MechaAccuracyTestBattle(playerAccuracy: accuracy, enemyEvasion: evasion);
            var preview = battle.Combat.GetWeaponPreview(battle.Player, battle.Enemy, WeaponSlot.LeftHand);
            Assert.That(preview.CanAttack, Is.True, preview.FailureReason); Assert.That(preview.HitChance, Is.EqualTo(expected));
        }
        [Test] public void AllMultipliersApplyBeforeFinalClampAndRounding()
        {
            battle = new MechaAccuracyTestBattle(playerAccuracy: 50);
            battle.Grid.TryGetCell(battle.Enemy.CurrentGridPosition, out var cell); cell.SetCover(CoverDirection.West, CoverType.Full);
            Assert.That(battle.Combat.GetWeaponPreview(battle.Player, battle.Enemy, WeaponSlot.RightHand).HitChance, Is.EqualTo(60), "Do not clamp80x1.5 before cover.");
            battle.Player.Combat.ConfigureTestRanged(50, 50, 7, 3);
            Assert.That(battle.Combat.GetWeaponPreview(battle.Player, battle.Enemy, WeaponSlot.RightHand).HitChance, Is.EqualTo(38), "50x1.5x.5 rounds37.5 once.");
        }
        [TestCase(31, true)] [TestCase(32, false)]
        public void MixedCoverPartHeadGripDistanceMatchesActualRoll(int roll, bool hit)
        {
            battle = new MechaAccuracyTestBattle(playerAccuracy: 50);
            var loadout = battle.Player.GetComponent<UnitWeaponLoadout>(); var weapon = loadout.RightWeapon;
            JsonUtility.FromJsonOverwrite("{\"gripRequirement\":2,\"twoHandHitMultiplier\":1.1,\"distanceBands\":[{\"MaxDistance\":7,\"Multiplier\":0.8}]}", weapon);
            loadout.Configure(null, GripMode.OneHand, weapon, GripMode.TwoHands);
            battle.Player.Health.ApplyDamage(TargetBodyPart.Head, 6);
            battle.Grid.TryGetCell(battle.Enemy.CurrentGridPosition, out var cell); cell.SetCover(CoverDirection.West, CoverType.Full);
            int rolls = 0; battle.Combat.SetRollSource(() => { rolls++; return roll; });
            string data = JsonUtility.ToJson(battle.PlayerData);
            var preview = battle.Combat.GetWeaponPreview(battle.Player, battle.Enemy, WeaponSlot.RightHand, TargetBodyPart.Head);
            Assert.That(preview.CanAttack, Is.True); Assert.That(preview.HitChance, Is.EqualTo(32));
            Assert.That(rolls, Is.Zero); Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            var result = battle.Combat.TryAttackWithWeapon(battle.Player, battle.Enemy, WeaponSlot.RightHand, TargetBodyPart.Head);
            Assert.That(result.WasExecuted, Is.True); Assert.That(result.HitChance, Is.EqualTo(preview.HitChance));
            Assert.That(result.Hit, Is.EqualTo(hit)); Assert.That(rolls, Is.EqualTo(1));
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(JsonUtility.ToJson(battle.PlayerData), Is.EqualTo(data));
        }
        [Test] public void InitializationSnapshotsStatsAndDoesNotReapplyEditedData()
        {
            battle = new MechaAccuracyTestBattle(playerAccuracy: 20, playerEvasion: 30);
            Assert.That(Stat(battle.Player, "MechaAccuracy"), Is.EqualTo(20)); Assert.That(Stat(battle.Player, "MechaEvasion"), Is.EqualTo(30));
            battle.Player.ActionPoints.TrySpend(2); battle.Player.Health.ApplyDamage(TargetBodyPart.Torso, 1);
            JsonUtility.FromJsonOverwrite("{\"accuracy\":50,\"evasion\":0}", battle.PlayerData);
            Assert.That(battle.Player.Initialize(battle.Grid), Is.True);
            Assert.That(Stat(battle.Player, "MechaAccuracy"), Is.EqualTo(20)); Assert.That(Stat(battle.Player, "MechaEvasion"), Is.EqualTo(30));
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(3)); Assert.That(battle.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(19));
            var later = battle.Unit(new Vector2Int(5, 1), UnitTeam.Player, battle.PlayerData);
            Assert.That(Stat(later, "MechaAccuracy"), Is.EqualTo(50)); Assert.That(Stat(later, "MechaEvasion"), Is.Zero);
        }
        [TestCase("accuracy", -1)] [TestCase("accuracy", 51)] [TestCase("evasion", -1)] [TestCase("evasion", 51)]
        public void DataRejectsInvalidRatings(string name, int value)
        {
            var data = ScriptableObject.CreateInstance<MechaData>();
            try
            {
                JsonUtility.FromJsonOverwrite("{\"mechaId\":\"valid\"}", data);
                var field = typeof(MechaData).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance); Assert.That(field, Is.Not.Null);
                field.SetValue(data, value); Assert.That(data.IsValid(out var reason), Is.False); Assert.That(reason, Is.Not.Empty);
            }
            finally { Object.DestroyImmediate(data); }
        }
        [Test] public void DataOnValidateClampsRatings()
        {
            var data = ScriptableObject.CreateInstance<MechaData>();
            try
            {
                var accuracy = typeof(MechaData).GetField("accuracy", BindingFlags.NonPublic | BindingFlags.Instance);
                var evasion = typeof(MechaData).GetField("evasion", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(accuracy, Is.Not.Null); Assert.That(evasion, Is.Not.Null);
                accuracy.SetValue(data, -3); evasion.SetValue(data, 99);
                typeof(MechaData).GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(data, null);
                Assert.That(accuracy.GetValue(data), Is.EqualTo(0)); Assert.That(evasion.GetValue(data), Is.EqualTo(50));
            }
            finally { Object.DestroyImmediate(data); }
        }
        [Test] public void UnitsWithoutDataKeepNeutralRatings()
        {
            using (var legacy = new OverwatchTestBattle())
            {
                Assert.That(Stat(legacy.Watcher, "MechaAccuracy"), Is.Zero); Assert.That(Stat(legacy.Mover, "MechaEvasion"), Is.Zero);
                Assert.That(legacy.Combat.GetWeaponPreview(legacy.Watcher, legacy.Mover, WeaponSlot.RightHand).HitChance, Is.EqualTo(80));
            }
        }
    }
}
