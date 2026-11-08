# Damage Taken Percent Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans, test-first, one fresh read-only final review.

**Goal:** 기체별 실탄/빔 피해율을 50~150%로 설정하고 공통 전투 계산에 연결한다.

**Architecture:** MechaData 기본값 → BattleUnit 초기화 스냅샷 → 최종 배율 함수 → 기존 피해/미리보기 경로. 레거시 타입은 이전 데이터 읽기에만 사용한다.

**Tech Stack:** Unity 6000.5.5f1/C#/NUnit, 기존 테스트 복제본 사용.

**Spec:** docs/superpowers/specs/2026-11-08-damage-taken-design.md

## Global Constraints

Git 금지. 기존 씬/에셋/모델/속도/카메라/엄폐 수치 보존. 부품 장착 시스템은 추가하지 않는다.

## Review Focus

레거시와 새 수치 중복 적용 금지; 명시100도 레거시80을 대체; null/초기화 스냅샷 보존; 멀티 편집/Undo에 따른 레거시 해석; 폭발/근접 유지와 치명타 반올림.

### Task 1: 데이터·최종 배율·공통 계산

Files: Unit/MechaData.cs, Unit/BattleUnit.cs, Combat/CombatMath.cs, Combat/CombatSystem.cs, Editor/MechaDataEditor.cs, Presentation/BattleDebugUI.cs. 새 EditMode/DamageTakenPercentTests.cs.

- [x] 수치/명시값/레거시/스냅샷/실제 피해 테스트 RED.
- [x] 기본값과 최종 조회 함수, Inspector 슬라이더, 공통 전투 연결 및 GREEN.

### Task 2: 통합과 회귀

Files: PlayMode/DamageTakenPercentPlayTests.cs, 현재 안내 문서/README.

- [x] 실제 입력 미리보기/Fire, 경계/AI의 새 수치 검증.
- [x] 독립 최종 검토 1회 및 발견 사항 재현/수정.
- [x] 전체 EditMode/그래픽 PlayMode, 소스 일치/에셋 보존 확인.
- [x] 문서와 최종 결과 정리.

기준: Artifacts/DamageTakenBefore. 진행 기록: Artifacts/damage-taken-progress.md. 사용자가 Git 통합을 담당한다.

결과: EditMode867/867, 그래픽 PlayMode403/403, 실패·건너뜀0. 독립 검토의 혼합값 표시 문제를 수정했다. 원본 씬/에셋/설정 해시 변경0. 부품 장착 시스템은 범위 밖으로 유지했다.
