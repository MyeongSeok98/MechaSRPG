using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class UnitActionMenuTests
    {
        private EnemyAITestBattle battle;
        private PlayerInputController input;
        private GridVisualizer visualizer;
        private Camera camera;
        private Mouse mouse;
        private Keyboard keyboard;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private InputSettings.BackgroundBehavior oldBackground;
        private BattleUnit Player => battle.Players[0];
        private BattleUnit Enemy => battle.Enemies[0];
        private Transform Ranges => battle.Root.transform.Find("Reachable Cells");
        [UnitySetUp]
        public IEnumerator Setup()
        {
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            battle = new EnemyAITestBattle(new[] { new Vector2Int(3, 3) }, new[] { new Vector2Int(6, 3) });
            Player.gameObject.AddComponent<CapsuleCollider>(); Enemy.gameObject.AddComponent<CapsuleCollider>();
            visualizer = battle.Root.AddComponent<GridVisualizer>(); visualizer.Initialize(battle.Grid);
            var debug = battle.Root.AddComponent<BattleDebugUI>(); debug.Initialize(battle.Grid, battle.Root.GetComponent<CoverSystem>());
            camera = new GameObject("Action camera").AddComponent<Camera>(); camera.transform.SetParent(battle.Root.transform);
            camera.orthographic = true; camera.orthographicSize = 15;
            camera.transform.position = new Vector3(10, 28, -20); camera.transform.LookAt(new Vector3(10, 0, 10));
            input = battle.Root.AddComponent<PlayerInputController>(); input.Initialize(camera, battle.Grid, visualizer, debug);
            input.InitializeCombat(battle.Combat, battle.Turns); debug.InitializeCombat(input, battle.Turns);
            mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            Physics.SyncTransforms(); yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            InputSystem.settings.backgroundBehavior = oldBackground;
            battle.Dispose(); yield return null;
        }
        private bool Begin(string name)
        {
            return name == "BeginMove" ? input.BeginMove() : input.BeginAttack();
        }
        private UnitActionUI Menu()
        {
            var result = battle.Root.GetComponent<UnitActionUI>();
            Assert.That(result, Is.Not.Null, "Legacy scenes need a runtime action menu.");
            return result;
        }
        private Rect Button(string property)
        {
            var menu = Menu();
            return property == "MoveButtonRect" ? menu.MoveButtonRect :
                property == "AttackButtonRect" ? menu.AttackButtonRect :
                property == "LeftWeaponButtonRect" ? menu.GetWeaponButtonRect(WeaponSlot.LeftHand) :
                property == "RightWeaponButtonRect" ? menu.GetWeaponButtonRect(WeaponSlot.RightHand) : menu.FireButtonRect;
        }
        private IEnumerator ClickButton(string property)
        {
            var rect = Button(property);
            var point = new Vector2(rect.center.x, Screen.height - rect.center.y);
            yield return ClickAt(point);
        }
        private IEnumerator ClickAt(Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
        }
        private IEnumerator FinishMove(bool keepFacing = true)
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (Player.Movement.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Player.Movement.IsMoving, Is.False); yield return null;
            if (keepFacing && Player.HasPendingFacing)
            {
                Assert.That(Player.Movement.TryKeepArrivalFacing(), Is.True);
                yield return null;
            }
        }
        private IEnumerator ClickRect(Rect rect) => ClickAt(new Vector2(rect.center.x, Screen.height - rect.center.y));
        private IEnumerator FinishFacing()
        {
            float deadline = Time.realtimeSinceStartup + 4;
            while (Player.HasPendingFacing && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Player.HasPendingFacing, Is.False);
            yield return null;
        }
        private Transform AddVisual()
        {
            var visual = new GameObject("Visual").transform;
            visual.SetParent(Player.transform, false);
            visual.localPosition = Vector3.down;
            Player.Movement.Initialize(battle.Grid, Player);
            return visual;
        }
        [UnityTest]
        public IEnumerator FacingButtonsTurnModelToAllFourWorldDirections()
        {
            var visual = AddVisual();
            var rootRotation = Player.transform.rotation;
            for (int i = 0; i < 4; i++)
            {
                input.SelectUnit(Player); input.BeginMove();
                Assert.That(input.TryMoveSelected(Player.CurrentGridPosition + Vector2Int.up), Is.True);
                yield return FinishMove(false);
                var before = visual.rotation;
                int ap = Player.ActionPoints.CurrentPoints, evasion = Player.MovementEvasion;
                yield return ClickRect(Menu().GetFacingButtonRect((FacingDirection)i));
                Assert.That(input.SelectedFacing, Is.EqualTo((FacingDirection)i));
                Assert.That(Quaternion.Angle(visual.rotation, before), Is.LessThan(.01f));
                yield return ClickRect(Menu().ConfirmFacingButtonRect);
                yield return FinishFacing();
                Assert.That(Player.Facing, Is.EqualTo((FacingDirection)i));
                Assert.That(Vector3.Angle(visual.forward, Quaternion.Euler(0, i * 90, 0) * Vector3.forward), Is.LessThan(.01f));
                Assert.That(Player.transform.rotation, Is.EqualTo(rootRotation));
                Assert.That(Player.GetComponent<CapsuleCollider>().enabled, Is.True);
                Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(ap));
                Assert.That(Player.MovementEvasion, Is.EqualTo(evasion));
                Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Menu));
            }
        }
        [UnityTest]
        public IEnumerator ZeroAPAndMissingVisualStillAllowFacingConfirmation()
        {
            input.SelectUnit(Player); input.BeginMove();
            Assert.That(input.TryMoveSelected(new Vector2Int(3, 8)), Is.True);
            yield return FinishMove(false);
            Assert.That(Player.ActionPoints.CurrentPoints, Is.Zero);
            Assert.That(input.CanChooseFacing, Is.True);
            Assert.That(input.TryChooseFacing(FacingDirection.West), Is.True);
            Assert.That(input.TryConfirmFacing(), Is.True);
            Assert.That(input.TryConfirmFacing(), Is.False);
            yield return FinishFacing();
            Assert.That(Player.Facing, Is.EqualTo(FacingDirection.West));
            Assert.That(Player.ActionPoints.CurrentPoints, Is.Zero);
        }
        [UnityTest]
        public IEnumerator PendingFacingBlocksDirectAPIsWithoutChangingHitChance()
        {
            input.SelectUnit(Player); input.BeginMove(); input.TryMoveSelected(new Vector2Int(3, 4));
            yield return FinishMove(false);
            int ap = Player.ActionPoints.CurrentPoints;
            Assert.That(Player.Movement.TryMove(new Vector2Int(3, 3)), Is.False);
            Assert.That(battle.Combat.TryAttack(Player, Enemy).WasExecuted, Is.False);
            Assert.That(Player.TryEject(), Is.False);
            Assert.That(battle.Turns.TryEndTurn(), Is.False);
            Assert.That(input.SelectUnit(Player), Is.False);
            Assert.That(input.TryChooseFacing((FacingDirection)99), Is.False);
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(ap));
            int hitChance = battle.Combat.GetPreview(Player, Enemy).HitChance;
            input.TryChooseFacing(FacingDirection.East); input.TryConfirmFacing();
            yield return FinishFacing();
            Assert.That(battle.Combat.GetPreview(Player, Enemy).CanAttack, Is.True);
            Assert.That(battle.Combat.GetPreview(Player, Enemy).HitChance, Is.EqualTo(hitChance));
        }
        [UnityTest]
        public IEnumerator EscapeDuringTravelReopensFacingAndEscapeKeepsArrivalDirection()
        {
            AddVisual(); input.SelectUnit(Player); input.BeginMove(); input.TryMoveSelected(new Vector2Int(3, 5));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(input.SelectedUnit, Is.Null);
            yield return FinishMove(false);
            Assert.That(input.SelectedUnit, Is.SameAs(Player));
            Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Facing));
            input.TryChooseFacing(FacingDirection.West);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(input.SelectedUnit, Is.Null);
            Assert.That(Player.Facing, Is.EqualTo(FacingDirection.North));
            Assert.That(Player.HasPendingFacing, Is.False);
        }
        [UnityTest]
        public IEnumerator EscapeDuringConfirmationDoesNotInterruptRotation()
        {
            var visual = AddVisual(); input.SelectUnit(Player); input.BeginMove(); input.TryMoveSelected(new Vector2Int(3, 4));
            yield return FinishMove(false);
            input.TryChooseFacing(FacingDirection.South); Assert.That(input.TryConfirmFacing(), Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(Player.IsConfirmingFacing, Is.True);
            Assert.That(input.SelectedUnit, Is.SameAs(Player));
            yield return FinishFacing();
            Assert.That(Vector3.Angle(visual.forward, Vector3.back), Is.LessThan(.01f));
        }
        [UnityTest]
        public IEnumerator CriticalDuringConfirmationReleasesLockAndRestoresCommittedDirection()
        {
            var visual = AddVisual(); input.SelectUnit(Player); input.BeginMove(); input.TryMoveSelected(new Vector2Int(3, 4));
            yield return FinishMove(false);
            input.TryChooseFacing(FacingDirection.South); input.TryConfirmFacing(); yield return null;
            MechStateTestRules.MakeCritical(Player);
            yield return null;
            Assert.That(Player.HasPendingFacing, Is.False);
            Assert.That(Player.IsConfirmingFacing, Is.False);
            Assert.That(battle.Turns.PendingFacingUnit, Is.Null);
            Assert.That(Player.Facing, Is.EqualTo(FacingDirection.North));
            Assert.That(Vector3.Angle(visual.forward, Vector3.forward), Is.LessThan(.01f));
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
        }
        [UnityTest]
        public IEnumerator BattleEndDuringTravelDoesNotLeaveFacingPending()
        {
            input.SelectUnit(Player); input.BeginMove();
            Assert.That(input.TryMoveSelected(new Vector2Int(3, 8)), Is.True);
            Enemy.Health.ApplyDamage(TargetBodyPart.Torso, 20);
            Assert.That(battle.Turns.IsBattleOver, Is.True);
            yield return FinishMove(false);
            Assert.That(Player.HasPendingFacing, Is.False);
            Assert.That(Player.IsConfirmingFacing, Is.False);
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator DisabledTurnManagerDuringTravelDoesNotLeaveFacingPending()
        {
            input.SelectUnit(Player); input.BeginMove();
            Assert.That(input.TryMoveSelected(new Vector2Int(3, 8)), Is.True);
            battle.Turns.enabled = false;
            yield return FinishMove(false);
            Assert.That(Player.HasPendingFacing, Is.False);
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator MoveArrivalOpensFacingChoice()
        {
            input.SelectUnit(Player);
            Assert.That(input.BeginMove(), Is.True);
            Assert.That(input.TryMoveSelected(new Vector2Int(3, 4)), Is.True);
            yield return FinishMove(false);
            Assert.That(input.ActionMode.ToString(), Is.EqualTo("Facing"));
            Assert.That(Menu().IsVisible, Is.True);
            Assert.That(input.CanBeginMove, Is.False);
            Assert.That(battle.Turns.TryEndTurn(), Is.False);
        }
        [UnityTest]
        public IEnumerator RangedDiagramSelectsCockpitThenFiresUsingDisplayedChance()
        {
            input.SelectUnit(Player); input.BeginAttack(); input.TrySelectTarget(Enemy);
            var diagram = battle.Root.GetComponent<BodyTargetUI>();
            Assert.That(diagram, Is.Not.Null, "Ranged targeting needs a clickable body diagram.");
            Assert.That(diagram.IsVisible, Is.True);
            Assert.That(diagram.CurrentTexture.name, Is.EqualTo("Mecha"));
            var rect = diagram.GetPartRect(TargetBodyPart.Cockpit);
            yield return ClickAt(new Vector2(rect.center.x, Screen.height - rect.center.y));
            Assert.That(input.CurrentAttackPreview.HitChance, Is.EqualTo(36));
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            Assert.That(diagram.CurrentTexture.name, Is.EqualTo("MechaCockpit"));
            battle.Combat.SetRollSource(() => 36);
            var fire = diagram.FireButtonRect;
            yield return ClickAt(new Vector2(fire.center.x, Screen.height - fire.center.y));
            Assert.That(input.LastAttackResult.WasExecuted, Is.True);
            Assert.That(input.LastAttackResult.Hit, Is.False);
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(diagram.IsVisible, Is.False);
        }
        [UnityTest]
        public IEnumerator DiagramClicksSelectAllPartsAndEscapeClosesThePanel()
        {
            input.SelectUnit(Player); input.BeginAttack(); input.TrySelectTarget(Enemy);
            var diagram = battle.Root.GetComponent<BodyTargetUI>();
            var parts = new[] { TargetBodyPart.Head, TargetBodyPart.Torso, TargetBodyPart.Cockpit,
                TargetBodyPart.RightArm, TargetBodyPart.LeftArm, TargetBodyPart.RightLeg, TargetBodyPart.LeftLeg };
            var points = new[] { new Vector2(.48f,.09f), new Vector2(.52f,.20f), new Vector2(.445f,.335f),
                new Vector2(.30f,.40f), new Vector2(.73f,.43f), new Vector2(.35f,.75f), new Vector2(.68f,.76f) };
            var images = new[] { "MechaHead", "MechaTorso", "MechaCockpit", "MechaRightarm", "MechaLeftarm", "MechaRightleg", "MechaLeftleg" };
            var chances = new[] { 64, 80, 36, 72, 72, 72, 72 };
            for (int i = 0; i < parts.Length; i++)
            {
                var image = diagram.ImageRect;
                yield return ClickAt(new Vector2(image.x + points[i].x * image.width, Screen.height - image.y - points[i].y * image.height));
                Assert.That(input.SelectedBodyPart, Is.EqualTo(parts[i]));
                Assert.That(diagram.CurrentTexture.name, Is.EqualTo(images[i]));
                Assert.That(input.CurrentAttackPreview.HitChance, Is.EqualTo(chances[i]));
                Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            }
            input.BeginAttack(); input.TrySelectTarget(Enemy);
            Assert.That(input.SelectedBodyPart, Is.EqualTo(TargetBodyPart.Whole));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(diagram.IsVisible, Is.False); Assert.That(input.SelectedUnit, Is.Null);
            Player.Combat.ConfigureTestMelee(8, 95, 2, 3); battle.Grid.TryMoveUnit(Enemy, new Vector2Int(4,3));
            input.SelectUnit(Player); input.BeginMelee(); input.TrySelectTarget(Enemy);
            Assert.That(diagram.IsVisible, Is.False);
            Assert.That(input.TrySelectBodyPart(TargetBodyPart.Cockpit), Is.False);
            Assert.That(input.CurrentAttackPreview.HitChance, Is.EqualTo(95));
        }
        [UnityTest]
        public IEnumerator VisibleCockpitShoulderAndThighClicksSelectTheirParts()
        {
            input.SelectUnit(Player); input.BeginAttack(); input.TrySelectTarget(Enemy);
            var diagram = battle.Root.GetComponent<BodyTargetUI>();
            var points = new[] { new Vector2(.45f,.28f), new Vector2(.415f,.30f), new Vector2(.32f,.15f), new Vector2(.41f,.50f) };
            var parts = new[] { TargetBodyPart.Cockpit, TargetBodyPart.Cockpit, TargetBodyPart.RightArm, TargetBodyPart.RightLeg };
            for (int i = 0; i < parts.Length; i++)
            {
                var image = diagram.ImageRect;
                yield return ClickAt(new Vector2(image.x + points[i].x * image.width, Screen.height - image.y - points[i].y * image.height));
                Assert.That(input.SelectedBodyPart, Is.EqualTo(parts[i]));
            }
        }
        [UnityTest]
        public IEnumerator RightEdgeUnitKeepsActionMenuOutsideBodyDiagram()
        {
            input.SelectUnit(Player); input.BeginAttack(); input.TrySelectTarget(Enemy);
            float viewportX = camera.WorldToViewportPoint(Player.transform.position).x;
            camera.transform.position += camera.transform.right * ((viewportX - .96f) * 2 * camera.orthographicSize * camera.aspect);
            var diagram = battle.Root.GetComponent<BodyTargetUI>();
            Assert.That(Menu().TryGetPanelRect(out var menu), Is.True);
            Assert.That(menu.Overlaps(diagram.PanelRect), Is.False, "Body targeting must not capture Move/Attack controls.");
            yield return ClickButton("MoveButtonRect");
            Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Move));
            Assert.That(diagram.IsVisible, Is.False);
        }
        [UnityTest]
        public IEnumerator MeleeButtonPreviewsCoverIgnoringAttackAndConfirmsEightDamage()
        {
            Player.Combat.ConfigureTestMelee(8, 95, 2, 3);
            battle.Grid.TryMoveUnit(Enemy, new Vector2Int(4, 3));
            Enemy.transform.position = battle.Grid.GetWorldPosition(Enemy.CurrentGridPosition) + Vector3.up;
            battle.Grid.TryGetCell(Enemy.CurrentGridPosition, out var cell); cell.SetCover(CoverDirection.West, CoverType.Full);
            for (int i = 0; i < 3; i++) Enemy.Mobility.RecordCellMoved();
            input.SelectUnit(Player);
            yield return ClickButton("AttackButtonRect");
            var rect = Menu().GetWeaponButtonRect(WeaponSlot.LeftHand);
            yield return ClickAt(new Vector2(rect.center.x, Screen.height - rect.center.y));
            Assert.That(input.TrySelectTarget(Enemy), Is.True);
            Assert.That(input.CurrentAttackPreview.HitChance, Is.EqualTo(95));
            Assert.That(input.CurrentAttackPreview.MinDamage, Is.EqualTo(8));
            Assert.That(Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
            yield return ClickButton("FireButtonRect");
            Assert.That(Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(12));
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Menu));
        }
        [UnityTest]
        public IEnumerator SelectionOpensMenuWithoutRangeOrImplicitAction()
        {
            input.SelectUnit(Player);
            Assert.That(Ranges.gameObject.activeSelf, Is.False);
            Menu();
            Assert.That(input.TryMoveSelected(new Vector2Int(4, 3)), Is.False);
            Assert.That(input.TrySelectTarget(Enemy), Is.False);
            Assert.That(input.TryAttackSelected().WasExecuted, Is.False);
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            yield return null;
        }
        [UnityTest]
        public IEnumerator MoveModeShowsRangeAndCompletionReturnsToMenu()
        {
            input.SelectUnit(Player); Assert.That(Begin("BeginMove"), Is.True);
            Assert.That(Ranges.gameObject.activeSelf, Is.True);
            Assert.That(input.TryMoveSelected(new Vector2Int(4, 3)), Is.True);
            yield return FinishMove();
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(4));
            Assert.That(input.SelectedUnit, Is.SameAs(Player));
            Assert.That(Ranges.gameObject.activeSelf, Is.False);
            Assert.That(input.TryMoveSelected(new Vector2Int(5, 3)), Is.False);
        }
        [UnityTest]
        public IEnumerator AttackModeShowsRedClearShotCellsAndRequiresConfirmation()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.transform.SetParent(battle.Root.transform);
            cube.transform.position = battle.Grid.GetWorldPosition(new Vector2Int(4, 4)) + Vector3.up;
            cube.transform.localScale = new Vector3(2, 4, 2);
            battle.Grid.RegisterObstacle(cube.AddComponent<GridObstacle>().Collider);
            input.SelectUnit(Player); Assert.That(Begin("BeginAttack"), Is.True);
            var displays = Ranges.GetComponentsInChildren<Renderer>();
            Assert.That(displays.Length, Is.GreaterThan(0));
            Assert.That(displays.All(r => r.sharedMaterial.color.r > r.sharedMaterial.color.g), Is.True);
            Assert.That(displays.Any(r => Vector2.Distance(new Vector2(r.transform.position.x, r.transform.position.z), new Vector2(13, 7)) < .01f), Is.True);
            Assert.That(displays.Any(r => Vector2.Distance(new Vector2(r.transform.position.x, r.transform.position.z), new Vector2(13, 13)) < .01f), Is.False, "Building-occluded cells must not be highlighted.");
            Assert.That(displays.Any(r => Vector2.Distance(new Vector2(r.transform.position.x, r.transform.position.z), new Vector2(19, 19)) < .01f), Is.False);
            int rolls = 0; battle.Combat.SetRollSource(() => { rolls++; return 0; });
            Assert.That(input.TrySelectTarget(Enemy), Is.True);
            Assert.That(input.CurrentAttackPreview.HitChance, Is.EqualTo(80));
            Assert.That(Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20)); Assert.That(rolls, Is.Zero);
            Assert.That(input.TryAttackSelected().Hit, Is.True);
            Assert.That(Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15)); Assert.That(rolls, Is.EqualTo(1));
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(Ranges.gameObject.activeSelf, Is.False);
            Assert.That(input.TargetUnit, Is.Null); yield return null;
        }
        [UnityTest]
        public IEnumerator PopupMouseButtonsChooseModesAndFireExactlyOnce()
        {
            input.SelectUnit(Player);
            yield return ClickButton("MoveButtonRect");
            Assert.That(Ranges.gameObject.activeSelf, Is.True);
            Assert.That(Menu().IsVisible, Is.False, "Mode selection must not obscure destination clicks.");
            yield return ClickAt(camera.WorldToScreenPoint(Player.transform.position));
            yield return ClickButton("AttackButtonRect");
            Assert.That(Ranges.gameObject.activeSelf, Is.False, "Opening choices must not begin an attack.");
            yield return ClickButton("RightWeaponButtonRect");
            Assert.That(Menu().IsVisible, Is.False, "Hide the menu until a target is selected.");
            Assert.That(Ranges.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterial.color.r > r.sharedMaterial.color.g), Is.True);
            Assert.That(input.TrySelectTarget(Enemy), Is.True);
            battle.Combat.SetRollSource(() => 0);
            yield return ClickButton("FireButtonRect");
            Assert.That(Player.CurrentGridPosition, Is.EqualTo(new Vector2Int(3, 3)));
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(2));
            Assert.That(Enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(15));
            Assert.That(input.SelectedUnit, Is.SameAs(Player));
        }
        [UnityTest]
        public IEnumerator AttackOpensSeparateChoicesWithoutActionAndEscapeClosesEverything()
        {
            input.SelectUnit(Player);
            yield return ClickButton("AttackButtonRect");
            Assert.That(Menu().IsVisible, Is.True);
            Assert.That(Ranges.gameObject.activeSelf, Is.False);
            Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Menu));
            Assert.That(input.TrySelectTarget(Enemy), Is.False);
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
            Assert.That(Menu().TryGetAttackMenuRect(out var choices), Is.True);
            Assert.That(Menu().TryGetPanelRect(out var main), Is.True);
            Assert.That(main.Overlaps(choices), Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(input.SelectedUnit, Is.Null);
            Assert.That(Menu().IsVisible, Is.False);
            Assert.That(Menu().TryGetAttackMenuRect(out _), Is.False);
        }
        [UnityTest]
        public IEnumerator AttackChoicesFitBesideMainPanelNearScreenEdge()
        {
            float currentX = camera.WorldToViewportPoint(Player.transform.position).x;
            camera.transform.position += camera.transform.right * ((currentX - .82f) * 2 * camera.orthographicSize * camera.aspect);
            input.SelectUnit(Player); yield return ClickButton("AttackButtonRect");
            Assert.That(Menu().IsVisible, Is.True);
            Assert.That(Menu().TryGetAttackMenuRect(out var choices), Is.True);
            Assert.That(Menu().TryGetPanelRect(out var main), Is.True);
            Assert.That(choices.xMax, Is.LessThanOrEqualTo(main.xMin));
            Assert.That(choices.xMin, Is.GreaterThanOrEqualTo(0));
            Assert.That(choices.yMin, Is.GreaterThanOrEqualTo(0));
            Assert.That(choices.xMax, Is.LessThanOrEqualTo(Screen.width));
            Assert.That(choices.yMax, Is.LessThanOrEqualTo(Screen.height));
            yield return ClickButton("RightWeaponButtonRect");
            Assert.That(input.SelectedUnit, Is.SameAs(Player));
            Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Attack));
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator EscapeClearsSelectionMenuTargetAndEitherRange()
        {
            foreach (var mode in new[] { "BeginMove", "BeginAttack" })
            {
                input.SelectUnit(Player); Assert.That(Begin(mode), Is.True);
                if (mode == "BeginAttack") input.TrySelectTarget(Enemy);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Assert.That(input.SelectedUnit, Is.Null); Assert.That(input.TargetUnit, Is.Null);
                Assert.That(Ranges.gameObject.activeSelf, Is.False);
                Assert.That(Menu().IsVisible, Is.False);
            }
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator InsufficientAPDisablesUnavailableActions()
        {
            input.SelectUnit(Player); Player.ActionPoints.TrySpend(3);
            Assert.That(Begin("BeginAttack"), Is.False);
            Assert.That(Begin("BeginMove"), Is.True);
            Player.ActionPoints.TrySpend(2); yield return null;
            Assert.That(Begin("BeginMove"), Is.False);
            Assert.That(Ranges.gameObject.activeSelf, Is.False);
            Assert.That(input.ActionMode, Is.EqualTo(UnitActionMode.Menu));
            Assert.That(Menu().IsVisible, Is.True, "Show the menu with disabled actions after the active mode becomes unavailable.");
        }
        [UnityTest]
        public IEnumerator FactionChangeClosesRangeAndReturnsToMenu()
        {
            input.SelectUnit(Player); Begin("BeginAttack"); input.TrySelectTarget(Enemy);
            battle.Combat.SetRollSource(() => 99);
            Assert.That(input.TryEndTurn(), Is.True);
            Assert.That(Ranges.gameObject.activeSelf, Is.False);
            Assert.That(Menu().IsVisible, Is.False);
            yield return battle.WaitForPlayerTurn(); yield return null;
            Assert.That(Ranges.gameObject.activeSelf, Is.False);
            Assert.That(input.TargetUnit, Is.Null);
            Assert.That(Menu().IsVisible, Is.True);
        }
        [UnityTest]
        public IEnumerator PopupFollowsCameraProjectionWithoutChangingAP()
        {
            input.SelectUnit(Player);
            var before = Button("MoveButtonRect");
            camera.transform.position += Vector3.right * 3;
            yield return null;
            Assert.That(Button("MoveButtonRect").x, Is.Not.EqualTo(before.x));
            Assert.That(Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
    }
}
