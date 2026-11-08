# Combat Feedback Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline, test-first, one final fresh read-only reviewer.

**Goal:** 모든 공격 결과의 부위별 영어 팝업과 공통 행동 대기를 연결한다.

**Architecture:** CombatSystem 결과 스냅샷/이벤트 → CombatFeedbackPresenter 표시와 대기 요청 → TurnManager/Overwatch/AI 재개.

**Tech Stack:** Unity6000.5.5f1, C#, NUnit/UnityTest. 새 패키지 없음.

**Spec:** docs/superpowers/specs/2026-11-08-combat-feedback-design.md

## Global Constraints

Git 금지, 기존 저장 씬·데이터·모델·프리팹 불변. movementSpeed12f, pan25, zoom60, 그림자·엄폐 보정 불변. 기존 복제본에서만 테스트한다.

## Review Focus

공격 전 플레이어 시야 스냅샷, 죽은 대상 위치 보존, 마지막 공격의 종료 지연, 경계 다음 결정/이동 대기, 표시기 해제/중복 초기화 시 잠금 누수 및 자원 중복 소모.

### Task1: 결과 데이터·영어 부위명

- [x] EditMode 영어 부위명/피해·MISS·치명타/부위 파괴 포맷 RED.
- [x] CombatFeedback 데이터/텍스트, CombatSystem 공통 이벤트와 직사·곡사·엄폐물 연결 GREEN.

### Task2: 표시·진행 제어

- [x] PlayMode 일반/빗나감/곡사·미탐지/엄폐물/치명타·종료/비활성화·AI·경계 대기 RED.
- [x] CombatFeedbackPresenter, TurnManager 대기/종료, 경계 대기, BattleSceneController 런타임 연결 GREEN.

### Task3: 검증·문서

- [x] 독립 읽기 전용 최종 검토와 발견 사항 수정.
- [x] 전체 EditMode/그래픽 PlayMode, 저장 에셋 보존/소스 일치 확인.
- [x] 사용 문서·README·체크리스트, 검증 기록 정리.

단계별 승인 중단 없이 Native 연속 실행. 기준 Artifacts/CombatFeedbackBefore, 기록 Artifacts/combat-feedback-progress.md.


