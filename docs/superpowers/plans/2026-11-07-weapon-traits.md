# Weapon Traits Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 무기별 특성 목록으로 건물 너머 곡사 발사를 선택한다.

**Architecture:** WeaponData가 특성 보유 여부를 제공한다. GridManager의 기존 선분 차단 검사를 재사용하며 CombatSystem의 공통 착탄점 검증으로 범위/미리보기/발사/AI를 연결한다.

**Tech Stack:** Unity 6000.5.5f1, C#, NUnit/Unity Test Framework. 외부 패키지 추가 없음.

**Spec:** docs/superpowers/specs/2026-11-07-weapon-traits-design.md (사용자가 대화에서 승인한 범위).

## Global Constraints

- Git/GitHub 작업 금지. 기존 체크아웃에서 수정하고 기존 테스트 복제본에서만 Unity 실행.
- 씬/에셋/모델/속도/카메라/그림자/엄폐 밸런스 수치 보존. 특성 자동 할당 없음.
- 엄폐물 HP/파괴, 실제 탄도/높이 판정, 파일럿 특성은 구현하지 않는다.

## Review Focus

- null/빈 목록과 기존 직렬화 데이터는 특성 없음으로 처리한다.
- 큰 건물과 일반 엄폐/골짜기를 구분한다.
- 대각선 모서리도 기존 차단 규칙을 유지한다.
- 특성이 있어도 사거리/폭발 차단/직사 차단을 우회하지 않는다.
- 조준 이후 경로가 막히면 Fire는 자원 소모 없이 거부한다.

### Task 1: 데이터와 발사 판정

Files: Combat/WeaponData.cs, Combat/WeaponTrait.cs (새 파일), Grid/GridManager.cs, Combat/CombatSystem.cs, Combat/CombatSystem.Indirect.cs, Editor/WeaponDataEditor.cs. Tests: EditMode/WeaponTraitTests.cs (새 파일), 기존 IndirectCombatTests의 명시적 건물 관통 사례에만 특성 지정.

Interfaces: `WeaponData.HasTrait(WeaponTrait)`; `GridManager.HasClearIndirectLaunchPath(from, to)`; private 공통 착탄점 검증.

- [x] 특성 기본값/직렬화/유효성, 건물 차단/허용, 범위 일치/자원 보존 테스트 작성 및 RED 확인.
- [x] 최소 구현 후 신규/기존 곡사 테스트 GREEN 확인.

### Task 2: 실제 입력과 AI

Files: Tests/PlayMode/IndirectAttackPlayTests.cs; 기존 UI/AI는 공통 CombatSystem API를 사용한다. 최종 검토에서 발견한 AI 이동 후보 검증을 위해 AI/EnemyAIController.cs와 Grid/Pathfinder.cs에 공통 곡사 위치 조건을 연결한다.

- [x] 차단 칸 선택 거부, 특성으로 선택 허용, 조준 후 차단 시 Fire 거부, AI 판정 연동 검증.
- [x] 테스트 복제본에서 그래픽 PlayMode 실행.

### Task 3: 회귀 검증과 정리

- [x] 현재 곡사/무기 가이드와 README를 갱신한다.
- [x] 전체 EditMode/그래픽 PlayMode, 에셋 보존 해시 비교.
- [x] 독립 읽기 전용 최종 검토 1회, 발견 사항 재현/수정 후 필요한 전체 검증.

커밋은 사용자 담당이므로 수행하지 않는다. 기록: Artifacts/weapon-traits-progress.md. 기준 스냅샷: Artifacts/WeaponTraitsBefore.

완료: EditMode 851/851, 그래픽 PlayMode 399/399, 실패·건너뜀 0. 독립 검토의 AI 이동 후보 사선 문제를 재현한 뒤 공통 곡사 검사로 수정하고 전체 검증을 반복했다. 기존 씬·에셋·설정 해시 변경은 0건이다.
