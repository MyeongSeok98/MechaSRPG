# 선택 기체 정보 패널

기체를 선택하면 왼쪽에 실제 uGUI 패널이 나타납니다. 기존 올리브색 반투명9-slice 배경을 사용합니다.

- 기체명과 파일럿명
- 파일럿 Default 초상화(미지정 시 기존 샘플 대체)
- 현재/최대 AP
- CONDITION: 전투 가능하면 ACTIVE, Critical이면 INACTIVE
- STATE: READY, MOVING, FACING, OVERWATCH, WAITING, NO AP, CRITICAL 또는 적 정보 확인용 INSPECT ONLY
- UNIT STATUS: 부위명·체력바·현재/최대 HP 숫자

무장 목록과 노란 알림 박스는 포함하지 않았습니다. 공격/재장전 등 기존 행동 메뉴는 유지합니다. 임무·현재 턴·End Turn·결과/안내는 오른쪽 작은 컨트롤에 남아 있습니다. 조준 UI가 열려 있을 때는 오른쪽 턴 컨트롤을 숨겨 겹치지 않게 합니다.

## 프리팹과 연결

- `Assets/_Game/Prefabs/UI/SelectedUnitInfoPanel.prefab`: Canvas·패널 배치·초상화·상태·목록
- `Assets/_Game/Prefabs/UI/UnitStatusRow.prefab`: 부위 한 줄
- 기존 네 씬의 Battle Systems → BattleSceneController → **선택 기체 정보 패널**에 프리팹이 연결되어 있습니다. 시작 시 한 번 생성하며 씬에 따로 또 배치하지 않아도 됩니다.

프리팹 모드에서 텍스트/색상/여백/기본 너비를 편집할 수 있습니다. 패널 높이는 ContentSizeFitter와 부위 목록 높이로 결정합니다. 목록이 짧으면 패널도 줄고, 길면 상태 영역만 스크롤합니다. 숫자 HP와 AP는 기존 Oxanium 리소스를 사용하며 일반 이름에는 한글 표시용 폰트를 적용합니다.

F3로 기존 상세 디버그창과 전환합니다. 선택을 해제하거나 적을 놓치면 선택 정보도 숨깁니다. 적은 정보 확인만 가능하고, 패널 위 클릭은 월드 행동으로 전달되지 않습니다. 목록에서 휠을 사용해도 카메라가 함께 줌하지 않습니다.

## 유동적인 부위 수

UI는 `IUnitStatusSource.GetStatusParts(List<UnitStatusPart>)`가 제공한 목록만큼 행을 재사용합니다. 현재 UnitHealth는 Head·Body·C.Pit·L.Arm·R.arm·L.Leg·R.Leg 순서의7개를 제공하고 CoverDurability는 단일 Body를 제공합니다. 체력/파괴 규칙은 변경하지 않았습니다.

표시용 UnitStatusPart는 ID·이름·현재 HP·최대 HP를 가집니다. HP는 실제 상태에서 읽으며 공유 데이터에 복사해서 변경하지 않습니다. 정상 바는 녹색, 절반 미만은 주황, 파괴된 부위는0 HP 숫자와 빈 바로 구분합니다.

향후 단일 HP 보병/전투기는 해당 체력 소스를 연결해 동일한 목록 뷰를 사용할 수 있습니다. 현재 보병/전투기 전투 구현을 추가한 것은 아니며, 자동 선택 연결은 기존 BattleUnit의 UnitHealth를 사용합니다. 패널의 `ShowUnit`은 별도 IUnitStatusSource도 받아1부위·많은 부위를 표시할 수 있습니다.

프리팹 생성 도구는 `MechaXCOM/Create Selected Unit Info Panel Prefabs`입니다. 이미 생성된 패널이 있으면 덮어쓰기를 거부합니다. 기존 에셋은 프리팹 모드로 편집하세요.

## 검증

2026-11-11: 전체 EditMode1051/1051·그래픽 PlayMode484/484 통과, 실패·건너뜀0. 1/7/20부위, 실제 HP/AP 갱신, Critical, 시야 상실, F3 전환, UI클릭/휠 차단, 턴 배너 스크롤 잠금, 임무 성공·실패/공격 안내, 조준창/행동 메뉴 겹침 방지와 네 저장 씬의 실제 표시를 검증했습니다. 기존 데이터·이미지·카메라/그림자 설정은 유지했습니다. 기록과 캡처는 `Artifacts/SelectedUnitPanelVerification/`에 있습니다.
