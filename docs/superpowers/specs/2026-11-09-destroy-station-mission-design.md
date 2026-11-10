# 통신소 파괴 샘플 임무

사용자 요청은 통신소 파괴 임무의 플레이 가능한 샘플 씬(아군2/적2)이다. 데이터뿐 아니라 실제 공격·HP·목표 연결을 완성한다. 첫 샘플은 기존 직사 폭발무기 경로를 재사용한다. 일반 라이플/빔의 시설 공격과 고정포대 AI는 범위 밖이다.

MissionData DestroyTarget은 단일 targetObjectId를 요구한다. MissionTarget은 씬의 고유 ID와 UnitHealth 또는 CoverDurability 중 하나를 참조한다. ID 중복/누락/모호한 내구도/비활성·잘못된 정의를 전투 시작 전에 거부한다. 대상 기체는 전투 명단의 적이어야 한다. 시설 HP는 CoverDurability 하나만 관리하고 선택 DestructibleObjectData에서 최대값을 읽는다. 기존 엄폐물은 프리팹 maxHP를 계속 사용한다. HP값을 이중 유지하지 않으며 런타임 공유 데이터 변경/재설정은 없다.

IMissionObjective 공통 진행도 인터페이스로 기존 EliminationObjective와 새 DestroyTargetObjective를 읽는다. 새 목표는 초기 바인딩한 내구도의 실제 파괴만 기록한다. 단순 삭제/비활성화/기체 Critical/탈출은 인정하지 않는다. 파괴 기록은 오브젝트 제거 후에도 유지되고 완료/제거 시 구독 해제한다. 공통 아군 전멸 실패와 피해/팝업 종료 대기·중복 종료 방지는 기존 MissionController/TurnManager 경로를 유지한다. 목표 외 적이 살아 있어도 통신소 파괴 시 성공한다.

새 씬 DestroyCommunicationStationTest는 SqurdWeaponTest를 별도로 복사한다. 새 씬 안에서만 기존 장애물/엄폐물을 정리해 명확한 샘플 구역을 만들고, 아군2·적2·통신소1을 배치한다. 원본의 그리드 크기·카메라/이동 속도 설정은 보존한다. 양 아군에 새 테스트용 직사 폭발무기(Demolition Launcher, 피해1000/AP3/장탄3)를 연결해 HP2000 통신소를 두 번 맞혀 검증할 수 있게 한다. 기존 무기/기체/통신소 데이터는 수정하지 않는다. 생성기는 원본 씬이 이미 열려 있거나 목적 자산이 있으면 거부한다.

씬 목표와 신규 MissionData/WeaponData만 생성하며 기존 파일의 튜닝·배치·Git은 변경하지 않는다. Unity는 기존 복제본에서만 실행하고 새로 생성한 자산과 .meta를 원본으로 전달한다.
