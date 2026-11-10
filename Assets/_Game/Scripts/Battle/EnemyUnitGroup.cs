using System;
using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM
{
    [DisallowMultipleComponent]
    public class EnemyUnitGroup : MonoBehaviour
    {
        [SerializeField, InspectorName("적 집단 ID")] private string groupId;
        [SerializeField, InspectorName("대상 적 기체")] private BattleUnit[] members = Array.Empty<BattleUnit>();
        public string GroupId => groupId;
        public BattleUnit[] GetMembers() => members == null ? Array.Empty<BattleUnit>() : (BattleUnit[])members.Clone();
        public void Configure(string id, BattleUnit[] units)
        { groupId = id; members = units == null ? Array.Empty<BattleUnit>() : (BattleUnit[])units.Clone(); }
        public bool ValidateSetup(BattleUnit[] roster, out string reason)
        {
            reason = null;
            if (!isActiveAndEnabled) reason = "적 집단을 활성화하세요.";
            else if (string.IsNullOrWhiteSpace(groupId)) reason = "적 집단 ID를 입력하세요.";
            else if (members == null || members.Length == 0) reason = "섬멸 대상 적을 한 기체 이상 등록하세요.";
            if (reason != null) return false;
            var seen = new HashSet<BattleUnit>();
            foreach (var unit in members)
            {
                if (!unit) reason = "적 집단에 누락된 기체가 있습니다.";
                else if (!seen.Add(unit)) reason = "같은 기체를 적 집단에 중복 등록할 수 없습니다.";
                else if (unit.Team != UnitTeam.Enemy) reason = "섬멸 대상은 적 기체여야 합니다.";
                else if (!unit.isActiveAndEnabled) reason = "섬멸 대상 기체를 활성화하세요.";
                else if (roster == null || Array.IndexOf(roster, unit) < 0) reason = "섬멸 대상이 전투 기체 목록에 없습니다.";
                if (reason != null) return false;
            }
            return true;
        }
    }
}
