using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class BackpackCombatTests
    {
        [TestCase(AttackType.Ranged)] [TestCase(AttackType.Melee)]
        public void BackpackAppearsAsThirdAttackSlot(AttackType type)
        {
            using (var b = new BackpackTestBattle(type))
            {
                CollectionAssert.AreEqual(new[] { WeaponSlot.RightHand, WeaponSlot.LeftHand, WeaponSlot.Backpack }, b.Player.Combat.GetAttackSlots(type));
                Assert.That(b.Player.Combat.TryGetAttack(WeaponSlot.Backpack, out var stats, out var reason), Is.True, reason);
                Assert.That(stats.Weapon, Is.SameAs(b.Weapon)); Assert.That(stats.DisplayName, Is.EqualTo("Integrated test weapon"));
                Assert.That(stats.Slot, Is.EqualTo(WeaponSlot.Backpack)); Assert.That(stats.Type, Is.EqualTo(type));
                Assert.That(stats.BaseHitChance, Is.EqualTo(80)); Assert.That(stats.MinDamage, Is.EqualTo(7));
                Assert.That(stats.MaxDamage, Is.EqualTo(7)); Assert.That(stats.APCost, Is.EqualTo(3));
                Assert.That(stats.Range, Is.EqualTo(type == AttackType.Ranged ? 7 : 2)); Assert.That(stats.GripMultiplier, Is.EqualTo(1f));
                Assert.That(b.Player.Combat.GetAttackSlots(type == AttackType.Ranged ? AttackType.Melee : AttackType.Ranged), Is.Empty);
            }
        }
        [TestCase(false, true)] [TestCase(true, false)]
        public void MissingBackpackHasNoAttackSlot(bool equipped, bool armed)
        {
            using (var b = new BackpackTestBattle(withBackpack: equipped, armed: armed))
            {
                Assert.That(b.Player.Combat.TryGetAttack(WeaponSlot.Backpack, out _, out var reason), Is.False);
                Assert.That(reason, Is.Not.Empty);
                CollectionAssert.AreEqual(new[] { WeaponSlot.RightHand, WeaponSlot.LeftHand }, b.Player.Combat.GetAttackSlots(AttackType.Ranged));
            }
        }
        [TestCase(AttackType.Ranged)] [TestCase(AttackType.Melee)]
        public void BackpackIgnoresHandGripBonusAndDestroyedArms(AttackType type)
        {
            using (var b = new BackpackTestBattle(type))
            {
                var loadout = b.Player.GetComponent<UnitWeaponLoadout>();
                loadout.Configure(null, GripMode.OneHand, b.Weapon, GripMode.TwoHands);
                Assert.That(b.Player.Combat.TryGetAttack(WeaponSlot.Backpack, out var pack, out _), Is.True);
                Assert.That(b.Player.Combat.TryGetAttack(WeaponSlot.RightHand, out var hand, out _), Is.True);
                Assert.That(pack.GripMultiplier, Is.EqualTo(1f)); Assert.That(hand.GripMultiplier, Is.EqualTo(1.5f));
                b.Player.Health.ApplyDamage(TargetBodyPart.LeftArm, b.Player.Health.GetCurrentHP(TargetBodyPart.LeftArm));
                b.Player.Health.ApplyDamage(TargetBodyPart.RightArm, b.Player.Health.GetCurrentHP(TargetBodyPart.RightArm));
                Assert.That(b.Player.Health.State, Is.EqualTo(MechState.Active));
                Assert.That(b.Player.Combat.TryGetAttack(WeaponSlot.Backpack, out pack, out _), Is.True);
                Assert.That(pack.GripMultiplier, Is.EqualTo(1f));
                Assert.That(b.Player.Combat.TryGetAttack(WeaponSlot.LeftHand, out _, out _), Is.False);
                Assert.That(b.Player.Combat.TryGetAttack(WeaponSlot.RightHand, out _, out _), Is.False);
                CollectionAssert.AreEqual(new[] { WeaponSlot.Backpack }, b.Player.Combat.GetAttackSlots(type));
                if (type == AttackType.Melee)
                {
                    var result = b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.Backpack);
                    Assert.That(result.WasExecuted && result.Hit, Is.True); Assert.That(result.DamageDealt, Is.EqualTo(8));
                }
            }
        }
        [TestCase(0, true, false, 8)] [TestCase(88, false, false, 0)] [TestCase(0, true, true, 10)]
        public void BackpackAndHandsShareSameFinalHitDamageRules(int roll, bool hit, bool critical, int damage)
        {
            using (var b = new BackpackTestBattle(AttackType.Melee, enemyArmor: ArmorType.BeamResistant))
            {
                string weaponBefore = JsonUtility.ToJson(b.Weapon), packBefore = JsonUtility.ToJson(b.Backpack);
                b.Grid.TryGetCell(b.Enemy.CurrentGridPosition, out var cell); cell.SetCover(CoverDirection.West, CoverType.Full);
                var hand = b.Combat.GetWeaponPreview(b.Player, b.Enemy, WeaponSlot.RightHand);
                var pack = b.Combat.GetWeaponPreview(b.Player, b.Enemy, WeaponSlot.Backpack, TargetBodyPart.Cockpit);
                Assert.That(pack.CanAttack, Is.True, pack.FailureReason); Assert.That(pack.TargetPart, Is.EqualTo(TargetBodyPart.Torso));
                Assert.That(pack.HitChance, Is.EqualTo(88)); Assert.That(pack.HitChance, Is.EqualTo(hand.HitChance));
                Assert.That(pack.CriticalChance, Is.EqualTo(14f)); Assert.That(pack.ArmorMultiplier, Is.EqualTo(1f));
                Assert.That(pack.PilotDamageMultiplier, Is.EqualTo(1.1f).Within(.00001f));
                Assert.That(AttackPreviewText.DamageRange(pack), Is.EqualTo("8"));
                b.Combat.SetRollSource(() => roll); b.Combat.SetCriticalRollSource(() => critical ? 0f : 99f);
                var result = b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.Backpack, TargetBodyPart.Cockpit);
                Assert.That(result.WasExecuted, Is.True); Assert.That(result.Hit, Is.EqualTo(hit));
                Assert.That(result.IsCritical, Is.EqualTo(critical)); Assert.That(result.DamageDealt, Is.EqualTo(damage));
                Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20 - damage));
                Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(2));
                Assert.That(b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.Backpack).WasExecuted, Is.False);
                Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(2));
                Assert.That(JsonUtility.ToJson(b.Weapon), Is.EqualTo(weaponBefore)); Assert.That(JsonUtility.ToJson(b.Backpack), Is.EqualTo(packBefore));
            }
        }
        [Test] public void RangedBackpackUsesCommonPreviewMathAndActuallyConsumesLoadedAmmo()
        {
            using (var b = new BackpackTestBattle(enemyArmor: ArmorType.BeamResistant))
            {
                var hand = b.Combat.GetWeaponPreview(b.Player, b.Enemy, WeaponSlot.RightHand, TargetBodyPart.Head);
                var pack = b.Combat.GetWeaponPreview(b.Player, b.Enemy, WeaponSlot.Backpack, TargetBodyPart.Head);
                Assert.That(hand.CanAttack, Is.True);
                Assert.That(pack.HitChance, Is.EqualTo(59)); Assert.That(pack.HitChance, Is.EqualTo(hand.HitChance));
                Assert.That(pack.PartMultiplier, Is.EqualTo(.8f)); Assert.That(pack.DistanceMultiplier, Is.EqualTo(.8f));
                Assert.That(pack.PilotHitMultiplier, Is.EqualTo(1.1f).Within(.00001f)); Assert.That(pack.MechaMultiplier, Is.EqualTo(1.05f).Within(.00001f));
                Assert.That(pack.ArmorMultiplier, Is.EqualTo(.8f)); Assert.That(AttackPreviewText.DamageRange(pack), Is.EqualTo("6"));
                Assert.That(pack.CanAttack, Is.True, pack.FailureReason); Assert.That(pack.UsesAmmo, Is.True);
                Assert.That(pack.AmmoLoaded, Is.EqualTo(6)); Assert.That(pack.SpareMagazines, Is.Zero);
                int rolls = 0; b.Combat.SetRollSource(() => { rolls++; return 0; });
                var result = b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.Backpack, TargetBodyPart.Head);
                Assert.That(result.WasExecuted && result.Hit, Is.True); Assert.That(result.DamageDealt, Is.EqualTo(6));
                Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(2)); Assert.That(rolls, Is.EqualTo(1));
                Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.Backpack).Loaded, Is.EqualTo(5));
                Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Head), Is.Zero);
            }
        }
        [TestCase(MechState.Critical)] [TestCase(MechState.Ejected)] [TestCase(MechState.Destroyed)]
        public void CriticalAndEjectedUnitsCannotAttackWithBackpack(MechState state)
        {
            using (var b = new BackpackTestBattle(AttackType.Melee))
            {
                if (state == MechState.Critical)
                {
                    b.Player.Health.ConfigureDestructionChance(0);
                    foreach (var part in new[] { TargetBodyPart.LeftArm, TargetBodyPart.RightArm, TargetBodyPart.LeftLeg })
                        b.Player.Health.ApplyDamage(part, b.Player.Health.GetCurrentHP(part));
                }
                else if (state == MechState.Ejected) Assert.That(b.Player.TryEject(), Is.True);
                else b.Player.Health.ApplyDamage(TargetBodyPart.Torso, b.Player.Health.GetCurrentHP(TargetBodyPart.Torso));
                Assert.That(b.Player.Health.State, Is.EqualTo(state));
                int before = b.Player.ActionPoints.CurrentPoints, rolls = 0;
                b.Combat.SetRollSource(() => { rolls++; return 0; });
                Assert.That(b.Combat.GetWeaponPreview(b.Player, b.Enemy, WeaponSlot.Backpack).CanAttack, Is.False);
                Assert.That(b.Combat.GetWeaponRangeCells(b.Player, WeaponSlot.Backpack), Is.Empty);
                Assert.That(b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.Backpack).WasExecuted, Is.False);
                Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(before)); Assert.That(rolls, Is.Zero);
                Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            }
        }
        [Test] public void BackpackOnlyMeleeUnitUsesDefaultAttackAndRangePath()
        {
            using (var b = new BackpackTestBattle(AttackType.Melee))
            {
                b.Player.GetComponent<UnitWeaponLoadout>().Configure(null, GripMode.OneHand, null, GripMode.OneHand);
                Assert.That(b.Player.Combat.GetAttack(AttackType.Melee).Slot, Is.EqualTo(WeaponSlot.Backpack));
                Assert.That(b.Combat.GetPreview(b.Player, b.Enemy, AttackType.Melee).CanAttack, Is.True);
                Assert.That(b.Combat.GetAttackRangeCells(b.Player, AttackType.Melee), Does.Contain(GetEnemyCell(b)));
                Assert.That(b.Combat.TryAttack(b.Player, b.Enemy, AttackType.Melee).DamageDealt, Is.EqualTo(8));
            }
        }
        private static GridCell GetEnemyCell(BackpackTestBattle b)
        { b.Grid.TryGetCell(b.Enemy.CurrentGridPosition, out var cell); return cell; }
    }
}
