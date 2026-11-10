#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class DestroyStationPlayTests
    {
        private const string ScenePath = "Assets/_Game/Scenes/DestroyCommunicationStationTest.unity";
        private byte[] sceneBytes;
        private TurnManager turns;
        private MissionController mission;
        private MissionTarget target;
        private CoverDurability health;
        private CoverObject cover;
        private PlayerInputController input;
        private BattleUnit[] allies, enemies;
        private MissionData missionData;
        private DestructibleObjectData objectData;
        private string missionJson, objectJson;
        private Mouse mouse;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private InputSettings.BackgroundBehavior oldBackground;
        [UnitySetUp] public IEnumerator Setup()
        {
            sceneBytes = File.ReadAllBytes(ScenePath);
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode; oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            yield return Load(); missionData=mission.Data; objectData=health.Data;
            missionJson=JsonUtility.ToJson(missionData); objectJson=JsonUtility.ToJson(objectData);
        }
        private IEnumerator Load()
        {
            EditorSceneManager.LoadSceneInPlayMode(ScenePath,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            turns=Object.FindFirstObjectByType<TurnManager>(); mission=turns.Mission;
            target=Object.FindFirstObjectByType<MissionTarget>(); health=target.ObjectHealth; cover=health.GetComponent<CoverObject>();
            input=Object.FindFirstObjectByType<PlayerInputController>();
            var units=Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None);
            allies=units.Where(u=>u.Team==UnitTeam.Player).OrderBy(u=>u.StartGridPosition.y).ToArray();
            enemies=units.Where(u=>u.Team==UnitTeam.Enemy).ToArray();
            Assert.That(turns.IsInitialized,Is.True); yield return TurnBannerTestSupport.Finish(turns);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(mouse!=null) InputSystem.RemoveDevice(mouse); mouse=null;
            InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor; InputSystem.settings.backgroundBehavior=oldBackground;
            Assert.That(JsonUtility.ToJson(missionData),Is.EqualTo(missionJson));
            Assert.That(JsonUtility.ToJson(objectData),Is.EqualTo(objectJson));
            var old=SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("Station cleanup"));
            yield return SceneManager.UnloadSceneAsync(old);
            Assert.That(File.ReadAllBytes(ScenePath),Is.EqualTo(sceneBytes));
        }
        private IEnumerator FinishPopup()
        {
            float deadline=Time.realtimeSinceStartup+3;
            while(turns.IsPresentingAttack&&Time.realtimeSinceStartup<deadline) yield return null;
            yield return null; Assert.That(turns.IsPresentingAttack,Is.False);
        }
        private void Aim(BattleUnit actor)
        {
            Assert.That(input.SelectUnit(actor),Is.True);
            Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand),Is.True);
            Assert.That(input.TrySelectCover(cover),Is.True,input.CurrentCoverPreview.FailureReason);
        }
        private void FireViaUI(BattleUnit actor)
        {
            Aim(actor); var panel=input.GetComponent<CoverTargetUI>(); Assert.That(panel.IsVisible,Is.True);
            var rect=panel.FireButtonRect;
            panel.HandleScreenClick(new Vector2(rect.center.x,Screen.height-rect.center.y));
            Assert.That(input.LastCoverAttackResult.WasExecuted,Is.True); Assert.That(input.LastCoverAttackResult.DamageDealt,Is.EqualTo(1000));
        }
        [UnityTest] public IEnumerator SceneHasTwoVsTwoAndDataDrivenStation()
        {
            Assert.That(allies.Length,Is.EqualTo(2)); Assert.That(enemies.Length,Is.EqualTo(2));
            Assert.That(mission.Data.ObjectiveType,Is.EqualTo(MissionObjectiveType.DestroyTarget));
            Assert.That(target.TargetId,Is.EqualTo(mission.Data.TargetObjectId));
            Assert.That(health.MaxHP,Is.EqualTo(objectData.MaxHP)); Assert.That(health.CurrentHP,Is.EqualTo(2000));
            Assert.That(mission.Objective.TotalCount,Is.EqualTo(1)); Assert.That(mission.Objective.DestroyedCount,Is.Zero);
            Aim(allies[0]); Assert.That(input.CurrentCoverPreview.MinDamage,Is.EqualTo(1000)); yield return null;
        }
        [UnityTest] public IEnumerator TwoUIShotsDestroyStationAndWaitForPopupBeforeVictory()
        {
            int ap=allies[0].ActionPoints.CurrentPoints; int ammo=allies[0].Ammo.GetStatus(WeaponSlot.RightHand).Loaded;
            FireViaUI(allies[0]); Assert.That(health.CurrentHP,Is.EqualTo(1000));
            Assert.That(allies[0].ActionPoints.CurrentPoints,Is.EqualTo(ap-3)); Assert.That(allies[0].Ammo.GetStatus(WeaponSlot.RightHand).Loaded,Is.EqualTo(ammo-1));
            Assert.That(turns.IsBattleOver,Is.False); yield return FinishPopup();
            FireViaUI(allies[1]); Assert.That(health.IsDestroyed,Is.True); Assert.That(mission.Objective.DestroyedCount,Is.EqualTo(1));
            Assert.That(turns.IsPresentingAttack,Is.True); Assert.That(turns.IsBattleOver,Is.False);
            Assert.That(cover.Collider.enabled,Is.False); yield return FinishPopup();
            Assert.That(mission.Result.Outcome,Is.EqualTo(MissionOutcome.Success)); Assert.That(turns.IsBattleOver,Is.True);
            Assert.That(enemies.All(e=>e.IsAlive),Is.True);
        }
        [UnityTest] public IEnumerator EnemyEliminationAloneDoesNotFinishStationMission()
        {
            foreach(var enemy in enemies) enemy.Health.ApplyDamage(TargetBodyPart.Torso,int.MaxValue);
            yield return null; Assert.That(turns.IsBattleOver,Is.False); Assert.That(health.CurrentHP,Is.EqualTo(2000));
        }
        [UnityTest] public IEnumerator PlayerAnnihilationFailsWithStationIntact()
        {
            foreach(var ally in allies) ally.Health.ApplyDamage(TargetBodyPart.Torso,int.MaxValue);
            yield return null; Assert.That(mission.Result.Outcome,Is.EqualTo(MissionOutcome.Failure)); Assert.That(health.IsDestroyed,Is.False);
        }
        [UnityTest] public IEnumerator ReloadStartsFreshHealthAmmoAPAndProgress()
        {
            FireViaUI(allies[0]); yield return FinishPopup(); Assert.That(health.CurrentHP,Is.EqualTo(1000));
            yield return Load(); Assert.That(health.CurrentHP,Is.EqualTo(2000)); Assert.That(mission.Objective.DestroyedCount,Is.Zero);
            Assert.That(allies[0].Ammo.GetStatus(WeaponSlot.RightHand).Loaded,Is.EqualTo(3));
            Assert.That(allies[0].ActionPoints.CurrentPoints,Is.EqualTo(allies[0].ActionPoints.MaxPoints));
        }
        [UnityTest] public IEnumerator RemovingStationWithoutDamageDoesNotWin()
        {
            Object.Destroy(health.gameObject); yield return null; yield return null;
            Assert.That(turns.IsBattleOver,Is.False); Assert.That(mission.Objective.DestroyedCount,Is.Zero);
        }
        [UnityTest] public IEnumerator MouseClickOnVisibleBuildingSelectsStationForAttack()
        {
            Assert.That(input.SelectUnit(allies[0]),Is.True); Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand),Is.True);
            var camera=Camera.main; var point=camera.WorldToScreenPoint(cover.Collider.bounds.center);
            Assert.That(camera.pixelRect.Contains(point),Is.True,"Sample station must be inside the starting camera viewport.");
            Physics.SyncTransforms(); Assert.That(Physics.Raycast(camera.ScreenPointToRay(point),out var hit),Is.True);
            Assert.That(hit.collider.GetComponentInParent<CoverObject>(),Is.SameAs(cover));
            mouse=InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}); yield return null;
            Assert.That(input.TargetCover,Is.SameAs(cover));
        }
    }
}
#endif
