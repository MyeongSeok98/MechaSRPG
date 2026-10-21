# 소대 공유 시야·탐지 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking. 사용자가 선택한 Native 방식과 단계별 완료 후 확인을 유지한다.

**Goal:** 시작 즉시 소대 공유 탐지를 적용하고, 적은 시야 상실 시 숨김·대기하며, 지형은 탐험 상태에 따라 세 단계로 표시한다.

**Architecture:** UnitVision은 탐지 거리를, VisibilitySystem은 진영별 공유 결과와 칸별 탐험 기록을 소유한다. 그리드 직선 검사로 건물 차폐를 판단하며 Combat/Input/AI는 같은 결과를 조회한다. 유닛 숨김과 지형의 회색 안개 표시는 별도 컴포넌트로 분리한다.

**Tech Stack:** 기존 Unity 6000.5.5f1, C#, Input System, URP, Unity Test Framework. 외부 패키지 추가 없음.

**Spec:** [승인된 설계](../specs/2026-10-12-shared-visibility-design.md)

2026-10-13 추가 범위: 지형의 3단계 전장의 안개와 탐험 기록. Task 4를 추가하며 기존 Task 1~3의 적 추적 기억 없음 규칙은 유지한다. 계획 검토와 사용자 승인이 완료되어 단계별로 구현한다.

## Global Constraints

- 탐지 거리 기본 10칸, 360도 원형, 경계 포함. 기체 방향과 AP는 탐지에 영향 없음.
- Active 기체만 관측한다. Critical 기체는 대상에는 남고, 파괴·탈출 기체는 제외한다.
- 양 진영이 각각 탐지를 공유한다. 마지막 관측자가 시야를 잃으면 즉시 잊는다.
- 건물은 시야 차단. Half/Full 엄폐·골짜기·다른 유닛은 차단하지 않는다.
- 공유 탐지는 개별 공격자의 사거리·건물 사격 차단을 우회하지 않는다.
- 이동은 매 완료 칸에서 갱신하고, 적 목표 상실 시 마지막 완료 칸에서 이동을 취소한다.
- 숨겨진 적의 GameObject/로직/점유를 비활성화하지 않는다. 지형은 계속 표시한다.
- 지형은 현재 시야 안 원래 색, 탐험 후 시야 밖 회색, 미탐험 더 짙은 회색으로 구분한다. 장애물·골짜기·엄폐물 형태는 항상 보인다.
- 지형 탐험 기록은 전투 중 유지하고 새 전투에서 초기화한다. 적 위치 기억·순찰·은신·센서 손상·미지 점유 경로 처리는 추가하지 않는다.
- zoomSpeed=60f, Shadow Max Distance, 이동속도 12f, 카메라 이동속도, 맵 크기·배치와 무기/모델 설정은 보존한다. 씬은 재저장하지 않는다.
- Git 저장소를 새로 만들지 않는다. 기존 분리 검증 프로젝트에서 Unity를 순차 실행한다. 단계별 완료 후 사용자 확인 전 다음 단계는 실행하지 않는다.

## Review Focus

- 시작 프레임부터 탐지가 준비되어 숨긴 적이 잠깐 노출되거나 공격이 먼저 허용되지 않을 것(Task 2/3).
- 관측자 비활성화·제거·Critical 전환이 마지막 공유 시야를 즉시 없앨 것(Task 1).
- 탐지 시스템 비활성화·누락으로 직접 공격이 허용되지 않을 것(Task 2).
- 적 이동 중 목표 상실 취소가 점유·완료 칸 AP·턴 진행을 깨뜨리지 않을 것(Task 2).
- 숨김 복원이 원래 꺼진 렌더러나 파괴·탈출 모델을 다시 켜지 않을 것(Task 3).

## 실행 및 검증 규칙

실행 시작 시 보호 파일 SHA-256과 변경 대상 코드의 before 사본을 Artifacts/SharedVisibilityBefore에 기록한다. 보호 대상은 카메라 스크립트, Assets/Settings, 전투 씬, 프리팹, Art/Models 및 기존 Materials다. 새 전장의 안개 셰이더와 런타임 표시용 머티리얼만 별도로 만든다.

검증용 Unity가 종료된 상태에서만 원본 Assets를 Artifacts/MeleeRangedTestProject/Assets로 동기화한다. 다음 명령은 검증 프로젝트 디렉터리에서 실행한다.

```powershell
.\tools\Run-UnityTests.ps1 -Mode EditMode -Name visibility-core -Filter 'VisibilityTests;GridTests'
.\tools\Run-UnityTests.ps1 -Mode PlayMode -Name visibility-ai -Filter 'VisibilityAITests;CombatInputTests;CombatLifecycleTests'
.\tools\Run-UnityTests.ps1 -Mode PlayMode -Name visibility-presentation -Filter 'VisibilityPresentationTests;SceneIntegrationTests;SqurdBattlePlayTests'
.\tools\Run-UnityTests.ps1 -Mode EditMode -Name visibility-final-editmode
.\tools\Run-UnityTests.ps1 -Mode PlayMode -Name visibility-final-playmode
```

각 단계의 RED/GREEN은 동일한 필터로 실행하고 이름을 visibility-taskN-red/green으로 구분한다. 컴파일 오류만으로 RED 검증을 끝내지 않는다. 최소 인터페이스를 선언한 뒤 기능 부족에 의한 단언 실패를 확인하고 구현한다.

## Task 1: 그리드 시선과 공유 탐지 계산

**Files:**
- Create: Assets/_Game/Scripts/Unit/UnitVision.cs
- Create: Assets/_Game/Scripts/Battle/VisibilitySystem.cs (Battle 폴더와 meta 포함)
- Modify: Assets/_Game/Scripts/Grid/GridCell.cs, GridManager.cs
- Modify: Assets/_Game/Scripts/Unit/BattleUnit.cs
- Test: Assets/_Game/Tests/EditMode/VisibilityTests.cs, GridTests.cs
- Test: Assets/_Game/Tests/PlayMode/VisibilityLifecycleTests.cs (자동 생명주기 갱신은 실제 PlayMode에서 검증)

**Interfaces:**
- UnitVision: `public int DetectionRange { get; }`, `public void Configure(int cells)`, `public event System.Action Changed`; SerializeField 기본값 10, 음수는 0으로 보정. 거리 설정·센서 활성 상태 변경을 탐지 시스템에 알린다.
- GridCell: `public bool BlocksVision { get; internal set; }`.
- GridManager: `public bool HasLineOfSight(Vector2Int from, Vector2Int to)`. 기존 HasClearShot과 같은 경계 순회를 private 함수로 공유하고 차단 속성만 구분한다.
- BattleUnit: `public UnitVision Vision { get; private set; }`, `public event System.Action VisibilityChanged`. Initialize에서 UnitVision이 없으면 추가한다.
- VisibilitySystem: `public void Initialize(GridManager grid, BattleUnit[] units)`, `public void Refresh()`, `public bool IsReady { get; }`, `public bool IsVisibleTo(UnitTeam team, BattleUnit target)`, `public event System.Action Changed`.

- [x] **RED 테스트 작성.** `StartupUsesInclusiveCircleAndBuildingLine`: 관측자 (1,1), 거리10에서 대상 (7,9)은 발견, (9,9)은 미발견, 중간 건물 뒤 대상은 미발견을 검증한다. 아래 단언의 각 대상을 서로 다른 독립 배치로 초기화한다.

```csharp
Assert.That(vision.IsVisibleTo(UnitTeam.Player, boundaryTarget), Is.True);
Assert.That(vision.IsVisibleTo(UnitTeam.Player, outsideTarget), Is.False);
Assert.That(vision.IsVisibleTo(UnitTeam.Player, occludedTarget), Is.False);
```

- [x] **공유·상태·지형 테스트 작성.** `SharedDetectionEndsOnlyAfterLastObserverLosesSight`는 첫 관측자 상실 후 true, 마지막 상실 후 false를 검증한다. `CriticalDisabledRemovedObserversCannotRevealTargets`, `CoverRavinesAndUnitsDoNotBlockSight`, `CornerBuildingsBlockSightLikeShots`를 추가한다. 테스트 소유 그리드와 유닛을 사용한다.
- [x] **RED 실행.** 위 EditMode 필터로 의미 있는 실패를 저장한다.
- [x] **거리·시선 구현.** 제곱 거리 비교에는 long을 사용한다. 장애물 등록 시 BlocksVision을 BlocksShots에서 초기화하고 이후 검사 속성은 독립시킨다. 유효한 그리드 등록 유닛만 관측·대상에 포함한다.
- [x] **공유 결과·갱신 알림 구현.** BattleUnit.VisibilityChanged를 위치 갱신 후, 상태 변경 후, OnEnable/OnDisable 및 제거 후에 발행한다. VisibilitySystem은 중복 등록·구독을 방지하고 무효화 시 구독을 정리한다. 비활성화 시 IsReady=false, 상대 IsVisibleTo=false, Changed 알림을 보낸다. 재활성화 시 다시 구독·계산한다.
- [x] **GREEN과 기존 EditMode 실행.** 실패0과 보호 해시를 확인한다. 이 단계에서는 게임의 초기화 흐름에 아직 연결하지 않는다. 계산 기능의 결과를 보고하고 사용자 확인을 기다린다.

## Task 2: 시작 탐지, 공격 제한과 적 AI 대기

**Files:**
- Modify: Assets/_Game/Scripts/Setup/BattleSceneController.cs
- Modify: Assets/_Game/Scripts/Turn/TurnManager.cs
- Modify: Assets/_Game/Scripts/Combat/CombatSystem.cs
- Modify: Assets/_Game/Scripts/AI/EnemyAIController.cs
- Test: Assets/_Game/Tests/PlayMode/VisibilityAITests.cs
- Modify test setup: Assets/_Game/Tests/PlayMode/EnemyAITestBattle.cs, CombatInputTests.cs, CombatLifecycleTests.cs, FactionTurnTests.cs, UnitCameraFocusTests.cs
- Modify test setup: Assets/_Game/Tests/EditMode/BodyTargetCombatTests.cs, CombatSystemTests.cs, MeleeCombatTests.cs, PartAttackTests.cs, TurnTests.cs

**Interfaces:**
- Consumes: Task 1의 VisibilitySystem, UnitVision.
- TurnManager: `public VisibilitySystem Visibility { get; private set; }`, `public void ConfigureVisibility(VisibilitySystem visibility)`.
- BattleSceneController는 기존 보완 패턴대로 VisibilitySystem을 생성/재사용한다. 유닛 배치 후 Initialize하고 TurnManager.ConfigureVisibility로 연결한다. 입력·표시 초기화 전에 첫 탐지를 계산한다. StartTeamTurn에서도 Refresh한다.
- Combat/AI는 turns.Visibility의 동일한 결과를 조회한다. 누락·미초기화·비활성 상태를 발견으로 취급하지 않는다.

- [x] **RED 테스트 작성.** `BattleStartsWithNearbyEnemyAlreadyDetected`, `UndetectedEnemyWaitsWithoutSpendingAPAndTurnReturns`, `DetectedEnemyUsesExistingAttack`, `LostTargetIsNotPursuedNextTurn`을 작성한다. 시작·범위 밖·건물 뒤·발견 후 시야 상실을 실제 전투 fixture로 검증한다.
- [x] **API 우회와 이동 취소 RED 작성.** `HiddenTargetCannotBeAttackedDirectly`는 근접·원거리 TryAttack 실패와 AP/HP 불변을 검증한다. `DisabledVisibilityDoesNotPermitAttack`, `TargetLostDuringMoveStopsAtCommittedCell`은 미완료 칸 AP 미소비·올바른 점유·정상 다음 턴 진입을 검증한다.

```csharp
Assert.That(result.WasExecuted, Is.False);
Assert.That(attacker.ActionPoints.CurrentPoints, Is.EqualTo(apBefore));
Assert.That(target.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(hpBefore));
```

- [x] **RED 실행.** VisibilityAITests PlayMode 필터로 기존 AI가 숨은 대상을 추적하거나 직접 공격을 허용하는 실패를 확인한다.
- [x] **초기화·공격 제한 연결.** CombatSystem.GetPreview에 공유 탐지 검사를 넣고 기존 Preview 경유 TryAttack에도 적용한다. 피해·명중률·부위·사거리·차폐 계산은 유지한다. GetAttackRangeCells는 지형·거리 기반 범위를 유지하고 미발견 대상의 정보를 새로 표시하지 않는다.
- [x] **적 AI 연결.** ValidTarget에 적 진영 탐지를 추가한다. 대상이 없으면 AP 소비 없이 행동을 끝낸다. 이동 중 목표 상실 시 기존 CancelCurrentAction/CancelMove로 종료한다. 다른 대상 선택은 다음 기체의 행동에서 최신 결과로 수행한다. 기억·순찰·반응 사격은 추가하지 않는다.
- [x] **기존 fixture 이전.** 위 setup에 VisibilitySystem.Initialize/ConfigureVisibility를 명시한다. 기존 공격 전제에 큰 탐지 거리가 필요하면 테스트 유닛의 UnitVision.Configure를 사용한다. 전역 기본값을 늘리거나 탐지 검사를 우회하지 않는다. 씬 기반 fixture는 실제 초기화를 사용한다.
- [x] **GREEN·전투/AI 회귀 실행.** 전체 EditMode와 AI/Combat 관련 PlayMode를 실행하고 보호 해시를 확인한다. 이 단계는 표시 연결 전이므로 API·테스트 결과를 보고하고 사용자 확인을 기다린다.

## Task 3: 모델 숨김·입력·행동 표시와 씬 통합

**Files:**
- Create: Assets/_Game/Scripts/Presentation/UnitVisibilityPresenter.cs
- Modify: Assets/_Game/Scripts/Setup/BattleSceneController.cs
- Modify: Assets/_Game/Scripts/Input/PlayerInputController.cs
- Modify: Assets/_Game/Scripts/Presentation/GridVisualizer.cs
- Test: Assets/_Game/Tests/PlayMode/VisibilityPresentationTests.cs
- Modify tests: Assets/_Game/Tests/PlayMode/SceneIntegrationTests.cs, SqurdBattlePlayTests.cs (필요한 탐지 전제만)

**Interfaces:**
- Consumes: TurnManager.Visibility, VisibilitySystem.Changed/IsVisibleTo.
- UnitVisibilityPresenter: `public void Initialize(BattleUnit unit, VisibilitySystem visibility)`. 플레이어 관점의 표시만 관리한다. 숨기기 직전 Renderer.enabled를 보관하고 복원 시 원래 상태 및 유닛 생존/활성 상태를 지킨다.
- PlayerInputController.IsTargetSelectable에 플레이어 공유 탐지를 추가한다. Changed에서 현재 목표 상실 시 target/SelectedBodyPart/LastAttackResult/InputMessage를 기존 의미대로 초기화한다.
- GridVisualizer.UpdateEnemyIndicator에 플레이어 IsVisibleTo 조건을 추가한다. 아군 선택 표시는 유지한다.

- [x] **RED 테스트 작성.** `InitiallyHiddenEnemyHasNoModelShadowSelectionOrIndicator`, `SharedDiscoveryShowsEnemyAndAllowsTargeting`, `LastSightLostClosesTargetPreview`, `HiddenUnitStillOccupiesCellAndCountsForVictory`를 작성한다. 실제 마우스 입력과 렌더러·선택·UI 상태를 검증한다.

```csharp
Assert.That(enemy.gameObject.activeSelf, Is.True);
Assert.That(enemyRenderer.enabled, Is.False);
Assert.That(input.TrySelectTarget(enemy), Is.False);
Assert.That(enemyCell.OccupyingUnit, Is.SameAs(enemy));
```

- [x] **복원·초기화·씬 RED 작성.** `RestorePreservesOriginallyDisabledRenderer`, `DestroyedOrEjectedEnemyNeverReappears`, `RepeatedInitializationDoesNotDuplicatePresenters`를 추가한다. BattleTest/1V1Test/SqurdBattleTest는 저장하지 않고 불러와 현재 배치로 시작 탐지를 평가한다. 사용자 배치·속도·줌·그림자 거리의 고정값을 테스트하지 않는다.
- [x] **RED 실행.** VisibilityPresentationTests 필터로 현재 숨은 모델이 그대로 표시되는 실패를 확인한다.
- [x] **표시 연결.** SceneController에서 presenter를 재사용/보완하고 첫 탐지 결과를 즉시 적용한다. GameObject/Collider/로직을 비활성화하지 않고 Renderer를 숨겨 투영 그림자도 제거한다. 정리·복원으로 파괴/탈출 모델을 되살리지 않는다.
- [x] **입력·적 빨간 테두리 연결.** 숨은 대상의 클릭·직접 TrySelectTarget을 거절한다. 시야 상실 시 조준만 해제하고 아군 선택·방향 확정 대기는 유지한다. 미발견 적의 행동 테두리도 숨긴다.
- [x] **GREEN·전체 회귀 실행.** 위 전체 EditMode/PlayMode, 보호 해시, 원본과 검증 프로젝트의 C# 일치를 확인한다. XML/로그를 원본 Artifacts에 보관한다.
- [x] **3단계 결과 보고.** 모델 숨김·입력 연동 결과를 보고하고 사용자 확인 후 Task 4로 진행한다. 전체 기능의 독립 최종 리뷰는 Task 4 완료 시 수행한다.

## Task 4: 지형 탐험 기록과 3단계 전장의 안개

**Files:**
- Create: Assets/_Game/Scripts/Battle/CellVisibility.cs
- Modify: Assets/_Game/Scripts/Battle/VisibilitySystem.cs
- Modify: Assets/_Game/Scripts/Grid/GridManager.cs
- Create: Assets/_Game/Scripts/Presentation/FogOfWarPresenter.cs
- Create: Assets/_Game/Resources/Shaders/FogTerrain.shader
- Modify: Assets/_Game/Scripts/Setup/BattleSceneController.cs
- Test: Assets/_Game/Tests/EditMode/FogOfWarTests.cs
- Test: Assets/_Game/Tests/PlayMode/FogOfWarPresentationTests.cs

**Interfaces:**
- `public enum CellVisibility { Unexplored, Explored, Visible }`.
- VisibilitySystem: `public CellVisibility GetCellVisibility(UnitTeam team, Vector2Int position)`를 추가한다. Refresh는 진영별 현재 시야와 탐험 기록을 함께 갱신한다. Initialize로 새 전투를 구성할 때 기록을 비우며 단순 Refresh/재활성화는 기존 기록을 유지한다.
- GridManager: `public bool HasLineOfSightToCell(Vector2Int from, Vector2Int to)`를 추가한다. 공통 직선 순회를 사용하되 최종 차폐 칸은 허용하고 중간 차폐 칸은 허용하지 않는다. 유닛용 HasLineOfSight와 사격용 HasClearShot의 판정은 유지한다.
- FogOfWarPresenter: `public void Initialize(GridManager grid, VisibilitySystem visibility, Renderer[] terrainRenderers)`; 플레이어 시야 상태 텍스처와 Inspector의 exploredBrightness/unexploredBrightness를 관리한다. 초기값은 각각 0.6/0.3으로 두며 0보다 크고 미탐험 밝기가 탐험 밝기보다 낮도록 검증한다.
- FogTerrain.shader는 URP Lit 기반 지형 전용 표시다. 칸 상태 텍스처를 표면의 월드 X/Z로 조회하고 현재 시야에는 기존 색·조명, 나머지에는 회색 명암을 적용한다. 그리드 밖 표면은 기존 표시를 유지한다. 화면 전체 후처리나 불투명 지면 덮개를 사용하지 않는다.

- [x] **탐험 상태 RED 작성.** `UnexploredVisibleExploredAndRediscoveredCells`, `NewBattleClearsExplorationButRefreshDoesNot`, `FirstBlockingBuildingCellIsVisibleButCellsBehindAreNot`을 작성한다. 아래 상태 순서를 실제 관측자 위치 변경과 Refresh로 검증한다.

```csharp
Assert.That(vision.GetCellVisibility(UnitTeam.Player, cell), Is.EqualTo(CellVisibility.Unexplored));
// 관측자 이동으로 해당 칸을 처음 발견한다.
Assert.That(vision.GetCellVisibility(UnitTeam.Player, cell), Is.EqualTo(CellVisibility.Visible));
// 관측자가 떠나 현재 시야에서 벗어난다.
Assert.That(vision.GetCellVisibility(UnitTeam.Player, cell), Is.EqualTo(CellVisibility.Explored));
```

- [x] **표시 RED 작성.** `TerrainStaysRenderedInAllThreeStates`, `FogDoesNotAffectUnitsUIOrOriginalMaterials`, `LargeSurfaceSamplesDifferentCellStates`를 작성한다. 여러 상태에 걸친 큰 지면과 건물을 만들어 단일 중심 칸 판정의 오류를 잡는다. 머티리얼 정리 시 원래 참조가 복원되고 반복 초기화로 재질/텍스처가 중복 생성되지 않는지도 검증한다.
- [x] **RED 실행.** FogOfWarTests EditMode와 FogOfWarPresentationTests PlayMode 필터를 사용한다. 렌더링 확인에는 기존 runner의 -Graphics 옵션을 사용한다.
- [x] **칸별 시야·기록 구현.** 양 진영의 관측자로 칸 시야를 합산하고 보인 칸만 탐험됨으로 기록한다. 건물은 앞면 칸까지만 보인다. 지형 기록을 적 목표 후보나 마지막 위치 기억으로 사용하지 않는다. 기존 40×40 맵은 상태 텍스처 한 장으로 처리한다.
- [x] **지형 표시 연결.** SceneController에서 Ground와 등록된 장애물·골짜기·엄폐물의 Renderer만 수집하고 유닛/선택/범위/디버그 표시를 제외한다. 원본 머티리얼을 보존한 런타임 복사본에 지형 셰이더를 적용하고 기본 텍스처·색과 관련 URP Lit 속성을 유지한다. 비활성화/종료 때 참조를 복원하고 소유한 자원을 해제한다. 시작 프레임에 첫 시야 텍스처를 적용한다.
- [x] **GREEN·화면 검증.** 분리된 그래픽 테스트에서 세 상태, 큰 지면/건물 옆면의 경계, 미탐험 골짜기/엄폐물 형태, 아군 정상 색과 적 완전 숨김을 이미지로 저장해 확인한다. 카메라 회전·줌 후에도 지형에 붙어 있는 월드 좌표 표시인지 검증한다. 원본 씬·머티리얼·카메라·그림자 설정을 변경하지 않는다.
- [x] **전체 회귀와 보호 검사.** 전체 EditMode/PlayMode 및 원본/검증 프로젝트 C# 일치를 확인하고 XML/로그/화면 증거를 Artifacts에 보관한다. 보존 대상의 해시를 다시 확인한다.
- [x] **Native 최종 독립 리뷰.** requesting-code-review에 따라 별도 리뷰 담당에 전체 변경·승인 설계·검증 결과를 전달한다. 중요 지적을 수정하고 관련 검증을 다시 실행한다. Git 초기화/커밋은 하지 않는다. 결과를 보고하고 사용자 확인 전 다른 기능에 착수하지 않는다.

## 완료 조건

승인 설계의 검증 범위가 Task 1~4에 모두 연결되고, 전체 테스트 실패0, 세 단계 지형 표시의 화면 확인, 미해결 중요 리뷰 사항 없음, 보호 대상 변경 없음이어야 한다. 결과는 Artifacts/shared-visibility-progress.md에 기록한다. 숨은 점유를 이동 범위에서 간접 추측할 수 있는 기존 한계는 유지한다.

