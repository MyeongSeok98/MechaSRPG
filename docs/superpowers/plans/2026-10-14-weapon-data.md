# 무기 데이터·시작 장착·공격 연결 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking. 기존 사용자의 Native 실행 방식과 단계 완료 후 확인을 유지한다. 계획 검토 완료 전 구현하지 않는다.

**Goal:** 사용자가 무기 에셋을 직접 생성·복제하고 양손 장착/파지를 지정하면, 플레이어와 적이 그 무기의 거리별 명중률·랜덤 피해로 공격한다.

**Architecture:** WeaponData는 공유 설정, UnitWeaponLoadout은 기체별 손 점유와 시작 파지, UnitCombat은 기존 전투 API와의 연결을 담당한다. CombatSystem은 선택한 슬롯의 동일한 설정으로 미리보기·공격을 수행하며 Input/UI와 AI도 슬롯을 전달한다. 기존 Loadout 없는 기체는 저장된 수치를 유지한다.

**Tech Stack:** Unity6000.5.5f1, C#, ScriptableObject, 기존 IMGUI/Input System/URP/Unity Test Framework. 외부 패키지 추가 없음.

**Spec:** [최신 합의 설계](../specs/2026-10-13-weapon-data-design.md) — 2026-10-14 개정. 초기 초안의 고정 근접/원거리 슬롯을 손 슬롯으로, 고정 피해를 최소/최대 피해로 대체했다.

## Global Constraints

- 원거리 Beam/Ballistic/Explosive, 근거리 Edged/Blunt. 분류 자체의 추가 효과 없음.
- WeaponData: 이름, 종류/분류, 최소/최대 정수 피해, 기본 명중률, 사거리, AP, 중량(t), 파지 조건, 양손 명중 배율, 거리 구간.
- GripRequirement=OneHandOnly/TwoHandsRequired/Either, GripMode=OneHand/TwoHands. 겸용 TwoHands만 양손 보너스.
- 시작 장착만 설정. 한손 두 자루 또는 양손 한 자루. 양손은 한 슬롯에만 저장. 점유 충돌 시 전체 장착을 거절.
- Loadout 없는 기체만 기존 수치 사용. Loadout이 비어 있거나 비활성/잘못되었으면 기존 수치로 되돌리지 않음.
- 맨해튼 거리. 상한 포함 구간, 비어 있는 목록/마지막 상한 뒤는 ×1.0. 근접은 거리 배율1. 유효하지 않은 구간은 공격 거절.
- 명중 공식: round(clamp(max(0, BaseHit × Grip × Distance - Evasion) × Head × Part × Cover, 0, 100)). 최종 한 번만 반올림/상한 적용.
- 피해는 명중 시에만 양 끝 포함 정수 추첨1회. 미리보기·빗나감·거절은 피해 난수 소비0. 기존 피해 결과의 HP 제한 유지.
- 샘플 원거리4~6/80/7/AP3, 근거리7~9/95/2/AP3. 원거리 Beam/Either/양손1.1, 구간(2,.9)/(5,1)/(7,.8). 근거리 Edged/OneHandOnly. 중량0t. 모두 편집 가능한 초기 검증값.
- 방패·수납·전투 중 교체/파지 전환·탄약·중량 합계/이동 보정·폭발/곡사/좌표 조준·팔 손상 무기 제한·모델 자세 제외.
- zoomSpeed60f, movementSpeed12f, 카메라 이동속도, Shadow Max Distance, 맵 크기/배치, 모델/총구/손잡이를 보존. 기존 씬·프리팹·머티리얼·Assets/Settings·Art/Models는 재저장하지 않음.
- Git 저장소 없음. Git 초기화/커밋/새 worktree 금지. 기존 분리 검증 프로젝트에서 Unity를 순차 실행.

## Review Focus

- 두 손에 같은 에셋을 지정한 두 자루를 하나로 합치거나, 양손 무기를 두 번 열거하지 않을 것(Task2).
- 빈/비활성/잘못된 Loadout이 기존 무기를 부활시키거나 잘못된 슬롯 요청을 기본 무기로 대체하지 않을 것(Task2/3).
- 거리 경계·구간 누락·중간 반올림으로 표시와 실제 명중률이 달라지지 않을 것(Task1/3).
- 미리보기/부위 선택/UI 재그리기가 난수를 소비하거나 다른 무기로 Fire하지 않을 것(Task3/4).
- 적의 이동 AP 예약·사거리·근접 도달이 선택 무기와 달라지지 않고 시야 상실 취소가 유지될 것(Task5).

## 파일별 책임과 공통 인터페이스

- Create Scripts/Combat/WeaponData.cs: 데이터, RangedWeaponCategory, MeleeWeaponCategory, GripRequirement, GripMode, DistanceAccuracyBand.
- Create Scripts/Unit/UnitWeaponLoadout.cs: 손 슬롯/파지, WeaponSlot enum, 점유 검증. 슬롯은 RightHand/LeftHand/LegacyRanged/LegacyMelee. 뒤의 두 값은 호환 경로 전용.
- Modify Scripts/Unit/UnitCombat.cs: `bool TryGetAttack(WeaponSlot slot, out AttackStats stats, out string reason)`, `IReadOnlyList<WeaponSlot> GetAttackSlots(AttackType type)`.
- AttackStats: Type/Slot/DisplayName/MinDamage/MaxDamage/BaseHitChance/Range/APCost/GripMultiplier/Weapon을 가진 값 구조체. 기존 Damage는 MinDamage를 반환하는 obsolete 호환 프로퍼티로만 남기고 새 제품 코드에서는 사용하지 않음. 기존 int Damage 접근이 필요한 UnitCombat 프로퍼티도 같은 호환 원칙. 전체 호출부를 검색해 새 계산에서 고정 피해가 남지 않게 함.
- Create Editor/WeaponDataEditor.cs, Editor/UnitWeaponLoadoutEditor.cs: 종류별 필드와 장착 오류/점유 표시. 기존 MechaXCOM.Editor.asmdef 재사용.
- Modify Combat/CombatSystem.cs, CombatMath.cs, AttackPreview.cs: 슬롯 지정 공격·미리보기, 명중 계산, 피해 추첨.
- Modify Input/PlayerInputController.cs, Presentation/UnitActionUI.cs, BodyTargetUI.cs, BattleDebugUI.cs: 슬롯 선택·무기명·피해 범위.
- Modify AI/EnemyAIController.cs, Grid/Pathfinder.cs: 선택 슬롯의 사거리/AP/공격 종류로 접근 후 공격.
- Create Data/Weapons/Ranged/StandardRifle.asset, Data/Weapons/Melee/StandardBlade.asset, docs/weapon-data-guide.ko.md.
- 경로는 별도 표기가 없으면 Assets/_Game/ 기준. 생성 파일·폴더 meta를 보관.

## 검증 준비와 실행 명령

실행 시작에 Artifacts/WeaponDataBefore/protected.json으로 보호 대상 SHA256, 변경 코드 before 사본을 저장한다. 기존 SharedVisibilityBefore 목록을 기준으로 새로 해시를 계산해 이번 작업의 기준을 만든다. 기존 보호 파일을 수정하지 않는다. 진행/판단은 Artifacts/weapon-data-progress.md에 기록한다.

원본 Assets를 검증용 Unity 종료 상태에서만 Artifacts/MeleeRangedTestProject/Assets로 동기화한다. 아래 명령은 해당 프로젝트 디렉터리에서 실행한다. Unity는 병렬 실행하지 않는다.

```powershell
.\tools\Run-UnityTests.ps1 -Mode EditMode -Name weapon-task1-red -Filter WeaponDataTests
.\tools\Run-UnityTests.ps1 -Mode EditMode -Name weapon-task2-red -Filter 'WeaponLoadoutTests;WeaponDataTests'
.\tools\Run-UnityTests.ps1 -Mode EditMode -Name weapon-task3-red -Filter 'WeaponCombatTests;CombatSystemTests;PartAttackTests'
.\tools\Run-UnityTests.ps1 -Mode PlayMode -Name weapon-task4-red -Graphics -Filter 'WeaponInputTests;UnitActionMenuTests;CombatInputTests'
.\tools\Run-UnityTests.ps1 -Mode PlayMode -Name weapon-task5-red -Filter 'WeaponAITests;VisibilityAITests;EnemyAITests'
.\tools\Run-UnityTests.ps1 -Mode EditMode -Name weapon-final-editmode
.\tools\Run-UnityTests.ps1 -Mode PlayMode -Name weapon-final-playmode -Graphics
```

각 Task의 RED→GREEN은 동일 필터로 실행하고 이름의 red를 green으로 바꾼다. 컴파일 오류만으로 RED를 인정하지 않는다. 최소 인터페이스를 준비한 뒤 기능 부족에 따른 단언 실패를 확인한다. 기존 테스트 클래스 이름은 rg로 확인해 필터를 보완한다. XML/log/화면 증거는 원본 Artifacts에 복사한다.

## Task1: 편집 가능한 무기 에셋

**Files:** Create WeaponData.cs, WeaponDataEditor.cs, Data/Weapons의 두 샘플; Test EditMode/WeaponDataTests.cs.

**Interfaces:** WeaponData는 읽기 전용 프로퍼티, `bool IsValid(out string reason)`, `float GetDistanceHitMultiplier(long distance)` 제공. DistanceAccuracyBand는 int MaxDistance/float Multiplier. Inspector는 SerializedObject를 사용해 Undo/멀티 편집을 지킴. 테스트 설정은 SerializedObject 사용, 불필요한 런타임 쓰기 API 추가 금지.

- [x] **RED 작성:** `CreateAndReloadAllFiveCategories`에서 임시 에셋 생성/저장/재로드 후 종류·분류·피해·중량 유지 확인. `BandsAreInclusive`에서 (2,.9)/(5,1)/(7,.8)에 대해 거리1/2=.9,3/5=1,6/7=.8,8=1, 빈 목록=1 확인. `InvalidDataRejected`는 역전 피해, 음수/NaN/Infinity, 중복/역순/사거리 초과 구간, 잘못된 enum 거절. `MeleeIgnoresDistanceBands`는근접×1 확인. 테스트 임시 에셋은 테스트 전용 디렉터리에만 생성/정리.
- [x] **RED 실행:** 위 Task1 명령, 행동 단언 실패 확인.
- [x] **구현:** CreateAssetMenu("MechaXCOM/Weapon Data"), 유효성 검사, 구간 조회, 분류별 Inspector. 일반 숫자 입력은 허용 범위로 보정하고 잘못된 enum/구간은 명확한 오류. runtime IsValid로 외부 직렬화 오류도 차단. 거리 목록은 자동 정렬·중복 제거로 숨기지 않음.
- [x] **샘플 작성:** 합의된 초기값의 두 에셋 생성. 기존 씬/프리팹에 자동 할당하지 않음. 에셋 복제 후 원본과 독립 편집 가능한지 확인.
- [x] **GREEN 및 기록:** Task1 필터 통과·보호 해시 확인, ledger에 결과 기록 후 사용자 확인.

## Task2: 시작 장착·파지·기존 수치 호환

**Files:** Create UnitWeaponLoadout.cs, UnitWeaponLoadoutEditor.cs; Modify UnitCombat.cs; Test EditMode/WeaponLoadoutTests.cs.

**Interfaces:** Loadout `bool IsValid(out string reason)`, `void Configure(WeaponData left, GripMode leftGrip, WeaponData right, GripMode rightGrip)`; UnitCombat TryGetAttack/GetAttackSlots는 위 공통 인터페이스. 열거 순서는 RightHand→LeftHand. 컴포넌트 부재에서만 Legacy 슬롯 반환. 잘못된 enum/빈 슬롯 조회 실패. 런타임 Configure는 초기 테스트/생성 용도이며 전투 중 교체 UI와 AP 규칙은 제공하지 않음.

- [x] **RED 작성:** `SeparateHandsCanUseSameAsset`는 같은 에셋 양쪽 한손 지정 후 두 슬롯 반환. `TwoHandWeaponEnumeratedOnce`는 한 슬롯 TwoHands/반대 null이면1개. `ConflictingHandsFailClosed`는 반대 무기 점유와 파지 조건 불일치 모두 실패·목록 비어 있음. `EmptyDisabledInvalidLoadoutNeverFallsBack`와 `LegacyValuesRemainFixed`에서 각각 비무장/비활성/오류 거절, Loadout 없음 기존 damage5→min=max5 확인. `GripBonusOnlyForEitherTwoHands`에서한손1,겸용양손1.1,양손필수1 확인. 같은 에셋 공유 기체 간 파지와 값 독립성 확인.
- [x] **RED 실행:** Task2 명령, 실패 저장.
- [x] **구현:** 시작 슬롯 저장·손 점유 검사·Inspector 오류. UnitCombat은 공유 에셋을 수정하지 않고 AttackStats를 반환. 기존 Configure API는 이전 필드만 유지. 기존 GetAttack(type)/프로퍼티는 첫 유효 슬롯에 위임하고 실패 시 안전한 무효 수치 반환. 잘못된 명시 슬롯을 다른 무기로 대체하지 않음.
- [x] **GREEN:** 기존 CombatSystemTests/PartAttackTests 포함 EditMode 전체 실행해 legacy 회귀 확인. 보호 검사·ledger 후 사용자 확인. 이번 Task는 전투 소비자 슬롯 연결 전까지의 어댑터 단계.

## Task3: 선택 무기 명중률·랜덤 피해

**Files:** Modify CombatSystem.cs, CombatMath.cs, AttackPreview.cs; Test EditMode/WeaponCombatTests.cs; 기존 고정 피해 미리보기 참조 테스트 필요 부분 이전.

**Interfaces:** `GetWeaponPreview(BattleUnit attacker, BattleUnit target, WeaponSlot slot, TargetBodyPart part=Whole)`; `TryAttackWithWeapon(...)` 같은 인자; `GetWeaponRangeCells(BattleUnit attacker, WeaponSlot slot)`; `SetDamageRollSource(Func<int,int,int> source)`는 min/max 포함 계약. 기존 GetPreview/TryAttack/GetAttackRangeCells는 종류의 첫 유효 슬롯으로 위임. AttackPreview에 WeaponSlot/WeaponName/MinDamage/MaxDamage/GripMultiplier/DistanceMultiplier 추가; Damage 호환 프로퍼티는 MinDamage이며 모든 UI를 Task4에서 범위 표시로 이전.

- [x] **RED 작성:** `PreviewNeverConsumesRandom`에서 미리보기/범위 호출 반복 후 hit/damage 난수 호출0. `HitRollsInclusiveDamageOnce`에서 주입 피해=min/max 모두 부위HP와 DamageDealt 확인. `MissAndInvalidNeverRollDamage`에서 빗나감은 AP소비+피해난수0, 거절은 AP/HP/모든난수 불변. `FixedDamageAndRemainingPartHP`에서 min=max5, 남은HP1은 DamageDealt1 확인.
- [x] **명중 RED:** `GripDistanceBeforeEvasionWithFinalRounding`에서 base80×1.1×.8−10=60.4→60, head/part/cover=.5 각각 적용 시 최종8 확인. 배율을 곱한 기본값이100을 초과해도 회피 차감 전에는100으로 자르지 않는 케이스 확인. 거리2/3/5/6/7 경계, 동일 위치/사거리 밖/건물/미탐지/파괴 부위/턴/AP 거절, 양손/근접 및 잘못된명시슬롯을 검증.
- [x] **RED 실행:** Task3 명령, 실패 저장.
- [x] **구현:** 명시 슬롯을 매 Fire 때 다시 해결·검증. 기존 명중 함수에 optional gripMultiplier=1/distanceMultiplier=1을 추가해 legacy 동일값 유지. 최소/최대 포함 기본 난수는 `Random.Range(min-1,max)+1`(min>=1)로 int.MaxValue의 max+1 overflow 방지. 주입 함수는 양 끝 포함 범위 계약이며 범위 밖 반환값은 Clamp한다. 정상 기본 난수는 범위 밖 값을 반환하지 않는다. 기존 SetRollSource와 피해 난수는 분리. 범위0/비활성/무효 슬롯에서 fail closed.
- [x] **GREEN:** Task3 및 전체 EditMode 통과. 에셋 해시/원본 보호 검사, ledger 후 사용자 확인.

## Task4: 무기 선택과 미리보기 UI

**Files:** Create Presentation/AttackPreviewText.cs; Modify PlayerInputController.cs, UnitActionUI.cs, BodyTargetUI.cs, BattleDebugUI.cs; Test PlayMode/WeaponInputTests.cs; 기존 UnitActionMenuTests/CombatInputTests 등 회귀.

**Interfaces:** Input `WeaponSlot? SelectedWeaponSlot`, `bool BeginWeaponAttack(WeaponSlot slot)`, `IReadOnlyList<WeaponSlot> GetAvailableWeaponSlots(AttackType type)`와 선택 목록 표시 상태. CurrentAttackPreview/범위/Fire는 선택 슬롯 API 사용. 종류별 버튼/BeginAttack(type)은 무기1개시 바로선택,2개시목록을열고 명시 선택 전 조준/Fire 불가. UnitActionUI `Rect GetWeaponButtonRect(WeaponSlot slot)`로 실제 클릭 테스트 지원.

- [x] **RED 작성:** `SameTypeWeaponsUseClickedHand`는 서로 다른 수치의 라이플2개와 동일에셋2자루 모두 손 표시·클릭·선택슬롯 확인. `SingleWeaponSkipsListAndEmptyTypeDisabled`, `ChangingWeaponClearsTargetPartAndResult`, `FireUsesSelectedSlotAndRange` 확인. raycast보다 UI 클릭 우선, 겹침/화면 경계/작은 화면에서 목록 선택 가능 여부를 검증.
- [x] **표시 RED:** 무기명·hit·4~6범위/AP 텍스트를 실제 조회 가능한 표시값으로 확인. UI/부위 선택/프레임 반복에서 피해 난수0, 두 Fire 진입점이 동일 슬롯 사용. ESC/유닛 변경/턴 종료/시야 상실 후 기존 선택 정리 동작 유지. Loadout 오류시 버튼/API 모두거절.
- [x] **RED 실행:** Task4 그래픽 명령. 기존 테스트 실제 이름을 확인하여 필터 보완.
- [x] **구현:** 기존 Melee/Ranged 옆 목록을 재사용/확장하고 Right/Left+이름 표시. 별도 인벤토리/수납패널 추가 없음. 동시1회무기공격 유지. 초기값·부위HP·안개 표시 변경 없음. 모든 preview.Damage 표시를 MinDamage~MaxDamage로 이전(동일값이면 단일수 표시).
- [x] **GREEN/화면:** 실제 클릭 테스트와 그래픽 캡처로 두 무기 목록·조준·Fire와 부위 패널 겹침 확인. 보호 해시·ledger 후 사용자 확인.

## Task5: 적 AI·최종 통합·사용 안내

**Files:** Modify EnemyAIController.cs, Pathfinder.cs; Create docs/weapon-data-guide.ko.md; Test PlayMode/WeaponAITests.cs; Modify SceneIntegrationTests.cs에 기존 씬의 Loadout 부재와 공격 수치 호환 검사 추가(씬 저장 없음).

**Interfaces:** Pathfinder 기존 `FindPathToRange(grid,unit,target,range)` 유지, overload `FindPathToRange(GridManager grid, BattleUnit unit, Vector2Int target, int range, AttackType type)` 추가. 기존 호출은 Ranged로 위임. 목표 칸 판정에서 원거리 HasClearShot,근접 HasMeleeReach를 사용. AI 계획에는 target/slot/path를 함께 보관.

- [x] **RED 작성:** `AIUsesSecondHandWhenFirstCannotAttack`(첫사거리2/둘째7), `AIReservesSelectedWeaponAP`(AP5,AP비용4이면이동1후사격), `MeleeOnlyAIUsesMeleeReach`(골짜기 건너 사격가능해도근접불가), `UnarmedInvalidAIWaitsWithoutAP`, `SameAssetTwoHandsNoDoubleAttack` 확인. 기존 VisibilityAITests의 이동 중 목표 상실·마지막완료칸점유/AP 검증도 실행.
- [x] **RED 실행:** Task5 명령, 실패 확인.
- [x] **구현:** 발견 대상 중 즉시 근접가능→원거리가능 순으로 기존 가까운대상 선택. 동일 종류는 오른손→왼손 순의 실행가능 슬롯. 이동후 공격 후보는 기존 최단경로 우선, 동률일때기존 대상순서/근접우선/오른손우선; 선택 슬롯 AP를 예약. 이동후 최신유효성 확인하고 슬롯 공격1회. 사격불가시 접근계획도 실제 슬롯 사거리 기반. 비무장대기와 기존 시야상실취소 유지.
- [x] **GREEN 및 안내:** 필터 통과 후 사용자 안내에 Create/Duplicate,5분류,거리상한/빈목록,중량미적용,한손두개/양손한개,Loadout없는호환과빈Loadout차이,모델자동교체없음을 설명. 검증용 런타임/임시씬에서 샘플 적용을 보여주고 원본씬은 재저장하지 않음.
- [x] **전체 검증:** 전체 EditMode/그래픽 PlayMode 실패0,원본과검증프로젝트 C#/새에셋일치,보호해시변경0 확인. XML/log/PNG를 원본Artifacts에 보관. 기준 개수262/197은 참고일뿐 새 테스트 포함 실제 개수 보고. 플레이어 빌드를 실행하지 않았다면 빌드 성공을 주장하지 않음.
- [x] **Native 독립 최종 리뷰:** requesting-code-review 절차로 전체 설계/변경/증거를 한 리뷰 담당에 전달. 중요 사항은 RED→GREEN과전체회귀로 수정,사소한항목은ledger 기록. Git작업하지 않음. 공유 검증프로젝트/보호사본은삭제하지 않음.
- [x] **완료 보고:** 무기 생성·장착 방법,실제 검증 결과,미구현범위 보고. 사용자 확인 없이 다음 시스템으로 확장하지 않음.

## 완료 조건

코드 수정 없이 새 무기를 생성·복제해 시작 장착할 수 있고, 양손점유·파지·거리명중률·피해범위가 입력/UI/공격/AI에서 일관되게 적용된다. 기존Loadout없는씬은 기존수치로작동한다. 보호자산변경0,전체테스트실패0,중요리뷰미해결0이어야 한다.
## 계획 자체 검토

데이터 생성/검증은 Task1, 손 점유/호환은 Task2, 배율/피해/슬롯 API는 Task3, 실제 선택과 표시/Fire는 Task4, AI/사용 안내/전체 회귀는 Task5에 연결했다. Review Focus의 다섯 항목도 각 단계 테스트에 연결했다. 코드·씬·자산의 구현 변경이나 테스트 실행은 이 계획 작성 단계에서 하지 않았다.