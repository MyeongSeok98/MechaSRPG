#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class MechaSpecTurnTests
    {
        [UnityTest] public IEnumerator AuthoredPrefabUsesCustomSpecsAndRestoresConfiguredAPNextTurn()
        {
            var root = new GameObject("Data-backed battle");
            var source = AssetDatabase.LoadAssetAtPath<MechaData>("Assets/_Game/Data/Mecha/SampleMecha.asset");
            string before = EditorJsonUtility.ToJson(source);
            var data = Object.Instantiate(source);
            JsonUtility.FromJsonOverwrite("{\"baseAP\":7,\"bodyMaxHP\":35}", data);
            try
            {
                var grid = root.AddComponent<GridManager>(); grid.Generate();
                var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/BattleUnit.prefab"), root.transform).GetComponent<BattleUnit>();
                var enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/DummyEnemy.prefab"), root.transform).GetComponent<BattleUnit>();
                player.Configure(new Vector2Int(1, 1), 5); enemy.Configure(new Vector2Int(4, 1), 5);
                var settings = new SerializedObject(player);
                settings.FindProperty("mechaData").objectReferenceValue = data; settings.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(player.Initialize(grid), Is.True); Assert.That(enemy.Initialize(grid), Is.True);
                var turns = root.AddComponent<TurnManager>();
                var combat = root.AddComponent<CombatSystem>(); combat.Initialize(grid, root.AddComponent<CoverSystem>(), turns);
                var ai = root.AddComponent<EnemyAIController>(); ai.Initialize(grid, combat, turns); turns.ConfigureEnemyAI(ai);
                var visibility = root.AddComponent<VisibilitySystem>(); visibility.Initialize(grid, new[] { player, enemy });
                turns.ConfigureVisibility(visibility); turns.Initialize(new[] { player, enemy });
                Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(7));
                Assert.That(player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(35));
                player.Health.ApplyDamage(TargetBodyPart.Torso, 5); player.ActionPoints.TrySpend(4);
                Assert.That(turns.TryEndTurn(), Is.True);
                float deadline = Time.realtimeSinceStartup + 5;
                while (!turns.IsPlayerTurn && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(turns.IsPlayerTurn, Is.True);
                Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(7));
                Assert.That(player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(30));
                Assert.That(player.Initialize(grid), Is.True);
                Assert.That(player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(30));
                Assert.That(player.transform.Find("Visual").childCount, Is.EqualTo(1));
                Assert.That(enemy.ActionPoints.MaxPoints, Is.EqualTo(source.BaseAP));
                Assert.That(EditorJsonUtility.ToJson(source), Is.EqualTo(before));
            }
            finally { Object.Destroy(root); Object.Destroy(data); }
            yield return null;
        }
    }
}
#endif
