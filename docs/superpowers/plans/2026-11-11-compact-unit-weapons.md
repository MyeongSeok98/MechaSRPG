# Compact Unit Info and Weapons Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline with TDD and one fresh read-only final review.

**Goal:** 승인한 구성대로 정보창 글씨 확대/간격 축소, AP 숫자 유지, 무장 목록과 파괴로 사용 불가인 무장의 빨간 표시를 추가한다.

**Design approved by user:** 기존 반투명9-slice 유지. 이름·초상화·AP·활성/현재 상태·유동 부위 HP 유지. AP 원형 게이지 없음. 무기명·잔탄·예비 탄창, 양손 무기 한 행, 백팩 한 행. UnitCombat.TryGetAttack을 읽어 구조적 사용 불가를 빨간색으로 표시하고 AP/탄약 부족은 별도 주황 표시. 무기 자체 HP/신규 전투 규칙/노란 박스 없음. 목록은 읽기 전용이며 공격은 기존 메뉴를 사용한다.

**Architecture:** UnitWeaponInfo 읽기 전용 데이터 → UnitWeaponRowUI 재사용 행 → SelectedUnitInfoPanel의 기존 선택/시야/표정 대체/잠금 흐름. 프리팹은 기존 GUID/연결을 보존하며 명시적 업데이트 메서드로 갱신한다. 메카/무기/파일럿 수치와 씬 배치는 바꾸지 않는다.

- [x] 무장 상태·양손/전환·팔 파괴·AP/탄약 구분 RED.
- [x] 무장 데이터와 행/패널 연결, 부위 행 크기/간격 설정값 구현.
- [x] 기존 프리팹 폰트/레이아웃 갱신 및 새 무장 행 프리팹 생성.
- [x] 실제 목록 갱신/빨강 표시/사격 차단/레이아웃과 기존 UI 회귀/캡처 검증.
- [x] 최종 검토·필요한 전체 검증·원본 보존·안내/기록 정리.

Tech: Unity C#, uGUI, NUnit/Graphics PlayMode. Git 작업 없음. 기존 테스트 복제본 사용.

