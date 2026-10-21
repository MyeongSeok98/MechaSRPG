using System;
using NUnit.Framework;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class BodyTargetCombatTests
    {
        private GameObject root;
        private CombatSystem combat;
        private BattleUnit player, enemy;
        private GridManager grid;
        [SetUp] public void Setup()
        {
            root = new GameObject("Body target rules");
            grid = root.AddComponent<GridManager>(); grid.Generate();
            player = Unit(new Vector2Int(1, 1), UnitTeam.Player);
            enemy = Unit(new Vector2Int(3, 1), UnitTeam.Enemy);
            var turns = root.AddComponent<TurnManager>(); turns.Initialize(new[] { player, enemy });
            var visibility = root.AddComponent<VisibilitySystem>();
            visibility.Initialize(grid, new[] { player, enemy });
            turns.ConfigureVisibility(visibility);
            combat = root.AddComponent<CombatSystem>(); combat.Initialize(grid, root.AddComponent<CoverSystem>(), turns);
        }
        private BattleUnit Unit(Vector2Int position, UnitTeam team)
        {
            var unit = new GameObject(team.ToString()).AddTestBattleUnit(); unit.transform.SetParent(root.transform);
            unit.Configure(position, 5); unit.ConfigureTeam(team); unit.Initialize(grid); return unit;
        }
        [TearDown] public void Cleanup() => UnityEngine.Object.DestroyImmediate(root);
        private object Invoke(string method, AttackType type, string part)
        {
            var value = (TargetBodyPart)Enum.Parse(typeof(TargetBodyPart), part);
            if (method == "GetPreview") return combat.GetPreview(player, enemy, type, value);
            return combat.TryAttack(player, enemy, type, value);
        }
        [TestCase("Whole", 80)] [TestCase("Torso", 80)] [TestCase("Cockpit", 36)]
        [TestCase("Head", 64)] [TestCase("LeftArm", 72)] [TestCase("RightArm", 72)]
        [TestCase("LeftLeg", 72)] [TestCase("RightLeg", 72)]
        public void RangedPreviewAppliesPartMultiplier(string part, int chance)
        {
            var preview = (AttackPreview)Invoke("GetPreview", AttackType.Ranged, part);
            Assert.That(preview.HitChance, Is.EqualTo(chance));
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [Test] public void PreviewPenaltyIncludesCoverIgnoresMovementAndFireUsesThatChance()
        {
            grid.TryGetCell(enemy.CurrentGridPosition, out var cell); cell.SetCover(CoverDirection.West, CoverType.Half);
            enemy.Mobility.RecordCellMoved();
            Assert.That(((AttackPreview)Invoke("GetPreview", AttackType.Ranged, "Cockpit")).HitChance, Is.EqualTo(27));
            combat.SetRollSource(() => 27);
            var result = (AttackResult)Invoke("TryAttack", AttackType.Ranged, "Cockpit");
            Assert.That(result.HitChance, Is.EqualTo(27)); Assert.That(result.Hit, Is.False);
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Cockpit), Is.EqualTo(6));
            Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(2));
        }
        [Test] public void MeleeIgnoresBodyPenalty()
        { Assert.That(((AttackPreview)Invoke("GetPreview", AttackType.Melee, "Cockpit")).HitChance, Is.EqualTo(95)); }
        [Test] public void LowAccuracyStillScalesByPartMultiplier()
        {
            player.Combat.ConfigureTestRanged(5, 10, 7, 3);
            Assert.That(((AttackPreview)Invoke("GetPreview", AttackType.Ranged, "Cockpit")).HitChance, Is.EqualTo(5));
        }
    }
}
