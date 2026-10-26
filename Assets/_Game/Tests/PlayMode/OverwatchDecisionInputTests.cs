using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class OverwatchDecisionInputTests
    {
        private OverwatchTestBattle battle;
        private PlayerInputController input;
        private BodyTargetUI body;
        private UnitActionUI menu;
        private Camera camera;
        private Component controller;
        private Mouse mouse;
        private Keyboard keyboard;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private InputSettings.BackgroundBehavior oldBackground;
        private int oldWidth, oldHeight;
        private FullScreenMode oldScreenMode;
#if UNITY_EDITOR
        private UnityEditor.EditorWindow gameView;
        private object gameViewGroup;
        private PropertyInfo sizeSelection;
        private int oldSizeIndex, originalCustomCount, builtinCount;
#endif

        [UnitySetUp] public IEnumerator Setup()
        {
            oldWidth = Screen.width; oldHeight = Screen.height; oldScreenMode = Screen.fullScreenMode;
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            battle = new OverwatchTestBattle();
            foreach (var unit in new[] { battle.Watcher, battle.Second, battle.Mover, battle.Spare })
            {
                unit.gameObject.AddComponent<CapsuleCollider>();
                var model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                model.transform.SetParent(unit.transform, false); Object.Destroy(model.GetComponent<Collider>());
            }
            var visualizer = battle.Root.AddComponent<GridVisualizer>(); visualizer.Initialize(battle.Grid, battle.Turns);
            var debug = battle.Root.AddComponent<BattleDebugUI>(); debug.Initialize(battle.Grid, battle.Root.GetComponent<CoverSystem>());
            camera = new GameObject("Reaction UI Camera").AddComponent<Camera>(); camera.transform.SetParent(battle.Root.transform);
            camera.orthographic = true; camera.orthographicSize = 15;
            camera.transform.position = new Vector3(10, 28, -20); camera.transform.LookAt(new Vector3(10, 0, 10));
            camera.gameObject.AddComponent<BattleCameraOrbit>().Initialize(battle.Grid);
            input = battle.Root.AddComponent<PlayerInputController>(); input.Initialize(camera, battle.Grid, visualizer, debug);
            input.InitializeCombat(battle.Combat, battle.Turns); debug.InitializeCombat(input, battle.Turns);
            body = battle.Root.GetComponent<BodyTargetUI>(); menu = battle.Root.GetComponent<UnitActionUI>();
            mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            var type = typeof(BattleUnit).Assembly.GetType("MechaXCOM.OverwatchDecisionController");
            Assert.That(type, Is.Not.Null, "Player decisions need a separate UI controller.");
            controller = battle.Root.AddComponent(type);
            type.GetMethod("Initialize").Invoke(controller, new object[] { battle.Overwatch, input, body });
            Physics.SyncTransforms(); yield return null;
            Assert.That(input.SelectUnit(battle.Watcher), Is.True);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            bool capturedView = false;
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            InputSystem.settings.backgroundBehavior = oldBackground;
#if UNITY_EDITOR
            if (gameViewGroup != null)
            {
                capturedView = true;
                yield return SetViewSize(oldWidth, oldHeight);
                sizeSelection.SetValue(gameView, oldSizeIndex);
                int count = (int)gameViewGroup.GetType().GetMethod("GetCustomCount").Invoke(gameViewGroup, null);
                for (int i = count - 1; i >= originalCustomCount; i--)
                    gameViewGroup.GetType().GetMethod("RemoveCustomSize").Invoke(gameViewGroup, new object[] { builtinCount + i });
                gameView.Close(); gameViewGroup = null; gameView = null; sizeSelection = null;
            }
#endif
            Screen.SetResolution(oldWidth, oldHeight, oldScreenMode);
            battle?.Dispose(); yield return null;
            if (capturedView)
            {
                Assert.That(Screen.width, Is.EqualTo(oldWidth));
                Assert.That(Screen.height, Is.EqualTo(oldHeight));
            }
        }
        private T Read<T>(string name) => (T)controller.GetType().GetProperty(name).GetValue(controller);
        private Rect SkipRect
        {
            get
            {
                var property = typeof(BodyTargetUI).GetProperty("SkipButtonRect");
                Assert.That(property, Is.Not.Null, "Reaction UI must offer Skip.");
                return (Rect)property.GetValue(body);
            }
        }
        private IEnumerator Click(Rect rect)
        {
            Assert.That(rect.width, Is.GreaterThan(0));
            yield return ClickPoint(new Vector2(rect.center.x, Screen.height - rect.center.y));
        }
        private IEnumerator ClickPoint(Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
        }
        private IEnumerator Begin(bool second = false)
        {
            battle.Arm(); if (second) battle.Arm(battle.Second);
            battle.StartTeam(UnitTeam.Enemy);
            Assert.That(battle.Mover.Movement.TryMove(new Vector2Int(3, 2)), Is.True);
            yield return battle.WaitForDecision(); yield return null;
            Assert.That(Read<bool>("IsPending"), Is.True);
        }
        private IEnumerator Finish()
        {
            float deadline = Time.realtimeSinceStartup + 4;
            while (battle.Mover.Movement.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(battle.Mover.Movement.IsMoving, Is.False);
            Assert.That(battle.Overwatch.PendingDecision.HasValue, Is.False);
        }

        [UnityTest] public IEnumerator EnemyTurnBodyPanelShowsShooterTargetWeaponAndDefaultBody()
        {
            yield return Begin();
            Assert.That(body.IsVisible, Is.True); Assert.That(menu.IsVisible, Is.False);
            Assert.That(Read<TargetBodyPart>("SelectedPart"), Is.EqualTo(TargetBodyPart.Torso));
            Assert.That(Read<AttackPreview>("CurrentPreview").HitChance, Is.EqualTo(60));
            var header = typeof(BodyTargetUI).GetProperty("ContextInfoText"); Assert.That(header, Is.Not.Null);
            Assert.That((string)header.GetValue(body), Does.Contain(battle.Watcher.name).And.Contain(battle.Mover.name));
            Assert.That(body.WeaponInfoText, Is.EqualTo(battle.Watcher.GetComponent<UnitWeaponLoadout>().RightWeapon.DisplayName));
            Assert.That(body.AttackInfoText, Does.Contain("AP 0"));
            Assert.That(body.CurrentTexture, Is.Not.Null);
            Assert.That(input.SelectedUnit, Is.SameAs(battle.Watcher));
            Assert.That(input.TargetUnit, Is.Null);
            yield return Click(SkipRect); yield return Finish();
        }

        [UnityTest] public IEnumerator ClickHeadUpdatesReactionChanceAndFireHitsHead()
        {
            yield return Begin(); yield return Click(body.GetPartRect(TargetBodyPart.Head));
            Assert.That(Read<TargetBodyPart>("SelectedPart"), Is.EqualTo(TargetBodyPart.Head));
            Assert.That(body.SelectedPartInfo, Does.Contain("48%"));
            yield return Click(body.FireButtonRect); yield return Finish();
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(1));
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(3));
            Assert.That(body.IsVisible, Is.False);
        }

        [UnityTest] public IEnumerator CriticalReactionIsRecordedForVisibleAttackFeedback()
        {
            battle.Combat.SetCriticalRollSource(() => 0f);
            yield return Begin();
            var result = (AttackResult)controller.GetType().GetMethod("TryFire").Invoke(controller, null);
            Assert.That(result.IsCritical, Is.True);
            Assert.That(input.LastAttackResult.WasExecuted, Is.True);
            Assert.That(input.LastAttackResult.IsCritical, Is.True);
            Assert.That(input.LastAttackResult.DamageDealt, Is.EqualTo(6));
            yield return Finish();
        }

        [UnityTest] public IEnumerator SkipDoesNotSpendAPAndCannotRepeatSameEnemy()
        {
            int rolls = 0; battle.Combat.SetRollSource(() => { rolls++; return 0; });
            yield return Begin(); yield return Click(SkipRect); yield return Finish();
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(battle.Mover.Mobility.MovedCells, Is.EqualTo(2));
            Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(3));
            Assert.That(rolls, Is.Zero); Assert.That(battle.Watcher.Overwatch.IsActive, Is.True);
        }

        [UnityTest] public IEnumerator EscapeAndWorldClicksKeepPending()
        {
            yield return Begin(); long id = battle.Overwatch.PendingDecision.Value.Id;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            yield return ClickPoint(Vector2.zero);
            yield return ClickPoint(camera.WorldToScreenPoint(battle.Second.transform.position));
            Assert.That(battle.Overwatch.PendingDecision.Value.Id, Is.EqualTo(id));
            Assert.That(input.SelectedUnit, Is.SameAs(battle.Watcher));
            Assert.That(input.SelectUnit(battle.Second) || input.BeginMove() || input.OpenAttackMenu() ||
                input.TryEjectSelected() || battle.Turns.TryEndTurn(), Is.False);
            yield return Click(SkipRect); yield return Finish();
        }

        [UnityTest] public IEnumerator CameraMovesDuringDecision()
        {
            yield return Begin(); long id = battle.Overwatch.PendingDecision.Value.Id;
            var position = camera.transform.position; var rotation = camera.transform.rotation;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(Vector3.Distance(camera.transform.position, position), Is.GreaterThan(0));
            float size = camera.orthographicSize;
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, 120) }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
            Assert.That(camera.orthographicSize, Is.LessThan(size));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSecondsRealtime(.6f);
            Assert.That(Quaternion.Angle(camera.transform.rotation, rotation), Is.GreaterThan(0));
            Assert.That(battle.Overwatch.PendingDecision.Value.Id, Is.EqualTo(id));
            yield return Click(SkipRect); yield return Finish();
        }

        [UnityTest] public IEnumerator NewTargetResetsPartToBody()
        {
            yield return Begin(true); yield return Click(body.GetPartRect(TargetBodyPart.Head));
            Assert.That(Read<TargetBodyPart>("SelectedPart"), Is.EqualTo(TargetBodyPart.Head));
            yield return Click(SkipRect); yield return battle.WaitForDecision(); yield return null;
            Assert.That(Read<OverwatchDecision?>("CurrentDecision").Value.Watcher, Is.SameAs(battle.Second));
            Assert.That(Read<TargetBodyPart>("SelectedPart"), Is.EqualTo(TargetBodyPart.Torso));
            Assert.That(body.SelectedPartInfo, Does.Contain("60%"));
            yield return new WaitForSecondsRealtime(.35f);
            yield return Click(SkipRect); yield return Finish();
        }

        [UnityTest] public IEnumerator OrdinaryAttackStillNeedsOwnTurn()
        {
            yield return Begin();
            Assert.That(battle.Combat.TryAttack(battle.Watcher, battle.Mover).WasExecuted, Is.False);
            Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand), Is.False);
            Assert.That(body.IsVisible, Is.True);
            yield return Click(SkipRect); yield return Finish();
        }

        [UnityTest] public IEnumerator NormalAttackPanelStillVisibleAlongsideBodyUI()
        {
            Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand), Is.True);
            Assert.That(input.TrySelectTarget(battle.Mover), Is.True);
            yield return null;
            Assert.That(body.IsVisible, Is.True);
            Assert.That(menu.IsVisible, Is.True, "Only the reaction should hide the ordinary action panel.");
            Assert.That(SkipRect.width, Is.Zero);
        }

        [UnityTest] public IEnumerator HoldingFireDoesNotShootNextWatcherWithoutNewPress()
        {
            yield return Begin(true);
            var point = new Vector2(body.FireButtonRect.center.x, Screen.height - body.FireButtonRect.center.y);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
            yield return null; yield return null; yield return null;
            Assert.That(battle.Overwatch.PendingDecision.HasValue, Is.True);
            Assert.That(battle.Overwatch.PendingDecision.Value.Watcher, Is.SameAs(battle.Second));
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
            yield return Click(SkipRect); yield return Finish();
        }

        [UnityTest] public IEnumerator DoubleClickFireDoesNotShootNextWatcher() => DoubleClickDecision(false);
        [UnityTest] public IEnumerator DoubleClickSkipDoesNotSkipNextWatcher() => DoubleClickDecision(true);
        private IEnumerator DoubleClickDecision(bool skip)
        {
            const float interval = .5f;
            typeof(PlayerInputController).GetField("doubleClickInterval", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(input, interval);
            int rolls = 0; battle.Combat.SetRollSource(() => { rolls++; return 0; });
            yield return Begin(true);
            var rect = skip ? SkipRect : body.FireButtonRect;
            float firstClick = Time.unscaledTime;
            yield return Click(rect);
            yield return battle.WaitForDecision(); yield return null;
            var second = battle.Overwatch.PendingDecision.Value;
            Assert.That(second.Watcher, Is.SameAs(battle.Second));
            Assert.That(Time.unscaledTime - firstClick, Is.LessThan(interval), "Reproduce a real double-click interval.");
            yield return Click(rect);
            Assert.That(battle.Overwatch.PendingDecision.HasValue, Is.True, "A double-click on the old dialog must not resolve the next watcher.");
            Assert.That(battle.Overwatch.PendingDecision.Value.Id, Is.EqualTo(second.Id));
            Assert.That(rolls, Is.EqualTo(skip ? 0 : 1));
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(skip ? 20 : 15));
            yield return new WaitForSecondsRealtime(interval + .05f);
            yield return Click(rect); yield return Finish();
            Assert.That(rolls, Is.EqualTo(skip ? 0 : 2), "A separate deliberate click must still work.");
        }
        [UnityTest] public IEnumerator ReinitializingControllerPreservesCurrentRequestAndPart()
        {
            yield return Begin(); yield return Click(body.GetPartRect(TargetBodyPart.Head));
            long id = battle.Overwatch.PendingDecision.Value.Id;
            controller.GetType().GetMethod("Initialize").Invoke(controller, new object[] { battle.Overwatch, input, body });
            Assert.That(battle.Overwatch.PendingDecision.Value.Id, Is.EqualTo(id));
            Assert.That(Read<TargetBodyPart>("SelectedPart"), Is.EqualTo(TargetBodyPart.Head));
            Assert.That(battle.Root.GetComponents(controller.GetType()).Length, Is.EqualTo(1));
            yield return Click(SkipRect); yield return Finish();
        }

        [UnityTest] public IEnumerator ReactionPanelsFitAndCaptureAtTwoResolutions()
        {
            yield return SetViewSize(1280, 720); yield return Begin();
            AssertPanelFits();
            yield return Click(body.GetPartRect(TargetBodyPart.Head));
            Assert.That(Read<TargetBodyPart>("SelectedPart"), Is.EqualTo(TargetBodyPart.Head));
            yield return Capture("interactive-overwatch-targeting.png");
            yield return SetViewSize(640, 360); AssertPanelFits();
            yield return Click(body.GetPartRect(TargetBodyPart.Torso));
            Assert.That(Read<TargetBodyPart>("SelectedPart"), Is.EqualTo(TargetBodyPart.Torso));
            yield return Click(body.GetPartRect(TargetBodyPart.Head));
            Assert.That(Read<TargetBodyPart>("SelectedPart"), Is.EqualTo(TargetBodyPart.Head));
            yield return Capture("interactive-overwatch-targeting-small.png");
            yield return Click(SkipRect); yield return Finish();
        }
        private void AssertPanelFits()
        {
            Assert.That(body.PanelRect.xMin, Is.GreaterThanOrEqualTo(0));
            Assert.That(body.PanelRect.yMin, Is.GreaterThanOrEqualTo(0));
            Assert.That(body.PanelRect.xMax, Is.LessThanOrEqualTo(Screen.width));
            Assert.That(body.PanelRect.yMax, Is.LessThanOrEqualTo(Screen.height));
            Assert.That(body.PanelRect.Contains(body.FireButtonRect.center), Is.True);
            Assert.That(body.PanelRect.Contains(SkipRect.center), Is.True);
            Assert.That(body.FireButtonRect.Overlaps(SkipRect), Is.False);
            Assert.That(menu.IsVisible, Is.False);
        }

        [UnityTest] public IEnumerator DisablingViewReleasesDecision() => DisableAndResume("view");
        [UnityTest] public IEnumerator DisablingInputReleasesDecision() => DisableAndResume("input");
        [UnityTest] public IEnumerator DisablingControllerReleasesDecision() => DisableAndResume("controller");
        private IEnumerator DisableAndResume(string component)
        {
            yield return Begin(); long id = battle.Overwatch.PendingDecision.Value.Id;
            var behaviour = component == "view" ? (Behaviour)body : component == "input" ? input : (Behaviour)controller;
            behaviour.enabled = false;
            yield return Finish();
            Assert.That(battle.Overwatch.TryFire(id, TargetBodyPart.Torso).WasExecuted, Is.False);
            Assert.That(battle.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            behaviour.enabled = true; yield return null;
            Assert.That(Read<bool>("IsReady"), Is.True);
            Assert.That(Read<bool>("IsPending"), Is.False);
        }
        private IEnumerator Capture(string name)
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts")); Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, name);
            if (File.Exists(path)) File.Delete(path);
            yield return null; ScreenCapture.CaptureScreenshot(path);
            float deadline = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(File.Exists(path), Is.True);
        }
        private IEnumerator SetViewSize(int width, int height)
        {
#if UNITY_EDITOR
            var assembly = typeof(UnityEditor.Editor).Assembly;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            if (gameViewGroup == null)
            {
                var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
                var singleton = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType);
                var sizes = singleton.GetProperty("instance").GetValue(null);
                var groupType = sizesType.GetProperty("currentGroupType", flags).GetValue(sizes);
                gameViewGroup = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
                var viewType = assembly.GetType("UnityEditor.GameView");
                gameView = UnityEditor.EditorWindow.GetWindow(viewType);
                sizeSelection = viewType.GetProperty("selectedSizeIndex", flags);
                oldSizeIndex = (int)sizeSelection.GetValue(gameView);
                builtinCount = (int)gameViewGroup.GetType().GetMethod("GetBuiltinCount").Invoke(gameViewGroup, null);
                originalCustomCount = (int)gameViewGroup.GetType().GetMethod("GetCustomCount").Invoke(gameViewGroup, null);
            }
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var modeType = assembly.GetType("UnityEditor.GameViewSizeType");
            var entry = System.Activator.CreateInstance(sizeType,
                new object[] { System.Enum.Parse(modeType, "FixedResolution"), width, height, "Overwatch UI test" });
            gameViewGroup.GetType().GetMethod("AddCustomSize").Invoke(gameViewGroup, new[] { entry });
            int customCount = (int)gameViewGroup.GetType().GetMethod("GetCustomCount").Invoke(gameViewGroup, null);
            sizeSelection.SetValue(gameView, builtinCount + customCount - 1); gameView.Repaint();
#else
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
#endif
            float deadline = Time.realtimeSinceStartup + 5;
            while ((Screen.width != width || Screen.height != height) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Screen.width, Is.EqualTo(width)); Assert.That(Screen.height, Is.EqualTo(height));
        }
    }
}
