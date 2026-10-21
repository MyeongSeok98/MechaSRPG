# HP·AP·공격과 더미 적 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** AP 5로 칸별 이동과 확률 공격을 조합하고 HP 20의 더미 적을 처치할 수 있는 테스트 전투를 만든다.

**Architecture:** HP, AP, 유닛 공격 설정, 공격 판정, 턴 진행을 작은 컴포넌트로 분리한다. 기존 그리드·BFS·이동·회피·엄폐를 유지하고 AP와 생존 상태를 연결한다. 미리보기와 공격은 동일한 판정 함수를 사용하며 화면은 계산 결과를 표시한다.

**Tech Stack:** Unity 6000.5.5f1, C#, URP, 기존 Input System·Unity Test Framework, IMGUI, PowerShell 테스트 실행기.

**Spec:** `docs/superpowers/specs/2026-10-05-basic-combat-design.md`

## Global Constraints

- 플레이어·더미 HP 20, 플레이어 최대 AP 5, 실제 도착 한 칸당 AP 1, 공격 AP 3.
- 공격력 5, 기본 명중률 80%, 그리드 좌표 기준 직선 사거리 5칸. 경계는 포함한다.
- 명중률은 기본 명중률에서 대상 이동 회피와 대상의 공격자 방향 엄폐 보너스를 빼고 0~100으로 제한한다.
- 대각선 엄폐 방향은 절댓값이 큰 축을 선택하고 동률이면 Z축을 우선한다.
- 더미는 행동하지 않는다. 턴 종료 시 다음 플레이어 턴으로 AP와 이동 회피를 초기화한다.
- 이동 중 전투 명령과 턴 종료를 차단한다. 사망 시 점유를 즉시 해제하고 전투를 종료한다.
- 시야 차단·적 AI·적 공격·애니메이션·탄약·재시작 버튼·험지 비용은 추가하지 않는다.
- 외부 에셋이나 패키지를 추가하지 않는다. 기존 SampleScene과 Build Settings를 보존한다.
- 기존 `Assets/_Game` 폴더와 `MechaXCOM` 네임스페이스를 유지한다. 가이드는 한국어로 작성한다.
- Git 저장소가 없으므로 커밋하지 않는다. 단계별 검증 기록을 `Artifacts/progress.md`에 남긴다.

## Review Focus

1. 두 번째 이동이 남은 AP를 무시하지 않아야 한다: 작업 2의 반복 이동 테스트로 확인한다.
2. 한 프레임 내 중복 공격·사망 요청이 AP·피해·사망을 중복 처리하지 않아야 한다: 작업 1·4의 테스트로 확인한다.
3. Collider가 비활성화된 대상이나 이동 컴포넌트가 비활성화된 선택 유닛은 유효한 행동으로 표시되지 않아야 한다: 작업 5의 테스트로 확인한다.
4. 생성기가 기존 Prefab을 재사용해도 새 전투 컴포넌트·설정이 누락되지 않아야 한다: 작업 6의 테스트로 확인한다.
5. 작은 Game View의 패널·버튼 클릭이 이동이나 적 선택으로 전달되지 않아야 한다: 작업 5·6의 테스트와 가능한 직접 화면 확인으로 검증한다.

## 파일 구성과 공통 검증

새 런타임 파일은 `Assets/_Game/Scripts/Unit/{UnitTeam,UnitHealth,UnitActionPoints,UnitCombat}.cs`,
`Assets/_Game/Scripts/Combat/{AttackPreview,AttackResult,CombatMath,CombatSystem}.cs`,
`Assets/_Game/Scripts/Turn/TurnManager.cs`이다. 기존 Runtime asmdef에서 빌드하고 새 패키지 참조는 필요 없다.
새 폴더·파일은 Unity가 `.meta`를 생성하도록 하며 전달 전에 누락을 검사한다.

모든 작업은 실패 테스트 작성 → 실패 확인 → 최소 구현 → 통과 확인 순서로 진행한다.
타입이 아직 없어서 발생하는 컴파일 오류도 의도한 RED 증거로 기록할 수 있다.
Unity 실행은 같은 프로젝트에 대해 직렬로 수행한다. Editor가 프로젝트를 열고 있으면
실행 충돌을 해결한 뒤 검증하며, 기존 테스트 결과를 새 실행 결과로 보고하지 않는다.

```powershell
.\tools\Run-UnityTests.ps1 -Mode EditMode -Name combat-taskN-red -Filter MechaXCOM.Tests.TestClass
.\tools\Run-UnityTests.ps1 -Mode EditMode -Name combat-taskN-green -Filter MechaXCOM.Tests.TestClass
# Coroutine·입력·저장 Scene 테스트는 -Mode PlayMode 사용
```

성공은 새 로그의 컴파일 오류 없음, 새 XML의 `result=Passed`, `failed=0`이다.
각 작업의 변경 파일과 실제 RED/GREEN 결과를 기록한다.

## 작업 1: HP·AP·공격 설정과 유닛 연결

**Files**
- Create: `Assets/_Game/Scripts/Unit/UnitTeam.cs`, `UnitHealth.cs`, `UnitActionPoints.cs`, `UnitCombat.cs`
- Modify: `Assets/_Game/Scripts/Unit/BattleUnit.cs`
- Create test: `Assets/_Game/Tests/EditMode/UnitStatsTests.cs`
- Modify test: `Assets/_Game/Tests/EditMode/InitializationTests.cs`

**Interfaces**
- `UnitTeam`: `Player`, `Enemy`.
- `UnitHealth`: `int MaxHealth`, `int CurrentHealth`, `bool IsAlive`, `event Action Died`; `void Configure(int maxHealth)`, `void Initialize()`, `bool ApplyDamage(int damage)`. 반환값은 살아 있는 대상에게 양수 피해를 적용했는지 나타낸다.
- `UnitActionPoints`: `int MaxPoints`, `int CurrentPoints`; `void Configure(int maxPoints)`, `void ResetForTurn()`, `bool CanSpend(int cost)`, `bool TrySpend(int cost)`.
- `UnitCombat`: `int Damage`, `int BaseHitChance`, `int Range`, `int AttackCost`; `void Configure(int damage, int baseHitChance, int range, int attackCost)`.
- `BattleUnit`: 기존 API를 유지하고 `UnitTeam Team`, `UnitHealth Health`, `UnitActionPoints ActionPoints`, `UnitCombat Combat`, `bool IsAlive`, `int AvailableMovementRange`를 제공한다. `void ConfigureTeam(UnitTeam team)`을 추가한다.
- 기존 `BattleUnit.Initialize(GridManager grid)`에서 새 필수 컴포넌트를 확보하고 HP·AP를 초기화한다. 반복 호출은 상태를 재설정하지 않는다.

- [x] 실패 테스트 `HealthInitializesAndDiesOnce`, `InvalidDamageDoesNotHeal`, `ActionPointsRejectOverspend`, `UnitInitializeDoesNotRefillTwice`를 작성한다. 각각 HP 20→15→0·Died 1회, 음수·0 피해 무효, AP 5→2·추가 비용 3 거절, 초기화 재호출 후 HP 15·AP 2 유지와 `AvailableMovementRange=2`를 검증한다.
- [x] `UnitStatsTests`와 `InitializationTests`를 실행하여 의도한 실패를 확인한다.
- [x] 위 API와 Inspector 검증을 구현한다. 새로 추가한 BattleUnit에는 필요한 컴포넌트를 자동 부착하고 기존 저장 자산은 작업 6에서 보완한다.
- [x] 같은 테스트를 실행하여 통과를 확인하고 기록한다.

## 작업 2: 남은 AP 기반 BFS와 실제 도착 비용

**Files**
- Modify: `Assets/_Game/Scripts/Grid/Pathfinder.cs`, `Assets/_Game/Scripts/Unit/UnitMovement.cs`
- Modify test: `Assets/_Game/Tests/EditMode/PathfinderTests.cs`, `Assets/_Game/Tests/PlayMode/MovementTests.cs`

**Interfaces**
- 소비: 작업 1의 `AvailableMovementRange`, `IsAlive`, `ActionPoints.CanSpend(int)`, `TrySpend(int)`.
- 유지: `Pathfinder.GetReachableCells(GridManager, BattleUnit)`, `FindPath(GridManager, BattleUnit, Vector2Int)`, `UnitMovement.TryMove(Vector2Int)`.
- `BattleUnit.AvailableMovementRange`는 생존 시 `min(MovementRange, ActionPoints.CurrentPoints)`, 사망 시 0이다.

- [x] 실패 테스트 `RangeUsesRemainingActionPoints`, `TwoMovesShareOneBudget`, `BlockedMoveChargesOnlyArrivedCells`, `DisabledMoveDoesNotChargePartialCell`를 추가한다. AP 2의 범위에 거리 2는 포함·3은 제외, 총 2+3칸 이동 후 AP 0·추가 이동 거절, 첫 칸 도착 뒤 차단 시 AP 4, 첫 도착 전 비활성화 시 AP 5를 검증한다.
- [x] Pathfinder는 EditMode, Movement는 PlayMode로 실행하여 실패를 확인한다.
- [x] BFS 깊이 제한을 `AvailableMovementRange`로 변경한다. 이동 요청·다음 셀 진입 시 생존·AP를 확인하고 점유 이전 성공 후 AP 1과 회피를 갱신한다. 완료하지 않은 셀에는 비용을 부과하지 않는다.
- [x] 기존 이동·경로 테스트와 새 테스트를 실행하여 통과를 확인하고 기록한다. 반복 이동 테스트에 임의 AP 회복을 삽입하여 규칙 위반을 숨기지 않는다.

## 작업 3: 직선 사거리·방향·확률 계산

**Files**
- Create: `Assets/_Game/Scripts/Combat/CombatMath.cs`, `AttackPreview.cs`, `AttackResult.cs`
- Create test: `Assets/_Game/Tests/EditMode/CombatMathTests.cs`

**Interfaces**
- `CombatMath`: `static bool IsInRange(Vector2Int attacker, Vector2Int target, int range)`, `static CoverDirection GetIncomingDirection(Vector2Int attacker, Vector2Int target)`, `static int CalculateHitChance(int baseChance, int evasion, int coverBonus)`, `static bool IsHit(int chance, int roll)`.
- `AttackPreview`는 값 데이터: `bool CanAttack`, `string FailureReason`, `float Distance`, `CoverDirection Direction`, `CoverType Cover`, `int CoverBonus`, `int Evasion`, `int HitChance`, `int Damage`, `int APCost`.
- `AttackResult`는 값 데이터: `bool WasExecuted`, `string FailureReason`, `bool Hit`, `int HitChance`, `int DamageDealt`, `int TargetHealthAfter`, `bool TargetKilled`. 제거된 대상 참조에 의존하지 않는다.

- [x] `StraightRangeIncludesBoundary`, `DiagonalDirectionPrefersZOnTie`, `HitChanceIncludesBothBonuses`, `RollBoundaryIsExclusive` 실패 테스트를 작성한다. 아래 기대값을 포함한다.

```csharp
Assert.That(CombatMath.IsInRange(Vector2Int.zero, new Vector2Int(3, 3), 5), Is.True);
Assert.That(CombatMath.IsInRange(Vector2Int.zero, new Vector2Int(3, 4), 5), Is.True);
Assert.That(CombatMath.IsInRange(Vector2Int.zero, new Vector2Int(4, 4), 5), Is.False);
Assert.That(CombatMath.GetIncomingDirection(new Vector2Int(1, 1), new Vector2Int(3, 3)), Is.EqualTo(CoverDirection.South));
Assert.That(CombatMath.CalculateHitChance(80, 10, 15), Is.EqualTo(55));
Assert.That(CombatMath.IsHit(55, 54), Is.True);
Assert.That(CombatMath.IsHit(55, 55), Is.False);
```

- [x] `CombatMathTests`를 실행하여 실패를 확인한다.
- [x] 순수 함수와 값 데이터를 구현한다. 사거리는 좌표 차이의 제곱합으로 비교하고 명중 판정의 정수 난수 범위는 0~99로 한다. 동일 좌표 요청은 CombatSystem에서 거절한다.
- [x] 네 방향, 축 크기 비교, 사거리 (5,0)/(6,0), 명중률 0/100 테스트를 추가하고 같은 클래스의 모든 테스트가 통과하는지 확인한다.

## 작업 4: 턴·공격 실행·사망 연결

**Files**
- Create: `Assets/_Game/Scripts/Turn/TurnManager.cs`, `Assets/_Game/Scripts/Combat/CombatSystem.cs`
- Modify: `Assets/_Game/Scripts/Setup/BattleSceneController.cs`, `Assets/_Game/Scripts/Unit/BattleUnit.cs`, `Assets/_Game/Scripts/Unit/UnitMovement.cs`
- Create test: `Assets/_Game/Tests/EditMode/CombatSystemTests.cs`, `TurnTests.cs`
- Create test: `Assets/_Game/Tests/PlayMode/CombatLifecycleTests.cs`

**Interfaces**
- `TurnManager`: `int TurnNumber`, `bool IsBattleOver`, `bool IsPlayerVictory`, `bool IsAnyUnitMoving`; `void Initialize(BattleUnit[] units)`, `bool CanAct(BattleUnit unit)`, `bool TryEndTurn()`, `void CheckBattleEnd()`.
- `CombatSystem`: `void Initialize(GridManager grid, CoverSystem cover, TurnManager turns)`, `AttackPreview GetPreview(BattleUnit attacker, BattleUnit target)`, `AttackResult TryAttack(BattleUnit attacker, BattleUnit target)`, `void SetRollSource(Func<int> rollSource)`. 실제 플레이는 Unity 정수 난수를 사용하고 테스트는 고정값과 호출 횟수 기록을 사용한다.
- `BattleSceneController`: `void ConfigureCombat(CombatSystem combat, TurnManager turns)`를 추가한다. 유닛 초기화·사망 구독과 시스템 연결 후 턴을 시작한다.
- `BattleUnit`: `void BindTurnManager(TurnManager turns)`로 이동 진입에도 `CanAct`를 연결한다. 단독 이동 테스트에서는 미연결 상태로 생존·AP 검증을 수행한다.

- [x] `HitAndMissBothCostThree`, `InvalidAttackDoesNotRollOrSpend`, `EndTurnRefillsAPAndResetsEvasion`, `DeadTargetCannotBeHitTwice`를 작성한다. 고정 roll 0에서 HP 20→15·AP 5→2, roll 99에서 HP 20·AP 2, 재공격 AP 부족·난수 호출 없음, 턴 1→2·AP 5·회피 0·HP/위치 유지, 죽은 대상 재요청 거절을 검증한다.
- [x] EditMode 실패를 확인한다. PlayMode에는 `DeathReleasesOccupancyImmediately`, `MovingBlocksAttackAndEndTurn`, `VictoryLocksMovementAndTurns` 실패 테스트를 작성·실행한다.
- [x] 미리보기에서 팀·생존·그리드 등록·같은 좌표·사거리·턴·이동·AP를 검증한다. 공격은 미리보기를 다시 계산하고 유효할 때만 AP 소모→난수→피해 순서로 실행한다.
- [x] 턴 초기화·종료와 사망 알림을 연결한다. 사망 시 점유 해제→Collider/표시 비활성화→종료 상태 갱신→GameObject 제거 순서로 처리하고, 파괴 시 구독을 해제한다.
- [x] 재초기화·중복 사망·같은 프레임 재공격·대상 파괴·이동 비활성화의 테스트를 포함하여 해당 EditMode·PlayMode 테스트를 통과시키고 기록한다.

## 작업 5: 적 대상 지정·공격 미리보기·버튼

**Files**
- Modify: `Assets/_Game/Scripts/Input/PlayerInputController.cs`, `Assets/_Game/Scripts/Presentation/BattleDebugUI.cs`, `Assets/_Game/Scripts/Setup/BattleSceneController.cs`
- Modify test: `Assets/_Game/Tests/PlayMode/InputPresentationTests.cs`
- Create test: `Assets/_Game/Tests/PlayMode/CombatInputTests.cs`

**Interfaces**
- `PlayerInputController`: 기존 `Initialize`를 유지하고 `void InitializeCombat(CombatSystem combat, TurnManager turns)`, `BattleUnit TargetUnit`, `AttackPreview CurrentAttackPreview`, `AttackResult LastAttackResult`, `bool TrySelectTarget(BattleUnit target)`, `AttackResult TryAttackSelected()`, `bool TryEndTurn()`를 추가한다.
- `BattleDebugUI`: 기존 초기화·선택·클릭 차단 API를 유지하고 `void InitializeCombat(PlayerInputController input, TurnManager turns)`로 표시·버튼 요청을 연결한다.
- 소비: 작업 4의 GetPreview/TryAttack/TryEndTurn. UI 내부에서 명중률·AP 규칙을 재계산하지 않는다.

- [x] 실패 테스트 `EnemyClickKeepsPlayerSelection`, `AttackPreviewMatchesExecution`, `DisabledUnitClearsRangeAndTarget`, `UIRectBlocksMapInput`를 만든다. 적 클릭 후 SelectedUnit=플레이어·TargetUnit=적, 초기 배치 예상 65%, 비활성화된 유닛의 명령 거절과 표시 제거, 640×480 UI 좌표의 클릭 차단을 검증한다.
- [x] PlayMode 실패를 확인한다. 기존 모의 Mouse 입력 방식을 재사용하며 변경한 InputSystem 설정을 teardown에서 복원한다.
- [x] 플레이어 선택·빈 칸 이동·적 대상 지정을 분기한다. 선택되지 않았을 때 적 클릭은 조작권을 주지 않고 안내한다. 이동·선택 변경·턴 변경·사망 시 대상 해제, 공격 후 미리보기·범위 갱신을 구현한다.
- [x] 패널에 턴·HP·AP·대상 HP/거리/방향/명중률·거절 이유·결과와 공격/턴 종료 버튼을 추가한다. 모든 버튼은 담당 API의 조건으로 활성화하고 종료 상태를 표시한다. 패널이 커지면 DisplayRect와 클릭 차단도 함께 갱신한다.
- [x] 이동 중·AP 부족·대상 Collider 비활성화·전투 종료·UI 클릭 테스트를 통과시키고 기록한다. Collider 비활성화 대상은 화면 대상 지정에서 제외하되 HP 규칙은 렌더러에 의존하지 않는다.

## 작업 6: 저장 Scene·Prefab·회귀 검증과 가이드

**Files**
- Modify: `Assets/_Game/Editor/BattleTestSceneBuilder.cs`, `Assets/_Game/Scenes/BattleTest.unity`, `Assets/_Game/Prefabs/BattleUnit.prefab`
- Create: `Assets/_Game/Prefabs/DummyEnemy.prefab`, `Assets/_Game/Materials/DummyEnemy.mat`
- Modify test: `Assets/_Game/Tests/EditMode/SceneBuilderTests.cs`, `Assets/_Game/Tests/PlayMode/SceneIntegrationTests.cs`
- Modify: `docs/battle-skeleton-guide.ko.md`, `Artifacts/progress.md`

**Interfaces**
- 유지: `BattleTestSceneBuilder.BuildSceneAtPath(string scenePath, string assetFolder)`와 기존 확인·저장 보호.
- 추가: `static void UpgradeDefaultSceneBatch()` — 기존 BattleTest를 열어 필요한 컴포넌트·더미·연결을 보완하고 저장한다. 기존 Scene GUID, 배치, 카메라, 플레이어 이동 속도와 관련 없는 사용자 설정을 보존하며 반복 호출해도 더미가 늘어나지 않는다.
- 생성·보완 결과: 플레이어 (1,1), Enemy 더미 (3,3), 각 HP 20, 플레이어 AP 5·공격 설정 5/80/5/3. 기존 엄폐·장애물은 유지한다.

- [x] `BuilderCreatesCombatScene`, `ExistingPrefabGetsCombatComponents`, `UpgradeIsIdempotent` 실패 테스트를 작성한다. 생성 Scene의 두 유닛·팀·설정·전투 참조, 구형 Prefab 재사용 보완, 두 번 업그레이드 후 적 1체와 같은 Scene GUID를 검증한다. 테스트는 별도 임시 자산을 사용한다.
- [x] EditMode에서 실패를 확인한다.
- [x] 생성기의 신규/기존 Prefab 처리를 보완하고 더미·CombatSystem·TurnManager·참조를 생성한다. 기본 저장 Scene은 별도 업그레이드로 갱신하며 기존 생성 메뉴의 덮어쓰기 보호를 우회하지 않는다.
- [x] 다음 명령으로 기본 Scene을 갱신하고 생성기 테스트를 통과시킨다.

```powershell
.\tools\Run-UnityTests.ps1 -Name combat-scene-upgrade -ExecuteMethod MechaXCOM.Editor.BattleTestSceneBuilder.UpgradeDefaultSceneBatch
```

- [x] 기존 SceneIntegrationTests를 AP·턴·새 더미 배치에 맞게 수정한다. (3,3)은 이제 점유 셀이므로 기존 이동 목적지를 빈 셀로 바꾸고, 별도 적 클릭 테스트로 65% 미리보기를 검증한다. 테스트를 삭제하여 기존 장애물 클릭·점유·회피 검증을 잃지 않는다.
- [x] 저장 Scene 통합 테스트 `MoveTwoThenAttack`, `AttackThenMoveTwo`, `KillDummyEndsBattle`, `NextTurnRestoresBudget`를 작성·실행한다. 고정 난수로 피해와 사망을 재현하며 남은 AP·점유·입력 차단을 검증한다.
- [x] 전체 EditMode·PlayMode를 아래 명령으로 실행하고 새 XML·로그를 확인한다.

```powershell
.\tools\Run-UnityTests.ps1 -Mode EditMode -Name combat-final-editmode
.\tools\Run-UnityTests.ps1 -Mode PlayMode -Name combat-final-playmode -Graphics
```

- [x] 가이드의 자유 이동 설명을 새 규칙으로 갱신하고 컴포넌트 설정·직선 사거리·엄폐·버튼·시야 차단 미구현을 설명한다. 가능한 화면 확인을 수행하며 실제 OS 입력·IMGUI 검사가 불가능하면 그 한계를 명시한다.
- [x] 최종 독립 코드 검토에서 Review Focus와 명세 충족 여부를 확인한다. 중요 결함은 재현 테스트→수정→관련 검증 순서로 해결하고 변경 후 필요한 전체 검증을 수행한다.

## 자체 검토 및 실행 방식

명세의 초기 수치, 직선 사거리 경계, AP·턴·회피, 거절 시 무변경,
사망과 점유, 더미 배치, UI·저장 자산·기존 테스트·가이드 요구사항을 작업 1~6에 대응시켰다.
인터페이스와 테스트 연결, Review Focus의 소유 작업을 점검했다.

사용자 계획 검토 후 실행한다. 권장 방식은 이 세션에서 직접 순차 구현하고 마지막에
독립 검토를 받는 방식이다. AP→이동→공격·턴→입력·Scene 순서의 의존성이 강하고
동일 Unity 프로젝트의 실행을 직렬로 유지해야 하기 때문이다.
작업마다 구현·검토 에이전트를 따로 사용하는 방식도 선택할 수 있으나 더 많은 문맥 비용이 든다.

