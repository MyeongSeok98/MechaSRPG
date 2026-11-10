# 통신소·고정포대 데이터

승인 범위: 통신소와 팔다리 없는 고정포대를 Inspector에서 편집할 수 있는 데이터로 만들고 샘플 에셋을 하나씩 제공한다. 기존 메카 구조를 강제로 사용하지 않는다.

DestructibleObjectData는 ID·표시 이름·설명·단일 최대 HP·선택 모델 프리팹을 정의한다. TurretData가 이 정의를 상속하고 기본 AP·탐지 거리·원거리 WeaponData를 추가한다. 현재 HP/AP/탄약·진영·씬 인스턴스·AI·부위·이동 컴포넌트는 포함하지 않는다. 이 1단계 상속은 실제 두 데이터의 공통 설정 중복을 줄이고 후속 단일 내구도 코드에서 둘을 함께 읽도록 한다.

검증: 공백 ID 거부, HP>=1. 표시 이름 공백은 에셋 이름 대체. 포대 AP>=1, 탐지>=0, 유효한 원거리 무장 필수, 발사 AP는 기본 AP 이하여야 한다. 근접 무장은 거부한다. 모델은 선택이다. 값은 Inspector Min과 IsValid로 검증하며 공유 에셋을 실행 상태로 사용하지 않는다.

샘플 경로는 Data/Objects/SampleCommunicationStation.asset(HP2000), SampleFixedTurret.asset(HP2500/AP5/탐지10/기존 StandardRifle 참조)이다. 모델은 미지정. 샘플 수치는 임시 조절값이다.

실제 오브젝트 배치, 공격/피격, HP 컴포넌트, AI, MissionTarget, 특정 목표 파괴 판정은 이번 범위에 없다. 기존 씬/데이터/모델/이동·카메라·엄폐 튜닝/Git을 변경하지 않는다. Unity는 기존 테스트 복제본에서만 실행한다.
