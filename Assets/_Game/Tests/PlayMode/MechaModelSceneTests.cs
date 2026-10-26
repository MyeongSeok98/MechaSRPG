#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class MechaModelSceneTests
    {
        [UnityTest] public IEnumerator BothSavedScenesHaveOneDataModelPerUnitAndCorrectFogVisibility()
        {
            foreach (var name in new[] { "1V1Test", "SqurdWeaponTest" })
            {
                string path = "Assets/_Game/Scenes/" + name + ".unity";
                var bytes = File.ReadAllBytes(path);
                EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
                yield return null; yield return null;
                try
                {
                    var units = Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None);
                    Assert.That(units, Has.Length.EqualTo(name == "1V1Test" ? 3 : 6));
                    var visibility = Object.FindFirstObjectByType<VisibilitySystem>();
                    foreach (var unit in units)
                    {
                        var presenter = unit.GetComponent<UnitMechaVisual>();
                        Assert.That(presenter, Is.Not.Null, unit.name);
                        string expectedId = unit.Team == UnitTeam.Player ? "sample_mecha" : "sample_enemy_mecha";
                        Assert.That(unit.MechaData.MechaId, Is.EqualTo(expectedId), unit.name);
                        Assert.That(unit.ActionPoints.MaxPoints, Is.EqualTo(unit.MechaData.BaseAP));
                        Assert.That(unit.MechaAccuracy, Is.EqualTo(unit.MechaData.Accuracy));
                        Assert.That(unit.MechaEvasion, Is.EqualTo(unit.MechaData.Evasion));
                        for (int part = (int)TargetBodyPart.Torso; part <= (int)TargetBodyPart.RightLeg; part++)
                            Assert.That(unit.Health.GetMaxHP((TargetBodyPart)part), Is.EqualTo(unit.MechaData.GetMaxHP((TargetBodyPart)part)), unit.name);
                        Assert.That(presenter.ModelInstance, Is.Not.Null);
                        Assert.That(unit.transform.Find("Visual").childCount, Is.EqualTo(1));
                        Assert.That(unit.GetComponent<MeshRenderer>().enabled, Is.False);
                        Assert.That(unit.GetComponent<CapsuleCollider>().enabled, Is.True);
                        var renderers = presenter.ModelInstance.GetComponentsInChildren<Renderer>(true);
                        Assert.That(renderers, Is.Not.Empty);
                        bool visible = unit.Team == UnitTeam.Player || visibility.IsVisibleTo(UnitTeam.Player, unit);
                        Assert.That(renderers.Any(r => r.enabled), Is.EqualTo(visible));
                    }
                    var enemy = units.First(u => u.Team == UnitTeam.Enemy);
                    enemy.Health.ApplyDamage(TargetBodyPart.Torso, enemy.Health.GetMaxHP(TargetBodyPart.Torso));
                    Assert.That(enemy.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled), Is.True);
                }
                finally
                {
                    var old = SceneManager.GetActiveScene();
                    SceneManager.SetActiveScene(SceneManager.CreateScene("Model scene cleanup"));
                    SceneManager.UnloadSceneAsync(old);
                    Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
                }
                yield return null;
            }
        }
    }
}
#endif
