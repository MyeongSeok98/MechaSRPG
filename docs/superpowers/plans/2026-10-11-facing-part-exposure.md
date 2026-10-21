# 이동 후 방향 선택과 부위 노출 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 이동을 마친 아군이 AP 소모 없이 4방향 중 하나를 확정하고, 대상 기체 기준 8방향의 부위 노출 배율을 원거리 명중률에 적용한다.

**Architecture:** BattleUnit이 확정 방향과 방향 확정 상태를 보관하고 UnitMovement가 이동 및 Visual 회전을 수행한다. TurnManager가 전체 행동 잠금을 판단하며 PlayerInputController와 UnitActionUI가 선택·확정 흐름을 제공한다. 순수 함수 FacingMath가 상대 방향과 부위 노출 배율을 계산하고 CombatSystem이 기존 명중률에 연결한다.

**Tech Stack:** Unity 6000.5.5f1, C#, URP, Input System, Unity Test Framework/NUnit. 외부 패키지 추가 없음.

**Spec:** [승인된 설계](../specs/2026-10-09-facing-part-exposure-design.md)

## Global Constraints

- 기체의 로컬 +Z를 정면으로 사용한다. 이동 후 선택은 북(+Z)·동(+X)·남(-Z)·서(-X), 피격 상대 방향은 8방향이다.
- 정면/후면: 양쪽 팔다리 ×1. 왼쪽: 왼쪽 ×1.2, 오른쪽 ×0.7. 오른쪽은 반대다.
- 왼쪽 앞/뒤: 왼쪽 ×1.1, 오른쪽 ×0.85. 오른쪽 앞/뒤는 반대다.
- 몸통·머리·콕피트와 근접 공격의 노출 배율은 ×1이다.
- 이동 완료 → 방향 선택 → 확정 회전 → 행동 재개. 방향 선택 및 회전에는 AP가 들지 않는다. AP 0에서도 가능하다.
- 선택 대기 및 확정 회전 중 이동·공격·탈출·다른 유닛 선택·턴 종료를 막는다. UI와 직접 API 호출 모두 검사한다.
- 방향 선택 중 ESC는 마지막 이동 방향을 그대로 확정하고 선택을 해제한다. 확정 회전 중 ESC는 무시한다.
- 이동 중 선택 해제 후에도 정상 도착하면 해당 유닛의 방향 선택 UI를 표시한다.
- 적은 마지막 이동 방향을 자동 확정한다. 이동하지 않으면 방향을 유지하며 최적 방향 선택 AI를 추가하지 않는다.
- 방향은 턴이 바뀌어도 유지한다. 공격 시 자동으로 대상을 바라보게 하지 않는다.
- 명중률은 `(기본 − 회피)의 0 이상 값 × 머리 손상 × 부위 × 노출 × 엄폐`, 최종 단계에서 0~100 제한과 반올림을 적용한다.
- 기존 부위 배율은 몸통 1 / 머리 0.8 / 팔·다리 0.9 / 콕피트 0.45, 엄폐 배율은 None 1 / Half 0.75 / Full 0.5다.
- 엄폐는 월드 방향으로 판정한다. 기체 방향 변경으로 엄폐·건물 사선·피해·사거리·공격 AP가 바뀌지 않는다.
- 이동속도 12f와 해당 기본값을 보존한다. 회전 속도 조절은 현재 UnitMovement의 Rotation Speed를 그대로 사용한다.
- 씬·모델·무기·메카 프리팹을 재생성하거나 저장하지 않는다. 맵 크기·배치·카메라 설정·라이플 손잡이·위치·크기를 수정하거나 고정 좌표로 검증하지 않는다.
- 검증은 기존 `Artifacts/MeleeRangedTestProject`에서 Unity를 순차 실행한다. 원본 편집기와 테스트 프로젝트를 분리한다.
- Git 초기화·worktree·커밋을 하지 않는다. 각 단계의 변경과 RED/GREEN, 보존 결과를 `Artifacts/facing-part-exposure-progress.md`에 기록한다.
- 기존 Native 방식을 유지한다. executing-plans로 직접 구현하고 각 Task의 관련 검증 완료 후 사용자에게 결과를 보고한 뒤 다음 단계 확인을 받는다. 최종 단계에서 별도 코드 리뷰를 수행한다.

## Review Focus

1. 이동 중 ESC로 선택을 해제해도 도착 유닛의 방향 선택이 열리고 잠금이 해제 가능해야 한다. Task 3.
2. AP 0, Visual 없음, 같은 방향 확정도 회전을 완료하고 잠금을 해제해야 한다. Task 2·3.
3. 회전 중 Critical·파괴·비활성화·전투 종료로 코루틴과 논리 방향이 어긋나거나 잠금이 남지 않아야 한다. Task 2·3.
4. UI를 거치지 않는 이동·사격·탈출·턴 종료 호출도 방향 확정을 우회하지 못해야 한다. Task 3.
5. 기체의 상대 좌우와 맵의 엄폐 방향을 혼동하지 않으며, 오래된 씬과 모델 없는 적도 같은 규칙을 사용해야 한다. Task 1·4·5.

## 파일 구조와 책임

경로는 프로젝트 루트 기준이다. 런타임 namespace는 `MechaXCOM`, 테스트는 `MechaXCOM.Tests`다.
기존 전투 부위 enum과 엄폐 시스템을 재사용한다. 방향을 위해 새 MonoBehaviour를 필수로 추가하지 않는다.

| 파일 | 변경과 책임 |
|---|---|
| 새 `Assets/_Game/Scripts/Unit/FacingTypes.cs` | 4방향, 8방향, 방향 확정 상태 enum |
| 새 `Assets/_Game/Scripts/Combat/FacingMath.cs` | 초기 방향 정규화, 상대 방향, 부위 노출 배율 |
| `Assets/_Game/Scripts/Unit/BattleUnit.cs` | 확정 방향과 대기/회전 상태, 생명주기 정리 |
| `Assets/_Game/Scripts/Unit/UnitMovement.cs` | 이동 완료 시 방향 기록, 확정 회전, 취소 복원 |
| `Assets/_Game/Scripts/Turn/TurnManager.cs` | 대기 유닛 조회, 전체 행동 잠금과 전투 종료 정리 |
| `Assets/_Game/Scripts/Input/PlayerInputController.cs` | Facing 모드, 선택 후보, 확정·ESC 입력 |
| `Assets/_Game/Scripts/Presentation/UnitActionUI.cs` | 기존 행동 패널을 대체하는 4방향·확정 버튼 |
| `Assets/_Game/Scripts/Combat/CombatMath.cs`, `CombatSystem.cs`, `AttackPreview.cs` | 노출 배율 적용과 미리보기 데이터 |
| `Assets/_Game/Scripts/Presentation/BattleDebugUI.cs` | 기체 방향, 상대 공격 방향, 노출 배율 표시와 End Turn 비활성화 |
| `Assets/_Game/Scripts/AI/EnemyAIController.cs` | 기존 이동 후 자동 확정 동작 검증; 필요한 경우에만 접점 수정 |
| 기존 `BodyTargetUI.cs` | CurrentAttackPreview를 재사용하므로 별도 명중률 계산을 만들지 않는다 |

신규 테스트: EditMode `FacingMathTests.cs`, PlayMode `FacingSelectionTests.cs`, `FacingSelectionInputTests.cs`,
`FacingExposureCombatTests.cs`, `FacingSavedSceneTests.cs`.
기존 MovementTests·MovementFacingTests·UnitActionMenuTests·SqurdBattlePlayTests 및 이동 후 다음 행동을
수행하는 기존 테스트에는 새 방향 확정을 명시적으로 수행하는 단계를 추가한다.

## 검증 실행 규약

각 Task 시작 시 변경 전 파일과 보호 자산의 SHA-256을 기록한다. 보호 대상은 씬·프리팹·모델·무기
및 관련 meta다. UnitMovement는 허용된 코드 변경 대상이지만 movementSpeed 필드와 기본값은 보존한다.
새 코드와 테스트의 `.meta`도 함께 보관하고 기존 GUID는 변경하지 않는다.

Unity가 실행 중이지 않을 때 원본 Assets를 검증 프로젝트에 동기화한다.
신규 스크립트의 meta가 검증 프로젝트에서 생성되었다면 원본에 이미 생성된 meta와 충돌하지 않게
일치시킨 뒤 최종 동기화한다. 테스트용 씬 생성기를 사용자가 작성한 씬에 실행하지 않는다.

```powershell
# 원본 프로젝트 루트, 검증 Unity가 종료된 상태에서 실행
Copy-Item Assets/* Artifacts/MeleeRangedTestProject/Assets -Recurse -Force
```

아래 Run-UnityTests 명령은 모두 `Artifacts/MeleeRangedTestProject`를 작업 디렉터리로 실행한다.
실행은 하나씩 진행하고 완료·종료코드·XML 결과를 확인한 뒤 다음 실행을 시작한다.
RED는 의도한 기능 부재로 실패해야 하며, 컴파일 오류면 이름과 참조를 확인하고 최소 선언을
추가해 행동 단언이 실패하는 상태를 확인한다. GREEN은 종료코드 0, XML failed=0이어야 한다.

---

### Task 1: 방향과 부위 노출 계산

**Files:** Create FacingTypes.cs, FacingMath.cs, `Assets/_Game/Tests/EditMode/FacingMathTests.cs`.

**Interfaces:**
- `FacingDirection { North=0, East=1, South=2, West=3 }`.
- `RelativeAttackDirection { Front, FrontRight, Right, RearRight, Rear, RearLeft, Left, FrontLeft }`는 시계 방향 순서다.
- `FacingSelectionState { None, AwaitingConfirmation, Turning }`.
- `FacingMath.FromForward(Vector3 forward) -> FacingDirection`: XZ를 사용하고 45도 경계는 시계 방향. 수평 성분이 0이면 North.
- `FacingMath.Forward(FacingDirection facing) -> Vector3`.
- `FacingMath.GetRelativeDirection(CoverDirection incoming, FacingDirection facing) -> RelativeAttackDirection`.
- `FacingMath.GetExposureMultiplier(TargetBodyPart part, RelativeAttackDirection incoming) -> float`.
- 순수 함수의 잘못된 방향 enum은 ArgumentOutOfRangeException으로 거부한다. Whole은 기존처럼 Torso로 정규화한다.

- [ ] **Step 1: 계산 테스트 작성.** `EveryFacingAndIncomingDirectionUsesExpectedExposure`에서 4×8×7 조합을
  독립적인 기대 배율 표와 비교한다. `EastFacingSeesNorthAsLeft`는 North/East 조합을 Left로 단언한다.
  `InitialFacingNormalizesToCardinals`는 +Z/+X/-Z/-X 및 정확한 대각선의 시계 방향 정규화를 검사한다.
  `IncomingSectorBoundaryUsesExistingWorldRule`는 공격자 좌표 (414,1000)/(415,1000)이 각각 North/NorthEast이며
  같은 월드 방향을 기체 방향별 상대 구역으로 변환하는지 확인한다. 8구역의 나머지 경계도 회전된 좌표로 검사한다.
  `InvalidDirectionIsRejected`는 잘못된 enum을 검사한다.
- [ ] **Step 2: RED 확인.** `./tools/Run-UnityTests.ps1 -Mode EditMode -Name facing-1-red -Filter FacingMathTests`.
- [ ] **Step 3: 순수 함수 구현.** CoverDirections.Clockwise의 각도 순서를 사용한다.
  상대 구역 인덱스는 `(월드 구역 인덱스 - (int)facing * 2 + 8) % 8`이다.
  CoverDirection enum 자체의 정수 값을 각도로 사용하지 않는다. 배율은 Global Constraints 표 그대로 반환한다.
- [ ] **Step 4: GREEN 및 기존 방향 회귀 확인.** 같은 필터로 `facing-1-green`, 이어서
  `-Mode EditMode -Name facing-1-regression -Filter 'FacingMathTests;CombatMathTests'` 실행. 모두 PASS.
- [ ] **Step 5: 결과 기록과 단계 검토.** 변경 파일, RED/GREEN XML을 기록하고 사용자 확인을 받는다.

대표 계산 테스트의 단언:

```csharp
Assert.That(FacingMath.GetRelativeDirection(CoverDirection.North, FacingDirection.East),
    Is.EqualTo(RelativeAttackDirection.Left));
Assert.That(FacingMath.GetExposureMultiplier(TargetBodyPart.LeftArm, RelativeAttackDirection.Left), Is.EqualTo(1.2f));
Assert.That(FacingMath.GetExposureMultiplier(TargetBodyPart.RightLeg, RelativeAttackDirection.Left), Is.EqualTo(.7f));
Assert.That(FacingMath.GetExposureMultiplier(TargetBodyPart.Cockpit, RelativeAttackDirection.FrontLeft), Is.EqualTo(1f));
```

### Task 2: 기체 방향 데이터와 이동 완료·확정 회전

**Files:** Modify BattleUnit.cs, UnitMovement.cs, 기존 MovementTests.cs·MovementFacingTests.cs.
Create `Assets/_Game/Tests/PlayMode/FacingSelectionTests.cs`.

**Interfaces:**
- Consumes Task 1의 enum과 FacingMath.
- `BattleUnit.Facing: FacingDirection`, `BattleUnit.FacingState: FacingSelectionState`, `BattleUnit.HasPendingFacing: bool`은 읽기 전용이다.
- `BattleUnit.CanResolveFacing: bool`: 초기화·활성 상태·CanFight·대기 상태 및 바인딩된 턴의 플레이어 턴/전투 진행 여부를 검사한다.
  AP와 일반 CanAct는 검사하지 않는다. CanAct는 방향 잠금 때문에 false이므로 확정 허용에 재사용하면 안 된다.
- `internal BattleUnit.SetFacing(FacingDirection facing)`와 `SetFacingState(FacingSelectionState state)`는 이동 구현만 호출한다.
- `UnitMovement.TryConfirmFacing(FacingDirection facing) -> bool`: AwaitingConfirmation에서만 허용, AP와 무관하다.
- `UnitMovement.TryKeepArrivalFacing() -> bool`: AwaitingConfirmation에서 현재 확정 방향을 유지하고 즉시 잠금을 해제한다. ESC에 사용한다.
- 기존 `UnitMovement.TryMove(Vector2Int)`, `IsMoving`, `CancelMove()`의 경로 이동 의미를 유지한다.
  최종 방향 회전은 FacingState.Turning으로 구분한다.

- [ ] **Step 1: 상태 전이 테스트 작성.** 정상 아군 이동은 `IsMoving=false`, Facing=마지막 이동 방향,
  FacingState=AwaitingConfirmation이 된다. 적 이동은 FacingState=None이며 같은 논리 방향을 기록한다.
  `ConfirmAtZeroAPDoesNotChangeAPOrEvasion`, `ConfirmWithoutVisualCompletes`, `ConfirmSameDirectionCompletes`,
  `RejectRepeatedOrInvalidConfirmation`을 작성한다. AP 0은 테스트 중 설정한 이동 예산을 소비해서 만든다.
  확정 완료 후 Visual.forward와 FacingMath.Forward(Facing)가 일치하고 루트 회전·Collider가 보존되어야 한다.
- [ ] **Step 2: RED 확인.** `./tools/Run-UnityTests.ps1 -Mode PlayMode -Name facing-2-red -Filter FacingSelectionTests`.
- [ ] **Step 3: 방향 데이터 및 이동 연결 구현.** BattleUnit.Initialize에서 Visual.forward 또는 루트 정면으로
  초기 Facing을 정규화하고 Visual만 맞춘다. 각 칸에 완전히 도착했을 때 Facing을 기록한다.
  목적지 정상 도착 시 플레이어만 대기 상태를 설정한다. 상태 설정은 IsMoving 해제와 같은 완료 경로에서
  수행해 다음 행동이 허용되는 한 프레임의 틈을 만들지 않는다. standalone 유닛도 자기 HasPendingFacing으로 재이동을 막는다.
- [ ] **Step 4: 확정 회전과 취소 구현.** TryConfirmFacing은 즉시 Turning으로 전환하고 코루틴의 첫 프레임을
  yield한 뒤 기존 rotationSpeed로 Visual을 돌린다. 새 Facing은 회전 완료 시 기록한다.
  Visual 없음/같은 방향도 정상 종료한다. 중복 입력은 false이고 AP·회피·난수를 바꾸지 않는다.
  취소는 마지막 완료 칸으로 복원하고 확정된 Facing에 Visual을 맞추며 대기와 회전을 정리한다.
  BattleUnit 상태 변경·OnDisable 및 UnitMovement.OnDisable에서 동일 정리 경로를 사용한다.
- [ ] **Step 5: 취소 검증과 기존 이동 테스트 보완.** `CancelBeforeArrivalDoesNotOpenFacing`,
  `BlockedPathRestoresCompletedCellFacing`, `CriticalOrDisableDuringTurnRestoresCommittedFacing`를 추가한다.
  기존 테스트가 이동 후 재이동을 수행할 때 TryKeepArrivalFacing을 명시적으로 호출한다.
  새 대기 상태를 런타임 설정으로 끄거나 fixture에서 매 프레임 자동 확정하지 않는다.
- [ ] **Step 6: GREEN 확인.** `./tools/Run-UnityTests.ps1 -Mode PlayMode -Name facing-2-green -Filter 'FacingSelectionTests;MovementFacingTests;MovementTests'`.
- [ ] **Step 7: 결과 기록과 단계 검토.** movementSpeed 기본값 및 보호 자산 보존을 확인하고 사용자 확인을 받는다.

AP 0 도착 후 확정 테스트의 핵심 단언(유닛은 테스트 예산을 모두 소모해 정상 도착한 상태):

```csharp
Assert.That(unit.ActionPoints.CurrentPoints, Is.Zero);
Assert.That(unit.FacingState, Is.EqualTo(FacingSelectionState.AwaitingConfirmation));
int evasion = unit.MovementEvasion;
Assert.That(unit.Movement.TryConfirmFacing(FacingDirection.West), Is.True);
// 테스트 코루틴에서 FacingState.Turning이 끝날 때까지 제한 시간 내 대기한다.
Assert.That(unit.Facing, Is.EqualTo(FacingDirection.West));
Assert.That(unit.HasPendingFacing, Is.False);
Assert.That(unit.ActionPoints.CurrentPoints, Is.Zero);
Assert.That(unit.MovementEvasion, Is.EqualTo(evasion));
```

### Task 3: 행동 잠금과 4방향 선택 UI

**Files:** Modify TurnManager.cs, PlayerInputController.cs, UnitActionUI.cs, BattleDebugUI.cs.
Create `Assets/_Game/Tests/PlayMode/FacingSelectionInputTests.cs`; Modify 이동 완료를 사용하는 기존 입력·턴 테스트.

**Interfaces:**
- Consumes Task 2의 FacingState와 UnitMovement의 두 확정 API.
- `TurnManager.PendingFacingUnit: BattleUnit`: 등록 유닛 중 HasPendingFacing인 유효 유닛, 없으면 null.
- `TurnManager.IsAnyUnitBusy: bool`: 기존 IsAnyUnitMoving 또는 PendingFacingUnit이 있는 상태.
- `UnitActionMode.Facing` 추가. `PlayerInputController.SelectedFacing: FacingDirection`은 버튼 후보를 보관한다.
- `PlayerInputController.CanChooseFacing: bool`, `TryChooseFacing(FacingDirection facing) -> bool`, `TryConfirmFacing() -> bool`.
- `UnitActionUI.GetFacingButtonRect(FacingDirection facing) -> Rect`, `ConfirmFacingButtonRect: Rect`를 입력 테스트에도 사용한다.

- [ ] **Step 1: 대기 상태 행동 테스트 작성.** `PendingFacingBlocksDirectActionAPIs`에서 다른 아군의
  Movement.TryMove, CombatSystem.TryAttack, BattleUnit.TryEject, TurnManager.TryEndTurn이 false/미실행인지 단언한다.
  원래 공격 가능한 대상과 AP를 갖춘 fixture를 사용해 다른 실패 사유 때문에 우연히 통과하지 않게 한다.
  `WaitingAtZeroAPStillAllowsConfirmation`, `ClickingElsewhereKeepsPendingUnit`,
  `EscDuringTravelReopensFacingOnArrival`, `EscWhileWaitingKeepsArrivalFacingAndDeselects`,
  `EscDuringConfirmationDoesNotInterrupt`를 작성한다. 실제 Input System 마우스/키 입력으로 검사한다.
- [ ] **Step 2: RED 확인.** `./tools/Run-UnityTests.ps1 -Mode PlayMode -Name facing-3-red -Filter FacingSelectionInputTests`.
- [ ] **Step 3: TurnManager 잠금 구현.** HasTurnPermission과 TryEndTurn이 IsAnyUnitBusy를 확인한다.
  기존 IsAnyUnitMoving의 의미는 변경하지 않는다. 전투 종료 및 TurnManager 비활성화 시 pending 유닛을
  CancelMove로 정리한다. PendingFacingUnit 조회는 비활성/파괴/Critical 유닛을 정상적으로 제외하고 상태 정리와 일치시킨다.
- [ ] **Step 4: 입력 흐름 구현.** 선택된 유닛과 무관하게 TurnManager.PendingFacingUnit을 조회해
  해당 유닛을 표시하고 Facing 모드를 유지한다. 일반 SelectUnit의 Busy 제한을 우회할 수 있는
  private 표시 경로를 사용하며 외부 SelectUnit은 계속 거부한다. 후보 선택은 Visual/Facing을 바꾸지 않는다.
  확정은 Movement.TryConfirmFacing을 호출하고, 완료 후 Menu로 돌아간다.
  대기 ESC는 TryKeepArrivalFacing 후 ClearSelection, Turning ESC는 무시한다.
  얼굴 방향 UI의 입력은 일반 Busy 차단과 맵 raycast보다 먼저 처리하되 일반 행동은 허용하지 않는다.
- [ ] **Step 5: UI 구현.** 기존 UnitActionUI의 스케일·화면 범위 보정을 재사용한다.
  Facing 모드에서만 N(+Z), E(+X), S(-Z), W(-X), Confirm을 표시하며 후보와 회전 중 상태를 구분한다.
  Turning 중 방향·Confirm 버튼을 비활성화한다. IMGUI는 그리기만 하고 기존 Input System 경로에서 클릭을 한 번 처리한다.
  카메라 WASD/QE는 그대로 사용한다. BattleDebugUI.CanEndTurn도 IsAnyUnitBusy를 반영한다.
- [ ] **Step 6: 생명주기 회귀 테스트 추가.** 대기와 Turning 각각에서 Critical, Body 파괴, 외부 탈출 상태 전환,
  GameObject/BattleUnit/UnitMovement 비활성화, 전투 종료를 발생시켜 잠금이 남지 않는지 검사한다.
  입력 컴포넌트를 껐다 켜도 유닛의 대기 상태를 다시 찾아 확정 가능해야 한다.
- [ ] **Step 7: GREEN 확인.** `./tools/Run-UnityTests.ps1 -Mode PlayMode -Name facing-3-green -Filter 'FacingSelectionInputTests;UnitActionMenuTests;FactionTurnTests;EnemyTurnInputTests'`.
  기존 테스트의 다음 행동 앞에 새 방향 확정을 추가하되 잠금 단언은 제거하지 않는다.
- [ ] **Step 8: 결과 기록과 단계 검토.** AP 0, 실제 클릭·ESC, 직접 API 차단 결과를 보고하고 사용자 확인을 받는다.

방향 대기 유닛과 다른 행동 가능한 아군이 있는 fixture의 핵심 단언:

```csharp
Assert.That(turns.PendingFacingUnit, Is.SameAs(unit));
Assert.That(turns.TryEndTurn(), Is.False);
Assert.That(ally.Movement.TryMove(freeDestination), Is.False);
Assert.That(combat.TryAttack(ally, enemy).WasExecuted, Is.False);
Assert.That(ally.TryEject(), Is.False);
Assert.That(input.TryChooseFacing(FacingDirection.East), Is.True);
Assert.That(input.TryConfirmFacing(), Is.True);
```

### Task 4: 원거리 명중률과 부위 선택 표시 연결

**Files:** Modify CombatMath.cs, CombatSystem.cs, AttackPreview.cs, BattleDebugUI.cs.
Modify `Assets/_Game/Tests/EditMode/CombatMathTests.cs`; Create `Assets/_Game/Tests/PlayMode/FacingExposureCombatTests.cs`.

**Interfaces:**
- Consumes BattleUnit.Facing, FacingMath.GetRelativeDirection/GetExposureMultiplier.
- `AttackPreview.RelativeDirection: RelativeAttackDirection`, `AttackPreview.ExposureMultiplier: float` 추가.
- `CombatMath.CalculateRangedHitChance(int baseChance, int evasion, float partMultiplier, float coverMultiplier,
  float headMultiplier = 1f, float exposureMultiplier = 1f) -> int`로 기존 호출 호환을 유지한다.
- Preview.Direction은 기존 월드 엄폐 방향이다. RelativeDirection과 바꾸거나 재사용하지 않는다.

- [ ] **Step 1: 산술 및 전투 테스트 작성.** 기본 80/회피 0/머리 정상/대상 북향/서쪽 공격자일 때
  왼팔 86%, 오른팔 50%, Full에서 각각 43%, 25%를 단언한다.
  `ExposureIsMultipliedBeforeFinalClamp`는 100×0.9×1.2×0.5=54%를 단언한다.
  `ExposureOnlyRoundsOnce`는 80×0.9×1.1×0.75=59%를 단언한다.
  회피가 기본보다 크면 0%, 최종 결과가 100을 넘으면 100%여야 한다.
  `RotatingTargetChangesExposureButNotWorldCover`와 `BuildingStillRejectsAttackAfterFacingChange`를 작성한다.
- [ ] **Step 2: RED 확인.** EditMode `-Name facing-4-math-red -Filter CombatMathTests`,
  이어서 PlayMode `-Name facing-4-combat-red -Filter FacingExposureCombatTests`를 순차 실행한다.
- [ ] **Step 3: 계산 연결 구현.** GetPreview에서 기존 월드 incoming 방향과 target.Facing으로
  상대 방향을 구한다. 원거리만 노출 배율을 산출하고 근접은 1을 사용한다.
  배율을 모두 곱한 뒤 기존 Clamp 및 반올림을 한 번 적용한다. TryAttack은 계속 GetPreview 결과를 재사용한다.
  공격자의 Facing은 대상 부위의 좌우를 정하는 데 사용하지 않는다.
- [ ] **Step 4: 표시와 실제 사격 검증.** BattleDebugUI에 선택된 기체 Facing과 공격 상대 방향/노출 배율을
  표시한다. BodyTargetUI는 기존 CurrentAttackPreview 경로로 명중률을 보여준다.
  테스트에서 부위를 바꿔 화면 정보와 Preview를 비교하고 고정 roll을 사용해 Preview와 실제 명중 판정이 일치하는지 확인한다.
  실제 사격/턴 전환이 양쪽 Facing을 바꾸지 않는지도 단언한다.
  몸통·머리·콕피트 및 근접의 기존 결과, 머리 손상 배율과 회피의 동시 적용을 검사한다.
- [ ] **Step 5: GREEN 확인.** EditMode `-Name facing-4-math-green -Filter 'CombatMathTests;FacingMathTests'`,
  PlayMode `-Name facing-4-combat-green -Filter FacingExposureCombatTests` 실행. 모두 PASS.
- [ ] **Step 6: 결과 기록과 단계 검토.** 좌우·대각선 예제 및 엄폐 독립성을 보고하고 사용자 확인을 받는다.

중간 제한·반올림 방지 테스트의 단언:

```csharp
Assert.That(CombatMath.CalculateRangedHitChance(100, 0, .9f, .5f, 1f, 1.2f), Is.EqualTo(54));
Assert.That(CombatMath.CalculateRangedHitChance(80, 0, .9f, .75f, 1f, 1.1f), Is.EqualTo(59));
Assert.That(CombatMath.CalculateRangedHitChance(80, 90, .9f, 1f, 1f, 1.2f), Is.Zero);
Assert.That(CombatMath.CalculateRangedHitChance(100, 0, .9f, 1f, 1f, 1.2f), Is.EqualTo(100));
```

### Task 5: 적·저장 씬 통합 검증과 최종 리뷰

**Files:** Create `Assets/_Game/Tests/PlayMode/FacingSavedSceneTests.cs`.
Modify 필요한 기존 PlayMode 테스트 및 SqurdBattlePlayTests.cs. EnemyAIController.cs는 통합 결함이 확인될 때만 수정한다.

**Interfaces:** 앞 Task의 공개 API를 그대로 사용한다. 새 런타임 인터페이스를 추가하지 않는다.

- [ ] **Step 1: 적과 저장 씬 테스트 작성.** 적 이동은 FacingState=None으로 종료하고 마지막 경로 방향을
  유지하며 다음 적/아군 턴으로 진행하는지 확인한다. 이동하지 않은 적 방향은 유지되어야 한다.
  `BattleTest.unity`, `SqurdBattleTest.unity`, `1V1Test.unity`를 parameterized UnityTest로 각각 로드한다.
  실제 등록된 아군 중 동적으로 찾은 빈 인접 칸으로 이동하고, 4방향 선택/확정, 원거리 Preview를 확인한다.
  저작된 위치나 수를 단언하지 않으며 유효 유닛·경로가 없으면 이유와 재현 정보를 보고한다. 씬을 고쳐서 통과시키지 않는다.
  각 테스트 전후 씬 bytes가 같아야 한다.
- [ ] **Step 2: RED/GREEN 및 통합 접점 확인.**
  `./tools/Run-UnityTests.ps1 -Mode PlayMode -Name facing-5-integration -Filter 'FacingSavedSceneTests;EnemyAITests;MeleeEnemyTests;SqurdBattlePlayTests'`.
  이미 구현된 동작의 통합 테스트는 최초 PASS도 허용한다. 실패 시 원인을 좁힌 재현 테스트가 실패하는 것을 확인하고
  해당 접점만 수정한다. AI의 타깃 선택·공격 부위·이동 전략은 바꾸지 않는다.
- [ ] **Step 3: 전체 회귀 실행.** 최종 Assets를 동기화한 뒤 EditMode `-Name facing-final-editmode`,
  종료 확인 후 PlayMode `-Name facing-final-playmode`를 실행한다. 실제 XML의 총수·성공수·실패수와 종료코드를 기록한다.
  과거 테스트 총수를 예상값으로 고정하지 않는다. 사용자 속도·배치·무기 좌표에 대한 새 고정 단언은 추가하지 않는다.
- [ ] **Step 4: 별도 코드 리뷰.** Native 방식에 따라 requesting-code-review 스킬로 전체 변경의 독립 리뷰를 수행한다.
  승인된 spec와 이 plan, 변경 파일, 테스트 결과를 전달하고 잠금 우회·콜라이더 회전·상대 방향 계산·생명주기 정리에 집중한다.
  리뷰 지적 수정 시 재현 테스트와 해당 검증을 다시 수행한다. 파일시스템/Git 제약은 유지한다.
- [ ] **Step 5: 보존 및 완료 확인.** 보호 자산 SHA-256, 이동속도 코드와 기존 값, 검증 코드와 원본 코드 일치를 확인한다.
  최종 결과·사용법·확인하지 못한 제한을 `Artifacts/facing-part-exposure-progress.md`에 기록한다.
  사용자에게 방향 패널/ESC 사용법, 상대 8방향 배율 적용, 실제 테스트 수를 간단히 보고한다.

## 계획 자체 검토

- Spec의 승인 수치·4/8방향·AP 0·ESC·선택 해제·적·전투 종료·엄폐 독립성·저장 씬 검증을 Task 1~5에 배정했다.
- Facing/HasPendingFacing/FacingState와 Confirm API 이름을 전 Task에서 일치시켰다.
- 입력 컴포넌트가 없어도 논리 대기 상태는 유닛에 남고, 모델이 없어도 방향 및 확정은 동작한다.
- UI 후보는 전투 판정에 쓰지 않는다. 논리 방향은 이동 칸 완료 또는 최종 방향 회전 완료 때만 바뀐다.
- Review Focus 다섯 항목을 소유 Task의 테스트 단계에 포함했다.
- 실행 방식은 기존 Native를 유지한다. 계획 검토 후 구현을 시작하며 단계별 사용자 확인 요청도 유지한다.
