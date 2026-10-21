using UnityEngine;

namespace MechaXCOM
{
    [RequireComponent(typeof(UnitMovement), typeof(UnitMobility))]
    [RequireComponent(typeof(UnitHealth), typeof(UnitActionPoints), typeof(UnitCombat))]
    public class BattleUnit : MonoBehaviour
    {
        [SerializeField, InspectorName("기체 데이터")] private MechaData mechaData;
        public MechaData MechaData => mechaData;
        [SerializeField, Min(0)] private int movementRange = 5;
        [SerializeField] private Vector2Int startGridPosition = new Vector2Int(1, 1);
        [SerializeField, Min(0)] private float heightOffset = 1f;
        [SerializeField] private UnitTeam team = UnitTeam.Player;
        [SerializeField, Range(0, 1)] private float destroyedHeadHitMultiplier = .75f;
        private GridManager grid;
        private TurnManager turns;
        public event System.Action VisibilityChanged;
        public FacingDirection Facing { get; internal set; }
        public bool HasPendingFacing { get; internal set; }
        public bool IsConfirmingFacing { get; internal set; }
        internal bool RequiresArrivalFacing => turns && turns.isActiveAndEnabled && turns.IsPlayerTurn &&
            !turns.IsBattleOver && Team == UnitTeam.Player && Health.CanFight;
        public bool CanResolveFacing => isActiveAndEnabled && IsAlive && Health.CanFight && HasPendingFacing &&
            !IsConfirmingFacing && Movement && Movement.isActiveAndEnabled &&
            (!turns || (turns.isActiveAndEnabled && turns.IsPlayerTurn && !turns.IsBattleOver));
        public bool CanTakeAction => IsAlive && Health.CanFight && !(Overwatch && Overwatch.IsActive) &&
            !HasPendingFacing && (!turns || turns.CanAct(this));
        public bool CanEject => IsInitialized && turns && turns.CanEject(this);
        internal bool CanContinueMovement => isActiveAndEnabled && IsAlive && Health.isActiveAndEnabled && Health.CanFight &&
            (!turns || (turns.isActiveAndEnabled && !turns.IsBattleOver && turns.ActiveTeam == Team));
        internal bool IsRegisteredOnGrid => grid && grid.TryGetCell(CurrentGridPosition, out var cell) && cell.OccupyingUnit == this;
        public bool TryEject()
        {
            if (!CanEject || !ActionPoints.TrySpend(1)) return false;
            return Health.MarkEjected();
        }
        public void BindTurnManager(TurnManager turns) => this.turns = turns;
        public int MovementRange => movementRange;
        public int DamagedMovementRange
        {
            get
            {
                if (!IsInitialized || !Health) return movementRange;
                bool left = Health.GetCurrentHP(TargetBodyPart.LeftLeg) == 0;
                bool right = Health.GetCurrentHP(TargetBodyPart.RightLeg) == 0;
                if (left && right) return 0;
                return (left || right) && movementRange > 0 ? Mathf.Max(1, movementRange / 2) : movementRange;
            }
        }
        public float HeadHitMultiplier => IsInitialized && Health && Health.GetCurrentHP(TargetBodyPart.Head) == 0
            ? destroyedHeadHitMultiplier : 1f;
        public Vector2Int StartGridPosition => startGridPosition;
        public Vector2Int CurrentGridPosition { get; private set; }
        public UnitMovement Movement { get; private set; }
        public UnitMobility Mobility { get; private set; }
        public UnitHealth Health { get; private set; }
        public UnitActionPoints ActionPoints { get; private set; }
        public UnitCombat Combat { get; private set; }
        public UnitVision Vision { get; private set; }
        public UnitOverwatch Overwatch { get; private set; }
        public UnitTeam Team => team;
        public bool IsAlive => IsInitialized && Health && Health.IsPresent;
        public int AvailableMovementRange => IsAlive && Health.CanFight && ActionPoints ? Mathf.Min(DamagedMovementRange, ActionPoints.CurrentPoints) : 0;
        public void ConfigureTeam(UnitTeam team)
        {
            this.team = team;
            VisibilityChanged?.Invoke();
        }
        public int MovementEvasion => Mobility ? Mobility.MovementEvasion : 0;
        public bool IsInitialized { get; private set; }
        public float HeightOffset => heightOffset;
        public bool Initialize(GridManager grid)
        {
            if (IsInitialized) return this.grid == grid;
            if (mechaData && !mechaData.IsValid(out var reason))
            {
                Debug.LogWarning($"{name}: invalid MechaData: {reason}", this);
                return false;
            }
            Movement = GetComponent<UnitMovement>();
            Mobility = GetComponent<UnitMobility>();
            Health = GetComponent<UnitHealth>();
            ActionPoints = GetComponent<UnitActionPoints>();
            Combat = GetComponent<UnitCombat>();
            Vision = GetComponent<UnitVision>() ?? gameObject.AddComponent<UnitVision>();
            if (!grid || !Movement || !Mobility || !Health || !ActionPoints || !Combat ||
                !grid.TryPlaceUnit(this, startGridPosition))
            {
                Debug.LogWarning($"{name}: cannot initialize unit at {startGridPosition}; check grid, occupancy and components.", this);
                return false;
            }
            this.grid = grid;
            transform.position = grid.GetWorldPosition(CurrentGridPosition) + Vector3.up * heightOffset;
            Mobility.ResetMovement();
            if (mechaData)
            {
                for (int part = (int)TargetBodyPart.Torso; part <= (int)TargetBodyPart.RightLeg; part++)
                    Health.ConfigurePartHP((TargetBodyPart)part, mechaData.GetMaxHP((TargetBodyPart)part));
                ActionPoints.Configure(mechaData.BaseAP);
            }
            Health.Initialize();
            ActionPoints.ResetForTurn();
            Health.StateChanged += HandleStateChanged;
            Health.PartDestroyed += HandlePartDestroyed;
            var mechaVisual = GetComponent<UnitMechaVisual>();
            if (!mechaVisual && mechaData && mechaData.ModelPrefab) mechaVisual = gameObject.AddComponent<UnitMechaVisual>();
            if (mechaVisual) mechaVisual.Initialize(mechaData ? mechaData.ModelPrefab : null, heightOffset);
            Movement.Initialize(grid, this);
            IsInitialized = true;
            Overwatch = GetComponent<UnitOverwatch>() ?? gameObject.AddComponent<UnitOverwatch>();
            Overwatch.Initialize(this);
            VisibilityChanged?.Invoke();
            return true;
        }
        private void OnDestroy()
        {
            if (Health) Health.StateChanged -= HandleStateChanged;
            if (Health) Health.PartDestroyed -= HandlePartDestroyed;
            if (grid) grid.ReleaseUnit(this);
            IsInitialized = false;
            VisibilityChanged?.Invoke();
            if (turns) turns.CheckBattleEnd();
        }
        private void OnEnable() => VisibilityChanged?.Invoke();
        private void OnDisable()
        {
            if (Overwatch) Overwatch.Cancel();
            if (Movement) Movement.CancelMove();
            VisibilityChanged?.Invoke();
        }
        private void HandleStateChanged()
        {
            if (Health.CanFight) { VisibilityChanged?.Invoke(); return; }
            if (Overwatch) Overwatch.Cancel();
            if (Movement && !Movement.IsResolvingStep) Movement.CancelMove();
            if (Health.IsPresent) { VisibilityChanged?.Invoke(); return; }
            if (grid) grid.ReleaseUnit(this);
            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            gameObject.SetActive(false);
        }
        private void HandlePartDestroyed(TargetBodyPart part)
        {
            if ((part == TargetBodyPart.LeftLeg || part == TargetBodyPart.RightLeg) && Movement && Movement.IsMoving && !Movement.IsResolvingStep)
                Movement.CancelMove();
        }
        public void Configure(Vector2Int start, int movementRange)
        {
            startGridPosition = start;
            this.movementRange = Mathf.Max(0, movementRange);
        }
        internal void SetGridPosition(Vector2Int position)
        {
            CurrentGridPosition = position;
            VisibilityChanged?.Invoke();
        }
        private void OnValidate()
        {
            movementRange = Mathf.Max(0, movementRange);
            heightOffset = Mathf.Max(0, heightOffset);
            destroyedHeadHitMultiplier = Mathf.Clamp01(destroyedHeadHitMultiplier);
        }
    }
}
