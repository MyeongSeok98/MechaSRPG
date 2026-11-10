# MissionController Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline, TDD and one fresh-context final review.

**Goal:** 임무 결과 판단과 전투 종료 실행의 책임을 분리한다.

**Architecture:** MissionController(MissionResult/요청/공통 실패) → TurnManager(안전한 평가 시점/종료 실행). 기존 씬만 적 전멸 자동 승리를 유지한다.

**Tech Stack:** Unity6000.5.5f1, C#, NUnit/UnityTest. 새 패키지 없음.

**Spec:** docs/superpowers/specs/2026-11-09-mission-controller-design.md

## Global Constraints

저장 씬/데이터/사용자 튜닝/Git 변경 없음. 테스트 복제본 재사용. Native 연속 실행, 단계 승인 중단 없음.

- [x] 기존 TurnManager/UnitHealth/종료·팝업·경계 테스트 조사, 기준 해시 보관.
- [x] EditMode 미션 테스트 RED: 공통 실패/복수 아군/Critical/명시적 요청/중복/동시 실패 우선/커스텀 임무 무조건 승리 방지/레거시 호환.
- [x] MissionResult와 MissionController 추가, TurnManager 종료 요청·평가 위임 GREEN.
- [x] PlayMode 피해 원자성/팝업 종료 후 요청/이동·AI·경계 중단/실제 씬 연결 검증.
- [x] 단일 최종 읽기 전용 검토, 중요 발견 수정. 전체 EditMode·그래픽 PlayMode 확인.
- [x] 새 .meta 복사, 원본/복제본 소스 일치·기존 자산 보존 확인, 사용 가이드와 결과 기록.
