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
| 기량 | Technique | 정밀 조작 기본 능력치 |
| 회피 | Evasion | 회피 기본 능력치 |

네 능력치는 0 이상의 정수다. 샘플의 초기 레벨은 1, 능력치는 모두 0이다. 이는 편집용 시작값이며 전투 밸런스 기준을 확정한 수치가 아니다.

## 책임과 연결 범위

`CharacterData`는 공유 가능한 초기 정의 에셋이다. 나중에 현재 레벨, 경험치, 성장 포인트와 분배 후 능력치를 관리하는 개별 파일럿 상태를 만들고 초기값을 읽도록 연결한다. 레벨업할 때 이 공유 에셋을 직접 수정하지 않는다.

현재는 데이터와 샘플 에셋만 존재한다. BattleUnit 배정, 명중률·회피 계산, 레벨업, 패시브 목록과 효과 적용은 연결하지 않았다. 기존 `IPilotAttackPassive`는 C# 계약이므로 Inspector에 연결할 패시브 정의 에셋은 별도 작업이다.
