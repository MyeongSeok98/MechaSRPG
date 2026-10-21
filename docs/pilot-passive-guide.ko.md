# 파일럿 공격 패시브 확장 구조

이번 코드는 공격 시점의 패시브 계약만 준비한다. 현재 전투에는 연결하지 않았으며, Inspector에서 파일럿이나 패시브를 지정하는 기능도 아직 없다.

## 각 코드의 역할

- `IPilotAttackPassive`: 고유 ID, 발동 조건, 효과 반환을 정의한다.
- `PilotAttackContext`: 공격자·대상·무기·그립·조준 부위·거리·이동량을 전달한다. 패시브 설명 문구를 담는 데이터가 아니다.
- `PilotAttackModifiers`: 개별 패시브의 공격 AP 감소량과 명중률 배율이다. 기본값은 감소 0, 배율 1이다.
- `PilotAttackPassiveEvaluator`: 여러 패시브 중 조건을 만족한 효과를 각각 반환한다.

## 새 패시브 작성 예

아래는 일반 C# 구현 예시다. 이 예시는 게임에 등록되어 있지 않다.

```csharp
public sealed class StationaryFirePassive : IPilotAttackPassive
{
    public string Id => "stationary_fire";

    public bool CanApply(PilotAttackContext context) =>
        context.AttackType == AttackType.Ranged && context.MovedCells == 0;

    public PilotAttackModifiers GetModifiers(PilotAttackContext context) =>
        new PilotAttackModifiers(accuracyMultiplier: 1.1f);
}
```

`Evaluate(passives, context)`에 여러 구현을 전달하면 발동한 효과를 입력 순서로 받는다. 같은 ID가 중복되거나 ID가 비어 있으면 설정 오류를 던진다. null 목록은 빈 결과이며 null 항목은 건너뛴다. ID는 대소문자를 구분한다.

효과는 아직 합산하거나 곱하지 않는다. AP 할인 중 최대값 선택, 최종 AP 최솟값, 명중률 중첩 방식은 실제 전투 연결 단계에서 공통 계산으로 구현한다. 공유 `WeaponData`나 `MechaData`의 값을 변경해서 적용하지 않는다.

## Context 사용 시 주의점

Context의 속성은 읽기 전용이지만 참조한 유닛·에셋 자체의 복사본은 아니다. 패시브의 두 메서드는 조회와 계산만 수행해야 한다. 전투 상태가 달라지면 새 Context를 만들어 다시 평가한다.

이동량은 호출자가 넘기는 값이다. 현재 이동 코드가 가진 이번 이동량과 턴 누적 이동량은 서로 다를 수 있으므로, 실제 연결 시 사용할 규칙을 먼저 결정한다.

공격자·대상은 미지정할 수 있다. 해당 정보가 필요한 패시브는 조건에서 null 또는 `HasTarget`을 확인한다. 대상 미선택 상태의 거리 0을 실제 근접 거리로 해석하지 않는다.

## 향후 확장

턴 시작 시 회복이나 피격 시 발동 패시브를 구현할 때는 해당 시점 전용 Context와 계약을 추가한다. 이번 공격 Context에 모든 상황 정보를 넣지 않는다. 파일럿 보유 목록, 설명·아이콘, 에셋 편집 UI, 실제 공격 계산 연결은 별도 작업이다.
