using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class MechaDetectionRangePlayTests
    {
        private GameObject root;
        private GridManager grid;
        private BattleSceneController controller;
        private BattleUnit player, enemy;
        private TurnManager turns;
        private readonly List<MechaData> data = new List<MechaData>();
        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private MechaData Mecha(int range)
        {
            var mecha = ScriptableObject.CreateInstance<MechaData>(); data.Add(mecha);
            Set(mecha, "mechaId", "play_sensor"); Set(mecha, "detectionRange", range);
            Set(mecha, "bodyMaxHP", 100); return mecha;
        }
        private BattleUnit Unit(int x, UnitTeam team, int range)
        {
            var unit = new GameObject(team.ToString()).AddTestBattleUnit(); unit.transform.SetParent(root.transform);
            unit.Configure(new Vector2Int(x, 2), 5); unit.ConfigureTeam(team); Set(unit, "mechaData", Mecha(range));
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube); model.name = "Test visual"; model.transform.SetParent(unit.transform);
            return unit;
        }
        private void Create(int playerRange, int enemyRange)
        {
            root = new GameObject("Mecha detection play tests"); grid = root.AddComponent<GridManager>(); grid.Configure(24, 6, 2);
            player = Unit(2, UnitTeam.Player, playerRange); enemy = Unit(8, UnitTeam.Enemy, enemyRange);
            turns = root.AddComponent<TurnManager>(); Set(turns, "enemyActionDelay", 0f);
            var combat = root.AddTestCombatSystem(); combat.SetRollSource(() => 0); combat.SetDamageRollSource((min, max) => min);
            controller = root.AddComponent<BattleSceneController>();
            controller.Configure(grid, root.AddComponent<CoverSystem>(), new[] { player, enemy }, null, null);
            controller.ConfigureCombat(combat, turns);
            Assert.That(controller.InitializeBattle(), Is.True);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (root) Object.Destroy(root); yield return null;
            foreach (var mecha in data) if (mecha) Object.Destroy(mecha); data.Clear(); yield return null;
        }
        [TestCase(4, false)] [TestCase(8, true)] public void SceneStartsWithMechaDetectionAndFog(int range, bool detected)
        {
            Create(range, 4);
            var visibility = turns.Visibility; Assert.That(player.Vision.DetectionRange, Is.EqualTo(range));
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.EqualTo(detected));
            Assert.That(enemy.GetComponentInChildren<Renderer>().enabled, Is.EqualTo(detected));
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, enemy.CurrentGridPosition), Is.EqualTo(detected ? CellVisibility.Visible : CellVisibility.Unexplored));
            Assert.That(root.GetComponent<FogOfWarPresenter>(), Is.Not.Null);
            Assert.That(grid.TryMoveUnit(player, new Vector2Int(18, 2)), Is.True);
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.False);
            Assert.That(enemy.GetComponentInChildren<Renderer>().enabled, Is.False);
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, new Vector2Int(2, 2)), Is.EqualTo(CellVisibility.Explored));
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, enemy.CurrentGridPosition), Is.EqualTo(detected ? CellVisibility.Explored : CellVisibility.Unexplored));
        }
        [UnityTest] public IEnumerator EnemyUsesShortMechaRangeToWait() { yield return EnemyPhase(4, false); }
        [UnityTest] public IEnumerator EnemyUsesLongMechaRangeToAttack() { yield return EnemyPhase(8, true); }
        private IEnumerator EnemyPhase(int enemyRange, bool attacks)
        {
            Create(10, enemyRange);
            Assert.That(enemy.Vision.DetectionRange, Is.EqualTo(enemyRange));
            int hp = player.Health.GetCurrentHP(TargetBodyPart.Torso);
            int ammo = enemy.Ammo.GetStatus(WeaponSlot.RightHand).Loaded;
            Assert.That(turns.Visibility.IsVisibleTo(UnitTeam.Enemy, player), Is.EqualTo(attacks));
            Assert.That(turns.TryEndTurn(), Is.True); yield return WaitForPlayer();
            Assert.That(enemy.CurrentGridPosition, Is.EqualTo(new Vector2Int(8, 2)));
            Assert.That(enemy.ActionPoints.CurrentPoints, Is.EqualTo(attacks ? 2 : 5));
            Assert.That(enemy.Ammo.GetStatus(WeaponSlot.RightHand).Loaded, Is.EqualTo(ammo - (attacks ? 1 : 0)));
            Assert.That(player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(hp - (attacks ? 5 : 0)));
        }
        [UnityTest] public IEnumerator TurnChangeDoesNotResetUnitVision()
        {
            Create(12, 0); Assert.That(player.Vision.DetectionRange, Is.EqualTo(12)); player.Vision.Configure(4);
            Assert.That(controller.InitializeBattle(), Is.True);
            Assert.That(turns.TryEndTurn(), Is.True); yield return WaitForPlayer();
            Assert.That(player.Vision.DetectionRange, Is.EqualTo(4)); Assert.That(player.MechaData.DetectionRange, Is.EqualTo(12));
            Assert.That(player.Initialize(grid), Is.True); Assert.That(player.Vision.DetectionRange, Is.EqualTo(4));
        }
        private IEnumerator WaitForPlayer()
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (!turns.IsPlayerTurn && !turns.IsBattleOver && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(turns.IsPlayerTurn, Is.True, "Enemy phase did not finish.");
        }
    }
}
