# 선택 기체 정보 패널

기체를 선택하면 왼쪽에 실제 uGUI 패널이 나타납니다. 기존 올리브색 반투명9-slice 배경을 사용합니다.

- 기체명과 파일럿명
- 파일럿 Default 초상화(미지정 시 기존 샘플 대체)
- 현재/최대 AP
- CONDITION: 전투 가능하면 ACTIVE, Critical이면 INACTIVE
- STATE: READY, MOVING, FACING, OVERWATCH, WAITING, NO AP, CRITICAL 또는 적 정보 확인용 INSPECT ONLY
- UNIT STATUS: 부위명·체력바·현재/최대 HP 숫자

WEAPON LOADOUT에는 왼손(L)·오른손(R)·백팩(B)의 무기명, 잔탄/장탄 수와 예비 탄창을 표시합니다. 양손 무기는 LR 한 행이며 백팩은 NO RELOAD로 표시합니다. 팔 파괴로 사용할 수 없는 무장은 목록에 남아 빨간색 ‘사용 불가’로 표시되고, AP 부족·탄약 없음은 주황색으로 구분됩니다. 양손/한손 겸용 무기의 자동 손 전환도 바로 반영합니다. 무기 자체에 별도 HP를 추가하지 않았습니다.

AP는 숫자만 표시하며 원형 게이지와 노란 경고 박스는 없습니다. 공격/재장전은 기존 행동 메뉴를 사용합니다. 임무·현재 턴·End Turn·결과/안내는 오른쪽 작은 컨트롤에 남아 있습니다. 조준 UI가 열려 있을 때는 오른쪽 턴 컨트롤을 숨겨 겹치지 않게 합니다.

## 프리팹과 연결

- `Assets/_Game/Prefabs/UI/SelectedUnitInfoPanel.prefab`: Canvas·패널 배치·초상화·상태·목록
- `Assets/_Game/Prefabs/UI/UnitStatusRow.prefab`: 부위 한 줄
- `Assets/_Game/Prefabs/UI/UnitWeaponRow.prefab`: 무장 한 줄
- 기존 네 씬의 Battle Systems → BattleSceneController → **선택 기체 정보 패널**에 프리팹이 연결되어 있습니다. 시작 시 한 번 생성하며 씬에 따로 또 배치하지 않아도 됩니다.

프리팹 모드에서 텍스트/색상/여백/기본 너비를 편집할 수 있습니다. 패널 높이는 ContentSizeFitter와 부위 목록 높이로 결정합니다. 목록이 짧으면 패널도 줄고, 길면 상태 영역만 스크롤합니다. 숫자 HP와 AP는 기존 Oxanium 리소스를 사용하며 일반 이름에는 한글 표시용 폰트를 적용합니다.

F3로 기존 상세 디버그창과 전환합니다. 선택을 해제하거나 적을 놓치면 선택 정보도 숨깁니다. 적은 정보 확인만 가능하고, 패널 위 클릭은 월드 행동으로 전달되지 않습니다. 목록에서 휠을 사용해도 카메라가 함께 줌하지 않습니다.

## 유동적인 부위 수

UI는 `IUnitStatusSource.GetStatusParts(List<UnitStatusPart>)`가 제공한 목록만큼 행을 재사용합니다. 현재 UnitHealth는 Head·Body·C.Pit·L.Arm·R.arm·L.Leg·R.Leg 순서의7개를 제공하고 CoverDurability는 단일 Body를 제공합니다. 체력/파괴 규칙은 변경하지 않았습니다.

표시용 UnitStatusPart는 ID·이름·현재 HP·최대 HP를 가집니다. HP는 실제 상태에서 읽으며 공유 데이터에 복사해서 변경하지 않습니다. 정상 바는 녹색, 절반 미만은 주황, 파괴된 부위는0 HP 숫자와 빈 바로 구분합니다.

향후 단일 HP 보병/전투기는 해당 체력 소스를 연결해 동일한 목록 뷰를 사용할 수 있습니다. 현재 보병/전투기 전투 구현을 추가한 것은 아니며, 자동 선택 연결은 기존 BattleUnit의 UnitHealth를 사용합니다. 패널의 `ShowUnit`은 별도 IUnitStatusSource도 받아1부위·많은 부위를 표시할 수 있습니다.

프리팹 생성 도구는 `MechaXCOM/Create Selected Unit Info Panel Prefabs`입니다. 이미 생성된 패널이 있으면 덮어쓰기를 거부합니다. 기존 에셋은 프리팹 모드로 편집하세요.

컴팩트 버전은 너비390, 기체명28px·AP32px·부위명/HP16px, 부위 행26px/간격1px를 기본으로 합니다. `MechaXCOM/Apply Compact Unit Info Layout`은 기존 프리팹 GUID를 보존하며 이 레이아웃과 무장 영역을 적용하는 명시적 갱신 도구입니다. 직접 프리팹 배치를 바꿨다면 이 도구를 다시 실행할 때 해당 배치 값이 기본값으로 돌아갑니다.

## 검증

2026-11-11: 전체 EditMode1051/1051·그래픽 PlayMode484/484 통과, 실패·건너뜀0. 1/7/20부위, 실제 HP/AP 갱신, Critical, 시야 상실, F3 전환, UI클릭/휠 차단, 턴 배너 스크롤 잠금, 임무 성공·실패/공격 안내, 조준창/행동 메뉴 겹침 방지와 네 저장 씬의 실제 표시를 검증했습니다. 기존 데이터·이미지·카메라/그림자 설정은 유지했습니다. 기록과 캡처는 `Artifacts/SelectedUnitPanelVerification/`에 있습니다.

2026-11-11 컴팩트 무장 목록 적용 후: 전체 EditMode1055/1055·그래픽 PlayMode486/486 통과, 실패·건너뜀0. 팔 파괴 빨간 표시/공격 차단, 자원 부족 구분, 양손 무기와 손 전환, 섹션 제목 표시를 추가 검증했습니다. 기록/화면: Artifacts/CompactUnitWeaponsVerification/.

체력바는 HealthBarFill.png의 불투명 사각형 Sprite를 사용합니다. 최대 체력일 때 트랙 전체를 채우며 체력 감소 시 Horizontal Filled 비율로 줄어듭니다. 안쪽 여백은0이며, 프리팹 재생성/컴팩트 레이아웃 적용 시에도 동일하게 유지됩니다. 사각형 바 적용 후 실제 씬 캡처와 관련 그래픽 PlayMode2/2 통과 (Artifacts/SquareHealthBarsVerification).

무장 목록은 높이28px의 한 줄 배치입니다. 청록색 슬롯 테두리 → 무기명 → 구분선 → AMMO 현재/최대 → 구분선 → RESERVE 예비 수 순서이며, 백팩은 NO RELOAD로 표시합니다. AP·탄약 부족/사용 불가 안내는 오른쪽 예비 수 자리에 표시됩니다. 기존 무기 사용 판정과 빨간색/주황색 구분은 유지합니다.

한 줄 무장 목록 변경 후 관련 EditMode7/7·그래픽 PlayMode16/16 통과. 실제 씬 화면과 팔 파괴 사용 불가 표시·자원 상태 갱신 확인: Artifacts/SingleLineWeaponRowsVerification/.

슬롯 테두리는 축소 화면에서도 선이 빠지지 않도록 박스 안쪽에2px 두께로 배치합니다. 기존1px 선이 Canvas 축소 시1픽셀 미만으로 내려가 일부 변이 사라지는 현상을 수정했습니다. 960×540 실제 씬 전후 캡처 및 관련 PlayMode1/1 통과: Artifacts/SlotBadgeBorderVerification/. 검증용 해상도 변경은 테스트 복제본에서만 임시 적용 후 복구했습니다.


현재 선택 기체 패널의 제목 폰트는 Saira Stencil SemiBold, 본문 폰트는 Oxanium Regular입니다.

- Saira Stencil: 기체명·파일럿명·UNIT STATUS·WEAPON LOADOUT
- Oxanium: AP·상태·부위명/HP·무기명·슬롯·AMMO/RESERVE
- 한글이 포함된 문구: 기존 맑은 고딕/Arial 대체 폰트

SelectedUnitInfoPanel 프리팹의 ‘제목 폰트’와 ‘본문 폰트’에서 변경합니다. Saira는 글자에 비해 행 상자가 커서 네 제목은 세로 Overflow를 허용해 전체 행이 사라지는 현상을 방지합니다. 슬롯 위치·체력바·선택/행동 규칙은 유지합니다.

Saira Stencil 출처: https://github.com/Omnibus-Type/Saira_Stencil . 공식 정적 SemiBold TTF와 OFL.txt를 Assets/_Game/Art/UI/Fonts/SairaStencil/에 함께 보관합니다. 이전 Stencilia 파일은 프로젝트에 남아 있지만 현재 패널에서는 참조하지 않습니다.

관련 그래픽 PlayMode17/17 통과. 네 제목의 실제 글자 메시, 본문 폰트 구분, 한글 이름 전환, 기존 상태·무장 정보 갱신과 저장 씬 캡처 확인: Artifacts/SairaOxaniumUIVerification/.
