using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class OverwatchDecisionTests
    {
        private OverwatchTestBattle battle;
        private OverwatchDecisionTestReceiver receiver;
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        [SetUp] public void Setup() => battle = new OverwatchTestBattle();
        [UnityTearDown] public IEnumerator Cleanup() { battle.Dispose(); yield return null; }

        private object Call(string name, params object[] args)
        {
            var method = typeof(OverwatchSystem).GetMethod(name);
            Assert.That(method, Is.Not.Null, name + " must support an explicit overwatch decision.");
            return method.Invoke(battle.Overwatch, args);
        }
        private OverwatchDecision? Pending
        {
            get
            {
                var property = typeof(OverwatchSystem).GetProperty("PendingDecision");
                Assert.That(property, Is.Not.Null, "Reaction fire must wait for a decision.");
                return (OverwatchDecision?)property.GetValue(battle.Overwatch);
            }
        }
        private AttackResult Fire(long id, TargetBodyPart part = TargetBodyPart.Torso) =>
            (AttackResult)Call("TryFire", id, part);
        private bool Skip(long id) => (bool)Call("TrySkip", id);
        private AttackPreview Preview(long id, TargetBodyPart part = TargetBodyPart.Torso) =>
            (AttackPreview)Call("GetDecisionPreview", id, part);
        private bool CanReact(BattleUnit watcher, BattleUnit target) => (bool)typeof(UnitOverwatch)
            .GetMethod("CanReactTo", Hidden).Invoke(watcher.Overwatch, new object[] { target });
        private void Bind()
        {
            receiver = new OverwatchDecisionTestReceiver();
            Call("ConfigureDecisionReceiver", receiver);
        }
        private void Begin(bool secondWatcher = false)
        {
            Bind(); battle.Arm(); if (secondWatcher) battle.Arm(battle.Second);
            battle.StartTeam(UnitTeam.Enemy);
            Assert.That(battle.Mover.Movement.TryMove(new Vector2Int(3, 2)), Is.True);
        }
        private IEnumerator WaitForDecision()
        {
            float deadline = Time.realtimeSinceStartup + 3;
            while (!Pending.HasValue && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Pending.HasValue, Is.True, "An eligible cell must offer a decision.");
        }
        private IEnumerator Finish()
        {
            float deadline = Time.realtimeSinceStartup + 4;
            while (battle.Mover.Movement.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(battle.Mover.Movement.IsMoving, Is.False);
            Assert.That(Pending.HasValue || battle.Overwatch.IsResolving, Is.False);
        }

        [UnityTest] public IEnumerator WaitsWithoutAutoFire()
        {
            int rolls = 0; battle.Combat.SetRollSource(() => { rolls++; return 0; });
            Begin(); yield return WaitForDecision();
            var decision = Pending.Value;
            Assert.That(decision.Watcher, Is.SameAs(battle.Watcher));
            Assert.That(decision.Target, Is.SameAs(battle.Mover));
            Assert.That(decision.Slot, Is.EqualTo(WeaponSlot.RightHand));
            for (int i = 0; i < 3; i++)
            {
                yield return null;
                Assert.That(battle.Mover.CurrentGridPosition, Is.EqualTo(new Vector2Int(3, 3)));
                Assert.That(battle.Mover.ActionPoints.CurrentPoints, Is.EqualTo(4));
                Assert.That(battle.Mover.Mobility.MovedCells, Is.EqualTo(1));
                Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
                Assert.That(battle.Turns.IsAnyUnitBusy, Is.True);
                Assert.That((bool)Call("IsResolvingMover", battle.Mover), Is.True);
                Assert.That(rolls, Is.Zero);
            }
            Assert.That(Skip(decision.Id), Is.True); yield return Finish();
        }

        [UnityTest] public IEnumerator FireUsesSelectedPartAndKeepsGuard()
        {
            Begin(); yield return WaitForDecision(); long id = Pending.Value.Id;
            var preview = Preview(id, TargetBodyPart.Head);
            Assert.That(preview.CanAttack, Is.True);
            Assert.That(preview.HitChance, Is.EqualTo(48));
            Assert.That(preview.ReactionMultiplier, Is.EqualTo(.75f));
            Assert.That(preview.APCost, Is.Zero);
            var result = Fire(id, TargetBodyPart.Head);
            Assert.That(result.WasExecuted && result.Hit, Is.True);
            Assert.That(result.HitChance, Is.EqualTo(preview.HitChance));
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(1));
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.True);
            Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(3));
            yield return Finish();
        }

        [UnityTest] public IEnumerator MissAndSkipConsumeOnlyThisTarget()
        {
            int rolls = 0; battle.Combat.SetRollSource(() => { rolls++; return 99; });
            Begin(true); yield return WaitForDecision(); long first = Pending.Value.Id;
            Assert.That(Fire(first).Hit, Is.False);
            yield return WaitForDecision();
            Assert.That(Pending.Value.Watcher, Is.SameAs(battle.Second));
            Assert.That(Skip(Pending.Value.Id), Is.True);
            yield return Finish();
            Assert.That(rolls, Is.EqualTo(1));
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            foreach (var watcher in new[] { battle.Watcher, battle.Second })
            {
                Assert.That(watcher.Overwatch.IsActive, Is.True);
                Assert.That(CanReact(watcher, battle.Mover), Is.False);
                Assert.That(CanReact(watcher, battle.Spare), Is.True);
                Assert.That(watcher.ActionPoints.CurrentPoints, Is.EqualTo(3));
            }
        }

        [UnityTest] public IEnumerator StaleAndDoubleFireDoNotExecute()
        {
            int rolls = 0; battle.Combat.SetRollSource(() => { rolls++; return 0; });
            Begin(true); yield return WaitForDecision(); long first = Pending.Value.Id;
            Assert.That(Fire(first).WasExecuted, Is.True);
            Assert.That(Fire(first).WasExecuted || Skip(first), Is.False);
            yield return WaitForDecision(); long second = Pending.Value.Id;
            Assert.That(second, Is.GreaterThan(first));
            Assert.That(Fire(first).WasExecuted || Skip(first) || Preview(first).CanAttack, Is.False);
            Assert.That(Pending.Value.Id, Is.EqualTo(second));
            Assert.That(rolls, Is.EqualTo(1));
            Assert.That(Skip(second), Is.True); yield return Finish();
        }

        [UnityTest] public IEnumerator DirectReactionCallDuringPendingIsRejected()
        {
            Begin(); yield return WaitForDecision();
            var method = typeof(CombatSystem).GetMethod("TryOverwatchAttack", Hidden);
            Assert.That(method, Is.Not.Null);
            var result = (AttackResult)method.Invoke(battle.Combat,
                new object[] { battle.Watcher, battle.Mover, WeaponSlot.RightHand, TargetBodyPart.Head });
            Assert.That(result.WasExecuted, Is.False);
            Assert.That(battle.Combat.TryAttack(battle.Watcher, battle.Mover).WasExecuted, Is.False);
            Assert.That(CanReact(battle.Watcher, battle.Mover), Is.True);
            Assert.That(Skip(Pending.Value.Id), Is.True); yield return Finish();
        }

        [UnityTest] public IEnumerator InvalidPartDoesNotConsumeOrCloseValidRequest()
        {
            Begin(); yield return WaitForDecision(); long id = Pending.Value.Id;
            Assert.That(Preview(id, (TargetBodyPart)99).CanAttack, Is.False);
            Assert.That(Fire(id, (TargetBodyPart)99).WasExecuted, Is.False);
            Assert.That(Pending.Value.Id, Is.EqualTo(id));
            Assert.That(CanReact(battle.Watcher, battle.Mover), Is.True);
            Assert.That(Fire(id, TargetBodyPart.Head).WasExecuted, Is.True); yield return Finish();
        }

        [UnityTest] public IEnumerator WeaponOrVisionInvalidatedWhilePendingDoesNotFire()
        {
            Begin(true); yield return WaitForDecision(); long first = Pending.Value.Id;
            battle.Watcher.Vision.Configure(1);
            Assert.That(Fire(first).WasExecuted || Preview(first).CanAttack, Is.False);
            Assert.That(CanReact(battle.Watcher, battle.Mover), Is.True);
            yield return WaitForDecision(); long second = Pending.Value.Id;
            var loadout = battle.Second.GetComponent<UnitWeaponLoadout>();
            loadout.Configure(null, GripMode.OneHand, null, GripMode.OneHand);
            Assert.That(Fire(second).WasExecuted, Is.False);
            // No shot/Skip consumed the first watcher's opportunity. On the next cell the
            // target is within its reduced vision again, so it correctly offers a fresh request.
            yield return WaitForDecision();
            Assert.That(Pending.Value.Watcher, Is.SameAs(battle.Watcher));
            Assert.That(Pending.Value.Id, Is.GreaterThan(second));
            Assert.That(battle.Mover.CurrentGridPosition, Is.EqualTo(new Vector2Int(3, 2)));
            Assert.That(Skip(Pending.Value.Id), Is.True);
            yield return Finish();
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }

        [UnityTest] public IEnumerator BuildingAddedWhilePendingPreventsFire()
        {
            Begin(); yield return WaitForDecision(); long id = Pending.Value.Id;
            battle.Building(new Vector2Int(3, 2));
            Assert.That(Fire(id).WasExecuted || Skip(id), Is.False);
            Assert.That(CanReact(battle.Watcher, battle.Mover), Is.True);
            yield return Finish();
        }

        [UnityTest] public IEnumerator ReceiverLostReleasesBatchWithoutConsumingTarget()
        {
            Begin(true); yield return WaitForDecision(); long id = Pending.Value.Id;
            receiver.IsReady = false;
            yield return Finish();
            Assert.That(Fire(id).WasExecuted || Skip(id), Is.False);
            Assert.That(CanReact(battle.Watcher, battle.Mover), Is.True);
            Assert.That(CanReact(battle.Second, battle.Mover), Is.True);
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }

        [UnityTest] public IEnumerator OldReceiverCannotUnbindReplacement()
        {
            Begin(); yield return WaitForDecision(); long oldId = Pending.Value.Id;
            var oldReceiver = receiver;
            receiver = new OverwatchDecisionTestReceiver();
            Call("ConfigureDecisionReceiver", receiver);
            Call("UnregisterDecisionReceiver", oldReceiver);
            Assert.That(Fire(oldId).WasExecuted, Is.False);
            yield return WaitForDecision();
            Assert.That(Pending.Value.Id, Is.GreaterThan(oldId));
            Assert.That(Fire(Pending.Value.Id).WasExecuted, Is.True); yield return Finish();
        }

        [UnityTest] public IEnumerator ReentrantInputsDuringShotCannotFireOrSkipAgain()
        {
            Begin(true); yield return WaitForDecision(); long id = Pending.Value.Id;
            int rolls = 0;
            battle.Combat.SetRollSource(() =>
            {
                rolls++;
                Assert.That(Fire(id).WasExecuted || Skip(id), Is.False);
                return 0;
            });
            Assert.That(Fire(id).WasExecuted, Is.True);
            yield return WaitForDecision();
            Assert.That(Skip(Pending.Value.Id), Is.True); yield return Finish();
            Assert.That(rolls, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator ServiceDisabledDuringShotClearsPendingAndBusyState()
        {
            Begin(true); yield return WaitForDecision(); long id = Pending.Value.Id;
            battle.Combat.SetRollSource(() => { battle.Overwatch.enabled = false; return 0; });
            Assert.That(Fire(id).WasExecuted, Is.True);
            yield return Finish();
            Assert.That(Fire(id).WasExecuted, Is.False);
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
        }

        [UnityTest] public IEnumerator NoReceiverDoesNotAutoFireOrLeaveBusyState()
        {
            battle.Arm(); battle.StartTeam(UnitTeam.Enemy);
            Assert.That(battle.Mover.Movement.TryMove(new Vector2Int(3, 2)), Is.True);
            yield return Finish();
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(CanReact(battle.Watcher, battle.Mover), Is.True);
        }

        [UnityTest] public IEnumerator ReactionSharesCoverGripDistanceHeadAndFinalRounding()
        {
            var loadout = battle.Watcher.GetComponent<UnitWeaponLoadout>();
            var weapon = loadout.RightWeapon;
            JsonUtility.FromJsonOverwrite("{\"gripRequirement\":2,\"twoHandHitMultiplier\":1.1,\"distanceBands\":[{\"MaxDistance\":2,\"Multiplier\":0.9}]}", weapon);
            loadout.Configure(null, GripMode.OneHand, weapon, GripMode.TwoHands);
            battle.Watcher.Health.ApplyDamage(TargetBodyPart.Head, 6);
            battle.Grid.TryGetCell(new Vector2Int(3, 3), out var cell);
            cell.SetCover(CoverDirection.South, CoverType.Full);
            battle.Combat.SetRollSource(() => 17);
            Begin(); yield return WaitForDecision(); long id = Pending.Value.Id;
            var preview = Preview(id, TargetBodyPart.Head);
            Assert.That(preview.HitChance, Is.EqualTo(18)); // 80 * .8 * .5 * .75 * 1.1 * .9 * .75
            Assert.That(preview.PartMultiplier, Is.EqualTo(.8f));
            Assert.That(preview.CoverMultiplier, Is.EqualTo(.5f));
            var result = Fire(id, TargetBodyPart.Head);
            Assert.That(result.WasExecuted && result.Hit, Is.True);
            Assert.That(result.HitChance, Is.EqualTo(preview.HitChance));
            yield return Finish();
        }

        [UnityTest] public IEnumerator ZeroAPCanFireWeaponWhoseNormalAttackCostsFive()
        {
            battle.Watcher.Combat.ConfigureTestRanged(5, 80, 7, 5);
            battle.Watcher.ActionPoints.TrySpend(3);
            Begin(); yield return WaitForDecision(); long id = Pending.Value.Id;
            Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.Zero);
            Assert.That(Preview(id).APCost, Is.Zero);
            Assert.That(Fire(id).WasExecuted, Is.True);
            Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.Zero);
            yield return Finish();
        }

        [UnityTest] public IEnumerator UnregisterCurrentReceiverCancelsPendingWithoutConsumingTarget()
        {
            Begin(); yield return WaitForDecision(); long id = Pending.Value.Id;
            Call("UnregisterDecisionReceiver", receiver);
            Assert.That(Pending.HasValue || battle.Overwatch.IsResolving, Is.False);
            Assert.That(Skip(id) || Fire(id).WasExecuted, Is.False);
            Assert.That(CanReact(battle.Watcher, battle.Mover), Is.True);
            yield return Finish();
        }

        [UnityTest] public IEnumerator LethalFireClearsAllRequestsWhenBattleEnds()
        {
            battle.Spare.Health.ApplyDamage(TargetBodyPart.Torso, 20);
            battle.Mover.Health.ApplyDamage(TargetBodyPart.Torso, 15);
            Begin(true); yield return WaitForDecision(); long id = Pending.Value.Id;
            Assert.That(Fire(id).WasExecuted, Is.True);
            yield return Finish();
            Assert.That(battle.Turns.IsBattleOver && battle.Turns.IsPlayerVictory, Is.True);
            Assert.That(Fire(id).WasExecuted || Skip(id), Is.False);
            Assert.That(battle.Second.Overwatch.IsActive, Is.False);
            Assert.That(battle.Turns.IsAnyUnitBusy, Is.False);
        }

        [UnityTest] public IEnumerator UnreadyReceiverDoesNotWaitOrAutoFire()
        {
            Bind(); receiver.IsReady = false;
            battle.Arm(); battle.StartTeam(UnitTeam.Enemy);
            Assert.That(battle.Mover.Movement.TryMove(new Vector2Int(3, 2)), Is.True);
            yield return Finish();
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(CanReact(battle.Watcher, battle.Mover), Is.True);
        }

        [UnityTest] public IEnumerator CancelMoveClearsPendingAndRejectsLateInput()
        {
            Begin(); yield return WaitForDecision(); long id = Pending.Value.Id;
            battle.Mover.Movement.CancelMove();
            Assert.That(Pending.HasValue || battle.Overwatch.IsResolving, Is.False);
            Assert.That(Fire(id).WasExecuted || Skip(id), Is.False);
            Assert.That(battle.Mover.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(CanReact(battle.Watcher, battle.Mover), Is.True);
            yield return Finish();
        }

        [UnityTest] public IEnumerator RequestIdsAreNotReusedAfterServiceReinitialization()
        {
            Begin(); yield return WaitForDecision(); long oldId = Pending.Value.Id;
            battle.Mover.Movement.CancelMove();
            battle.Overwatch.Initialize(battle.Grid, battle.Combat, battle.Turns,
                new[] { battle.Watcher, battle.Second, battle.Mover, battle.Spare });
            battle.StartTeam(UnitTeam.Player); battle.Arm(); battle.StartTeam(UnitTeam.Enemy);
            Assert.That(battle.Mover.Movement.TryMove(new Vector2Int(3, 2)), Is.True);
            yield return WaitForDecision();
            Assert.That(Pending.Value.Id, Is.GreaterThan(oldId));
            Assert.That(Fire(oldId).WasExecuted || Skip(oldId), Is.False);
            Assert.That(Skip(Pending.Value.Id), Is.True); yield return Finish();
        }
    }
}
