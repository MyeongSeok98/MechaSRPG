# 지정 적 집단 섬멸

`Assets/_Game/Scenes/EliminationMissionTest.unity`를 열고 Play합니다. 기존 `SqurdWeaponTest`의 맵·기체·무장을 복사한 아군3기/적3기 씬입니다. 현재 원본의 맵 크기20×20과 사용자 설정을 그대로 유지했습니다. 원본 씬 자체는 변경하지 않았습니다.

왼쪽 정보창에 임무 이름·지역과 **Destroyed: 0 / 3**이 표시됩니다. 대상 적의 머리·팔·다리를 파괴해 Critical로 만들어도 수가 늘지 않으며, 몸통/콕피트 파괴 등으로 **Destroyed**가 되어야 파괴 수가 늘어납니다. 등록된3기 모두 파괴하면 성공하고 전투를 중단합니다. 실제 공격의 피해 팝업이 끝난 뒤 결과를 확정합니다.

## Inspector 설정

1. Project의 `Assets/_Game/Data/Missions/SampleEliminationMission.asset`에서 이름·설명·지역·대상 적 집단 ID를 편집합니다. 샘플 ID는 `EnemySquad_A`입니다.
2. 씬의 `Battle Systems`에 있는 `MissionController`의 **임무 데이터**에 에셋을 연결합니다. **씬의 적 집단**에 대상 집단 컴포넌트를 등록합니다.
3. `Battle Systems/Mission Targets - EnemySquad_A`의 `EnemyUnitGroup`에서 **적 집단 ID**와 **대상 적 기체** 목록을 지정합니다. 데이터의 대상 ID와 정확히 일치시킵니다.
4. 같은 기체가 `BattleSceneController`의 전투 기체 목록에도 들어 있어야 합니다. 등록된 적 수를 목표 수로 자동 계산합니다.

빈/누락 집단·중복 집단 ID·중복 기체·아군·전투 목록 밖/비활성 대상을 거부합니다. 잘못된 설정이면 Console에 이유를 알리고 전투 시작을 중단합니다. 임무 설정 검사는 그리드·AP 초기화 전에 실행합니다.

## 코드 책임과 흐름

| 코드 | 책임 |
|---|---|
| `MissionData` | 공유 임무 설정. 현재 수치나 씬 오브젝트를 저장하지 않음 |
| `EnemyUnitGroup` | 씬의 실제 적 목록과 ID 등록·검증, 목록 복사 제공 |
| `EliminationObjective` | 전투마다 생성하는 순수 C# 진행 상태. 대상 수·파괴 수·달성 여부 관리 |
| `MissionController` | 데이터의 ID로 집단 선택, 진행 상태 생성, 공통 실패/명시적 요청/섬멸 성공 판단 |
| `TurnManager` | 피해 원자성·팝업 대기 시점 제공, 최종 전투 중단 실행 |
| `BattleSceneController` | 전투 초기화 전 임무 설정 검증 |

```text
MissionData의 대상 집단 ID
          ↓ 같은 ID 찾기
EnemyUnitGroup의 실제 적 목록
          ↓ 전투 시작 시 복사
EliminationObjective가 각 UnitHealth.StateChanged 관찰
          ↓ Destroyed만 한 번 기록
MissionController가 피해/팝업 종료 후 달성 판정
          ↓ 공통 아군 전멸 실패가 우선
TurnManager가 이동·AI·경계·방향 선택·턴 배너 중단
```

`EliminationObjective`는 MonoBehaviour가 아니므로 직접 오브젝트에 붙이지 않습니다. `MissionController`가 실행 시 생성합니다. 파괴 기록은 따로 보존하므로 파괴된 오브젝트를 나중에 제거해도 수가 유지됩니다. 살아 있는 적의 단순 비활성화·삭제·긴급탈출은 Destroyed로 세지 않습니다. Critical도 인정하지 않습니다. 현재 적 AI는 긴급탈출하지 않으며, 향후 적의 이탈 실패 조건은 별도 설계 대상입니다.

등록하지 않은 다른 적이 살아 있어도 지정 집단을 모두 파괴하면 성공합니다. 목록은 시작 시 스냅샷이므로 진행 중 Inspector에서 집단 목록을 바꿔도 현재 목표 수는 바뀌지 않습니다. 새 전투를 시작하면0부터 다시 생성하며 MissionData는 변하지 않습니다. 임무 종료/제거 시 관찰 구독을 해제해 완료한 진행도를 유지합니다.

## 기존 전투와 호환

MissionData를 연결하면 기존 **적 전멸 시 자동 성공** 옵션은 무시합니다. MissionData 없는 기존 씬은 이전처럼 전장에 남은 적이 없어지면 성공합니다. 공통 실패는 이전처럼 전장에 남은 아군이 없는 경우이며 Critical 아군은 남아 있는 기체로 셉니다. 양측 동시 파괴 시 실패가 우선합니다.

## 씬 생성 도구

`MechaXCOM → Create Elimination Mission Test Scene`은 원본이 닫혀 있고 목적 씬이 아직 없는 경우에만 생성합니다. 목적 씬이 이미 있거나 원본 SqurdWeaponTest가 열려 있으면 거부합니다. 이 프로젝트에는 생성된 씬을 이미 포함했습니다. 메뉴로 기존 편집 내용을 덮어쓰지 않습니다.

생포·호위·철수와 별도 결과 화면은 이번 작업에 추가하지 않았습니다.

## 검증

2026-11-09 전체 EditMode **983/983**, 최종 그래픽 PlayMode **461/461** 통과(실패·건너뜀0). 실제 씬3대3 연결·Critical 제외·마지막 파괴·재시작 초기화·공유 에셋 불변·팝업 종료 대기·동시 피해 실패 우선·잘못된 등록 차단·원본 씬 보호를 확인했습니다. 기존 씬과 데이터 해시는 유지했습니다.
