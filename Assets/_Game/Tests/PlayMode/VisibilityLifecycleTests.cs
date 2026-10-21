using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class VisibilityLifecycleTests
    {
        private GameObject root;
        private GridManager grid;
        private VisibilitySystem visibility;
        private BattleUnit player, enemy;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            root = new GameObject("Visibility lifecycle");
            grid = root.AddComponent<GridManager>(); grid.Configure(20, 20, 2); grid.Generate();
            player = Unit(1, 1, UnitTeam.Player);
            enemy = Unit(3, 1, UnitTeam.Enemy);
            visibility = root.AddComponent<VisibilitySystem>();
            visibility.Initialize(grid, new[] { player, enemy });
            yield return null;
        }
        private BattleUnit Unit(int x, int z, UnitTeam team)
        {
            var unit = new GameObject(team.ToString()).AddTestBattleUnit();
            unit.transform.SetParent(root.transform); unit.Configure(new Vector2Int(x, z), 5);
            unit.ConfigureTeam(team); Assert.That(unit.Initialize(grid), Is.True); return unit;
        }
        [UnityTearDown]
        public IEnumerator TearDown() { Object.Destroy(root); yield return null; }

        [UnityTest]
        public IEnumerator ObserverAndSensorEnableDisableRefreshVisibilityImmediately()
        {
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.True);
            player.enabled = false;
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.False);
            player.enabled = true;
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.True);
            player.gameObject.SetActive(false);
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.False);
            player.gameObject.SetActive(true);
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.True);
            player.Vision.enabled = false;
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.False);
            player.Vision.enabled = true;
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.True);
            player.Vision.Configure(0);
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CriticalAndEjectedObserversLoseSightWithoutManualRefresh()
        {
            player.Health.ConfigureDestructionChance(0);
            player.Health.ApplyDamage(TargetBodyPart.Head, 6);
            player.Health.ApplyDamage(TargetBodyPart.LeftArm, 8);
            player.Health.ApplyDamage(TargetBodyPart.RightArm, 8);
            Assert.That(player.Health.State, Is.EqualTo(MechState.Critical));
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.False);
            Assert.That(visibility.IsVisibleTo(UnitTeam.Enemy, player), Is.True);
            player.Health.MarkEjected();
            Assert.That(visibility.IsVisibleTo(UnitTeam.Enemy, player), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemovedObserverLosesSightWithoutManualRefresh()
        {
            Object.Destroy(player.gameObject);
            yield return null;
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.False);
        }

        [UnityTest]
        public IEnumerator DestroyedObserverLosesSightWithoutManualRefresh()
        {
            player.Health.ApplyDamage(TargetBodyPart.Torso, 20);
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReinitializationAndDisableDoNotLeaveDuplicateSubscriptions()
        {
            visibility.Initialize(grid, new[] { player, enemy, player });
            visibility.Initialize(grid, new[] { player, enemy });
            int notifications = 0;
            visibility.Changed += () => notifications++;
            visibility.enabled = false;
            Assert.That(visibility.IsReady, Is.False);
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.False);
            int disabledNotifications = notifications;
            grid.TryMoveUnit(player, new Vector2Int(1, 15));
            Assert.That(notifications, Is.EqualTo(disabledNotifications));
            visibility.enabled = true;
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.False);
            int enabledNotifications = notifications;
            grid.TryMoveUnit(player, new Vector2Int(1, 1));
            Assert.That(visibility.IsVisibleTo(UnitTeam.Player, enemy), Is.True);
            Assert.That(notifications, Is.EqualTo(enabledNotifications + 1));
            yield return null;
        }
    }
}
