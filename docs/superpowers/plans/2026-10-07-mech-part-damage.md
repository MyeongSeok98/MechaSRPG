# 부위 HP, 전투불능과 긴급탈출 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 전체 HP를 부위별 HP로 교체하고, CRITICAL·완전파괴·AP 1 긴급탈출을 기존 소대 전투에 연결한다.

**Architecture:** UnitHealth의 내부를 부위 HP와 기체 상태로 교체하며 Script GUID와 기존 프리팹 참조를 유지한다. CombatSystem이 피해 부위를 결정하고 BattleUnit·TurnManager가 행동 권한, 점유와 전투 종료를 관리한다. 기존 조준·행동 UI를 확장하며 파일럿 시스템이나 새 부스트 시스템은 만들지 않는다.

**Tech Stack:** Unity 6000.5.5f1, C#, URP, Input System, Unity Test Framework/NUnit. 외부 패키지 추가 없음.

**Spec:** [승인된 설계](../specs/2026-10-07-mech-part-damage-design.md)

## Global Constraints

- HP: Body/Torso 20, Cockpit 6, Head 6, LeftArm/RightArm 각각 8, LeftLeg/RightLeg 각각 10. 전체 HP나 합계 체력을 별도로 관리하지 않는다.
- 원거리 패널티: Whole/Torso 0%p, Cockpit 45%p, Head 15%p, 양팔·양다리 각각 10%p. 기존 엄폐·회피 계산 이후 차감하고 최소 0%로 제한한다.
- 원거리 피해 5/명중률 80%/사거리 7/AP 3, 근접 Body 고정 피해 8/명중률 95%/사거리 2/AP 3. 엄폐·회피·사거리의 기존 규칙을 유지한다.
- 주요 부위는 머리·양팔·양다리 5개다. 서로 다른 3개 파괴 시 CRITICAL 진입 후 25% 완전파괴 판정, CRITICAL 추가 명중마다 재판정, 4개 이상 또는 Body/Cockpit 0이면 즉시 DESTROYED.
- CRITICAL은 선택·피격·점유·AP 회복이 가능하지만 이동·공격은 불가능하다. AP 1로 탈출하며 ACTIVE에서도 탈출할 수 있다.
- DESTROYED/EJECTED는 전투·점유에서 제외하되 GameObject와 결과 상태를 전투 종료까지 보존한다. 파일럿 사망은 추정하지 않는다.
- ACTIVE/CRITICAL이 한 기라도 남은 진영은 전투에 남는다. 마지막 CRITICAL 아군도 탈출 기회를 갖는다. 적 AI의 자동 탈출은 구현하지 않는다.
- 사용자 맵 크기·배치·카메라 설정·movement speed 12f·원본 PNG를 보존한다. UnitMovement.cs와 그 meta는 변경하지 않는다.
- 사용자 배치 좌표와 이동 속도를 고정값으로 검사하거나 테스트 편의를 위해 수정하지 않는다. authored 씬/프리팹을 재생성·저장하지 않는다.
- Unity 원본 편집기가 열려 있으면 기존 `Artifacts/MeleeRangedTestProject`를 검증에 재사용한다. 한 프로젝트의 Unity 테스트를 동시에 실행하지 않는다.
- 기존 Native 요청을 유지한다. `executing-plans`로 직접 구현하며 각 Task의 검증 결과를 보고하고 사용자의 다음 단계 확인을 받는다. 마지막에는 별도 코드 리뷰를 수행한다.
- 현재 Git 저장소가 없으므로 초기화·커밋·브랜치·worktree는 만들지 않는다. Task별 변경·RED/GREEN·보존 검증을 `Artifacts/mech-part-damage-progress.md`에 기록한다.

## Review Focus

1. 무효 부위, 음수/0 피해, 반복된 파괴 명령은 AP·HP·난수를 바꾸지 않아야 한다. Task 1·2.
2. CRITICAL 명중이 4번째 부위 또는 치명 부위를 파괴할 때 확률 판정을 중복하지 않아야 한다. Task 1·2.
3. 이동·AI 코루틴 도중 상태 전환으로 AP를 환급하거나 다음 적을 중복 실행하지 않아야 한다. Task 3.
4. AP 0인 마지막 CRITICAL 아군도 다음 턴에 탈출할 수 있어야 하며, 양쪽 CRITICAL만 남아도 턴을 진행할 수 있어야 한다. Task 3.
5. 오래된 씬의 UnitHealth 직렬화 값과 화면 오른쪽 UI, 상태 변경 중 선택이 정상적으로 연결되어야 한다. Task 4·5.

## 파일과 책임

모든 런타임 namespace는 `MechaXCOM`, 테스트는 `MechaXCOM.Tests`다. 부위 enum을 새로 복제하지 않는다.

| 파일 | 책임 |
|---|---|
| 새 Scripts/Unit/MechState.cs | Active, Critical, Destroyed, Ejected 상태 enum |
| 새 Scripts/Unit/PartDamageResult.cs | 단일 부위 피해와 상태/파괴 판정 결과 |
| Scripts/Unit/UnitHealth.cs | 부위 HP, 파괴 개수, 25% 판정, 최종 상태 변경 이벤트 |
| Scripts/Combat/TargetBodyPart.cs | Whole→Torso 정규화, 원거리 콕피트 패널티 45 |
| Scripts/Combat/CombatSystem.cs, AttackPreview.cs, AttackResult.cs | 피해 부위 결정, 미리보기/사격 결과 |
| Scripts/Unit/BattleUnit.cs | 상태별 권한, 이동 취소, 탈출, 점유·표현 처리 |
| Scripts/Turn/TurnManager.cs, Scripts/AI/EnemyAIController.cs | ACTIVE 행동, CRITICAL 턴·탈출 기회, 승패 |
| Scripts/Input/PlayerInputController.cs | CRITICAL 선택, 행동 차단, 탈출 입력 |
| Scripts/Presentation/UnitActionUI.cs, BodyTargetUI.cs, BattleDebugUI.cs, GridVisualizer.cs | 상태·부위 HP·Eject·선택/행동 표시 |
| Editor/BattleTestSceneBuilder.cs 및 기존 Tests | 기존 전체 HP API 의존을 부위 API로 이전 |

위 파일 경로는 `Assets/_Game/` 아래다. Resources/BodyTargetArt.asset, 이미지, 씬·프리팹에는 변경이 필요 없다.

## 검증 실행 방식

아래 명령의 작업 디렉터리는 원본을 단독 실행할 수 있을 때 프로젝트 루트, 편집기가 열려 있으면 `Artifacts/MeleeRangedTestProject`다.
후자의 경우 실행 전에 원본 Assets를 복사하며 생성 테스트 씬 폴더가 남아 있지 않은지 확인한다.
명령 결과는 프로세스 exit 0과 XML의 Failed 0을 함께 확인한다. RED는 컴파일 오류가 아니라 기능 부재 또는 틀린 행동으로 실패해야 한다.
새 API가 아직 없으면 첫 RED만 reflection으로 호출해 의도한 assertion 실패를 확인하고, GREEN 이후 직접 호출로 교체한다.

```powershell
Copy-Item Assets/* Artifacts/MeleeRangedTestProject/Assets -Recurse -Force
```

기존 asset meta를 덮어쓰거나 GUID를 재발급하지 않는다. 새 코드/테스트의 meta만 원본과 검증 사본에서 일치시킨다.

---

### Task 1: 전체 HP를 부위 HP와 기체 상태로 교체

**Files:**
- Create: `Assets/_Game/Scripts/Unit/MechState.cs`, `PartDamageResult.cs`
- Modify: `Assets/_Game/Scripts/Unit/UnitHealth.cs`, `Assets/_Game/Scripts/Combat/TargetBodyPart.cs`
- Test: 새 `Assets/_Game/Tests/EditMode/MechPartHealthTests.cs`, `PartHealthSerializationTests.cs`; 기존 `UnitStatsTests.cs`

**Interfaces:**
- `MechState { Active, Critical, Destroyed, Ejected }`. UI에서 대문자로 표시한다.
- `BodyTargetRules.Normalize(TargetBodyPart part)`: Whole을 Torso로 바꾸고 나머지 유효 enum은 그대로 반환한다. enum의 기존 숫자 순서를 유지한다.
- `UnitHealth.GetCurrentHP(TargetBodyPart part)`, `GetMaxHP(TargetBodyPart part)` → `int`.
- `UnitHealth.State` → `MechState`, `IsPresent` → Active/Critical 여부, `CanFight` → Active 여부, `MajorPartsDestroyed` → 서로 다른 주요 부위 파괴 개수, `PilotEjected` → 탈출 기록.
- `UnitHealth.Initialize()`, `ConfigurePartHP(TargetBodyPart part, int maximum)`, `ConfigureDestructionChance(int percent)`, `SetDestructionRollSource(Func<int> source)`.
- `UnitHealth.ApplyDamage(TargetBodyPart part, int damage)` → `PartDamageResult`; `public bool UnitHealth.MarkEjected()`. 실제 게임 탈출 명령은 BattleUnit.TryEject를 통해 AP·턴 검증 후 호출한다.
- `PartDamageResult`: `bool Applied`, `TargetBodyPart Part`, `int DamageDealt`, `int PartHPAfter`, `MechState StateBefore`, `MechState StateAfter`, `bool DestructionChecked`.
- `event Action UnitHealth.StateChanged`: HP 처리와 파괴 판정을 모두 마친 최종 상태가 바뀔 때 한 번만 발생한다.

- [x] **Step 1: 실패 테스트 작성.** 새 fixture는 GameObject에 UnitHealth를 추가하고 Initialize하며 teardown에서 DestroyImmediate한다. 기본 HP 7개를 parameterized case로 검증한다. 다음 테스트를 포함한다.

```csharp
// ThreeDestroyedPartsCheckOnceAtThe25PercentBoundary: roll 24 → Destroyed, 25 → Critical.
int checks = 0;
health.SetDestructionRollSource(() => { checks++; return 25; });
health.ApplyDamage(TargetBodyPart.Head, 6);
health.ApplyDamage(TargetBodyPart.LeftArm, 8);
Assert.That(health.State, Is.EqualTo(MechState.Active));
health.ApplyDamage(TargetBodyPart.RightArm, 8);
Assert.That(health.State, Is.EqualTo(MechState.Critical));
Assert.That(checks, Is.EqualTo(1));
health.ApplyDamage(TargetBodyPart.Head, 5); // Already broken, but a CRITICAL hit.
Assert.That(health.MajorPartsDestroyed, Is.EqualTo(3));
Assert.That(checks, Is.EqualTo(2));
```

`FourthPartAndVitalDestructionSkipRandomCheck`: 3개 파괴 후 생존시킨 fixture에서 LeftLeg 10 또는 Body 20 또는 Cockpit 6 피해 → Destroyed, 추가 판정 횟수 0.
`DamageStaysOnOnePartWithoutOverflow`: Head에 100 피해 → Head 0, Body 20, 다른 부위 원래 HP 유지.
`InvalidDamageDoesNotChangePartsStateOrRandomness`: 0/음수 피해, `(TargetBodyPart)999`, Destroyed/Ejected 대상 → Applied false, 난수·상태 이벤트 0.
`MajorPartCountNeverDoubleCounts`와 `DamageEmitsOnlyTheFinalStateChange`로 중복 파괴 및 한 명중의 중복 이벤트를 검증한다.
`LegacyMaxHealthBecomesBodyMaximum`은 테스트 소유 prefab의 Body 최대 HP 37을 이전 `maxHealth: 37` YAML로 바꿔 재임포트한 뒤 Body 최대 HP=37, Cockpit=6인지 검사한다. 사용자 prefab은 수정하지 않는다.

- [x] **Step 2: RED 확인.** `.\tools\Run-UnityTests.ps1 -Mode EditMode -Name part-health-red -Filter 'MechPartHealthTests;PartHealthSerializationTests'` → 부위/상태 기능 부재 assertion 실패.
- [x] **Step 3: 최소 구현.** 최대 HP는 SerializeField로 7개를 관리하고 초기 현재 HP도 7개만 유지한다. 기존 `maxHealth`는 `[FormerlySerializedAs("maxHealth")]`를 붙인 `bodyMaxHP` 필드로 이전한다. UnitHealth.cs.meta를 보존한다. 치명 부위 → 4개 파괴 → CRITICAL 진입/재명중 순서로 판정하며 확률은 초기 25다. 무효 입력은 판정 전에 거절한다.
- [x] **Step 4: GREEN 및 기존 fixture 연결.** 같은 필터를 `part-health-green`으로 실행해 Failed 0을 확인한다. 다음 Task까지 컴파일을 유지할 임시 API가 필요하면 기존 CurrentHealth/MaxHealth/Configure/ApplyDamage(int)/IsAlive/Died를 Body 또는 상태를 읽는 어댑터로만 둔다. 별도 전체 HP 필드는 만들지 않는다. 제거 시점은 Task 5다.
- [x] **Step 5: 기록·확인.** 부위 HP, 24/25 경계, 중복 판정 결과를 기록하고 사용자에게 Task 1 결과를 보고한다.

### Task 2: 원거리 부위 피해와 근접 Body 피해 연결

**Files:**
- Modify: `Assets/_Game/Scripts/Combat/CombatSystem.cs`, `AttackPreview.cs`, `AttackResult.cs`, `TargetBodyPart.cs`
- Modify: `Assets/_Game/Scripts/Presentation/BattleDebugUI.cs`의 공격 결과 표시
- Test: 새 `Assets/_Game/Tests/EditMode/PartAttackTests.cs`; 기존 `BodyTargetCombatTests.cs`, `MeleeCombatTests.cs`, `CombatSystemTests.cs`

**Interfaces:**
- 기존 `GetPreview(BattleUnit attacker, BattleUnit target, AttackType type = Ranged, TargetBodyPart part = Whole)`와 `TryAttack(...)` 호출 형태를 유지한다.
- 근접은 받은 part와 무관하게 Torso, 원거리 Whole은 Torso, 나머지 원거리는 선택한 부위다.
- `AttackPreview.TargetPart`, `PartHP`, `PartMaxHP`, `TargetState`를 추가한다.
- `AttackResult.TargetPart`, `TargetPartHPAfter`, `TargetStateAfter`, `DestructionChecked`, `TargetDestroyed`를 추가한다. `TargetDestroyed`는 파일럿 사망을 뜻하지 않는다. 이전 TargetHealthAfter/TargetKilled는 필요 시 새 결과를 읽는 임시 속성으로만 유지하고 Task 5에서 제거한다.
- Task 1의 ApplyDamage 결과를 사용하며 명중 난수는 기존 CombatSystem, 완전파괴 난수는 UnitHealth에 분리한다.

- [x] **Step 1: 실패 테스트 작성.** fixture에서 player/enemy를 2칸 간격에 등록한다. 다음 계약을 검증한다.

```csharp
// CockpitTwoRangedHitsDestroyItWithoutDamagingBody
Assert.That(combat.GetPreview(player, enemy, AttackType.Ranged, TargetBodyPart.Cockpit).HitChance, Is.EqualTo(35));
combat.SetRollSource(() => 0);
combat.TryAttack(player, enemy, AttackType.Ranged, TargetBodyPart.Cockpit);
Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Cockpit), Is.EqualTo(1));
Assert.That(enemy.Health.GetCurrentHP(TargetBodyPart.Torso), Is.EqualTo(20));
player.ActionPoints.ResetForTurn();
var second = combat.TryAttack(player, enemy, AttackType.Ranged, TargetBodyPart.Cockpit);
Assert.That(second.TargetPartHPAfter, Is.Zero);
Assert.That(second.TargetStateAfter, Is.EqualTo(MechState.Destroyed));
Assert.That(second.DestructionChecked, Is.False);
```

`MeleeAlwaysDamagesBodyDespiteRequestedHead`: GetPreview/TryAttack에 Melee와 Head를 전달해도 TargetPart=Torso, Body=12, Head=6, HitChance=95, AP=2인지 검증한다.
`CockpitCoverEvasionAndRollBoundary`: Full Cover에서 5%, 기동 회피 5를 더하면 0%. 평지에서 roll 34는 명중하고 35는 빗나간다.
`CriticalMissDoesNotRollDestruction`과 `CriticalHitToBrokenPartRollsOnce`: AP 소모, 판정 횟수, 상태, 부위 HP를 검증한다.
`InvalidPartAttackDoesNotSpendAPOrRoll`: `(TargetBodyPart)999`의 원거리 요청을 거절하고 AP/난수를 보존한다.

- [x] **Step 2: RED 확인.** `.\tools\Run-UnityTests.ps1 -Mode EditMode -Name part-attack-red -Filter 'PartAttackTests;BodyTargetCombatTests;MeleeCombatTests'` → 부위 피해/45%p 기대값 실패.
- [x] **Step 3: 최소 구현.** Cockpit 패널티를 45로 변경한다. GetPreview에서 실제 피해 부위를 결정하고 무효 부위는 AP 지출 전에 거절한다. 명중한 경우에만 ApplyDamage를 호출한다. 결과에 최종 부위 HP·상태·실제 피해를 전달한다. 총 HP 기반 결과 이름은 새 필드로 이전한다.
- [x] **Step 4: GREEN 확인.** 같은 필터를 `part-attack-green`으로 실행한다. 기존 테스트에서 원거리 Whole/근접 피해의 기대값은 Body HP로, 콕피트 60% 기대값은 35%로 바꾼다. 디버그 공격 결과도 새 필드로 이전해 컴파일을 유지한다.
- [x] **Step 5: 기록·확인.** 콕피트 2회 명중, 근접 Body 8 피해, 표시/실제 확률 일치를 보고한다.

### Task 3: 상태별 행동, 긴급탈출과 진영 턴 연결

**Files:**
- Modify: `Assets/_Game/Scripts/Unit/BattleUnit.cs`, `Assets/_Game/Scripts/Turn/TurnManager.cs`, `Assets/_Game/Scripts/AI/EnemyAIController.cs`
- Test: 새 `Assets/_Game/Tests/PlayMode/MechStateTurnTests.cs`, `EmergencyEjectionTests.cs`; 기존 `TurnTests.cs`, `CombatLifecycleTests.cs`, `EnemyAITests.cs`, `FactionTurnTests.cs`

**Interfaces:**
- `BattleUnit.IsAlive`는 초기화된 Active/Critical을 뜻한다. `CanTakeAction`은 Active와 자기 턴 권한을 함께 검사한다.
- `BattleUnit.CanEject` → `bool`, `BattleUnit.TryEject()` → `bool`. 자기 진영 턴, 등록 상태, AP·컴포넌트 유효성, 이동/전투 종료 여부를 검사한다.
- `TurnManager.CanAct(BattleUnit unit)`는 Active만 허용한다. `CanEject(BattleUnit unit)`는 Active/Critical을 허용한다. 공통 진영·등록·이동 중 조건을 중복 없이 공유한다.
- TurnManager의 탈출 검증은 UnitHealth 상태를 직접 확인하며 BattleUnit.CanEject를 다시 호출하지 않는다. 권한 getter 사이의 순환 호출을 만들지 않는다.
- UnitHealth.StateChanged는 기존 Died 구독을 대체한다. BattleUnit은 Critical에서 CancelMove, Destroyed/Ejected에서 CancelMove·점유 해제·Collider/Renderer 비활성화를 수행한다.
- 턴 시작 AP·회피 회복 및 승패의 생존 판정에는 IsPresent를 사용한다. AI 실행 actor는 CanFight, 공격 target은 IsPresent다.

- [x] **Step 1: 실패 PlayMode 테스트 작성.** 새 fixture는 EnemyAITestBattle의 Units/AP/AI 구조를 재사용하며 파괴 난수를 99로 고정해 Critical을 재현한다.

```csharp
// CriticalUnitCanEjectButCannotMoveOrAttack
player.Health.SetDestructionRollSource(() => 99);
player.Health.ApplyDamage(TargetBodyPart.Head, 6);
player.Health.ApplyDamage(TargetBodyPart.LeftArm, 8);
player.Health.ApplyDamage(TargetBodyPart.RightArm, 8);
Assert.That(turns.IsBattleOver, Is.False);
Assert.That(turns.CanAct(player), Is.False);
Assert.That(player.Movement.TryMove(new Vector2Int(2, 1)), Is.False);
Assert.That(combat.TryAttack(player, enemy).WasExecuted, Is.False);
Assert.That(player.TryEject(), Is.True);
Assert.That(player.ActionPoints.CurrentPoints, Is.EqualTo(4));
Assert.That(player.Health.State, Is.EqualTo(MechState.Ejected));
Assert.That(player.Health.PilotEjected, Is.True);
Assert.That(player.TryEject(), Is.False);
```

`ZeroAPLastCriticalUnitCanEjectNextPlayerTurn`: 양쪽을 Critical로 만들고 플레이어 AP 0 → 탈출 거절 → 턴 종료 → Critical 적 무행동 → 아군 AP 회복 → 탈출 가능·패배 판정.
`ActiveUnitCanEjectAndCannotBeRetargeted`: ACTIVE 선제 탈출, 셀 점유 해제, 모델/Collider 비활성화, 같은 기체 AP 중복 지출 없음.
`CriticalDuringMovementCancelsAtItsRegisteredCell`: 첫 칸 도착 뒤 Critical 진입 → 기존 CancelMove로 중단, 도착 셀과 실제 위치 일치, 추가 AP 지출/환급 없음. 속도 값을 설정하거나 검사하지 않는다.
`StateChangeDuringEnemyTurnDoesNotRepeatOrSkipNextActiveEnemy`: 첫 적 상태를 변경하고 다음 ACTIVE 적의 행동이 정확히 한 번 발생하는지 실제 HP/AP 결과로 검증한다.
`DestroyedAndEjectedStateRemainQueryableAfterRemoval`: 비활성 표현 이후에도 기체 상태·탈출 기록을 조회할 수 있다.

- [x] **Step 2: RED 확인.** `.\tools\Run-UnityTests.ps1 -Mode PlayMode -Name mech-state-turn-red -Filter 'MechStateTurnTests;EmergencyEjectionTests'` → Critical 행동/탈출/승패 실패.
- [x] **Step 3: 최소 구현.** Task 1의 상태 이벤트를 연결한다. 탈출 검증 뒤 AP 1을 한 번 소비하고 MarkEjected를 호출한다. 현재 HandleDeath의 즉시 Destroy를 제거하고 객체를 보존한다. AI는 Critical actor를 건너뛰되 Critical target은 공격 가능하다. 모든 기체가 Destroyed/Ejected인 진영만 전투 종료로 처리한다. UnitMovement.cs는 수정하지 않는다.
- [x] **Step 4: GREEN 및 회귀.** 같은 새 필터와 `EnemyAITests;FactionTurnTests;CombatLifecycleTests`를 실행한다. 기존 삭제 여부 검사는 새 상태/표현/점유 계약으로 바꾸되 이동·순차 AI·AP 관련 assertion을 약화하지 않는다.
- [x] **Step 5: 기록·확인.** ACTIVE/CRITICAL 탈출, AP 0 다음 턴, 마지막 기체와 이동 중 상태 전환 결과를 보고한다.

### Task 4: 부위 HP·상태·Eject UI와 입력 연결

**Files:**
- Modify: `Assets/_Game/Scripts/Input/PlayerInputController.cs`
- Modify: `Assets/_Game/Scripts/Presentation/UnitActionUI.cs`, `BodyTargetUI.cs`, `BattleDebugUI.cs`, `GridVisualizer.cs`
- Test: 새 `Assets/_Game/Tests/PlayMode/EjectionInputTests.cs`; 기존 `UnitActionMenuTests.cs`, `EnemyTurnInputTests.cs`, `EnemyActionIndicatorTests.cs`

**Interfaces:**
- `PlayerInputController.CanManageSelectedUnit` → 자기 턴의 선택 가능한 ACTIVE/CRITICAL 상태; `CanChooseAction`은 기존 이름을 유지하며 ACTIVE 행동만 허용한다.
- `PlayerInputController.CanEjectSelected`, `TryEjectSelected()` → `bool`. 실제 권한은 Task 3의 BattleUnit에 위임한다.
- `UnitActionUI.EjectButtonRect` → `Rect`. 기본 메뉴는 Move/Attack/Eject의 3행이다. target 정보/Fire 행은 겹치지 않도록 한 행 아래로 이동한다.
- BodyTargetUI는 7개 부위 HP를 간결한 표로 표시하고 선택한 부위의 명중률과 TargetState를 표시한다. Melee에는 표시하지 않는다. 그림과 Fire 버튼을 포함한 패널은 화면 안에 들어가도록 기존 Scale 규칙으로 배치한다.
- BattleDebugUI는 선택한 아군의 7개 부위 HP와 상태를 표시하고 사격 결과는 해당 부위 HP/상태로 설명한다. 전체 HP를 대신하는 합계 숫자는 표시하지 않는다.
- 공격 결과, 선택 디버그 정보와 EJECTED 피드백에서 전체 HP 또는 파일럿 사망으로 오해되는 문구를 제거한다.

- [x] **Step 1: 실제 InputSystem 클릭 실패 테스트 작성.** Test helper는 기존 UnitActionMenuTests의 Mouse/Keyboard 입력 설정을 사용한다. Critical unit을 클릭하면 선택되고 Move/Attack은 거절되며 Eject가 활성화된다. EjectButtonRect 중앙을 실제 클릭한 뒤 State=Ejected, AP=4, 선택/메뉴/범위 해제, 셀 점유 해제를 검사한다.
  `ZeroAPEjectionButtonCannotSpendOrRemoveUnit`, `EscapeClosesCriticalSelection`, `EnemyTurnCannotEjectSelectedUnit`로 거절 경로를 확인한다.
  `BodyTargetPanelReportsOnlySelectedPartHP`는 Cockpit 조준 후 HP=6/6, 첫 명중 후 1/6과 Body=20을 확인한다.
  `RightEdgeTargetPanelKeepsMoveAttackEjectReachable`는 오른쪽 화면에서 메뉴와 조준 창이 겹치지 않고 실제 버튼 클릭이 동작하는지 검증한다.
  `SelectionAndIndicatorClearAfterExternalDestructionOrEjection`으로 선택 중 외부 상태 전환을 확인한다.
- [x] **Step 2: RED 확인.** `.\tools\Run-UnityTests.ps1 -Mode PlayMode -Name ejection-ui-red -Filter 'EjectionInputTests;UnitActionMenuTests'` → Eject UI/새 부위 HP 기대값 실패.
- [x] **Step 3: 최소 구현.** 선택 가능과 전투 행동 가능을 분리한다. ACTIVE/CRITICAL 메뉴는 보이되 버튼별 권한을 검사한다. 클릭은 기존 InputSystem에서 한 번만 처리하고 맵 raycast 전에 UI를 소비한다. 탈출 성공과 외부 최종 상태 전환에서는 선택·범위·공격 대상·이전 사격 결과를 정리한다. Critical 적을 행동 중인 적으로 표시하지 않는다.
- [x] **Step 4: GREEN 및 UI 회귀.** 새 필터와 `EnemyTurnInputTests;EnemyActionIndicatorTests;UnitCameraFocusTests`를 실행한다. 기존 실제 클릭·화면 경계·ESC·더블클릭·카메라 검사와 AP assertion을 유지한다.
- [x] **Step 5: 기록·확인.** 부위 HP, 상태, Eject UI와 원거리/근접 흐름을 보고한다. Native 육안 검증이 불가능하면 자동화 PlayMode 검증이라고 명시한다.

### Task 5: 임시 전체 HP API 제거, 저장 씬과 전체 통합 검증

**Files:**
- Modify: `Assets/_Game/Scripts/Unit/UnitHealth.cs`의 임시 어댑터 제거
- Modify: `Assets/_Game/Scripts/Combat/AttackResult.cs`의 이전 결과 필드가 남았으면 제거
- Modify: `Assets/_Game/Editor/BattleTestSceneBuilder.cs`의 HP 초기화/검증 호출
- Modify: 기존 `Assets/_Game/Tests/EditMode/*.cs`, `Assets/_Game/Tests/PlayMode/*.cs` 중 전체 HP·Died API를 사용하는 테스트
- Test: Task 1의 `Assets/_Game/Tests/EditMode/PartHealthSerializationTests.cs`; 기존 `SqurdBattleSceneTests.cs`, `SqurdBattlePlayTests.cs`, `SceneIntegrationTests.cs`, `SceneBuilderTests.cs`
- Modify: `docs/battle-skeleton-guide.ko.md`; 검증 기록 `Artifacts/mech-part-damage-progress.md`

**Interfaces:**
- 최종 public HP API는 Task 1의 부위 getter/피해 API다. CurrentHealth, MaxHealth, Configure(int), ApplyDamage(int), UnitHealth.IsAlive, Died는 남기지 않는다. BattleUnit.IsAlive는 Task 3의 Active/Critical 계약으로 유지한다.
- AttackResult의 전체 HP/사망 필드는 TargetPartHPAfter/TargetStateAfter/TargetDestroyed로 이전한다.
- UnitHealth의 기존 Script GUID 및 `[FormerlySerializedAs("maxHealth")]` Body 최대 HP 이전을 유지한다. Resources의 기존 TargetBodyPart 인덱스와 텍스처 순서도 유지한다.

- [x] **Step 1: 저장 자산·통합 검증 추가.** 기존 prefab을 저장 없이 로드해 UnitHealth가 누락되지 않고 Body 최대 HP=20, Cockpit 최대 HP=6으로 이전되는지 검사한다. Task 1의 이전 `maxHealth: 37` 직렬화 fixture를 함께 실행한다. 사용자 prefab은 수정하지 않는다.
  기존 SqurdBattleTest를 저장 없이 PlayMode 로드해 원거리 특정 부위 피해, 근접 Body 피해, Critical 생존, 한 아군 탈출 후 나머지 소대 행동과 승패를 검증한다. 파괴 난수는 fixture에서 결정적으로 주입한다. 실제 시작 좌표나 movement speed를 고정값으로 확인하지 않는다.
- [x] **Step 2: 통합 연결 확인.** `.\tools\Run-UnityTests.ps1 -Mode EditMode -Name part-serialization-integration -Filter PartHealthSerializationTests`와 `-Mode PlayMode -Name part-squad-integration -Filter SqurdBattlePlayTests`를 실행한다. 부위/탈출 기능의 RED는 앞 Task에서 이미 확인했으므로, 통합 테스트를 억지로 실패시키지 않는다. 실패가 드러나면 그 실제 원인에 대한 회귀 테스트를 보존한 뒤 수정한다.
- [x] **Step 3: 이전 API와 테스트 의존 정리.** 기존 일반/근접 공격 HP assertion은 Body HP, 특정 부위 공격은 해당 HP, 제거는 상태·점유 검사로 옮긴다. 공격력/AP/이동 경로 회귀를 유지한다. Scene Builder는 테스트 소유 경로에서만 실행한다. 이전 API 제거 후 `rg -n 'CurrentHealth|MaxHealth|TargetHealthAfter|TargetKilled|\.Died' Assets/_Game`로 남은 호출을 점검하고 컴파일한다. 이 텍스트 검색을 기능 검증으로 간주하지 않는다.
- [x] **Step 4: 최종 전체 검증.** 아래 두 명령을 순차 실행한다.

```powershell
.\tools\Run-UnityTests.ps1 -Mode EditMode -Name mech-part-final-edit
.\tools\Run-UnityTests.ps1 -Mode PlayMode -Name mech-part-final-play
```

모든 테스트 Failed 0과 exit 0을 확인한다. 새 실패를 발견하면 원인을 기록하고 필요한 회귀 테스트와 최소 수정 후 관련 검사/최종 검증을 다시 수행한다.
실행 전 스냅샷과 비교해 authored 씬·프리팹·UnitMovement.cs/meta·원본 PNG가 동일한지 확인한다.
검증 사본과 원본 런타임/테스트/새 meta의 SHA256 일치, GeneratedEnemyAIIntegration 폴더 정리, XML/log 원본 Artifacts 보관을 확인한다.
- [x] **Step 5: 전체 리뷰와 사용자 확인.** 별도 read-only 리뷰어에게 승인 설계, 실제 변경 파일, 테스트 결과를 제공해 상태 판정·중복 난수·탈출 권한·저장 자산을 검토한다. 유효한 지적을 해결하고 필요한 검증을 마친 뒤 최종 변경과 테스트 결과를 보고한다.

## 완료 기준

전체 HP 필드/API 없이 7개 부위 HP만 남고, 승인된 상태 전환과 콕피트 수치가 실제 사격에 적용된다.
Critical 아군은 선택·탈출할 수 있지만 이동·공격할 수 없으며, 마지막 아군과 AP 0에서도 탈출 기회가 보존된다.
기존 맵·속도·카메라·이미지와 전투 조작을 유지한 상태로 저장 씬 및 전체 EditMode/PlayMode 검증이 통과한다.
계획 승인 전에는 제품 코드·자산을 수정하지 않는다. 승인 후 Native 방식으로 Task 1부터 진행한다.
