using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class UnitStatusSourceTests
    {
        private BackpackTestBattle battle;
        [TearDown] public void Cleanup() => battle?.Dispose();
        private IList Parts(Component health)
        {
            var type = typeof(BattleUnit).Assembly.GetType("MechaXCOM.UnitStatusPart");
            Assert.That(type, Is.Not.Null, "UI must read a variable-size status list.");
            var list = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(type));
            var method = health.GetType().GetMethod("GetStatusParts"); Assert.That(method, Is.Not.Null);
            method.Invoke(health, new object[] { list }); return list;
        }
        private object Value(object part, string name) => part.GetType().GetProperty(name).GetValue(part);
        [Test] public void MechaProvidesSevenPartsInDisplayOrderWithLiveHealth()
        {
            battle = new BackpackTestBattle(); var parts = Parts(battle.Player.Health);
            Assert.That(parts.Count, Is.EqualTo(7));
            Assert.That(Value(parts[0], "Label"), Is.EqualTo("Head"));
            Assert.That(Value(parts[1], "Label"), Is.EqualTo("Body"));
            battle.Player.Health.ApplyDamage(TargetBodyPart.RightArm, 3); parts = Parts(battle.Player.Health);
            Assert.That(Value(parts[4], "CurrentHP"), Is.EqualTo(battle.Player.Health.GetCurrentHP(TargetBodyPart.RightArm)));
            Assert.That(Value(parts[4], "MaxHP"), Is.EqualTo(battle.Player.Health.GetMaxHP(TargetBodyPart.RightArm)));
        }
        [Test] public void SingleHealthObjectProvidesOneBodyRow()
        {
            battle = new BackpackTestBattle(); var obj = new GameObject("Single health"); obj.transform.SetParent(battle.Root.transform);
            var health = obj.AddComponent<CoverDurability>(); health.Initialize(); health.ApplyDamage(10);
            var parts = Parts(health); Assert.That(parts.Count, Is.EqualTo(1));
            Assert.That(Value(parts[0], "Label"), Is.EqualTo("Body"));
            Assert.That(Value(parts[0], "CurrentHP"), Is.EqualTo(health.CurrentHP));
        }
        [Test] public void PanelAndReusableRowPrefabsExistWithoutWeaponOrWarningSections()
        {
            var panel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/UI/SelectedUnitInfoPanel.prefab");
            var row = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/UI/UnitStatusRow.prefab");
            Assert.That(panel, Is.Not.Null); Assert.That(row, Is.Not.Null);
            foreach (var transform in panel.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(transform.name, Does.Not.Contain("Weapon"));
                Assert.That(transform.name, Does.Not.Contain("Warning"));
            }
        }
    }
}
