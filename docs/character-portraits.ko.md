# 파일럿 초상화 연결

Project에서 CharacterData 에셋을 선택합니다. Inspector의 **초상화 폴더**에 `Assets/_Game/Art/Characters/캐릭터ID/Portraits` 폴더를 드래그합니다.

```text
Portraits/
├ Default.png
├ Laugh.png       (선택)
└ Damaged.png     (선택)
```

폴더를 지정하면 바로 파일 이름별로 연결합니다. 하위 폴더는 검색하지 않습니다. 이미지 추가·삭제·교체 후에는 **초상화 새로고침**을 누릅니다. 파일 이름은 위 규칙으로 통일하세요. Inspector 아래의 세 Sprite 필드는 실제 사용되는 이미지 참조를 읽기 전용으로 보여줍니다.

- Laugh 또는 Damaged가 없으면 Default를 반환합니다.
- Default가 없거나 읽을 수 없으면 경고합니다. 해당 이미지도 없는 경우 null이며, 파일럿 능력치/전투 유효성과는 별개입니다.
- 폴더가 미지정이거나 삭제되어 연결이 끊긴 경우, Inspector를 열거나 새로고침하면 `Assets/_Game/Art/Characters/SampleAllyCharacter/Portraits`를 기본으로 연결합니다. 폴더를 비워도 이 기본 폴더로 돌아갑니다.
- 직접 지정한 폴더가 존재하지만 Default.png만 없는 경우에는 다른 캐릭터 이미지로 바꾸지 않고 기존처럼 누락 경고를 표시합니다. 기본 폴더 자체가 없거나 그 안의 Default.png가 없으면 경고하고 해당 초상화는 null입니다.
- 일반 PNG는 Sprite (Single)로 가져옵니다. 기존 Multiple Sprite 시트는 자동으로 바꾸지 않고 경고합니다.
- 폴더는 Assets 아래에서 선택합니다. 폴더 참조를 저장하므로 Unity 안에서 폴더를 이동·이름 변경해도 새로고침할 수 있습니다.

폴더 검색과 Import 설정은 에디터에서만 수행합니다. 게임 빌드에서는 CharacterData에 저장된 Sprite 참조를 사용합니다.

```csharp
Sprite normal = character.DefaultPortrait;
Sprite laughing = character.GetPortrait(PortraitExpression.Laugh);
Sprite damaged = character.GetPortrait(PortraitExpression.Damaged);
```

이 기능은 이미지 정의/연결까지 제공합니다. 피격 이벤트를 받아 Damaged로 바꾸고 일정 시간 뒤 Default로 돌아가는 HUD 동작은 아직 연결하지 않았습니다. 실제로 UI에서 참조하는 CharacterData가 빌드에 포함되어야 합니다.

검증: 전체 EditMode 1039/1039 통과(실패·건너뜀0). 초상화 테스트10개는 세 파일 검색, Sprite 가져오기, 누락 대체, 삭제/폴더 해제/변경, 잘못된 파일 지정, 직계 폴더 검색, 에셋 재로드, Multiple 설정 보존, 폴더 이름 변경을 검증합니다. 플레이어 빌드는 별도로 실행하지 않았습니다.

기본 폴더 후속 변경 검증: 초상화13개 + 기존 CharacterData13개 = 26/26 통과. 미지정·폴더 해제·폴더 삭제·Inspector 열기에서 SampleAllyCharacter/Portraits 연결을 확인했습니다.

전투 왼쪽 위 정보창은 선택 유닛 파일럿의 DefaultPortrait를 표시합니다. 파일럿이나 Default가 없으면 BattleDebugUI의 기본 파일럿 초상화를 사용합니다. 기존 네 씬에는 SampleAllyCharacter/Portraits/Default.png를 연결했습니다.
