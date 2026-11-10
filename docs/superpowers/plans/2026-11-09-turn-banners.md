# Turn Banners Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline, TDD, one fresh read-only review.

**Goal:** 진영별 턴 배너와 표시 중 전체 플레이어 조작/적 AI 대기를 연결한다.

**Architecture:** TurnManager.TurnStarted → TurnBannerPresenter/TurnBannerAnimation, 독립 턴 표시 잠금 → 기존 입력/카메라/AI 게이트.

**Tech Stack:** Unity6000.5.5f1, IMGUI, C#, NUnit/UnityTest, PNG Resources. 새 패키지 없음.

**Spec:** docs/superpowers/specs/2026-11-09-turn-banners-design.md

## Global Constraints

Git/저장 씬/기체·무기 데이터 변경 없음. 기존 movementSpeed12f, 카메라 pan/zoom60·회전·그림자·엄폐 보정 보존. 원본 PNG 픽셀 그대로 복사. 기존 복제본에서만 Unity 실행.

## Review Focus

초기 턴/왕복 진영 전환 중복·누락, 배너 중 AI/AP 소모 없음, ESC·선택·모든 카메라 입력 차단, 시간 배율0, 전투 종료/비활성화/재초기화의 잠금 누수, 기존 공격 팝업/경계 흐름 보존.

### Task1: 애니메이션과 배너 연결

- [x] 3단계 위치·알파·0.4초 정지 및 초기/양 진영 배너 테스트 RED.
- [x] 진영 이벤트·표시기·원본 이미지 Resources 연결 GREEN.

### Task2: 진행·입력 차단

- [x] 선택/ESC/행동/카메라/AI·비활성화·전투 종료·중복 초기화 테스트 RED.
- [x] 독립 턴 표시 잠금과 카메라/입력/AI 게이트 GREEN.

### Task3: 검증·정리

- [x] 기존 저장 씬/가시성 UI 테스트가 초기 배너를 기다리도록 기대 흐름 갱신.
- [x] 독립 읽기 전용 검토와 중요한 발견 사항 수정.
- [x] 전체 EditMode·그래픽 PlayMode, 원본 자산 보존/PNG·테스트 소스 일치 확인.
- [x] 가이드/README/체크리스트 및 검증 기록 갱신.

Native 연속 실행, 중간 단계 승인 중단 없음. 기준 Artifacts/TurnBannerBefore, 기록 Artifacts/turn-banner-progress.md.
