using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public sealed class BackpackTestBattle : IDisposable
    {
        public GameObject Root { get; }
        public GridManager Grid { get; }
        public BattleUnit Player { get; }
        public BattleUnit Enemy { get; }
        public WeaponData Weapon { get; }
        public BackpackData Backpack { get; }
        public TurnManager Turns { get; }
        public CombatSystem Combat { get; }
        public CoverSystem Cover { get; }
        public OverwatchSystem Overwatch { get; }
        private readonly List<ScriptableObject> data = new List<ScriptableObject>();
        private T Own<T>(T value) where T : ScriptableObject { data.Add(value); return value; }

        public BackpackTestBattle(AttackType type = AttackType.Ranged, bool withBackpack = true, bool armed = true,
            ArmorType enemyArmor = ArmorType.Standard, int capacity = 6, int fireAP = 3, bool enemyHasBackpack = false)
        {
            Root = new GameObject("Backpack battle tests");
            Grid = Root.AddComponent<GridManager>(); Grid.Configure(12, 12, 2); Grid.Generate();
            Weapon = Own(ScriptableObject.CreateInstance<WeaponData>());
            JsonUtility.FromJsonOverwrite("{\"displayName\":\"Integrated test weapon\",\"attackType\":" + (int)type +
                ",\"minDamage\":7,\"maxDamage\":7,\"baseHitChance\":80,\"range\":" + (type == AttackType.Melee ? 2 : 7) +
                ",\"apCost\":" + fireAP + ",\"magazineCapacity\":" + capacity + ",\"spareMagazineCount\":2,\"reloadAPCost\":2,\"gripRequirement\":2,\"twoHandHitMultiplier\":1.5,\"distanceBands\":[{\"MaxDistance\":2,\"Multiplier\":0.8}]}", Weapon);
            Backpack = Own(ScriptableObject.CreateInstance<BackpackData>());
            JsonUtility.FromJsonOverwrite("{\"backpackId\":\"test_backpack\",\"displayName\":\"Test backpack\",\"weightTonnes\":1}", Backpack);
            if (armed) typeof(BackpackData).GetField("weapon", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(Backpack, Weapon);
            Player = Unit(new Vector2Int(1, 1), UnitTeam.Player, withBackpack ? Backpack : null, ArmorType.Standard);
            Enemy = Unit(new Vector2Int(3, 1), UnitTeam.Enemy, enemyHasBackpack ? Backpack : null, enemyArmor);
            Turns = Root.AddComponent<TurnManager>();
            Combat = Root.AddTestCombatSystem(); Cover = Root.AddComponent<CoverSystem>(); Combat.Initialize(Grid, Cover, Turns);
            Combat.SetRollSource(() => 0); Combat.SetCriticalRollSource(() => 99f); Combat.SetDamageRollSource((min, max) => min);
            var units = new[] { Player, Enemy };
            var visibility = Root.AddComponent<VisibilitySystem>(); visibility.Initialize(Grid, units); Turns.ConfigureVisibility(visibility);
            Turns.Initialize(units);
            Overwatch = Root.AddComponent<OverwatchSystem>(); Overwatch.Initialize(Grid, Combat, Turns, units);
        }
        public void StartEnemyTeam() => typeof(TurnManager).GetMethod("StartTeamTurn", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(Turns, new object[] { UnitTeam.Enemy });
        private BattleUnit Unit(Vector2Int position, UnitTeam team, BackpackData backpack, ArmorType armor)
        {
            var unit = new GameObject(team.ToString()).AddTestBattleUnit(); unit.transform.SetParent(Root.transform);
            var mech = Own(ScriptableObject.CreateInstance<MechaData>());
            JsonUtility.FromJsonOverwrite("{\"mechaId\":\"" + team + "\",\"accuracy\":10,\"evasion\":5,\"armorType\":" + (int)armor + "}", mech);
            var pilot = Own(ScriptableObject.CreateInstance<CharacterData>());
            JsonUtility.FromJsonOverwrite("{\"characterId\":\"" + team + "\",\"shooting\":" + (team == UnitTeam.Player ? 20 : 0) +
                ",\"melee\":20,\"technique\":" + (team == UnitTeam.Player ? 20 : 10) + ",\"evasion\":10}", pilot);
            typeof(BattleUnit).GetField("mechaData", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(unit, mech);
            typeof(BattleUnit).GetField("pilotData", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(unit, pilot);
            var loadout = unit.GetComponent<UnitWeaponLoadout>();
            loadout.Configure(Weapon, GripMode.OneHand, Weapon, GripMode.OneHand);
            Assert.That(loadout.TryConfigureBackpack(backpack), Is.True);
            unit.Configure(position, 5); unit.ConfigureTeam(team); Assert.That(unit.Initialize(Grid), Is.True);
            unit.Vision.Configure(20); return unit;
        }
        public void Dispose()
        {
            if (Application.isPlaying) Object.Destroy(Root); else Object.DestroyImmediate(Root);
            foreach (var item in data)
                if (item) { if (Application.isPlaying) Object.Destroy(item); else Object.DestroyImmediate(item); }
        }
    }
}
