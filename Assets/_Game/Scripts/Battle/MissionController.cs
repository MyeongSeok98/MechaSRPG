using System;
using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM
{
    [DisallowMultipleComponent]
    public class MissionController : MonoBehaviour
    {
        [SerializeField, InspectorName("임무 데이터")] private MissionData missionData;
        [SerializeField, InspectorName("씬의 적 집단")] private EnemyUnitGroup[] enemyGroups = Array.Empty<EnemyUnitGroup>();
        [SerializeField, InspectorName("씬의 파괴 대상")] private MissionTarget[] missionTargets = Array.Empty<MissionTarget>();
        [SerializeField, InspectorName("적 전멸 시 자동 성공")]
        [Tooltip("MissionData가 없는 기존 전투의 호환 설정입니다. 임무 데이터가 연결되면 사용하지 않습니다.")]
        private bool succeedOnEnemyElimination;
        private TurnManager turns;
        private BattleUnit[] units = Array.Empty<BattleUnit>();
        private bool initialized;
        private MissionOutcome pendingOutcome;
        private string pendingReason;
        public MissionResult Result { get; private set; } = new MissionResult(MissionOutcome.InProgress, string.Empty);
        public bool IsComplete => Result.Outcome != MissionOutcome.InProgress;
        public bool SucceedOnEnemyElimination => !missionData && succeedOnEnemyElimination;
        public MissionData Data => missionData;
        public IMissionObjective Objective { get; private set; }
        public event Action<MissionResult> Completed;

        internal void EnableLegacyEliminationVictory() => succeedOnEnemyElimination = true;
        public bool Configure(MissionData data, EnemyUnitGroup[] groups)
        {
            if (initialized) return false;
            missionData = data; enemyGroups = groups == null ? Array.Empty<EnemyUnitGroup>() : (EnemyUnitGroup[])groups.Clone();
            missionTargets = Array.Empty<MissionTarget>();
            return true;
        }
        public bool ConfigureDestruction(MissionData data, MissionTarget[] targets)
        {
            if (initialized) return false;
            missionData = data; missionTargets = targets == null ? Array.Empty<MissionTarget>() : (MissionTarget[])targets.Clone();
            enemyGroups = Array.Empty<EnemyUnitGroup>(); return true;
        }
        public bool ValidateSetup(BattleUnit[] roster, out string reason) => missionData && missionData.ObjectiveType == MissionObjectiveType.DestroyTarget
            ? TryResolveTarget(roster, out _, out reason) : TryResolveGroup(roster, out _, out reason);
        private bool TryResolveTarget(BattleUnit[] roster, out MissionTarget selected, out string reason)
        {
            selected = null; reason = null;
            if (!isActiveAndEnabled) { reason = "임무 관리자를 활성화하세요."; return false; }
            if (!missionData.IsValid(out reason)) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var target in missionTargets ?? Array.Empty<MissionTarget>())
            {
                if (!target) { reason = "씬의 파괴 대상 참조가 누락됐습니다."; return false; }
                if (!target.ValidateSetup(roster, out reason)) return false;
                if (!ids.Add(target.TargetId)) { reason = "파괴 대상 ID가 중복됐습니다: " + target.TargetId; return false; }
                if (target.TargetId == missionData.TargetObjectId) selected = target;
            }
            if (!selected) { reason = "파괴 대상을 찾을 수 없습니다: " + missionData.TargetObjectId; return false; }
            return true;
        }
        private bool TryResolveGroup(BattleUnit[] roster, out EnemyUnitGroup selected, out string reason)
        {
            selected = null; reason = null;
            if (!missionData) return true; // Existing manually controlled missions need no objective data.
            if (!isActiveAndEnabled) { reason = "임무 관리자를 활성화하세요."; return false; }
            if (!missionData.IsValid(out reason)) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (enemyGroups != null)
                foreach (var group in enemyGroups)
                {
                    if (!group) { reason = "씬의 적 집단 참조가 누락됐습니다."; return false; }
                    if (!group.ValidateSetup(roster, out reason)) return false;
                    if (!ids.Add(group.GroupId)) { reason = "씬의 적 집단 ID가 중복됐습니다: " + group.GroupId; return false; }
                    if (group.GroupId == missionData.TargetEnemyGroupId) selected = group;
                }
            if (!selected) { reason = "섬멸 대상 적 집단을 찾을 수 없습니다: " + missionData.TargetEnemyGroupId; return false; }
            return true;
        }
        internal bool Initialize(TurnManager manager, BattleUnit[] roster, out string reason)
        {
            reason = null;
            if (initialized) return turns == manager;
            if (!ValidateSetup(roster, out reason)) return false;
            if (missionData && missionData.ObjectiveType == MissionObjectiveType.DestroyTarget)
            {
                TryResolveTarget(roster, out var target, out reason);
                if (target.UnitHealth && !target.UnitHealth.GetComponent<BattleUnit>().IsInitialized)
                { reason = "파괴 대상 기체 초기화에 실패했습니다."; return false; }
                Objective = new DestroyTargetObjective(target);
            }
            else
            {
                TryResolveGroup(roster, out var group, out reason);
                if (group)
                {
                    var members = group.GetMembers();
                    foreach (var unit in members)
                        if (!unit.IsInitialized || !unit.Health)
                        { reason = "섬멸 대상 기체 초기화에 실패했습니다: " + unit.name; return false; }
                    Objective = new EliminationObjective(members);
                }
            }
            turns = manager;
            units = roster == null ? Array.Empty<BattleUnit>() : (BattleUnit[])roster.Clone();
            initialized = true;
            if (Objective != null) Objective.Changed += HandleObjectiveChanged;
            return true;
        }
        // Future objective implementations request an outcome; only this controller commits it.
        public bool TrySucceed(string reason) => Request(MissionOutcome.Success, reason);
        public bool TryFail(string reason) => Request(MissionOutcome.Failure, reason);
        private bool Request(MissionOutcome outcome, string reason)
        {
            if (!initialized || !isActiveAndEnabled || !turns || !turns.isActiveAndEnabled ||
                turns.IsBattleOver || IsComplete || pendingOutcome != MissionOutcome.InProgress) return false;
            pendingOutcome = outcome;
            pendingReason = string.IsNullOrWhiteSpace(reason) ?
                (outcome == MissionOutcome.Success ? "Mission completed." : "Mission failed.") : reason;
            turns.CheckBattleEnd();
            return true;
        }
        internal void Evaluate()
        {
            if (!initialized || !isActiveAndEnabled || !turns || !turns.CanResolveMission || IsComplete) return;
            bool playerPresent = false, enemyPresent = false;
            foreach (var unit in units)
            {
                if (!unit || !unit.IsAlive) continue;
                if (unit.Team == UnitTeam.Player) playerPresent = true;
                else enemyPresent = true;
            }
            // Common defeat takes precedence after all damage has been applied.
            if (!playerPresent) Complete(MissionOutcome.Failure, "No player units remain on the battlefield.");
            else if (pendingOutcome != MissionOutcome.InProgress) Complete(pendingOutcome, pendingReason);
            else if (Objective != null && Objective.IsComplete) Complete(MissionOutcome.Success,
                missionData && missionData.ObjectiveType == MissionObjectiveType.DestroyTarget ? "Designated target destroyed." : "All designated enemy units destroyed.");
            else if (SucceedOnEnemyElimination && !enemyPresent) Complete(MissionOutcome.Success, "All enemy units eliminated.");
        }
        private void Complete(MissionOutcome outcome, string reason)
        {
            var result = new MissionResult(outcome, reason);
            if (!turns.FinishMission(this, result)) return;
            Result = result;
            DisposeObjective();
            pendingOutcome = MissionOutcome.InProgress; pendingReason = null;
            Completed?.Invoke(result);
        }
        private void HandleObjectiveChanged() { if (turns) turns.CheckBattleEnd(); }
        private void DisposeObjective()
        {
            if (Objective == null) return;
            Objective.Changed -= HandleObjectiveChanged; Objective.Dispose();
        }
        private void OnDestroy() => DisposeObjective();
    }
}
