using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class BackpackAmmoPlayTests
    {
        private BackpackTestBattle b;
        [UnityTearDown] public IEnumerator Cleanup() { b?.Dispose(); yield return null; }
        [UnityTest] public IEnumerator BackpackGuardSurvivesHandGripChangesAndBothDestroyedArms()
        {
            b = new BackpackTestBattle(capacity: 2, fireAP: 1);
            Assert.That(b.Overwatch.TryBegin(b.Player, WeaponSlot.Backpack, FacingDirection.East), Is.True);
            b.Player.GetComponent<UnitWeaponLoadout>().Configure(null, GripMode.OneHand, b.Weapon, GripMode.TwoHands);
            Assert.That(b.Player.Overwatch.IsActive, Is.True, "Backpack does not track right-hand grip.");
            b.Player.Health.ApplyDamage(TargetBodyPart.LeftArm, 8); b.Player.Health.ApplyDamage(TargetBodyPart.RightArm, 8);
            Assert.That(b.Player.Health.CanFight, Is.True); Assert.That(b.Player.Overwatch.IsActive, Is.True);
            yield return null;
        }
        [UnityTest] public IEnumerator LastBackpackReactionRoundCompletesAndReleasesMovement()
        {
            b = new BackpackTestBattle(capacity: 1, fireAP: 1);
            yield return BeginReaction();
            long id = b.Overwatch.PendingDecision.Value.Id;
            Assert.That(b.Overwatch.GetDecisionPreview(id, TargetBodyPart.Torso).AmmoLoaded, Is.EqualTo(1));
            var result = b.Overwatch.TryFire(id, TargetBodyPart.Torso);
            Assert.That(result.WasExecuted && result.Hit, Is.True); Assert.That(result.DamageDealt, Is.EqualTo(7));
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.Backpack).Loaded, Is.Zero);
            Assert.That(b.Player.Overwatch.IsActive, Is.False); Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(3));
            yield return FinishMovement();
            Assert.That(b.Overwatch.IsResolving || b.Overwatch.PendingDecision.HasValue, Is.False);
        }
        [UnityTest] public IEnumerator SkippingBackpackReactionPreservesAmmo()
        {
            b = new BackpackTestBattle(capacity: 1, fireAP: 1);
            yield return BeginReaction();
            Assert.That(b.Overwatch.TrySkip(b.Overwatch.PendingDecision.Value.Id), Is.True);
            yield return FinishMovement();
            Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.Backpack).Loaded, Is.EqualTo(1));
            Assert.That(b.Player.Overwatch.IsActive, Is.True); Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
        }
        private IEnumerator BeginReaction()
        {
            b.Overwatch.ConfigureDecisionReceiver(new OverwatchDecisionTestReceiver());
            Assert.That(b.Overwatch.TryBegin(b.Player, WeaponSlot.Backpack, FacingDirection.East), Is.True);
            b.StartEnemyTeam(); Assert.That(b.Enemy.Movement.TryMove(new Vector2Int(2, 1)), Is.True);
            float end = Time.realtimeSinceStartup + 3;
            while (!b.Overwatch.PendingDecision.HasValue && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(b.Overwatch.PendingDecision.HasValue, Is.True);
        }
        private IEnumerator FinishMovement()
        {
            float end = Time.realtimeSinceStartup + 3;
            while (b.Enemy.Movement.IsMoving && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(b.Enemy.Movement.IsMoving, Is.False);
        }
    }
}
