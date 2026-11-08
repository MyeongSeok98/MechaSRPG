# MechaData 탐지 거리 연동 구현계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 기체의 기본 탐지 거리를 MechaData에서 읽어 전투 시작 시 UnitVision에 적용한다.

**Architecture:** MechaData는 기종 정의, BattleUnit은 전투 시작 연결, UnitVision은 유닛별 적용 수치와 변경 알림을 맡는다. VisibilitySystem과 전장의 안개·AI는 기존 UnitVision 소비 경로를 그대로 사용한다. 추가부품 보너스는 이번에 구현하지 않는다.

**Tech Stack:** Unity 6000.5.5f1, C#, ScriptableObject, 기존 NUnit EditMode / Unity PlayMode 테스트.

**Spec:** [MechaData 탐지 거리 설계](../specs/2026-11-04-mecha-detection-range-design.md)

## 공통 제약

- 기존에 선택한 Native 방식으로 직접 구현한다. 단계별 진행 요청이 오면 해당 단계까지만 수행한다.
- Git·GitHub·스테이징·커밋·브랜치 작업을 하지 않는다. 파일 해시 기준으로 변경을 확인한다.
- Unity 실행은 기존 `Artifacts/MeleeRangedTestProject` 복제본에서만 한다. Unity 프로세스가 종료된 뒤에만 소스를 동기화한다.
- 기존 씬·프리팹·기체/무기/백팩 에셋과 사용자 설정을 테스트에 맞추어 변경하지 않는다.
- 새 테스트 파일의 Unity 생성 `.meta`는 원본에 복사한다. 임시 테스트 에셋은 테스트 소유 폴더에서 생성하고 정리한다.
- 새 전투 서비스나 부품 프레임워크를 만들지 않는다. UnitVision.Configure(int)와 VisibilitySystem을 재사용한다.
- 필드가 없는 기존 MechaData는 기본10칸, 명시적인0은 반경0, MechaData가 없는 유닛은 기존 UnitVision 값 유지.
- 계획 검토 후 사용자가 두 단계 구현을 승인했다.

## 검토 초점

1. 기존 MechaData의 누락 필드가0으로 잘못 로드되어 탐지가 사라지는 경우: Task1의 기존 에셋 기본값 검사.
2. UnitVision에 남은 개별 값이 기체 데이터를 덮어쓰는 경우: Task2의 기존 컴포넌트 우선순위 검사.
3. 초기화 재호출·턴 전환으로 유닛별 적용 값이 초기화되는 경우: Task2의 중복 초기화·턴 전환 검사.
4. 공유 MechaData를 바꾸어 다른 유닛의 탐지가 함께 변하는 경우: Task2의 유닛 독립성·에셋 불변 검사.
5. 거리는 바뀌었지만 초기 발견·시야 차단·안개·AI 판단이 기존 값에 머무는 경우: Task2의 실제 전투 초기화와 통합 검사.

## Task 1: 기체 기본 탐지 거리 데이터와 Inspector

**변경 파일**

- `Assets/_Game/Scripts/Unit/MechaData.cs`
- `Assets/_Game/Editor/MechaDataEditor.cs`
- `Assets/_Game/Tests/EditMode/MechaDataTests.cs`

**인터페이스**

- 추가: `[SerializeField, Min(0)] private int detectionRange = 10;`
- 추가: `public int DetectionRange => detectionRange;`
- 확장: `IsValid(out string reason)`에서 음수 거부. `OnValidate()`에서0 이상으로 보정.
- 커스텀 Inspector의 이동 효율 다음에 `기본 탐지 거리 (칸)` 표시. 초기화 때 적용되고 추가부품 보정은 없다는 안내를 작성한다.

- [x] **1. 변경 전 기록:** 관련 C#과 Assets/ProjectSettings/Packages의 해시를 작업 소유 Artifacts 폴더에 기록한다. 이전 검증 소스와 현재 파일의 차이가 있으면 먼저 확인한다.
- [x] **2. 테스트 먼저 작성:** 아래 항목을 MechaDataTests에 추가한다. 새 속성 존재 여부는 기존 reflection 테스트 패턴으로 검사해 구현 전 컴파일 오류 대신 명확한 실패를 얻는다.
  - `DetectionRangeDefaultsToTen`: 새 MechaData의 DetectionRange는10.
  - `ZeroDetectionRangeIsValid`: 유효 ID와 detectionRange0은 유효하며 값0을 보존.
  - `NegativeDetectionRangeIsRejectedAndAuthoringClampsToZero`: 음수는 IsValid 실패, OnValidate 호출 후0.
  - 기존 `AllRequestedFieldsSurviveAssetReload`에 detectionRange12 저장·재읽기 검사를 추가한다. 필요하면 테스트 소유 에셋을 unload/reload하여 메모리 객체만 검사하지 않도록 한다.
  - `ExistingMechaAssetsWithoutDetectionFieldUseTen`: 기존 `Assets/_Game/Data/Mecha/SampleMecha.asset`과 `Assets/_Game/Data/Mecha/SampleEnemyMecha 1.asset`을 읽기 전용으로 로드해 기본10과 유효성을 확인한다. 기존 에셋을 저장하지 않는다.
- [x] **3. RED 실행:** 복제본에서 `.\tools\Run-UnityTests.ps1 -Mode EditMode -Name mecha-detection-data-red -Filter MechaDataTests` 실행. 새 탐지 필드/속성 부재에 따른 실패를 확인하고 기록한다.
- [x] **4. 구현:** 필드·속성·검증·한글 Inspector만 추가한다. BattleUnit과 기존 에셋의 YAML은 이 단계에서 변경하지 않는다.
- [x] **5. GREEN 실행:** 복제본에서 `.\tools\Run-UnityTests.ps1 -Mode EditMode -Name mecha-detection-data-green -Filter MechaDataTests` 실행. 실패·건너뜀0을 확인한다.
- [x] **6. 단계 기록:** 변경 파일·RED/GREEN 결과·기존 에셋 해시 보존을 기록한다. 이 단계에서는 데이터 정의만 추가되며 실제 탐지 연동은 Task2에 남는다.

## Task 2: 전투 초기화 연결과 탐지 통합 검증

**변경 파일**

- `Assets/_Game/Scripts/Unit/BattleUnit.cs`
- 신규 `Assets/_Game/Tests/EditMode/MechaDetectionRangeTests.cs`
- 신규 `Assets/_Game/Tests/PlayMode/MechaDetectionRangePlayTests.cs`
- 안내: `docs/mecha-data-guide.ko.md`, `docs/current-code-architecture.ko.md`, `docs/combat-system-checklist.ko.md`, `README.md`

**소비 인터페이스**

- Task1의 `MechaData.DetectionRange`.
- 기존 `UnitVision.Configure(int cells)`, `UnitVision.DetectionRange`, `UnitVision.Changed`.
- 기존 `BattleUnit.Initialize(GridManager grid)`, `BattleSceneController.InitializeBattle()`.

**초기화 규칙**

`BattleUnit.Initialize`에서 기존 필수 컴포넌트·기체 데이터·중량·배치 검증을 유지한다. 기존 `if (mechaData)` 스펙 적용 블록에 `Vision.Configure(mechaData.DetectionRange)`를 연결하여 정상 초기화 완료와 VisibilitySystem 최초 Refresh 전에 적용한다. MechaData가 없으면 Configure를 호출하지 않는다. 이미 초기화된 유닛의 조기 반환은 그대로 유지한다. UnitVision·VisibilitySystem의 제품 코드 수정은 원칙적으로 필요 없다.

- [x] **1. EditMode 테스트 작성:** 테스트용 기체/유닛만 생성하고 소유 자원을 정리한다.
  - `MechaRangeOverridesExistingUnitVision`: 기존 UnitVision3, MechaData12 → 초기화 후12. UnitVision 자동 생성 경로도 검사.
  - `MissingMechaKeepsLegacyRange`: MechaData 없음 + 기존 UnitVision6 →6, 컴포넌트도 없으면10.
  - `ZeroAndNegativeMechaRangesRespectValidation`:0이면 반경0, 음수 MechaData는 초기화 실패·그리드 점유 없음.
  - `SharedMechaProducesIndependentUnitVision`: 같은 데이터12를 쓰는 두 유닛 중 하나를 Configure4로 바꿔도 다른 유닛과 MechaData는12 유지.
  - `RepeatedInitializeDoesNotOverwriteAppliedRange`: 초기화 후 Configure4, 같은 그리드 재초기화 →4 유지. MechaData를 편집해도 현재 유닛에 자동 재적용하지 않음.
  - `StartupDetectionUsesMechaRangeAndExistingLOS`: 관측자(1,1), 반경5에서 대상(4,5)은 발견, (5,5)는 미발견. (5,1) 대상은 (3,1) 건물로 차단. 실제 VisibilitySystem 결과를 검사.
- [x] **2. PlayMode 테스트 작성:** 기존 VisibilityAITests/FogOfWarPresentationTests의 실제 초기화 패턴을 재사용하되 MechaData를 초기화 전에 지정한다. 최초 탐지 검사는 초기화 후 Vision.Configure로 원하는 결과를 강제로 만들지 않는다. 재적용 방지 검사는 별도로 런타임 Configure를 호출한다.
  - `SceneStartsWithMechaDetectionAndFog`: 반경4/8을 각각 독립된 테스트 전투로 실행한다. BattleSceneController로 초기화해 거리6의 적과 칸의 가시성 차이, 탐지 유닛의 이동 후 안개 갱신·탐험 기록 유지를 검사한다. 같은 진영의 다른 관측자가 공유 시야로 결과를 가리지 않도록 한다.
  - `EnemyUsesMechaRangeForWaitingAndAttacking`: 각각 독립된 테스트 전투에서 거리6의 적 탐지4이면 대기/AP 유지, 탐지8이면 기존 공통 공격 경로로 실제 AP·탄약·부위 HP 변화 확인. 첫 공격으로 전투가 끝나지 않을 내구도의 대상을 사용하고 RNG를 고정하여 턴 종료를 제한 시간 내 기다린다.
  - `TurnChangeDoesNotResetUnitVision`: 전투 시작 후 유닛별 Configure 변경 값이 턴 전환 뒤에도 유지되는지 검사한다.
- [x] **3. RED 실행:** 복제본에서 신규 EditMode/그래픽 PlayMode 필터 테스트를 각각 실행한다. 데이터만 추가된 상태에서 전투 초기화가 기존10 또는 개별 값을 사용하는 실패를 확인한다. 이미 정상인 레거시·재초기화 검사는 통과한 사실도 구분해 기록한다.
- [x] **4. 구현:** BattleUnit 초기화 연결을 추가한다. 탐지의 원형 거리·시야 차단·공유 시야·AI·안개 규칙은 수정하지 않는다. 신규 테스트를 다시 실행해 모두 통과시킨다.
- [x] **5. 전체 회귀:** 복제본에서 다음 명령을 순서대로 실행하고 XML의 실패·건너뜀0을 확인한다.

```powershell
.\tools\Run-UnityTests.ps1 -Mode EditMode -Name mecha-detection-final-edit
.\tools\Run-UnityTests.ps1 -Mode PlayMode -Graphics -Name mecha-detection-final-play
```

작업 디렉터리는 두 명령 모두 `Artifacts/MeleeRangedTestProject`. 원본 씬을 변경하는 대신 기존 두 저장 씬과 AI·안개·경계의 전체 회귀를 활용한다. 네이티브 엔진 종료 오류가 나면 근거를 보존하고 테스트 실패/미완료와 구분해 기록한다.

- [x] **6. 안내 갱신:** MechaData가 있는 유닛의 탐지 조절 위치·기본10·0의 의미·레거시 동작·전투 재시작 적용·추가부품 미구현을 설명한다. 체크리스트의 기체 데이터 정의/초기화/탐지·안개 검증은 실제 완료 후에만 체크한다. 추가부품·머리 손상 항목은 완료로 표시하지 않는다.
- [x] **7. 최종 검토:** Native 실행 스킬에 따라 신선한 읽기 전용 리뷰1회를 수행한다. 위 검토 초점과 변경 전 파일을 전달하고 Git·Unity 실행·수정은 금지한다. 영향 있는 결함은 실패 테스트로 재현한 뒤 수정하고 필요한 전체 회귀를 재실행한다.
- [x] **8. 완료 증거:** 원본 보호 에셋/씬/설정 해시가 그대로이고 복제본 검증 C#이 원본과 일치하는지 확인한다. 테스트 결과·문서·생성 메타와 단계를 기록하고 변경된 설정 위치를 사용자에게 안내한다. Git 작업은 하지 않는다.

## 계획 자체 검토

- 데이터 정의·기본값·음수 검증·기존 에셋 호환은 Task1이 담당한다.
- 기존 UnitVision 값의 우선순위·레거시·독립성·중복 초기화·실제 탐지/안개/AI는 Task2가 담당한다.
- 현재 최종 탐지 거리 = 기본 탐지 거리. 미래 부품 보너스를 위한 공유 에셋 변경이나 빈 합산 프레임워크는 추가하지 않는다.
- 두 단계 구현 및 검증 완료. 제품 변경은 MechaData·MechaDataEditor·BattleUnit으로 한정했고 기존 에셋·씬은 변경하지 않았다.

## 완료 결과

Task1: 데이터 RED 36개 중 신규/확장 검사6개 실패 → GREEN36/36. Task2: 초기화 RED9개 중6개 실패, 그래픽 통합 RED5/5 실패 → 최종 전체 EditMode740/740, 그래픽 PlayMode385/385 통과. 실패·건너뜀0. 읽기 전용 최종 검토에서 조치할 결함 없음.

첫 전체 PlayMode는 기존 화면 크기 복원 검사에서321×531로 바뀌면서 후속 카메라/입력 검사가 실패했다. 신규 탐지5개는 당시에도 통과했다. 소스 변경 없이 새 프로세스에서 관련12개를 모두 통과한 뒤 전체385개를 다시 통과했다. 초기 실패·진단·최종 결과는 Artifacts에 각각 보존했다. 이 실행 환경의 간헐적 해상도 복원 문제를 해결했다고 주장하지 않는다.

기존 보호 파일 해시와 테스트 복제본의 C# 소스 일치를 확인했다. 기존 설정값, 추가부품/머리 파괴 보정, UnitVision/VisibilitySystem의 판정 로직은 변경하지 않았다. Git 작업 없음.
