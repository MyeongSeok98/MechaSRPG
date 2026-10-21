using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
#endif

namespace MechaXCOM.Tests
{
    public class WeaponInputTests
    {
        private EnemyAITestBattle battle;
        private PlayerInputController input;
        private UnitActionUI menu;
        private BodyTargetUI body;
        private UnitWeaponLoadout loadout;
        private Camera camera;
        private Mouse mouse;
        private Keyboard keyboard;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private InputSettings.BackgroundBehavior oldBackground;
        private readonly List<WeaponData> weapons = new List<WeaponData>();
        private int damageRolls;
        private BattleUnit Player => battle.Players[0];
        private BattleUnit Enemy => battle.Enemies[0];
        [UnitySetUp] public IEnumerator Setup()
        {
            damageRolls = 0;
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            battle = new EnemyAITestBattle(new[] { new Vector2Int(3, 3), new Vector2Int(3, 5) }, new[] { new Vector2Int(6, 3) });
            foreach (var unit in battle.Players.Concat(battle.Enemies)) unit.gameObject.AddComponent<CapsuleCollider>();
            var visualizer = battle.Root.AddComponent<GridVisualizer>(); visualizer.Initialize(battle.Grid);
            var debug = battle.Root.AddComponent<BattleDebugUI>(); debug.Initialize(battle.Grid, battle.Root.GetComponent<CoverSystem>());
            camera = new GameObject("Weapon UI camera").AddComponent<Camera>(); camera.transform.SetParent(battle.Root.transform);
            camera.orthographic = true; camera.orthographicSize = 15;
            camera.transform.position = new Vector3(10, 28, -20); camera.transform.LookAt(new Vector3(10, 0, 10));
            input = battle.Root.AddComponent<PlayerInputController>(); input.Initialize(camera, battle.Grid, visualizer, debug);
            input.InitializeCombat(battle.Combat, battle.Turns); debug.InitializeCombat(input, battle.Turns);
            menu = battle.Root.GetComponent<UnitActionUI>(); body = battle.Root.GetComponent<BodyTargetUI>();
            loadout = Player.gameObject.EnsureTestLoadout();
            loadout.Configure(Weapon("Left Rifle", 1, 2, 5, 1), GripMode.OneHand, Weapon("Right Rifle", 4, 6, 7, 3), GripMode.OneHand);
            battle.Combat.SetDamageRollSource((min, max) => { damageRolls++; return max; });
            mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            Physics.SyncTransforms(); yield return null;
            Assert.That(input.SelectUnit(Player), Is.True);
        }
        private WeaponData Weapon(string name, int min, int max, int range, int cost, AttackType type = AttackType.Ranged)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponData>(); weapons.Add(weapon);
            JsonUtility.FromJsonOverwrite($"{{\"displayName\":\"{name}\",\"minDamage\":{min},\"maxDamage\":{max},\"range\":{range},\"apCost\":{cost},\"attackType\":{(int)type}}}", weapon);
            return weapon;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            InputSystem.settings.backgroundBehavior = oldBackground;
            battle.Dispose(); foreach (var weapon in weapons) Object.Destroy(weapon); weapons.Clear(); yield return null;
        }
        private IEnumerator Click(Rect rect)
        {
            Assert.That(rect.width, Is.GreaterThan(0));
            var point = new Vector2(rect.center.x, Screen.height - rect.center.y);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
        }
        private IEnumerator OpenRangedList()
        {
            yield return Click(menu.AttackButtonRect);
            Assert.That(input.IsWeaponMenuOpen, Is.True);
            Assert.That(input.SelectedWeaponSlot, Is.Null);
            Assert.That(input.TrySelectTarget(Enemy), Is.False);
        }
        [UnityTest] public IEnumerator SameTypeWeaponsUseClickedHand()
        {
            yield return OpenRangedList();
            Assert.That(menu.GetWeaponButtonText(WeaponSlot.LeftHand), Does.Contain("Left").And.Contain("Left Rifle"));
            Assert.That(menu.GetWeaponButtonText(WeaponSlot.RightHand), Does.Contain("Right").And.Contain("Right Rifle"));
            yield return Click(menu.GetWeaponButtonRect(WeaponSlot.LeftHand));
            Assert.That(input.SelectedWeaponSlot, Is.EqualTo(WeaponSlot.LeftHand));
            Assert.That(input.SelectedUnit, Is.SameAs(Player));
            Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Attack));
            Assert.That(input.TrySelectTarget(Enemy), Is.True);
            Assert.That(input.CurrentAttackPreview.MaxDamage, Is.EqualTo(2));
            Assert.That(damageRolls, Is.Zero);
        }
        [UnityTest] public IEnumerator SameAssetTwoCopiesRemainSeparatelySelectable()
        {
            loadout.Configure(loadout.RightWeapon, GripMode.OneHand, loadout.RightWeapon, GripMode.OneHand);
            yield return OpenRangedList(); yield return Click(menu.GetWeaponButtonRect(WeaponSlot.RightHand));
            Assert.That(input.SelectedWeaponSlot, Is.EqualTo(WeaponSlot.RightHand));
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator SingleTwoHandWeaponStillRequiresExplicitSelection()
        {
            loadout.Configure(null, GripMode.OneHand, loadout.RightWeapon, GripMode.TwoHands);
            Assert.That(input.CanBeginMelee, Is.False); Assert.That(input.BeginMelee(), Is.False);
            yield return Click(menu.AttackButtonRect);
            Assert.That(input.IsWeaponMenuOpen, Is.True);
            Assert.That(input.SelectedWeaponSlot, Is.Null);
            Assert.That(menu.GetWeaponButtonRect(WeaponSlot.LeftHand).width, Is.Zero);
            Assert.That(menu.GetWeaponButtonText(WeaponSlot.RightHand), Does.Contain("Both hands").And.Contain("Right Rifle"));
            yield return Click(menu.GetWeaponButtonRect(WeaponSlot.RightHand));
            Assert.That(input.IsWeaponMenuOpen, Is.False);
            Assert.That(input.SelectedWeaponSlot, Is.EqualTo(WeaponSlot.RightHand));
            Assert.That(menu.IsVisible, Is.False, "Target selection must leave the map clear.");
        }
        [UnityTest] public IEnumerator AttackShowsMixedWeaponsAndMeleeOnlyLoadoutWithoutTypeStep()
        {
            var blade = Weapon("Axe", 8, 8, 2, 3, AttackType.Melee);
            loadout.Configure(blade, GripMode.OneHand, loadout.RightWeapon, GripMode.OneHand);
            yield return Click(menu.AttackButtonRect);
            Assert.That(input.IsWeaponMenuOpen, Is.True);
            Assert.That(menu.GetWeaponButtonRect(WeaponSlot.LeftHand).width, Is.GreaterThan(0));
            Assert.That(menu.GetWeaponButtonRect(WeaponSlot.RightHand).width, Is.GreaterThan(0));
            Assert.That(menu.GetWeaponButtonText(WeaponSlot.LeftHand), Does.Contain("Axe"));
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(input.TrySelectTarget(Enemy), Is.False);
            yield return Click(menu.GetWeaponButtonRect(WeaponSlot.LeftHand));
            Assert.That(input.SelectedAttackType, Is.EqualTo(AttackType.Melee));
            Assert.That(input.SelectedWeaponSlot, Is.EqualTo(WeaponSlot.LeftHand));
            loadout.Configure(blade, GripMode.OneHand, null, GripMode.OneHand);
            input.SelectUnit(Player);
            yield return Click(menu.AttackButtonRect);
            yield return null;
            Assert.That(input.IsWeaponMenuOpen, Is.True, "A melee-only list must stay open across Update.");
            Assert.That(menu.GetWeaponButtonRect(WeaponSlot.RightHand).width, Is.Zero);
            yield return Click(menu.GetWeaponButtonRect(WeaponSlot.LeftHand));
            Assert.That(input.SelectedAttackType, Is.EqualTo(AttackType.Melee));
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator ChangingWeaponClearsTargetPartAndResult()
        {
            Assert.That(input.BeginWeaponAttack(WeaponSlot.LeftHand), Is.True); input.TrySelectTarget(Enemy);
            Assert.That(input.TryAttackSelected().WasExecuted, Is.True);
            Assert.That(input.LastAttackResult.WasExecuted, Is.True);
            input.BeginWeaponAttack(WeaponSlot.LeftHand); input.TrySelectTarget(Enemy); input.TrySelectBodyPart(TargetBodyPart.Head);
            Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand), Is.True);
            Assert.That(input.TargetUnit, Is.Null); Assert.That(input.SelectedBodyPart, Is.EqualTo(TargetBodyPart.Whole));
            Assert.That(input.LastAttackResult.WasExecuted, Is.False);
            Assert.That(input.InputMessage, Is.Null); yield return null;
        }
        [UnityTest] public IEnumerator LeftStoredTwoHandWeaponHasOnlyOneButton()
        {
            loadout.Configure(loadout.LeftWeapon, GripMode.TwoHands, null, GripMode.OneHand);
            yield return Click(menu.AttackButtonRect);
            Assert.That(menu.GetWeaponButtonRect(WeaponSlot.RightHand).width, Is.Zero);
            Assert.That(menu.GetWeaponButtonText(WeaponSlot.LeftHand), Does.Contain("Both hands").And.Contain("Left Rifle"));
            yield return Click(menu.GetWeaponButtonRect(WeaponSlot.LeftHand));
            Assert.That(input.SelectedWeaponSlot, Is.EqualTo(WeaponSlot.LeftHand));
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        private IEnumerator Fire(bool bodyButton)
        {
            yield return OpenRangedList(); yield return Click(menu.GetWeaponButtonRect(WeaponSlot.LeftHand));
            Assert.That(input.TrySelectTarget(Enemy), Is.True);
            Assert.That(body.IsVisible, Is.True);
            Assert.That(menu.WeaponInfoText, Does.Contain("Left Rifle"));
            Assert.That(menu.AttackInfoText, Does.Contain("80%").And.Contain("1-2"));
            Assert.That(body.WeaponInfoText, Does.Contain("Left Rifle"));
            Assert.That(body.AttackInfoText, Does.Contain("1-2").And.Contain("AP 1"));
            Assert.That(menu.TryGetPanelRect(out var panel), Is.True); Assert.That(panel.Overlaps(body.PanelRect), Is.False);
            yield return Click(bodyButton ? body.FireButtonRect : menu.FireButtonRect);
            Assert.That(input.LastAttackResult.DamageDealt, Is.EqualTo(2)); Assert.That(damageRolls, Is.EqualTo(1));
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(18));
        }
        [UnityTest] public IEnumerator ActionPanelFireUsesSelectedSlot() { yield return Fire(false); }
        [UnityTest] public IEnumerator BodyPanelFireUsesSelectedSlot() { yield return Fire(true); }
        [UnityTest] public IEnumerator RangeAndFireCannotUseOtherHandAsFallback()
        {
            var shortRifle = Weapon("Short Rifle", 1, 2, 2, 1);
            loadout.Configure(shortRifle, GripMode.OneHand, loadout.RightWeapon, GripMode.OneHand);
            input.BeginWeaponAttack(WeaponSlot.LeftHand); input.TrySelectTarget(Enemy);
            var ranges = battle.Root.transform.Find("Reachable Cells");
            Assert.That(ranges.childCount, Is.EqualTo(11), "Range 2 has 12 cells, excluding the other friendly unit's cell.");
            Assert.That(input.CurrentAttackPreview.CanAttack, Is.False);
            Assert.That(input.TryAttackSelected().WasExecuted, Is.False);
            Assert.That(damageRolls, Is.Zero); Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            input.BeginWeaponAttack(WeaponSlot.RightHand); input.TrySelectTarget(Enemy);
            Assert.That(input.CurrentAttackPreview.CanAttack, Is.True);
            Assert.That(ranges.childCount, Is.GreaterThan(12)); yield return null;
        }
        [UnityTest] public IEnumerator PreviewAndBodyClicksNeverRollDamage()
        {
            input.BeginWeaponAttack(WeaponSlot.RightHand); input.TrySelectTarget(Enemy);
            for (int i = 0; i < 4; i++)
            { yield return Click(body.GetPartRect(TargetBodyPart.Head)); var preview = input.CurrentAttackPreview; Assert.That(preview.MaxDamage, Is.EqualTo(6)); }
            Assert.That(damageRolls, Is.Zero); Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator EscapeAndUnitChangeClearWeaponState()
        {
            yield return OpenRangedList();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(input.SelectedUnit, Is.Null); Assert.That(input.IsWeaponMenuOpen, Is.False); Assert.That(input.SelectedWeaponSlot, Is.Null);
            input.SelectUnit(Player); input.BeginWeaponAttack(WeaponSlot.RightHand);
            input.SelectUnit(battle.Players[1]);
            Assert.That(input.SelectedWeaponSlot, Is.Null); Assert.That(input.IsWeaponMenuOpen, Is.False);
        }
        [UnityTest] public IEnumerator InvalidLoadoutBlocksMenusAndDirectInput()
        {
            loadout.Configure(loadout.LeftWeapon, GripMode.OneHand, loadout.RightWeapon, GripMode.TwoHands);
            Assert.That(input.CanOpenAttackMenu, Is.False); Assert.That(input.CanBeginRanged, Is.False);
            Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand), Is.False);
            Assert.That(input.BeginAttack(), Is.False); yield return null;
        }
        [UnityTest] public IEnumerator SecondAffordableWeaponKeepsTypeEnabled()
        {
            Player.ActionPoints.TrySpend(4);
            Assert.That(input.CanBeginRanged, Is.True);
            yield return Click(menu.AttackButtonRect);
            Assert.That(input.IsWeaponMenuOpen, Is.True);
            Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand), Is.False);
            Assert.That(menu.GetWeaponButtonRect(WeaponSlot.RightHand).width, Is.GreaterThan(0));
            yield return Click(menu.GetWeaponButtonRect(WeaponSlot.RightHand));
            Assert.That(input.SelectedWeaponSlot, Is.Null);
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(1));
            yield return Click(menu.GetWeaponButtonRect(WeaponSlot.LeftHand));
            Assert.That(input.SelectedWeaponSlot, Is.EqualTo(WeaponSlot.LeftHand));
        }
        [UnityTest] public IEnumerator WeaponListFitsNearScreenEdge()
        {
            float x = camera.WorldToViewportPoint(Player.transform.position).x;
            camera.transform.position += camera.transform.right * ((x - .82f) * 2 * camera.orthographicSize * camera.aspect);
            yield return OpenRangedList();
            Assert.That(menu.TryGetPanelRect(out var panel), Is.True);
            Assert.That(menu.TryGetAttackMenuRect(out var list), Is.True);
            Assert.That(panel.Overlaps(list), Is.False);
            Assert.That(list.xMin, Is.GreaterThanOrEqualTo(0)); Assert.That(list.xMax, Is.LessThanOrEqualTo(Screen.width));
            Assert.That(list.yMin, Is.GreaterThanOrEqualTo(0)); Assert.That(list.yMax, Is.LessThanOrEqualTo(Screen.height));
            yield return Click(menu.GetWeaponButtonRect(WeaponSlot.LeftHand));
            Assert.That(input.SelectedWeaponSlot, Is.EqualTo(WeaponSlot.LeftHand));
        }
        [UnityTest] public IEnumerator SightLossAndTurnEndKeepExistingCleanup()
        {
            input.BeginWeaponAttack(WeaponSlot.RightHand); input.TrySelectTarget(Enemy); input.TrySelectBodyPart(TargetBodyPart.Head);
            battle.Turns.Visibility.enabled = false;
            Assert.That(input.TargetUnit, Is.Null); Assert.That(input.SelectedBodyPart, Is.EqualTo(TargetBodyPart.Whole));
            Assert.That(input.LastAttackResult.WasExecuted, Is.False);
            battle.Turns.Visibility.enabled = true;
            input.BeginAttack(); Assert.That(input.IsWeaponMenuOpen, Is.True);
            Assert.That(input.TryEndTurn(), Is.True);
            Assert.That(input.IsWeaponMenuOpen, Is.False); Assert.That(input.SelectedWeaponSlot, Is.Null); yield return null;
        }
        [UnityTest] public IEnumerator FixedDamageDisplayAndDisabledListStayConsistent()
        {
            var fixedRifle = Weapon("Fixed Rifle", 3, 3, 7, 1);
            loadout.Configure(null, GripMode.OneHand, fixedRifle, GripMode.OneHand);
            input.BeginAttack(); input.TrySelectTarget(Enemy);
            Assert.That(body.AttackInfoText, Is.EqualTo("Damage 3 / AP 1"));
            input.OpenAttackMenu(); loadout.enabled = false; yield return null;
            Assert.That(input.CanOpenAttackMenu, Is.False); Assert.That(input.IsAttackMenuOpen, Is.False);
            Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand), Is.False);
        }
        private IEnumerator Capture(string filename)
        {
            Directory.CreateDirectory("Artifacts"); string path = Path.GetFullPath("Artifacts/" + filename);
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            float deadline = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(File.Exists(path), Is.True, "Game-view screenshot was not written.");
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(1000));
        }
        [UnityTest] public IEnumerator GraphicsPanelsFitLargeAndSmallScreens()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("Run with -Graphics.");
            loadout.Configure(Weapon("Axe", 8, 8, 2, 3, AttackType.Melee), GripMode.OneHand,
                loadout.RightWeapon, GripMode.OneHand);
#if UNITY_EDITOR
            var sizesType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance").GetValue(null);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var groupType = sizesType.GetProperty("currentGroupType", flags).GetValue(sizes);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
            var viewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(viewType);
            var selection = viewType.GetProperty("selectedSizeIndex", flags);
            int oldSelection = (int)selection.GetValue(view);
            int builtinCount = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
            int previousCustoms = (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
            // Remove only our named test entries left by an interrupted earlier test run.
            for (int i = previousCustoms - 1; i >= 0; i--)
            {
                var entry = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { builtinCount + i });
                if ((string)entry.GetType().GetProperty("baseText", flags).GetValue(entry) == "Weapon UI test")
                    group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { builtinCount + i });
            }
            int customCount = (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
#else
            int oldWidth = Screen.width, oldHeight = Screen.height;
#endif
            try
            {
                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(640, 360) })
                {
#if UNITY_EDITOR
                    var sizeType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSize");
                    var modeType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
                    var entry = Activator.CreateInstance(sizeType, new object[] { Enum.Parse(modeType, "FixedResolution"), size.x, size.y, "Weapon UI test" });
                    group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { entry });
                    int builtins = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
                    int customs = (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
                    selection.SetValue(view, builtins + customs - 1); view.Repaint();
#else
                    Screen.SetResolution(size.x, size.y, false);
#endif
                    float resizeDeadline = Time.realtimeSinceStartup + 5;
                    while ((Screen.width != size.x || Screen.height != size.y) && Time.realtimeSinceStartup < resizeDeadline) yield return null;
                    Assert.That(Screen.width, Is.EqualTo(size.x)); Assert.That(Screen.height, Is.EqualTo(size.y));
                    input.SelectUnit(Player); input.OpenAttackMenu();
                    Assert.That(menu.TryGetPanelRect(out var panel), Is.True);
                    Assert.That(menu.TryGetAttackMenuRect(out var list), Is.True);
                    Assert.That(panel.Overlaps(list), Is.False);
                    Assert.That(list.xMin, Is.GreaterThanOrEqualTo(0)); Assert.That(list.xMax, Is.LessThanOrEqualTo(Screen.width));
                    yield return Capture("weapon-menu-" + size.x + ".png");
                    yield return Click(menu.GetWeaponButtonRect(WeaponSlot.RightHand)); input.TrySelectTarget(Enemy);
                    Assert.That(menu.TryGetPanelRect(out panel), Is.True); Assert.That(panel.Overlaps(body.PanelRect), Is.False);
                    Assert.That(panel.xMin, Is.GreaterThanOrEqualTo(0)); Assert.That(panel.yMin, Is.GreaterThanOrEqualTo(0));
                    Assert.That(panel.xMax, Is.LessThanOrEqualTo(Screen.width)); Assert.That(panel.yMax, Is.LessThanOrEqualTo(Screen.height));
                    Assert.That(body.PanelRect.xMax, Is.LessThanOrEqualTo(Screen.width));
                    yield return Capture("weapon-target-" + size.x + ".png");
                }
            }
            finally
            {
#if UNITY_EDITOR
                selection.SetValue(view, oldSelection);
                int currentCount = (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
                for (int i = currentCount - 1; i >= customCount; i--) group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { builtinCount + i });
                view.Repaint();
#else
                Screen.SetResolution(oldWidth, oldHeight, false);
#endif
            }
            yield return null;
        }
    }
}
