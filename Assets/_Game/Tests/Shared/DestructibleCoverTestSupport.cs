using System;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public static class DestructibleCoverTestSupport
    {
        public static CoverObject Add(BackpackTestBattle b, Vector2Int position, int hp = 1000, bool building = false)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = "Destructible cover";
            obj.transform.SetParent(b.Root.transform); obj.transform.position = b.Grid.GetWorldPosition(position);
            var cover = obj.AddComponent<CoverObject>(); cover.Configure(CoverType.Full);
            if (building) obj.AddComponent<GridObstacle>();
            var type = typeof(CoverObject).Assembly.GetType("MechaXCOM.CoverDurability"); Assert.That(type, Is.Not.Null);
            var health = obj.AddComponent(type); JsonUtility.FromJsonOverwrite("{\"maxHP\":" + hp + "}", health);
            return cover;
        }
        public static void Build(BackpackTestBattle b) => b.Cover.Build(b.Grid, b.Root.GetComponentsInChildren<CoverObject>(true));
        public static Component Health(CoverObject cover) => cover.GetComponent(typeof(CoverObject).Assembly.GetType("MechaXCOM.CoverDurability"));
        public static int HP(CoverObject cover) => (int)Health(cover).GetType().GetProperty("CurrentHP").GetValue(Health(cover));
        public static object Preview(BackpackTestBattle b, CoverObject cover, WeaponSlot slot = WeaponSlot.RightHand) =>
            IndirectTestSupport.Call(b.Combat, "GetCoverAttackPreview", b.Player, cover, slot);
        public static object Fire(BackpackTestBattle b, CoverObject cover, WeaponSlot slot = WeaponSlot.RightHand) =>
            IndirectTestSupport.Call(b.Combat, "TryAttackCover", b.Player, cover, slot);
    }
}
