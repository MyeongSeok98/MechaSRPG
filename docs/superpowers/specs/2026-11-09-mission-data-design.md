# MissionData

승인 범위는 Inspector에서 편집 가능한 MissionData 구현 후 섬멸 목표의 다음 구현 방식을 설명하는 것이다. 기존 MissionController의 결과 판단/전투 종료는 바꾸지 않는다.

ScriptableObject 필드: 임무 ID, 표시 이름, 여러 줄 설명, 지역 표시 이름, 섬멸 대상 적 집단 ID. 대상 수는 중복 설정하지 않고 후속 실행 상태에서 등록 집단으로 계산한다. 공백 ID는 검증 실패, 이름 공백은 에셋 이름으로 표시한다. 설명/지역은 비어 있어도 작성 중 데이터로 허용한다.

한글 Inspector와 기존 프로젝트 방식의 에러 안내, Create 메뉴, 편집용 샘플 에셋1개를 제공한다. 런타임 진행 수/성공 상태/씬 오브젝트 참조를 저장하지 않는다. 적 집단 등록/Destroyed 목표 판정/MissionController 연결/씬 자동 로딩/생포/철수/결과 화면은 이번 범위에 없다.

샘플: Assets/_Game/Data/Missions/SampleEliminationMission.asset. ID sample_elimination, 지역 남부 폐허, 대상 EnemySquad_A. 실제 씬에 연결하지 않는다.

기존 씬/데이터/카메라·이동속도·엄폐 튜닝/Git 보존. 기존 복제본에서 Unity EditMode 직렬화·유효성·샘플과 전체 데이터 회귀를 검증한다.
