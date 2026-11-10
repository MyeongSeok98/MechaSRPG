#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class AllWeaponStationPlayTests
    {
        private const string Path="Assets/_Game/Scenes/DestroyCommunicationStationTest.unity";
        private byte[] bytes;
        private TurnManager turns;
        private PlayerInputController input;
        private CoverObject station;
        private CoverDurability health;
        private BattleUnit actor;
        private bool loaded;
        private Mouse mouse;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private InputSettings.BackgroundBehavior oldBackground;
        [UnitySetUp] public IEnumerator Setup()
        {
            bytes=File.ReadAllBytes(Path); loaded=false;
            oldEditor=InputSystem.settings.editorInputBehaviorInPlayMode; oldBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus; yield return null;
        }
        private IEnumerator Load(bool melee)
        {
            loaded=true; EditorSceneManager.LoadSceneInPlayMode(Path,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            if(melee)
            {
                // Only this test instance changes equipment; never save the authored scene/asset.
                var blade=AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Game/Data/Weapons/Melee/StandardBlade.asset");
                foreach(var unit in Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None).Where(u=>u.Team==UnitTeam.Player))
                {
                    var loadout=unit.GetComponent<UnitWeaponLoadout>();
                    Assert.That(loadout.TryConfigure(blade,GripMode.OneHand,loadout.RightWeapon,loadout.RightGrip),Is.True);
                }
            }
            turns=Object.FindFirstObjectByType<TurnManager>(); input=Object.FindFirstObjectByType<PlayerInputController>();
            station=Object.FindFirstObjectByType<MissionTarget>().ObjectHealth.GetComponent<CoverObject>(); health=station.GetComponent<CoverDurability>();
            actor=Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None).First(u=>u.Team==UnitTeam.Player);
            yield return TurnBannerTestSupport.Finish(turns);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(mouse!=null) InputSystem.RemoveDevice(mouse); mouse=null;
            InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor; InputSystem.settings.backgroundBehavior=oldBackground;
            if(loaded)
            {
                var old=SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("All weapon station cleanup"));
                yield return SceneManager.UnloadSceneAsync(old);
            }
            Assert.That(File.ReadAllBytes(Path),Is.EqualTo(bytes));
        }
        private IEnumerator ConfirmAndFinish(bool usesAmmo)
        {
            Assert.That(input.TargetCover,Is.SameAs(station));
            var preview=input.CurrentCoverPreview; Assert.That(preview.UsesAmmo,Is.EqualTo(usesAmmo));
            if(health.CurrentHP>preview.MinDamage) health.ApplyDamage(health.CurrentHP-preview.MinDamage);
            int before=health.CurrentHP, ap=actor.ActionPoints.CurrentPoints;
            int ammo=usesAmmo?actor.Ammo.GetStatus(WeaponSlot.LeftHand).Loaded:0;
            var panel=input.GetComponent<CoverTargetUI>(); var rect=panel.FireButtonRect;
            panel.HandleScreenClick(new Vector2(rect.center.x,Screen.height-rect.center.y));
            Assert.That(input.LastCoverAttackResult.WasExecuted,Is.True,input.LastCoverAttackResult.FailureReason);
            Assert.That(input.LastCoverAttackResult.DamageDealt,Is.EqualTo(before));
            Assert.That(actor.ActionPoints.CurrentPoints,Is.EqualTo(ap-preview.APCost));
            if(usesAmmo) Assert.That(actor.Ammo.GetStatus(WeaponSlot.LeftHand).Loaded,Is.EqualTo(ammo-1));
            Assert.That(turns.IsPresentingAttack,Is.True); Assert.That(turns.IsBattleOver,Is.False);
            float deadline=Time.realtimeSinceStartup+3;
            while(!turns.IsBattleOver&&Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(turns.Mission.Result.Outcome,Is.EqualTo(MissionOutcome.Success));
        }
        [UnityTest] public IEnumerator BeamRifleMouseTargetAndFireCompleteStationMission()
        {
            yield return Load(false); var combat=Object.FindFirstObjectByType<CombatSystem>(); combat.SetDamageRollSource((min,max)=>min);
            Assert.That(input.SelectUnit(actor),Is.True); Assert.That(input.BeginWeaponAttack(WeaponSlot.LeftHand),Is.True);
            // Focus the runtime camera so the wider overlay cannot hide the station click.
            var orbit=Camera.main.GetComponent<BattleCameraOrbit>(); Assert.That(orbit.TryFocus(station.Collider.bounds.center),Is.True);
            float focusUntil=Time.realtimeSinceStartup+2;
            while((bool)typeof(BattleCameraOrbit).GetField("focusing",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(orbit) && Time.realtimeSinceStartup<focusUntil) yield return null;
            yield return null;
            var point=Camera.main.WorldToScreenPoint(station.Collider.bounds.center);
            Assert.That(Object.FindFirstObjectByType<BattleDebugUI>().ContainsScreenPoint(point),Is.False);
            Assert.That(Camera.main.pixelRect.Contains(point),Is.True); Physics.SyncTransforms();
            mouse=InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}); yield return null;
            yield return ConfirmAndFinish(true);
        }
        [UnityTest] public IEnumerator MeleeConfirmUsesAPWithoutAmmoAndCompletesStationMission()
        {
            yield return Load(true); var grid=Object.FindFirstObjectByType<GridManager>();
            Assert.That(grid.TryMoveUnit(actor,new Vector2Int(5,2)),Is.True); actor.Ammo.enabled=false;
            var combat=Object.FindFirstObjectByType<CombatSystem>(); combat.SetDamageRollSource((min,max)=>min);
            Assert.That(input.SelectUnit(actor),Is.True,"Select melee actor");
            Assert.That(actor.GetComponent<UnitWeaponLoadout>().LeftWeapon.AttackType,Is.EqualTo(AttackType.Melee));
            Assert.That(input.BeginWeaponAttack(WeaponSlot.LeftHand),Is.True,"Begin melee weapon attack");
            Assert.That(input.TrySelectCover(station),Is.True,input.InputMessage); Assert.That(input.CurrentCoverPreview.Type,Is.EqualTo(AttackType.Melee));
            yield return ConfirmAndFinish(false);
        }
    }
}
#endif
