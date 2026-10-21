using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace MechaXCOM.Tests
{
    public class FogOfWarPresentationTests
    {
        private GameObject root;
        private GridManager grid;
        private VisibilitySystem visibility;
        private FogOfWarPresenter fog;
        private BattleUnit observer;
        private Renderer ground, wall;
        private Material original;
        private Camera camera;
#if UNITY_EDITOR
        private bool oldAsyncCompilation;
#endif
        [UnitySetUp] public IEnumerator Setup()
        {
#if UNITY_EDITOR
            oldAsyncCompilation = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
#endif
            root = new GameObject("Fog rendering tests");
            grid = root.AddComponent<GridManager>(); grid.Configure(8, 4, 2); grid.Generate();
            observer = new GameObject("Observer").AddTestBattleUnit(); observer.transform.SetParent(root.transform);
            observer.Configure(new Vector2Int(1, 1), 5); observer.Initialize(grid); observer.Vision.Configure(2);
            visibility = root.AddComponent<VisibilitySystem>(); visibility.Initialize(grid, new[] { observer });
            original = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            original.SetColor("_BaseColor", new Color(.9f, .2f, .1f));
            original.SetColor("_EmissionColor", new Color(.5f, .08f, .02f)); original.EnableKeyword("_EMISSION");
            ground = Surface("Ground", new Vector3(8, -.1f, 4), new Vector3(20, .2f, 8));
            wall = Surface("Long wall", new Vector3(8, .6f, 7), new Vector3(16, 1, 1));
            fog = root.AddComponent<FogOfWarPresenter>(); fog.Initialize(grid, visibility, new[] { ground, wall });
            grid.TryMoveUnit(observer, new Vector2Int(4, 1));
            camera = new GameObject("Fog camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
            camera.orthographic = true; camera.orthographicSize = 5; camera.aspect = 2;
            camera.transform.position = new Vector3(8, 25, 4); camera.transform.LookAt(new Vector3(8, 0, 4), Vector3.forward);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            yield return null;
        }
        private Renderer Surface(string name, Vector3 pos, Vector3 scale)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name; obj.transform.SetParent(root.transform);
            obj.transform.position = pos; obj.transform.localScale = scale;
            var renderer = obj.GetComponent<Renderer>(); renderer.sharedMaterial = original; return renderer;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
#if UNITY_EDITOR
            ShaderUtil.allowAsyncCompilation = oldAsyncCompilation;
#endif
            Object.Destroy(root); Object.Destroy(original); yield return null;
        }
        [Test] public void TerrainStaysRenderedInAllThreeStates()
        {
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, new Vector2Int(1, 1)), Is.EqualTo(CellVisibility.Explored));
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, new Vector2Int(4, 1)), Is.EqualTo(CellVisibility.Visible));
            Assert.That(visibility.GetCellVisibility(UnitTeam.Player, new Vector2Int(7, 1)), Is.EqualTo(CellVisibility.Unexplored));
            foreach (var renderer in new[] { ground, wall })
            {
                Assert.That(renderer.enabled, Is.True);
                Assert.That(renderer.sharedMaterial, Is.Not.SameAs(original));
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("MechaXCOM/FogTerrain"));
            }
        }
        [Test] public void FogDoesNotAffectUnitsUIOrOriginalMaterials()
        {
            var unitVisual = Surface("Unit visual", Vector3.up, Vector3.one);
            unitVisual.transform.SetParent(observer.transform);
            var uiVisual = Surface("Selection indicator", Vector3.zero, Vector3.one);
            var beforeColor = original.GetColor("_BaseColor");
            Assert.That(unitVisual.sharedMaterial, Is.SameAs(original));
            Assert.That(uiVisual.sharedMaterial, Is.SameAs(original));
            Assert.That(original.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(original.GetColor("_BaseColor"), Is.EqualTo(beforeColor));
            Assert.That(ground.sharedMaterial.GetColor("_BaseColor"), Is.EqualTo(beforeColor));
            Assert.That(ground.sharedMaterial.GetColor("_EmissionColor"), Is.EqualTo(original.GetColor("_EmissionColor")));
            Assert.That(ground.sharedMaterial.IsKeywordEnabled("_EMISSION"), Is.True);
        }
        [UnityTest] public IEnumerator CleanupRestoresReferencesAndReinitializeDoesNotLeakResources()
        {
            var firstMaterial = ground.sharedMaterial;
            Assert.That(firstMaterial, Is.Not.SameAs(original));
            var firstTexture = firstMaterial.GetTexture("_FogStates");
            Assert.That(firstTexture, Is.Not.Null);
            fog.Initialize(grid, visibility, new[] { ground, wall, ground });
            yield return null;
            Assert.That(firstMaterial == null, Is.True);
            Assert.That(firstTexture == null, Is.True);
            Assert.That(ground.sharedMaterial.GetTexture("_FogStates"), Is.SameAs(wall.sharedMaterial.GetTexture("_FogStates")));
            fog.enabled = false;
            Assert.That(ground.sharedMaterial, Is.SameAs(original));
            Assert.That(wall.sharedMaterial, Is.SameAs(original));
            fog.enabled = true;
            Assert.That(ground.sharedMaterial.shader.name, Is.EqualTo("MechaXCOM/FogTerrain"));
        }
        [UnityTest] public IEnumerator LargeSurfaceSamplesDifferentCellStates()
        {
            RequireGraphics();
            yield return null;
            var first = Render("fog-first-render.png");
            try { AssertThreeStates(first, new[] { new Vector3(3, 0, 3), new Vector3(9, 0, 5), new Vector3(15, 0, 3) }); }
            finally { Object.Destroy(first); }
            fog.enabled = false;
            var baseline = Render("fog-original-baseline.png");
            try { Assert.That(Pixel(baseline, new Vector3(9, 0, 5)).r, Is.GreaterThan(.1f)); }
            finally { Object.Destroy(baseline); }
            fog.enabled = true;
            yield return null;
            var image = Render("fog-three-states.png");
            try
            {
                AssertThreeStates(image, new[] { new Vector3(3, 0, 3), new Vector3(9, 0, 5), new Vector3(15, 0, 3) });
                AssertThreeStates(image, new[] { new Vector3(3, 1.1f, 7), new Vector3(9, 1.1f, 7), new Vector3(15, 1.1f, 7) });
                var outside = Pixel(image, new Vector3(-.5f, 0, 3));
                Assert.That(outside.r, Is.GreaterThan(outside.g * 1.5f), "Surfaces outside the grid keep their original color.");
            }
            finally { Object.Destroy(image); }
            camera.transform.position = new Vector3(8, 22, -12); camera.transform.LookAt(new Vector3(8, 0, 4));
            camera.orthographicSize = 6;
            yield return null;
            image = Render("fog-rotated-zoom.png");
            try { AssertThreeStates(image, new[] { new Vector3(3, 0, 3), new Vector3(9, 0, 3), new Vector3(15, 0, 3) }); }
            finally { Object.Destroy(image); }
        }
        [Test] public void InspectorBrightnessCannotHideTerrainOrReverseTheTwoGrayStates()
        {
            JsonUtility.FromJsonOverwrite("{\"exploredBrightness\":0.1,\"unexploredBrightness\":0.9}", fog);
            fog.Initialize(grid, visibility, new[] { ground, wall });
            var brightness = ground.sharedMaterial.GetVector("_FogBrightness");
            Assert.That(brightness.x, Is.GreaterThan(brightness.y));
            Assert.That(brightness.y, Is.GreaterThan(0));
        }
#if UNITY_EDITOR
        [UnityTest] public IEnumerator SavedSquadShowsTerrainFogAndPreservesUnitMaterials()
        {
            RequireGraphics();
            const string path = "Assets/_Game/Scenes/SqurdWeaponTest.unity";
            var saved = File.ReadAllBytes(path);
            EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            try
            {
                yield return null; yield return null;
                var sceneFog = Object.FindFirstObjectByType<FogOfWarPresenter>();
                Assert.That(sceneFog, Is.Not.Null);
                var sceneGround = GameObject.Find("Ground").GetComponent<Renderer>();
                Assert.That(sceneGround.sharedMaterial.shader.name, Is.EqualTo("MechaXCOM/FogTerrain"));
                foreach (var terrain in Object.FindObjectsByType<GridObstacle>(FindObjectsSortMode.None))
                    foreach (var renderer in terrain.GetComponentsInChildren<Renderer>())
                        Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("MechaXCOM/FogTerrain"));
                foreach (var cover in Object.FindObjectsByType<CoverObject>(FindObjectsSortMode.None))
                    foreach (var renderer in cover.GetComponentsInChildren<Renderer>())
                        Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("MechaXCOM/FogTerrain"));
                foreach (var unit in Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None))
                    foreach (var renderer in unit.GetComponentsInChildren<Renderer>(true))
                        Assert.That(renderer.sharedMaterial.shader.name, Is.Not.EqualTo("MechaXCOM/FogTerrain"));
                camera = Camera.main;
                Assert.That(camera, Is.Not.Null);
                var oldRect = camera.rect;
                try { camera.rect = new Rect(0, 0, 1, 1); Object.Destroy(Render("fog-squad-scene.png")); }
                finally { camera.rect = oldRect; }
            }
            finally
            {
                var oldScene = SceneManager.GetActiveScene();
                SceneManager.SetActiveScene(SceneManager.CreateScene("Fog scene cleanup"));
                SceneManager.UnloadSceneAsync(oldScene);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(saved));
            }
            yield return null;
        }
#endif
        private void AssertThreeStates(Texture2D image, Vector3[] points)
        {
            var explored = Pixel(image, points[0]); var visible = Pixel(image, points[1]); var unknown = Pixel(image, points[2]);
            Assert.That(visible.r, Is.GreaterThan(visible.g * 1.5f));
            Assert.That(Mathf.Abs(explored.r - explored.g), Is.LessThan(.04f));
            Assert.That(Mathf.Abs(unknown.r - unknown.g), Is.LessThan(.04f));
            Assert.That(explored.r, Is.GreaterThan(unknown.r + .05f));
            Assert.That(unknown.r, Is.GreaterThan(.05f), "Unexplored terrain remains readable.");
        }
        private Color Pixel(Texture2D image, Vector3 position)
        {
            var p = camera.WorldToViewportPoint(position);
            Assert.That(p.x, Is.InRange(0f, 1f)); Assert.That(p.y, Is.InRange(0f, 1f));
            return image.GetPixel(Mathf.Clamp((int)(p.x * image.width), 0, image.width - 1),
                Mathf.Clamp((int)(p.y * image.height), 0, image.height - 1));
        }
        private void RequireGraphics()
        { if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Run with -Graphics to verify pixels."); }
        private Texture2D Render(string name)
        {
            var target = RenderTexture.GetTemporary(800, 400, 24, RenderTextureFormat.ARGB32);
            var old = RenderTexture.active;
            var image = new Texture2D(800, 400, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 800, 400), 0, 0); image.Apply();
                Directory.CreateDirectory("Artifacts"); File.WriteAllBytes("Artifacts/" + name, image.EncodeToPNG());
            }
            finally { RenderTexture.active = old; RenderTexture.ReleaseTemporary(target); }
            return image;
        }
    }
}
