# CharacterData 사용 안내

`Assets/_Game/Data/Characters/SampleCharacter.asset`을 선택해 Inspector에서 파일럿의 초기 정보를 편집한다.

새 파일럿은 Project 창의 `Create > MechaXCOM > Character Data`로 만들 수 있다. 기본 저장 위치는 `Assets/_Game/Data/Characters/`를 권장한다. 샘플을 복제해도 되지만 각 캐릭터의 ID는 직접 다르게 지정해야 한다. 프로젝트 전체의 ID 중복을 자동 검사하지는 않는다.

## 저장하는 값

| Inspector | 코드 속성 | 의미 |
|---|---|---|
| 캐릭터 ID | CharacterId | 파일럿을 식별하는 문자열 |
| 이름 | DisplayName | 표시 이름. 비어 있으면 에셋 이름 사용 |
| 설명 | Description | 파일럿 설명 |
| 초기 레벨 | InitialLevel | 시작 레벨, 1 이상 |
| 사격 | Shooting | 원거리 전투 기본 능력치 |
| 근접 | Melee | 근접 전투 기본 능력치 |
| 기량 | Technique | 공격자와 대상의 차이로 치명타 확률 계산 |
| 회피 | Evasion | 회피 기본 능력치 |

네 능력치의 에셋 필드는 0 이상의 정수다. 전투에서는 각각 0~50으로 제한하여 사용한다. 샘플의 초기 레벨은 1, 능력치는 모두 0이다.

## 책임과 연결 범위

`CharacterData`는 공유 가능한 초기 정의 에셋이다. 나중에 현재 레벨, 경험치, 성장 포인트와 분배 후 능력치를 관리하는 개별 파일럿 상태를 만들고 초기값을 읽도록 연결한다. 레벨업할 때 이 공유 에셋을 직접 수정하지 않는다.

`BattleUnit` Inspector의 파일럿 데이터에 에셋을 연결한다. 시작 시 사격·근접·기량·회피를 유닛별로 복사하며, 파일럿 미지정은 모두0이다. 에셋을 변경한 효과는 전투를 다시 시작해 확인한다.

원거리 명중은 사격−대상 회피 차이당×.01, 근접 명중은 근접−대상 회피 차이당×.005를 기본 배율1에 더한다. 근접 피해는 공격자의 근접1당×.005를 기본 배율1에 더한다. 기량은 기존 치명타 확률에 적용한다. 자세한 공식과 계수 위치는 `docs/hit-chance-balancing.ko.md`를 참고한다.

레벨업, 패시브 목록과 효과 적용은 아직 연결하지 않았다. 기존 `IPilotAttackPassive`는 C# 계약이므로 Inspector에 연결할 패시브 정의 에셋은 별도 작업이다.
