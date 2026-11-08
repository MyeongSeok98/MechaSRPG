using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class ExplosiveCoverTests
    {
        private BackpackTestBattle b;
        [SetUp] public void Setup()
        { b=new BackpackTestBattle(); ExplosiveTestSupport.Set(b.Weapon,"rangedCategory",RangedWeaponCategory.Explosive); }
        [TearDown] public void Cleanup()=>b.Dispose();
        private bool Executed(object value)=>IndirectTestSupport.Field<bool>(value,"WasExecuted");
        [Test] public void DirectCoverHitIsGuaranteedAndPaysOneShotWithoutCriticalOrHitRoll()
        {
            var cover=DestructibleCoverTestSupport.Add(b,new Vector2Int(4,1),100);DestructibleCoverTestSupport.Build(b);
            b.Combat.SetRollSource(()=>{Assert.Fail("Static target has no hit roll");return 0;});
            b.Combat.SetCriticalRollSource(()=>{Assert.Fail("Cover has no critical");return 0;});
            var preview=DestructibleCoverTestSupport.Preview(b,cover);Assert.That(IndirectTestSupport.Field<bool>(preview,"CanAttack"),Is.True);
            Assert.That(Executed(DestructibleCoverTestSupport.Fire(b,cover)),Is.True);
            Assert.That(DestructibleCoverTestSupport.HP(cover),Is.EqualTo(93));
            Assert.That(b.Player.ActionPoints.CurrentPoints,Is.EqualTo(2));Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded,Is.EqualTo(5));
            Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso),Is.EqualTo(20));
        }
        [Test] public void CoverDamageUsesItsOwnMultiplierAndRoundsOnce()
        {
            var cover=DestructibleCoverTestSupport.Add(b,new Vector2Int(4,1),100);DestructibleCoverTestSupport.Build(b);
            JsonUtility.FromJsonOverwrite("{\"minDamage\":5,\"maxDamage\":5,\"coverDamageMultiplier\":0.7}",b.Weapon);
            Assert.That(Executed(DestructibleCoverTestSupport.Fire(b,cover)),Is.True);Assert.That(DestructibleCoverTestSupport.HP(cover),Is.EqualTo(96));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void WrongWeaponModesOrZeroCoverDamageCannotSpendResources(int mode)
        {
            var cover=DestructibleCoverTestSupport.Add(b,new Vector2Int(4,1));DestructibleCoverTestSupport.Build(b);
            if(mode==0)ExplosiveTestSupport.Set(b.Weapon,"rangedCategory",RangedWeaponCategory.Beam);
            if(mode==1)ExplosiveTestSupport.Set(b.Weapon,"rangedAttackMode",RangedAttackMode.Indirect);
            if(mode==2)JsonUtility.FromJsonOverwrite("{\"coverDamageMultiplier\":0}",b.Weapon);
            Assert.That(Executed(DestructibleCoverTestSupport.Fire(b,cover)),Is.False);
            Assert.That(b.Player.ActionPoints.CurrentPoints,Is.EqualTo(5));Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded,Is.EqualTo(6));
        }
        [Test] public void DirectRejectsOutOfRangeAndUnregisteredCovers()
        {
            var far=DestructibleCoverTestSupport.Add(b,new Vector2Int(10,10));var local=DestructibleCoverTestSupport.Add(b,new Vector2Int(4,1));
            b.Cover.Build(b.Grid,new[]{far});
            Assert.That(Executed(DestructibleCoverTestSupport.Fire(b,far)),Is.False);
            Assert.That(Executed(DestructibleCoverTestSupport.Fire(b,local)),Is.False);
        }
        private void Wall(Vector2Int cell)
        {
            var obj=new GameObject("Indestructible building");obj.transform.SetParent(b.Root.transform);obj.transform.position=b.Grid.GetWorldPosition(cell);
            var wall=obj.AddComponent<GridObstacle>();wall.Collider.size=Vector3.one;b.Grid.RegisterObstacle(wall.Collider);
        }
        [Test] public void DirectCanHitOwnBuildingFaceButCannotBypassAnOverlappingBuilding()
        {
            var cover=DestructibleCoverTestSupport.Add(b,new Vector2Int(4,1),100,true);DestructibleCoverTestSupport.Build(b);
            Assert.That(IndirectTestSupport.Field<bool>(DestructibleCoverTestSupport.Preview(b,cover),"CanAttack"),Is.True);
            Assert.That(b.Combat.GetWeaponRangeCells(b.Player,WeaponSlot.RightHand).Any(c=>c.Position==new Vector2Int(4,1)),Is.True);
            Wall(new Vector2Int(4,1));
            Assert.That(Executed(DestructibleCoverTestSupport.Fire(b,cover)),Is.False);
        }
        [Test] public void FireRechecksBuildingAfterPreview()
        {
            var cover=DestructibleCoverTestSupport.Add(b,new Vector2Int(4,1));DestructibleCoverTestSupport.Build(b);
            Assert.That(IndirectTestSupport.Field<bool>(DestructibleCoverTestSupport.Preview(b,cover),"CanAttack"),Is.True);
            Wall(new Vector2Int(2,1));Assert.That(Executed(DestructibleCoverTestSupport.Fire(b,cover)),Is.False);
            Assert.That(b.Player.ActionPoints.CurrentPoints,Is.EqualTo(5));
        }
        [Test] public void IndirectDamagesEachCoverOnceRegardlessOfUnitPartCountOrFootprint()
        {
            IndirectTestSupport.Configure(b,3,2,2);
            var large=DestructibleCoverTestSupport.Add(b,new Vector2Int(3,3));large.GetComponent<BoxCollider>().size=new Vector3(4,1,2);
            var other=DestructibleCoverTestSupport.Add(b,new Vector2Int(4,2));DestructibleCoverTestSupport.Build(b);
            int rolls=0;b.Combat.SetDamageRollSource((min,max)=>{rolls++;return min;});b.Combat.SetIndirectPartIndexRollSource(count=>0);
            Assert.That(Executed(IndirectTestSupport.Fire(b,new Vector2Int(3,2))),Is.True);
            Assert.That(DestructibleCoverTestSupport.HP(large),Is.EqualTo(900));Assert.That(DestructibleCoverTestSupport.HP(other),Is.EqualTo(900));
            Assert.That(rolls,Is.EqualTo(6),"Two units x2 parts and two unique cover rolls.");
        }
        [Test] public void DestroyingBlastWallDoesNotHitNewlyExposedEnemyInSameShot()
        {
            IndirectTestSupport.Configure(b,2);Assert.That(b.Grid.TryMoveUnit(b.Enemy,new Vector2Int(4,1)),Is.True);
            var wall=DestructibleCoverTestSupport.Add(b,new Vector2Int(3,1),1,true);DestructibleCoverTestSupport.Build(b);
            b.Turns.Visibility.Refresh();Assert.That(b.Turns.Visibility.IsVisibleTo(UnitTeam.Player,b.Enemy),Is.False);
            Assert.That(Executed(IndirectTestSupport.Fire(b,new Vector2Int(2,1))),Is.True);
            Assert.That(DestructibleCoverTestSupport.HP(wall),Is.Zero);Assert.That(b.Enemy.Health.GetCurrentHP(TargetBodyPart.Torso),Is.EqualTo(1000));
            Assert.That(b.Grid.HasClearBlast(new Vector2Int(2,1),new Vector2Int(4,1)),Is.True);
        }
        [Test] public void IndirectBuildingBlocksCoverBehindIt()
        {
            IndirectTestSupport.Configure(b,3);var cover=DestructibleCoverTestSupport.Add(b,new Vector2Int(5,1));DestructibleCoverTestSupport.Build(b);Wall(new Vector2Int(4,1));
            Assert.That(Executed(IndirectTestSupport.Fire(b,new Vector2Int(3,1))),Is.True);Assert.That(DestructibleCoverTestSupport.HP(cover),Is.EqualTo(1000));
        }
        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidCoverDamageMultiplierIsRejected(float value)
        {
            var field=typeof(WeaponData).GetField("coverDamageMultiplier",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Assert.That(field,Is.Not.Null);field.SetValue(b.Weapon,value);Assert.That(b.Weapon.IsValid(out _),Is.False);
        }
        [TestCase(1,2147483520f,int.MaxValue)]
        [TestCase(int.MaxValue,.5f,1073741824)]
        [TestCase(1,float.MaxValue,int.MaxValue)]
        public void HugeCoverMultiplierRoundsAndSaturatesWithoutOverflow(int raw,float multiplier,int expected)
            => Assert.That(CombatMath.CalculateCoverDamage(raw,multiplier),Is.EqualTo(expected));
    }
}
