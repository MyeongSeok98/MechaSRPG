# MissionController 기반 종료

사용자 승인 범위: MissionController가 임무별 성공/실패 요청과 공통 아군 전멸 실패를 결정하고, 기존 TurnManager는 종료 요청에 따라 전투를 중단한다. 시설/호위/철수 구역/MissionData/결과 화면은 구현하지 않는다.

공통 실패는 초기 등록 아군 중 IsAlive(IsPresent: Active 또는 Critical)가 하나도 없는 경우다. 기존처럼 파괴/탈출/오브젝트 제거는 남은 전장 아군에서 제외하고 Critical은 유지한다. 양측 동시 제거는 실패 우선이다. 비활성화만으로 죽은 것으로 보지 않는다.

MissionController는 결과(진행/성공/실패와 이유), 완료 이벤트, TrySucceed/TryFail API를 제공한다. 요청은 피해 처리/공격 팝업 종료 후 평가한다. 완료는 한 번만 발생한다. TurnManager.CheckBattleEnd는 미션 평가를 위임하고, 결과를 받아 기존 이동/AI/경계/턴 배너 중단을 실행한다. 결과 이벤트는 정리 후 발생한다.

TurnManager.Initialize는 지정 또는 같은 오브젝트의 MissionController를 사용한다. 없는 경우에만 기존 씬/테스트 호환용 MissionController를 자동 추가하고 적 전멸 승리를 켠다. 직접 추가한 MissionController는 기본적으로 적 전멸 자동 승리를 사용하지 않는다. 임무 설정 bool은 Inspector에 노출하며 런타임 요청을 통해 향후 목표 코드를 연결할 수 있다. 동일 관리자 재초기화는 무시하고 다른 관리자로 재결합은 금지한다.

기존 씬/수치/모델/Git 변경 없음. Unity는 기존 테스트 복제본에서만 실행한다.
