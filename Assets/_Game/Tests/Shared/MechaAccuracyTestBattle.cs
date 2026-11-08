using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public sealed class MechaAccuracyTestBattle : IDisposable
    {
        public GameObject Root { get; }
        public GridManager Grid { get; }
        public BattleUnit Player { get; }
        public BattleUnit Enemy { get; }
        public MechaData PlayerData { get; }
        public MechaData EnemyData { get; }
        public CharacterData PlayerPilot { get; }
        public CharacterData EnemyPilot { get; }
        public TurnManager Turns { get; }
        public CombatSystem Combat { get; }
        public OverwatchSystem Overwatch { get; }
        public MechaAccuracyTestBattle(int playerAccuracy = 0, int playerEvasion = 0,
            int enemyAccuracy = 0, int enemyEvasion = 0, bool withAI = false,
            int? playerTechnique = null, int? enemyTechnique = null,
            ArmorType playerArmor = ArmorType.Standard, ArmorType enemyArmor = ArmorType.Standard,
            int playerShooting = 0, int playerMelee = 0, int playerPilotEvasion = 0,
            int enemyShooting = 0, int enemyMelee = 0, int enemyPilotEvasion = 0,
            Action<MechaData, MechaData> configureMechaData = null)
        {
            Root = new GameObject("Mecha accuracy tests");
            Grid = Root.AddComponent<GridManager>(); Grid.Configure(12, 12, 2); Grid.Generate();
            PlayerData = Data("p", playerAccuracy, playerEvasion, playerArmor); EnemyData = Data("e", enemyAccuracy, enemyEvasion, enemyArmor);
            configureMechaData?.Invoke(PlayerData, EnemyData);
            PlayerPilot = Pilot("p", playerTechnique, playerShooting, playerMelee, playerPilotEvasion);
            EnemyPilot = Pilot("e", enemyTechnique, enemyShooting, enemyMelee, enemyPilotEvasion);
            Player = Unit(new Vector2Int(1, 1), UnitTeam.Player, PlayerData, PlayerPilot);
            Enemy = Unit(new Vector2Int(3, 1), UnitTeam.Enemy, EnemyData, EnemyPilot);
            Turns = Root.AddComponent<TurnManager>();
            Combat = Root.AddTestCombatSystem(); Combat.Initialize(Grid, Root.AddComponent<CoverSystem>(), Turns);
            Combat.SetRollSource(() => 0); Combat.SetDamageRollSource((min, max) => min);
            if (withAI)
            {
                Enemy.Combat.ConfigureTestMelee(8, 95, 0, 3);
                var ai = Root.AddComponent<EnemyAIController>(); ai.Initialize(Grid, Combat, Turns); Turns.ConfigureEnemyAI(ai);
            }
            var units = new[] { Player, Enemy };
            var vision = Root.AddComponent<VisibilitySystem>(); vision.Initialize(Grid, units); Turns.ConfigureVisibility(vision);
            Turns.Initialize(units);
            typeof(TurnManager).GetField("enemyActionDelay", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Turns, 0f);
            Overwatch = Root.AddComponent<OverwatchSystem>(); Overwatch.Initialize(Grid, Combat, Turns, units);
        }
        private static MechaData Data(string id, int accuracy, int evasion, ArmorType armor)
        {
            var data = ScriptableObject.CreateInstance<MechaData>();
            JsonUtility.FromJsonOverwrite($"{{\"mechaId\":\"{id}\",\"accuracy\":{accuracy},\"evasion\":{evasion},\"armorType\":{(int)armor}}}", data); return data;
        }
        private static CharacterData Pilot(string id, int? technique, int shooting, int melee, int evasion)
        {
            if (!technique.HasValue && shooting == 0 && melee == 0 && evasion == 0) return null;
            var data = ScriptableObject.CreateInstance<CharacterData>();
            JsonUtility.FromJsonOverwrite($"{{\"characterId\":\"{id}\",\"technique\":{technique ?? 0},\"shooting\":{shooting},\"melee\":{melee},\"evasion\":{evasion}}}", data);
            return data;
        }
        public BattleUnit Unit(Vector2Int position, UnitTeam team, MechaData data, CharacterData pilot = null)
        {
            var unit = new GameObject(team.ToString()).AddTestBattleUnit(); unit.transform.SetParent(Root.transform);
            typeof(BattleUnit).GetField("mechaData", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(unit, data);
            if (pilot)
            {
                var field = typeof(BattleUnit).GetField("pilotData", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, "BattleUnit must accept pilot data."); field.SetValue(unit, pilot);
            }
            unit.Configure(position, 5); unit.ConfigureTeam(team); Assert.That(unit.Initialize(Grid), Is.True);
            unit.Vision.Configure(20); return unit;
        }
        public void StartEnemyTeam() => typeof(TurnManager).GetMethod("StartTeamTurn", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(Turns, new object[] { UnitTeam.Enemy });
        public void Dispose()
        {
            if (Application.isPlaying) { Object.Destroy(Root); Object.Destroy(PlayerData); Object.Destroy(EnemyData); }
            else { Object.DestroyImmediate(Root); Object.DestroyImmediate(PlayerData); Object.DestroyImmediate(EnemyData); }
            if (Application.isPlaying) { Object.Destroy(PlayerPilot); Object.Destroy(EnemyPilot); }
            else { Object.DestroyImmediate(PlayerPilot); Object.DestroyImmediate(EnemyPilot); }
        }
    }
}
