using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    // Reflection keeps the first red run compilable before the requested runtime types exist.
    internal sealed class OverwatchTestFixture : IDisposable
    {
        public GameObject Root { get; }
        public GridManager Grid { get; }
        public BattleUnit Player { get; }
        public BattleUnit Ally { get; }
        public BattleUnit Enemy { get; }
        public TurnManager Turns { get; }
        public CombatSystem Combat { get; }
        public Component System { get; }
        public Component State => Player.GetComponent(StateType);
        public Type StateType { get; }
        public bool Active => (bool)StateType.GetProperty("IsActive").GetValue(State);

        public OverwatchTestFixture()
        {
            var systemType = typeof(BattleUnit).Assembly.GetType("MechaXCOM.OverwatchSystem");
            StateType = typeof(BattleUnit).Assembly.GetType("MechaXCOM.UnitOverwatch");
            Assert.That(systemType, Is.Not.Null, "The overwatch service must exist.");
            Assert.That(StateType, Is.Not.Null, "Each unit needs its own overwatch state.");
            Root = new GameObject("Overwatch state tests");
            Grid = Root.AddComponent<GridManager>();
            Player = Unit(new Vector2Int(3, 3), UnitTeam.Player);
            Ally = Unit(new Vector2Int(1, 1), UnitTeam.Player);
            Enemy = Unit(new Vector2Int(3, 5), UnitTeam.Enemy);
            Turns = Root.AddComponent<TurnManager>();
            Combat = Root.AddComponent<CombatSystem>();
            var setup = Root.AddComponent<BattleSceneController>();
            setup.Configure(Grid, Root.AddComponent<CoverSystem>(), new[] { Player, Ally, Enemy }, null, null);
            setup.ConfigureCombat(Combat, Turns);
            Assert.That(setup.InitializeBattle(), Is.True);
            System = Root.GetComponent(systemType);
            Assert.That(System, Is.Not.Null, "Existing scene bootstrap must wire overwatch at runtime.");
            Assert.That(State, Is.Not.Null);
        }

        private BattleUnit Unit(Vector2Int position, UnitTeam team)
        {
            var unit = new GameObject(team.ToString()).AddTestBattleUnit(); unit.transform.SetParent(Root.transform);
            unit.Configure(position, 5); unit.ConfigureTeam(team); return unit;
        }
        public bool CanBegin(WeaponSlot slot = WeaponSlot.RightHand) =>
            (bool)System.GetType().GetMethod("CanBegin").Invoke(System, new object[] { Player, slot });
        public bool Begin(FacingDirection facing = FacingDirection.North, WeaponSlot slot = WeaponSlot.RightHand) =>
            (bool)System.GetType().GetMethod("TryBegin").Invoke(System, new object[] { Player, slot, facing });
        public List<GridCell> Coverage(FacingDirection facing = FacingDirection.North) =>
            (List<GridCell>)System.GetType().GetMethod("GetCoverageCells").Invoke(System,
                new object[] { Player, WeaponSlot.RightHand, facing });
        public void StartTeam(UnitTeam team) => typeof(TurnManager).GetMethod("StartTeamTurn", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(Turns, new object[] { team });
        public void Building(Vector2Int position)
        {
            var building = GameObject.CreatePrimitive(PrimitiveType.Cube); building.transform.SetParent(Root.transform);
            building.transform.position = Grid.GetWorldPosition(position) + Vector3.up;
            building.transform.localScale = new Vector3(1.8f, 4, 1.8f);
            Grid.RegisterObstacle(building.AddComponent<GridObstacle>().Collider);
        }
        public void Dispose() { if (Root) Object.DestroyImmediate(Root); }
    }

    public class OverwatchStateTests
    {
        private OverwatchTestFixture battle;
        [SetUp] public void Setup() => battle = new OverwatchTestFixture();
        [TearDown] public void Cleanup() => battle?.Dispose();

        [TestCase(5, true, 3)] [TestCase(2, true, 0)] [TestCase(1, false, 1)]
        public void EntryCostsExactlyTwoAP(int available, bool success, int remaining)
        {
            battle.Player.ActionPoints.TrySpend(5 - available);
            Assert.That(battle.Begin(), Is.EqualTo(success));
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(remaining));
            Assert.That(battle.Active, Is.EqualTo(success));
            Assert.That(battle.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20), "Entry must not fire.");
        }
        [Test]
        public void WeaponAttackCostFiveDoesNotPreventEntryWithTwoAP()
        {
            battle.Player.Combat.ConfigureTestRanged(5, 80, 7, 5);
            battle.Player.ActionPoints.TrySpend(3);
            Assert.That(battle.CanBegin(), Is.True); Assert.That(battle.Begin(), Is.True);
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.Zero);
        }
        [Test]
        public void MeleeInvalidDirectionAndRepeatedEntryDoNotSpendAP()
        {
            Assert.That(battle.Begin(slot: WeaponSlot.LeftHand), Is.False);
            Assert.That(battle.Begin((FacingDirection)99), Is.False);
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(battle.Begin(), Is.True); Assert.That(battle.Begin(), Is.False);
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(3));
        }
        [Test]
        public void GuardBlocksNormalActionsButStillAllowsEmergencyEjection()
        {
            Assert.That(battle.Combat.GetPreview(battle.Player, battle.Enemy).CanAttack, Is.True);
            Assert.That(battle.Begin(), Is.True);
            Assert.That(battle.Player.CanTakeAction, Is.False); Assert.That(battle.Turns.CanAct(battle.Player), Is.False);
            Assert.That(battle.Player.Movement.TryMove(new Vector2Int(4, 3)), Is.False);
            Assert.That(battle.Combat.TryAttack(battle.Player, battle.Enemy).WasExecuted, Is.False);
            Assert.That(battle.Player.CanEject, Is.True); Assert.That(battle.Player.TryEject(), Is.True);
            Assert.That(battle.Active, Is.False); Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [Test]
        public void WeaponReplacementInvalidatesGuardWithoutRefundEvenIfOriginalIsRestored()
        {
            Assert.That(battle.Begin(), Is.True);
            var loadout = battle.Player.GetComponent<UnitWeaponLoadout>();
            var original = loadout.RightWeapon;
            loadout.Configure(loadout.LeftWeapon, loadout.LeftGrip, null, loadout.RightGrip);
            loadout.Configure(loadout.LeftWeapon, loadout.LeftGrip, original, GripMode.OneHand);
            Assert.That(battle.Active, Is.False); Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(3));
        }
        [Test]
        public void GuardRetainsChosenWeaponAndFacing()
        {
            Assert.That(battle.Begin(FacingDirection.East), Is.True);
            Assert.That(battle.Player.Facing, Is.EqualTo(FacingDirection.East));
            Assert.That(battle.StateType.GetProperty("Facing").GetValue(battle.State), Is.EqualTo(FacingDirection.East));
            Assert.That(battle.StateType.GetProperty("Weapon").GetValue(battle.State),
                Is.SameAs(battle.Player.GetComponent<UnitWeaponLoadout>().RightWeapon));
        }
        [Test]
        public void ReplacingLoadoutComponentUsesCurrentEquipment()
        {
            var old = battle.Player.GetComponent<UnitWeaponLoadout>();
            var weapon = old.RightWeapon;
            Object.DestroyImmediate(old);
            var replacement = battle.Player.gameObject.AddComponent<UnitWeaponLoadout>();
            replacement.Configure(null, GripMode.OneHand, weapon, GripMode.OneHand);
            Assert.That(battle.Begin(), Is.True);
            Assert.That(battle.Active, Is.True, "Arming must bind the current loadout, not a destroyed component.");
            Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(3));
            replacement.Configure(null, GripMode.OneHand, null, GripMode.OneHand);
            Assert.That(battle.Active, Is.False, "The new loadout must also invalidate an armed weapon immediately.");
        }
        [Test]
        public void GuardPersistsThroughEnemyTurnThenExpiresOnOwnTurn()
        {
            Assert.That(battle.Begin(), Is.True); battle.StartTeam(UnitTeam.Enemy);
            Assert.That(battle.Active, Is.True); Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(3));
            battle.StartTeam(UnitTeam.Player);
            Assert.That(battle.Active, Is.False); Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [Test]
        public void GuardCanBeConsumedOnlyOnceWithoutFurtherAPCost()
        {
            Assert.That(battle.Begin(), Is.True);
            var consume = battle.StateType.GetMethod("TryConsume", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(consume.Invoke(battle.State, null), Is.True); Assert.That(consume.Invoke(battle.State, null), Is.False);
            Assert.That(battle.Active, Is.False); Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(3));
        }
        [Test]
        public void CriticalStateCancelsGuardWithoutRefund()
        {
            Assert.That(battle.Begin(), Is.True); battle.Player.Health.SetDestructionRollSource(() => 99);
            battle.Player.Health.ApplyDamage(TargetBodyPart.Head, 6);
            battle.Player.Health.ApplyDamage(TargetBodyPart.LeftArm, 8);
            battle.Player.Health.ApplyDamage(TargetBodyPart.RightArm, 8);
            Assert.That(battle.Player.Health.State, Is.EqualTo(MechState.Critical));
            Assert.That(battle.Active, Is.False); Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(3));
        }
        [TestCase("unit")] [TestCase("state")] [TestCase("combat")] [TestCase("system")] [TestCase("turns")]
        public void DisablingBattleComponentsCancelsGuard(string component)
        {
            Assert.That(battle.Begin(), Is.True);
            switch (component)
            {
                case "unit": battle.Player.enabled = false; break;
                case "state": ((Behaviour)battle.State).enabled = false; break;
                case "combat": battle.Player.Combat.enabled = false; break;
                case "system": ((Behaviour)battle.System).enabled = false; break;
                case "turns": battle.Turns.enabled = false; break;
            }
            Assert.That(battle.Active, Is.False); Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(3));
        }
        [Test]
        public void BattleEndCancelsAllGuards()
        {
            Assert.That(battle.Begin(), Is.True); battle.Enemy.Health.ApplyDamage(TargetBodyPart.Torso, 20);
            Assert.That(battle.Turns.IsBattleOver, Is.True); Assert.That(battle.Active, Is.False);
        }
        [Test]
        public void OpponentTurnAndUnregisteredUnitsCannotBegin()
        {
            battle.StartTeam(UnitTeam.Enemy); Assert.That(battle.Begin(), Is.False);
            battle.StartTeam(UnitTeam.Player); battle.Grid.ReleaseUnit(battle.Player);
            Assert.That(battle.Begin(), Is.False); Assert.That(battle.Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
    }
}
