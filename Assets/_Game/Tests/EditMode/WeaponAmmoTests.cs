using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class WeaponAmmoTests
    {
        private MechaAccuracyTestBattle battle;
        [SetUp] public void Setup() => battle = new MechaAccuracyTestBattle();
        [TearDown] public void Cleanup() => battle.Dispose();
        private object Ammo(BattleUnit unit)
        {
            var property = typeof(BattleUnit).GetProperty("Ammo"); Assert.That(property, Is.Not.Null, "Per-unit weapon ammo is required.");
            var ammo = property.GetValue(unit); Assert.That(ammo, Is.Not.Null); return ammo;
        }
        private object Status(BattleUnit unit, WeaponSlot slot = WeaponSlot.RightHand) =>
            Ammo(unit).GetType().GetMethod("GetStatus").Invoke(Ammo(unit), new object[] { slot });
        private int Count(BattleUnit unit, string name, WeaponSlot slot = WeaponSlot.RightHand) =>
            Convert.ToInt32(Status(unit, slot).GetType().GetField(name).GetValue(Status(unit, slot)));
        private bool Reload(BattleUnit unit, WeaponSlot slot = WeaponSlot.RightHand)
        {
            var method = typeof(CombatSystem).GetMethod("TryReloadWithWeapon"); Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(battle.Combat, new object[] { unit, slot });
        }
        private AttackResult Fire(WeaponSlot slot = WeaponSlot.RightHand) =>
            battle.Combat.TryAttackWithWeapon(battle.Player, battle.Enemy, slot);

        [TestCase(true)] [TestCase(false)]
        public void ExecutedHitAndMissConsumeOneRoundAndWeaponAP(bool hit)
        {
            AmmoTestWeapons.Equip(battle.Player); battle.Combat.SetRollSource(() => hit ? 0 : 99);
            Assert.That(Count(battle.Player, "Loaded"), Is.EqualTo(2));
            Assert.That(Fire().WasExecuted, Is.True);
            Assert.That(Count(battle.Player, "Loaded"), Is.EqualTo(1));
            Assert.That(Count(battle.Player, "SpareMagazines"), Is.EqualTo(2));
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
        }
        [Test] public void EmptyWeaponRejectsShotWithoutAPOrRandomRolls()
        {
            AmmoTestWeapons.Equip(battle.Player, capacity: 1); Assert.That(Fire().WasExecuted, Is.True);
            int rolls = 0; battle.Combat.SetRollSource(() => { rolls++; return 0; });
            Assert.That(Fire().WasExecuted, Is.False); Assert.That(rolls, Is.Zero);
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(Count(battle.Player, "Loaded"), Is.Zero);
        }
        [Test] public void PartialReloadDiscardsRemainingAmmoAndConsumesOneSpare()
        {
            AmmoTestWeapons.Equip(battle.Player, capacity: 3); Fire();
            Assert.That(Reload(battle.Player), Is.True);
            Assert.That(Count(battle.Player, "Loaded"), Is.EqualTo(3));
            Assert.That(Count(battle.Player, "SpareMagazines"), Is.EqualTo(1));
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(Reload(battle.Player), Is.False);
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [Test] public void ReserveMagazinesAreFinite()
        {
            AmmoTestWeapons.Equip(battle.Player, capacity: 1, spares: 1, reload: 1);
            Assert.That(Fire().WasExecuted, Is.True); Assert.That(Reload(battle.Player), Is.True);
            Assert.That(Fire().WasExecuted, Is.True); Assert.That(Reload(battle.Player), Is.False);
            Assert.That(Fire().WasExecuted, Is.False);
            Assert.That(Count(battle.Player, "Loaded"), Is.Zero);
            Assert.That(Count(battle.Player, "SpareMagazines"), Is.Zero);
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [Test] public void InsufficientReloadAPChangesNeitherAmmoNorReserves()
        {
            AmmoTestWeapons.Equip(battle.Player, reload: 5); Fire();
            Assert.That(Reload(battle.Player), Is.False);
            Assert.That(Count(battle.Player, "Loaded"), Is.EqualTo(1));
            Assert.That(Count(battle.Player, "SpareMagazines"), Is.EqualTo(2));
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
        }
        [Test] public void InvalidAttackAndPreviewDoNotConsumeAmmo()
        {
            AmmoTestWeapons.Equip(battle.Player);
            battle.Combat.GetWeaponPreview(battle.Player, battle.Enemy, WeaponSlot.RightHand);
            Assert.That(battle.Combat.TryAttackWithWeapon(battle.Player, battle.Player, WeaponSlot.RightHand).WasExecuted, Is.False);
            Assert.That(Count(battle.Player, "Loaded"), Is.EqualTo(2));
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [Test] public void ResetAPReinitializationAndSameLoadoutDoNotRefill()
        {
            var weapon = AmmoTestWeapons.Equip(battle.Player); Fire();
            battle.Player.ActionPoints.ResetForTurn(); Assert.That(battle.Player.Initialize(battle.Grid), Is.True);
            var loadout = battle.Player.GetComponent<UnitWeaponLoadout>();
            loadout.Configure(loadout.LeftWeapon, loadout.LeftGrip, weapon, GripMode.OneHand);
            Assert.That(Count(battle.Player, "Loaded"), Is.EqualTo(1));
        }
        [Test] public void SharedWeaponDataHasIndependentAmmoForUnitsAndHands()
        {
            var weapon = AmmoTestWeapons.Equip(battle.Player, capacity: 3);
            battle.Player.GetComponent<UnitWeaponLoadout>().Configure(weapon, GripMode.OneHand, weapon, GripMode.OneHand);
            battle.Enemy.GetComponent<UnitWeaponLoadout>().Configure(null, GripMode.OneHand, weapon, GripMode.OneHand);
            string before = JsonUtility.ToJson(weapon); Fire();
            Assert.That(Count(battle.Player, "Loaded"), Is.EqualTo(2));
            Assert.That(Count(battle.Player, "Loaded", WeaponSlot.LeftHand), Is.EqualTo(3));
            Assert.That(Count(battle.Enemy, "Loaded"), Is.EqualTo(3));
            Assert.That(JsonUtility.ToJson(weapon), Is.EqualTo(before));
        }
        [Test] public void EitherWeaponMovingToSurvivingHandKeepsAmmo()
        {
            var weapon = AmmoTestWeapons.Equip(battle.Player);
            JsonUtility.FromJsonOverwrite("{\"gripRequirement\":2}", weapon);
            battle.Player.GetComponent<UnitWeaponLoadout>().Configure(weapon, GripMode.TwoHands, null, GripMode.OneHand);
            Assert.That(Fire(WeaponSlot.LeftHand).WasExecuted, Is.True);
            battle.Player.Health.ApplyDamage(TargetBodyPart.LeftArm, 8);
            Assert.That(battle.Player.GetComponent<UnitWeaponLoadout>().RightWeapon, Is.SameAs(weapon));
            Assert.That(Count(battle.Player, "Loaded"), Is.EqualTo(1));
            Assert.That(Count(battle.Player, "SpareMagazines"), Is.EqualTo(2));
            Assert.That(Fire().WasExecuted, Is.True); Assert.That(Fire().WasExecuted, Is.False);
        }
        [Test] public void DestroyedArmAndEnemyTurnPreventReload()
        {
            AmmoTestWeapons.Equip(battle.Player); Fire();
            battle.StartEnemyTeam(); Assert.That(Reload(battle.Player), Is.False);
            battle.Player.Health.ApplyDamage(TargetBodyPart.RightArm, 8);
            Assert.That(Reload(battle.Player), Is.False);
            Assert.That(Count(battle.Player, "Loaded"), Is.EqualTo(1));
        }
        [Test] public void MeleeHasNoAmmoOrReloadCost()
        {
            Assert.That(Fire(WeaponSlot.LeftHand).WasExecuted, Is.True);
            Assert.That((bool)Status(battle.Player, WeaponSlot.LeftHand).GetType().GetField("UsesAmmo").GetValue(Status(battle.Player, WeaponSlot.LeftHand)), Is.False);
            Assert.That(Reload(battle.Player, WeaponSlot.LeftHand), Is.False);
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [TestCase("magazineCapacity", 0)] [TestCase("spareMagazineCount", -1)] [TestCase("reloadAPCost", 0)]
        public void InvalidRangedAmmoDefinitionsAreRejected(string field, int value)
        {
            var weapon = AmmoTestWeapons.Equip(battle.Player);
            JsonUtility.FromJsonOverwrite("{\"" + field + "\":" + value + "}", weapon);
            Assert.That(weapon.IsValid(out _), Is.False);
        }
        [Test] public void CombatServiceFromDifferentGridCannotReloadRegisteredUnit()
        {
            AmmoTestWeapons.Equip(battle.Player); Fire();
            var other = new GameObject("Other grid"); other.transform.SetParent(battle.Root.transform);
            var grid = other.AddComponent<GridManager>(); grid.Configure(12, 12, 2); grid.Generate();
            var combat = other.AddTestCombatSystem(); combat.Initialize(grid, other.AddComponent<CoverSystem>(), battle.Turns);
            Assert.That(combat.TryReloadWithWeapon(battle.Player, WeaponSlot.RightHand), Is.False);
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(Count(battle.Player, "Loaded"), Is.EqualTo(1));
        }
        [Test] public void LastRoundIsPaidBeforeAttackCallbacksCanReenter()
        {
            AmmoTestWeapons.Equip(battle.Player, capacity: 1);
            int callbacks = 0;
            battle.Combat.SetRollSource(() =>
            {
                callbacks++;
                Assert.That(Count(battle.Player, "Loaded"), Is.Zero);
                Assert.That(Fire().WasExecuted, Is.False);
                return 0;
            });
            Assert.That(Fire().WasExecuted, Is.True);
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
        }
        [Test] public void AmmoComponentDisableAndEnableDoNotRefill()
        {
            AmmoTestWeapons.Equip(battle.Player); Fire();
            var component = (Behaviour)Ammo(battle.Player); component.enabled = false;
            Assert.That(Fire().WasExecuted, Is.False); Assert.That(Reload(battle.Player), Is.False);
            _ = Status(battle.Player); component.enabled = true;
            Assert.That(Count(battle.Player, "Loaded"), Is.EqualTo(1));
            Assert.That(Fire().WasExecuted, Is.True);
        }
        [Test] public void CriticalUnitCannotReloadEvenWithSpareAndAP()
        {
            AmmoTestWeapons.Equip(battle.Player); Fire();
            battle.Player.Health.SetDestructionRollSource(() => 99);
            battle.Player.Health.ApplyDamage(TargetBodyPart.Head, 6);
            battle.Player.Health.ApplyDamage(TargetBodyPart.LeftArm, 8);
            battle.Player.Health.ApplyDamage(TargetBodyPart.RightArm, 8);
            Assert.That(battle.Player.Health.State, Is.EqualTo(MechState.Critical));
            Assert.That(Reload(battle.Player), Is.False);
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
        }
        [Test] public void MeleeDoesNotValidateUnusedAmmoDefinition()
        {
            var weapon = battle.Player.GetComponent<UnitWeaponLoadout>().LeftWeapon;
            JsonUtility.FromJsonOverwrite("{\"magazineCapacity\":0,\"spareMagazineCount\":-1,\"reloadAPCost\":0}", weapon);
            Assert.That(weapon.IsValid(out _), Is.True);
            Assert.That(Fire(WeaponSlot.LeftHand).WasExecuted, Is.True);
        }
    }
}
