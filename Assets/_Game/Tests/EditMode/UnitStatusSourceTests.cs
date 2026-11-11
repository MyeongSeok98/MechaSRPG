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
        [Test] public void PanelHasReusableHealthAndWeaponRowsWithoutWarningOrAPPips()
        {
            var panel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/UI/SelectedUnitInfoPanel.prefab");
            var row = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/UI/UnitStatusRow.prefab");
            Assert.That(panel, Is.Not.Null); Assert.That(row, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/UI/UnitWeaponRow.prefab"), Is.Not.Null);
            Assert.That(panel.transform.Find("Panel/Weapons"), Is.Not.Null);
            foreach (var path in new[] { "Panel/Status Header/Status Title", "Panel/Weapon Header/Weapon Title" })
            {
                var title = panel.transform.Find(path).GetComponent<UnityEngine.UI.Text>();
                float headerHeight = title.transform.parent.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight;
                float available = headerHeight + title.rectTransform.sizeDelta.y;
                Assert.That(available, Is.GreaterThanOrEqualTo(title.fontSize), "Section title must fit without vertical truncation.");
            }
            foreach (var transform in panel.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(transform.name, Does.Not.Contain("Pip"));
                Assert.That(transform.name, Does.Not.Contain("Warning"));
            }
        }
    }
}
