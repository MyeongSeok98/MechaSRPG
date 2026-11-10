# MissionData 편집

Project 창에서 `Create → MechaXCOM → Mission Data`로 새 임무 에셋을 만듭니다. 저장 권장 위치는 `Assets/_Game/Data/Missions/`입니다.

편집용 샘플은 `Assets/_Game/Data/Missions/SampleEliminationMission.asset`입니다. Inspector에서 임무 ID·임무 이름·임무 설명·임무 지역·목표 종류를 편집합니다. 목표 종류가 적 전멸이면 섬멸 대상 적 집단 ID가 표시됩니다. 이름을 비우면 에셋 이름을 표시하며 임무 ID와 적 집단 ID가 비어 있으면 오류 안내가 나옵니다. 복제한 임무에는 고유한 임무 ID를 입력합니다.

지역은 표시용 이름입니다. 씬 자동 로딩은 하지 않습니다. 대상 집단 ID의 예는 `EnemySquad_A`이며 씬에 등록한 EnemyUnitGroup의 ID와 정확히 일치시킵니다. 집단 연결 검증은 전투 시작 시 MissionController가 수행합니다.

에셋에는 현재 파괴 수·성공·실패 상태나 씬 오브젝트를 저장하지 않습니다. 필요한 적 수는 씬에 등록한 대상 집단에서 자동 계산합니다. EliminationMissionTest 씬에서 MissionController와 연결된 예를 확인할 수 있습니다.

## 목표 종류 선택

- **적 전멸**: 기본값이며 기존 데이터는 에셋 수정 없이 이 종류로 해석합니다. 현재 섬멸 판정과 동일하게 동작합니다.
- **특정 목표 파괴**: 파괴 대상 ID를 입력하고 MissionController의 씬의 파괴 대상에 같은 ID의 MissionTarget을 연결합니다. 시설 HP0 또는 메카 Destroyed를 인정합니다. [통신소 파괴 샘플](destroy-station-mission.ko.md)을 참고하세요.

종류를 바꿔도 기존 적 집단 ID 값은 지우지 않으므로 적 전멸로 돌아오면 다시 표시됩니다.

## 섬멸 목표 연결 흐름

1. 씬에 적 집단 컴포넌트를 만들고 집단 ID와 대상 BattleUnit 목록을 등록합니다. 빈 집단·중복 ID·잘못된 진영을 전투 시작 전에 검증합니다.
2. MissionController에서 사용할 MissionData를 연결하고 대상 집단 ID로 등록된 목록을 찾습니다. 이 임무에서는 기존 호환용 전멸 승리를 사용하지 않습니다.
3. 전투마다 별도의 섬멸 목표 실행 상태를 만들어 대상 수·Destroyed 수를 관리합니다. Critical·긴급탈출·단순 비활성화/삭제는 Destroyed로 간주하지 않습니다.
4. 등록된 적 전원이 Destroyed이면 MissionController가 성공을 판단하고 TurnManager에 종료를 요청합니다. 기존 공통 아군 전멸 실패·피해 처리 및 팝업 종료 대기·전투 중단 흐름을 재사용합니다.

위 흐름은 구현되어 있으며 [섬멸 임무 사용 안내](elimination-mission.ko.md)에 설정 방법과 코드 책임을 설명했습니다. 생포 기능은 별도입니다.

## 검증

2026-11-09 전체 EditMode 983/983, 그래픽 PlayMode 461/461 통과. 데이터 저장·재로딩과 실제 섬멸 씬의 연결·Destroyed 전용 진행도·실패 우선·팝업 대기까지 검증했습니다.

목표 종류 선택 추가 검증(2026-11-09): 전체 EditMode **989/989**, 기존 섬멸 씬·전투 관련 PlayMode **8/8** 통과. 기존 씬·에셋 파일은 변경하지 않았습니다.
