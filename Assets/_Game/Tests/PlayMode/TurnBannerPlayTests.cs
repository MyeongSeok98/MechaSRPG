using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class TurnBannerPlayTests
    {
        private BackpackTestBattle b;
        private Camera camera;
        private BattleCameraOrbit orbit;
        private PlayerInputController input;
        private Component banner;
        private Mouse mouse;
        private Keyboard keyboard;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private InputSettings.BackgroundBehavior oldBackground;
        [UnitySetUp] public IEnumerator Setup()
        {
            b = new BackpackTestBattle();
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode; oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            b.Player.gameObject.AddComponent<CapsuleCollider>(); b.Enemy.gameObject.AddComponent<CapsuleCollider>();
            var visual = b.Root.AddComponent<GridVisualizer>(); visual.Initialize(b.Grid, b.Turns);
            var debug = b.Root.AddComponent<BattleDebugUI>(); debug.Initialize(b.Grid, b.Cover); debug.enabled = false;
            camera = new GameObject("Banner camera").AddComponent<Camera>(); camera.transform.SetParent(b.Root.transform);
            camera.orthographic = true; camera.orthographicSize = 16;
            camera.transform.position = new Vector3(12, 26, -18); camera.transform.LookAt(new Vector3(12, 0, 12));
            orbit = camera.gameObject.AddComponent<BattleCameraOrbit>(); orbit.Initialize(b.Grid);
            input = b.Root.AddComponent<PlayerInputController>(); input.Initialize(camera, b.Grid, visual, debug); input.InitializeCombat(b.Combat, b.Turns);
            mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            Assert.That(input.SelectUnit(b.Player), Is.True); Physics.SyncTransforms(); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor; InputSystem.settings.backgroundBehavior = oldBackground;
            b.Dispose(); yield return null;
        }
        private void Attach(bool fast = true)
        {
            var type = typeof(TurnManager).Assembly.GetType("MechaXCOM.TurnBannerPresenter"); Assert.That(type, Is.Not.Null);
            var bind = typeof(BattleCameraOrbit).GetMethod("ConfigureTurns"); Assert.That(bind, Is.Not.Null); bind.Invoke(orbit, new object[] { b.Turns });
            banner = b.Root.AddComponent(type);
            if (fast) JsonUtility.FromJsonOverwrite("{\"slideInDuration\":0.06,\"holdDuration\":0.12,\"slideOutDuration\":0.06}", banner);
            type.GetMethod("Initialize").Invoke(banner, new object[] { b.Turns });
        }
        private T Read<T>(string name) => (T)banner.GetType().GetProperty(name).GetValue(banner);
        private bool Locked => (bool)typeof(TurnManager).GetProperty("IsPresentingTurn").GetValue(b.Turns);
        private IEnumerator Finish()
        {
            float end = Time.realtimeSinceStartup + 3;
            while (Read<bool>("IsVisible") && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(Read<bool>("IsVisible"), Is.False); Assert.That(Locked, Is.False);
        }
        [UnityTest] public IEnumerator InitialPlayerBannerUsesYourTurnAndBlocksSelectionActionsEscapeAndCamera()
        {
            Attach(false); Assert.That(Read<Texture2D>("CurrentTexture").name, Is.EqualTo("YourTurn"));
            Assert.That(Read<UnitTeam>("CurrentTeam"), Is.EqualTo(UnitTeam.Player)); Assert.That(Locked, Is.True);
            Assert.That(input.CanChooseAction || input.BeginMove() || input.OpenAttackMenu() || input.CanOpenReloadMenu || input.CanOpenOverwatchMenu || input.TryEjectSelected(), Is.False);
            Assert.That(input.SelectUnit(b.Enemy) || input.TryEndTurn(), Is.False);
            var position = camera.transform.position; var rotation = camera.transform.rotation; float zoom = camera.orthographicSize;
            orbit.RotateLeft(); Assert.That(orbit.TryFocus(b.Enemy.transform.position), Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape, Key.W, Key.Q));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = camera.WorldToScreenPoint(b.Enemy.transform.position), scroll = new Vector2(0, 120) }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
            Assert.That(input.SelectedUnit, Is.SameAs(b.Player)); Assert.That(camera.transform.position, Is.EqualTo(position));
            Assert.That(camera.transform.rotation, Is.EqualTo(rotation)); Assert.That(camera.orthographicSize, Is.EqualTo(zoom));
            yield return Finish(); Assert.That(input.CanChooseAction, Is.True);
        }
        [UnityTest] public IEnumerator EnemyBannerDelaysAIAndNextPlayerTurnShowsYourTurnAgain()
        {
            Attach(); yield return Finish();
            var ai = b.Root.AddComponent<EnemyAIController>(); ai.Initialize(b.Grid, b.Combat, b.Turns); b.Turns.ConfigureEnemyAI(ai);
            int hp = b.Player.Health.GetCurrentHP(TargetBodyPart.Torso);
            Assert.That(b.Turns.TryEndTurn(), Is.True); Assert.That(Read<Texture2D>("CurrentTexture").name, Is.EqualTo("EnemyTurn"));
            Assert.That(Read<UnitTeam>("CurrentTeam"), Is.EqualTo(UnitTeam.Enemy));
            float bannerEnd = Time.realtimeSinceStartup + 3;
            while (Read<bool>("IsVisible") && Read<UnitTeam>("CurrentTeam") == UnitTeam.Enemy && Time.realtimeSinceStartup < bannerEnd)
            {
                Assert.That(b.Enemy.ActionPoints.CurrentPoints, Is.EqualTo(5)); Assert.That(b.Player.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(hp));
                Assert.That(b.Turns.ActiveEnemy, Is.Null); yield return null;
            }
            float end = Time.realtimeSinceStartup + 3;
            while (!b.Turns.IsPlayerTurn && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(b.Turns.IsPlayerTurn, Is.True); Assert.That(Read<bool>("IsVisible"), Is.True);
            Assert.That(Read<Texture2D>("CurrentTexture").name, Is.EqualTo("YourTurn")); Assert.That(input.CanChooseAction, Is.False);
            yield return Finish(); Assert.That(b.Player.ActionPoints.CurrentPoints, Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator DisablingDestroyingAndReenablingCannotLeaveAStaleLockOrReplay()
        {
            Attach(); ((Behaviour)banner).enabled = false; Assert.That(Locked, Is.False); Assert.That(Read<bool>("IsVisible"), Is.False);
            ((Behaviour)banner).enabled = true; yield return null; Assert.That(Read<bool>("IsVisible"), Is.False);
            b.StartEnemyTeam(); Assert.That(Read<bool>("IsVisible"), Is.True);
            Object.Destroy(banner); yield return null; Assert.That(Locked, Is.False);
        }
        [UnityTest] public IEnumerator InitializingAgainDoesNotRestartCurrentBanner()
        {
            Attach(false); float end = Time.realtimeSinceStartup + 2;
            while (Read<object>("CurrentFrame").GetType().GetProperty("Phase").GetValue(Read<object>("CurrentFrame")).ToString() != "Hold" && Time.realtimeSinceStartup < end) yield return null;
            banner.GetType().GetMethod("Initialize").Invoke(banner, new object[] { b.Turns });
            Assert.That(Read<object>("CurrentFrame").GetType().GetProperty("Phase").GetValue(Read<object>("CurrentFrame")).ToString(), Is.EqualTo("Hold"));
            yield return Finish();
            banner.GetType().GetMethod("Initialize").Invoke(banner, new object[] { b.Turns }); Assert.That(Read<bool>("IsVisible"), Is.False);
        }
        [UnityTest] public IEnumerator BannerUsesUnscaledTimeAndPausesThenResumesExistingCameraMotion()
        {
            orbit.RotateLeft(); var original = camera.transform.rotation; Attach(); float previous = Time.timeScale;
            try
            {
                Time.timeScale = 0; yield return null; Assert.That(camera.transform.rotation, Is.EqualTo(original));
                yield return Finish(); yield return new WaitForSecondsRealtime(.6f);
                Assert.That(camera.transform.rotation, Is.Not.EqualTo(original));
            }
            finally { Time.timeScale = previous; }
        }
        [UnityTest] public IEnumerator BattleEndCancelsBannerAndLock()
        {
            Attach(); b.Enemy.Health.ApplyDamage(TargetBodyPart.Torso, int.MaxValue);
            Assert.That(b.Turns.IsBattleOver, Is.True); Assert.That(Read<bool>("IsVisible"), Is.False); Assert.That(Locked, Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator MissingImageDoesNotCreateInvisibleInputLock()
        {
            Attach(); yield return Finish();
            banner.GetType().GetField("enemyTurnImage", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(banner, null);
            b.StartEnemyTeam(); Assert.That(Read<bool>("IsVisible"), Is.False); Assert.That(Locked, Is.False); yield return null;
        }
        [UnityTest] public IEnumerator EnemyBannerDoesNotCancelExistingPlayerOverwatch()
        {
            Attach(); yield return Finish();
            Assert.That(b.Overwatch.TryBegin(b.Player, WeaponSlot.RightHand, FacingDirection.North), Is.True);
            b.StartEnemyTeam(); Assert.That(Locked, Is.True);
            Assert.That(b.Player.Overwatch.IsActive, Is.True, "A temporary turn banner must not invalidate an armed guard.");
            yield return Finish(); Assert.That(b.Player.Overwatch.IsActive, Is.True);
        }
    }
}
