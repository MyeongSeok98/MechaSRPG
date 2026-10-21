using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM
{
    public class GridVisualizer : MonoBehaviour
    {
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Material rangeMaterial;
        [SerializeField] private Material selectionMaterial;
        [SerializeField] private Material attackMaterial;
        [SerializeField] private Material overwatchMaterial;
        [SerializeField] private Material enemySelectionMaterial;
        private readonly List<Material> ownedMaterials = new List<Material>();
        private GridManager grid;
        private Transform lines;
        private Transform ranges;
        private Transform indicator;
        private Transform enemyIndicator;
        private TurnManager turns;
        private VisibilitySystem visibility;
        private BattleUnit selected;

        public void ConfigureMaterials(Material lines, Material range, Material selection)
        {
            lineMaterial = lines;
            rangeMaterial = range;
            selectionMaterial = selection;
        }
        public void Initialize(GridManager grid, TurnManager turns = null)
        {
            UnsubscribeVisibility();
            this.turns = turns;
            visibility = turns ? turns.Visibility : null;
            if (isActiveAndEnabled && visibility) visibility.Changed += UpdateEnemyIndicator;
            if (!grid || lines) return;
            this.grid = grid;
            lineMaterial = EnsureMaterial(lineMaterial, new Color(0.18f, 0.22f, 0.18f));
            rangeMaterial = EnsureMaterial(rangeMaterial, new Color(0.25f, 0.52f, 0.49f));
            selectionMaterial = EnsureMaterial(selectionMaterial, new Color(1f, 0.78f, 0.24f));
            attackMaterial = EnsureMaterial(attackMaterial, new Color(0.78f, 0.13f, 0.12f));
            overwatchMaterial = EnsureMaterial(overwatchMaterial, new Color(.95f, .55f, .12f));
            enemySelectionMaterial = EnsureMaterial(enemySelectionMaterial, new Color(0.95f, 0.12f, 0.1f));
            lines = MakeRoot("Grid Lines");
            ranges = MakeRoot("Reachable Cells");
            indicator = MakeIndicator("Selection Indicator", selectionMaterial);
            enemyIndicator = MakeIndicator("Enemy Action Indicator", enemySelectionMaterial);
            var origin = grid.transform.position;
            float width = grid.Width * grid.CellSize;
            float depth = grid.Height * grid.CellSize;
            for (int x = 0; x <= grid.Width; x++)
                MakeCube(lines, origin + new Vector3(x * grid.CellSize, 0.025f, depth / 2),
                    new Vector3(0.025f, 0.02f, depth), lineMaterial);
            for (int z = 0; z <= grid.Height; z++)
                MakeCube(lines, origin + new Vector3(width / 2, 0.025f, z * grid.CellSize),
                    new Vector3(width, 0.02f, 0.025f), lineMaterial);
            ClearReachableCells();
        }
        private Transform MakeIndicator(string name, Material material)
        {
            var marker = MakeRoot(name);
            float side = grid.CellSize * 0.72f;
            MakeCube(marker, Vector3.zero, new Vector3(side, 0.03f, 0.08f), material).localPosition = new Vector3(0, 0, side / 2);
            MakeCube(marker, Vector3.zero, new Vector3(side, 0.03f, 0.08f), material).localPosition = new Vector3(0, 0, -side / 2);
            MakeCube(marker, Vector3.zero, new Vector3(0.08f, 0.03f, side), material).localPosition = new Vector3(side / 2, 0, 0);
            MakeCube(marker, Vector3.zero, new Vector3(0.08f, 0.03f, side), material).localPosition = new Vector3(-side / 2, 0, 0);
            marker.gameObject.SetActive(false);
            return marker;
        }
        private Transform MakeRoot(string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            return root;
        }
        private Transform MakeCube(Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Display";
            cube.transform.SetParent(parent, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            var collider = cube.GetComponent<Collider>();
            collider.enabled = false;
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            return cube.transform;
        }
        private Material EnsureMaterial(Material material, Color color)
        {
            if (material) return material;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (!shader) return null;
            var created = new Material(shader) { color = color };
            ownedMaterials.Add(created);
            return created;
        }
        public void ShowReachableCells(List<GridCell> cells)
            => ShowRangeCells(cells, rangeMaterial);
        public void ShowAttackCells(List<GridCell> cells)
            => ShowRangeCells(cells, attackMaterial);
        public void ShowOverwatchCells(List<GridCell> cells)
            => ShowRangeCells(cells, overwatchMaterial);
        private void ShowRangeCells(List<GridCell> cells, Material material)
        {
            if (!ranges || !grid) return;
            while (ranges.childCount > 0)
            {
                var child = ranges.GetChild(0).gameObject;
                child.transform.SetParent(null);
                child.SetActive(false);
                Destroy(child);
            }
            if (cells != null)
                foreach (var cell in cells)
                    MakeCube(ranges, cell.WorldPosition + Vector3.up * 0.045f,
                        new Vector3(grid.CellSize * 0.84f, 0.025f, grid.CellSize * 0.84f), material);
            ranges.gameObject.SetActive(cells != null && cells.Count > 0);
        }
        public void ClearReachableCells()
        {
            if (ranges) ranges.gameObject.SetActive(false);
        }
        public void SetSelectedUnit(BattleUnit unit)
        {
            selected = unit;
            if (indicator) indicator.gameObject.SetActive(unit != null);
            UpdateIndicator();
        }
        private void LateUpdate()
        {
            UpdateIndicator();
            UpdateEnemyIndicator();
        }
        private void UpdateEnemyIndicator()
        {
            if (!enemyIndicator || !grid) return;
            var actor = turns && turns.isActiveAndEnabled && !turns.IsPlayerTurn && !turns.IsBattleOver
                ? turns.ActiveEnemy : null;
            bool visible = actor && actor.isActiveAndEnabled && actor.IsAlive && actor.Health.CanFight && actor.Team == UnitTeam.Enemy &&
                visibility && visibility.IsVisibleTo(UnitTeam.Player, actor);
            enemyIndicator.gameObject.SetActive(visible);
            if (!visible) return;
            var position = actor.transform.position;
            position.y = grid.transform.position.y + 0.085f;
            enemyIndicator.position = position;
        }
        private void OnDisable()
        {
            UnsubscribeVisibility();
            if (enemyIndicator) enemyIndicator.gameObject.SetActive(false);
        }
        private void OnEnable()
        {
            if (visibility) visibility.Changed += UpdateEnemyIndicator;
            UpdateEnemyIndicator();
        }
        private void UnsubscribeVisibility()
        {
            if (visibility) visibility.Changed -= UpdateEnemyIndicator;
        }
        private void UpdateIndicator()
        {
            if (!indicator || !grid) return;
            bool visible = selected && selected.isActiveAndEnabled && selected.IsAlive;
            indicator.gameObject.SetActive(visible);
            if (!visible) return;
            var position = selected.transform.position;
            position.y = grid.transform.position.y + 0.085f;
            indicator.position = position;
        }
        private void OnDestroy()
        {
            UnsubscribeVisibility();
            foreach (var material in ownedMaterials)
                if (material) Destroy(material);
        }
    }
}
