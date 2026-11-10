# Unit Inspection Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline, test-first, one final fresh read-only review.

**Goal:** 양 진영 정보 선택과 녹색/빨간 사각 선택 표시를 연결한다.

**Architecture:** PlayerInputController 선택/행동 권한 분리 → 기존 BattleDebugUI 정보/UnitModelPreview → GridVisualizer 진영별 런타임 재질.

**Tech Stack:** Unity6000.5.5f1, C#, NUnit/UnityTest, Unity CommandBuffer/RenderTexture. 새 패키지 없음.

**Spec:** docs/superpowers/specs/2026-11-09-unit-inspection-design.md

## Global Constraints

Git/저장 씬/데이터/프리팹 변경 없음. movementSpeed12f, camera pan25/zoom60, 그림자·엄폐 보정 유지. 기존 복제본에서만 Unity 실행.

## Review Focus

시야 상실 즉시 정보/외곽선 제거; 적 턴 중에도 적 조종 불가; 공격 대상과 정보 선택의 구분; 미리보기의 원본 메시/재질/레이어/콜라이더 불변; 렌더 자원 해제 및 재초기화; 양손/백팩/빈 슬롯·파괴된 부위 정보.

### Task1: 선택·정보·외곽선

- [x] 이름/HP/무기 포맷과 실제 적 클릭·권한·시야·색상·ESC/허공·공격 조준 테스트 RED.
- [x] Input 선택과 권한, UnitInspectionText, GridVisualizer 진영별 외곽선 GREEN.

### Task2: 외형 표시

- [x] 실제 모델 미리보기 픽셀/원본 불변/해제 테스트 RED.
- [x] UnitModelPreview 렌더 자원 소유·해제, BattleDebugUI 표시/입력 범위 연결 GREEN.

### Task3: 최종 검증

- [x] 독립 읽기 전용 검토 1회 및 중요한 발견 사항 수정.
- [x] 전체 EditMode/그래픽 PlayMode, 기존 저장 씬/행동/시야 회귀와 원본 보존 확인.
- [x] 가이드·README·체크리스트·검증 기록 정리.

Native 연속 실행, 단계별 승인 중단 없음. 기준 Artifacts/UnitInspectionBefore, 기록 Artifacts/unit-inspection-progress.md.


