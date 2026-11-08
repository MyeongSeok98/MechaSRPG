# 곡사 폭발무기 구현계획

Native / executing-plans / TDD. Git 작업 금지, Unity 실행은 기존 Artifacts/MeleeRangedTestProject 복제본만 사용한다. 기준 설계는 ../specs/2026-11-04-indirect-explosive-design.md 이다.

## Task 1: 독립적으로 확정된 무기 데이터

- [x] 기존 파일 해시 기록. IndirectWeaponDataTests를 먼저 작성하여 직사 기본값/기존 에셋 호환, 곡사 반경과 최소·최대 피격 부위 수 검증/저장, 직사 파편 설정 분리를 RED로 확인한다.
- [x] WeaponData에 RangedAttackMode(Direct0/Indirect1), 폭발 반경 기본1, 최소·최대 부위 수 기본1을 추가한다. 곡사는 Ranged/Explosive만 허용하며 피격 부위 수1~6, 최소≤최대, 반경1이상. 기존 에셋은 수정하지 않는다. WeaponDataEditor에서 직사/곡사에 필요한 필드만 표시한다.
- [x] 관련 데이터/직사 폭발 검사 GREEN. 이 단계는 시야·건물·보정 선택과 독립적이다.

## Task 2: 좌표 공격 실행

- [x] 구현 전 미정 규칙에 대한 사용자의 답변을 받아 설계와 계획을 구체화한다. 답변이 오기 전에는 시야·차단·보정/AI에 의존하는 구현을 진행하지 않는다.
- [x] 좌표 공격 미리보기/결과와 실행 테스트를 먼저 작성한다. 원형 경계·적만 피격·부위/피해 독립 추첨·제외/중복/후보 부족·원자적 상태·자원1회·미리보기 RNG 보존을 검증한다.
- [x] CombatSystem의 좌표 공격을 기존 자원/턴/그리드/UnitHealth 배치 피해에 연결한다. 일반 적 지정 공격과 혼용하지 않으며 곡사 경계 제외는 사용자 답변에 따른다.

## Task 3: 실제 좌표 선택·범위·Fire 및 AI

- [x] 실제 입력과 조준 해제/기체·무기 전환, 범위/대상 미리보기, Fire 자원/HP, 백팩/재장전·경계 제외, 시야 변경과 잘못된 좌표 재검증을 PlayMode에서 검증한다.
- [x] 기존 PlayerInputController와 GridVisualizer의 범위 표시를 재사용하고 곡사 좌표 확인 UI를 별도 소형 컴포넌트로 연결한다. 부위 조준 UI는 직사에서 유지한다.
- [x] 사용자 답변에 따라 AI 좌표 선택을 기존 턴에 연결하고 실제 HP/AP/탄약으로 확인한다.

## Task 4: 회귀·안내·검토

- [x] 전체 EditMode/그래픽 PlayMode를 복제본에서 실행한다. Unity 종료 후만 동기화하고 신규 meta를 원본에 복사한다.
- [x] 스킬 요구에 따른 fresh 읽기 전용 최종 리뷰1회. 숨겨진 적 정보 누출, 발사 시 재검증, 이벤트로 전투 종료되어도 한 발 피해 처리, 직사/곡사 혼용과 기존 AI/경계 회귀를 검토한다.
- [x] 중요한 발견은 실패 재현 후 수정하고 전체 회귀를 재실행한다. 문서/README/체크리스트·진행 기록을 갱신하며 기존 씬·에셋·카메라·속도·엄폐·설정은 해시로 보존 확인한다.

## 연결 규칙과 실제 인터페이스

사용자 답변 완료: 사거리 안 모든 칸(시야/탐험 무관), 아군/자신/미탐지 기체도 피격, 건물은 폭발만 차단, 원래 피해 범위 그대로·명중/치명/보정 없음, 빈 지점 허용, 적 AI 포함, 곡사 경계 제외.

CombatSystem.GetIndirectPreview(BattleUnit, Vector2Int, WeaponSlot), TryIndirectAttack, GetIndirectBlastCells를 사용한다. Preview.VisibleTargets와 Result.Targets는 발사자에게 알려진 기체만 포함한다. 실제 실행 대상은 별도로 수집하여 모든 기체 피해를 처리한다. PlayerInputController의 SelectedImpactCell/TrySelectImpactCell/TryFireIndirectSelected/LastIndirectAttackResult와 IndirectTargetUI를 연결한다. TurnManager의 BeginDamageResolution/EndDamageResolution은 한 발 처리 중 재진입과 전투 종료를 막고 finally에서 해제한다. Grid.HasClearBlast는 중심 칸만 차단 시작 검사에서 제외한다. 직사/곡사 코드는 같은 CombatSystem의 partial 파일로 나누며 별도 전투 서비스나 MonoBehaviour를 추가하지 않는다.


## 완료 결과

데이터 RED12/12 → 관련GREEN54/54. 실행 RED11/11 → GREEN23/23 후 백팩탄약/건물중심 검사2개 추가. Play RED5개실패/1개기존대기통과 → 실제입력GREEN6/6. 테스트 준비의 미탐험 예상1개는 기존 탐험 기록 때문에 초기 Visibility를 재설정하여 수정했고 제품 안개 규칙은 변경하지 않았다.

최종 전체 EditMode813/813, 그래픽 PlayMode395/395 통과(실패·건너뜀0). Fresh 읽기 전용 리뷰에서 조치할 결함 없음. 보호 파일 해시와 원본/복제본 C# 일치 확인, 신규8개 메타 복사. 기존씬/에셋/프리팹/카메라/이동속도/엄폐/설정은 그대로 유지했다. Git 작업 없음.
