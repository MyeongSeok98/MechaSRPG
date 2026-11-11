# Animated Unit Command Wheel Implementation Plan

> Execute inline with superpowers:executing-plans and one fresh read-only final review. User approved preview variant 2; no Git operations or new worktree.

**Goal:** 아군 선택 시 초록색 링 회전/등장 후 연결선과 타이핑 글자가 함께 표시된다. Move/Attack/Overwatch / AP 2/Eject / AP 1, 좌우 같은 선 길이와 오른쪽 끝 정렬. 선택한 행동은 초록색 외곽/회색 내부로 표시한다.

**Architecture:** 기존 UnitActionUI가 기존 입력 명령을 호출한다. CommandWheelAnimation은 시간에 따른 표시만, UnitCommandWheel은 IMGUI 링/선/문구/선택 표시와 클릭 영역을 담당한다. 선택 성공 시에만 애니메이션 시작을 알린다. 새 Canvas/씬 시스템은 만들지 않는다.

**Scope decisions:** 이동/공격 대기 중에도 선택된 행동을 보이되 네 버튼과 Reload만 클릭을 소비한다. 링 중앙/빈 공간은 맵 클릭을 통과한다. 애니메이션은 행동 입력을 잠그지 않는다. 기존 재장전은 아래 작은 버튼으로 유지한다. 무기 선택/이동 후 방향 선택/공격 확정의 기존 기능은 유지한다. 대상 조준/방향 선택의 기존 세부 패널은 유지한다. HUD/화면 경계와 겹치면 메뉴 위치를 보정한다. Inspector 조절 값은 애니메이션 속도/색상이며 게임 데이터·씬 배치·카메라 수치는 바꾸지 않는다.

- [x] RED: 링 이후 선/글자 동시 시작, 완료 타이밍, 이동 선택 표시/중앙 클릭 통과.
- [x] 순수 애니메이션 모델, 링/연결선/문구/회색 선택 배경 렌더러 구현.
- [x] UnitActionUI 기존 동작·재장전·무기/방향/조준 메뉴와 연결. 선택 성공 시 재생 시작.
- [x] 의미가 달라진 기존 UI 숨김 검증을 새 계약으로 갱신. 실제 클릭/ESC/턴 잠금/적 선택/화면 경계 회귀 및 캡처.
- [x] 독립 최종 검토, 필요한 전체 검사, 원본 보존 확인과 사용 안내.

Validation: existing isolated Unity clone Artifacts/MeleeRangedTestProject only, sequential Editor invocations. Edit animation RED/GREEN → relevant graphics PlayMode → full suites once stable. Tests do not assert incidental font sizes or line-segment constants. A runtime selection notification handles same-unit reselection without AP/camera changes restarting the animation.

