# Pilot Portrait HUD Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline, TDD and one fresh read-only final review.

**Goal:** 전투 왼쪽 위 유닛 정보의 3D 모델 미리보기를 파일럿 Default 초상화로 교체한다.

**Architecture:** BattleDebugUI.PilotPortrait는 DisplayedUnit 시야/선택 조건을 공유하고 PilotData.DefaultPortrait 우선, 미지정이면 직렬화된 샘플 Sprite를 사용한다. GUI는 Sprite UV와 4:5 비율을 유지한다. 기존 저장 씬 네 개에 fallbackPortrait만 추가하고 기존 샘플 이미지 참조를 사용한다. 3D 미리보기 전용 렌더링과 테스트는 초상화 표시 검증으로 대체한다. 표정 전환/파일럿 편성/전투 계산 변경은 없다.

**Tech Stack:** Unity C#, IMGUI, Sprite, EditMode/Graphics PlayMode.

- [x] 초상화 선택·누락·선택 전환·시야 보호 RED.
- [x] BattleDebugUI 초상화 표시 및 모델 렌더링 제거, 네 저장 씬의 샘플 초상화 연결.
- [x] 기존 모델 테스트를 초상화 테스트로 교체, 실제 그래픽 픽셀/씬 연결 확인.
- [x] 최종 코드 검토, 관련 EditMode/PlayMode 검증, 기존 설정 보존 확인 및 보고.

검증: RED6/6 → EditMode32/32·그래픽 PlayMode8/8. 독립 코드 검토 후 그래픽 테스트의 UV 제외 영역/4:5 비율 확인을 보강하고 재통과. Artifacts/PilotPortraitHUDVerification에 로그/XML/캡처 보관.
