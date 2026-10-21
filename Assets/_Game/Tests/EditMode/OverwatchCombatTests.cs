using System.Collections;
using System.Reflection;
using NUnit.Framework;

namespace MechaXCOM.Tests
{
    public class OverwatchCombatTests
    {
        [Test]
        public void DirectReactionCallCannotBypassCommittedMovementPermission()
        {
            using (var b = new OverwatchTestBattle())
            {
                var method = typeof(CombatSystem).GetMethod("TryOverwatchAttack", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(method, Is.Not.Null, "Reaction fire needs a guarded execution path.");
                b.Arm(); b.StartTeam(UnitTeam.Enemy);
                int rolls = 0; b.Combat.SetRollSource(() => { rolls++; return 0; });
                var result = (AttackResult)method.Invoke(b.Combat, new object[] { b.Watcher, b.Mover, WeaponSlot.RightHand });
                Assert.That(result.WasExecuted, Is.False); Assert.That(rolls, Is.Zero);
                Assert.That(b.Watcher.Overwatch.IsActive, Is.True);
                Assert.That(b.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(3));
                Assert.That(b.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            }
        }
        [Test]
        public void PublicResolutionRequestOutsideMovementDoesNotFire()
        {
            using (var b = new OverwatchTestBattle())
            {
                var method = typeof(OverwatchSystem).GetMethod("ResolveAfterStep");
                Assert.That(method, Is.Not.Null);
                b.Arm(); b.StartTeam(UnitTeam.Enemy);
                var routine = (IEnumerator)method.Invoke(b.Overwatch, new object[] { b.Mover });
                while (routine.MoveNext()) { }
                Assert.That(b.Resolving, Is.False); Assert.That(b.Watcher.Overwatch.IsActive, Is.True);
                Assert.That(b.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
                Assert.That(b.Combat.TryAttack(b.Watcher, b.Mover).WasExecuted, Is.False);
            }
        }
        [Test]
        public void OrdinaryPreviewHasNeutralReactionMultiplier()
        {
            using (var b = new OverwatchTestBattle())
            {
                var field = typeof(AttackPreview).GetField("ReactionMultiplier"); Assert.That(field, Is.Not.Null);
                var preview = b.Combat.GetPreview(b.Watcher, b.Mover);
                Assert.That(preview.CanAttack, Is.True); Assert.That(field.GetValue(preview), Is.EqualTo(1f));
            }
        }
    }
}
