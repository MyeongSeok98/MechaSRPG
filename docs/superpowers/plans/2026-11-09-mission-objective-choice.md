# Mission objective choice

**Goal:** MissionData Inspector에 적 전멸/특정 목표 파괴 선택만 추가한다. 기존 데이터의 기본값0은 적 전멸이며 기존 씬/에셋을 수정하지 않는다.

**Scope:** MissionData에 enum과 직렬화 필드, 종류별 데이터 검증. Editor는 적 전멸에만 집단 ID를 표시하고 미구현 시설 파괴를 안내한다. MissionController는 미지원 종류가 섬멸로 실행되지 않도록 시작을 거부한다. 시설 타깃/HP/공격/목표 실행 상태/씬은 추가하지 않는다.

- [x] 선택 저장·레거시 기본값·종류별 검증·미지원 실행 차단 RED.
- [x] 데이터와 Inspector·시작 검증 구현.
- [x] 전체 EditMode와 실제 섬멸 PlayMode 회귀 확인.
- [x] 기존 자산 보존 확인과 안내 갱신.

기존 테스트 복제본에서만 Unity 실행, 사용자 설정/기존 씬/데이터/Git 변경 없음.
