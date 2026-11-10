# 통신소 파괴 샘플 임무

`Assets/_Game/Scenes/DestroyCommunicationStationTest.unity`를 열고 Play합니다. 아군 메카2기·적 메카2기·통신소1개입니다. 기존 소대전 맵의 복사본을 사용하며 이 새 씬에서만 기존 엄폐물·장애물을 정리하고 샘플 전투 위치를 배치했습니다. 원본 씬·데이터·카메라/이동 속도 설정은 바꾸지 않았습니다.

## 플레이

1. YOUR TURN 배너가 끝나면 아군을 선택합니다.
2. **Attack → Demolition Launcher**를 선택합니다.
3. 안테나가 있는 **Communication Station [TARGET]** 건물을 클릭합니다.
4. 우측 목표 시설 공격창의 HP·피해·AP·탄약을 보고 **Fire**를 누릅니다.
5. 피해 팝업이 끝나면 다른 아군을 선택해 같은 순서로 한 번 더 발사합니다.

통신소 HP는2000이고 테스트용 발사기는 피해1000·AP3·장탄3입니다. 두 발이면 파괴할 수 있습니다. 적을 모두 격파하지 않아도 지정 통신소가 파괴되면 성공합니다. 마지막 팝업이 끝난 뒤 전투를 중단합니다. 반대로 적 메카만 모두 격파해도 통신소가 남아 있으면 계속 진행합니다. 아군이 모두 전장에서 사라지면 공통 실패입니다.

통신소는 **빔·실탄·직사 폭발·근접무기**로 선택해 공격할 수 있습니다. Attack에서 원하는 무기를 고른 뒤 시설을 클릭하고 Fire(근접은 Attack)로 확정합니다. 시설은 확정 명중이며 무기의 기본 피해 범위를 사용합니다. 원거리는 사거리·사선·AP·탄약을 검사하고, 근접은 이동 가능한 접근 경로와 사거리·AP를 검사하며 탄약은 소모하지 않습니다. 곡사 폭발은 좌표를 지정하고 범위 안 시설에 기본 피해를 줍니다. 일반 엄폐물은 기존처럼 폭발무기로만 손상됩니다.

## Inspector 연결

- `Data/Missions/SampleDestroyStationMission.asset`: 목표 종류 **특정 목표 파괴**, 파괴 대상 ID `CommunicationStation_A`.
- `Battle Systems/MissionController`: 위 임무 데이터와 씬의 파괴 대상 참조.
- `Communication Station [TARGET]/MissionTarget`: 같은 대상 ID와 **시설 내구도** 참조.
- `Communication Station [TARGET]/CoverDurability`: 오브젝트 데이터 **SampleCommunicationStation** 참조. 여기서 최대 HP2000을 읽습니다.

통신소의 HP를 조절하려면 `Assets/_Game/Data/Objects/SampleCommunicationStation.asset`의 최대 HP를 편집합니다. 데이터 ID는 시설 종류 ID이며 MissionTarget ID는 해당 씬 인스턴스 ID라 서로 같은 값일 필요가 없습니다.

CoverDurability가 현재 HP를 관리하는 유일한 컴포넌트입니다. 오브젝트 데이터를 지정하면 데이터의 최대 HP가 우선하고, 지정하지 않은 기존 엄폐물은 프리팹 최대 HP를 그대로 사용합니다. 시작한 개체의 HP는 스냅샷이므로 전투 중 데이터 수정이나 재활성화로 회복하지 않습니다.

## 코드 흐름

`MissionData.TargetObjectId → MissionTarget → DestroyTargetObjective → MissionController → TurnManager`

MissionTarget은 UnitHealth 또는 CoverDurability 중 하나를 연결합니다. 메카는 Destroyed만 인정하고 Critical/탈출은 인정하지 않습니다. 시설은 실제 HP0 파괴 이벤트를 기록합니다. 단순 삭제나 비활성화는 성공으로 세지 않습니다.

DestroyTargetObjective와 기존 EliminationObjective는 IMissionObjective로 공통 진행도·완료 여부·변경 이벤트를 제공합니다. 실행 시 내구도 참조를 고정하고, 완료/제거 시 구독을 해제합니다. 피해 처리와 팝업 중에는 MissionController가 결과를 확정하지 않으며 양측 동시 파괴 시 공통 아군 실패를 우선합니다.

새 발사기 데이터 `Assets/_Game/Data/Weapons/Ranged/MissionDemolitionLauncher.asset`은 테스트용이며 기존 무기를 수정하지 않았습니다. 포대 데이터/AI는 이 씬에 연결하지 않았습니다.

생성 도구 `MechaXCOM → Create Destroy Station Test Scene`은 원본 소대전 씬이 열려 있거나 목적 씬 파일이 이미 있으면 거부합니다. 프로젝트에는 생성된 씬이 이미 포함되어 있습니다.

## 검증

2026-11-09: 전체 EditMode **1029/1029**, 그래픽 PlayMode **470/470** 통과(실패·건너뜀0). 새 씬의2대2 등록·데이터 HP·실제 마우스 조준·Fire 버튼·AP/탄약·두 발 파괴·마지막 팝업 대기·적 생존 중 성공·아군 전멸 실패·재시작 초기화를 검증했습니다. 기존 씬·데이터·카메라/이동 속도 설정 파일은 변경하지 않았습니다.
추가 검증: 빔·실탄·직사 폭발·날붙이·둔기의 시설 기본 피해, 근접 AP 전용, 탄약 부족·사거리·벽/골짜기/유닛/겹침 차단, 배율0 곡사 시설 피해, 실제 라이플 클릭/Fire와 근접 확인 후 팝업/성공도 포함합니다.
