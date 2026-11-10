using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace MechaXCOM.Tests
{
    public class MissionControllerPlayTests
    {
        private BackpackTestBattle b;
        private bool loadedScene;
        [UnitySetUp] public IEnumerator Setup() { loadedScene = false; b = new BackpackTestBattle(); yield return null; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            b.Dispose(); yield return null;
#if UNITY_EDITOR
            if (loadedScene)
            {
                var old = SceneManager.GetActiveScene();
                SceneManager.SetActiveScene(SceneManager.CreateScene("Mission cleanup"));
                yield return SceneManager.UnloadSceneAsync(old);
            }
#endif
        }

        [UnityTest] public IEnumerator SuccessWaitsForAttackPopupAndNotifiesExactlyOnceAfterStopping()
        {
            var camera = new GameObject("Mission feedback camera").AddComponent<Camera>(); camera.transform.SetParent(b.Root.transform);
            var feedback = b.Root.AddComponent<CombatFeedbackPresenter>(); feedback.Initialize(b.Combat, b.Turns, camera);
            int notified = 0;
            b.Turns.Mission.Completed += result =>
            {
                notified++; Assert.That(b.Turns.IsBattleOver, Is.True);
                Assert.That(b.Turns.IsPresentingAttack, Is.False); Assert.That(b.Turns.CanAct(b.Player), Is.False);
                Assert.That(result.Outcome, Is.EqualTo(MissionOutcome.Success));
            };
            b.Combat.SetRollSource(() => 99);
            Assert.That(b.Combat.TryAttackWithWeapon(b.Player, b.Enemy, WeaponSlot.RightHand).WasExecuted, Is.True);
            Assert.That(b.Turns.IsPresentingAttack, Is.True);
            Assert.That(b.Turns.Mission.TrySucceed("Objective completed"), Is.True);
            Assert.That(b.Turns.IsBattleOver, Is.False); Assert.That(notified, Is.Zero);
            float deadline = Time.realtimeSinceStartup + 3;
            while (!b.Turns.IsBattleOver && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(b.Turns.IsBattleOver, Is.True); Assert.That(notified, Is.EqualTo(1));
            Assert.That(b.Turns.Mission.TryFail("Late failure"), Is.False);
            b.Turns.CheckBattleEnd(); Assert.That(notified, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator CompletionCancelsMovementAndFacing()
        {
            var target = b.Player.CurrentGridPosition + Vector2Int.up;
            Assert.That(b.Player.Movement.TryMove(target), Is.True); Assert.That(b.Player.Movement.IsMoving, Is.True);
            Assert.That(b.Turns.Mission.TrySucceed("Reached goal"), Is.True);
            Assert.That(b.Player.Movement.IsMoving || b.Player.HasPendingFacing, Is.False);
            Assert.That(b.Turns.CanAct(b.Player), Is.False); yield return null;
            Assert.That(b.Player.Movement.IsMoving, Is.False);
        }
        [UnityTest] public IEnumerator CompletionClearsArmedOverwatch()
        {
            Assert.That(b.Overwatch.TryBegin(b.Player, WeaponSlot.RightHand, FacingDirection.North), Is.True);
            Assert.That(b.Turns.Mission.TryFail("Protected target lost"), Is.True);
            Assert.That(b.Player.Overwatch.IsActive, Is.False); Assert.That(b.Overwatch.PendingDecision.HasValue, Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator FailureStopsPendingEnemyAIWithoutSpendingResources()
        {
            var ai = b.Root.AddComponent<EnemyAIController>(); ai.Initialize(b.Grid, b.Combat, b.Turns); b.Turns.ConfigureEnemyAI(ai);
            Assert.That(b.Turns.TryEndTurn(), Is.True);
            int ap = b.Enemy.ActionPoints.CurrentPoints;
            Assert.That(b.Turns.Mission.TryFail("Escort lost"), Is.True);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(b.Enemy.ActionPoints.CurrentPoints, Is.EqualTo(ap)); Assert.That(b.Turns.ActiveEnemy, Is.Null);
            Assert.That(ai.ActingUnit, Is.Null); Assert.That(b.Turns.IsPlayerVictory, Is.False);
        }
#if UNITY_EDITOR
        [UnityTest] public IEnumerator SavedScenesAutomaticallyConnectMissionAndAcceptExplicitOutcome()
        {
            loadedScene = true;
            foreach (var scene in new[] { "1V1Test", "SqurdWeaponTest" })
            {
                EditorSceneManager.LoadSceneInPlayMode("Assets/_Game/Scenes/" + scene + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null; yield return null;
                var turns = Object.FindFirstObjectByType<TurnManager>();
                Assert.That(turns.Mission, Is.Not.Null); Assert.That(turns.Mission.SucceedOnEnemyElimination, Is.True);
                Assert.That(turns.Mission.TrySucceed("Test objective completed"), Is.True);
                Assert.That(turns.IsBattleOver, Is.True); Assert.That(turns.IsPresentingTurn, Is.False);
                Assert.That(turns.Mission.Result.Reason, Is.EqualTo("Test objective completed"));
            }
        }
#endif
    }
}
