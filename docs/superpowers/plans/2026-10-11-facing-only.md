# 이동 후 방향 선택 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 이동 후 4방향을 선택·확정하고 모델의 정면이 실제로 바뀌는 것을 확인한다.
**Architecture:** BattleUnit에 확정 방향·대기 상태를 두고 UnitMovement가 회전한다. TurnManager는 대기·회전을 행동 잠금에 포함하고 기존 입력·행동 패널에서 4방향과 Confirm을 제공한다.
**Tech Stack:** 기존 Unity/C#/Input System/IMGUI/Unity Test Framework.
**Spec:** [기존 설계](../specs/2026-10-09-facing-part-exposure-design.md)의 방향 선택 부분. 사용자의 후속 지시로 부위 노출·방향별 명중률 보정은 이번 범위에서 전부 제외한다. 기존 facing-part-exposure 계획 대신 이 축소 계획을 실행한다.

## Global Constraints

- 이동 완료 → 북·동·남·서 선택 → 부드러운 확정 회전. +Z가 정면이며 별도 AP 소모 없음.
- AP 0에서도 확정한다. 대기·회전 중 다른 행동과 턴 종료를 차단한다.
- 대기 ESC는 도착 방향으로 확정하고 선택 해제, 확정 회전 중 ESC는 무시한다.
- 이동 중 선택 해제 후 도착하면 방향 선택이 열린다. 적은 마지막 이동 방향을 자동 유지한다.
- 루트/Collider와 이동속도 12f, 씬/무기/모델/엄폐/공격 코드는 보존한다.
- Git 초기화 없이 기존 분리 검증 프로젝트를 순차 사용한다. Native로 하나의 완성된 방향 기능을 구현하고 사용자 확인을 받는다.

## Review Focus

- AP 0·Visual 없음·같은 방향 확정에서도 잠금 해제.
- 도착 전 선택 해제 후 방향 패널 재표시.
- 직접 API로 이동·공격·탈출·턴 종료 우회 금지.
- 취소·Critical·파괴·비활성화·전투 종료 중 정리.
- 카메라 회전과 무관한 월드 4방향, 사용자 자산 보존.

## Task 1: 방향 선택 기능 전체 연결

Files: 새 Scripts/Unit/FacingDirection.cs, 기존 BattleUnit.cs/UnitMovement.cs/TurnManager.cs/PlayerInputController.cs/UnitActionUI.cs/BattleDebugUI.cs, 기존 PlayMode 테스트.

Interfaces: FacingDirection(North,East,South,West), BattleUnit.Facing/HasPendingFacing/IsConfirmingFacing, UnitMovement.TryConfirmFacing(FacingDirection)/TryKeepArrivalFacing(), TurnManager.PendingFacingUnit/IsAnyUnitBusy, PlayerInputController.CanChooseFacing/SelectedFacing/TryChooseFacing/TryConfirmFacing, UnitActionUI.GetFacingButtonRect/ConfirmFacingButtonRect.

- [x] 실제 이동 완료 후 Facing 모드와 패널이 표시되는 테스트를 먼저 작성하고 RED 확인.
- [x] 방향 상태·이동 완료 기록·확정 회전·취소 복원 구현. 공격/명중률은 수정하지 않음.
- [x] 전체 행동 잠금·ESC·선택 복원과 기존 행동 패널의 4방향/Confirm 연결.
- [x] 실제 방향 버튼 클릭, AP 0, 직접 API 잠금, 회전 취소, 모델 없는 적을 검증. 기존 테스트의 이동 후 다음 행동 앞에는 기본 방향 확정을 명시적으로 추가.
- [x] 전체 EditMode/PlayMode 회귀, 보호 자산 SHA-256 확인, 별도 Native 최종 리뷰 후 결과 보고.

검증 명령은 Artifacts/MeleeRangedTestProject에서 tools/Run-UnityTests.ps1을 실행한다. RED/GREEN 및 관련 단계는 필터로 실행하고 최종 검증은 두 모드를 순차 실행한다. 실제 테스트 결과와 변경 내역은 Artifacts/facing-only-progress.md에 기록한다.

