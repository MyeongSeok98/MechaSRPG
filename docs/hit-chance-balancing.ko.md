# 명중률 계산·밸런싱 안내

## 최종 공식

```text
무기 기본 명중률
× 파지 × 거리 × 사수 머리 손상 × 목표 부위 × 엄폐 × 경계사격 × 기체 보정
→ 모든 배율 적용 후 0~100 제한 → 마지막에 한 번 반올림
```

공식은 `Assets/_Game/Scripts/Combat/CombatMath.cs`의 `CalculateAttackHitChance`, 보정 수집은 `CombatSystem.cs`의 `BuildWeaponPreview`에서 처리한다. 일반 원거리/근접 공격, 적 AI, 경계사격이 같은 미리보기와 계산 경로를 사용한다. 근접은 엄폐·부위·거리 배율을1로 사용한다. 일반 공격의 경계 배율은1이다.

## 기체 명중력·회피력

`MechaData` 에셋 Inspector의 **명중력 (0~50)**, **회피력 (0~50)**을 수정한다. 최대값은 `MechaData.MaxCombatStat = 50`이며 기본값0은 기존 공격에 영향을 주지 않는다. 기존 에셋을 다시 저장하지 않아도 누락된 새 필드는0으로 읽는다.

`BattleUnit.Initialize`에서 `MechaAccuracy`와 `MechaEvasion`으로 유닛별 초기값을 읽는다. 데이터가 없으면0이다. 현재 전투의 값은 스냅샷이므로, 에셋을 수정한 효과는 전투를 새로 시작해 확인한다. 반복 초기화로 진행 중 수치나HP/AP를 덮어쓰지 않는다.

```text
d = 공격 기체 명중력 - 대상 기체 회피력

d >= 0: 기체 배율 = 1 + d × 0.01
d < 0 : 기체 배율 = 1 + d × 0.008
```

| 공격자 명중력 | 대상 회피력 | 기체 보정 |
|---:|---:|---:|
| 0 | 0 | ×1.0 |
| 50 | 0 | ×1.5 |
| 0 | 50 | ×0.60 |
| 50 | 50 | ×1.0 |
| 25 | 0 | ×1.25 |
| 0 | 25 | ×0.80 |

기체 명중력은 확률% 자체가 아니다. 명중력50이라고 최종 명중률이50%라는 뜻도 아니다. 두 수치가 같으면 상쇄된다. 기본 명중률80에 기체 배율.6만 적용하면48%, 여기에 경계 배율.75를 곱하면36%다. 기본80에 기체1.5와 경계.75는90%이며, 중간에120을100으로 제한하지 않는다.

### 기체 보정 강도 조절

`CombatMath.cs` 맨 위 두 상수만 조절하면 된다.

```csharp
public const float MechaAccuracyAdvantagePerPoint = .01f;
public const float MechaEvasionAdvantagePerPoint = .008f;
```

- 첫 값: 명중력이 회피력보다 높은 차이1점의 증가 배율. `.01f`는1% 증가다.
- 둘째 값: 회피력이 명중력보다 높은 차이1점의 감소 배율. `.008f`는0.8% 감소다.
- 계수를 바꾸면 위 표의 끝값도 달라진다. 최대 능력치50과 배율의 강도는 별개다.

기체 배율은 `AttackPreview.MechaMultiplier`에 남아 미리보기와 실제 발사가 같은 값을 사용한다. 현재 파일럿 능력치, 이동 거리 기반 기동 회피, 기체 업그레이드 진행/저장은 이 기능에 포함되지 않는다.

## 다른 보정 위치

| 항목 | 수정 위치 | 현재 기준 |
|---|---|---|
| 무기 기본 명중률 | WeaponData Inspector: 기본 명중률 (%) | 무기별 |
| 겸용 무기의 양손 보너스 | WeaponData Inspector: 양손 명중 배율 | 겸용+현재양손일 때만 |
| 거리 | WeaponData Inspector: 거리별 명중률 구간 | 해당 상한 칸까지 첫 일치 구간, 빈 목록/마지막 이후×1 |
| 사수 머리 파괴 | BattleUnit Inspector: Destroyed Head Hit Multiplier | 기본×.75 |
| 목표 부위 | TargetBodyPart.cs: BodyTargetRules.HitMultiplier | 몸통1, 콕피트.45, 머리.8, 팔/다리.9 |
| 엄폐 | 씬 CoverSystem Inspector | Half.75, Full.5 |
| 경계 | OverwatchSystem.ReactionHitMultiplier | .75 |

경로는 `Assets/_Game/Scripts/` 기준이며 무기 정의는 `Combat/WeaponData.cs`, 기체 정의는 `Unit/MechaData.cs`에 있다. Inspector에 저장된 값은 C# 기본값만 바꿔도 갱신되지 않으므로 실제 사용하는 에셋/컴포넌트를 조정한다. AP·사거리·시야·사선·팔 파괴 등 공격 가능 조건은 명중률과 별도로 검사한다.

## 치명타 (2026-10-24)

치명타는 명중률과 별도로, 명중한 공격에만 판정한다. 일반 원거리/근접, 적 AI, 경계사격이 같은 규칙을 사용한다.

`BattleUnit` Inspector의 **파일럿 데이터**에 `CharacterData`를 연결한다. 시작 시 기량을 0~50 범위로 복사하며, 파일럿이 없으면 0이다. 실행 중 에셋을 수정해도 이미 초기화한 유닛은 바뀌지 않는다. 사격/근접/회피 스탯은 이번 기능에서 연결하지 않았다.

`CombatMath.cs`에서 다음 상수로 밸런스를 조절한다.

- `BaseCriticalChance = 5`: 기량이 같을 때 확률(퍼센트).
- `CriticalAdvantagePerPoint = .9`: 기량이 높을 때 차이 1당 +0.9%p.
- `CriticalDisadvantagePerPoint = .25`: 기량이 낮을 때 차이 1당 -0.25%p. 현재 차이 -20 이하에서 0%.
- `MaxCriticalChance = 50`: 최종 확률 상한. 기량 차이 +50에서 50%.
- `BaseCriticalDamageMultiplier = 1.25`: 기본 치명타 피해 배율. 아이템/스킬의 배율 증가는 아직 구현하지 않았다.

피해는 무기 범위 내 정수를 추첨하고 장갑 배율과 치명타 배율 적용 뒤 한 번 반올림(0.5 올림)한다. 선택 부위 HP 차감과 기존 부위 파괴 처리는 유지된다. 치명타는 기체 `Critical` 상태 및 완전파괴 확률과 별도이다.

## 장갑 상성 피해 (2026-10-25)

`MechaData` Inspector의 **장갑 타입**을 선택한다. `BattleUnit`은 초기화 시 장갑 타입을 복사하며, 데이터 미지정은 일반 장갑이다. 실행 도중 에셋을 변경해도 이미 초기화된 유닛에는 소급하지 않는다. 기체의 모든 부위에 같은 장갑을 적용한다.

| 대상 장갑 | 빔 | 실탄 | 폭발물/근접 |
|---|---|---|---|
| 일반 | ×1 | ×1 | ×1 |
| 대빔 | ×.8 | ×1.1 | ×1 |
| 대실탄 | ×1.1 | ×.8 | ×1 |

무기 속성은 `WeaponData`의 원거리 분류를 사용한다. 근접 공격은 원거리 분류 값에 관계없이 ×1이다. 명중률/AP에는 영향을 주지 않으며 일반 공격·적 AI·경계사격이 같은 계산을 사용한다.

`CombatMath.ArmorResistanceMultiplier = .8f`, `ArmorWeaknessMultiplier = 1.1f`에서 계수를 바꾼다. `CalculateAttackDamage`가 원래 무기 피해 × 장갑 배율 × 치명타 배율을 계산한 뒤 마지막에 한 번 반올림한다. 예: 7 × .8 × 1.25 = 7이며, 일반 피해를 먼저 6으로 반올림해 치명타를 곱하지 않는다.

조준 UI의 Damage는 장갑 보정 후 일반 피해 범위다. 장갑/치명타 배율은 별도 표시한다. `AttackPreview.MinDamage/MaxDamage`는 정수 추첨에 사용할 원래 무기 범위이고 `ArmorMultiplier`가 장갑 보정이다. 피해 상한은 int 최대치이며 실제 HP 차감은 부위 잔여 HP까지만 적용한다.

## 기존 명중률 검증 기록

2026-10-22 전체 EditMode515/515·그래픽 PlayMode337/337 통과, 실패·건너뜀0. 새 기체 보정 EditMode27개와 실제UI/AI/경계 PlayMode6개를 포함한다. 독립 검토에서 Critical/Important/Minor 발견0. 원본 씬·기체/무기 에셋·배치·이동/카메라/렌더 설정은 유지했다. 결과XML/log는 Artifacts/mecha-accuracy-final-edit, mecha-accuracy-final-play 이름으로 보존한다.
