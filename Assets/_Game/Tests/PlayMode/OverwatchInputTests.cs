using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class OverwatchInputTests
    {
        private OverwatchTestBattle battle;
        private PlayerInputController input;
        private UnitActionUI menu;
        private GridVisualizer visualizer;
        private Camera camera;
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
        private Transform Ranges => visualizer.transform.Find("Reachable Cells");

        [UnitySetUp]
        public IEnumerator Setup()
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
                var model = GameObject.CreatePrimitive(PrimitiveType.Capsule); model.name = "UI Test Model";
                model.transform.SetParent(unit.transform, false); Object.Destroy(model.GetComponent<Collider>());
            }
            visualizer = battle.Root.AddComponent<GridVisualizer>(); visualizer.Initialize(battle.Grid, battle.Turns);
            var debug = battle.Root.AddComponent<BattleDebugUI>(); debug.Initialize(battle.Grid, battle.Root.GetComponent<CoverSystem>());
            camera = new GameObject("Overwatch UI camera").AddComponent<Camera>(); camera.transform.SetParent(battle.Root.transform);
            camera.orthographic = true; camera.orthographicSize = 15;
            camera.transform.position = new Vector3(10, 28, -20); camera.transform.LookAt(new Vector3(10, 0, 10));
            input = battle.Root.AddComponent<PlayerInputController>(); input.Initialize(camera, battle.Grid, visualizer, debug);
            input.InitializeCombat(battle.Combat, battle.Turns); debug.InitializeCombat(input, battle.Turns);
            menu = battle.Root.GetComponent<UnitActionUI>();
            mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            Physics.SyncTransforms(); yield return null;
            Assert.That(input.SelectUnit(battle.Watcher), Is.True);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            bool capturedView = false;
            InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            InputSystem.settings.backgroundBehavior = oldBackground;
#if UNITY_EDITOR
            if (gameViewGroup != null)
            {
                capturedView = true;
                // Batch mode retains the last rendered dimensions after the Game View closes.
                // Render the original size before restoring its editor selection and closing it.
                yield return SetViewSize(oldWidth, oldHeight);
                sizeSelection.SetValue(gameView, oldSizeIndex);
                int count = (int)gameViewGroup.GetType().GetMethod("GetCustomCount").Invoke(gameViewGroup, null);
                for (int i = count - 1; i >= originalCustomCount; i--)
                    gameViewGroup.GetType().GetMethod("RemoveCustomSize").Invoke(gameViewGroup, new object[] { builtinCount + i });
                gameView.Close();
                gameViewGroup = null; gameView = null; sizeSelection = null;
            }
#endif
            Screen.SetResolution(oldWidth, oldHeight, oldScreenMode);
            battle.Dispose(); yield return null;
            if (capturedView)
            {
                Assert.That(Screen.width, Is.EqualTo(oldWidth), "Capture must restore the following tests' screen width.");
                Assert.That(Screen.height, Is.EqualTo(oldHeight), "Capture must restore the following tests' screen height.");
            }
        }
        private object Read(object owner, string property)
        {
            var info = owner.GetType().GetProperty(property); Assert.That(info, Is.Not.Null, property + " must be available.");
            return info.GetValue(owner);
        }
        private bool Call(string method, params object[] args)
        {
            var info = typeof(PlayerInputController).GetMethod(method); Assert.That(info, Is.Not.Null);
            return (bool)info.Invoke(input, args);
        }
        private Rect OverwatchButton => (Rect)Read(menu, "OverwatchButtonRect");
        private IEnumerator Click(Rect rect)
        {
            Assert.That(rect.width, Is.GreaterThan(0), "An enabled menu needs a clickable rect.");
            var point = new Vector2(rect.center.x, Screen.height - rect.center.y);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
        }
        private IEnumerator Escape()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }
        private IEnumerator Prepare()
        {
            yield return Click(OverwatchButton);
            Assert.That(input.ActionMode.ToString(), Is.EqualTo("OverwatchWeapon"));
            yield return Click(menu.GetWeaponButtonRect(WeaponSlot.RightHand));
            Assert.That(input.ActionMode.ToString(), Is.EqualTo("OverwatchFacing"));
        }
        private void AssertCoverage(FacingDirection facing)
        {
            var expected = battle.Overwatch.GetCoverageCells(battle.Watcher, WeaponSlot.RightHand, facing);
            Assert.That(Ranges.gameObject.activeSelf, Is.EqualTo(expected.Count > 0));
            if (expected.Count == 0) return;
            var actual = Ranges.Cast<Transform>().Select(child =>
            {
                Assert.That(battle.Grid.TryWorldToGrid(child.position, out var position), Is.True); return position;
            });
            Assert.That(actual, Is.EquivalentTo(expected.Select(cell => cell.Position)));
        }

        [UnityTest]
        public IEnumerator ClickFlowCanArmCostFiveWeaponWithOnlyTwoAP()
        {
            battle.Watcher.Combat.ConfigureTestRanged(5, 80, 7, 5); battle.Watcher.ActionPoints.TrySpend(3); yield return null;
            yield return Prepare();
            Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(battle.Root.GetComponent<BodyTargetUI>().IsVisible, Is.False);
            yield return Click(menu.GetFacingButtonRect(FacingDirection.East)); AssertCoverage(FacingDirection.East);
            yield return Click(menu.ConfirmFacingButtonRect);
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.True); Assert.That(battle.Watcher.Overwatch.Facing, Is.EqualTo(FacingDirection.East));
            Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.Zero);
            Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Menu)); Assert.That(Ranges.gameObject.activeSelf, Is.False);
        }
        [UnityTest]
        public IEnumerator SingleTwoHandWeaponIsExplicitAndMeleeIsFilteredOut()
        {
            var loadout = battle.Watcher.GetComponent<UnitWeaponLoadout>();
            JsonUtility.FromJsonOverwrite("{\"gripRequirement\":2}", loadout.RightWeapon);
            loadout.Configure(null, GripMode.OneHand, loadout.RightWeapon, GripMode.TwoHands);
            yield return Click(OverwatchButton);
            Assert.That(input.ActionMode.ToString(), Is.EqualTo("OverwatchWeapon"));
            Assert.That(Read(input, "SelectedOverwatchSlot"), Is.Null);
            Assert.That(menu.GetWeaponButtonRect(WeaponSlot.LeftHand).width, Is.Zero);
            Assert.That(menu.GetWeaponButtonText(WeaponSlot.RightHand), Does.Contain("Both hands"));
            yield return Click(menu.GetWeaponButtonRect(WeaponSlot.RightHand));
            Assert.That(Read(input, "SelectedOverwatchSlot"), Is.EqualTo(WeaponSlot.RightHand));
            Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator MixedLoadoutOnlyListsItsRangedWeapon()
        {
            yield return Click(OverwatchButton);
            Assert.That(menu.GetWeaponButtonRect(WeaponSlot.LeftHand).width, Is.Zero);
            Assert.That(menu.GetWeaponButtonRect(WeaponSlot.RightHand).width, Is.GreaterThan(0));
            Assert.That(Call("BeginOverwatch", WeaponSlot.LeftHand), Is.False);
        }
        [UnityTest]
        public IEnumerator MeleeOnlyUnitCannotOpenOverwatch()
        {
            var loadout = battle.Watcher.GetComponent<UnitWeaponLoadout>();
            loadout.Configure(loadout.LeftWeapon, GripMode.OneHand, null, GripMode.OneHand); yield return null;
            Assert.That(Read(input, "CanOpenOverwatchMenu"), Is.False);
            yield return Click(OverwatchButton);
            Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Menu)); Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator InsufficientAPCannotOpenOrConfirmOverwatch()
        {
            battle.Watcher.ActionPoints.TrySpend(4); yield return null;
            Assert.That(Read(input, "CanOpenOverwatchMenu"), Is.False);
            Assert.That(Call("TryConfirmOverwatch"), Is.False);
            yield return Click(OverwatchButton);
            Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Menu)); Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator DirectionAndVisionUpdatePreviewWithoutSpendingAP()
        {
            yield return Prepare(); AssertCoverage(FacingDirection.North);
            yield return Click(menu.GetFacingButtonRect(FacingDirection.West)); AssertCoverage(FacingDirection.West);
            battle.Watcher.Vision.Configure(0); yield return null;
            Assert.That(Ranges.gameObject.activeSelf, Is.False);
            Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(5)); Assert.That(battle.Watcher.Overwatch.IsActive, Is.False);
        }
        [UnityTest]
        public IEnumerator EscapeCancelsPreparationWithoutSpendingAP()
        {
            yield return Prepare(); yield return Escape();
            Assert.That(input.SelectedUnit, Is.Null); Assert.That(Read(input, "SelectedOverwatchSlot"), Is.Null);
            Assert.That(Ranges.gameObject.activeSelf, Is.False); Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.False);
        }
        [UnityTest]
        public IEnumerator ConfirmedGuardSurvivesEscapeAndStillAllowsEjection()
        {
            yield return Prepare(); yield return Click(menu.ConfirmFacingButtonRect); yield return Escape();
            Assert.That(input.SelectedUnit, Is.Null); Assert.That(battle.Watcher.Overwatch.IsActive, Is.True);
            Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(3));
            Assert.That(input.SelectUnit(battle.Watcher), Is.True);
            Assert.That(Read(menu, "UnitStateText"), Does.Contain("OVERWATCH"));
            Assert.That(input.CanBeginMove, Is.False); Assert.That(input.CanOpenAttackMenu, Is.False); Assert.That(input.CanEjectSelected, Is.True);
            yield return Click(menu.EjectButtonRect);
            Assert.That(battle.Watcher.Health.PilotEjected, Is.True); Assert.That(battle.Watcher.Overwatch.IsActive, Is.False);
        }
        [UnityTest]
        public IEnumerator ChangingSelectionClearsUnconfirmedSetup()
        {
            yield return Prepare(); Assert.That(input.SelectUnit(battle.Second), Is.True); yield return null;
            Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Menu)); Assert.That(Read(input, "SelectedOverwatchSlot"), Is.Null);
            Assert.That(Ranges.gameObject.activeSelf, Is.False); Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator ChangingTurnClearsUnconfirmedSetup()
        {
            yield return Prepare(); battle.StartTeam(UnitTeam.Enemy); yield return null;
            Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Menu)); Assert.That(Read(input, "SelectedOverwatchSlot"), Is.Null);
            Assert.That(Ranges.gameObject.activeSelf, Is.False); Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator WeaponReplacementInvalidatesPreparedChoice()
        {
            yield return Prepare(); battle.Watcher.Combat.ConfigureTestRanged(5, 80, 7, 3); yield return null;
            Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Menu)); Assert.That(Read(input, "SelectedOverwatchSlot"), Is.Null);
            Assert.That(Ranges.gameObject.activeSelf, Is.False); Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator OtherActionClearsOverwatchSetupAndUsesDistinctRangeMaterial()
        {
            Assert.That(input.BeginAttack(), Is.True);
            var attackMaterial = Ranges.GetChild(0).GetComponent<Renderer>().sharedMaterial;
            Assert.That(Call("OpenOverwatchMenu"), Is.True);
            yield return Click(menu.GetWeaponButtonRect(WeaponSlot.RightHand));
            Assert.That(Ranges.GetChild(0).GetComponent<Renderer>().sharedMaterial, Is.Not.SameAs(attackMaterial));
            Assert.That(input.BeginMove(), Is.True); yield return null;
            Assert.That(Read(input, "SelectedOverwatchSlot"), Is.Null);
            Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Move)); Assert.That(battle.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(5));
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
        [UnityTest]
        public IEnumerator PanelsFitAndCaptureWeaponDirectionAndActiveState()
        {
            yield return SetViewSize(1280, 720);
            yield return Click(OverwatchButton); yield return Capture("overwatch-weapon-menu.png");
            yield return Click(menu.GetWeaponButtonRect(WeaponSlot.RightHand));
            Assert.That(menu.TryGetPanelRect(out var panel), Is.True);
            Assert.That(panel.xMin, Is.GreaterThanOrEqualTo(0)); Assert.That(panel.xMax, Is.LessThanOrEqualTo(Screen.width));
            Assert.That(panel.yMin, Is.GreaterThanOrEqualTo(0)); Assert.That(panel.yMax, Is.LessThanOrEqualTo(Screen.height));
            yield return Capture("overwatch-direction-preview.png");
            yield return Click(menu.ConfirmFacingButtonRect); yield return Capture("overwatch-active.png");
            Assert.That(battle.Watcher.Overwatch.IsActive, Is.True);
            yield return SetViewSize(640, 360);
            Assert.That(menu.TryGetPanelRect(out panel), Is.True);
            Assert.That(panel.xMin, Is.GreaterThanOrEqualTo(0)); Assert.That(panel.xMax, Is.LessThanOrEqualTo(Screen.width));
            Assert.That(panel.yMin, Is.GreaterThanOrEqualTo(0)); Assert.That(panel.yMax, Is.LessThanOrEqualTo(Screen.height));
            yield return Capture("overwatch-active-small.png");
        }
    }
}
