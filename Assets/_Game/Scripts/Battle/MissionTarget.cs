using System;
using UnityEngine;

namespace MechaXCOM
{
    [DisallowMultipleComponent]
    public class MissionTarget : MonoBehaviour
    {
        [SerializeField, InspectorName("대상 ID")] private string targetId;
        [SerializeField, InspectorName("메카 내구도 (둘 중 하나)")] private UnitHealth unitHealth;
        [SerializeField, InspectorName("시설 내구도 (둘 중 하나)")] private CoverDurability objectHealth;
        public string TargetId => targetId;
        public UnitHealth UnitHealth => unitHealth;
        public CoverDurability ObjectHealth => objectHealth;
        public void ConfigureUnit(string id, UnitHealth health) { targetId = id; unitHealth = health; objectHealth = null; }
        public void ConfigureObject(string id, CoverDurability health) { targetId = id; objectHealth = health; unitHealth = null; }
        public bool ValidateSetup(BattleUnit[] roster, out string reason)
        {
            reason = null;
            if (!isActiveAndEnabled) reason = "임무 대상을 활성화하세요.";
            else if (string.IsNullOrWhiteSpace(targetId)) reason = "임무 대상 ID를 입력하세요.";
            else if ((bool)unitHealth == (bool)objectHealth) reason = "메카/시설 내구도 중 정확히 하나를 연결하세요.";
            else if (unitHealth)
            {
                var unit = unitHealth.GetComponent<BattleUnit>();
                if (!unit || unit.Team != UnitTeam.Enemy || roster == null || Array.IndexOf(roster, unit) < 0)
                    reason = "대상 메카는 전투 명단에 등록된 적 기체여야 합니다.";
                else if (!unit.isActiveAndEnabled || !unitHealth.isActiveAndEnabled) reason = "대상 메카를 활성화하세요.";
            }
            else if (!objectHealth.isActiveAndEnabled) reason = "대상 시설 내구도를 활성화하세요.";
            else if (objectHealth.Data && !objectHealth.Data.IsValid(out reason)) return false;
            return reason == null;
        }
    }
}
