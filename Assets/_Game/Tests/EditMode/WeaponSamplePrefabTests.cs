using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace MechaXCOM.Tests
{
    public class WeaponSamplePrefabTests
    {
        [Test] public void RifleHasGripRootAndForwardMuzzle()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Weapons/Rifles/BeamRifle.prefab");
            Assert.That(asset, Is.Not.Null, "A separate attachable rifle prefab is required.");
            var muzzle = asset.transform.Find("Muzzle");
            Assert.That(muzzle, Is.Not.Null);
            Assert.That(asset.transform.Find("Model"), Is.Not.Null);
            Assert.That(asset.GetComponentsInChildren<Renderer>().Length, Is.GreaterThan(0));
        }

        [Test] public void SampleMechaAttachesTheRifleToItsOwnRightHandSocket()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Mecha/Samples/MechaReferenceWithRifle.prefab");
            Assert.That(asset, Is.Not.Null, "The model sample must not require modifying battle scenes.");
            var sockets = asset.GetComponentsInChildren<Transform>();
            Transform socket = null;
            foreach (var item in sockets) if (item.name == "RightHandWeaponSocket") socket = item;
            Assert.That(socket, Is.Not.Null);
            Assert.That(socket.parent.name, Is.EqualTo("Model"));
            Assert.That(socket.childCount, Is.EqualTo(1));
            var weapon = socket.GetChild(0);
            Assert.That(weapon.Find("Muzzle"), Is.Not.Null);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(weapon.gameObject),
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Weapons/Rifles/BeamRifle.prefab")));
        }

        [Test] public void MechaModelKeepsDistinctSourceColorsInURP()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Mecha/Samples/MechaReferenceWithRifle.prefab");
            Assert.That(asset, Is.Not.Null);
            var renderer = asset.transform.Find("Model/default").GetComponent<Renderer>();
            Assert.That(renderer.sharedMaterials.Select(material => material.GetColor("_BaseColor")).Distinct().Count(),
                Is.GreaterThan(3), "Source material colors must survive URP material import.");
        }

        [Test] public void PlayerUsesNestedMechaVisualAndKeepsBattleComponents()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/BattleUnit.prefab");
            var visual = asset.transform.Find("Visual");
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.childCount, Is.EqualTo(1));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(visual.GetChild(0).gameObject),
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Mecha/Samples/MechaReferenceWithRifle.prefab")));
            Assert.That(asset.GetComponent<MeshRenderer>().enabled, Is.False);
            Assert.That(asset.GetComponent<CapsuleCollider>().enabled, Is.True);
            Assert.That(asset.GetComponent<UnitMovement>(), Is.Not.Null);
            Assert.That(asset.GetComponent<UnitActionPoints>(), Is.Not.Null);
            Assert.That(asset.GetComponent<UnitCombat>(), Is.Not.Null);
            Assert.That(asset.GetComponent<UnitHealth>(), Is.Not.Null);
            Assert.That(visual.localPosition.y, Is.EqualTo(-asset.GetComponent<BattleUnit>().HeightOffset));
            Assert.That(visual.GetComponentsInChildren<Renderer>().Any(r => r.enabled), Is.True);
        }
    }
}
