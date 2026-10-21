using UnityEngine;
using System.Collections.Generic;
using System.Linq;
namespace MechaXCOM
{
    [DisallowMultipleComponent]
    public class FogOfWarPresenter : MonoBehaviour
    {
        [SerializeField, Range(.02f, 1f)] private float exploredBrightness = .6f;
        [SerializeField, Range(.001f, .99f)] private float unexploredBrightness = .3f;
        private GridManager grid;
        private VisibilitySystem visibility;
        private Renderer[] renderers;
        private readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        private readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
        private Texture2D states;
        private Color32[] pixels;
        private bool subscribed;

        public void Initialize(GridManager grid, VisibilitySystem visibility, Renderer[] terrainRenderers)
        {
            Cleanup();
            this.grid = grid;
            this.visibility = visibility;
            renderers = terrainRenderers == null ? new Renderer[0] : terrainRenderers.Where(r => r).Distinct().ToArray();
            if (isActiveAndEnabled) Build();
        }
        private void Build()
        {
            if (!grid || !visibility || states) return;
            // A Resources reference includes the shader in player builds as well as the Editor.
            var shader = Resources.Load<Shader>("Shaders/FogTerrain");
            if (!shader) { Debug.LogWarning("FogTerrain shader is missing.", this); return; }
            ValidateBrightness();
            states = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false, true)
            { name = "Battle visibility cells", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            pixels = new Color32[grid.Width * grid.Height];
            foreach (var renderer in renderers)
            {
                if (!renderer) continue;
                var source = renderer.sharedMaterials;
                originals.Add(renderer, source);
                var replacement = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    if (!source[i]) continue;
                    if (!copies.TryGetValue(source[i], out var material))
                    {
                        material = new Material(source[i]) { name = source[i].name + " (Battle Fog)", shader = shader };
                        material.SetTexture("_FogStates", states);
                        material.SetVector("_FogGrid", new Vector4(grid.transform.position.x, grid.transform.position.z,
                            grid.Width * grid.CellSize, grid.Height * grid.CellSize));
                        copies.Add(source[i], material);
                    }
                    replacement[i] = material;
                }
                renderer.sharedMaterials = replacement;
            }
            visibility.Changed += UpdateStates;
            subscribed = true;
            UpdateStates();
        }
        private void UpdateStates()
        {
            if (!states || !grid || !visibility) return;
            foreach (var cell in grid.GetCells())
            {
                var state = visibility.GetCellVisibility(UnitTeam.Player, cell.Position);
                byte value = state == CellVisibility.Visible ? (byte)255 : state == CellVisibility.Explored ? (byte)128 : (byte)0;
                pixels[cell.Position.y * grid.Width + cell.Position.x] = new Color32(value, value, value, 255);
            }
            states.SetPixels32(pixels); states.Apply(false, false);
            foreach (var material in copies.Values)
                material.SetVector("_FogBrightness", new Vector4(exploredBrightness, unexploredBrightness, 0, 0));
        }
        private void Cleanup()
        {
            if (subscribed && visibility) visibility.Changed -= UpdateStates;
            subscribed = false;
            foreach (var pair in originals)
                if (pair.Key) pair.Key.sharedMaterials = pair.Value;
            originals.Clear();
            foreach (var material in copies.Values) Release(material);
            copies.Clear();
            Release(states); states = null; pixels = null;
        }
        private static void Release(Object resource)
        {
            if (!resource) return;
            if (Application.isPlaying) Destroy(resource); else DestroyImmediate(resource);
        }
        private void ValidateBrightness()
        {
            exploredBrightness = Mathf.Clamp(exploredBrightness, .02f, 1f);
            unexploredBrightness = Mathf.Clamp(unexploredBrightness, .001f, exploredBrightness - .001f);
        }
        private void OnValidate() { ValidateBrightness(); UpdateStates(); }
        private void OnEnable() { if (renderers != null) Build(); }
        private void OnDisable() => Cleanup();
        private void OnDestroy() => Cleanup();
    }
}
