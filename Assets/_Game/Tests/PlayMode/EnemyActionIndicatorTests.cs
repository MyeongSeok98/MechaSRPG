using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class EnemyActionIndicatorTests
    {
        private GameObject root;
        private GridVisualizer visualizer;
        private TurnManager turns;
        private EnemyAIController ai;
        private BattleUnit player;
        private BattleUnit[] enemies;

        private void Create(int playerX, params Vector2Int[] enemyPositions)
        {
            root = new GameObject("Enemy indicator test");
            var grid = root.AddComponent<GridManager>();
            grid.Configure(16, 16, 2);
            var cover = root.AddComponent<CoverSystem>();
            turns = root.AddComponent<TurnManager>();
            var combat = root.AddComponent<CombatSystem>();
            combat.SetRollSource(() => 0);
            ai = root.AddComponent<EnemyAIController>();
            visualizer = root.AddComponent<GridVisualizer>();
            player = MakeUnit(new Vector2Int(playerX, 1), UnitTeam.Player);
            enemies = new BattleUnit[enemyPositions.Length];
            var units = new BattleUnit[enemies.Length + 1];
            units[0] = player;
            for (int i = 0; i < enemies.Length; i++)
                units[i + 1] = enemies[i] = MakeUnit(enemyPositions[i], UnitTeam.Enemy);
            var controller = root.AddComponent<BattleSceneController>();
            controller.Configure(grid, cover, units, null, null);
            controller.ConfigureCombat(combat, turns);
            controller.ConfigureEnemyAI(ai);
            controller.ConfigurePresentation(null, null, visualizer, null);
            Assert.That(controller.InitializeBattle(), Is.True);
            // This fixture checks action presentation, with distant actors already detected.
            foreach (var unit in units) unit.Vision.Configure(20);
            visualizer.SetSelectedUnit(player);
        }

        private BattleUnit MakeUnit(Vector2Int cell, UnitTeam team)
        {
            var unit = new GameObject(team.ToString()).AddTestBattleUnit();
            unit.transform.SetParent(root.transform);
            unit.Configure(cell, 5);
            unit.ConfigureTeam(team);
            unit.GetComponent<UnitCombat>().ConfigureTestRanged(5, 80, 5, 3);
            unit.GetComponent<UnitCombat>().ConfigureTestMelee(8, 95, 0, 3);
            return unit;
        }

        private Transform Indicator()
        {
            var marker = root.transform.Find("Enemy Action Indicator");
            Assert.That(marker, Is.Not.Null, "An acting enemy needs a separate floor outline.");
            return marker;
        }

        private IEnumerator WaitForMarkerAt(BattleUnit unit)
        {
            float deadline = Time.realtimeSinceStartup + 5;
            var marker = Indicator();
            while ((!marker.gameObject.activeSelf ||
                    Vector2.Distance(new Vector2(marker.position.x, marker.position.z),
                        new Vector2(unit.transform.position.x, unit.transform.position.z)) > 0.01f) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(marker.gameObject.activeSelf, Is.True);
            Assert.That(marker.position.x, Is.EqualTo(unit.transform.position.x).Within(0.01f));
            Assert.That(marker.position.z, Is.EqualTo(unit.transform.position.z).Within(0.01f));
        }

        [UnityTearDown]
        public IEnumerator Cleanup() { if (root) Object.Destroy(root); yield return null; }

        [UnityTest]
        public IEnumerator MovingEnemyHasRedFloorOutlineFollowingItsVisualPosition()
        {
            Create(12, new Vector2Int(1, 1));
            var marker = Indicator();
            Assert.That(marker.gameObject.activeSelf, Is.False);
            turns.TryEndTurn();
            yield return WaitForMarkerAt(enemies[0]);
            var before = marker.position;
            yield return new WaitForSeconds(0.1f);
            yield return null;
            Assert.That(enemies[0].Movement.IsMoving, Is.True);
            Assert.That(marker.position, Is.Not.EqualTo(before));
            Assert.That(marker.position.x, Is.EqualTo(enemies[0].transform.position.x).Within(0.3f));
            Assert.That(marker.position.z, Is.EqualTo(enemies[0].transform.position.z).Within(0.3f));
            Assert.That(marker.position.y, Is.EqualTo(0.085f).Within(0.001f));
            foreach (var renderer in marker.GetComponentsInChildren<Renderer>())
            {
                var color = renderer.sharedMaterial.color;
                Assert.That(color.r, Is.GreaterThan(color.g * 2));
                Assert.That(color.r, Is.GreaterThan(color.b * 2));
            }
            var friendly = root.transform.Find("Selection Indicator");
            Assert.That(friendly.gameObject.activeSelf, Is.True);
            Assert.That(friendly.position.x, Is.EqualTo(player.transform.position.x));
        }

        [UnityTest]
        public IEnumerator StationaryAttacksShowEachEnemyThenClearOnPlayerTurn()
        {
            Create(3, new Vector2Int(1, 1), new Vector2Int(1, 2));
            turns.TryEndTurn();
            yield return WaitForMarkerAt(enemies[0]);
            Assert.That(player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(turns.ActiveEnemy, Is.EqualTo(enemies[0]));
            yield return WaitForMarkerAt(enemies[1]);
            Assert.That(player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(10));
            Assert.That(turns.ActiveEnemy, Is.EqualTo(enemies[1]));
            float deadline = Time.realtimeSinceStartup + 5;
            while (!turns.IsPlayerTurn && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.That(turns.IsPlayerTurn, Is.True);
            Assert.That(Indicator().gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator DisabledAIClearsActingEnemyOutline()
        {
            Create(12, new Vector2Int(1, 1));
            turns.TryEndTurn();
            yield return WaitForMarkerAt(enemies[0]);
            ai.enabled = false;
            yield return null;
            yield return null;
            Assert.That(Indicator().gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator EnemyDeathAndBattleEndClearActingEnemyOutline()
        {
            Create(12, new Vector2Int(1, 1));
            turns.TryEndTurn();
            yield return WaitForMarkerAt(enemies[0]);
            enemies[0].Health.ApplyDamage(TargetBodyPart.Torso, 20);
            yield return null;
            yield return null;
            Assert.That(turns.IsBattleOver, Is.True);
            Assert.That(Indicator().gameObject.activeSelf, Is.False);
        }
    }
}
