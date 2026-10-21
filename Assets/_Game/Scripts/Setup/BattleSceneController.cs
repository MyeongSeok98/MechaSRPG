using UnityEngine;
using System.Collections.Generic;

namespace MechaXCOM
{
    public class BattleSceneController : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        [SerializeField] private CoverSystem coverSystem;
        [SerializeField] private BattleUnit[] units;
        [SerializeField] private GridObstacle[] obstacles;
        [SerializeField] private CoverObject[] covers;
        [SerializeField] private Camera battleCamera;
        [SerializeField] private PlayerInputController inputController;
        [SerializeField] private GridVisualizer visualizer;
        [SerializeField] private BattleDebugUI debugUI;
        [SerializeField] private CombatSystem combatSystem;
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private EnemyAIController enemyAI;
        private bool initialized;
        public void ConfigureCombat(CombatSystem combat, TurnManager turns)
        { combatSystem = combat; turnManager = turns; }
        public void ConfigureEnemyAI(EnemyAIController ai) => enemyAI = ai;

        public void ConfigurePresentation(Camera camera, PlayerInputController input,
            GridVisualizer visualizer, BattleDebugUI debugUI)
        {
            battleCamera = camera;
            inputController = input;
            this.visualizer = visualizer;
            this.debugUI = debugUI;
        }

        public void Configure(GridManager grid, CoverSystem coverSystem, BattleUnit[] units,
            GridObstacle[] obstacles, CoverObject[] covers)
        {
            this.grid = grid;
            this.coverSystem = coverSystem;
            this.units = units;
            this.obstacles = obstacles;
            this.covers = covers;
        }
        private void Start() => InitializeBattle();
        public bool InitializeBattle()
        {
            if (initialized) return true;
            if (!grid || !coverSystem)
            {
                Debug.LogWarning($"{name}: missing GridManager or CoverSystem; battle initialization stopped.", this);
                return false;
            }
            grid.Generate();
            if (battleCamera && battleCamera.TryGetComponent<BattleCameraOrbit>(out var cameraOrbit))
                cameraOrbit.Initialize(grid);
            if (obstacles != null)
                foreach (var obstacle in obstacles)
                    if (obstacle && obstacle.isActiveAndEnabled) grid.RegisterObstacle(obstacle.Collider);
            coverSystem.Build(grid, covers);
            if (units != null)
                foreach (var unit in units)
                    if (unit && unit.isActiveAndEnabled) unit.Initialize(grid);
            var visibility = GetComponent<VisibilitySystem>() ?? gameObject.AddComponent<VisibilitySystem>();
            visibility.Initialize(grid, units);
            if (turnManager) turnManager.ConfigureVisibility(visibility);
            if (units != null)
                foreach (var unit in units)
                    if (unit && unit.IsInitialized)
                    {
                        var presenter = unit.GetComponent<UnitVisibilityPresenter>() ??
                            unit.gameObject.AddComponent<UnitVisibilityPresenter>();
                        presenter.Initialize(unit, visibility);
                    }
            if (combatSystem) combatSystem.Initialize(grid, coverSystem, turnManager);
            if (combatSystem && turnManager)
            {
                // Older saved scenes keep their authored references; fill the missing AI at runtime.
                if (!enemyAI) enemyAI = turnManager.GetComponent<EnemyAIController>() ??
                    turnManager.gameObject.AddComponent<EnemyAIController>();
                enemyAI.Initialize(grid, combatSystem, turnManager);
                turnManager.ConfigureEnemyAI(enemyAI);
                var overwatch = GetComponent<OverwatchSystem>() ?? gameObject.AddComponent<OverwatchSystem>();
                overwatch.Initialize(grid, combatSystem, turnManager, units);
            }
            if (turnManager) turnManager.Initialize(units);
            var fog = GetComponent<FogOfWarPresenter>() ?? gameObject.AddComponent<FogOfWarPresenter>();
            fog.Initialize(grid, visibility, CollectTerrainRenderers());
            if (visualizer) visualizer.Initialize(grid, turnManager);
            if (debugUI) debugUI.Initialize(grid, coverSystem);
            if (inputController) inputController.Initialize(battleCamera, grid, visualizer, debugUI);
            if (inputController && combatSystem && turnManager) inputController.InitializeCombat(combatSystem, turnManager);
            if (debugUI && inputController && turnManager) debugUI.InitializeCombat(inputController, turnManager);
            initialized = true;
            return true;
        }
        private Renderer[] CollectTerrainRenderers()
        {
            var result = new HashSet<Renderer>();
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var node in root.GetComponentsInChildren<Transform>(true))
                    if (node.name == "Ground") AddTerrain(node.gameObject, result);
            if (obstacles != null)
                foreach (var obstacle in obstacles) if (obstacle) AddTerrain(obstacle.gameObject, result);
            if (covers != null)
                foreach (var cover in covers) if (cover) AddTerrain(cover.gameObject, result);
            return new List<Renderer>(result).ToArray();
        }
        private static void AddTerrain(GameObject terrain, HashSet<Renderer> result)
        {
            foreach (var renderer in terrain.GetComponentsInChildren<Renderer>(true))
                if (!renderer.GetComponentInParent<BattleUnit>() && !renderer.GetComponentInParent<GridVisualizer>())
                    result.Add(renderer);
        }
    }
}
