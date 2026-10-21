using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class WeaponAITests
    {
        private EnemyAITestBattle battle;
        private readonly List<WeaponData> weapons = new List<WeaponData>();
        private BattleUnit Actor => battle.Enemies[0];
        private BattleUnit Target => battle.Players[0];
        private int damageRolls;
        private void Create(int targetX)
        {
            damageRolls = 0;
            battle = new EnemyAITestBattle(new[] { new Vector2Int(targetX, 1) }, new[] { new Vector2Int(1, 1) });
            battle.Combat.SetDamageRollSource((min, max) => { damageRolls++; return max; });
        }
        private WeaponData Weapon(int range, int cost, int damage = 6, AttackType type = AttackType.Ranged)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponData>(); weapons.Add(weapon);
            JsonUtility.FromJsonOverwrite($"{{\"range\":{range},\"apCost\":{cost},\"minDamage\":{damage},\"maxDamage\":{damage},\"attackType\":{(int)type}}}", weapon);
            return weapon;
        }
        private UnitWeaponLoadout Equip(WeaponData left, WeaponData right, GripMode leftGrip = GripMode.OneHand, GripMode rightGrip = GripMode.OneHand)
        {
            var loadout = Actor.gameObject.EnsureTestLoadout(); loadout.Configure(left, leftGrip, right, rightGrip); return loadout;
        }
        private IEnumerator Run()
        { Assert.That(battle.Turns.TryEndTurn(), Is.True); yield return battle.WaitForPlayerTurn(); }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            battle?.Dispose(); foreach (var weapon in weapons) Object.Destroy(weapon); weapons.Clear(); yield return null;
        }
        [UnityTest] public IEnumerator AIUsesSecondHandWhenFirstCannotAttack()
        {
            Create(6); Equip(Weapon(7, 3), Weapon(2, 3, 2)); yield return Run();
            Assert.That(Target.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(14));
            Assert.That(Actor.Mobility.MovedCells, Is.Zero); Assert.That(Actor.ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(damageRolls, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator AIUsesAffordableSecondHand()
        {
            Create(4); Equip(Weapon(7, 1), Weapon(7, 6, 2)); yield return Run();
            Assert.That(Target.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(14));
            Assert.That(Actor.ActionPoints.CurrentPoints, Is.EqualTo(4)); Assert.That(Actor.Mobility.MovedCells, Is.Zero);
        }
        [UnityTest] public IEnumerator AIReservesSelectedWeaponAP()
        {
            Create(4); Equip(null, Weapon(2, 4)); yield return Run();
            Assert.That(Actor.CurrentGridPosition, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(Actor.Mobility.MovedCells, Is.EqualTo(1)); Assert.That(Actor.ActionPoints.CurrentPoints, Is.Zero);
            Assert.That(Target.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(14));
        }
        [UnityTest] public IEnumerator MovePlanUsesSecondWeaponRangeAndAP()
        {
            Create(5); Equip(Weapon(2, 2), Weapon(1, 4, 2)); yield return Run();
            Assert.That(Actor.Mobility.MovedCells, Is.EqualTo(2)); Assert.That(Actor.ActionPoints.CurrentPoints, Is.EqualTo(1));
            Assert.That(Target.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(14));
        }
        [UnityTest] public IEnumerator MeleeOnlyAIUsesMeleeReach()
        {
            Create(3); Equip(Weapon(2, 3, 8, AttackType.Melee), null);
            battle.Grid.SetWalkable(new Vector2Int(2, 1), false);
            Assert.That(battle.Grid.HasClearShot(Actor.CurrentGridPosition, Target.CurrentGridPosition), Is.True);
            Assert.That(battle.Grid.HasMeleeReach(Actor.CurrentGridPosition, Target.CurrentGridPosition, 2), Is.False);
            var path = Pathfinder.FindPathToRange(battle.Grid, Actor, Target.CurrentGridPosition, 2, AttackType.Melee);
            Assert.That(path.Count, Is.EqualTo(2));
            yield return Run();
            Assert.That(Actor.Mobility.MovedCells, Is.EqualTo(2)); Assert.That(Actor.ActionPoints.CurrentPoints, Is.Zero);
            Assert.That(Target.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(12));
        }
        [UnityTest] public IEnumerator MeleeCannotAttackAcrossImpassableRavineWall()
        {
            Create(3); Equip(Weapon(2, 3, 8, AttackType.Melee), null);
            for (int z = 0; z < battle.Grid.Height; z++) battle.Grid.SetWalkable(new Vector2Int(2, z), false);
            yield return Run();
            Assert.That(Actor.Mobility.MovedCells, Is.Zero); Assert.That(Actor.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(Target.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20)); Assert.That(damageRolls, Is.Zero);
        }
        private IEnumerator WaitWithoutWeapon(string mode)
        {
            Create(4);
            var loadout = mode == "empty" ? Equip(null, null) : Equip(null, Weapon(7, 3));
            if (mode == "disabled") loadout.enabled = false;
            if (mode == "invalid") loadout.Configure(Weapon(7, 1), GripMode.OneHand, loadout.RightWeapon, GripMode.TwoHands);
            yield return Run();
            Assert.That(Actor.ActionPoints.CurrentPoints, Is.EqualTo(5)); Assert.That(Actor.Mobility.MovedCells, Is.Zero);
            Assert.That(Target.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20)); Assert.That(damageRolls, Is.Zero);
        }
        [UnityTest] public IEnumerator UnarmedAIWaitsWithoutAP() { yield return WaitWithoutWeapon("empty"); }
        [UnityTest] public IEnumerator DisabledLoadoutAIWaitsWithoutAP() { yield return WaitWithoutWeapon("disabled"); }
        [UnityTest] public IEnumerator InvalidLoadoutAIWaitsWithoutAP() { yield return WaitWithoutWeapon("invalid"); }
        [UnityTest] public IEnumerator SameAssetTwoHandsNoDoubleAttack()
        {
            Create(4); var weapon = Weapon(7, 1); Equip(weapon, weapon); yield return Run();
            Assert.That(Target.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(14)); Assert.That(damageRolls, Is.EqualTo(1));
            Assert.That(Actor.ActionPoints.CurrentPoints, Is.EqualTo(4));
        }
        [UnityTest] public IEnumerator TwoHandWeaponAttacksOnlyOnce()
        {
            Create(4); Equip(Weapon(7, 1), null, GripMode.TwoHands); yield return Run();
            Assert.That(Target.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(14)); Assert.That(damageRolls, Is.EqualTo(1));
            Assert.That(Actor.ActionPoints.CurrentPoints, Is.EqualTo(4));
        }
        [UnityTest] public IEnumerator ImmediateMeleePriorityAndRightHandTieRemainStable()
        {
            Create(3); Equip(Weapon(2, 3, 8, AttackType.Melee), Weapon(7, 1, 2)); yield return Run();
            Assert.That(Target.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(12));
            Assert.That(Actor.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
    }
}
