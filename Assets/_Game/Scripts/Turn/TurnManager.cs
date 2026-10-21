using System;
using System.Collections;
using UnityEngine;

namespace MechaXCOM
{
    public class TurnManager : MonoBehaviour
    {
        private BattleUnit[] units = Array.Empty<BattleUnit>();
        private bool initialized;
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
        public bool IsAnyUnitBusy => IsAnyUnitMoving || PendingFacingUnit || (overwatch && overwatch.IsResolving);
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
            this.units = units == null ? Array.Empty<BattleUnit>() : (BattleUnit[])units.Clone();
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
                if (unit && unit.isActiveAndEnabled && unit.IsAlive && unit.Team == team)
                {
                    unit.ActionPoints.ResetForTurn();
                    unit.Mobility.ResetMovement();
                }
            if (Visibility) Visibility.Refresh();
        }
        private void Update() => ResumeEnemyPhase();
        private void OnEnable() => ResumeEnemyPhase();
        private void ResumeEnemyPhase()
        {
            if (Application.isPlaying && initialized && isActiveAndEnabled && !IsBattleOver && !IsPlayerTurn &&
                enemyRoutine == null && enemyAI && enemyAI.IsReady)
                enemyRoutine = StartCoroutine(ExecuteEnemyPhase());
        }
        private IEnumerator ExecuteEnemyPhase()
        {
            // Leave the transition observable before any enemy spends AP.
            yield return null;
            while (nextEnemyIndex < units.Length && !IsBattleOver)
            {
                if (!enemyAI || !enemyAI.IsReady) break;
                var actor = units[nextEnemyIndex++];
                if (!actor || actor.Team != UnitTeam.Enemy || !actor.isActiveAndEnabled || !actor.IsAlive || !actor.Health.CanFight) continue;
                ActiveEnemy = actor;
                yield return enemyAI.ExecuteUnitTurn(actor, units);
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
            if (overwatch) overwatch.CancelAll();
            SuspendEnemyPhase();
            CancelUnitActions();
        }
        private void HandleUnitStateChanged()
        {
            if (ActiveEnemy && !ActiveEnemy.Health.CanFight) ActiveEnemy = null;
            CheckBattleEnd();
        }
        public void CheckBattleEnd()
        {
            if (!initialized || IsBattleOver) return;
            bool playerAlive = false, enemyAlive = false;
            foreach (var unit in units)
            {
                if (!unit || !unit.IsAlive) continue;
                if (unit.Team == UnitTeam.Player) playerAlive = true;
                else enemyAlive = true;
            }
            if (playerAlive && enemyAlive) return;
            IsBattleOver = true;
            if (overwatch) overwatch.CancelAll();
            CancelUnitActions();
            IsPlayerVictory = playerAlive && !enemyAlive;
            SuspendEnemyPhase();
        }
        private void OnDestroy()
        {
            foreach (var unit in units)
                if (unit && unit.Health) unit.Health.StateChanged -= HandleUnitStateChanged;
        }
    }
}
