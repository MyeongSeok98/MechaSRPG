using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class AllWeaponFacilityTests
    {
        private BackpackTestBattle b;
        private readonly List<Object> owned=new List<Object>();
        [TearDown] public void Cleanup() { b?.Dispose(); foreach(var item in owned) if(item) Object.DestroyImmediate(item); owned.Clear(); }
        private CoverObject Facility(Vector2Int cell, int hp=100)
        {
            var cover=DestructibleCoverTestSupport.Add(b,cell,hp);
            var data=ScriptableObject.CreateInstance<DestructibleObjectData>(); owned.Add(data);
            JsonUtility.FromJsonOverwrite("{\"objectId\":\"test_facility\",\"maxHP\":"+hp+"}",data);
            Assert.That(cover.GetComponent<CoverDurability>().ConfigureData(data),Is.True);
            DestructibleCoverTestSupport.Build(b); return cover;
        }
        [TestCase(RangedWeaponCategory.Beam)] [TestCase(RangedWeaponCategory.Ballistic)] [TestCase(RangedWeaponCategory.Explosive)]
        public void EveryDirectRangedWeaponDealsBaseDamageEvenWithZeroCoverMultiplier(RangedWeaponCategory category)
        {
            b=new BackpackTestBattle(); ExplosiveTestSupport.Set(b.Weapon,"rangedCategory",category); ExplosiveTestSupport.Set(b.Weapon,"coverDamageMultiplier",0f);
            var target=Facility(new Vector2Int(4,1));
            b.Combat.SetRollSource(()=>{Assert.Fail("Facilities retain guaranteed hits."); return 0;});
            var preview=b.Combat.GetCoverAttackPreview(b.Player,target,WeaponSlot.RightHand);
            Assert.That(preview.CanAttack,Is.True,preview.FailureReason); Assert.That(preview.MinDamage,Is.EqualTo(7));
            Assert.That(b.Combat.GetWeaponRangeCells(b.Player,WeaponSlot.RightHand).Any(c=>c.Position==new Vector2Int(4,1)),Is.True);
            var result=b.Combat.TryAttackCover(b.Player,target,WeaponSlot.RightHand);
            Assert.That(result.WasExecuted,Is.True); Assert.That(result.DamageDealt,Is.EqualTo(7));
            Assert.That(b.Player.ActionPoints.CurrentPoints,Is.EqualTo(2)); Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded,Is.EqualTo(5));
        }
        [TestCase(MeleeWeaponCategory.Edged)] [TestCase(MeleeWeaponCategory.Blunt)]
        public void MeleeDamagesFacilityAndUsesNoAmmo(MeleeWeaponCategory category)
        {
            b=new BackpackTestBattle(AttackType.Melee); ExplosiveTestSupport.Set(b.Weapon,"meleeCategory",category);
            ExplosiveTestSupport.Set(b.Weapon,"coverDamageMultiplier",0f); b.Player.Ammo.enabled=false;
            var target=Facility(new Vector2Int(1,3));
            Assert.That(b.Combat.GetWeaponRangeCells(b.Player,WeaponSlot.RightHand).Any(c=>c.Position==new Vector2Int(1,3)),Is.True);
            var result=b.Combat.TryAttackCover(b.Player,target,WeaponSlot.RightHand);
            Assert.That(result.WasExecuted,Is.True,result.FailureReason); Assert.That(result.DamageDealt,Is.EqualTo(7));
            Assert.That(b.Player.ActionPoints.CurrentPoints,Is.EqualTo(2));
        }
        [TestCase("gap")] [TestCase("unit")] [TestCase("wall")]
        public void MeleeCannotReachThroughBlockedIntermediateCell(string kind)
        {
            b=new BackpackTestBattle(AttackType.Melee); var target=Facility(new Vector2Int(1,3));
            Assert.That(b.Combat.GetCoverAttackPreview(b.Player,target,WeaponSlot.RightHand).CanAttack,Is.True);
            if(kind=="gap") b.Grid.SetWalkable(new Vector2Int(1,2),false);
            else if(kind=="unit") Assert.That(b.Grid.TryMoveUnit(b.Enemy,new Vector2Int(1,2)),Is.True);
            else Wall(new Vector2Int(1,2));
            Assert.That(b.Combat.TryAttackCover(b.Player,target,WeaponSlot.RightHand).WasExecuted,Is.False);
            Assert.That(b.Player.ActionPoints.CurrentPoints,Is.EqualTo(5));
        }
        [Test] public void RangedFacilityAttackRespectsBuildings()
        {
            b=new BackpackTestBattle(); var target=Facility(new Vector2Int(4,1));
            Assert.That(b.Combat.GetCoverAttackPreview(b.Player,target,WeaponSlot.RightHand).CanAttack,Is.True);
            Wall(new Vector2Int(2,1)); Assert.That(b.Combat.TryAttackCover(b.Player,target,WeaponSlot.RightHand).WasExecuted,Is.False);
            Assert.That(b.Player.ActionPoints.CurrentPoints,Is.EqualTo(5));
        }
        [Test] public void EmptyAmmoRejectsWithoutSpendingAP()
        {
            b=new BackpackTestBattle(); var target=Facility(new Vector2Int(4,1));
            Assert.That(b.Combat.GetCoverAttackPreview(b.Player,target,WeaponSlot.RightHand).CanAttack,Is.True);
            var consume=typeof(UnitWeaponAmmo).GetMethod("TryConsumeShot",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Assert.That(consume,Is.Not.Null);
            while(b.Player.Ammo.CanFire(WeaponSlot.RightHand)) Assert.That(consume.Invoke(b.Player.Ammo,new object[]{WeaponSlot.RightHand}),Is.EqualTo(true));
            Assert.That(b.Combat.TryAttackCover(b.Player,target,WeaponSlot.RightHand).WasExecuted,Is.False);
            Assert.That(b.Player.ActionPoints.CurrentPoints,Is.EqualTo(5)); Assert.That(target.GetComponent<CoverDurability>().CurrentHP,Is.EqualTo(100));
        }
        [Test] public void MeleeCannotIgnoreAnotherBuildingOverlappingTarget()
        {
            b=new BackpackTestBattle(AttackType.Melee); var target=Facility(new Vector2Int(1,2));
            Assert.That(b.Combat.GetCoverAttackPreview(b.Player,target,WeaponSlot.RightHand).CanAttack,Is.True);
            Wall(new Vector2Int(1,2)); Assert.That(b.Combat.TryAttackCover(b.Player,target,WeaponSlot.RightHand).WasExecuted,Is.False);
        }
        [TestCase(AttackType.Ranged)] [TestCase(AttackType.Melee)]
        public void OutOfRangeFacilityIsNotHighlightedOrAttackable(AttackType type)
        {
            b=new BackpackTestBattle(type); var cell=type==AttackType.Melee?new Vector2Int(1,4):new Vector2Int(10,1);
            var target=Facility(cell); Assert.That(b.Combat.GetWeaponRangeCells(b.Player,WeaponSlot.RightHand).Any(c=>c.Position==cell),Is.False);
            Assert.That(b.Combat.TryAttackCover(b.Player,target,WeaponSlot.RightHand).WasExecuted,Is.False);
            Assert.That(b.Player.ActionPoints.CurrentPoints,Is.EqualTo(5));
        }
        [Test] public void IndirectStillDamagesFacilityWhenCoverMultiplierIsZero()
        {
            b=new BackpackTestBattle(); IndirectTestSupport.Configure(b,2); ExplosiveTestSupport.Set(b.Weapon,"coverDamageMultiplier",0f);
            var target=Facility(new Vector2Int(3,2));
            var result=b.Combat.TryIndirectAttack(b.Player,new Vector2Int(3,2),WeaponSlot.RightHand);
            Assert.That(result.WasExecuted,Is.True,result.FailureReason);
            Assert.That(result.Covers.Any(hit=>hit.Target==target&&hit.DamageDealt>0),Is.True);
        }
        private void Wall(Vector2Int cell)
        {
            var obj=new GameObject("Other building"); obj.transform.SetParent(b.Root.transform); obj.transform.position=b.Grid.GetWorldPosition(cell);
            var wall=obj.AddComponent<GridObstacle>(); wall.Collider.size=Vector3.one; b.Grid.RegisterObstacle(wall.Collider);
        }
    }
}
