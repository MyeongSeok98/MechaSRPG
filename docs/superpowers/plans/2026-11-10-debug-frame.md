# Debug Frame Trial Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline with one fresh final read-only review.

**Goal:** 올리브색 프레임을 기존 IMGUI 디버그창 배경에 시범 적용한다.

**Architecture:** IMGUI에서 Sprite Border/PPU를 사용해 9영역을 그린다. 기존 UI 프리팹의 PPU400/Border240과 동일한 크기로 모서리를 보존한다. BattleDebugUI에 선택 Sprite 참조를 추가하고 모서리와 글자가 겹치지 않도록 내용 여백을 둔다. 네 기존 씬에 참조만 추가한다. 프레임 제거 시 사용자가 정한 남색 배경으로 돌아온다. 새 Canvas/HUD 시스템은 만들지 않는다.

- [x] 그리기·프레임 연결·안쪽 여백 구현.
- [x] 기존 초상화/선택/스크롤 및 실제 네 씬 연결 검증, 캡처 확인.
- [x] 최종 읽기 전용 검토 및 기존 자산 보존 확인, 결과 보고.

검증은 기존 관련 그래픽 테스트를 사용한다. 반복 구현을 확인하는 별도 단위 테스트는 추가하지 않는다.

최종 검증: 관련 그래픽 PlayMode8/8 통과. 실제 통신소 씬 캡처 확인. 검토/캡처에서 발견한 내용 너비 넘침을 수정하고 재검증했다. 기존 씬은 panelFrame 참조 외 변경 없으며 데이터·이미지·설정은 유지한다. 기록: Artifacts/DebugFrameVerification.
