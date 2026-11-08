using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class CoverDurabilityTests
    {
        private GameObject root;
        private GridManager grid;
        private CoverSystem system;
        [SetUp] public void Setup()
        { root = new GameObject("Durability tests"); grid = root.AddComponent<GridManager>(); grid.Configure(8,8,2); grid.Generate(); system = root.AddComponent<CoverSystem>(); }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);
        private CoverObject Make(CoverType type = CoverType.Full, int hp = 100)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.transform.SetParent(root.transform);
            obj.transform.position = grid.GetWorldPosition(new Vector2Int(3,4));
            var cover = obj.AddComponent<CoverObject>(); cover.Configure(type);
            var durabilityType = typeof(CoverObject).Assembly.GetType("MechaXCOM.CoverDurability");
            Assert.That(durabilityType, Is.Not.Null, "CoverDurability is required.");
            var health = obj.AddComponent(durabilityType); JsonUtility.FromJsonOverwrite("{\"maxHP\":" + hp + "}", health);
            return cover;
        }
        private Component Health(CoverObject cover) => cover.GetComponent(typeof(CoverObject).Assembly.GetType("MechaXCOM.CoverDurability"));
        private int HP(CoverObject cover) => (int)Health(cover).GetType().GetProperty("CurrentHP").GetValue(Health(cover));
        private int Damage(CoverObject cover, int amount) => (int)Health(cover).GetType().GetMethod("ApplyDamage").Invoke(Health(cover), new object[] { amount });
        private GridCell Cell(int x,int z) { grid.TryGetCell(new Vector2Int(x,z),out var cell); return cell; }
        [Test] public void HPIsPerInstanceAndNonlethalDamageKeepsCover()
        {
            var a=Make(); var b=Make(); system.Build(grid,new[]{a,b});
            Assert.That(Damage(a,40),Is.EqualTo(40)); Assert.That(HP(a),Is.EqualTo(60)); Assert.That(HP(b),Is.EqualTo(100));
            Assert.That(Cell(3,3).GetCover(CoverDirection.North),Is.EqualTo(CoverType.Full));
        }
        [Test] public void ZeroHPRemovesVisualCollisionCoverAndWalkBlockWithoutDeletingOccupants()
        {
            var cover=Make(); system.Build(grid,new[]{cover});
            Assert.That(Cell(3,4).Walkable,Is.False); Assert.That(Damage(cover,999),Is.EqualTo(100));
            Assert.That(HP(cover),Is.Zero); Assert.That(cover.gameObject.activeInHierarchy,Is.False);
            Assert.That(Cell(3,4).Walkable,Is.True); Assert.That(Cell(3,3).GetCover(CoverDirection.North),Is.EqualTo(CoverType.None));
            Assert.That(Damage(cover,10),Is.Zero);
        }
        [Test] public void OverlappingHalfCoverRemainsAfterFullIsDestroyed()
        {
            var full=Make();var half=Make(CoverType.Half);system.Build(grid,new[]{full,half});Damage(full,100);
            Assert.That(Cell(3,4).Walkable,Is.False);Assert.That(Cell(3,3).GetCover(CoverDirection.North),Is.EqualTo(CoverType.Half));
            Damage(half,100);Assert.That(Cell(3,4).Walkable,Is.True);
        }
        [Test] public void OverlappingBuildingAndManualTerrainSurviveCoverDestruction()
        {
            var cover=Make();var wall=new GameObject("Other building");wall.transform.SetParent(root.transform);wall.transform.position=cover.transform.position;
            var obstacle=wall.AddComponent<GridObstacle>();obstacle.Collider.size=Vector3.one;grid.RegisterObstacle(obstacle.Collider);
            grid.SetWalkable(new Vector2Int(3,4),false);system.Build(grid,new[]{cover});Damage(cover,100);
            Assert.That(Cell(3,4).Walkable,Is.False);Assert.That(Cell(3,4).BlocksShots,Is.True);Assert.That(Cell(3,4).BlocksVision,Is.True);
        }
        [Test] public void DuplicateRegistrationDoesNotLeaveGhostBlockAfterRemoval()
        {
            var cover=Make();system.Build(grid,new[]{cover});system.Build(grid,new[]{cover});grid.RegisterObstacle(cover.Collider);
            Damage(cover,100);Assert.That(Cell(3,4).Walkable,Is.True);
        }
        [Test] public void ManualUnwalkableCellStaysBlockedAfterLastCoverDies()
        { var cover=Make();grid.SetWalkable(new Vector2Int(3,4),false);system.Build(grid,new[]{cover});Damage(cover,100);Assert.That(Cell(3,4).Walkable,Is.False); }
        [Test] public void DisableEnableDoesNotHealAndDestroyedCoverCannotReturn()
        {
            var cover=Make();system.Build(grid,new[]{cover});Damage(cover,30);cover.gameObject.SetActive(false);cover.gameObject.SetActive(true);
            Assert.That(HP(cover),Is.EqualTo(70));Damage(cover,70);cover.gameObject.SetActive(true);system.Build(grid,new[]{cover});
            Assert.That(HP(cover),Is.Zero);Assert.That(Cell(3,3).GetCover(CoverDirection.North),Is.EqualTo(CoverType.None));Assert.That(Cell(3,4).Walkable,Is.True);
        }
        [Test] public void InvalidOrZeroDamageDoesNotHealOrChangeHP()
        { var cover=Make();Assert.That(Damage(cover,-1),Is.Zero);Assert.That(Damage(cover,0),Is.Zero);Assert.That(HP(cover),Is.EqualTo(100)); }
        [TestCase("Assets/_Game/Prefabs/HalfCover.prefab")] [TestCase("Assets/_Game/Prefabs/FullCover.prefab")]
        public void AuthoredCoverPrefabsHaveDurabilityAndIndependentInstanceHealth(string path)
        {
            var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);Assert.That(prefab,Is.Not.Null);
            Assert.That(prefab.GetComponent<CoverDurability>(),Is.Not.Null);
            var first=Object.Instantiate(prefab,root.transform).GetComponent<CoverDurability>();
            var second=Object.Instantiate(prefab,root.transform).GetComponent<CoverDurability>();
            int before=second.CurrentHP;Assert.That(before,Is.GreaterThan(0));first.ApplyDamage(1);
            Assert.That(second.CurrentHP,Is.EqualTo(before));Assert.That(first.CurrentHP,Is.EqualTo(first.MaxHP-1));
        }
        [TestCase(false)] [TestCase(true)]
        public void DisabledCoverDoesNotBlockExceptForItsIndependentGridObstacle(bool terrain)
        {
            var cover=Make(); cover.enabled=false;
            if(terrain) { cover.gameObject.AddComponent<GridObstacle>(); grid.RegisterObstacle(cover.Collider); }
            system.Build(grid,new[]{cover});
            Assert.That(Cell(3,4).Walkable,Is.EqualTo(!terrain));
            Assert.That(Cell(3,4).BlocksShots,Is.EqualTo(terrain));
            Assert.That(Cell(3,3).GetCover(CoverDirection.North),Is.EqualTo(CoverType.None));
        }
        [TestCase(false)] [TestCase(true)]
        public void DestroyingParentRemovesRegisteredChildObstacleButPreservesUnrelatedOverlap(bool overlap)
        {
            var cover=Make();var child=new GameObject("Child obstacle");child.transform.SetParent(cover.transform);
            child.transform.position=grid.GetWorldPosition(new Vector2Int(3,5));
            var obstacle=child.AddComponent<GridObstacle>();obstacle.Collider.size=Vector3.one;grid.RegisterObstacle(obstacle.Collider);
            BoxCollider other=null;
            if(overlap)
            {
                var obj=new GameObject("Unrelated obstacle");obj.transform.SetParent(root.transform);obj.transform.position=child.transform.position;
                var terrain=obj.AddComponent<GridObstacle>();terrain.Collider.size=Vector3.one;other=terrain.Collider;grid.RegisterObstacle(other);
            }
            system.Build(grid,new[]{cover});Damage(cover,100);
            Assert.That(Cell(3,5).Walkable,Is.EqualTo(!overlap));Assert.That(Cell(3,5).BlocksShots,Is.EqualTo(overlap));
            if(other)grid.UnregisterObstacle(other);
            Assert.That(Cell(3,5).Walkable,Is.True);Assert.That(Cell(3,5).BlocksShots,Is.False);Assert.That(Cell(3,5).BlocksVision,Is.False);
        }
        [TestCase(false)] [TestCase(true)]
        public void MutatingReturnedFootprintCannotCorruptCanonicalRegistration(bool clear)
        {
            var cover=Make();var returned=grid.RegisterObstacle(cover.Collider);
            if(clear)returned.Clear();else returned.Add(Cell(1,1));
            Assert.That(grid.UnregisterObstacle(cover.Collider),Is.True);
            Assert.That(Cell(3,4).Walkable,Is.True);Assert.That(Cell(1,1).Walkable,Is.True);
        }
    }
}
