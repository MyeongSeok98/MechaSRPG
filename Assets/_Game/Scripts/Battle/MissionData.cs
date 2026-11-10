using UnityEngine;

namespace MechaXCOM
{
    public enum MissionObjectiveType
    {
        [InspectorName("적 전멸")] EliminateEnemies = 0,
        [InspectorName("특정 목표 파괴")] DestroyTarget = 1
    }

    // Shared mission definition only. Target instances and progress belong to the running battle.
    [CreateAssetMenu(fileName = "NewMission", menuName = "MechaXCOM/Mission Data")]
    public class MissionData : ScriptableObject
    {
        [SerializeField, InspectorName("임무 ID")] private string missionId;
        [SerializeField, InspectorName("임무 이름")] private string displayName;
        [SerializeField, InspectorName("임무 설명"), TextArea(3, 6)] private string description;
        [SerializeField, InspectorName("임무 지역")]
        [Tooltip("지역 표시용 이름입니다. 씬을 자동으로 불러오지 않습니다.")]
        private string regionName;
        [SerializeField, InspectorName("목표 종류")] private MissionObjectiveType objectiveType = MissionObjectiveType.EliminateEnemies;
        [SerializeField, InspectorName("섬멸 대상 적 집단 ID")]
        [Tooltip("씬에 등록한 적 집단 ID와 일치시킵니다. 대상 수와 파괴 수는 전투 실행 상태에서 계산합니다.")]
        private string targetEnemyGroupId;
        [SerializeField, InspectorName("파괴 대상 ID")] private string targetObjectId;

        public string MissionId => missionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public string RegionName => regionName;
        public MissionObjectiveType ObjectiveType => objectiveType;
        public string TargetEnemyGroupId => targetEnemyGroupId;
        public string TargetObjectId => targetObjectId;

        public bool IsValid(out string reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(missionId)) reason = "임무 ID를 입력하세요.";
            else if (!System.Enum.IsDefined(typeof(MissionObjectiveType), objectiveType)) reason = "유효한 목표 종류를 선택하세요.";
            else if (objectiveType == MissionObjectiveType.EliminateEnemies && string.IsNullOrWhiteSpace(targetEnemyGroupId))
                reason = "섬멸 대상 적 집단 ID를 입력하세요.";
            else if (objectiveType == MissionObjectiveType.DestroyTarget && string.IsNullOrWhiteSpace(targetObjectId))
                reason = "파괴 대상 ID를 입력하세요.";
            return reason == null;
        }
    }
}
