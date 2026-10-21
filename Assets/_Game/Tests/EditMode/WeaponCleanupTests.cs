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
            CollectionAssert.AreEquivalent(new[] { "1V1Test", "SqurdWeaponTest" }, paths);
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
                        Assert.That(loadout.RightWeapon, Is.Not.Null);
                        if (name == "1V1Test")
                        {
                            Assert.That(loadout.RightWeapon.MinDamage, Is.EqualTo(5));
                            Assert.That(loadout.RightWeapon.MaxDamage, Is.EqualTo(5));
                            Assert.That(loadout.RightWeapon.BaseHitChance, Is.EqualTo(80));
                            Assert.That(loadout.RightWeapon.Range, Is.EqualTo(7));
                            Assert.That(loadout.RightWeapon.APCost, Is.EqualTo(3));
                            Assert.That(loadout.LeftWeapon, Is.Not.Null);
                            Assert.That(loadout.LeftWeapon.MinDamage, Is.EqualTo(8));
                            Assert.That(loadout.LeftWeapon.MaxDamage, Is.EqualTo(8));
                            Assert.That(loadout.LeftWeapon.BaseHitChance, Is.EqualTo(95));
                            Assert.That(loadout.LeftWeapon.Range, Is.EqualTo(2));
                            Assert.That(loadout.LeftWeapon.APCost, Is.EqualTo(3));
                        }
                    }
                }
                finally { EditorSceneManager.CloseScene(scene, true); }
            }
        }
    }
}
