# Overwatch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** AP 2로 전방 90도 경계에 진입하고, 적 이동 중 명중률 ×0.75의 몸통 사격을 1회 수행한다. 기존 기동 회피의 명중률 차감은 제거한다.

**Architecture:** UnitOverwatch는 유닛 상태, OverwatchSystem은 감시 조건과 순차 반응을 담당한다. CombatSystem은 일반 공격과 반응 공격의 계산·피해를 공유하되 권한과 AP 소모를 구분한다. 이동 코루틴은 칸 진입 완료 후 반응을 기다리고 상태를 재검사한다.

**Tech Stack:** Unity 6000.5.5f1, C#, 기존 MonoBehaviour·코루틴·Input System·IMGUI, Unity Test Framework. 외부 패키지 추가 없음.

**Spec:** [승인된 설계](../specs/2026-10-19-overwatch-design.md)

## Global Constraints

- 경계 비용 AP 2와 명중률 배율 0.75다.
- 몸통을 조준하며 무기 피해·파지·거리·엄폐·머리 손상 보정은 일반 원거리 공격과 공유한다.
- 경계선은 포함한다. 무기 사거리는 맨해튼 거리, 개인 탐지 범위는 기존 원형 거리다.
- 다음 자기 진영 턴 시작 시 AP 회복 전에 남아 있는 경계를 해제한다.
- CharacterData와 파일럿 패시브는 연결하지 않는다. 적 AI의 경계 선택 행동도 추가하지 않는다.
- 기존 씬 배치, 모델·무기 위치, 이동 속도, 카메라 줌·이동 속도와 그림자 설정은 변경하지 않는다.
- 기존 이동량 기록은 유지한다. Movement Evasion은 공격식과 방어 보정 UI에서 제외한다.
- Native로 현재 세션에서 직접 구현하고, 각 단계 완료 후 사용자 확인을 받는 기존 진행 방식을 유지한다.
- Git 저장소가 없으므로 commit/worktree를 만들지 않는다. 변경 전 파일과 해시를 Artifacts/OverwatchBefore에 보존한다.

## Review Focus

1. 무기 AP 비용이 5여도 현재 AP 2로 경계를 설정하고, 사격에 추가 AP를 요구하지 않아야 한다. 단계 2·3에서 검증한다.
2. 팀원 시야만으로 발견한 적이 경계 기체 개인 탐지 범위를 벗어나면 반응하지 않아야 한다. 단계 2·3에서 검증한다.
3. 이동 중 사격으로 전투가 끝나 코루틴이 중단돼도 점유·반응 잠금·AI 턴 진행 상태가 남지 않아야 한다. 단계 3·5에서 검증한다.
4. 무기 교체·기체 비활성화·긴급탈출 후 유효하지 않은 경계로 발사하거나 AP가 환급되지 않아야 한다. 단계 2·5에서 검증한다.
5. 경계 ×0.75는 최종 정수 명중률에 곱하지 않으며, 아주 큰 배율과 0 배율 조합도 NaN 없이 처리해야 한다. 단계 1에서 검증한다.

## 파일과 책임

새 런타임 파일은 모두 기존 폴더를 사용한다.

- `Assets/_Game/Scripts/Combat/OverwatchMath.cs`: 4방향 전방 90도 기하 판정.
- `Assets/_Game/Scripts/Unit/UnitOverwatch.cs`: 활성 상태·고정 무기 슬롯/참조·방향·일회 소모.
- `Assets/_Game/Scripts/Combat/OverwatchSystem.cs`: 진입 검증·개인 시야·감시 칸·반응 요청과 순서.
- 기존 `CombatMath`, `CombatSystem`, `AttackPreview`: 공통 명중률과 실행 경로.
- 기존 `BattleUnit`, `UnitMovement`, `TurnManager`, `BattleSceneController`: 행동 제한·초기화·수명·이동 연결.
- 기존 `PlayerInputController`, `UnitActionUI`, `GridVisualizer`, `BattleDebugUI`: 조작과 표시.
- 새 테스트: 아래 각 단계의 EditMode/PlayMode 파일. 기존 테스트의 옛 기동 회피 기대값도 새 규칙으로 수정한다.

## 검증 실행 규약

테스트 프로젝트는 `Artifacts/MeleeRangedTestProject`를 사용한다. Unity가 완전히 종료된 뒤에만 수정한 Assets와 새 .meta를 동기화한다. 생성된 새 .meta는 원본에도 보존한다. 프로젝트 자체를 대량 삭제하거나 기존 씬을 재생성하지 않는다.

실행 위치는 해당 테스트 프로젝트 루트다.

```powershell
.\tools\Run-UnityTests.ps1 -Mode EditMode -Name overwatch-stepN-edit -Filter 'TestClass'
.\tools\Run-UnityTests.ps1 -Mode PlayMode -Graphics -Name overwatch-stepN-play -Filter 'TestClass'
```

각 단계는 테스트 작성 → 예상 실패 확인 → 최소 구현 → 통과 확인 순서다. 컴파일 오류만으로 실패 검증을 대신하지 않는다. 새 타입이 없으면 기존 MechaDataTests처럼 타입 조회 후 명시적 assertion으로 부재를 검출할 수 있다. 결과 XML과 로그는 원본 Artifacts에 복사한다. 매 단계 실제 파일 변경을 검토하고 보호 파일 해시를 비교한 뒤 사용자에게 결과와 다음 단계를 알리고 확인을 기다린다.

### 단계 1: 명중률에서 기동 회피 제거

**Files:** Modify `Scripts/Combat/{CombatMath,CombatSystem,AttackPreview}.cs`, `Scripts/Presentation/BattleDebugUI.cs`, 기존 `Tests/EditMode/{CombatMathTests,WeaponCombatTests,MeleeCombatTests,BodyTargetCombatTests,CombatSystemTests}.cs` 중 영향을 받는 테스트. 새 테스트 `Tests/EditMode/OverwatchAccuracyTests.cs`. 모두 `Assets/_Game/` 기준이다.

**Interfaces:**

- Produce `CombatMath.CalculateAttackHitChance(int baseChance, float partMultiplier, float coverMultiplier, float headMultiplier = 1f, float gripMultiplier = 1f, float distanceMultiplier = 1f, float reactionMultiplier = 1f) : int`.
- 일반 공격의 reactionMultiplier는 1, 경계사격은 0.75다. 이 단계는 경계 발동을 연결하지 않는다.

- [x] 테스트 `MovementDoesNotChangeRangedOrMeleePreviewOrExecution`에서 동일 조건 대상의 이동 기록 0칸/5칸 명중률이 같음을 검증한다. 기존 코드에서는 명중률이 달라 실패해야 한다.
- [x] `ReactionMultiplierAppliesBeforeSingleRounding`은 원시 명중률 79.4에 ×0.75를 적용한 결과 60을 검증한다. `HighAccuracyIsNotClampedBeforeReaction`은 100 ×1.2 ×0.75 = 90을 검증한다.
- [x] `ZeroDistanceMultiplierRemainsZeroWithLargeGrip`은 base 80, grip float.MaxValue, distance 0 결과 0을, 두 큰 양수 배율은 상한 100을 검증한다. 음수 base는 최종 0으로 제한한다.
- [x] 위 테스트의 예상 실패를 확인한 후 새 공통식을 구현한다. 중간 곱셈은 double로 처리하고, 최종 0~100 제한 후 반올림을 한 번 수행한다. 기동 회피 인자를 넣지 않는다.
- [x] CombatSystem을 새 식으로 연결한다. 사용되지 않는 구식 `CalculateHitChance`와 기존 `CalculateRangedHitChance`를 호출부와 함께 정리하고 `AttackPreview.Evasion`을 제거한다. 기존 테스트의 옛 식 기대값을 새 규칙으로 교체한다. `UnitMobility` 기록 테스트는 유지한다.
- [x] 방어 보정 UI의 Movement Evasion 문구를 제거한다. 이동량 표시는 유지할 수 있다. 전체 EditMode를 실행해 실패 0을 확인한다.
- [x] 변경 검토·검증 결과를 기록하고 사용자 확인을 받는다.

### 단계 2: 경계 상태와 감시 범위

**Files:** Create `Scripts/Combat/{OverwatchMath,OverwatchSystem}.cs`, `Scripts/Unit/UnitOverwatch.cs`, `Tests/EditMode/{OverwatchRangeTests,OverwatchStateTests}.cs`. Modify `Scripts/Unit/BattleUnit.cs`, `Scripts/Turn/TurnManager.cs`, `Scripts/Setup/BattleSceneController.cs`.

**Interfaces:**

- `OverwatchMath.IsInForwardCone(Vector2Int origin, Vector2Int target, FacingDirection facing) : bool`.
- `UnitOverwatch` provides `IsActive : bool`, `Slot : WeaponSlot`, `Weapon : WeaponData`, `Facing : FacingDirection`; internal `Arm(WeaponSlot slot, WeaponData weapon, FacingDirection facing) : void`, `Cancel() : void`, `TryConsume() : bool`. System만 정상 진입을 확정한다.
- `OverwatchSystem.Initialize(GridManager grid, CombatSystem combat, TurnManager turns, BattleUnit[] units) : void`.
- `CanBegin(BattleUnit unit, WeaponSlot slot) : bool`, `TryBegin(BattleUnit unit, WeaponSlot slot, FacingDirection facing) : bool`.
- `GetCoverageCells(BattleUnit unit, WeaponSlot slot, FacingDirection facing) : List<GridCell>`: 준비 화면 전용, AP 미소모.
- `BattleUnit.Overwatch : UnitOverwatch`; `TurnManager.ConfigureOverwatch(OverwatchSystem system) : void`.

- [x] `OverwatchRangeTests`에서 북쪽 기준 (0,3), (3,3), (-3,3)은 포함, (4,3), (3,0), (0,-1), 원점은 제외를 검증한다. 다른 3방향도 같은 회전 규칙으로 검증한다. 유효하지 않은 Facing은 false다.
- [x] 개인 탐지 반경 3, 무기 사거리 7에서 상대 좌표 (3,3)을 제외하고, 무기 맨해튼 거리 경계는 포함하는 테스트를 추가한다. 건물 너머, 팀원만 탐지한 칸을 제외하는 fixture를 만든다.
- [x] `OverwatchStateTests`에서 AP 5→3, AP 2→0 진입 성공, AP 1 거부, 무기 공격 비용 5여도 AP 2로 진입 성공을 검증한다. melee·잘못된 방향·중복 진입·미등록 유닛 거부, 실패 시 AP 불변을 검증한다.
- [x] 예상 실패 후 기하 판정을 정수 좌표로 구현한다. 정면 성분 >0이고 정면 성분 ≥ 수직 성분 절댓값이면 포함한다. 개인 시야와 사선은 기존 GridManager 메서드를 재사용한다.
- [x] 런타임 초기화에서 기존 프리팹/씬을 수정하지 않고 UnitOverwatch를 연결한다. 활성 경계는 일반 `CanAct`를 막되 `CanEject`와 선택은 허용한다. 일반 공격과 이동의 기존 직접 진입 경로도 경계를 우회하지 못하게 한다.
- [x] 다음 자기 턴 AP 회복 전에 해제하고, 전투불능·탈출·비활성화·전투 종료 시 정리한다. 시작할 때의 WeaponData 참조와 슬롯을 저장하고 이후 부적합하면 취소한다. 검사할 때마다 현재 장착 정보를 검증한다.
- [x] `WeaponReplacementInvalidatesGuardWithoutRefundEvenIfOriginalIsRestored`와 `GuardBlocksNormalActionsButStillAllowsEmergencyEjection`를 추가하고 해당 EditMode 및 기존 턴/상태 테스트를 통과시킨다. 이 단계에는 UI와 실제 반응 사격이 아직 없다.
- [x] 변경 검토·검증 결과를 기록하고 사용자 확인을 받는다.

### 단계 3: 이동 중 반응 사격

**Files:** Modify `Scripts/Combat/{OverwatchSystem,CombatSystem,AttackPreview}.cs`, `Scripts/Unit/UnitMovement.cs`, `Scripts/Turn/TurnManager.cs`, 필요 시 `Scripts/AI/EnemyAIController.cs`. Create `Tests/EditMode/OverwatchCombatTests.cs`, `Tests/PlayMode/OverwatchMovementTests.cs`.

**Interfaces:**

- `OverwatchSystem.ResolveAfterStep(BattleUnit mover) : IEnumerator`는 UnitMovement가 칸 완료 후 기다리는 진입점이다. 등록된 상대 진영의 실제 이동 중 유닛만 허용한다.
- `OverwatchSystem.IsResolving : bool`, internal `CanResolveShot(BattleUnit watcher, BattleUnit target, WeaponSlot slot) : bool`. 현재 처리 중 이동 유닛과 현재 사수에 한해서만 승인한다.
- `CombatSystem.ConfigureOverwatch(OverwatchSystem system) : void`, internal `TryOverwatchAttack(BattleUnit attacker, BattleUnit target, WeaponSlot slot) : AttackResult`.
- `AttackPreview.ReactionMultiplier : float`를 추가한다. 일반/반응 경로는 명중률 계산과 피해 실행 함수를 공유하며, 반응 실행은 유효한 시스템 요청만 허용한다.

- [x] `OverwatchCombatTests`와 실제 이동의 `OverwatchMovementTests`에 자기 턴 밖 일반 사격 실패 유지, 경계 반응만 허용, 사격 시 추가 AP 소모 없음, 몸통 고정, ×0.75, 빗나감도 1회 소모, 사선 실패 시 경계 유지 테스트를 작성하고 실패를 확인한다.
- [x] CombatSystem을 작은 공통 preview/피해 실행 경로로 분리한다. 반응 경로에서 일반 CanAct/AP 조건만 무작정 우회하지 말고 `CanResolveShot`을 필수 검증한다. 발사 직전 경계 1회를 원자적으로 소모해 재진입을 막는다. 명중/피해 난수 주입은 기존 메서드를 재사용한다.
- [x] `ResolveAfterStep`은 유닛 등록 순서로 한 명씩 처리한다. 각 사격 전 개인 시야·상태·무기·거리·부채꼴·사선·현재 전투 상태를 재검증한다. 일반 공격이나 시야 이벤트만으로 호출하지 않는다.
- [x] 이동 코루틴에서 그리드 점유·AP 1 소모·이동 기록 갱신 뒤 반응을 기다린다. 계속 이동 전 Health.CanFight, 전투 종료, DamagedMovementRange와 이미 이동한 칸 수, 다음 칸/AP 조건을 재검사한다. 이미 지불한 이동 AP는 환급하지 않는다.
- [x] 반응 중에는 새 입력과 턴 종료를 막고, 이동 취소·OnDisable·전투 종료 때 반응 잠금과 활성 처리 참조를 해제한다. AI도 반응 후 행동 가능 상태를 다시 확인한다.
- [x] PlayMode에서 실제 이동 경로 첫 유효 칸의 사격→생존 후 재개를 검증한다. 시작 시 즉발 없음, 영역 안에서 시작한 적 이동 발동, 자기 팀 이동/회전/차폐/개인 시야 밖 제외를 검증한다.
- [x] Critical/Destroyed로 중단, 양 다리 손상 시 다음 칸 진입 금지, 그리드·transform 일치와 이동 AP 정확성을 검증한다. 몸통 사격만으로는 다리를 파괴할 수 없으므로 다리 검증은 반응 완료 직후 상태 재검사 fixture에서 손상을 주입하고 실제 몸통 피해가 다리를 파괴했다고 가정하지 않는다.
- [x] 여러 사수 중 첫 사수가 적을 파괴하면 나머지는 경계 유지, Critical이면 다음 사수도 사격 가능을 검증한다. 전투 종료/이동 비활성화 도중 정리 후 잠금이 남지 않는 PlayMode 테스트를 통과시킨다.
- [x] 해당 Edit/PlayMode와 기존 이동·턴·AI·공격 수명 테스트를 실행하고 사용자 확인을 받는다.

### 단계 4: 플레이어 경계 조작과 범위 표시

**Files:** Modify `Scripts/Input/PlayerInputController.cs`, `Scripts/Presentation/{UnitActionUI,GridVisualizer,BattleDebugUI}.cs`, `Scripts/Turn/TurnManager.cs`의 읽기 전용 서비스 getter. Create `Tests/PlayMode/OverwatchInputTests.cs`.

**Interfaces:**

- `UnitActionMode`에 `OverwatchWeapon`, `OverwatchFacing`을 추가한다. 기존 `Facing`은 이동 후 방향 선택 전용으로 유지한다.
- `PlayerInputController.OpenOverwatchMenu() : bool`, `CanBeginOverwatch(WeaponSlot slot) : bool`, `BeginOverwatch(WeaponSlot slot) : bool`, `TryChooseOverwatchFacing(FacingDirection facing) : bool`, `TryConfirmOverwatch() : bool`.
- 읽기 전용 `SelectedOverwatchFacing : FacingDirection`, `CanOpenOverwatchMenu : bool`, `CanConfirmOverwatch : bool`.
- 읽기 전용 `SelectedOverwatchSlot : WeaponSlot?`, `GetOverwatchWeaponSlots() : IReadOnlyList<WeaponSlot>`, `TurnManager.Overwatch : OverwatchSystem`, `UnitActionUI.UnitStateText : string`.
- `UnitActionUI.OverwatchButtonRect : Rect` 및 기존 무기/방향 버튼 사각형 접근 방식을 확장해 실제 화면 클릭 테스트에 사용한다.

- [x] 실제 Input System 클릭 테스트로 `Overwatch → ranged 무기 → 4방향 → Confirm / AP 2`를 검증한다. AP가 2일 때 일반 공격 비용 5인 무기도 경계 목록에서 활성화됨을 확인한다.
- [x] melee 무기 숨김, 양손 무기 한 번 표시, 원거리 한 개도 선택 요구, AP 부족 비활성화를 검증하고 예상 실패를 확인한다.
- [x] UI와 입력 상태를 구현한다. 원거리 필터와 기존 무기 이름 표시 코드를 재사용하고 일반 공격 메뉴 상태와 분리한다. 확정 전만 coverage를 갱신하며 body targeting UI는 열지 않는다.
- [x] 감시 칸 표시는 `GetCoverageCells` 결과로 만들고 기존 이동/공격 범위와 모드 전환 시 정리한다. 확정된 상태에는 `OVERWATCH`를 표시한다. 공격·이동 버튼은 비활성화하되 잔여 AP가 있으면 Eject는 활성화한다.
- [x] ESC 준비 취소/선택 해제 시 AP 불변, 확정 후 ESC 선택 해제해도 경계 유지, 설정 도중 다른 유닛 선택/턴 변경 시 미확정 설정 정리 테스트를 추가한다.
- [x] 기존 이동 후 방향 선택·일반 무기 메뉴·긴급탈출·카메라 클릭 입력 테스트와 새 PlayMode 입력 테스트를 통과시킨다.
- [x] 변경 검토·검증 결과를 기록하고 사용자 확인을 받는다.

### 단계 5: 샘플 씬 통합 검증과 사용 안내

**Files:** Create `Tests/PlayMode/OverwatchSceneTests.cs`, `docs/overwatch-guide.ko.md`. Update `docs/current-player-turn-actions.ko.md`, `docs/current-code-architecture.ko.md`의 해당 흐름만 수정한다. 기존 씬과 에셋 파일은 수정하지 않는다.

- [x] 실제 `1V1Test`와 `SqurdWeaponTest`를 읽어 런타임 초기화, 경계 서비스·유닛 상태 연결, 입력 메뉴 접근을 검증하는 테스트를 작성한다. 사용자 배치 좌표나 이동 속도 값에 대한 고정 assertion을 넣지 않는다.
- [x] 임시 테스트 씬/fixture에서 실제 기존 적 AI가 이동하다 경계사격을 받고, 생존 시 자신의 행동을 계속하며 다음 아군 턴이 정상 시작됨을 검증한다. 실제 저장된 씬에 경계사격을 위해 장애물이나 유닛을 옮겨 저장하지 않는다.
- [x] 비활성화/재활성화, 전투 종료 직전 다중 사격, 무기 변경 시 낡은 참조로 발사하지 않음을 통합 검증한다. 결함이 있으면 재현 테스트를 먼저 추가하고 해당 구현만 수정한다.
- [x] 사용 안내에 AP 2, ×0.75, 90도, 몸통 1회, 개인 시야, 다음 자기 턴 만료, 일반 행동 제한/탈출 허용을 적는다. 기동 회피 제외와 파일럿 능력치 미연결 상태도 반영한다.
- [x] 전체 테스트를 아래 명령으로 실행한다. 두 결과 모두 실패/건너뜀 0인지 확인하고 변경과 관련 없는 실패도 이름과 원인을 보고한다.

```powershell
.\tools\Run-UnityTests.ps1 -Mode EditMode -Name overwatch-final-edit
.\tools\Run-UnityTests.ps1 -Mode PlayMode -Graphics -Name overwatch-final-play
```

- [x] 새 .meta와 결과를 원본에 보존하고, 보호 대상 씬·프리팹·무기/기체 데이터·카메라/렌더 설정의 해시가 변경 전과 같은지 확인한다. 명시적으로 수정한 스크립트·테스트·문서만 변경 목록에 포함한다.
- [x] requesting-code-review 절차로 독립 최종 검토를 한 번 수행한다. 확정 설계/계획/실제 변경 파일/테스트 결과를 전달하고 필요한 수정 후 관련 테스트를 다시 실행한다. 검증 결과와 현재 제한을 기록해 사용자에게 완료를 보고한다.

## 자체 검토와 인계

설계의 조작, 감시/발동, 사격/만료, 기동 회피 제거, 책임 분리, 검증 요구를 단계 1~5에 대응시켰다. Review Focus 5개는 각 담당 단계에 테스트를 배치했다. 코드 실행이나 Unity 테스트는 계획 작성 단계에서 수행하지 않았다.

설계와 구현계획은 사용자 승인 상태다. 단계별 사용자 확인을 거쳐 1~5단계 구현과 검증을 완료했다. 최종 EditMode 460/460, 그래픽 PlayMode 267/267 통과(실패·건너뜀 0). 독립 코드 검토에서 수정이 필요한 문제는 발견되지 않았다. 실행 근거와 검증 결과는 `Artifacts/overwatch-progress.md`에 기록했다.
