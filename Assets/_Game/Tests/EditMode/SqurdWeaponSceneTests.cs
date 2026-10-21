using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class SqurdWeaponSceneTests
    {
        private const string Target = "Assets/_Game/Scenes/SqurdWeaponTest.unity";
        [Test] public void RetainedSceneHasSixEquippedUnits()
        {
            Assert.That(File.Exists(Target), Is.True, "Create the requested weapon test scene.");
            var bytes = File.ReadAllBytes(Target);
            var target = EditorSceneManager.OpenScene(Target, OpenSceneMode.Additive);
            try
            {
                var units = target.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<BattleUnit>(true)).ToArray();
                Assert.That(units.Count(u => u.Team == UnitTeam.Player), Is.EqualTo(3));
                Assert.That(units.Count(u => u.Team == UnitTeam.Enemy), Is.EqualTo(3));
                foreach (var unit in units)
                {
                    var loadout = unit.GetComponent<UnitWeaponLoadout>(); Assert.That(loadout, Is.Not.Null);
                    Assert.That(loadout.IsValid(out var reason), Is.True, reason);
                    var combat = unit.GetComponent<UnitCombat>();
                    Assert.That(loadout.RightWeapon || loadout.LeftWeapon, Is.True, unit.name + " must be armed.");
                    foreach (var type in new[] { AttackType.Ranged, AttackType.Melee })
                        foreach (var slot in combat.GetAttackSlots(type))
                        {
                            Assert.That(combat.TryGetAttack(slot, out var stats, out reason), Is.True, reason);
                            var weapon = slot == WeaponSlot.RightHand ? loadout.RightWeapon : loadout.LeftWeapon;
                            Assert.That(stats.Weapon, Is.SameAs(weapon));
                            Assert.That(stats.MinDamage, Is.EqualTo(weapon.MinDamage));
                            Assert.That(stats.MaxDamage, Is.EqualTo(weapon.MaxDamage));
                            Assert.That(stats.Range, Is.EqualTo(weapon.Range));
                            Assert.That(stats.APCost, Is.EqualTo(weapon.APCost));
                        }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(target, true);
                Assert.That(File.ReadAllBytes(Target), Is.EqualTo(bytes));
            }
        }
    }
}
