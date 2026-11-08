using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class CombatFeedbackPlayTests
    {
        private BackpackTestBattle b;
        private OverwatchTestBattle o;
        private Component presenter;
        private Component Attach(GameObject root, CombatSystem combat, TurnManager turns)
        {
            var type = typeof(CombatSystem).Assembly.GetType("MechaXCOM.CombatFeedbackPresenter");
            Assert.That(type, Is.Not.Null, "Feedback presentation must be shared by all attack paths.");
            var camera = new GameObject("Feedback camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
            camera.transform.position = new Vector3(10, 20, -10); camera.transform.LookAt(new Vector3(5, 0, 5));
            var value = root.AddComponent(type);
            JsonUtility.FromJsonOverwrite("{\"duration\":0.12}", value);
            type.GetMethod("Initialize").Invoke(value, new object[] { combat, turns, camera });
            return value;
        }
        private object[] Entries => ((IEnumerable)presenter.GetType().GetProperty("Entries").GetValue(presenter)).Cast<object>().ToArray();
        private string Text(int index = 0) => (string)Entries[index].GetType().GetProperty("Text").GetValue(Entries[index]);
        private bool Presenting(TurnManager turns) => (bool)typeof(TurnManager).GetProperty("IsPresentingAttack").GetValue(turns);
        private IEnumerator Finish(TurnManager turns)
        {
            float end = Time.realtimeSinceStartup + 2;
            while (Presenting(turns) && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(Presenting(turns), Is.False);
        }
        [UnityTearDown] public IEnumerator Cleanup() { b?.Dispose(); o?.Dispose(); b = null; o = null; yield return null; }

        [UnityTest] public IEnumerator DirectHitAppliesImmediatelyAndLocksNextActionUntilPopupEnds()
        {
            b = new BackpackTestBattle(); presenter = Attach(b.Root, b.Combat, b.Turns);
            int before = b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso);
            var hit = b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.RightHand);
            Assert.That(hit.WasExecuted && hit.Hit, Is.True); Assert.That(Entries.Length, Is.EqualTo(1));
            Assert.That(Text(), Is.EqualTo("Body -" + hit.DamageDealt));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(before - hit.DamageDealt));
            Assert.That(b.Turns.IsAnyUnitBusy, Is.True); Assert.That(b.Player.CanTakeAction, Is.False);
            Assert.That(b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.RightHand).WasExecuted, Is.False);
            Assert.That(b.Turns.TryEndTurn(), Is.False); Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(5));
            yield return Finish(b.Turns); Assert.That(b.Turns.CanAct(b.Player), Is.True);
            yield return null; Assert.That(Entries, Is.Empty);
        }
        [UnityTest] public IEnumerator MissUsesTheSamePauseAndCostsOnlyOneShot()
        {
            b = new BackpackTestBattle(); presenter = Attach(b.Root, b.Combat, b.Turns); b.Combat.SetRollSource(() => 99);
            var hit = b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.RightHand);
            Assert.That(hit.WasExecuted && !hit.Hit, Is.True); Assert.That(Text(), Is.EqualTo("MISS"));
            Assert.That(Presenting(b.Turns), Is.True); yield return Finish(b.Turns);
        }
        [UnityTest] public IEnumerator MeleeCriticalUsesEnglishFeedbackAndUnscaledDuration()
        {
            b = new BackpackTestBattle(AttackType.Melee); presenter = Attach(b.Root, b.Combat, b.Turns);
            b.Combat.SetCriticalRollSource(() => 0);
            float oldScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                var hit = b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.RightHand);
                Assert.That(hit.IsCritical, Is.True); Assert.That(Text(), Is.EqualTo("CRITICAL!\nBody -" + hit.DamageDealt));
                yield return Finish(b.Turns); Assert.That(b.Turns.CanAct(b.Player), Is.True);
            }
            finally { Time.timeScale = oldScale; }
        }
        [UnityTest] public IEnumerator IndirectTargetsShowSimultaneouslyAndHiddenEnemyDoesNotLeak()
        {
            b = new BackpackTestBattle(); presenter = Attach(b.Root, b.Combat, b.Turns);
            IndirectTestSupport.Configure(b, 2); b.Combat.SetIndirectPartIndexRollSource(_ => 0);
            var hit = b.Combat.TryIndirectAttack(b.Player, new Vector2Int(2, 1), WeaponSlot.RightHand);
            Assert.That(hit.WasExecuted, Is.True); Assert.That(Entries.Length, Is.EqualTo(2));
            Assert.That(Text(0), Does.Match(@"^Body -\d+$")); Assert.That(Text(1), Does.Match(@"^Body -\d+$"));
            yield return Finish(b.Turns);
            b.Player.ActionPoints.ResetForTurn(); b.Player.Vision.Configure(1); b.Turns.Visibility.Refresh();
            JsonUtility.FromJsonOverwrite("{\"blastRadius\":1}", b.Weapon);
            Assert.That(b.Combat.TryIndirectAttack(b.Player, b.Enemy.CurrentGridPosition, WeaponSlot.RightHand).WasExecuted, Is.True);
            Assert.That(Entries, Is.Empty); Assert.That(Presenting(b.Turns), Is.False);
        }
        [UnityTest] public IEnumerator DestroyedCoverKeepsSnapshotAndReleasingPresenterUnblocksActions()
        {
            b = new BackpackTestBattle(); presenter = Attach(b.Root, b.Combat, b.Turns);
            JsonUtility.FromJsonOverwrite("{\"rangedCategory\":2}", b.Weapon);
            var cover = DestructibleCoverTestSupport.Add(b, new Vector2Int(4, 1), 1); DestructibleCoverTestSupport.Build(b);
            Assert.That(b.Combat.TryAttackCover(b.Player, cover, WeaponSlot.RightHand).WasDestroyed, Is.True);
            Assert.That(Text(), Is.EqualTo("-1"));
            Assert.That(Presenting(b.Turns), Is.True); ((Behaviour)presenter).enabled = false;
            Assert.That(Presenting(b.Turns), Is.False); Assert.That(Entries, Is.Empty);
            ((Behaviour)presenter).enabled = true; yield return null; Assert.That(Entries, Is.Empty);
        }
        [UnityTest] public IEnumerator LethalPopupDelaysBattleEndAndSurvivesTargetRemoval()
        {
            b = new BackpackTestBattle(); presenter = Attach(b.Root, b.Combat, b.Turns);
            JsonUtility.FromJsonOverwrite("{\"minDamage\":5000,\"maxDamage\":5000}", b.Weapon);
            var hit = b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.RightHand);
            Assert.That(hit.TargetDestroyed, Is.True);
            Assert.That(Text(), Is.EqualTo("Body -" + hit.DamageDealt)); Assert.That(b.Turns.IsBattleOver, Is.False);
            yield return Finish(b.Turns); yield return null; Assert.That(b.Turns.IsBattleOver, Is.True);
        }
        [UnityTest] public IEnumerator EnemyTurnWaitsForTheSamePopupBeforeReturningToPlayer()
        {
            b = new BackpackTestBattle(); presenter = Attach(b.Root, b.Combat, b.Turns);
            var ai = b.Root.AddComponent<EnemyAIController>(); ai.Initialize(b.Grid, b.Combat, b.Turns); b.Turns.ConfigureEnemyAI(ai);
            Assert.That(b.Turns.TryEndTurn(), Is.True);
            float end = Time.realtimeSinceStartup + 2;
            while (Entries.Length == 0 && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(Entries.Length, Is.EqualTo(1)); Assert.That(b.Turns.IsPlayerTurn, Is.False);
            yield return Finish(b.Turns);
            while (!b.Turns.IsPlayerTurn && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(b.Turns.IsPlayerTurn, Is.True);
        }
        [UnityTest] public IEnumerator OverwatchWaitsBeforeNextWatcherAndMovementContinuation()
        {
            o = new OverwatchTestBattle(); presenter = Attach(o.Root, o.Combat, o.Turns); o.AttachDecisionReceiver();
            o.Arm(o.Watcher); o.Arm(o.Second); o.StartTeam(UnitTeam.Enemy);
            Assert.That(o.Mover.Movement.TryMove(new Vector2Int(3, 3)), Is.True); yield return o.WaitForDecision();
            Assert.That(o.Overwatch.TryFire(o.Overwatch.PendingDecision.Value.Id, TargetBodyPart.Torso).WasExecuted, Is.True);
            Assert.That(Entries.Length, Is.EqualTo(1)); yield return null;
            Assert.That(o.Overwatch.PendingDecision.HasValue, Is.False); Assert.That(o.Mover.Movement.IsMoving, Is.True);
            yield return Finish(o.Turns); yield return o.WaitForDecision();
            Assert.That(o.Overwatch.PendingDecision.Value.Watcher, Is.SameAs(o.Second));
            o.Overwatch.TrySkip(o.Overwatch.PendingDecision.Value.Id); yield return null;
        }
    }
}
