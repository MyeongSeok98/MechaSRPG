using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace MechaXCOM.Tests
{
    public class OverwatchSceneTests
    {
#if UNITY_EDITOR
        [UnityTest] public IEnumerator SavedOneVsOneSceneConnectsOverwatchAtRuntime() => CheckSavedScene("1V1Test");
        [UnityTest] public IEnumerator SavedSquadWeaponSceneConnectsOverwatchAtRuntime() => CheckSavedScene("SqurdWeaponTest");

        private IEnumerator CheckSavedScene(string name)
        {
            string path = "Assets/_Game/Scenes/" + name + ".unity";
            byte[] original = File.ReadAllBytes(path);
            EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            try
            {
                var turns = Object.FindFirstObjectByType<TurnManager>();
                var service = Object.FindFirstObjectByType<OverwatchSystem>();
                var input = Object.FindFirstObjectByType<PlayerInputController>();
                Assert.That(service, Is.Not.Null);
                Assert.That(turns.Overwatch, Is.SameAs(service));
                var units = Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None);
                Assert.That(units, Is.Not.Empty);
                foreach (var unit in units)
                {
                    Assert.That(unit.IsInitialized, Is.True, unit.name);
                    Assert.That(unit.Overwatch, Is.Not.Null, unit.name);
                    Assert.That(unit.Overwatch.IsActive, Is.False, unit.name);
                }
                var player = units.First(u => u.Team == UnitTeam.Player &&
                    u.Combat.GetAttackSlots(AttackType.Ranged).Any(s => service.CanBegin(u, s)));
                Assert.That(input.SelectUnit(player), Is.True);
                Assert.That(input.OpenOverwatchMenu(), Is.True);
                var slot = input.GetOverwatchWeaponSlots().First(s => service.CanBegin(player, s));
                int ap = player.ActionPoints.CurrentPoints;
                Assert.That(input.BeginOverwatch(slot), Is.True);
                Assert.That(input.TryChooseOverwatchFacing(FacingDirection.North), Is.True);
                Assert.That(input.TryConfirmOverwatch(), Is.True);
                Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(ap - 2));
                Assert.That(player.Overwatch.IsActive, Is.True);
                Assert.That(Object.FindFirstObjectByType<UnitActionUI>().UnitStateText, Does.Contain("OVERWATCH"));
            }
            finally
            {
                var loaded = SceneManager.GetActiveScene();
                SceneManager.SetActiveScene(SceneManager.CreateScene("Overwatch scene cleanup"));
                SceneManager.UnloadSceneAsync(loaded);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(original), "Saved scenes must stay unchanged.");
            }
            yield return null;
        }
#endif

        [UnityTest] public IEnumerator RealEnemyAIContinuesAfterReactionAndStartsNextPlayerTurn()
        {
            using (var battle = CreateAI(out var service))
            {
                var player = battle.Players[0]; var enemy = battle.Enemies[0];
                Assert.That(service.TryBegin(player, WeaponSlot.RightHand, FacingDirection.North), Is.True);
                Assert.That(battle.Turns.TryEndTurn(), Is.True);
                yield return battle.WaitForPlayerTurn();
                Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15), "One reaction hits the moving enemy.");
                Assert.That(enemy.Mobility.MovedCells, Is.EqualTo(2));
                Assert.That(enemy.ActionPoints.CurrentPoints, Is.Zero, "Two steps plus an ordinary AP 3 attack.");
                Assert.That(player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15), "The survivor fires after moving.");
                Assert.That(battle.Turns.TurnNumber, Is.EqualTo(2));
                Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(player.ActionPoints.MaxPoints));
                Assert.That(player.Overwatch.IsActive, Is.False);
                Assert.That(service.IsResolving || battle.Turns.IsAnyUnitBusy, Is.False);
                Assert.That(battle.Grid.TryGetCell(enemy.CurrentGridPosition, out var cell), Is.True);
                Assert.That(cell.OccupyingUnit, Is.SameAs(enemy));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator DisabledAndReenabledServiceDoesNotRestoreOldGuard()
        {
            using (var battle = CreateAI(out var service))
            {
                var player = battle.Players[0];
                Assert.That(service.TryBegin(player, WeaponSlot.RightHand, FacingDirection.North), Is.True);
                service.enabled = false; service.enabled = true;
                Assert.That(player.Overwatch.IsActive, Is.False);
                Assert.That(battle.Turns.TryEndTurn(), Is.True);
                yield return battle.WaitForPlayerTurn();
                Assert.That(battle.Enemies[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
                Assert.That(player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
                Assert.That(service.IsResolving || battle.Turns.IsAnyUnitBusy, Is.False);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator ReplacedThenRestoredWeaponCannotFireStaleGuardInEnemyPhase()
        {
            using (var battle = CreateAI(out var service))
            {
                var player = battle.Players[0];
                var loadout = player.GetComponent<UnitWeaponLoadout>();
                var weapon = loadout.RightWeapon;
                Assert.That(service.TryBegin(player, WeaponSlot.RightHand, FacingDirection.North), Is.True);
                loadout.Configure(null, GripMode.OneHand, null, GripMode.OneHand);
                loadout.Configure(null, GripMode.OneHand, weapon, GripMode.OneHand);
                Assert.That(battle.Turns.TryEndTurn(), Is.True);
                yield return battle.WaitForPlayerTurn();
                Assert.That(battle.Enemies[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
                Assert.That(player.Overwatch.IsActive, Is.False);
                Assert.That(service.IsResolving || battle.Turns.IsAnyUnitBusy, Is.False);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator LethalReactionDuringRealEnemyAIEndsBattleWithoutBusyLock()
        {
            using (var battle = CreateAI(out var service))
            {
                battle.Enemies[0].Health.ApplyDamage(TargetBodyPart.Torso, 15);
                Assert.That(service.TryBegin(battle.Players[0], WeaponSlot.RightHand, FacingDirection.North), Is.True);
                Assert.That(battle.Turns.TryEndTurn(), Is.True);
                yield return battle.WaitForPlayerTurn();
                yield return null;
                Assert.That(battle.Turns.IsBattleOver && battle.Turns.IsPlayerVictory, Is.True);
                Assert.That(battle.Enemies[0].Mobility.MovedCells, Is.EqualTo(1));
                Assert.That(battle.Players[0].Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
                Assert.That(battle.Turns.ActiveEnemy, Is.Null);
                Assert.That(service.IsResolving || battle.Turns.IsAnyUnitBusy, Is.False);
            }
            yield return null;
        }

        private EnemyAITestBattle CreateAI(out OverwatchSystem service)
        {
            var battle = new EnemyAITestBattle(new[] { new Vector2Int(3, 1) }, new[] { new Vector2Int(3, 4) });
            battle.Enemies[0].Combat.ConfigureTestRanged(5, 80, 1, 3);
            battle.Combat.SetDamageRollSource((min, max) => min);
            service = battle.Root.AddComponent<OverwatchSystem>();
            service.Initialize(battle.Grid, battle.Combat, battle.Turns, battle.Players.Concat(battle.Enemies).ToArray());
            return battle;
        }
    }
}
