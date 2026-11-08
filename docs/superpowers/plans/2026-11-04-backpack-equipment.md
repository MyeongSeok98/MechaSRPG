# Backpack Equipment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans inline. Use test-driven-development for implementation and one fresh read-only review at the end. User previously selected Native execution; preserve that choice. No Git commands or commits.

**Goal:** 선택적인 일체형 백팩 무장을 기존 공격·탄약·UI·AI 경로에 연결한다.
**Architecture:** BackpackData를 장착 설정에 연결하고 WeaponSlot.Backpack을 기존 enum 뒤에 추가한다. UnitCombat과 UnitWeaponAmmo를 확장해 손 파지/팔 상태 및 재장전 규칙을 분리하고, 기존 CombatSystem을 재사용한다.
**Tech Stack:** Unity6000.5.5f1, C#, ScriptableObject, NUnit / Unity Test Framework, IMGUI.
**Spec:** docs/superpowers/specs/2026-11-04-backpack-equipment-design.md

## Global Constraints

- 백팩당 무기 최대1개, 무장 없음/백팩 없음 허용. 백팩 무기는 개별 교체 불가.
- 손 미점유. 탄약 무기는 시작 장탄만 사용, 예비0, 재장전 불가.
- 백팩 자체 중량 + 포함 무기 중량을 한 번 합산한다.
- 기존 enum 값과 직렬화 필드를 보존한다. Git/GitHub 작업 금지.
- 기존 씬·프리팹·사용자 에셋/배치·모델·이동속도12f·카메라/줌/그림자·반엄폐×.8/완전엄폐×.6 보존.
- Unity 실행은 기존 Artifacts/MeleeRangedTestProject 복제본만 사용한다.
- 전투 중 장비 교체, 백팩 부위 HP, 자동 모델 부착, 과적 출격 차단, 예비 탄약 중량, 이동력 변경은 제외한다.

## Review Focus

1. WeaponData를 공유하는 손/백팩/다른 유닛의 잔탄이 독립적이며 백팩 규칙이 손 무기 재장전을 막지 않는다.
2. 양팔 파괴 또는 양손 무기 장착 상태에서도 백팩 슬롯을 손으로 잘못 판정하지 않는다.
3. 백팩의 마지막 경계 탄약이 사용된 뒤 대기 중인 이동/결정 처리가 해제된다.
4. 세 번째 무기 버튼의 실제 클릭 영역과 화면 경계가 일치하고 Reload에는 백팩이 없다.
5. 초기화 반복/턴 변경/재활성화가 잔탄을 복구하지 않고 기존 두 손 씬과 에셋을 수정하지 않는다.

## 검증 명령 공통

아래 명령은 모두 `Artifacts/MeleeRangedTestProject`를 작업 디렉터리로 사용한다. 원본을 백업/해시 기록하고 Unity가 종료된 상태에서 Assets를 복제본으로 동기화한다. 실행 결과 XML/log는 원본 Artifacts에 보존한다.

```powershell
.\tools\Run-UnityTests.ps1 -Mode EditMode -Name backpack-edit -Filter 'BackpackDataTests;BackpackLoadoutTests;BackpackCombatTests;BackpackAmmoTests'
.\tools\Run-UnityTests.ps1 -Mode PlayMode -Graphics -Name backpack-play -Filter BackpackPlayTests
```

Expected: 구현 전에는 해당 요구사항의 assertion 실패를 확인한다. 각 단계 구현 후 해당 테스트가 모두 통과해야 한다.

### Task 1: 백팩 정의와 장착 설정

**Files:**
- Create: `Assets/_Game/Scripts/Combat/BackpackData.cs`
- Modify: `Assets/_Game/Scripts/Unit/UnitWeaponLoadout.cs`
- Modify: `Assets/_Game/Editor/UnitWeaponLoadoutEditor.cs`
- Create: `Assets/_Game/Tests/EditMode/BackpackDataTests.cs`
- Create: `Assets/_Game/Tests/EditMode/BackpackLoadoutTests.cs`

**Interfaces:**
- BackpackData: `BackpackId`, `DisplayName`, `Description`, `WeightTonnes`, `Weapon`, `ModelPrefab`, `IsValid(out string reason)`; ScriptableObject, CreateAssetMenu, 한글 Inspector 라벨.
- WeaponSlot: append `Backpack = 2` without changing hand values.
- UnitWeaponLoadout: `BackpackData Backpack`, `WeaponData GetWeapon(WeaponSlot slot)`, `float TotalEquipmentWeightTonnes`, `bool TryConfigureBackpack(BackpackData backpack)`.
- TryConfigureBackpack changes only pre-battle configuration (reject initialized owner); existing Configure(hand fields) preserves the backpack reference.
- TotalEquipmentWeightTonnes counts both hands' equipped weapons plus backpack.WeightTonnes and backpack.Weapon.WeightTonnes; two-handed weapon stored once counts once. A normal backpack contributes only its own weight.

- [x] Write tests `EmptyBackpackAndUnarmedBackpackAreValid`, `BackpackRequiresIdAndFiniteNonnegativeWeight`, `BackpackAssetRoundTripPreservesWeaponReference`, `TwoHandWeaponCanCoexistWithBackpack`, `BackpackWeightCountsIntegratedWeaponOnce`, `BackpackCannotBeChangedAfterInitialization`. Assert empty reference is valid, NaN/Infinity/negative mass invalid, mass1+weapon2=3, hand3+backpack1+integrated2=6, and shared data unchanged.
- [x] Run new data/loadout tests in clone; verify failure for missing behavior rather than setup errors.
- [x] Implement interfaces and Korean Inspector fields; preserve current serialized names and hand validation. No new mech capability flags or inventory system.
- [x] Run data/loadout tests; expected all pass. Record result in Artifacts ledger without Git.

1단계 검증: EditMode 683/683, 그래픽 PlayMode 365/365 통과. 신규 16개 실패→통과 확인. 읽기 전용 검토에서 조치할 사항 없음. 샘플 에셋과 공격/탄약 연결은 이후 단계에 남긴다.

### Task 2: 백팩 공격 경로 연결

**Files:**
- Modify: `Assets/_Game/Scripts/Unit/UnitCombat.cs`
- Modify only if required by shared integration: `Assets/_Game/Scripts/Combat/CombatSystem.cs`
- Create: `Assets/_Game/Tests/EditMode/BackpackCombatTests.cs`
- Create shared test fixture: `Assets/_Game/Tests/Shared/BackpackTestBattle.cs`

**Interfaces:**
- Consume Task1 `GetWeapon(WeaponSlot.Backpack)`.
- Existing `UnitCombat.TryGetAttack(WeaponSlot, out AttackStats, out string)` and `GetAttackSlots(AttackType)` return the backpack when valid; enumerate RightHand, LeftHand, Backpack in that order.
- AttackStats.GripMultiplier is1 for backpack. Only hand slots enforce arm integrity/grip. Shared battle permission/state rules remain mandatory.

- [x] Write `BackpackAppearsAsThirdAttackSlot`, `MissingBackpackHasNoAttackSlot`, `BackpackIgnoresHandGripBonusAndDestroyedArms`, `BackpackAndHandsShareSameFinalHitDamageRules`, `CriticalAndEjectedUnitsCannotAttackWithBackpack`. Keep deterministic RNG and assert actual AP/part HP changes as well as preview.
- [x] Run new combat tests and observe expected failures.
- [x] Implement slot-aware lookup and arm/grip checks. Do not add a second combat service or change armor/crit/pilot formulas or the99% cap.
- [x] Run data/loadout/combat suites; expected all pass, existing hand tests unchanged except explicitly necessary slot enumeration assumptions.

2단계 검증: EditMode697/697, 그래픽 PlayMode365/365 통과. 신규14개 중9개 실패→통과, 기존 부정/빈 슬롯 사례5개는 이미 통과. 제품 변경은 UnitCombat 한 파일. 원거리 백팩은 3단계 잔탄 연결 전이므로 사격 권한을 우회하지 않고 계산된 미리보기 수치와 안전한 차단을 검증했다. 실제 피해/AP/치명타는 기존 무기 타입을 유지하는 근접 백팩으로 검증했다.

### Task 3: 유닛별 잔탄과 재장전 금지

`BackpackCombatTests.RangedBackpackUsesCommonPreviewMathAndActuallyConsumesLoadedAmmo`로 갱신 완료. 2단계 임시 탄약 미연결 차단 검사를 실제 장탄·사격·피해·탄약 소모 검증으로 전환했다.

**Files:**
- Modify: `Assets/_Game/Scripts/Unit/UnitWeaponAmmo.cs`
- Modify: `Assets/_Game/Scripts/Unit/UnitOverwatch.cs`
- Create: `Assets/_Game/Tests/EditMode/BackpackAmmoTests.cs`

**Interfaces:**
- Consume Task1 slots/loadout and Task2 attack API.
- Existing `GetStatus`, `CanFire`, `CanReload`, `TryConsumeShot`, `TryReload` support Backpack. Its status has SpareMagazines0 and ReloadAPCost0; `CanReload`/`TryReload` always false for Backpack regardless of WeaponData spare count.
- Existing hand ammunition behavior and Either-hand transfer remain unchanged.
- UnitOverwatch tracks slot-aware weapon identity; compare hand grip only for hand slots. Backpack uses fixed neutral grip and shared pending-decision checks.

- [x] Write `BackpackStartsLoadedWithoutSpares`, `MissConsumesOneBackpackRound`, `PreviewAndInvalidAttackConsumeNoAmmo`, `BackpackReloadRejectedWithoutSpendingAP`, `SameDefinitionInHandAndBackpackHasIndependentAmmo`, `SharedBackpackDataDoesNotShareRuntimeAmmo`, `TurnReinitializeAndReenableDoNotRefill`, `HandTransferDoesNotStealBackpackAmmo`. Use capacity2, assert after2 executed shots empty; third rejected with no AP/RNG cost; hand still reloadable.
- [x] Run new ammo tests and observe failures before implementation.
- [x] Add separate backpack magazine state and mandatory slot reload rejection. Never modify shared WeaponData ammo fields to enforce backpack policy.
- [x] Run all new EditMode tests plus existing WeaponAmmo/ArmWeaponDamage/Overwatch suites; expected all pass.

3단계 검증: 전체 EditMode709/709 및 그래픽 PlayMode368/368 통과(실패·건너뜀0). 신규 잔탄 테스트12개와 갱신한 원거리 공격 검사, 경계 상태·마지막 탄환 이동 해제·Skip의 PlayMode3개를 실패→통과로 확인했다. 읽기 전용 검토에서 조치할 사항 없음. 공격/재장전 메뉴와 샘플 에셋은 이후 단계에 남긴다.

### Task 4: 실제 메뉴·AI·경계 연결

**Files:**
- Modify: `Assets/_Game/Scripts/Input/PlayerInputController.cs`
- Modify: `Assets/_Game/Scripts/Presentation/UnitActionUI.cs`
- Modify: `Assets/_Game/Scripts/Presentation/BattleDebugUI.cs`
- Modify: `Assets/_Game/Scripts/Presentation/AttackPreviewText.cs` only for a backpack no-reload resource label if needed; BodyTargetUI already consumes common previews.
- Inspect/modify as necessary: `Assets/_Game/Scripts/AI/EnemyAIController.cs`, `Assets/_Game/Scripts/Combat/OverwatchSystem.cs` (use common slots, no AI redesign).
- Create: `Assets/_Game/Tests/PlayMode/BackpackPlayTests.cs`

**Interfaces:**
- `GetAvailableWeaponSlots` and ranged overwatch list include Backpack; `GetReloadWeaponSlots` excludes it.
- UnitActionUI labels backpack slot as `Backpack`/`백팩`, not Left/Right/Both hands. Size menu as32header+44perchoice+8bottom:2choices128px,3choices172px before scaling; preserve screen bounds/click geometry.
- Existing BodyTargetUI selection/Fire and CombatSystem execution operate on selected Backpack slot without a separate attack flow.

- [x] Write PlayMode tests for three visible/clickable weapons, backpack-only unit attack, empty/unarmed backpack hidden, zero ammo disabled but named, Reload exclusion, ordinary hand reload still works, AI using backpack and not reloading it, backpack ranged overwatch Fire/Skip and last-round movement release, Critical/ejection action gates. Assert actual input/button behavior, AP/ammo/HP, not source text.
- [x] Run filtered PlayMode tests and observe failures for missing integration.
- [x] Implement slot enumeration, dynamic menu height/labels, debug ammo and no-reload text. AI must not loop on empty backpack; it may use remaining usable hand weapon as before.
- [x] Run filtered PlayMode and full new EditMode suites; expected all pass. No edits to saved scenes to make tests pass.

4단계 검증: 신규 실제 입력 PlayMode12/12, 전체 EditMode709/709 및 그래픽 PlayMode380/380 통과(실패·건너뜀0). 세 번째 무기 버튼/화면 모서리 클릭/부위 선택 Fire/탄약 소진/Reload 제외/손 재장전/ESC/Critical 탈출/백팩 경계 Fire·Skip/마지막 탄약 이동 해제를 확인했다. AI는 기존 공통 슬롯 경로로 백팩 사용·소진 대기·손 무기 대체가 이미 정상 동작하여 별도 코드 변경하지 않았다. 읽기 전용 검토에서 조치할 사항 없음. 샘플 에셋과 안내 문서는 5단계에 남긴다.

### Task 5: 샘플 에셋·안내·전체 회귀

**Files:**
- Create: `Assets/_Game/Data/Backpacks/SampleUnarmedBackpack.asset`
- Create: `Assets/_Game/Data/Backpacks/SampleWeaponBackpack.asset`
- Create: `docs/backpack-equipment.ko.md`
- Update: `README.md`

**Interfaces:**
- Assets use Task1 BackpackData with unique IDs and Korean display names. Armed sample references existing fixed ranged WeaponData (the sample asset is a demonstration, not an authored-scene loadout change). Sample backpack own mass defaults0 pending user balance values; describe that weight must be authored rather than silently making existing equipment heavier.
- Guide explains selecting UnitWeaponLoadout.Backpack in Inspector before Play, integrated weapon read-only at unit level, finite ammo/no reload, total mass, no automatic model attachment and no deployment weight enforcement yet.

- [x] Add asset-persistence/empty legacy loadout regression assertions to BackpackDataTests before creating assets; observe failure, then create the2sample assets and generated metas.
- [x] Run full `.\tools\Run-UnityTests.ps1 -Mode EditMode -Name backpack-final-edit`; expected0failed/0skipped.
- [x] Run full `.\tools\Run-UnityTests.ps1 -Mode PlayMode -Graphics -Name backpack-final-play`; expected0failed/0skipped. Preserve native-engine crash evidence rather than misreporting success; isolate/retry clone-only generated fixtures safely if needed.
- [x] Update user guide and README with actual supported behavior and result counts.
- [x] Request one fresh read-only final review using spec/plan and filesystem baseline; no Git/Unity/edits by reviewer. Fix actionable findings with failing regression tests and rerun relevant/full suites.
- [x] Compare hashes: original user scenes/assets/models/positions/settings/cameras preserved; clone verified C# sources equal original; all new script/asset metas present. Record evidence and deliver final summary.

## Self-review

All eight confirmed rules map to Tasks1–4 and regression Task5. Slot identities agree across loadout/combat/ammo/input/overwatch; no hand renumbering. Review Focus1/5 tested in Task3,2 in Tasks1/2,3/4 in Task4. Sample/visual/deployment limitations are explicit. Native method is preserved; no Git steps or automatic scene loadout modifications.

5단계 완료: 샘플 백팩 2종과 Unity 메타, 한글 사용 안내, README를 추가·갱신했다. 에셋 생성 전 누락 실패 2개와 기존 손 무장 회귀 1개를 확인한 뒤 전체 EditMode 712/712, 그래픽 PlayMode 380/380 통과(실패·건너뜀 0)를 확인했다. 최종 읽기 전용 검토에서 조치할 사항 없음. 기존 씬·기체/무기 에셋·모델·설정은 보존했다. 샘플 자체 중량 0은 미조정 값이며 자동 모델 장착과 중량 제한 적용은 포함하지 않는다.
