using System;
using System.Collections;
using UnityEngine;

namespace MechaXCOM
{
    public partial class TurnManager : MonoBehaviour
    {
        private BattleUnit[] units = Array.Empty<BattleUnit>();
        private bool initialized;
        private int damageResolutionDepth;
        private UnityEngine.Object feedbackOwner;
        private float feedbackUntil;
        public bool IsPresentingAttack => Application.isPlaying && feedbackOwner && Time.unscaledTime < feedbackUntil;
        internal void HoldAttackPresentation(UnityEngine.Object owner, float seconds)
        { feedbackOwner = owner; feedbackUntil = Time.unscaledTime + seconds; }
        internal void ReleaseAttackPresentation(UnityEngine.Object owner)
        {
            if (!ReferenceEquals(feedbackOwner, owner)) return;
            feedbackOwner = null; feedbackUntil = 0; CheckBattleEnd();
        }
        internal void BeginDamageResolution() => damageResolutionDepth++;
        internal void EndDamageResolution()
        { damageResolutionDepth = Mathf.Max(0, damageResolutionDepth - 1); if (damageResolutionDepth == 0) CheckBattleEnd(); }
        [SerializeField, Min(0)] private float enemyActionDelay = 0.25f;
        private EnemyAIController enemyAI;
        private OverwatchSystem overwatch;
        public OverwatchSystem Overwatch => overwatch;
        private Coroutine enemyRoutine;
        private int nextEnemyIndex;
        public int TurnNumber { get; private set; }
        public UnitTeam ActiveTeam { get; private set; } = UnitTeam.Player;
        public bool IsPlayerTurn => ActiveTeam == UnitTeam.Player;
        public BattleUnit ActiveEnemy { get; private set; }
        public bool IsBattleOver { get; private set; }
        public bool IsPlayerVictory { get; private set; }
        public MissionController Mission { get; private set; }
        public bool IsInitialized => initialized;
        internal bool CanResolveMission => initialized && isActiveAndEnabled && !IsBattleOver &&
            damageResolutionDepth == 0 && !IsPresentingAttack;
        public VisibilitySystem Visibility { get; private set; }
        public void ConfigureVisibility(VisibilitySystem visibility) => Visibility = visibility;
        public BattleUnit PendingFacingUnit
        {
            get
            {
                foreach (var unit in units)
                    if (unit && unit.isActiveAndEnabled && unit.IsAlive && unit.Health.CanFight && unit.HasPendingFacing) return unit;
                return null;
            }
        }
        public bool IsAnyUnitBusy => damageResolutionDepth > 0 || IsPresentingTurn || IsPresentingAttack || IsAnyUnitMoving || PendingFacingUnit || (overwatch && overwatch.IsResolving);
        public bool IsAnyUnitMoving
        {
            get
            {
                foreach (var unit in units)
                    if (unit && unit.Movement && unit.Movement.IsMoving) return true;
                return false;
            }
        }
        public void Initialize(BattleUnit[] units)
        {
            if (initialized) return;
            var roster = units == null ? Array.Empty<BattleUnit>() : (BattleUnit[])units.Clone();
            Mission = GetComponent<MissionController>();
            if (!Mission)
            {
                Mission = gameObject.AddComponent<MissionController>();
                Mission.EnableLegacyEliminationVictory();
            }
            if (!Mission.Initialize(this, roster, out var reason))
            {
                Debug.LogWarning($"{name}: {reason} 전투 시작을 중단합니다.", this);
                return;
            }
            this.units = roster;
            initialized = true;
            TurnNumber = 1;
            foreach (var unit in this.units)
            {
                if (!unit || !unit.IsInitialized) continue;
                unit.BindTurnManager(this);
                unit.Health.StateChanged += HandleUnitStateChanged;
            }
            StartTeamTurn(UnitTeam.Player);
            CheckBattleEnd();
        }
        private bool HasTurnPermission(BattleUnit unit) => initialized && isActiveAndEnabled && !IsBattleOver &&
            !IsAnyUnitBusy && unit && unit.isActiveAndEnabled && unit.IsAlive &&
            unit.Team == ActiveTeam && unit.IsRegisteredOnGrid && Array.IndexOf(units, unit) >= 0;
        public bool CanAct(BattleUnit unit) => HasTurnPermission(unit) && unit.Health.CanFight &&
            !(unit.Overwatch && unit.Overwatch.IsActive);
        public void ConfigureOverwatch(OverwatchSystem system)
        {
            if (overwatch && overwatch != system) overwatch.CancelAll();
            overwatch = system;
        }
        public bool CanEject(BattleUnit unit) => HasTurnPermission(unit) && unit.Health.isActiveAndEnabled &&
            unit.Health.IsPresent && unit.Movement && unit.Movement.isActiveAndEnabled &&
            unit.ActionPoints && unit.ActionPoints.isActiveAndEnabled && unit.ActionPoints.CanSpend(1);
        public void ConfigureEnemyAI(EnemyAIController controller)
        {
            if (enemyAI != controller) SuspendEnemyPhase();
            enemyAI = controller;
        }
        public bool TryEndTurn()
        {
            if (!initialized || !isActiveAndEnabled || !IsPlayerTurn || IsBattleOver || IsAnyUnitBusy) return false;
            if (!enemyAI || !enemyAI.IsReady)
            {
                Debug.LogWarning("Enemy AI is unavailable; cannot end the player turn.", this);
                return false;
            }
            StartTeamTurn(UnitTeam.Enemy);
            nextEnemyIndex = 0;
            ResumeEnemyPhase();
            return true;
        }
        private void StartTeamTurn(UnitTeam team)
        {
            ActiveTeam = team;
            if (overwatch) overwatch.ExpireTeam(team);
            foreach (var unit in units)
                if (unit && unit.IsAlive && unit.Team == team)
                {
                    unit.ActionPoints.ResetForTurn();
                    if (unit.Movement) unit.Movement.ResetForTurn();
                    unit.Mobility.ResetMovement();
                }
            if (Visibility) Visibility.Refresh();
            TurnStarted?.Invoke(team);
        }
        private void Update() { if (!IsPresentingAttack) CheckBattleEnd(); ResumeEnemyPhase(); }
        private void OnEnable() => ResumeEnemyPhase();
        private void ResumeEnemyPhase()
        {
            if (Application.isPlaying && initialized && isActiveAndEnabled && !IsBattleOver && !IsPlayerTurn &&
                enemyRoutine == null && !IsPresentingTurn && !IsPresentingAttack && enemyAI && enemyAI.IsReady)
                enemyRoutine = StartCoroutine(ExecuteEnemyPhase());
        }
        private IEnumerator ExecuteEnemyPhase()
        {
            // Leave the transition observable before any enemy spends AP.
            yield return null;
            while (nextEnemyIndex < units.Length && !IsBattleOver)
            {
                while (IsPresentingTurn) yield return null;
                if (IsBattleOver) break;
                if (!enemyAI || !enemyAI.IsReady) break;
                var actor = units[nextEnemyIndex++];
                if (!actor || actor.Team != UnitTeam.Enemy || !actor.isActiveAndEnabled || !actor.IsAlive || !actor.Health.CanFight) continue;
                ActiveEnemy = actor;
                yield return enemyAI.ExecuteUnitTurn(actor, units);
                while (IsPresentingAttack) yield return null;
                CheckBattleEnd();
                if (IsBattleOver) break;
                // Keep instant attacks visible, including the last enemy's action.
                if (enemyActionDelay > 0)
                    yield return new WaitForSeconds(enemyActionDelay);
                else
                    yield return null;
                ActiveEnemy = null;
            }
            ActiveEnemy = null;
            enemyRoutine = null;
            if (!IsBattleOver && enemyAI && enemyAI.IsReady && nextEnemyIndex >= units.Length)
            {
                TurnNumber++;
                StartTeamTurn(UnitTeam.Player);
            }
        }
        internal void NotifyAIUnavailable(EnemyAIController controller)
        {
            if (enemyAI == controller) SuspendEnemyPhase();
        }
        private void SuspendEnemyPhase()
        {
            if (enemyAI) enemyAI.CancelCurrentAction();
            if (enemyRoutine != null) StopCoroutine(enemyRoutine);
            enemyRoutine = null;
            ActiveEnemy = null;
        }
        private void CancelUnitActions()
        {
            foreach (var unit in units)
                if (unit && unit.Movement && (unit.HasPendingFacing || unit.Movement.IsMoving)) unit.Movement.CancelMove();
        }
        private void OnDisable()
        {
            ClearTurnPresentation();
            feedbackOwner = null; feedbackUntil = 0;
            if (overwatch) overwatch.CancelAll();
            SuspendEnemyPhase();
            CancelUnitActions();
        }
        private void HandleUnitStateChanged()
        {
            if (ActiveEnemy && !ActiveEnemy.Health.CanFight &&
                !(overwatch && overwatch.IsResolvingMover(ActiveEnemy))) ActiveEnemy = null;
            CheckBattleEnd();
        }
        public void CheckBattleEnd()
        {
            if (CanResolveMission && Mission) Mission.Evaluate();
        }
        internal bool FinishMission(MissionController source, MissionResult result)
        {
            if (!CanResolveMission || source != Mission ||
                (result.Outcome != MissionOutcome.Success && result.Outcome != MissionOutcome.Failure)) return false;
            IsBattleOver = true;
            IsPlayerVictory = result.Outcome == MissionOutcome.Success;
            ClearTurnPresentation();
            feedbackOwner = null; feedbackUntil = 0;
            if (overwatch) overwatch.CancelAll();
            CancelUnitActions();
            SuspendEnemyPhase();
            return true;
        }
        private void OnDestroy()
        {
            foreach (var unit in units)
                if (unit && unit.Health) unit.Health.StateChanged -= HandleUnitStateChanged;
        }
    }
}
