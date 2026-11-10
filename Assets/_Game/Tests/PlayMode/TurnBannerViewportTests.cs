#if UNITY_EDITOR
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class TurnBannerViewportTests
    {
        private UnityEditor.EditorWindow captureView, previousFocus;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (captureView) captureView.Close();
            if (previousFocus) previousFocus.Focus();
            captureView = null; previousFocus = null;
            var old = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Viewport cleanup"));
            yield return SceneManager.UnloadSceneAsync(old);
        }
        [UnityTest] public IEnumerator OneVsOneClearsBannerPixelsOutsideCamera() => Check("1V1Test");
        [UnityTest] public IEnumerator SquadClearsBannerPixelsOutsideCamera() => Check("SqurdWeaponTest");

        private IEnumerator Check(string scene)
        {
            // Own a temporary capture window, so later tests retain their original Game View.
            previousFocus = UnityEditor.EditorWindow.focusedWindow;
            captureView = (UnityEditor.EditorWindow)ScriptableObject.CreateInstance(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            captureView.position = new Rect(100, 100, 1280, 760);
            captureView.Show(); captureView.Focus();
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Game/Scenes/" + scene + ".unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            var turns = Object.FindFirstObjectByType<TurnManager>();
            yield return TurnBannerTestSupport.Finish(turns);
            Object.FindFirstObjectByType<BattleDebugUI>().enabled = false;
            var camera = Object.FindFirstObjectByType<BattleCameraOrbit>().GetComponent<Camera>();
            Assert.That(camera.rect, Is.EqualTo(new Rect(0, 0, 1, 1)));
            // Saved scenes use full screen. Exercise legacy gutter clearing only in this temporary test instance.
            camera.rect = new Rect(.23f, 0, .77f, 1);
            var savedRect = camera.rect;
            var probe = new GameObject("Gutter pixel seed").AddComponent<TurnBannerGutterSeed>();
            yield return Capture(scene + "-seed");
            AssertGutter(scene + "-seed", Color.magenta);
            probe.enabled = false;
            // A full-screen overlay leaves pixels here unless the viewport gutter is redrawn.
            yield return null; yield return null;
            yield return Capture(scene + "-cleared");
            Object.Destroy(probe.gameObject);
            AssertGutter(scene + "-cleared", Color.black);
            Assert.That(camera.rect, Is.EqualTo(savedRect), "Keep the user's viewport unchanged.");
            var banner = Object.FindFirstObjectByType<TurnBannerPresenter>();
            JsonUtility.FromJsonOverwrite("{\"holdDuration\":1}", banner);
            Assert.That(turns.TryEndTurn(), Is.True);
            float deadline = Time.realtimeSinceStartup + 2;
            while (banner.CurrentFrame.Phase != TurnBannerPhase.Hold && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(banner.CurrentFrame.Phase, Is.EqualTo(TurnBannerPhase.Hold));
            yield return Capture(scene + "-banner");
            var bannerImage = new Texture2D(2, 2);
            try
            {
                bannerImage.LoadImage(File.ReadAllBytes(PathFor(scene + "-banner")));
                var pixel = bannerImage.GetPixel(bannerImage.width / 10, bannerImage.height / 2);
                Assert.That(pixel.r + pixel.g + pixel.b, Is.GreaterThan(.1f), "Background must remain behind the visible banner.");
            }
            finally { Object.Destroy(bannerImage); }
            banner.enabled = false;
            yield return null; yield return null;
            yield return Capture(scene + "-banner-removed");
            AssertGutter(scene + "-banner-removed", Color.black);
        }
        private static string PathFor(string name) => Path.GetFullPath("Artifacts/turn-banner-" + name + ".png");
        private static IEnumerator Capture(string name)
        {
            Directory.CreateDirectory("Artifacts");
            var path = PathFor(name);
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            float deadline = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline)
            { UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); yield return null; }
            Assert.That(File.Exists(path), Is.True);
        }
        private static void AssertGutter(string name, Color expected)
        {
            var image = new Texture2D(2, 2);
            try
            {
                image.LoadImage(File.ReadAllBytes(PathFor(name)));
                var actual = image.GetPixel(image.width / 10, image.height / 2);
                Assert.That(Vector3.Distance(new Vector3(actual.r, actual.g, actual.b),
                    new Vector3(expected.r, expected.g, expected.b)), Is.LessThan(.05f),
                    "Pixels outside the camera must be refreshed after the overlay disappears.");
            }
            finally { Object.Destroy(image); }
        }
    }
    public class TurnBannerGutterSeed : MonoBehaviour
    {
        private void OnGUI()
        {
            var color = GUI.color;
            GUI.depth = -2000; GUI.color = Color.magenta;
            GUI.DrawTexture(new Rect(0, 0, Screen.width * .2f, Screen.height), Texture2D.whiteTexture);
            GUI.color = color;
        }
    }
}
#endif
