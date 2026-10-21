#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class SqurdWeaponPlayTests
    {
        [UnityTest] public IEnumerator SceneUsesWeaponDataInPreviewAndActualFire()
        {
            const string path = "Assets/_Game/Scenes/SqurdWeaponTest.unity";
            Assert.That(File.Exists(path), Is.True);
            var bytes = File.ReadAllBytes(path);
            EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            try
            {
                var units = Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None);
                Assert.That(units.Length, Is.EqualTo(6)); Assert.That(units.All(u => u.IsInitialized), Is.True);
                var player = units.First(u => u.Team == UnitTeam.Player &&
                    u.GetComponent<UnitWeaponLoadout>().RightWeapon.name == "StandardRifle");
                var enemy = units.First(u => u.Team == UnitTeam.Enemy);
                var grid = Object.FindFirstObjectByType<GridManager>();
                var input = Object.FindFirstObjectByType<PlayerInputController>();
                var combat = Object.FindFirstObjectByType<CombatSystem>();
                // Move only the runtime target for a deterministic shot; never save the authored placement.
                var cell = grid.GetCells().First(c => c.Walkable && !c.Occupied &&
                    CombatMath.GetGridDistance(player.CurrentGridPosition, c.Position) == 3 && grid.HasClearShot(player.CurrentGridPosition, c.Position));
                Assert.That(grid.TryMoveUnit(enemy, cell.Position), Is.True);
                enemy.transform.position = cell.WorldPosition + Vector3.up * enemy.HeightOffset;
                Assert.That(input.SelectUnit(player), Is.True); Assert.That(input.BeginAttack(), Is.True);
                Assert.That(input.SelectedWeaponSlot, Is.EqualTo(WeaponSlot.RightHand));
                Assert.That(input.TrySelectTarget(enemy), Is.True);
                var preview = input.CurrentAttackPreview;
                Assert.That(preview.CanAttack, Is.True, preview.FailureReason);
                Assert.That(preview.WeaponName, Is.EqualTo("Standard Beam Rifle"));
                Assert.That(preview.MinDamage, Is.EqualTo(4)); Assert.That(preview.MaxDamage, Is.EqualTo(6));
                Assert.That(input.GetComponent<BodyTargetUI>().AttackInfoText, Is.EqualTo("Damage 4-6 / AP 3"));
                // UI rendering and screen sizes are covered by WeaponInputTests.
                combat.SetRollSource(() => 0); combat.SetDamageRollSource((min, max) => max);
                Assert.That(input.TryAttackSelected().DamageDealt, Is.EqualTo(6));
                Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
            }
            finally
            {
                var old = SceneManager.GetActiveScene();
                SceneManager.SetActiveScene(SceneManager.CreateScene("Weapon scene cleanup"));
                SceneManager.UnloadSceneAsync(old);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
            }
            yield return null;
        }
    }
}
#endif
