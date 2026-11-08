using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class PilotCombatTests
    {
        private static object MathCall(string name, params object[] args)
        {
            var method = typeof(CombatMath).GetMethod(name);
            Assert.That(method, Is.Not.Null, name);
            return method.Invoke(null, args);
        }
        private static int Stat(BattleUnit unit, string name)
        {
            var property = typeof(BattleUnit).GetProperty(name);
            Assert.That(property, Is.Not.Null, name);
            return (int)property.GetValue(unit);
        }
        private static float Factor(AttackPreview preview, string name)
        {
            var field = typeof(AttackPreview).GetField(name);
            Assert.That(field, Is.Not.Null, name);
            return (float)field.GetValue(preview);
        }

        [TestCase(AttackType.Ranged, 0, 50, .5f)]
        [TestCase(AttackType.Ranged, 10, 30, .8f)]
        [TestCase(AttackType.Ranged, 0, 0, 1f)]
        [TestCase(AttackType.Ranged, 50, 50, 1f)]
        [TestCase(AttackType.Ranged, 30, 10, 1.2f)]
        [TestCase(AttackType.Ranged, 50, 0, 1.5f)]
        [TestCase(AttackType.Melee, 0, 50, .75f)]
        [TestCase(AttackType.Melee, 10, 30, .9f)]
        [TestCase(AttackType.Melee, 0, 0, 1f)]
        [TestCase(AttackType.Melee, 50, 50, 1f)]
        [TestCase(AttackType.Melee, 30, 10, 1.1f)]
        [TestCase(AttackType.Melee, 50, 0, 1.25f)]
        [TestCase(AttackType.Ranged, 100, -10, 1.5f)]
        [TestCase(AttackType.Melee, -10, 100, .75f)]
        public void RequestedHitTable(AttackType type, int attack, int evasion, float expected)
            => Assert.That((float)MathCall("CalculatePilotHitMultiplier", type, attack, evasion), Is.EqualTo(expected).Within(.00001f));

        [TestCase(AttackType.Melee, 0, 1f)] [TestCase(AttackType.Melee, 20, 1.1f)]
        [TestCase(AttackType.Melee, 50, 1.25f)] [TestCase(AttackType.Melee, 100, 1.25f)]
        [TestCase(AttackType.Melee, -10, 1f)] [TestCase(AttackType.Ranged, 50, 1f)]
        public void RequestedDamageTable(AttackType type, int melee, float expected)
            => Assert.That((float)MathCall("CalculatePilotDamageMultiplier", type, melee), Is.EqualTo(expected).Within(.00001f));

        [TestCase(7, true, .8f, 1.25f, 9)]
        [TestCase(2, true, 1f, 1.25f, 3)]
        [TestCase(1000, false, 1f, 1.25f, 1250)]
        [TestCase(1000, true, 1f, 1.25f, 1563)]
        [TestCase(100000000, false, 1.1f, 1.1f, 121000000)]
        [TestCase(int.MaxValue, true, 1.1f, 1.25f, int.MaxValue)]
        public void DamageRoundsOnlyAfterAllFactors(int raw, bool critical, float armor, float pilot, int expected)
            => Assert.That((int)MathCall("CalculateModifiedAttackDamage", raw, critical, armor, pilot), Is.EqualTo(expected));

        [TestCase(AttackType.Ranged, 50, 0, 99)]
        [TestCase(AttackType.Ranged, 0, 50, 40)]
        [TestCase(AttackType.Ranged, 25, 25, 80)]
        [TestCase(AttackType.Melee, 50, 0, 99)]
        [TestCase(AttackType.Melee, 0, 50, 71)]
        [TestCase(AttackType.Melee, 25, 25, 95)]
        public void ActualAttackUsesCorrectPilotStatAndTargetEvasion(AttackType type, int attack, int evasion, int expected)
        {
            using (var b = new MechaAccuracyTestBattle(playerShooting: type == AttackType.Ranged ? attack : 50,
                playerMelee: type == AttackType.Melee ? attack : 50, enemyPilotEvasion: evasion))
            {
                b.Combat.SetCriticalRollSource(() => 99f);
                b.Combat.SetRollSource(() => expected - 1);
                var slot = type == AttackType.Ranged ? WeaponSlot.RightHand : WeaponSlot.LeftHand;
                var preview = b.Combat.GetWeaponPreview(b.Player, b.Enemy, slot);
                Assert.That(preview.CanAttack, Is.True, preview.FailureReason);
                Assert.That(preview.HitChance, Is.EqualTo(expected));
                var result = b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, slot);
                Assert.That(result.WasExecuted && result.Hit, Is.True);
                Assert.That(result.HitChance, Is.EqualTo(expected));
                Assert.That(result.DamageDealt, Is.EqualTo(type == AttackType.Ranged ? 5 : attack == 50 ? 10 : 8 + (attack == 25 ? 1 : 0)));
                Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(2));
            }
        }

        [TestCase(89, true)] [TestCase(90, false)]
        public void MechaAndPilotBoostAreNotClampedBeforeCover(int roll, bool hit)
        {
            using (var b = new MechaAccuracyTestBattle(playerAccuracy: 50, playerShooting: 50))
            {
                b.Grid.TryGetCell(b.Enemy.CurrentGridPosition, out var cell); cell.SetCover(CoverDirection.West, CoverType.Full);
                b.Combat.SetRollSource(() => roll); b.Combat.SetCriticalRollSource(() => 99f);
                var preview = b.Combat.GetWeaponPreview(b.Player, b.Enemy, WeaponSlot.RightHand);
                Assert.That(preview.HitChance, Is.EqualTo(90), "80 x 1.5 x 1.5 x .5 = 90.");
                Assert.That(Factor(preview, "PilotHitMultiplier"), Is.EqualTo(1.5f));
                Assert.That(b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.RightHand).Hit, Is.EqualTo(hit));
            }
        }

        [TestCase(false, 3)] [TestCase(true, 3)]
        public void MeleeDamagePreviewAndExecutionKeepRawRollBounds(bool critical, int expected)
        {
            using (var b = new MechaAccuracyTestBattle(playerMelee: 50, enemyPilotEvasion: 50, enemyArmor: ArmorType.BeamResistant))
            {
                b.Player.Combat.ConfigureTestMelee(2, 95, 2, 3);
                var weapon = b.Player.GetComponent<UnitWeaponLoadout>().LeftWeapon;
                JsonUtility.FromJsonOverwrite("{\"maxDamage\":3}", weapon);
                b.Combat.SetCriticalRollSource(() => critical ? 0f : 99f);
                int rolls = 0;
                b.Combat.SetDamageRollSource((min, max) => { rolls++; Assert.That(min, Is.EqualTo(2)); Assert.That(max, Is.EqualTo(3)); return min; });
                var preview = b.Combat.GetWeaponPreview(b.Player, b.Enemy, WeaponSlot.LeftHand);
                Assert.That(AttackPreviewText.DamageRange(preview), Is.EqualTo("3-4"));
                Assert.That(Factor(preview, "PilotDamageMultiplier"), Is.EqualTo(1.25f));
                Assert.That(preview.HitChance, Is.EqualTo(95)); Assert.That(preview.ArmorMultiplier, Is.EqualTo(1f));
                Assert.That(rolls, Is.Zero);
                var result = b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.LeftHand);
                Assert.That(result.DamageDealt, Is.EqualTo(expected)); Assert.That(result.IsCritical, Is.EqualTo(critical));
                Assert.That(rolls, Is.EqualTo(1));
            }
        }

        [Test] public void InitializationSnapshotsAllPilotStatsAndPreservesSharedDataAndCurrentState()
        {
            using (var b = new MechaAccuracyTestBattle(playerShooting: 100, playerMelee: 20, playerPilotEvasion: -5))
            {
                Assert.That(Stat(b.Player, "PilotShooting"), Is.EqualTo(50));
                Assert.That(Stat(b.Player, "PilotMelee"), Is.EqualTo(20));
                Assert.That(Stat(b.Player, "PilotEvasion"), Is.Zero);
                Assert.That(b.PlayerPilot.Shooting, Is.EqualTo(100), "Do not clamp the shared asset itself.");
                var twin = b.Unit(new Vector2Int(5, 1), UnitTeam.Player, b.PlayerData, b.PlayerPilot);
                b.Player.ActionPoints.TrySpend(2); b.Player.Health.ApplyDamage(TargetBodyPart.Torso, 1);
                JsonUtility.FromJsonOverwrite("{\"shooting\":10,\"melee\":50,\"evasion\":100}", b.PlayerPilot);
                Assert.That(b.Player.Initialize(b.Grid), Is.True);
                Assert.That(Stat(b.Player, "PilotShooting"), Is.EqualTo(50));
                Assert.That(Stat(twin, "PilotMelee"), Is.EqualTo(20));
                Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(3));
                Assert.That(b.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(19));
                var later = b.Unit(new Vector2Int(7, 1), UnitTeam.Player, b.PlayerData, b.PlayerPilot);
                Assert.That(Stat(later, "PilotShooting"), Is.EqualTo(10));
                Assert.That(Stat(later, "PilotMelee"), Is.EqualTo(50));
                Assert.That(Stat(later, "PilotEvasion"), Is.EqualTo(50));
            }
        }

        [Test] public void MissingPilotKeepsNeutralCorrections()
        {
            using (var b = new MechaAccuracyTestBattle())
            {
                Assert.That(Stat(b.Player, "PilotShooting"), Is.Zero);
                Assert.That(Stat(b.Player, "PilotMelee"), Is.Zero);
                Assert.That(Stat(b.Player, "PilotEvasion"), Is.Zero);
                var preview = b.Combat.GetWeaponPreview(b.Player, b.Enemy, WeaponSlot.LeftHand);
                Assert.That(Factor(preview, "PilotHitMultiplier"), Is.EqualTo(1f));
                Assert.That(Factor(preview, "PilotDamageMultiplier"), Is.EqualTo(1f));
                Assert.That(AttackPreviewText.DamageRange(preview), Is.EqualTo("8"));
            }
        }
    }
}
