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
    public class SquadWeaponRegressionTests
    {
        private const string Path = "Assets/_Game/Scenes/SqurdWeaponTest.unity";
        private byte[] bytes;
        private BattleUnit[] players, enemies;
        private TurnManager turns;
        private PlayerInputController input;
        private CombatSystem combat;
        [UnitySetUp]
        public IEnumerator Load()
        {
            Assert.That(File.Exists(Path), Is.True);
            bytes = File.ReadAllBytes(Path);
            EditorSceneManager.LoadSceneInPlayMode(Path, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            var units = Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None);
            players = units.Where(u => u.Team == UnitTeam.Player).OrderBy(u => u.StartGridPosition.x).ToArray();
            enemies = units.Where(u => u.Team == UnitTeam.Enemy).ToArray();
            Assert.That(players.Length, Is.EqualTo(3)); Assert.That(enemies.Length, Is.EqualTo(3));
            Assert.That(units.All(u => u.IsAlive), Is.True);
            turns = Object.FindFirstObjectByType<TurnManager>();
            input = Object.FindFirstObjectByType<PlayerInputController>();
            combat = Object.FindFirstObjectByType<CombatSystem>();
            combat.SetRollSource(() => 0);
            combat.SetDamageRollSource((min, max) => (min + max) / 2);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (bytes == null) yield break;
            var old = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Squad test cleanup"));
            yield return SceneManager.UnloadSceneAsync(old);
            Assert.That(File.ReadAllBytes(Path), Is.EqualTo(bytes), "Playing must not save the scene.");
        }
        private IEnumerator WaitForPlayerTurn()
        {
            float deadline = Time.realtimeSinceStartup + 20;
            while (!turns.IsPlayerTurn && !turns.IsBattleOver && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(enemies.Count(e => e && e.Movement.IsMoving), Is.LessThanOrEqualTo(1));
                yield return null;
            }
            Assert.That(turns.IsPlayerTurn, Is.True, "Enemy phase must finish.");
            Assert.That(turns.IsBattleOver, Is.False);
        }
        [UnityTest]
        public IEnumerator PlayerVisualFollowsMovementAndHidesOnEjectionOrDestruction()
        {
            foreach (var player in players)
            {
                Assert.That(player.transform.Find("Visual"), Is.Not.Null);
                Assert.That(player.GetComponent<MeshRenderer>().enabled, Is.False);
                Assert.That(player.GetComponent<CapsuleCollider>().enabled, Is.True);
            }
            var unit = players[0];
            var visual = unit.transform.Find("Visual");
            var before = visual.position;
            var rootBefore = unit.transform.position;
            input.SelectUnit(unit);
            Assert.That(input.BeginMove(), Is.True);
            Assert.That(input.TryMoveSelected(unit.CurrentGridPosition + Vector2Int.up), Is.True);
            float deadline = Time.realtimeSinceStartup + 3;
            while (unit.Movement.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(unit.Movement.IsMoving, Is.False);
            Assert.That(unit.Movement.TryKeepArrivalFacing(), Is.True);
            Assert.That(visual.position - before, Is.EqualTo(unit.transform.position - rootBefore));
            Assert.That(input.TryEjectSelected(), Is.True);
            Assert.That(unit.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled), Is.True);
            Assert.That(unit.GetComponent<CapsuleCollider>().enabled, Is.False);
            players[1].Health.ApplyDamage(TargetBodyPart.Torso, 20);
            Assert.That(players[1].GetComponentsInChildren<Renderer>(true).All(r => !r.enabled), Is.True);
            Assert.That(turns.IsBattleOver, Is.False);
        }
        [UnityTest]
        public IEnumerator SquadMovesWithIndependentAPThenBothTeamsAttack()
        {
            // Keep this combat-cycle fixture detected without changing the saved scene.
            foreach (var unit in players.Concat(enemies)) unit.Vision.Configure(100);
            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i];
                input.SelectUnit(player);
                Assert.That(input.BeginMove(), Is.True);
                Assert.That(input.TryMoveSelected(player.CurrentGridPosition + Vector2Int.up), Is.True);
                float deadline = Time.realtimeSinceStartup + 3;
                while (player.Movement.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(player.Movement.IsMoving, Is.False);
                Assert.That(player.Movement.TryKeepArrivalFacing(), Is.True);
                Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(4));
                for (int j = 0; j < players.Length; j++)
                    Assert.That(players[j].ActionPoints.CurrentPoints, Is.EqualTo(j <= i ? 4 : 5));
            }
            int rolls = 0;
            combat.SetRollSource(() => { rolls++; return 0; });
            Assert.That(turns.TryEndTurn(), Is.True);
            yield return WaitForPlayerTurn();
            Assert.That(enemies.All(e => e.Mobility.MovedCells > 0), Is.True);
            Assert.That(players.All(p => p.ActionPoints.CurrentPoints == 5), Is.True);
            for (int i = 0; i < 4 && rolls == 0; i++)
            {
                Assert.That(turns.TryEndTurn(), Is.True);
                yield return WaitForPlayerTurn();
            }
            Assert.That(rolls, Is.GreaterThan(0), "Enemies must reach and attack the squad.");
            Assert.That(players.Any(p => !p || p.Health.GetCurrentHP(TargetBodyPart.Torso) < 20), Is.True);
            var attacker = players.First(p => p && enemies.Any(e => e && combat.GetPreview(p, e).CanAttack));
            var target = enemies.First(e => e && combat.GetPreview(attacker, e).CanAttack);
            input.SelectUnit(attacker);
            Assert.That(input.BeginAttack(), Is.True);
            Assert.That(input.TrySelectTarget(target), Is.True);
            var preview = input.CurrentAttackPreview;
            Assert.That(target.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(input.TryAttackSelected().WasExecuted, Is.True);
            Assert.That(attacker.ActionPoints.CurrentPoints, Is.EqualTo(5 - preview.APCost));
            Assert.That(target.Health.GetCurrentHP(TargetBodyPart.Torso),
                Is.EqualTo(20 - (preview.MinDamage + preview.MaxDamage) / 2));
            Assert.That(players.Where(p => p && p != attacker).All(p => p.ActionPoints.CurrentPoints == 5), Is.True);
        }
        [UnityTest]
        public IEnumerator SavedSquadSupportsPartDamageCriticalEjectionAndRemainingAllies()
        {
            var grid = Object.FindFirstObjectByType<GridManager>();
            var attacker = players.First(p => p.Combat.GetAttackSlots(AttackType.Melee).Count > 0 &&
                p.GetComponent<UnitWeaponLoadout>().RightWeapon.name == "StandardRifle");
            var ally = players.First(p => p != attacker);
            var target = enemies[0];
            grid.ReleaseUnit(attacker); grid.ReleaseUnit(target);
            var free = grid.GetCells().Where(c => c.Walkable && !c.Occupied).ToArray();
            GridCell source = null, destination = null;
            foreach (var cell in free)
            {
                destination = free.FirstOrDefault(other => CombatMath.GetGridDistance(cell.Position, other.Position) == 1 &&
                    grid.HasClearShot(cell.Position, other.Position) && grid.HasMeleeReach(cell.Position, other.Position, 2));
                if (destination == null) continue;
                source = cell; break;
            }
            Assert.That(source, Is.Not.Null, "Saved grid needs an open adjacent pair for this runtime fixture.");
            Assert.That(grid.TryPlaceUnit(attacker, source.Position), Is.True);
            Assert.That(grid.TryPlaceUnit(target, destination.Position), Is.True);
            attacker.transform.position = source.WorldPosition + Vector3.up * attacker.HeightOffset;
            target.transform.position = destination.WorldPosition + Vector3.up * target.HeightOffset;
            Physics.SyncTransforms();
            target.Health.SetDestructionRollSource(() => 99);
            var ranged = combat.TryAttack(attacker, target, AttackType.Ranged, TargetBodyPart.Cockpit);
            Assert.That(ranged.Hit, Is.True);
            Assert.That(target.Health.GetCurrentHP(TargetBodyPart.Cockpit), Is.EqualTo(1));
            Assert.That(target.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            attacker.ActionPoints.ResetForTurn();
            var melee = combat.TryAttack(attacker, target, AttackType.Melee, TargetBodyPart.Head);
            Assert.That(melee.Hit, Is.True);
            Assert.That(target.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(12));
            Assert.That(target.Health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(6));
            foreach (var foe in enemies) MechStateTestRules.MakeCritical(foe);
            MechStateTestRules.MakeCritical(attacker);
            Assert.That(turns.IsBattleOver, Is.False);
            Assert.That(input.SelectUnit(attacker), Is.True);
            Assert.That(input.TryEjectSelected(), Is.True);
            Assert.That(attacker.Health.State, Is.EqualTo(MechState.Ejected));
            Assert.That(attacker.ActionPoints.CurrentPoints, Is.EqualTo(1));
            Assert.That(turns.CanAct(ally), Is.True);
            Assert.That(turns.TryEndTurn(), Is.True);
            yield return WaitForPlayerTurn();
            Assert.That(ally.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(turns.CanAct(ally), Is.True);
            Assert.That(attacker.ActionPoints.CurrentPoints, Is.EqualTo(1));
            foreach (var foe in enemies) foe.Health.ApplyDamage(TargetBodyPart.Torso, 20);
            Assert.That(turns.IsBattleOver, Is.True); Assert.That(turns.IsPlayerVictory, Is.True);
            Assert.That(attacker.Health.PilotEjected, Is.True);
        }
        [UnityTest]
        public IEnumerator LosingOneAllyKeepsBattleRunningAndDefeatingAllEnemiesWins()
        {
            players[0].Health.ApplyDamage(TargetBodyPart.Torso, 20);
            Assert.That(turns.IsBattleOver, Is.False);
            foreach (var enemy in enemies) enemy.Health.ApplyDamage(TargetBodyPart.Torso, 20);
            Assert.That(turns.IsBattleOver, Is.True);
            Assert.That(turns.IsPlayerVictory, Is.True);
            Assert.That(turns.TryEndTurn(), Is.False);
            yield return null;
        }
    }
}
#endif
