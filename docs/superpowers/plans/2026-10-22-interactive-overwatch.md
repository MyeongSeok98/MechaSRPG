# 적별 수동 경계사격 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. 기존 사용자 지정 방식은 Native 실행과 단계별 확인이다. 구현은 직접 수행하고 마지막에 독립 검토를 한 번 요청한다.

**Goal:** 경계 기체마다 적별 한 번의 부위 조준·Fire / Skip 기회를 제공하고, 결정 후 적 이동·행동을 안전하게 재개한다.

**Architecture:** UnitOverwatch의 전체 1회 소모를 적별 처리 집합으로 바꾼다. OverwatchSystem이 칸 완료 반응과 식별자 있는 결정 요청을 관리하고 CombatSystem의 공통 명중·피해 경로를 호출한다. 부위 조준 UI는 재사용하되 일반 공격 입력과 경계 결정 입력의 상태·권한은 분리한다.

**Tech Stack:** Unity 6000.5.5f1, C#, 기존 Input System·IMGUI·Unity Test Framework. 새 패키지는 추가하지 않는다.

**Spec:** [승인 설계](../specs/2026-10-22-interactive-overwatch-design.md). 2026-10-22 사용자가 해당 설계대로 구현계획 작성을 요청해 설계 문서 승인 완료. 이 계획은 사용자 승인 후 Native 방식으로 단계마다 확인받아 전체5단계를 완료했다. 최종 EditMode467/467·그래픽 PlayMode318/318 통과, 실패·건너뜀0. 독립 검토의 더블클릭 Important1건을 재현·수정하고 전체 회귀를 재실행했다.

## Global Constraints

- 경계 진입 비용 AP 2, 사격 추가 AP 없음, 명중률 ×0.75, 다음 자기 진영 턴 시작 시 만료를 유지한다.
- 현재 경계에서 Fire·빗나감·Skip으로 처리한 적은 다시 묻지 않는다. 사수별 기록은 독립이며 새 경계에서 초기화한다.
- 기존 전방 90도·맨해튼 사거리·개인 원형 시야·건물 시야/사선, 칸 도착 후 점유/AP/이동 기록 갱신 순서를 유지한다.
- 결정 전에는 이동을 재개하지 않는다. 여러 사수는 등록 순서대로 처리하고 Critical은 후속 피격 가능, Destroyed/Ejected는 후속 요청을 중단한다.
- 탄약·총 발사 횟수 제한·발사마다 AP 소비·적 AI의 경계 선택·파일럿 보정·새 연출은 추가하지 않는다.
- 일반 공격의 자기 턴·AP·이동 중 금지 권한을 완화하지 않는다. 전역 Time.timeScale로 일시정지하지 않는다.
- 사용자 맵·기체/장애물 배치·모델/무기 위치·이동/카메라 속도·그림자 설정, 기존 씬·프리팹·데이터를 수정하지 않는다.
- Git/GitHub 조회·스테이징·커밋·브랜치·worktree·푸시를 수행하지 않는다. 실행 전 파일 해시와 Artifacts 아래 백업으로 변경 범위를 확인한다.
- 구현은 Native 방식으로 한 단계씩 진행하고 각 단계의 검증 결과를 보고한 뒤 사용자 확인을 기다린다. 최종 독립 검토 외 구현 위임은 하지 않는다.

## Review Focus

1. 이전 창의 Fire·이중 클릭·요청 중 취소가 새로운 적에게 발사하지 않아야 한다. 단계 2의 식별자·재진입 테스트로 고정한다.
2. 첫 사격으로 Critical이 된 적이 다음 사수의 입력 대기 중 AI에 의해 취소되지 않아야 한다. 단계 3의 실제 AI 다중 사수 테스트로 고정한다.
3. 입력/UI가 꺼지거나 씬이 끝나도 적 이동·턴 busy가 남지 않아야 한다. 단계 2의 수신자 취소와 단계 4~5 수명 테스트로 고정한다.
4. Skip한 적의 다음 칸·영역 재진입은 다시 묻지 않고, 다른 사수·다른 적은 기회를 가져야 한다. 단계 1·3의 적별 기록 테스트로 고정한다.
5. 창을 연 뒤 무기·개인 시야·사선이 무효가 되면 오래된 미리보기로 발사하지 않아야 한다. 단계 2·4의 재검증 테스트로 고정한다.

---

## 파일과 책임

경로의 `Scripts/`, `Tests/`는 모두 `Assets/_Game/` 아래를 가리킨다.

| 파일 | 변경 책임 |
|---|---|
| `Scripts/Unit/UnitOverwatch.cs` | 처리한 적 집합, 유효 경계와 적별 기회 |
| `Scripts/Combat/OverwatchDecision.cs` (신규) | 읽기 전용 요청 값과 경계 결정 수신자 계약 |
| `Scripts/Combat/OverwatchSystem.cs` | 현재 요청·수신자·Fire/Skip·순차 대기·정리 |
| `Scripts/Combat/CombatSystem.cs` | 선택 부위 반응 미리보기/사격과 엄격한 실행 권한 |
| `Scripts/Unit/UnitMovement.cs`, `BattleUnit.cs` | 반응 완료 대기·손상 후 이동 재검사와 취소 |
| `Scripts/AI/EnemyAIController.cs`, `Scripts/Turn/TurnManager.cs` | Critical 후 후속 반응 대기·AI/턴 중단 정리 |
| `Scripts/Input/OverwatchDecisionController.cs` (신규) | 경계 요청 관찰·부위 선택·결정 명령·입력 수신 가능 상태 |
| `Scripts/Input/PlayerInputController.cs` | 경계 입력을 일반 선택/ESC/턴 초기화보다 먼저 처리 |
| `Scripts/Presentation/BodyTargetUI.cs` | 기존 이미지·클릭 영역·표시 재사용, 경계 Fire/Skip 분기 |
| `Scripts/Presentation/UnitActionUI.cs`, `BattleDebugUI.cs` | 대기 중 일반 패널 숨김·요청 상태 표시 |
| `Scripts/Setup/BattleSceneController.cs` | 기존 씬에서 결정 컨트롤러 런타임 연결 |
| `Tests/EditMode/Overwatch*Tests.cs`, `Tests/PlayMode/Overwatch*Tests.cs`, `Tests/Shared/OverwatchTestBattle.cs` | 기존 자동 1회 기대 수정과 수동 결정 회귀 검증 |
| `Tests/Shared/OverwatchDecisionTestReceiver.cs` (신규) | UI 없이 테스트가 명시적으로 Fire/Skip하도록 하는 수신자. 제품의 자동 사격 경로로 사용하지 않음 |

## 실행 전 준비와 검증 명령

- 구현 시작 때 적용 중인 AGENTS.md와 승인 설계/계획을 읽고, 대상 소스 및 보호 대상 파일을 SHA256 manifest와 함께 `Artifacts/InteractiveOverwatchBefore/`에 백업한다. 기존 파일이나 테스트 산출물을 삭제하지 않는다.
- 실행 시작 시 `Artifacts/interactive-overwatch-progress.md`를 만들고 단계별 변경·실패 원인·검증 증거·사용자 확인을 누적한다.
- Unity가 실행 중이지 않은 기존 복제 검증 프로젝트 `Artifacts/MeleeRangedTestProject`를 사용한다. 원본 Assets를 복제에 동기화하고 복제의 tools/Run-UnityTests.ps1을 실행한다. 복제 Unity 실행 중 소스를 덮어쓰지 않는다.
- 실행 위치: `Artifacts/MeleeRangedTestProject`. 아래 명령은 해당 복제 프로젝트에서 실행하며 XML/log를 원본 Artifacts에 보존한다. Unity 실행 경로가 다른 환경에서는 실행 도구의 현재 경로를 먼저 확인한다.

```powershell
.\tools\Run-UnityTests.ps1 -Mode EditMode -Filter OverwatchTargetStateTests -Name interactive-ow-step1
.\tools\Run-UnityTests.ps1 -Mode PlayMode -Graphics -Filter OverwatchDecisionTests -Name interactive-ow-step2
.\tools\Run-UnityTests.ps1 -Mode PlayMode -Graphics -Filter 'OverwatchMovementTests;OverwatchDecisionAITests' -Name interactive-ow-step3
.\tools\Run-UnityTests.ps1 -Mode PlayMode -Graphics -Filter 'OverwatchDecisionInputTests;OverwatchInputTests;UnitCameraFocusTests;WeaponInputTests' -Name interactive-ow-step4
.\tools\Run-UnityTests.ps1 -Mode EditMode -Name interactive-ow-final-edit
.\tools\Run-UnityTests.ps1 -Mode PlayMode -Graphics -Name interactive-ow-final-play
```

필터에는 실제 테스트 클래스만 지정하며 실행 건수가 0이 아닌지 확인한다. 기존 부위 조준 회귀도 관련 PlayMode 테스트에 포함한다.

각 단계는 의미 있는 테스트의 기대 실패를 확인한 뒤 최소 구현한다. 컴파일 오류만으로 기능의 실패 검증을 대신하지 않는다. 통과 후 관련 회귀를 실행하고 실행 건수 0을 성공으로 간주하지 않는다.

### 단계 1: 경계 유지와 적별 처리 기록

**Files:** Modify `Scripts/Unit/UnitOverwatch.cs`, `Scripts/Combat/{OverwatchSystem,CombatSystem}.cs`. Create `Tests/EditMode/OverwatchTargetStateTests.cs`. Modify `Tests/EditMode/OverwatchStateTests.cs`, `Tests/PlayMode/OverwatchMovementTests.cs`의 경계 종료 기대.

**Interfaces:**

- `UnitOverwatch` internal `bool CanReactTo(BattleUnit target)`, `bool TryConsumeTarget(BattleUnit target)`. 기존 `TryConsume()`은 제거하고 모든 호출자를 수정한다.
- 기존 `Arm`, `Cancel`, `IsActive`와 무기/방향/수명 검증은 유지한다. private `HashSet<BattleUnit>`으로 현재 경계에서 처리한 적을 보관한다.
- 테스트는 기존 reflection 방식의 작은 fixture helper로 internal 메서드를 호출한다. 테스트를 위해 제품 API를 public으로 넓히지 않는다.

- [x] `DifferentTargetsHaveIndependentOpportunities`, `DifferentWatchersHaveIndependentRecords`, `UsedTargetCannotReactAgainUntilRearmed`를 작성한다. X 소모 후 X=false/Y=true/다른 사수 X=true, 경계 IsActive=true, Cancel/새 Arm 후 X=true를 검증한다.
- [x] 필터 `OverwatchTargetStateTests`를 실행해 기존 전체 1회 소비 구현에서 기대 실패를 확인한다.
- [x] 적별 소비 API를 구현하고 Arm/Cancel에서 기록을 비운다. 무효 경계·null/파괴된 참조는 기회를 제공하지 않는다. 이 단계의 기존 자동 반응 호출도 적 인자를 전달하고 범위 판정에 CanReactTo를 연결한다.
- [x] 첫 사격 이후 전체 경계를 종료한다고 가정한 기존 상태/이동 테스트만 새 의미에 맞게 수정한다. 한 적의 여러 칸에서는 1회, 다른 적에게는 추가 발사가 가능한 중간 동작을 검증한다.
- [x] 상태 테스트와 기존 경계 범위·수명·명중 계산 테스트를 실행하고 결과를 기록한다. 관련 EditMode 65/65, PlayMode 38/38 통과. 전체 EditMode는 기존 씬의 오른손 무기 필수 가정 1건 실패를 변경 전 코드에서도 재현했다.
- 사용자 확인 완료: 적별 상태 기반을 승인받아 단계2로 진행했다.

### 단계 2: 식별자 있는 Fire / Skip 대기와 선택 부위 사격

**Files:** Create `Scripts/Combat/OverwatchDecision.cs`, `Tests/Shared/OverwatchDecisionTestReceiver.cs`, `Tests/PlayMode/OverwatchDecisionTests.cs`. Modify `Scripts/Combat/{OverwatchSystem,CombatSystem}.cs`, `Tests/Shared/OverwatchTestBattle.cs`, `Tests/EditMode/OverwatchCombatTests.cs`.

**Interfaces:**

- `readonly struct OverwatchDecision`: 읽기 전용 `long Id`, `BattleUnit Watcher`, `BattleUnit Target`, `WeaponSlot Slot`. 식별자는 같은 서비스 수명 동안 재사용하지 않는다.
- 같은 파일의 `IOverwatchDecisionReceiver { bool IsReady { get; } }`: 특정 경계 입력의 수신 가능 여부만 표현한다. 범용 이벤트 버스는 만들지 않는다.
- OverwatchSystem: `OverwatchDecision? PendingDecision { get; }`, `ConfigureDecisionReceiver(IOverwatchDecisionReceiver receiver)`, `UnregisterDecisionReceiver(IOverwatchDecisionReceiver receiver)`.
- OverwatchSystem: `AttackPreview GetDecisionPreview(long id, TargetBodyPart part)`, `AttackResult TryFire(long id, TargetBodyPart part)`, `bool TrySkip(long id)`, `bool IsResolvingMover(BattleUnit mover)`.
- CombatSystem internal: `AttackPreview GetOverwatchPreview(BattleUnit attacker, BattleUnit target, WeaponSlot slot, TargetBodyPart part)`, `AttackResult TryOverwatchAttack(BattleUnit attacker, BattleUnit target, WeaponSlot slot, TargetBodyPart part)`.
- OverwatchSystem internal: `bool CanPreviewDecision(BattleUnit watcher, BattleUnit target, WeaponSlot slot)`, `bool CanResolveShot(BattleUnit watcher, BattleUnit target, WeaponSlot slot, TargetBodyPart part)`. 후자는 TryFire가 동기 실행 중인 해당 부위와 사수/적 조합에만 권한을 준다.
- `OverwatchDecisionTestReceiver : IOverwatchDecisionReceiver`는 `bool IsReady { get; set; }`만 제공하며 테스트가 준비 상태를 직접 조절한다. `OverwatchTestBattle`에 `AttachDecisionReceiver() : OverwatchDecisionTestReceiver`와 `WaitForDecision(float timeoutSeconds = 3f) : IEnumerator`를 추가한다. 후자는 제한 시간 뒤 PendingDecision이 없으면 assertion 실패로 끝내며 자동 Fire/Skip은 하지 않는다.

단계2 테스트에서 고정할 대표 기대값은 다음과 같다. 기본 fixture는 몸통20·머리6, 고정 피해5·명중 난수0을 사용하며, 테스트가 직접 적 이동을 시작하고 WaitForDecision으로 도착을 기다린 상태다.

```csharp
long id = b.Overwatch.PendingDecision.Value.Id;
Assert.That(b.Mover.Mobility.MovedCells, Is.EqualTo(1));
Assert.That(b.Mover.ActionPoints.CurrentPoints, Is.EqualTo(4));
Assert.That(b.Mover.Health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(6));
Assert.That(b.Overwatch.TryFire(id, TargetBodyPart.Head).WasExecuted, Is.True);
Assert.That(b.Mover.Health.GetCurrentHP(TargetBodyPart.Head), Is.EqualTo(1));
Assert.That(b.Mover.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
Assert.That(b.Watcher.ActionPoints.CurrentPoints, Is.EqualTo(3));
Assert.That(b.Watcher.Overwatch.IsActive, Is.True);
Assert.That(b.Overwatch.TryFire(id, TargetBodyPart.Head).WasExecuted, Is.False);
```

- [x] `WaitsWithoutAutoFire`, `FireUsesSelectedPartAndKeepsGuard`, `MissAndSkipConsumeOnlyThisTarget`, `StaleAndDoubleFireDoNotExecute`를 작성한다. 실제 칸 도착 뒤 최소 3프레임 HP/AP/좌표 불변, Head 사격 시 Head만 감소, Skip 시 난수 호출0/AP 불변, 이전 ID와 중복 입력의 WasExecuted=false를 검증한다.
- [x] `DirectReactionCallDuringPendingIsRejected`를 추가한다. 요청이 열려 있어도 TryFire 바깥의 internal 공격 호출은 실패해야 한다. 이전 일반 공격 권한 테스트도 유지한다.
- [x] 기대 실패를 실행한 뒤, ResolveAfterStep이 등록 순서로 요청 하나를 열고 Fire/Skip/무효화까지 yield하도록 구현한다. 발동 조건에 맞아도 수신자가 없거나 준비되지 않으면 이번 반응 배치를 끝내며 자동 사격이나 무한 대기를 하지 않는다.
- [x] 미리보기와 실행을 분리한다. TryFire는 ID/상태/무기/개인 시야/사선/부위를 다시 검사하고, 동일 요청의 재진입을 먼저 잠근 후 공통 CombatSystem 실행을 호출한다. 발사 직전 TryConsumeTarget으로 해당 적만 소모한다. 실패 시 Skip으로 기록하지 않는다.
- [x] TrySkip은 유효 ID의 해당 사수/적만 처리 기록에 넣고 요청을 완료한다. 정상 입력 완료는 IsResolving 전체 잠금을 즉시 풀지 않고 다음 사수 처리로 진행한다.
- [x] `WeaponOrVisionInvalidatedWhilePendingDoesNotFire`, `ReceiverLostReleasesBatchWithoutConsumingTarget`, `OldReceiverCannotUnbindReplacement`를 추가한다. 입력 수신자 해제 시 현재 배치를 취소하고, 무기/시야/사수만 무효일 때 다른 사수는 재검사하여 계속할 수 있음을 검증한다.
- [x] 요청 식별자와 배치 버전을 따로 관리한다. 발사 도중 파괴/전투 종료 이벤트로 CancelAll이 재진입해도 이전 finally가 새 요청을 정리하지 않게 한다. 취소 후 PendingDecision=null, IsResolving=false, 늦은 입력 거부를 검증한다.
- [x] 단계2 테스트와 EditMode 경계 전투/명중/범위 테스트를 실행한다. 공용 fixture는 명시적 테스트 수신자를 연결하되 자동 결정은 켜지 않는다. 기존 PlayMode 자동 기대의 나머지 수정은 단계3~5에 배정했음을 기록했다. 관련 EditMode 65/65, 새 결정 PlayMode 20/20 통과. 사용자 확인을 받아 단계3로 진행했다.

### 단계 3: 실제 적 AI의 일시 대기·다중 사수·이동 재개

**Files:** Modify `Scripts/AI/EnemyAIController.cs`, `Scripts/Turn/TurnManager.cs`, `Scripts/Unit/{UnitMovement,BattleUnit}.cs`, `Tests/PlayMode/OverwatchMovementTests.cs`. Create `Tests/PlayMode/OverwatchDecisionAITests.cs`.

**Interfaces:** 단계2의 `PendingDecision`, `IsResolvingMover`, `TryFire`, `TrySkip`를 소비한다. UnitMovement의 칸 완료 코루틴과 IsResolvingStep, TurnManager.IsAnyUnitBusy는 유지한다.

- [x] 실제 EnemyAIController fixture로 `EnemyStaysOnCommittedCellUntilDecisionThenCompletesTurn`, `SkippedEnemyDoesNotPromptOnNextCellOrReentry`, `SecondEnemyGetsItsOwnDecision`을 작성하고 기대 실패를 확인한다. AP는 실제 도착한 칸만 소모하며 입력 전 다음 적/턴이 시작되지 않아야 한다.
- [x] `CriticalMoverWaitsForSecondWatcherBeforeStopping`을 작성한다. 첫 부위 사격이 세 번째 주요 부위를 파괴하도록 기존 HP와 파괴 난수만 테스트에서 설정한다. 다음 사수의 창이 최소 3프레임 유지되고 AI가 취소하지 않아야 한다.
- [x] AI의 이동 대기 루프는 전투·유닛 존재·자기 실행 권한의 무효화와, 반응 종료 후 검사할 CanFight를 구분한다. 유효한 반응 배치 중 Critical 때문에 CancelCurrentAction을 먼저 호출하지 않는다. 배치가 끝난 뒤 이동·공격 가능 상태를 다시 판단한다.
- [x] TurnManager.HandleUnitStateChanged가 반응 도중 ActiveEnemy를 비울 때 후속 요청을 취소하지 않도록 관련 실행 상태를 정리한다. 사망·탈출·전투 종료·컴포넌트 비활성화에 의한 취소는 지연하지 않는다.
- [x] `FirstWatcherSkipsSecondWatcherFires`, `DestroyedTargetClosesRemainingRequests`, `LegDamageStopsAtCommittedCell`, `HeadDamageUsesExistingPenalty`를 추가한다. 등록 순서, AP 정확성, transform/점유 일치, 후속 공격 유무를 검증한다.
- [x] 이동/AI/턴/서비스 비활성화 중 요청 취소 후 IsResolving=false와 새 입력 가능 상태를 검증한다. 기존 OverwatchMovementTests는 명시적인 Fire/Skip을 넣어 새 규칙으로 갱신하며, 기다리는 동안 프레임을 실제로 넘겨 수명 회귀를 드러낸다.
- [x] 단계3 필터와 기존 EnemyAITests/FactionTurnTests/이동·부위 손상 관련 테스트를 실행한다. 관련 EditMode 134/134와 PlayMode 중복 제외 142/142 통과. 사용자 확인을 받아 단계4로 진행했다.

### 단계 4: 기존 부위 조준 UI의 경계 결정 표시와 입력

**Files:** Create `Scripts/Input/OverwatchDecisionController.cs`, `Tests/PlayMode/OverwatchDecisionInputTests.cs`. Modify `Scripts/Input/PlayerInputController.cs`, `Scripts/Presentation/{BodyTargetUI,UnitActionUI,BattleDebugUI}.cs`, `Scripts/Setup/BattleSceneController.cs`, `Tests/PlayMode/OverwatchInputTests.cs`.

**Interfaces:**

- `OverwatchDecisionController : MonoBehaviour, IOverwatchDecisionReceiver`.
- `Initialize(OverwatchSystem system, PlayerInputController input, BodyTargetUI view)`, `bool IsReady { get; }`, `bool IsPending { get; }`, `OverwatchDecision? CurrentDecision { get; }`, `TargetBodyPart SelectedPart { get; }`, `AttackPreview CurrentPreview { get; }`.
- `bool TrySelectPart(TargetBodyPart part)`, `AttackResult TryFire()`, `bool TrySkip()`. 내부에 표시 중인 ID를 저장하고 입력은 그 ID로 서비스에 전달한다.
- BodyTargetUI: 기존 `Initialize(PlayerInputController)`를 유지하고 `InitializeOverwatch(OverwatchDecisionController controller)`, `Rect SkipButtonRect { get; }`를 추가한다. PanelRect/ImageRect/GetPartRect와 기존 표시 프로퍼티는 일반 공격과 반응에서 공유한다.
- PlayerInputController: `ConfigureOverwatchDecision(OverwatchDecisionController controller)`로 경계 입력 경로만 연결한다. BodyTargetUI의 `IsVisible`은 controller.IsReady가 참조하지 않는다. 준비 여부는 컴포넌트 활성·초기화로만 판단해 순환 조회를 피한다.

- [x] `EnemyTurnBodyPanelShowsShooterTargetWeaponAndDefaultBody`, `ClickHeadUpdatesReactionChanceAndFireHitsHead`, `SkipDoesNotSpendAPAndCannotRepeatSameEnemy`의 실제 Input System 클릭 테스트를 작성하고 기대 실패를 확인한다.
- [x] 컨트롤러는 새 ID마다 부위를 Torso로 초기화한다. 부위 변경은 기록을 소비하지 않고 현재 미리보기를 읽는다. 일반 SelectedUnit/TargetUnit/SelectedWeaponSlot에 반응 조합을 덮어쓰지 않는다.
- [x] BodyTargetUI는 반응 중 CanChooseAction/자기 턴을 표시 조건으로 사용하지 않고 controller의 유효 Pending을 사용한다. 기존 이미지·클릭 영역·HP 표시를 공유하고 Fire/Skip을 별도 버튼으로 표시한다. 사수·적·무기를 구분할 수 있는 표시를 추가한다.
- [x] PlayerInputController.Update는 일반 ESC/턴 변경/목표 지우기보다 먼저 반응 입력을 처리한다. 반응 중 클릭은 부위 UI로 전달하고 ESC/허공 클릭/다른 기체/일반 행동은 반응을 취소하지 않고 무시한다. 직접 SelectUnit/일반 행동 메서드 호출도 거부한다.
- [x] BodyTargetUI 클릭은 일반 공격에서 기존 TrySelectBodyPart/TryAttackSelected, 반응에서 controller API를 사용한다. UnitActionUI는 반응 중 숨겨 공격창이 겹치지 않게 한다. 카메라의 WASD/QE/휠 입력은 유지한다.
- [x] controller.IsReady는 자신·PlayerInputController·BodyTargetUI의 실재/활성 상태와 필수 입력 초기화를 검증한다. OnDisable/OnDestroy 및 입력/표시 비활성화에서 등록을 해제하거나 취소한다. 재활성화는 새 등록만 하며 이전 요청을 복원하지 않는다.
- [x] BattleSceneController의 기존 입력 초기화 뒤 같은 루트에 controller를 하나만 생성·초기화한다. 반복 초기화로 수신자나 UI가 늘어나지 않게 한다.
- [x] `EscapeAndWorldClicksKeepPending`, `CameraMovesDuringDecision`, `DisablingViewOrInputReleasesDecision`, `NewTargetResetsPartToBody`, `OrdinaryAttackStillNeedsOwnTurn`을 추가한다. 1280×720/640×360에서 Fire/Skip과 부위 클릭 영역을 검증·촬영하고 기존 Game View 크기 복구를 재사용한다.
- [x] 단계4 필터와 기존 무기 선택/긴급탈출/이동 후 방향/UI 클릭 회귀를 실행하고 화면과 결과를 확인했다. 관련 EditMode75/75·PlayMode 중복 제외156/156 통과. 마지막 단계5 진행은 사용자 확인 대기다.

### 단계 5: 샘플 씬·전체 회귀·문서·독립 검토

**Files:** Modify `Tests/PlayMode/OverwatchSceneTests.cs`, `docs/overwatch-guide.ko.md`, `docs/current-player-turn-actions.ko.md`, `docs/current-code-architecture.ko.md`, `Readme.md`. Finalize execution ledger `Artifacts/interactive-overwatch-progress.md`.

- [x] 실제 1V1Test/SqurdWeaponTest를 읽어 서비스·입력 수신자·기체 상태가 런타임에 연결되고 경계 진입 메뉴가 정상 동작하는지 검증한다. 원본 씬 바이트를 비교하며 배치/속도 assertion이나 씬 저장은 하지 않는다.
- [x] 기존 OverwatchSceneTests의 실제 AI 사례를 수동 Fire/Skip으로 전환한다. 정상 다음 턴, 여러 적별 기회, 무기 제거/복원, 서비스 재활성화, 치명적 반응으로 전투 종료 후 잠금 해제를 검증한다.
- [x] 기존 UI·직접 이동 fixture에 대기 결정을 명시하고, 전체 1회 소모 또는 몸통 자동 사격을 주장하는 낡은 기대를 제거한다. 새 테스트를 건너뛰거나 자동 타임아웃 사격으로 통과시키지 않는다.
- [x] 안내/README는 적별 1회·Skip 재질문 없음·부위 조준·명중률·AP·입력 대기·만료를 반영한다. 이전의 전체 1회 자동 몸통 사격 설명을 교체한다. 최초 설계/계획 문서는 역사로 남기고 새 문서 링크로 변경 이력을 안내한다.
- [x] 전체 EditMode와 그래픽 PlayMode를 위 최종 명령으로 실행한다. 실제 XML의 실행 건수·통과·실패·건너뜀을 읽고 실패/건너뜀0을 확인한다. 이전의 460/267을 현재 건수로 가정하지 않는다.
- [x] 새 .meta, XML/log, UI 캡처를 원본에 보존한다. 실행 전 manifest와 비교해 보호 대상 씬/프리팹/데이터/Art/카메라·렌더 설정 불변과 허용한 소스·테스트·문서 변경 목록을 확인한다.
- [x] requesting-code-review 절차로 새 문맥의 독립 검토자에게 승인 설계·이 계획·변경 목록·백업 기준·최종 테스트 증거를 제공한다. Git 조작 없는 읽기 전용 검토로 Critical/Important 문제를 확인한다. 필요 수정은 재현 테스트부터 진행하고 관련 검증 후 최종 결과를 갱신한다.
- [x] 실행 기록과 체크리스트를 마감하고 사용법·검증 결과·제한을 보고한다. 이 마지막 단계가 끝나면 추가 단계 확인을 요청하지 않는다.

## 자체 검토와 인계

설계의 적별 기록은 단계1, 요청/권한/재검증은 단계2, 실제 AI와 Critical·이동 재개는 단계3, 표시/입력/카메라/수신자 수명은 단계4, 저장 씬과 전체 회귀·문서·독립 검토는 단계5에 대응시켰다. Review Focus의 다섯 항목에 담당 테스트를 배치했다.

각 단계의 API와 후속 소비 이름을 대조한다. 수동 반응을 일반 공격의 CanChooseAction에 넣지 않고, 실행 허용과 미리보기 허용을 구분한다. UI 없는 코어 테스트는 명시적인 수신자 fixture로 제어하며 제품 자동 사격 우회 경로는 두지 않는다.

이 계획 작성 단계에서는 제품 코드·씬·데이터를 변경하거나 테스트를 실행하지 않았다. 사용자 검토 후 기존 Native 방식으로 단계1부터 시작하며 단계마다 확인받는다.
