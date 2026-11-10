using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class UnitInspectionPlayTests
    {
        private BackpackTestBattle b;
        private PlayerInputController input;
        private BattleDebugUI info;
        private GridVisualizer visual;
        private Camera camera;
        private Mouse mouse;
        private Keyboard keyboard;
        private Material gold, modelMaterial;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private InputSettings.BackgroundBehavior oldBackground;
        [UnitySetUp] public IEnumerator Setup()
        {
            b = new BackpackTestBattle();
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode; oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            modelMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.red };
            foreach (var unit in new[] { b.Player, b.Enemy })
            {
                unit.gameObject.AddComponent<CapsuleCollider>();
                var model = GameObject.CreatePrimitive(PrimitiveType.Capsule); model.name = "Visual";
                model.transform.SetParent(unit.transform, false); model.GetComponent<Collider>().enabled = false;
                model.GetComponent<Renderer>().sharedMaterial = modelMaterial;
            }
            JsonUtility.FromJsonOverwrite("{\"displayName\":\"Enemy Scout\"}", b.Enemy.MechaData);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.transform.SetParent(b.Root.transform);
            ground.transform.position = new Vector3(12, -.1f, 12); ground.transform.localScale = new Vector3(24, .2f, 24);
            visual = b.Root.AddComponent<GridVisualizer>();
            gold = new Material(modelMaterial) { color = Color.yellow }; visual.ConfigureMaterials(null, null, gold); visual.Initialize(b.Grid, b.Turns);
            info = b.Root.AddComponent<BattleDebugUI>(); info.Initialize(b.Grid, b.Cover);
#if UNITY_EDITOR
            var frame = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/UI/Panels/UnitInfoFrame_Olive.png");
            Assert.That(frame, Is.Not.Null);
            typeof(BattleDebugUI).GetField("panelFrame", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(info, frame);
#endif
            camera = new GameObject("Inspection Camera").AddComponent<Camera>(); camera.transform.SetParent(b.Root.transform);
            camera.rect = new Rect(.23f, 0, .77f, 1); camera.orthographic = true; camera.orthographicSize = 9;
            camera.transform.position = new Vector3(6, 20, -15); camera.transform.LookAt(new Vector3(6, 0, 4));
            input = b.Root.AddComponent<PlayerInputController>(); input.Initialize(camera, b.Grid, visual, info); input.InitializeCombat(b.Combat, b.Turns);
            info.InitializeCombat(input, b.Turns); mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            Physics.SyncTransforms(); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor; InputSystem.settings.backgroundBehavior = oldBackground;
            b.Dispose(); Object.Destroy(gold); Object.Destroy(modelMaterial); yield return null;
        }
        private T Read<T>(string property) => (T)typeof(BattleDebugUI).GetProperty(property).GetValue(info);
        private Transform Marker => b.Root.transform.Find("Selection Indicator");
        private IEnumerator Click(Vector3 position)
        {
            Vector2 point = camera.WorldToScreenPoint(position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
        }
        [UnityTest] public IEnumerator EnemyClickShowsNameHealthAndEquipmentWithoutActions()
        {
            yield return Click(b.Enemy.transform.position);
            Assert.That(input.SelectedUnit, Is.SameAs(b.Enemy)); Assert.That(Read<BattleUnit>("DisplayedUnit"), Is.SameAs(b.Enemy));
            Assert.That(Read<string>("MechaName"), Is.EqualTo("Enemy Scout"));
            Assert.That(Read<string>("EquipmentText"), Does.Contain(b.Weapon.DisplayName));
            Assert.That(Read<string>("PartHealthText"), Does.Contain("Body "));
            Assert.That(input.CanChooseAction || input.CanEjectSelected || input.CanOpenReloadMenu || input.CanOpenOverwatchMenu, Is.False);
            Assert.That(input.BeginMove() || input.BeginAttack() || input.TryEjectSelected(), Is.False);
            Assert.That(b.Root.GetComponent<UnitActionUI>().IsVisible, Is.False);
            b.StartEnemyTeam(); yield return null;
            Assert.That(input.CanChooseAction || input.CanEjectSelected || input.BeginMove(), Is.False, "Inspection must never grant control of enemies on their turn.");
        }
        [UnityTest] public IEnumerator SelectionSquareChangesTeamColorWithoutEditingAuthoredMaterial()
        {
            Assert.That(input.SelectUnit(b.Player), Is.True); yield return null;
            Assert.That(Marker.childCount, Is.EqualTo(4));
            foreach (var renderer in Marker.GetComponentsInChildren<Renderer>())
            { var color = renderer.sharedMaterial.color; Assert.That(color.g, Is.GreaterThan(color.r * 2)); }
            Assert.That(gold.color, Is.EqualTo(Color.yellow));
            Assert.That(input.SelectUnit(b.Enemy), Is.True); yield return null;
            foreach (var renderer in Marker.GetComponentsInChildren<Renderer>())
            { var color = renderer.sharedMaterial.color; Assert.That(color.r, Is.GreaterThan(color.g * 2)); }
            Assert.That(Marker.position.x, Is.EqualTo(b.Enemy.transform.position.x)); Assert.That(Marker.GetComponentsInChildren<Collider>(), Is.Empty);
        }
        [UnityTest] public IEnumerator FullScreenWorldBehindDebugPanelDoesNotReceiveClicks()
        {
            camera.rect = new Rect(0, 0, 1, 1);
            Assert.That(input.SelectUnit(b.Player), Is.True);
            b.Root.GetComponent<UnitActionUI>().enabled = false;
            var point = new Vector2(20, Screen.height - 40);
            Assert.That(info.ContainsScreenPoint(point), Is.True);
            float depth = camera.WorldToScreenPoint(b.Enemy.transform.position).z;
            camera.transform.position += b.Enemy.transform.position - camera.ScreenToWorldPoint(new Vector3(point.x, point.y, depth));
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(camera.ScreenPointToRay(point), out var hit, 1000), Is.True);
            Assert.That(hit.collider.GetComponentInParent<BattleUnit>(), Is.SameAs(b.Enemy), "An enemy must actually lie behind the panel.");
            yield return Click(b.Enemy.transform.position);
            Assert.That(input.SelectedUnit, Is.SameAs(b.Player));
            Assert.That(input.TargetUnit, Is.Null);
        }
        [UnityTest] public IEnumerator AttackModeEnemyClickStillTargetsWithoutReplacingActingAlly()
        {
            input.SelectUnit(b.Player); Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand), Is.True);
            yield return Click(b.Enemy.transform.position);
            Assert.That(input.SelectedUnit, Is.SameAs(b.Player)); Assert.That(input.TargetUnit, Is.SameAs(b.Enemy));
            Assert.That(b.Root.GetComponent<BodyTargetUI>().IsVisible, Is.True);
        }
        [UnityTest] public IEnumerator HiddenEnemiesCannotBeSelectedAndLostSightClearsInformationImmediately()
        {
            Assert.That(input.SelectUnit(b.Enemy), Is.True);
            b.Player.Vision.Configure(0); b.Turns.Visibility.Refresh();
            Assert.That(input.SelectedUnit, Is.Null); Assert.That(Read<BattleUnit>("DisplayedUnit"), Is.Null);
            Assert.That(Read<string>("MechaName"), Is.Empty); Assert.That(Marker.gameObject.activeSelf, Is.False);
            Assert.That(input.SelectUnit(b.Enemy), Is.False); yield return Click(b.Enemy.transform.position);
            Assert.That(input.SelectedUnit, Is.Null);
        }
        [UnityTest] public IEnumerator EscapeAndEmptyGroundClearEnemyInspection()
        {
            Assert.That(input.SelectUnit(b.Enemy), Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(input.SelectedUnit, Is.Null); Assert.That(Marker.gameObject.activeSelf, Is.False);
            Assert.That(input.SelectUnit(b.Enemy), Is.True); yield return Click(new Vector3(12, 0, 8));
            Assert.That(input.SelectedUnit, Is.Null); Assert.That(Read<string>("EquipmentText"), Is.Empty);
        }
        [UnityTest] public IEnumerator PortraitRendersPilotsSpriteWithCorrectUVAndOrientation()
        {
            var texture = new Texture2D(8, 5, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int x = 0; x < 8; x++) for (int y = 0; y < 5; y++)
                texture.SetPixel(x, y, x < 4 ? Color.green : y >= 2 ? Color.red : Color.blue);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(4, 0, 4, 5), Vector2.one * .5f);
            var pilot = ScriptableObject.CreateInstance<CharacterData>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(CharacterData).GetField("defaultPortrait", flags).SetValue(pilot, sprite);
            typeof(BattleUnit).GetField("pilotData", flags).SetValue(b.Enemy, pilot);
            Assert.That(input.SelectUnit(b.Enemy), Is.True);
            Assert.That(Read<Sprite>("PilotPortrait"), Is.SameAs(sprite));
            var source = b.Enemy.transform.Find("Visual").GetComponent<Renderer>(); var position = source.transform.position;
#if UNITY_EDITOR
            var previousFocus = UnityEditor.EditorWindow.focusedWindow;
            var view = (UnityEditor.EditorWindow)ScriptableObject.CreateInstance(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            view.position = new Rect(100, 100, 1280, 760); view.Show(); view.Focus();
#endif
            var image = new Texture2D(2, 2);
            try
            {
                yield return null; yield return null;
                Directory.CreateDirectory("Artifacts"); string path = Path.GetFullPath("Artifacts/unit-inspection-portrait.png");
                if (File.Exists(path)) File.Delete(path);
                ScreenCapture.CaptureScreenshot(path);
                float deadline = Time.realtimeSinceStartup + 5;
                while (!File.Exists(path) && Time.realtimeSinceStartup < deadline)
                {
#if UNITY_EDITOR
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif
                    yield return null;
                }
                Assert.That(File.Exists(path), Is.True); image.LoadImage(File.ReadAllBytes(path));
                int red = 0, blue = 0, green = 0;
                int minX = image.width, maxX = 0, minY = image.height, maxY = 0;
                double redY = 0, blueY = 0;
                for (int x = 0; x < image.width * .2f; x++) for (int y = 0; y < image.height; y++)
                {
                    var pixel = image.GetPixel(x, y);
                    bool isRed = pixel.r > .7f && pixel.g < .15f && pixel.b < .15f;
                    bool isBlue = pixel.b > .7f && pixel.g < .15f && pixel.r < .15f;
                    if (isRed) { red++; redY += y; }
                    if (isBlue) { blue++; blueY += y; }
                    if (pixel.g > .7f && pixel.r < .15f && pixel.b < .15f) green++;
                    if (isRed || isBlue)
                    { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
                }
                Assert.That(red, Is.GreaterThan(80)); Assert.That(blue, Is.GreaterThan(80));
                Assert.That(green, Is.Zero, "Pixels outside the Sprite's source rect must not be drawn.");
                Assert.That((maxX - minX + 1f) / (maxY - minY + 1f), Is.EqualTo(.8f).Within(.08f), "Preserve the portrait's 4:5 aspect ratio.");
                Assert.That(redY / red, Is.GreaterThan(blueY / blue), "Sprite's top red half must remain above its bottom blue half.");
                Assert.That(source.transform.position, Is.EqualTo(position)); Assert.That(source.sharedMaterial, Is.SameAs(modelMaterial));
                Assert.That(b.Root.GetComponentsInChildren<BattleUnit>().Length, Is.EqualTo(2));
                info.enabled = false; Assert.That(Read<Sprite>("PilotPortrait"), Is.Null);
            }
            finally
            {
#if UNITY_EDITOR
                view.Close(); if (previousFocus) previousFocus.Focus();
#endif
                Object.Destroy(image); Object.Destroy(pilot); Object.Destroy(sprite); Object.Destroy(texture);
            }
        }
        [UnityTest] public IEnumerator InformationTracksPartDamageAndDestroyedSelectionDisappears()
        {
            Assert.That(input.SelectUnit(b.Enemy), Is.True); b.Enemy.Health.ApplyDamage(TargetBodyPart.Head, 1);
            Assert.That(Read<string>("PartHealthText"), Does.Contain($"Head {b.Enemy.Health.GetCurrentHP(TargetBodyPart.Head)}/{b.Enemy.Health.GetMaxHP(TargetBodyPart.Head)}"));
            b.Enemy.Health.ApplyDamage(TargetBodyPart.Torso, int.MaxValue); yield return null;
            Assert.That(input.SelectedUnit, Is.Null); Assert.That(Read<string>("MechaName"), Is.Empty); Assert.That(Marker.gameObject.activeSelf, Is.False);
        }
    }
}
