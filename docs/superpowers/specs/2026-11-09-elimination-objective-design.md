# 섬멸 목표와 검증 씬

사용자 승인 범위: MissionData의 섬멸 대상 집단을 실제 전투와 연결하고, 별도 검증 씬을 만든 뒤 코드를 설명한다. 등록된 모든 적이 Destroyed일 때만 성공하며 Critical/긴급탈출/단순 삭제는 인정하지 않는다.

EnemyUnitGroup(MonoBehaviour)은 집단 ID와 BattleUnit 목록을 Inspector에서 등록한다. 빈 집단/ID, 중복 집단 ID/기체, null, 아군, 전투 명단 밖/비활성 대상을 거부한다. MissionController가 MissionData 및 그룹 참조를 받고 선택 집단을 찾는다. BattleSceneController는 잘못된 임무 설정을 그리드/AP 변경 전에 거부하며 직접 TurnManager.Initialize를 호출해도 검증한다.

EliminationObjective는 전투마다 생성하는 순수 C# 실행 상태다. 대상 목록을 스냅샷하고 UnitHealth.StateChanged를 관찰해 Destroyed 기록을 보존한다. 대상 수/파괴 수/달성 여부를 제공하고 Dispose에서 해제한다. 대상 외 적은 목표 달성에 영향을 주지 않는다. 임무 에셋은 변경하지 않는다. Runtime 목록 편집은 진행 중 목표에 반영하지 않는다.

MissionController는 피해 원자성/팝업 종료 후 공통 실패 우선, 명시적 요청, 섬멸 완료를 평가한다. MissionData 연결 시 호환용 적 전멸 성공은 무시한다. MissionData 없는 기존 씬/명시적 요청은 기존 동작을 유지한다. 종료는 TurnManager의 기존 중단 경로를 쓴다. 왼쪽 UI에 이름/지역/파괴 n/N을 표시한다.

EliminationMissionTest.unity는 SqurdWeaponTest를 새 파일로 복사하고 기존 3대3 배치/맵/튜닝을 유지한다. 새 MissionController와 EnemyUnitGroup을 붙이고 SampleEliminationMission을 연결한다. 생성기는 기존 목적 파일 덮어쓰기를 거부한다. 기존 씬/모델/수치/자산/Git은 변경하지 않는다.

새 생포/호위/철수/증원/목표 편집기/결과 화면은 범위 밖이다. 검증은 기존 복제본 Unity에서 수행한다.
