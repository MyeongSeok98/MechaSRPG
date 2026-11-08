# Cover Destruction Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans, test-first, one fresh read-only final review.

**Goal:** 엄폐물 HP와 파괴 후 기능 제거를 직사/곡사 폭발무기에 연결한다.

**Architecture:** 개체별 CoverDurability → CoverObject 가용성 → CoverSystem 재구성. GridManager는 콜라이더별 점유 기여를 제거한다. CombatSystem의 별도 엄폐 조준/기존 곡사 배치에서 공통 엄폐 피해를 적용하고 UI와 지도 갱신을 연결한다.

**Tech Stack:** Unity6000.5.5f1/C#/NUnit/UnityTest, 패키지 추가 없음.

**Spec:** docs/superpowers/specs/2026-11-08-cover-destruction-design.md

## Global Constraints

Git 금지. 원본 씬·무기 에셋·모델·속도12f·카메라·그림자·엄폐 배율 보존. 기존 HalfCover/FullCover 프리팹의 HP 컴포넌트 추가만 승인된 에셋 변경으로 허용한다.

## Review Focus

중복/겹침 등록 해제, 파괴/재활성화의 일회성, 다칸 엄폐 중복 피해, 파괴 전 대상 동결/미탐지 보호, Fire 재검증·RNG/AP/탄약 단일 지불과 UI 전환/ESC.

### Task1: HP·그리드·엄폐 갱신

Files: 새 Cover/CoverDurability.cs, Cover/CoverObject.cs/CoverSystem.cs, Grid/GridCell.cs/GridManager.cs. Tests 새 EditMode/CoverDurabilityTests.cs.

- [x] HP·파괴·겹침·수동 차단·중복 등록 테스트 RED.
- [x] 개체별 HP와 가용성, 소스별 그리드 기여와 방향 엄폐 재구성 GREEN.

### Task2: 폭발 전투 연결

Files: 새 Combat/CoverAttack.cs, Combat/CombatSystem.Cover.cs; Combat/WeaponData.cs/CombatMath.cs/CombatSystem.cs/CombatSystem.Indirect.cs/IndirectAttack.cs. Tests 새 EditMode/ExplosiveCoverTests.cs.

- [x] 직사 선택/API·모드/거리/사선·자원·곡사 다칸/차단/같은 발 대상 동결 테스트 RED.
- [x] 피해 배율·직사 확정·곡사 일괄 피해와 지도 변경 알림 GREEN.

### Task3: 입력·UI·프리팹

Files: 새 Input/PlayerInputController.Cover.cs, Presentation/CoverTargetUI.cs, Editor/CoverDurabilityEditor.cs; 기존 PlayerInputController/IndirectTargetUI/WeaponDataEditor와 HalfCover/FullCover.prefab. Tests 새 PlayMode/CoverDestructionPlayTests.cs.

- [x] 실제 엄폐 클릭→Fire, 무기 전환/ESC/파괴 후 갱신, 곡사/UI/AI 테스트 RED.
- [x] 한글 HP·무기 배율 필드, 조준 창과 입력 상태, 기본 프리팹 컴포넌트 추가 GREEN.

### Task4: 검증과 정리

- [x] 가이드/체크리스트/README 갱신.
- [x] 독립 읽기 전용 최종 검토 1회, 발견 사항 재현/수정.
- [x] 전체 EditMode/그래픽 PlayMode, 원본 보존·프리팹 최소 변경·테스트 소스 일치 확인.

기준 Artifacts/CoverDestructionBefore. 기록 Artifacts/cover-destruction-progress.md. 단계별 재승인 없이 Native 연속 실행한다.


