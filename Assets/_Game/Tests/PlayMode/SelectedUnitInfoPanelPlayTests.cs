#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MechaXCOM.Tests
{
    public class SelectedUnitInfoPanelPlayTests
    {
        private BackpackTestBattle battle;
        private BattleDebugUI debug;
        private SelectedUnitInfoPanel panel;
        private PlayerInputController input;
        private Camera camera;
        private Mouse mouse;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private InputSettings.BackgroundBehavior oldBackground;
        [UnitySetUp] public IEnumerator Setup()
        {
            battle = new BackpackTestBattle();
            battle.Player.gameObject.AddComponent<CapsuleCollider>(); battle.Enemy.gameObject.AddComponent<CapsuleCollider>();
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode; oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = InputSystem.AddDevice<Mouse>();
            var visual = battle.Root.AddComponent<GridVisualizer>(); visual.Initialize(battle.Grid, battle.Turns);
            debug = battle.Root.AddComponent<BattleDebugUI>(); debug.Initialize(battle.Grid, battle.Cover);
            camera = new GameObject("Panel camera").AddComponent<Camera>(); camera.transform.SetParent(battle.Root.transform);
            camera.orthographic = true; camera.orthographicSize = 12; camera.transform.position = new Vector3(12, 20, -16); camera.transform.LookAt(new Vector3(12, 0, 10));
            var orbit = camera.gameObject.AddComponent<BattleCameraOrbit>(); orbit.Initialize(battle.Grid); orbit.ConfigureUI(debug);
            input = battle.Root.AddComponent<PlayerInputController>(); input.Initialize(camera, battle.Grid, visual, debug); input.InitializeCombat(battle.Combat, battle.Turns);
            debug.InitializeCombat(input, battle.Turns);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/UI/SelectedUnitInfoPanel.prefab");
            Assert.That(prefab, Is.Not.Null); panel = Object.Instantiate(prefab, battle.Root.transform).GetComponent<SelectedUnitInfoPanel>();
            panel.Initialize(debug, battle.Turns); debug.ConfigureUnitInfoPanel(panel);
            if (!Object.FindFirstObjectByType<EventSystem>())
            { var events = new GameObject("Test UI events", typeof(EventSystem), typeof(InputSystemUIInputModule)); events.transform.SetParent(battle.Root.transform); }
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            InputSystem.RemoveDevice(mouse); InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor; InputSystem.settings.backgroundBehavior = oldBackground;
            battle.Dispose(); yield return null;
        }
        private UnitStatusRowUI[] Rows => panel.GetComponentsInChildren<UnitStatusRowUI>();
        private RectTransform Frame => panel.transform.Find("Panel").GetComponent<RectTransform>();
        [UnityTest] public IEnumerator RingStaysOnModelCenterAtEdgesAndAfterCameraChanges()
        {
            var template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            template.transform.SetParent(battle.Root.transform); template.transform.localPosition = Vector3.up * 3;
            template.transform.localScale = new Vector3(2, 6, 2);
            var visual = battle.Player.gameObject.AddComponent<UnitMechaVisual>(); visual.Initialize(template, 1);
            Assert.That(input.SelectUnit(battle.Player), Is.True); yield return null; Canvas.ForceUpdateCanvases();
            var actions = battle.Root.GetComponent<UnitActionUI>();
            var centerProperty = typeof(UnitActionUI).GetProperty("RingCenter"); Assert.That(centerProperty, Is.Not.Null);
            var body = visual.ModelInstance.GetComponent<Renderer>();
            foreach (var target in new[] { new Vector2(.6f, .97f), new Vector2(.24f, .5f), new Vector2(.97f, .5f) })
            {
                var anchor = new Vector3(battle.Player.transform.position.x, body.bounds.center.y, battle.Player.transform.position.z);
                float depth = camera.WorldToScreenPoint(anchor).z;
                camera.transform.position += anchor - camera.ScreenToWorldPoint(new Vector3(Screen.width * target.x, Screen.height * target.y, depth));
                yield return null;
                var expected = camera.WorldToScreenPoint(anchor);
                var actual = (Vector2)centerProperty.GetValue(actions);
                Assert.That(Vector2.Distance(actual, new Vector2(expected.x, Screen.height - expected.y)), Is.LessThan(.5f));
                Assert.That(actions.TryGetPanelRect(out _), Is.True, "The body can remain visible even when the root is near an edge.");
            }
            camera.transform.RotateAround(battle.Player.transform.position, Vector3.up, 90); camera.orthographicSize *= .7f;
            yield return null;
            var point = camera.WorldToScreenPoint(new Vector3(battle.Player.transform.position.x, body.bounds.center.y, battle.Player.transform.position.z));
            Assert.That(Vector2.Distance((Vector2)centerProperty.GetValue(actions), new Vector2(point.x, Screen.height - point.y)), Is.LessThan(.5f));
        }
        [UnityTest] public IEnumerator WeaponChoicesDoNotCoverWheelOrHUDWhenNearBothOverlays()
        {
            float depth = camera.WorldToScreenPoint(battle.Player.transform.position).z;
            camera.transform.position += battle.Player.transform.position - camera.ScreenToWorldPoint(new Vector3(Screen.width * .6f, Screen.height * .48f, depth));
            Assert.That(input.SelectUnit(battle.Player), Is.True); yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(input.OpenAttackMenu(), Is.True); yield return null;
            var actions = battle.Root.GetComponent<UnitActionUI>();
            Assert.That(actions.TryGetPanelRect(out var main), Is.True); Assert.That(actions.TryGetAttackMenuRect(out var choices), Is.True);
            Assert.That(main.Overlaps(choices), Is.False);
            Assert.That(debug.TryGetUnitInfoRect(out var info), Is.True); Assert.That(info.Overlaps(choices), Is.False);
            if (debug.TryGetCompactControlsRect(out var controls)) Assert.That(controls.Overlaps(choices), Is.False);
        }
        [UnityTest] public IEnumerator IndirectConfirmationLeavesPrimaryActionsOutsideItsPanel()
        {
            float depth = camera.WorldToScreenPoint(battle.Player.transform.position).z;
            camera.transform.position += battle.Player.transform.position - camera.ScreenToWorldPoint(new Vector3(Screen.width * .83f, Screen.height * .5f, depth));
            IndirectTestSupport.Configure(battle);
            Assert.That(input.SelectUnit(battle.Player), Is.True); Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand), Is.True);
            Assert.That(input.TrySelectImpactCell(new Vector2Int(2, 2)), Is.True); yield return null;
            var area = battle.Root.GetComponent<IndirectTargetUI>(); Assert.That(area.IsVisible, Is.True);
            var actions = battle.Root.GetComponent<UnitActionUI>(); Assert.That(actions.TryGetPanelRect(out var main), Is.True);
            Assert.That(main.Overlaps(area.PanelRect), Is.False);
        }
        [UnityTest] public IEnumerator CommandWheelRetainsChosenActionAndEmptyCenterDoesNotCaptureMap()
        {
            float depth = camera.WorldToScreenPoint(battle.Player.transform.position).z;
            camera.transform.position += battle.Player.transform.position - camera.ScreenToWorldPoint(new Vector3(Screen.width * .6f, Screen.height * .5f, depth));
            Assert.That(input.SelectUnit(battle.Player), Is.True); yield return null;
            var actions = battle.Root.GetComponent<UnitActionUI>();
            Assert.That(actions.IsCommandWheelVisible, Is.True);
            Assert.That(actions.TryGetPanelRect(out var rect), Is.True);
            Assert.That(actions.ContainsScreenPoint(new Vector2(rect.center.x, Screen.height - rect.center.y)), Is.False);
            float initial = actions.SelectionAnimationSeconds;
            Assert.That(input.BeginMove(), Is.True); yield return null;
            Assert.That(actions.IsCommandWheelVisible, Is.True);
            Assert.That(actions.SelectedCommand, Is.EqualTo(UnitCommandAction.Move));
            Assert.That(actions.SelectionAnimationSeconds, Is.GreaterThanOrEqualTo(initial));
            Assert.That(input.OpenAttackMenu(), Is.True); yield return null;
            Assert.That(actions.SelectedCommand, Is.EqualTo(UnitCommandAction.Attack));
            Assert.That(actions.TryGetAttackMenuRect(out var choices), Is.True);
            Assert.That(actions.TryGetPanelRect(out rect), Is.True); Assert.That(rect.Overlaps(choices), Is.False);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(input.SelectUnit(battle.Player), Is.True);
            Assert.That(actions.SelectionAnimationSeconds, Is.LessThan(.05f), "Explicit reselection restarts the effect.");
            Assert.That(actions.SelectedCommand, Is.EqualTo(UnitCommandAction.None));
        }
        [UnityTest] public IEnumerator AssignedFontsRenderBothTitlesAndLiveBodyRows()
        {
            JsonUtility.FromJsonOverwrite("{\"displayName\":\"Sample Cavalier\"}", battle.Player.MechaData);
            JsonUtility.FromJsonOverwrite("{\"displayName\":\"Sample Pilot\"}", battle.Player.PilotData);
            Assert.That(input.SelectUnit(battle.Player), Is.True); yield return null; yield return null; Canvas.ForceUpdateCanvases();
            foreach (var path in new[] { "Identity/Unit Name", "Identity/Pilot Name", "Status Header/Status Title", "Weapon Header/Weapon Title" })
            {
                var text = Frame.Find(path).GetComponent<Text>();
                Assert.That(text.font.name, Does.Contain("Saira"), path);
                var mesh = text.canvasRenderer.GetMesh();
                Assert.That(mesh && mesh.vertexCount > 0, Is.True, $"{path}: '{text.text}' must produce visible glyphs (font {text.font.name}, rect {text.rectTransform.rect}, lineHeight {text.font.lineHeight}, overflow {text.verticalOverflow}, active {text.isActiveAndEnabled}).");
            }
            foreach (var row in Rows)
                Assert.That(row.GetComponentInChildren<Text>().font.name, Is.EqualTo("Oxanium-Regular"));
            foreach (var row in panel.GetComponentsInChildren<UnitWeaponRowUI>())
                Assert.That(row.transform.Find("Name").GetComponent<Text>().font.name, Is.EqualTo("Oxanium-Regular"));
            JsonUtility.FromJsonOverwrite("{\"displayName\":\"테스트 파일럿\"}", battle.Player.PilotData);
            yield return null; yield return null; Canvas.ForceUpdateCanvases();
            var pilot = Frame.Find("Identity/Pilot Name").GetComponent<Text>();
            Assert.That(pilot.text, Is.EqualTo("테스트 파일럿"));
            Assert.That(pilot.font.name, Does.Not.Contain("Saira"));
            Assert.That(pilot.canvasRenderer.GetMesh().vertexCount, Is.GreaterThan(0));
        }
        [UnityTest] public IEnumerator WeaponRowsKeepDamagedArmsRedAndUpdateResources()
        {
            input.SelectUnit(battle.Player); yield return null;
            Assert.That(panel.GetComponentsInChildren<UnitWeaponRowUI>().Length, Is.EqualTo(3));
            battle.Player.Health.ApplyDamage(TargetBodyPart.RightArm, int.MaxValue); yield return null;
            var row = panel.GetComponentsInChildren<UnitWeaponRowUI>().Single(r => r.Slot == WeaponSlot.RightHand);
            Assert.That(row.IsDisabled, Is.True); Assert.That(row.StatusText, Is.EqualTo("사용 불가"));
            Assert.That(row.NameColor.r, Is.GreaterThan(row.NameColor.g * 2));
            Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand), Is.False);
            battle.Player.ActionPoints.TrySpend(5); yield return null;
            var left = panel.GetComponentsInChildren<UnitWeaponRowUI>().Single(r => r.Slot == WeaponSlot.LeftHand);
            Assert.That(left.IsDisabled, Is.False); Assert.That(left.StatusText, Is.EqualTo("AP 부족"));
            Assert.That(left.ResourceText, Does.Contain("6/6"));
            input.SelectUnit(battle.Enemy); yield return null;
            Assert.That(panel.GetComponentsInChildren<UnitWeaponRowUI>().All(r => !r.IsDisabled), Is.True);
        }
        [UnityTest] public IEnumerator TwoHandWeaponHasOneRowAndFollowsHandTransfer()
        {
            battle.Player.GetComponent<UnitWeaponLoadout>().TryConfigure(null, GripMode.OneHand, battle.Weapon, GripMode.TwoHands);
            input.SelectUnit(battle.Player); yield return null;
            Assert.That(panel.GetComponentsInChildren<UnitWeaponRowUI>().Length, Is.EqualTo(2));
            battle.Player.Health.ApplyDamage(TargetBodyPart.RightArm, int.MaxValue); yield return null;
            var rows = panel.GetComponentsInChildren<UnitWeaponRowUI>(); Assert.That(rows.Length, Is.EqualTo(2));
            Assert.That(rows.Any(r => r.Slot == WeaponSlot.LeftHand && !r.IsDisabled), Is.True);
            Canvas.ForceUpdateCanvases(); Assert.That(Frame.rect.height, Is.LessThan(690));
        }
        private T DebugValue<T>(string property)
        {
            var value = typeof(BattleDebugUI).GetProperty(property); Assert.That(value, Is.Not.Null, property);
            return (T)value.GetValue(debug);
        }
        [UnityTest] public IEnumerator CompactModeKeepsMissionOutcomeAndReasonVisible()
        {
            Assert.That(battle.Turns.Mission.TrySucceed("Panel mission result"), Is.True); yield return null;
            Assert.That(DebugValue<string>("CompactFeedbackText"), Does.Contain("MISSION SUCCESS").And.Contain("Panel mission result"));
            Assert.That(DebugValue<bool>("CompactControlsVisible"), Is.True);
        }
        [UnityTest] public IEnumerator CompactModeKeepsFailureReasonVisible()
        {
            Assert.That(battle.Turns.Mission.TryFail("Panel mission failure"), Is.True); yield return null;
            Assert.That(DebugValue<string>("CompactFeedbackText"), Does.Contain("MISSION FAILED").And.Contain("Panel mission failure"));
        }
        [UnityTest] public IEnumerator CompactModeRetainsLastAttackFeedback()
        {
            input.SelectUnit(battle.Player); input.BeginWeaponAttack(WeaponSlot.RightHand); input.TrySelectTarget(battle.Enemy);
            Assert.That(input.TryAttackSelected().WasExecuted, Is.True); yield return null;
            Assert.That(DebugValue<string>("CompactFeedbackText"), Does.Contain("Damage"));
        }
        [UnityTest] public IEnumerator CompactControlsHideWhileTargetPanelIsVisible()
        {
            input.SelectUnit(battle.Player); yield return null;
            Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand), Is.True); Assert.That(input.TrySelectTarget(battle.Enemy), Is.True); yield return null;
            Assert.That(battle.Root.GetComponent<BodyTargetUI>().IsVisible, Is.True);
            Assert.That(DebugValue<bool>("CompactControlsVisible"), Is.False);
            Assert.That(input.BeginMove(), Is.True); yield return null;
            Assert.That(DebugValue<bool>("CompactControlsVisible"), Is.True);
        }
        [UnityTest] public IEnumerator ActionMenuAvoidsPanelAndItsMoveButtonWorks()
        {
            Assert.That(input.SelectUnit(battle.Player), Is.True); yield return null; Canvas.ForceUpdateCanvases();
            Vector2 desired = RectTransformUtility.WorldToScreenPoint(null, Frame.TransformPoint(Frame.rect.center));
            float depth = camera.WorldToScreenPoint(battle.Player.transform.position).z;
            camera.transform.position += battle.Player.transform.position - camera.ScreenToWorldPoint(new Vector3(desired.x, desired.y, depth));
            yield return null;
            var actions = battle.Root.GetComponent<UnitActionUI>(); Assert.That(actions.TryGetPanelRect(out var rect), Is.True);
            var corners = new Vector3[4]; Frame.GetWorldCorners(corners);
            var guiRect = new Rect(corners[0].x, Screen.height - corners[1].y, corners[2].x - corners[0].x, corners[1].y - corners[0].y);
            Assert.That(rect.Overlaps(guiRect), Is.False, "Action controls must remain outside the unit panel.");
            var button = actions.MoveButtonRect; var point = new Vector2(button.center.x, Screen.height - button.center.y);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
            Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Move));
        }
        [UnityTest] public IEnumerator SelectionShowsSevenLiveRowsAndClearingHidesPanel()
        {
            Assert.That(panel.IsVisible, Is.False); Assert.That(input.SelectUnit(battle.Player), Is.True); yield return null;
            Assert.That(panel.DisplayedUnit, Is.SameAs(battle.Player)); Assert.That(Rows.Length, Is.EqualTo(7));
            Assert.That(panel.APText, Is.EqualTo("AP 5/5")); Assert.That(panel.AvailabilityText, Is.EqualTo("ACTIVE"));
            battle.Player.ActionPoints.TrySpend(2); battle.Player.Health.ApplyDamage(TargetBodyPart.Head, 2); yield return null;
            Assert.That(panel.APText, Is.EqualTo("AP 3/5"));
            var head = Rows.Single(row => row.PartId == "Head");
            Assert.That(head.HealthText, Is.EqualTo($"{battle.Player.Health.GetCurrentHP(TargetBodyPart.Head)}/{battle.Player.Health.GetMaxHP(TargetBodyPart.Head)}"));
            Assert.That(head.HealthFraction, Is.LessThan(1));
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Assert.That(panel.IsVisible, Is.False);
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }
        [UnityTest] public IEnumerator SingleHealthSourceCreatesOnlyOneRowAndSmallerPanel()
        {
            panel.Initialize(null, battle.Turns); panel.ShowUnit(battle.Player, battle.Player.Health); Canvas.ForceUpdateCanvases();
            float sevenHeight = Frame.rect.height;
            var obj = new GameObject("Single body"); obj.transform.SetParent(battle.Root.transform); var health = obj.AddComponent<CoverDurability>(); health.ApplyDamage(10);
            panel.ShowUnit(battle.Player, health); Canvas.ForceUpdateCanvases(); yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(Rows.Length, Is.EqualTo(1)); Assert.That(Rows[0].HealthText, Is.EqualTo("990/1000"));
            Assert.That(Frame.rect.height, Is.LessThan(sevenHeight));
        }
        [UnityTest] public IEnumerator CriticalStateAndDestroyedPartRemainReadable()
        {
            battle.Player.Health.ConfigureDestructionChance(0); input.SelectUnit(battle.Player);
            foreach (var part in new[] { TargetBodyPart.Head, TargetBodyPart.LeftArm, TargetBodyPart.RightArm }) battle.Player.Health.ApplyDamage(part, int.MaxValue);
            yield return null;
            Assert.That(panel.IsVisible, Is.True); Assert.That(panel.AvailabilityText, Is.EqualTo("INACTIVE")); Assert.That(panel.StateText, Is.EqualTo("CRITICAL"));
            Assert.That(Rows.Single(row => row.PartId == "Head").HealthFraction, Is.Zero);
        }
        [UnityTest] public IEnumerator EnemyInspectionAndLostSightUseExistingVisibilityRules()
        {
            Assert.That(input.SelectUnit(battle.Enemy), Is.True); yield return null;
            Assert.That(panel.DisplayedUnit, Is.SameAs(battle.Enemy)); Assert.That(panel.StateText, Is.EqualTo("INSPECT ONLY")); Assert.That(input.CanChooseAction, Is.False);
            battle.Player.Vision.Configure(0); battle.Turns.Visibility.Refresh(); yield return null;
            Assert.That(panel.IsVisible, Is.False); Assert.That(panel.DisplayedUnit, Is.Null);
        }
        [UnityTest] public IEnumerator DebugToggleDoesNotClearSelectionAndRestoresPanel()
        {
            input.SelectUnit(battle.Player); yield return null; Assert.That(panel.IsVisible, Is.True); Assert.That(debug.CanEndTurn, Is.True);
            debug.ToggleDetailedDebug(); yield return null; Assert.That(panel.IsVisible, Is.False); Assert.That(input.SelectedUnit, Is.SameAs(battle.Player));
            debug.ToggleDetailedDebug(); yield return null; Assert.That(panel.IsVisible, Is.True);
        }
        [UnityTest] public IEnumerator PanelPointerDoesNotZoomCameraOrClearSelection()
        {
            input.SelectUnit(battle.Player); yield return null; Canvas.ForceUpdateCanvases();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, Frame.TransformPoint(Frame.rect.center));
            Assert.That(panel.ContainsScreenPoint(point), Is.True);
            float zoom = camera.orthographicSize;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, scroll = new Vector2(0, 120) }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
            Assert.That(camera.orthographicSize, Is.EqualTo(zoom)); Assert.That(input.SelectedUnit, Is.SameAs(battle.Player));
        }
        private sealed class ManyParts : IUnitStatusSource
        {
            public void GetStatusParts(List<UnitStatusPart> parts)
            { parts.Clear(); for (int index = 0; index < 20; index++) parts.Add(new UnitStatusPart(index.ToString(), "Part " + index, 80, 100)); }
        }
        [UnityTest] public IEnumerator LongStatusListScrollsInsideBoundedPanel()
        {
            panel.Initialize(null, battle.Turns); panel.ShowUnit(battle.Player, new ManyParts()); yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(Rows.Length, Is.EqualTo(20)); var scroll = panel.GetComponentInChildren<ScrollRect>();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height)); Assert.That(Frame.rect.height, Is.LessThan(690));
            var point = RectTransformUtility.WorldToScreenPoint(null, scroll.viewport.TransformPoint(scroll.viewport.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, scroll = new Vector2(0, -120) }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
            Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(1));
        }
        [UnityTest] public IEnumerator TurnBannerLocksStatusScrollingUntilPresentationEnds()
        {
            panel.Initialize(null, battle.Turns); panel.ShowUnit(battle.Player, new ManyParts()); yield return null; Canvas.ForceUpdateCanvases();
            var scroll = panel.GetComponentInChildren<ScrollRect>();
            var point = RectTransformUtility.WorldToScreenPoint(null, scroll.viewport.TransformPoint(scroll.viewport.rect.center));
            var banner = battle.Root.AddComponent<TurnBannerPresenter>(); banner.Initialize(battle.Turns);
            Assert.That(battle.Turns.IsPresentingTurn, Is.True); yield return null;
            float before = scroll.verticalNormalizedPosition;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, scroll = new Vector2(0, -120) }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(before).Within(.001f));
            yield return TurnBannerTestSupport.Finish(battle.Turns);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, scroll = new Vector2(0, -120) }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
            Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(before));
        }
    }
}
#endif
