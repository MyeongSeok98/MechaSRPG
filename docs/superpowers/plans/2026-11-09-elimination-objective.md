# Elimination Objective Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline with TDD and one fresh-context read-only final review.

**Goal:** Destroyed만 인정하는 지정 집단 섬멸과 전용 샘플 씬을 제공한다.

**Architecture:** MissionData + EnemyUnitGroup → EliminationObjective → MissionController → TurnManager. 설정 검증은 전투 시작 전, 런타임 상태는 전투 인스턴스에만 보관한다.

**Tech Stack:** Unity6000.5.5f1, C#, ScriptableObject, IMGUI, NUnit/UnityTest.

**Spec:** docs/superpowers/specs/2026-11-09-elimination-objective-design.md

## Global Constraints

기존 씬/데이터/튜닝/Git 변경 없음. 테스트 복제본 재사용. 씬 생성은 복제본에서 새 경로에만 저장한 뒤 원본에 복사한다. Native 연속 실행.

- [x] 현재 종료/데이터/생성기 조사 및 기준 해시 보관.
- [x] EditMode RED: 등록 집단 검증, 대상 수, Critical/탈출/삭제 제외, 외부 적 무관, Destroyed 보존, 공유 에셋 불변, 재초기화/기존 호환.
- [x] EnemyUnitGroup/EliminationObjective, MissionController·TurnManager·BattleSceneController 연결 및 최소 UI 구현 GREEN.
- [x] 덮어쓰기 금지 생성기로 EliminationMissionTest 씬 추가.
- [x] 실제 씬과 팝업/동시 피해/실패 우선/독립 실행 상태 PlayMode 검증.
- [x] 단일 최종 읽기 전용 검토, 전체 EditMode와 그래픽 PlayMode, 코드/자산 일치 확인.
- [x] 사용/아키텍처 가이드·README·체크리스트·검증 기록 갱신 후 코드 설명.
