# 전투 스켈레톤 사용 설명

## 바로 실행하기

1. Unity 6000.5.5f1에서 이 프로젝트를 연다.
2. `Assets/_Game/Scenes/BattleTest.unity`를 연다.
3. Game View를 가로 화면으로 설정하고 Play를 누른다. 1280×720 이상을 추천한다.
4. 아군 기체를 클릭하면 금색 선택 표시와 기체 옆 Move / Attack / Eject 메뉴가 나타난다. 아직 범위는 표시하지 않는다.
5. Move를 누르면 메뉴를 숨기고 청록색 이동 가능 셀을 표시한다. 목적지를 클릭하면 이동한다. 완료 후 선택을 유지하고 메뉴로 돌아온다.
6. Attack → Melee 또는 Ranged를 선택하면 빨간 공격 범위를 표시한다. 적을 클릭하고 근접은 Strike, 원거리는 부위 조준 후 Fire로 확정한다. 원거리 기본 그림은 Mecha이며 기본 조준의 피해 부위는 Body다.
7. End Turn 버튼을 누르면 적이 순서대로 행동한다. 모두 끝나면 다음 플레이어 턴이 시작되어 아군 AP가 회복된다.
8. 왼쪽 UI에서 기체 상태·7개 부위 HP·AP·현재 셀, 이번 이동 칸 수, Movement Evasion과 8방향 엄폐를 확인한다.

왼쪽 `INCOMING COVER MULTIPLIER`는 각 방향에서 공격받을 때 적용되는 엄폐 배율이다.
대각선 칸 자체에 엄폐물이 없어도 인접한 두 방향을 포함해 가장 강한 엄폐가 표시된다.
예를 들어 South Full만 있으면 S·SE·SW는 ×0.5, E는 ×1.0이다.

턴마다 AP 5를 받고, 실제 도착한 한 칸마다 AP 1, 공격 시도마다 AP 3을 소모한다.
남은 AP 안에서 반복 이동할 수 있다. 새 이동을 시작하면 이전 이동 칸 수·회피는 초기화한다.
각 진영의 턴 시작에 그 진영만 이동 칸 수·회피를 0으로 초기화한다. 플레이어의 회피는 적 공격 동안 유지한다. HP와 위치는 유지한다.
이동 중에는 공격·턴 종료·추가 명령이 차단된다. 적 턴에도 플레이어 이동·공격·턴 종료는 차단된다.
적 진영에 ACTIVE/CRITICAL이 한 기도 남지 않으면 승리, 아군 진영에 한 기도 남지 않으면 패배다. CRITICAL만 남아도 턴과 탈출 기회는 유지한다. 다시 시험하려면 Play를 재시작한다.
불가능한 셀을 클릭하는 것만으로 기존 회피가 초기화되지는 않는다.
화면 왼쪽 23%는 UI 공간, 오른쪽은 쿼터뷰 카메라 공간이다.
UI는 화면 크기에 맞춰 축소된다. Q/E로 맵 중심 주위를 90도씩 회전한다.

## 생성한 파일

프로젝트 루트는 `C:/Users/myong/Documents/Unity/MechaXCOM`이다.
아래 경로는 이 루트를 기준으로 한다. Unity의 .meta 파일도 함께 생성되어 있다.

| 폴더 | 파일 |
| --- | --- |
| Assets/_Game/Scripts/Grid | GridCell.cs, GridManager.cs, GridObstacle.cs, Pathfinder.cs |
| Assets/_Game/Scripts/Unit | BattleUnit.cs, UnitMovement.cs, UnitMobility.cs, UnitTeam.cs, UnitHealth.cs, UnitActionPoints.cs, UnitCombat.cs |
| Assets/_Game/Scripts/Combat | CombatMath.cs, CombatSystem.cs, AttackPreview.cs, AttackResult.cs |
| Assets/_Game/Scripts/Turn | TurnManager.cs |
| Assets/_Game/Scripts/AI | EnemyAIController.cs |
| Assets/_Game/Scripts/Cover | CoverTypes.cs, CoverObject.cs, CoverSystem.cs |
| Assets/_Game/Scripts/Input | PlayerInputController.cs |
| Assets/_Game/Scripts/Presentation | GridVisualizer.cs, BattleDebugUI.cs, UnitActionUI.cs |
| Assets/_Game/Scripts/Setup | BattleSceneController.cs |
| Assets/_Game/Scripts | MechaXCOM.Runtime.asmdef |
| Assets/_Game/Editor | BattleTestSceneBuilder.cs, MechaXCOM.Editor.asmdef |
| Assets/_Game/Scenes | BattleTest.unity |
| Assets/_Game/Prefabs | BattleUnit.prefab, DummyEnemy.prefab, HalfCover.prefab, FullCover.prefab, GridObstacle.prefab |
| Assets/_Game/Materials | Ground.mat, Unit.mat, DummyEnemy.mat, HalfCover.mat, FullCover.mat, Obstacle.mat, GridLines.mat, MoveRange.mat, Selection.mat |
| Assets/_Game/Tests/EditMode | 기존 테스트와 UnitStatsTests.cs, CombatMathTests.cs, CombatSystemTests.cs, TurnTests.cs |
| Assets/_Game/Tests/PlayMode | MovementTests.cs, InputPresentationTests.cs, SceneIntegrationTests.cs, CombatLifecycleTests.cs, CombatInputTests.cs |
| tools | Run-UnityTests.ps1 |
| docs | battle-skeleton-guide.ko.md, superpowers/specs/2026-10-05-battle-skeleton-design.md, superpowers/plans/2026-10-05-battle-skeleton.md |
| Artifacts | 테스트 XML·로그, progress.md, battle-selected.png, battle-cover.png |

기존 SampleScene과 Build Settings를 유지했다. 외부 에셋·새 패키지를 추가하지 않았다.
기존 URP, Input System, Unity Test Framework를 사용한다.

## 각 클래스의 책임

| 클래스 | 책임 |
| --- | --- |
| GridCell | 일반 C# 셀 데이터: 좌표, 중심 위치, 통행, 점유 유닛, 8방향 엄폐. Occupied는 점유 유닛에서 계산한다. |
| GridManager | 셀 생성, 좌표 변환, 장애물의 점유 영역·사격 차단 등록, 셀 중심 간 사격 경로 검사, 유닛 점유 이동·해제. |
| GridObstacle | 건물형 장애물 표시와 BoxCollider 제공. 이동과 사격을 막는다. 바닥이나 일반 엄폐물에는 붙이지 않는다. |
| Pathfinder | 같은 비용의 4방향 BFS. 실제 이동 범위는 남은 AP와 이동 범위 중 작은 값으로 제한하고, AI의 공격 위치 탐색은 전체 그리드에서 경로를 찾는다. |
| BattleUnit | Inspector 이동 범위·시작 셀·높이 오프셋, 현재 셀과 이동·회피 컴포넌트 연결. |
| UnitMovement | 경로를 따라 이동하고 도착 시 점유와 이동 칸 수를 갱신한다. 중단 시 마지막 도착 셀로 돌아간다. |
| UnitMobility | 이번 이동 칸 수와 기동 회피. 새 이동·향후 턴에서 사용할 초기화 메서드. |
| UnitHealth | 7개 부위 HP, 기체 상태, 주요 부위 파괴 개수와 완전파괴 판정, 최종 상태 변경 알림. |
| UnitActionPoints | 최대·현재 AP, 비용 검증·소모·턴 회복. |
| UnitCombat | 공격력·기본 명중률·가로·세로 합산 사거리·공격 비용 설정. |
| CombatMath / CombatSystem | 사거리·엄폐 방향·명중 계산, 공격 검증·AP 소모·난수·피해·결과. |
| TurnManager | 현재 진영·행동 허용, 진영별 AP·회피 초기화, 적 순차 실행·중단, 턴 번호와 전투 종료. |
| EnemyAIController | 적 한 기체의 목표·경로 선택, 이동 완료 대기, 공통 CombatSystem으로 공격. |
| CoverTypes | 북·북동·동·남동·남·남서·서·북서 8방향과 None/Half/Full의 공통 정의. |
| CoverObject | Half/Full Inspector 선택과 BoxCollider 제공. |
| CoverSystem | 엄폐물의 점유 셀에서 인접 셀의 방향별 엄폐를 계산하고 별도 명중률 배율을 제공한다. |
| PlayerInputController | 유닛 선택과 Menu/Move/Attack 모드, UI 클릭 우선 처리, 이동·대상 선택·확정 사격·ESC 해제. |
| GridVisualizer | Cube로 그리드 선·청록색 이동 범위·빨간 공격 범위·선택 표시. 표현 Cube의 Collider는 제거한다. |
| UnitActionUI | 선택 기체를 따라 Move/Attack/Eject와 상태, Melee/Ranged 선택 메뉴, 대상·명중률·Strike/Fire 확정을 표시. |
| BattleDebugUI | IMGUI로 선택 유닛의 이동·엄폐 데이터를 별도로 표시. UI 영역 클릭을 구분한다. |
| BattleSceneController | 그리드 → 장애물 → 엄폐 → 유닛 → 화면·입력 순서로 한 번 초기화한다. |
| BattleTestSceneBuilder | Editor 전용 테스트 Scene·재질·Prefab 생성 메뉴. 런타임 규칙은 갖지 않는다. |

## 시스템 연결

```text
BattleSceneController
  ├─ GridManager.Generate
  ├─ GridObstacle의 BoxCollider 등록
  ├─ CoverSystem.Build → 엄폐 셀 차단 + 이웃 셀의 방향별 엄폐
  ├─ BattleUnit.Initialize → 시작 셀 점유 + 이동/회피/HP/AP 연결
  ├─ CombatSystem → EnemyAIController → TurnManager 순서로 공통 공격·적 행동·진영 턴 연결
  └─ GridVisualizer / BattleDebugUI / PlayerInputController 초기화

마우스 클릭
  → PlayerInputController
  → Pathfinder의 이동 범위 / 최단 경로
  → UnitMovement의 셀 중심 이동
  → GridManager의 점유 변경 + AP 1 소모 + UnitMobility.RecordCellMoved
  → 도착 셀 엄폐 조회 / 이동 범위·UI 갱신
```

이동 경로 길이는 시작 셀을 제외한 셀 수다. 장애물 우회도 이동 거리에 포함된다.
다른 유닛의 점유 셀은 통과와 목적지 선택이 모두 불가능하다.
유닛의 셀 좌표·엄폐는 이동 도중 마지막으로 도착한 셀을 기준으로 한다.
동시 이동은 지원하지 않는다.

## Unity Editor에서 직접 구성하기

빈 Scene을 저장한 뒤 아래 오브젝트를 만든다. 또는 제공한 Prefab을 드래그한다.

| GameObject | 필요한 컴포넌트·설정 |
| --- | --- |
| Battle Systems | GridManager, CoverSystem, GridVisualizer, BattleDebugUI, PlayerInputController, BattleSceneController, CombatSystem, TurnManager, EnemyAIController |
| Ground | Cube, Renderer, BoxCollider. 위치 (10,-0.25,10), 크기 (20,0.5,20). GridObstacle/CoverObject는 붙이지 않는다. |
| Battle Unit | Capsule, Renderer, CapsuleCollider, BattleUnit. UnitMovement·UnitMobility·UnitHealth·UnitActionPoints·UnitCombat은 RequireComponent로 자동 추가된다. 팀은 Player. |
| Dummy Enemy | 빨간 Capsule과 위 유닛 컴포넌트. 팀은 Enemy, 시작 셀 (3,3). Units 목록에 포함한다. |
| Half / Full Cover | Cube, Renderer, BoxCollider, CoverObject. Cover Type은 Half 또는 Full. |
| Obstacle | Cube, Renderer, BoxCollider, GridObstacle. CoverObject를 함께 붙일 필요는 없다. |
| Battle Camera | Camera, AudioListener. Orthographic, Size=15, 위치 (34,28,-22), (10,0,10)을 바라보도록 설정. MainCamera 태그. Viewport Rect X=0.23, Y=0, W=0.77, H=1. |
| Sun | Directional Light. 회전 (50,-35,0), Intensity=1.4. |

BattleSceneController의 Inspector에서 다음 참조를 연결한다.

- Grid, Cover System: Battle Systems의 해당 컴포넌트.
- Units: Scene의 모든 BattleUnit. Capsule을 복제하면 이 목록에도 추가한다.
- Obstacles: 일반 장애물의 GridObstacle 목록.
- Covers: 엄폐 Cube의 CoverObject 목록.
- Battle Camera: 위 카메라.
- Input Controller, Visualizer, Debug UI: Battle Systems의 해당 컴포넌트.
- Combat System, Turn Manager: Battle Systems의 해당 컴포넌트.

GridVisualizer에는 GridLines, MoveRange, Selection 재질을 연결한다.
없으면 기본 표시 재질을 런타임에 만든다.
입력·화면 클래스는 Controller가 초기화하므로 별도 Awake 순서 설정은 필요 없다.

GridManager: Width=10, Height=10, Cell Size=2, Transform 위치 (0,0,0), 회전 0, 스케일 1.
BattleUnit: Start Grid Position=(1,1), Movement Range=5, Height Offset=1.
시작 셀은 Transform 위치에서 추론하지 않는다. Play 시작 시 시작 좌표의 중심으로 배치한다.
유닛을 복제할 때 Start Grid Position을 반드시 서로 다르게 설정한다.

셀 (x,z)의 월드 중심은 `원점 + ((x+0.5)*CellSize, 0, (z+0.5)*CellSize)`이다.
기본 엄폐 Cube의 XZ 크기는 (2,2), Half 높이 1, Full 높이 2다.
Cube 바닥을 그리드 높이에 맞추고 회전 없이 셀 중심에 배치한다.
Kind 설정이 판정을 결정하며 높이·색은 사람이 구별하기 위한 표현이다.

그리드 크기·엄폐·장애물은 Play 전에 조정하고 다시 시작한다.
바닥 크기와 카메라 구도는 그리드 변경에 맞춰 직접 조정한다.
Play 도중 그리드 재생성, 엄폐 이동·파괴 자동 추적은 지원하지 않는다.

## 테스트 Scene 생성 메뉴

`MechaXCOM > Create Battle Test Scene`으로 예제를 다시 만들 수 있다.
현재 Scene은 먼저 저장해야 한다. 기존 BattleTest가 있으면 덮어쓰기 확인을 표시한다.
기존 재질·Prefab은 재사용하므로 수정한 에셋을 강제로 초기화하지 않는다.
생성기는 기존 Build Settings를 바꾸지 않는다.

기본 배치는 플레이어 (1,1), 더미 (3,3), Half (3,2)/(6,5), Full (3,4)/(7,7),
일반 장애물 (4,2)/(4,3)/(4,4)다.

## Movement Evasion 계산 위치

`UnitMobility.RecordCellMoved()`가 셀 도착마다 계산한다.

| 이동 칸 수 | 기동 회피 |
| --- | --- |
| 0 | 0 |
| 1~2 | +5 |
| 3~4 | +10 |
| 5 이상 | +15 |

Short/Medium/Long Move Bonus를 Inspector에서 바꿀 수 있다.
`UnitMovement.TryMove()`는 유효한 경로를 찾은 뒤 `ResetMovement()`를 호출한다.
유효하지 않은 이동 요청은 기록을 초기화하지 않는다.
경로 중단 시 실제 도착한 칸 수만 반영한다.

## Cover 판정 방식

`GridManager.RegisterObstacle()`가 BoxCollider의 월드 XZ 영역에 중심점이 포함된 셀을 막는다.
큰 Cube는 여러 셀을 막을 수 있다. 셀 중심을 포함하지 않거나 회전한 Cube는
이름을 포함한 경고와 함께 등록하지 않는다.

`CoverSystem.Build()`는 엄폐물의 점유 셀 주변에서 걸을 수 있는 셀에 엄폐를 제공한다.
엄폐 셀이 유닛 북쪽이면 North에 기록한다. 방향이 겹치면 Full이 Half보다 우선한다.
North=+Z, East=+X, South=-Z, West=-X다.
상하좌우와 대각선 인접 셀에 8방향 엄폐를 기록한다. Full은 전 방향 보호나 공격 완전 차단을 의미하지 않는다.
배율은 CoverSystem의 Half Cover Multiplier=0.75, Full Cover Multiplier=0.5에서 조회한다.

생성기의 테스트 예제에서는 (3,3) 셀이 North=Full(×0.5), South=Half(×0.75)다.
그 예제의 (3,3)은 적이 점유하여 이동할 수 없다. 직접 편집한 실제 맵의 배치는 다를 수 있다.
이동 회피와 방향별 엄폐는 별도 값으로 유지하고 공격 판정 시 조회한다.

## 테스트 실행

Editor에서 `Window > General > Test Runner`를 열고 EditMode와 PlayMode를 각각 실행한다.
SceneIntegrationTests는 테스트 전용 저장 씬을 생성해 불러온다. 사용자 BattleTest 씬의 좌표나 카메라 속도를 검증하지 않는다.
테스트 준비는 원본 편집 씬을 저장하지 않고, 종료 후 테스트가 생성한 전용 폴더만 정리한다.

Editor가 닫혀 있을 때 PowerShell로 실행할 수도 있다.

```powershell
.\tools\Run-UnityTests.ps1 -Name enemy-ai-verified-editmode -Mode EditMode
.\tools\Run-UnityTests.ps1 -Name enemy-ai-verified-playmode -Mode PlayMode -Graphics
```

같은 프로젝트가 Editor에 열려 있을 때는 배치 실행을 병행하지 않는다.
XML과 로그는 Artifacts에 저장한다. 테스트용 임시 마우스는 테스트 후 제거하고
Input System의 포커스 관련 설정도 복원한다. 프로젝트 입력 설정을 변경하지 않는다.

검증 대상은 좌표·경계, BFS 우회·점유·거리 제한, 회피 경계값,
엄폐 8방향·대각선 최강 엄폐 배율·크기·중첩, 이동 중 명령·중단·삭제,
필수 참조 누락, Scene 생성·재열기·Prefab 구성, 화면 표시용 Collider 제거,
실제 Input System 이벤트를 통한 클릭 선택·목적지 이동이다.

`combat-selected.png`, `combat-moved.png`, `combat-target.png`는 테스트 중 살아 있는 Scene을
카메라로 렌더링한 이미지다. IMGUI 패널과 OS Game View는 포함하지 않는다.
배치 실행에는 실제 OS 마우스 조작을 자동화하는 연결이 없으므로,
실물 Editor에서 직접 마우스를 움직인 사용감과 UI 시각 확인은 별도 수동 항목이다.

## 공격·턴 수치 조절

- 대상 기동 회피: `BattleUnit.MovementEvasion`.
- 대상 셀: `GridManager.TryGetCell(unit.CurrentGridPosition, out cell)`.
- 방향의 엄폐 종류: `cell.GetCover(direction)`.
- 방향 자체의 엄폐 배율: `CoverSystem.GetCoverMultiplier(cell, direction)`.
- 공격 방향에 적용되는 배율: `CoverSystem.GetAttackCoverMultiplier(cell, direction)`.
- 턴 종료: `PlayerInputController.TryEndTurn()`.
- 입력 행동 모드 선택: `PlayerInputController.BeginMove()` / `BeginAttack()`.
- 입력 이동·대상·확정 사격: 모드를 선택한 뒤 `TryMoveSelected()` / `TrySelectTarget()` / `TryAttackSelected()`.
- 공격 미리보기 / 실행: `CombatSystem.GetPreview()` / `TryAttack()`.

Play를 멈춘 상태에서 유닛 Inspector의 UnitHealth의 부위별 Max HP,
UnitActionPoints.Max Points(5), UnitCombat.Damage(5), Base Hit Chance(80),
Range(7), Attack Cost(3)를 바꾼다. 근접 설정은 같은 컴포넌트의 Melee 항목에서 조절한다.
UnitMovement.Movement Speed(코드 기본값 12)는 m/s이다.
화면에서 움직이는 속도와 칸별 AP 비용은 독립적이다.

공격 사거리는 `abs(가로 차이) + abs(세로 차이)`로 계산한다.
사거리 5에서는 (5,0), (3,2) 차이까지 공격 가능하고 (3,3)은 6칸으로 불가능하다.
빈 평지에서는 공격 범위가 마름모 모양이며, 플레이어와 적 AI가 같은 거리 기준을 사용한다.
이동은 계속 상하좌우 BFS를 사용한다.

엄폐 방향은 대상에서 공격자를 바라보는 각도를 45도 폭의 8방향으로 나눈다.
각 방향의 중심에서 ±22.5도 범위이며 경계는 시계 방향 쪽에 배정한다.
동서남북 공격은 해당 방향의 엄폐만 적용한다. 대각선 공격은 해당 대각선과
양옆 방향의 엄폐 중 가장 강한 하나(최소 배율)만 적용한다. 합산이나 중복 곱셈은 없다.
남동쪽 공격은 남·남동·동을 확인한다. 셋 다 Full이어도 ×0.5, Half만 있으면 ×0.75다.
그 밖의 방향은 기여하지 않는다. Inspector 배율은 0~1이며 가장 작은 값을 적용한다.
원거리 명중률은 `max(0, 무기 기본 명중률 - 대상 이동 회피) × 공격자 머리 상태 배율 × 부위 배율 × 엄폐 배율`이다. 최종 결과를 0~100으로 제한하고 한 번만 반올림한다(0.5는 올림). 근접은 `max(0, 기본 명중률 - 대상 이동 회피) × 공격자 머리 상태 배율`이다.
테스트 예제의 플레이어 (1,1) → 더미 (3,3)은 남서쪽 공격으로 South Half를
적용하여 Body 조준은 80×0.75=60%다. 양 진영에 동일한 계산을 적용한다.
빗나가도 AP 3을 소모한다. 유효하지 않은 요청은 AP·HP·난수를 변경하지 않는다.
더미 처치 시 점유를 즉시 해제하고 클릭 대상과 전투 행동을 종료한다.

## 건물의 사격 차단

`GridObstacle`이 있는 건물은 등록된 점유 셀에 `BlocksShots`를 기록한다.
공격자와 대상의 셀 중심을 잇는 직선이 건물 셀을 지나거나 모서리에 걸치면 공격 불가다.
건물 옆의 열린 경로는 사격할 수 있다. `CoverObject`만 있는 Half/Full 엄폐물은
사격을 차단하지 않고 기존 방향별 명중률 보정만 제공한다.

가려진 대상도 선택할 수 있지만 UI에 `Hit: BLOCKED BY BUILDING`과 차단 이유가 표시되며
Fire 버튼이 비활성화된다. API로 공격을 요청해도 AP·HP·난수를 소모하지 않는다.
양 진영에 같은 규칙을 적용하며 AI는 사거리와 사격 경로가 모두 확보된 셀을 찾는다.
이번 턴에 이동·공격 비용이 부족하면 그 위치로 접근하고, 경로가 없으면 대기한다.

건물 Collider의 기존 셀 점유 규칙을 사용한다. 높이·발사 부위·유닛에 의한 사격 차단은
계산하지 않으며, Play 중 건물 이동·파괴의 자동 반영도 지원하지 않는다.
배치는 Play 전에 조정한다. 기존 BattleTest 씬을 다시 생성할 필요는 없다.

## 의도적으로 제외한 기능

유닛 탐지·안개 시야, 탄약과 장비별 무기 시스템,
파일럿 스탯·관계, 부스트, 열 관리, 기체 커스터마이징, Save/Load,
Mission System은 없다. 자유 배치 경계 벽, 동시 이동, 런타임 엄폐 이동·파괴,
카메라 줌과 이동 애니메이션도 없다.

## 카메라 방향 전환

Play 상태에서 Game View를 클릭해 입력 포커스를 준 뒤 Q/E를 누른다.
Q는 현재 시점에서 왼쪽으로 90도, E는 오른쪽으로 90도 회전한다.
맵 중심을 축으로 회전하며 방향키는 카메라를 조작하지 않는다.
카메라 위치와 바라보는 방향을 함께 돌리므로, 각 회전이 끝난 뒤 같은 키를 네 번 누르면
처음의 위치·바라보는 각도·줌으로 돌아온다. 맵 중심으로 바라보는 각도를 다시 맞추지 않는다.

Battle Camera의 `Battle Camera Orbit > Rotation Duration`은 회전 시간(초)이다.
기본은 0.45초이며, 줄이면 빠르게, 늘리면 천천히 전환한다.
회전 도중의 Q/E 입력은 무시한다. 회전이 끝난 뒤 다시 누르면 다음 90도 회전을 시작한다.

맵 중심은 GridManager의 위치·Width·Height·Cell Size로 계산한다.
Play 시작 시 작성한 카메라 위치·회전·Orthographic Size를 유지하며,
첫 Q/E 입력부터 현재 높이와 맵 중심까지의 수평 거리를 유지해 회전한다.
맵을 넓혔을 때 바닥 크기와 카메라 줌은 직접 조정한다.

WASD를 누르고 있으면 현재 카메라 방향을 기준으로 지면 위를 이동한다.
W는 앞쪽, S는 뒤쪽, A는 왼쪽, D는 오른쪽이다. 높이·바라보는 각도·줌은 유지한다.
`Battle Camera > Battle Camera Orbit > Pan Speed`로 이동 속도(m/s, 기본 12)를 조절한다.
대각선 이동도 같은 속도이며, 이동한 위치를 기준으로 Q/E 회전이 이어진다.
회전·더블클릭 중앙 배치 중에는 WASD 이동을 잠시 무시한다.
ESC/WASD 변경 후 전체 그래픽 PlayMode 테스트 56개가 통과했다.

카메라 최초 추가 시 EditMode 80개, PlayMode 42개 모두 통과했다.
Q/E 변경 후에는 임시 프로젝트 사본에서 전체 PlayMode 43개가 통과했다.
입력·회전·맵 보존 근거는 `Artifacts/camera-progress.md`에 기록했다.

## 소대 선택과 더블클릭 카메라

아군 기체를 마우스 왼쪽으로 한 번 클릭하면 선택하고 기체 옆에 Move/Attack/Eject를 표시한다.
Attack을 누르면 바로 옆에 Melee/Ranged 선택 메뉴만 열리며, 아직 범위나 대상 선택은 활성화하지 않는다.
오른쪽 공간이 부족하면 확장 메뉴를 왼쪽에 표시한다. Move를 선택해야 이동 범위와 목적지 클릭이 활성화된다.
확장 메뉴에서 Melee 또는 Ranged를 선택해야 해당 빨간 공격 범위와
적 선택이 활성화된다. 적 선택만으로 공격하지 않으며 명중률 확인 후 Strike 또는 Fire로 확정한다.
공격 범위는 가로·세로 합산 사거리와 건물 차단을 반영하며, 아군 점유 셀과 사격이 차단된 셀은 표시하지 않는다.
이동 불가여도 사격이 통과하는 골짜기 셀은 빨간 공격 범위에 포함된다.

샘플 씬의 `Sample Ravine 1~3`은 어두운 바닥 표식으로 표현한 골짜기다.
`GridObstacle`의 `Blocks Shots`를 끄면 이동·진입은 차단하고 사격은 통과시킨다.
켜두면 기존 건물처럼 이동과 사격을 모두 차단한다. 골짜기를 옮기거나 크기를 바꿀 때는
유닛 및 기존 장애물과 겹치지 않도록 하고, `Battle Systems`의 `Obstacles` 목록에 등록한다.
현재 골짜기는 전술 판정용 표식이며 실제 바닥을 파거나 높낮이 판정을 추가하지 않는다.

소대 전투는 별도 씬 `Assets/_Game/Scenes/SqurdBattleTest.unity`에서 테스트한다.
월드 크기는 기존 샘플과 동일한 40×40이며, 2m 셀 20×20개를 사용한다.
아군 3기는 남쪽, 적 3기는 북쪽에 배치했다. 건물 4곳, 골짜기 3곳,
Half 엄폐물 6개, Full 엄폐물 4개 사이에 우회로를 남겼다.
유닛별 AP 5, 이동 1칸당 AP 1, 공격 AP 3을 사용한다. HP는 아래의 7개 부위로 관리한다.
원거리는 합산 사거리 7/기본 명중률 80%/피해 5이며 기존 방향별 엄폐와 기동 회피를 적용한다.
근접은 합산 사거리 2/기본 명중률 95%/피해 8이며 엄폐 보너스만 무시하고 기동 회피는 적용한다.
근접 범위에는 가로 1+세로 1의 대각선도 포함하지만, 범위 안에 이동 가능한 연결 경로가 있어야 한다.
골짜기·건물·엄폐 점유 셀·다른 유닛을 가로질러 근접 공격할 수 없다. 실제 이동/AP 이동 비용을 추가하지는 않는다.
메뉴의 Attack → Melee 또는 Ranged를 선택하고 대상·명중률·피해를 확인한 뒤 Strike 또는 Fire로 확정한다.
적 AI는 근접 가능한 가까운 대상에게 근접 공격을 우선 사용하며, 그 외에는 기존 원거리 접근/사격을 수행한다.
`BattleTest.unity`는 기존 배치 그대로 유지하며, 두 씬은 별도로 열어 플레이한다.
행동을 고른 뒤에는 메뉴를 숨겨 가까운 목적지나 적을 가리지 않는다. 대상 선택 시 사격 확인 메뉴가 다시 나타난다.
이동/사격 완료 후 선택은 유지하고 기본 메뉴로 돌아온다. 다른 행동을 고르려면 기체를 다시 클릭할 수 있다.
메뉴 상태에서 빈 바닥이나 맵 밖을 클릭하면 선택을 해제한다. UI 클릭은 맵 명령으로 전달하지 않는다.
ESC를 누르면 선택·공격 대상·범위 표시를 해제한다. S는 카메라 뒤로 이동에 사용한다.
기체 옆 메뉴도 함께 닫힌다. 적 턴에는 메뉴와 범위를 숨기고, 다음 플레이어 턴에는 메뉴 상태로 돌아온다.
AP가 부족한 행동 버튼과 건물에 가려진 대상의 Fire 버튼은 비활성화된다.
기존 씬에서는 PlayerInputController가 UnitActionUI를 런타임에 한 개 연결하므로 씬을 재생성하지 않는다.
이동 중에도 선택 해제가 가능하지만
이미 명령한 이동은 계속 진행하며, 선택 해제 자체는 AP를 소모하거나 회복하지 않는다.
각 기체는 개별 AP를 갖는다. 기본 5이며, 해당 기체가 이동한 칸당 1, 공격당 3을 소모한다.
기체를 바꿔 선택하거나 카메라를 이동해도 어느 기체의 AP도 회복하거나 소모하지 않는다.
턴을 종료하면 등록된 살아 있는 아군들의 AP가 각각 회복된다.

같은 아군 기체를 0.3초 안에 두 번 클릭하면 각도·높이·줌을 유지하면서 부드럽게 이동하여
그 기체를 전투 카메라 영역의 중심에 배치한다. 왼쪽 UI 영역은 카메라 화면에 포함하지 않는다.
다른 기체·적·UI·바닥·장애물을 사이에 클릭하면 더블클릭 연결은 끊긴다.
`Battle Systems > Player Input Controller > Double Click Interval`로 인식 간격(초),
`Battle Camera > Battle Camera Orbit > Focus Duration`으로 카메라 이동 시간(기본 0.35초)을 조절한다.

중앙 배치 이후 Q/E는 그 지점을 중심으로 회전한다. 카메라 전환 중 추가 카메라 명령은 무시하며,
기체가 이동해도 카메라는 자동으로 따라가지 않는다. 다시 더블클릭하면 이동한 위치를 중앙에 배치한다.

아군을 추가 배치할 때는 Player 팀의 기체마다 서로 다른 Start Grid Position을 지정하고,
BattleSceneController의 Units 목록에 모두 등록한다. 새 예제 기체나 맵 배치는 자동으로 추가하지 않는다.

더블클릭·소대 AP 검증을 포함하여 EditMode 80개, 그래픽 PlayMode 49개가 모두 통과했다.

GridObstacle 건물이 사격 경로에 있으면 공격할 수 없다. Half/Full 엄폐물은 기존 명중 보정이며 완전한 사격 차단이 아니다.
검증 결과와 실행 중 결정은 `Artifacts/combat-progress.md`와 개별 테스트 XML에 기록한다.

## 이전 스켈레톤 검증 기록

아래 55개는 확장 전 기록이다. 현재 전투 확장의 검증은 마지막 절을 참고한다.

- `Artifacts/final-editmode.xml`: 37개 통과, 실패 0개.
- `Artifacts/final-playmode.xml`: 18개 통과, 실패 0개. 그래픽 모드에서 실행했다.
- 저장된 Scene을 직접 로드하여 Input System 마우스 이벤트로 선택·이동했다.
- 1~5칸 반복 이동, 장애물 우회, 점유 차단과 제거 후 해제, 이동 중 명령 차단을 확인했다.
- (3,3)에서 회피 +10, North Full +30, South Half +15를 별도로 확인했다.
- 카메라 렌더 이미지 2개에서 기본 도형, 그리드, 범위 표시, 엄폐 배치를 확인했다.
- 작은 640×480 Game View에서도 UI가 위 클릭을 막지 않는 통합 테스트가 통과했다.
- 정상 구성 Scene의 실행에서 Unity Test Framework가 예상 밖 오류·경고를 보고하지 않았다.
- OS 마우스를 직접 조작한 수동 테스트와 IMGUI 패널의 최종 시각 검사는 실행하지 못했다.

## 독립 코드 검토

엄폐물·일반 장애물 클릭이 뒤쪽 셀의 이동으로 해석되던 문제를 실제 입력 이벤트로 재현했다.
두 회귀 테스트가 수정 전 실패했고, 해당 Collider 클릭을 무시하도록 수정한 뒤 통과했다.
이제 엄폐물을 클릭해도 이동하거나 기존 회피가 초기화되지 않는다.
셀 중앙이 물체에 가려진 경우에는 그 셀의 보이는 모서리 부분을 클릭한다.

정상 예제 실행을 막지 않는 다음 두 Inspector 예외의 개선은 남겨 두었다.

- Controller의 Input Controller 참조만 비우면 입력이 동작하지 않지만 누락 경고가 없다.
  입력이 안 되면 이 참조를 먼저 확인한다.
- 선택한 유닛의 UnitMovement를 끄면 범위가 남던 문제는 이번 전투 입력 확장에서
  선택·대상을 해제하고 범위 표시도 지우도록 보완했다.

## HP·AP·공격 확장 검증

- `Artifacts/combat-final-editmode.xml`: 80개 통과, 실패 0개.
- `Artifacts/combat-final-playmode.xml`: 36개 통과, 실패 0개. 그래픽 모드 실행.
- HP·중복 사망, AP 부족·도착 비용·중단 비용, 직선 사거리 경계, 엄폐 방향,
  확률 경계, 명중·빗나감, 턴 회복, 더미 처치·점유 해제·전투 종료를 검증했다.
- 저장 Scene을 직접 로드하여 적 클릭·초기 명중률 60%, 이동 후 공격·공격 후 이동,
  실제 Input System 이벤트와 UI 영역 클릭 차단을 검증했다.
- 기존 Scene과 플레이어 Prefab을 보완하여 원래 GUID와 기존 이동 속도를 보존했다.
  갱신 전 사본은 Artifacts의 `*-before-combat.*.backup`에 보관했다.
- 자동 검증과 카메라 렌더 결과는 실제 OS 마우스 조작·IMGUI 시각 확인을 대신하지 않는다.
- 구현 중 결정은 `Artifacts/combat-progress.md`, 독립 검토와 처리 결과는 `Artifacts/combat-code-review.md`에 기록했다.
- 직접 유닛을 삭제해도 전투 종료가 갱신되고, Scene 보완 시 지정한 참조·비활성 오브젝트·명시적으로 비운 목록을 보존한다.

정상 예제에서는 행동 컴포넌트를 켜 둔다. 다음 Inspector 예외는 보류했다.

- UnitActionPoints를 끄면 새 이동·공격을 거절하며 진행 중인 이동도 마지막 도착 칸에서 중단한다.
  플레이어의 범위 표시는 남을 수 있다. 정상 예제에서는 AP 컴포넌트를 켜 둔다.
- UnitMovement를 끈 바로 그 콜백에서 Update 전에 공격 API를 호출하면 공격할 수 있다.
  다음 Update에서 선택·대상·범위는 해제된다.

## 원거리 부위 조준

`Attack → Ranged → 적 선택` 후 화면 오른쪽에 기체 조준 창이 나타난다.
처음에는 `Mecha.png`와 전체 조준 명중률이 표시된다. 그림의 머리, 몸통, 콕피트,
양팔, 양다리를 클릭하면 해당 강조 이미지로 바뀌고 최종 명중률을 표시한다.
좌우는 기체 자신의 좌우를 기준으로 하므로 화면 왼쪽 팔이 기체의 오른팔이다.

| 부위 | 원거리 조준 배율 |
|---|---:|
| 전체 / 몸통 | ×1.0 |
| 콕피트 | ×0.45 |
| 머리 | ×0.8 |
| 양팔 / 양다리 | ×0.9 |

기본 명중률에서 기동 회피를 뺀 값에 위 부위 배율과 엄폐 배율을 곱하고 마지막에 한 번 반올림한다.
부위를 클릭하는 것만으로 AP는 소비하지 않는다. `Fire`를 누르면 표시한 확률로
공격을 판정하며 기존 3 AP를 소비한다. 전체 조준 버튼으로 기본 조준으로 돌아갈 수 있다.
새 대상을 선택하거나 원거리 공격을 다시 시작하면 전체 조준으로 초기화한다.
ESC로 선택과 조준 창을 닫는다. 근접 공격에는 이 창과 부위별 패널티를 적용하지 않는다.

피해는 선택한 부위 HP에만 적용한다. 주요 부위 파괴 수와 Body/Cockpit 파괴에 따라 상태를 판정한다. 머리와 다리 파괴 효과는 아래 설명을 따른다. 팔의 기능 저하는 아직 구현하지 않았다.
그림의 클릭 영역은 조준 창 내부의 부위별 영역으로 판정한다.
이미지 원본은 `Assets/_Game/Art/UI/Targeting/`에 유지하며,
`Assets/_Game/Resources/BodyTargetArt.asset`이 이 이미지들을 참조해 빌드에 포함한다.
이미지 파일을 바꾼 경우 `MechaXCOM > Configure Body Target Art` 메뉴로 참조를 갱신할 수 있다.

## 기초 적 AI와 진영별 턴

HUD의 `Turn: N / Player` 또는 `Turn: N / Enemy`에서 현재 진영을 확인한다.
적 행동 중에는 `Acting:` 뒤에 해당 기체 이름이 표시된다. 턴 번호는 플레이어·적 한 사이클이 끝날 때 증가한다.

적은 BattleSceneController의 Units 등록 순서로 한 기체씩 처리한다.

1. 현재 공격 가능한 가까운 아군을 한 번 공격한다.
2. 이동 후 공격이 가능하면 공격 AP를 남기고 이동한다. 기본 AP5·공격 비용3에서는 최대 2칸 이동 후 공격한다.
3. 이번 턴에 공격할 수 없으면 공격 위치까지의 경로를 따라 이동 범위·AP 안에서 접근한다.
4. 길이 없거나 사거리 안이지만 공격 AP가 부족하면 대기한다.

적도 이동 한 칸당 AP1, 공격당 AP3을 사용하며 빗나가도 공격 비용을 소모한다.
공격 비용·사거리·이동 범위를 바꾸면 AI도 그 값을 사용한다. 양쪽의 엄폐·기동 회피·확률·피해 규칙은 같다.
사거리 안이어도 건물이 사격 경로를 막으면 공격할 수 없다. AI는 경로가 열린 사격 위치로 접근한다.
엄폐 자리 선택·집중 공격·후퇴 같은 고급 전술은 아직 구현하지 않았다.

현재 행동하는 적 한 기에는 아군 선택 표시와 같은 모양의 빨간 바닥 테두리가 나타난다.
테두리는 이동 중 기체를 따라가며, 공격 후 대기 구간까지 유지한 뒤 다음 적으로 옮겨간다.
아군 턴으로 돌아오거나 전투가 종료되면 사라진다. AI가 중지된 경우에도 표시를 해제한다.
`Battle Systems > Turn Manager > Enemy Action Delay`로 각 적의 행동 후 대기 시간을 조절한다. 기본값은 0.25초다.
마지막 적도 같은 시간만큼 표시한 뒤 아군 턴으로 돌아오며, 대기 시간을 0으로 설정하면 최소 한 프레임 유지한다.
적 턴에도 ESC 선택 해제와 WASD·Q/E 카메라 조작은 가능하다. 이동 중 선택 제한은 유지하며,
이동하지 않는 구간에는 아군을 선택하거나 더블클릭하여 중앙에 배치할 수 있다.

기존 저장 씬의 Enemy AI 참조가 없으면 Play 시작 시 지정된 TurnManager 오브젝트의 AI를 재사용하거나 한 개 추가한다.
유닛·장애물·엄폐물 참조와 시작 위치, 맵 크기, 카메라 속도는 바꾸지 않는다. 씬 재생성 메뉴를 실행할 필요가 없다.
AI를 꺼 두거나 연결이 유효하지 않으면 플레이어 턴 종료 요청을 거절하고 경고를 표시한다.
AI 또는 TurnManager를 끄면 진행 중 적 이동을 취소하고 도착한 칸의 비용만 유지한다.
둘 다 다시 활성화되면 다음 적부터 이어가며, 중단한 적의 행동이나 AP 회복을 반복하지 않는다.

새 검증 결과와 독립 검토 내용은 `Artifacts/enemy-ai-progress.md`와 `Artifacts/enemy-ai-code-review.md`에 기록한다.
저장 씬 테스트는 `Assets/_Game/Tests/GeneratedEnemyAIIntegration`을 잠시 사용한다. 이 폴더가 이미 존재하면 덮어쓰지 않는다.

## 부위 피해와 긴급탈출

전체 HP는 사용하지 않는다. UnitHealth의 Inspector에서 각 부위 최대 HP와 Destruction Chance(기본 25%)를 조절한다.

| 부위 | HP | 원거리 조준 배율 |
| --- | ---: | ---: |
| Body / Torso | 20 | ×1.0 |
| Cockpit | 6 | ×0.45 |
| Head | 6 | ×0.8 |
| LeftArm / RightArm | 각각 8 | ×0.9 |
| LeftLeg / RightLeg | 각각 10 | ×0.9 |

원거리 기본 조준(Whole)은 Body에 피해 5를 준다. 부위를 클릭하면 해당 부위 HP와 명중률을 보여주고 Fire로 확정한다.
기본 명중률 80, 회피 0일 때 콕피트 명중률은 36%, Half Cover 27%, Full Cover 18%다. Full Cover와 회피 5는 17%다. 두 번 명중하면 콕피트 6이 파괴된다.
근접은 Body에만 피해 8을 주며, 조준 이미지와 부위 패널티는 사용하지 않는다.
초과 피해는 다른 부위로 넘어가지 않고 이미 파괴된 부위를 맞혀도 부위 피해는 0이다.

주요 부위는 머리·양팔·양다리 5개다. 서로 다른 0~2개 파괴는 ACTIVE를 유지한다.
세 번째 파괴 시 CRITICAL과 25% 완전파괴 판정을 한 번 처리한다. 난수 0~24는 DESTROYED, 25~99는 CRITICAL이다.
CRITICAL 추가 명중마다 같은 판정을 한 번 수행한다. 파괴된 부위의 명중도 포함하며 빗나감은 재판정하지 않는다.
네 번째 주요 부위 파괴 또는 Body/Cockpit HP 0은 확률 판정 없이 즉시 DESTROYED다.

CRITICAL은 모델·점유를 유지하고 선택·피격·자기 턴 AP 회복이 가능하지만 이동·공격은 불가능하다.
ACTIVE와 CRITICAL 모두 자기 턴에 Eject 버튼으로 AP 1을 소비해 파일럿을 탈출시킬 수 있다.
AP 0이면 다음 아군 턴까지 기다린다. 이동 중이나 적 턴·전투 종료 후에는 탈출할 수 없다.
탈출은 EJECTED 상태를 기록하며 현재 전투에서 재탑승하지 못한다. 적 AI는 CRITICAL 행동을 건너뛰고 자동 탈출하지 않는다.
DESTROYED/EJECTED는 점유·모델·Collider를 해제하고 GameObject는 전투 결과 조회를 위해 유지한다.
DESTROYED는 파일럿 사망을 뜻하지 않는다. 파일럿 부상·영구 사망과 임무 후 처리는 아직 구현하지 않았다.

API: `GetCurrentHP(part)`, `GetMaxHP(part)`, `ApplyDamage(part, damage)`, `UnitHealth.State`를 사용한다.
게임 탈출 명령은 `BattleUnit.TryEject()` 또는 선택 기체의 `PlayerInputController.TryEjectSelected()`를 사용한다.
기존 maxHealth 직렬화 값은 Body 최대 HP로 이전되므로 사용자 씬·프리팹을 다시 저장하거나 생성할 필요가 없다.
단계별 테스트와 보호 파일 해시는 `Artifacts/mech-part-damage-progress.md`에 기록한다.

## 머리·다리 파괴 효과

HP가 일부 줄어든 상태에는 기능 페널티가 없고 0이 되었을 때 적용한다.
다리 한쪽 파괴는 기본 이동 범위의 절반(소수점 버림, 양수 범위 최소 1칸), 양쪽 파괴는 이동 불가다.
기본 범위 5는 한쪽 파괴 시 2칸이며, 남은 AP로 다시 제한한다. 기본 이동 범위 0은 그대로 0이다.
이동 AP는 칸당 1을 유지하며 양쪽 다리 파괴 후에도 ACTIVE면 공격·긴급탈출이 가능하다.
이동 중 다리 파괴는 도착한 등록 셀에서 이동을 취소하고 이미 소비한 AP를 유지한다.
범위 표시·경로 탐색·적 AI 모두 같은 손상 이동 범위를 사용한다. 이동 속도는 바꾸지 않는다.

공격자 머리 파괴는 원거리·근접 명중률에 ×0.75를 적용한다. 대상의 머리 조준 배율 ×0.8과는 별개다.
BattleUnit의 Destroyed Head Hit Multiplier에서 손상 배율을 조절한다.
회피가 없을 때 기본 명중률 80의 몸통 조준은 60%, 콕피트 Full Cover 조준은 14%다.
모든 배율을 적용한 뒤 마지막에 한 번 반올림한다. CRITICAL은 이 규칙과 관계없이 이동·공격 불가가 우선한다.

## 라이플 장착 샘플 모델

`Assets/_Game/Prefabs/Weapons/Rifles/BeamRifle.prefab`은 무기 손잡이를 루트 원점으로 맞춘 프리팹이다.
`Model` 아래에 원본 OBJ를 연결하고 `Muzzle`을 총구 끝에 배치했다. Muzzle의 +Z가 발사 방향이다.
`Assets/_Game/Prefabs/Mecha/Samples/MechaReferenceWithRifle.prefab`은 오른손 소켓에 이 라이플을 연결한 샘플이다.
Project에서 프리팹을 더블클릭하면 Prefab Mode에서 확인할 수 있고, 원하는 씬에 직접 드래그해 배치할 수도 있다.
현재 전투 씬의 기체 표현을 자동 교체하거나 전투 로직을 추가하지는 않는다.

메카 `Model > RightHandWeaponSocket`이 메카별 장착 위치·회전을 담당한다.
그 아래의 BeamRifle은 로컬 위치·회전 0, 크기 1로 연결돼 있다. 총구는 무기 내부에 있어 메카별 재설정이 필요 없다.
다른 메카에 장착할 때는 그 메카의 손 위치에 소켓을 새로 맞춘다. 리깅된 모델에서는 손 뼈 아래에 소켓을 둔다.
현재 OBJ는 Unity에서 하나의 메시로 가져오므로 고정 자세의 표시용 샘플이며 손가락·팔 조준 애니메이션은 포함하지 않는다.

원본 OBJ/MTL은 변경하지 않았고 샘플용 URP 재질은 `Assets/_Game/Materials/WeaponSamples`에 있다.
생성 도구 `MechaXCOM > Create Rifle And Mecha Sample Prefabs`는 기존 프리팹을 덮어쓰지 않는다.
생성된 프리팹의 미세 보정은 Prefab Mode에서 한다. 자동 생성 위치는 원본 OBJ의 Main_grip, Muzzle_front_rim, R_fist_core 좌표를 기준으로 한다.
