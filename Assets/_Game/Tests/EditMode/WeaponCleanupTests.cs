using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class WeaponCleanupTests
    {
        [Test] public void UnitCombatHasNoSerializedAttackStatsAndNoWeaponFallback()
        {
            var go = new GameObject("No weapon");
            try
            {
                var combat = go.AddComponent<UnitCombat>(); var data = new SerializedObject(combat);
                foreach (var name in new[] { "damage", "baseHitChance", "range", "attackCost", "meleeDamage", "meleeBaseHitChance", "meleeRange", "meleeAttackCost" })
                    Assert.That(data.FindProperty(name), Is.Null, name + " must belong to WeaponData only.");
                Assert.That(combat.GetAttackSlots(AttackType.Ranged), Is.Empty);
                Assert.That(combat.GetAttackSlots(AttackType.Melee), Is.Empty);
                Assert.That(combat.TryGetAttack(WeaponSlot.RightHand, out _, out _), Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test] public void OnlyApprovedBattleScenesRemainAndAllUnitsUseWeaponData()
        {
            var paths = Directory.GetFiles("Assets/_Game/Scenes", "*.unity").Select(Path.GetFileNameWithoutExtension).OrderBy(p => p).ToArray();
            CollectionAssert.AreEquivalent(new[] { "1V1Test", "SqurdWeaponTest", "EliminationMissionTest", "DestroyCommunicationStationTest" }, paths);
            foreach (var name in paths)
            {
                var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/" + name + ".unity", OpenSceneMode.Additive);
                try
                {
                    var units = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<BattleUnit>(true)).ToArray();
                    Assert.That(units, Is.Not.Empty);
                    foreach (var unit in units)
                    {
                        var loadout = unit.GetComponent<UnitWeaponLoadout>(); Assert.That(loadout, Is.Not.Null, unit.name);
                        Assert.That(loadout.IsValid(out var reason), Is.True, reason);
                        var equipped = new[] { loadout.LeftWeapon, loadout.RightWeapon }.Where(w => w).ToArray();
                        Assert.That(equipped, Is.Not.Empty, unit.name + " must use equipped WeaponData.");
                        foreach (var weapon in equipped)
                            Assert.That(weapon.IsValid(out var weaponReason), Is.True, unit.name + ": " + weaponReason);
                        var combat = unit.GetComponent<UnitCombat>();
                        Assert.That(combat, Is.Not.Null, unit.name);
                        foreach (var slot in new[] { WeaponSlot.LeftHand, WeaponSlot.RightHand })
                        {
                            var weapon = slot == WeaponSlot.LeftHand ? loadout.LeftWeapon : loadout.RightWeapon;
                            Assert.That(combat.TryGetAttack(slot, out var attack, out _), Is.EqualTo(weapon != null), unit.name + ": " + slot);
                            if (weapon) Assert.That(attack.Weapon, Is.SameAs(weapon));
                        }
                    }
                }
                finally { EditorSceneManager.CloseScene(scene, true); }
            }
        }
    }
}
