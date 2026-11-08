# Movement AP Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans; test-first; one fresh read-only final reviewer.

**Goal:** 기체별 직접 지정 이동 효율로 턴 누적 AP 이동을 구현한다.

**Architecture:** MechaData → BattleUnit 기본/최종 효율 → 순수 MovementCostLedger → UnitMovement 칸 확정. 공통 장부 비용을 범위/BFS·UI·AI에 재사용하고 자기 턴 시작만 장부를 초기화한다.

**Tech Stack:** Unity6000.5.5f1, C#, NUnit/UnityTest.

**Spec:** docs/superpowers/specs/2026-11-08-movement-ap-design.md

## Global Constraints

Git 금지. 원본 에셋/씬/속도12f/카메라/그림자/엄폐 수치 보존. 지형 가중치·부품 장착·AI 전략 확장은 이번 범위에 없다.

## Review Focus

AP0 선불 이동, 이동-공격-이동, 분수 누적 경계, 이동 중 다리 파괴, 취소/재초기화/턴 리셋과 비용/점유 콜백의 일관성.

### Task1: 장부와 초기값

Files: 새 Unit/MovementCostLedger.cs, Unit/BattleUnit.cs, Unit/UnitMovement.cs. Tests: 새 EditMode/MovementAPTests.cs.

- [x] 순수 장부/분수/동적 효율/선불0·극단 수치/스냅샷/범위 테스트 RED.
- [x] 장부와 기본/최종 효율 조회, 예산 조회 API 구현 및 GREEN.

### Task2: 실제 이동·턴·AI·표시

Files: Unit/UnitMovement.cs, Turn/TurnManager.cs, Grid/Pathfinder.cs (필요시), AI/EnemyAIController.cs, Presentation/BattleDebugUI.cs, Editor/MechaDataEditor.cs. Tests: 새 PlayMode/MovementAPPlayTests.cs.

- [x] 이동-사격-이동/분할/중단/다리/자기 턴/AI 예산 테스트 RED.
- [x] 칸 확정 전 지불, 선불 잔여·AP0 이동, 자기 턴 장부 리셋, AI 비용 검사 연결.
- [x] 기존 이동·경계·방향·AI 집중 테스트 GREEN.

### Task3: 전체 검증

- [x] 현재 가이드/체크리스트/README 갱신.
- [x] 독립 읽기 전용 최종 검토 및 발견 사항 처리.
- [x] 전체 EditMode/그래픽 PlayMode, 테스트 소스 일치/에셋 보존 해시 확인.

기준 Artifacts/MovementAPBefore. 기록 Artifacts/movement-ap-progress.md. 사용자 승인한 연속 Native 실행을 유지하고 Git은 사용자가 담당한다.

완료: EditMode885/885, 그래픽 PlayMode415/415, 실패·건너뜀0. 독립 검토의 소수 경계·비활성 턴 리셋·오래된 경로 과금 문제를 RED→GREEN으로 수정했다. 원본 에셋/설정 해시 변경0. 그래픽 러너의 네이티브 충돌과 남은 소유 테스트 픽스처는 증거 보존/별도 보관 후 동일 코드의 전체 재실행으로 검증을 마쳤다.
