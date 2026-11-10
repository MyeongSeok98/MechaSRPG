# 임무 결과와 전투 종료

`MissionController`가 임무 결과를 결정하고 `TurnManager`는 종료 요청을 받아 이동·방향 선택·경계·적 AI·턴 배너와 추가 행동을 중단합니다. 별도 결과 화면은 아직 없으며, 지정 집단 섬멸 목표는 MissionData와 EnemyUnitGroup으로 연결합니다.

## 현재 공통 실패 조건

초기 등록한 아군 중 전장에 남은 기체가 없으면 실패합니다. `Active`와 `Critical`은 남아 있는 기체로 셉니다. 파괴·긴급탈출·오브젝트 제거는 제외하며 단순 비활성화는 전멸로 세지 않습니다. 이는 기존 전투의 생존 판정을 유지한 것입니다. 모든 파일럿이 긴급탈출한 경우도 이번 단계에서는 임무 실패이며, 파일럿 사망을 뜻하지 않습니다.

양측을 동시에 제거하는 폭발 등에서는 모든 피해를 반영한 뒤 공통 아군 전멸 실패를 우선합니다. 공격 결과 팝업이 있으면 팝업 종료 후 결과를 확정합니다.

## MissionData 섬멸 연결

같은 오브젝트의 MissionController에서 임무 데이터와 씬의 적 집단을 지정합니다. MissionData의 대상 집단 ID로 선택한 적 전원이 Destroyed가 되면 성공합니다. Critical·긴급탈출·단순 삭제는 인정하지 않습니다. 이때 기존 적 전멸 자동 성공 옵션은 무시합니다. 설정·검증 씬·실행 상태 설명은 [섬멸 임무 안내](elimination-mission.ko.md)를 참고하세요.

## 임무별 결과 요청

새 임무를 설정할 때 **TurnManager가 붙은 동일 오브젝트**에 `MissionController`를 미리 추가합니다. 이 컴포넌트는 기본적으로 적 전멸을 자동 성공으로 판단하지 않습니다. 별도 목표 코드에서 초기화가 끝난 뒤 다음 API를 호출합니다.

```csharp
var mission = turnManager.Mission;
mission.TrySucceed("Communication facility destroyed.");
// 또는
mission.TryFail("Escort target destroyed.");
```

반환값 true는 요청을 접수했다는 뜻입니다. 피해 처리나 팝업 중에는 확정까지 대기합니다. 대기 중에는 먼저 접수한 명시적 요청을 유지하지만, 확정 시 공통 아군 전멸 실패가 있으면 실패가 우선합니다. 초기화 전·비활성 상태·종료 후·이미 요청 대기 중에는 false입니다.

`mission.Result.Outcome`은 `InProgress`, `Success`, `Failure`이며 `Reason`은 종료 이유입니다. `Completed` 이벤트는 전투 중단 정리와 결과 저장이 끝난 뒤 한 번 발생합니다. 결과는 전투마다 독립적이며 ScriptableObject 에셋을 수정하지 않습니다.

```csharp
mission.Completed += result =>
{
    Debug.Log($"{result.Outcome}: {result.Reason}");
};
```

이벤트 구독 코드는 소유자의 생명주기에 맞춰 구독을 해제합니다. 현재 결과와 이유는 왼쪽 디버그 정보창에도 표시합니다.

## 기존 씬 호환

MissionController가 없는 기존 씬은 `TurnManager.Initialize`에서 같은 오브젝트에 자동 추가하고 **적 전멸 시 자동 성공**을 켭니다. 따라서 `1V1Test`와 `SqurdWeaponTest`는 기존 섬멸 전투를 유지합니다. 다른 임무를 만들 때는 Edit Mode에서 MissionController를 미리 추가하고 이 옵션을 꺼 둡니다. 기존 저장 씬은 자동 수정하지 않습니다.

MissionController는 TurnManager와 함께 활성 상태여야 평가합니다. 같은 전투를 다시 Initialize해도 결과나 AP를 초기화하지 않으며, 새 전투는 새 씬/전투 인스턴스로 시작합니다.

시설 파괴·호위·복합 목표·철수·보상·캠페인 저장은 이후 이 API에 연결할 대상이며 아직 구현하지 않았습니다.

## 검증

2026-11-09: 전체 EditMode **983/983**, 최종 그래픽 PlayMode **461/461** 통과(실패·건너뜀 0). 기존 저장 씬·데이터·사용자 설정은 변경하지 않았으며, 테스트 복제본과 실제 프로젝트의 C# 소스 일치를 확인했습니다.
