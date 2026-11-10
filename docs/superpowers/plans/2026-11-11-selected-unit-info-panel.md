# Selected Unit Info Panel Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline with TDD and one fresh final read-only review.

**Goal:** 예시 구성의 실제 선택 기체 정보 패널을 기존 9-slice 기반으로 연결한다.

**Architecture:** 체력→표시용 부위 목록→재사용 행/동적 목록 view. 기존 BattleDebugUI의 선택·시야/초상화 getter를 재사용하고 새 uGUI Canvas 프리팹을 BattleSceneController에서 초기화한다. debug 상세 보기/간략 턴 컨트롤/입력 차단은 기존 클래스로 통합한다.

**Tech Stack:** Unity C#, uGUI, IMGUI legacy debug, NUnit/Graphics PlayMode.

**Spec:** docs/superpowers/specs/2026-11-11-selected-unit-info-panel.md

- [x] 표시 데이터(7부위/1부위/실제 손상)·프리팹 구조 RED.
- [x] IUnitStatusSource/UnitStatusPart, 체력 어댑터, UnitStatusRowUI/SelectedUnitInfoPanel 구현.
- [x] 행/패널 프리팹 생성 도구와 에셋, 기존4씬 참조 추가, debug/턴/입력 연결.
- [x] 선택/HP/AP/상태/1부위·7부위/시야/입력/전환·실제 캡처 검증.
- [x] 최종 검토, 적절한 회귀 검증, 기존 설정 보존, 안내/기록 갱신.

범위: 무장/알림/새 보병·전투기 전투 규칙/표정 전환/기체 데이터 개편 없음. Git 작업 없음. 테스트 복제본 재사용.

최종 검증: EditMode1051/1051·그래픽 PlayMode484/484, 실패·건너뜀0. 독립 최종 검토에서 발견한 결과/안내 누락·UI겹침은 RED→GREEN 검증으로 수정했다. 기존 사용자 장비/파일럿 수치는 보존하고 오래된 테스트 가정만 수정했다. 데이터·이미지·설정 변경0, 네 씬은 패널 참조만 추가. 기록: Artifacts/SelectedUnitPanelVerification 및 Artifacts/selected-unit-panel-progress.md.
