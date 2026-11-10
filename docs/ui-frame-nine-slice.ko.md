# 선택 기체 정보창 프레임

- 이미지: `Assets/_Game/Art/UI/Panels/UnitInfoFrame_Olive.png`
- 프리팹: `Assets/_Game/Prefabs/UI/UnitInfoFrame.prefab`

선택한 올리브색 군용 프레임 원본(1254×1254)을 그대로 사용합니다. Sprite Single / Full Rect, Border는 Left·Bottom·Right·Top 모두240px입니다. 모서리 장갑판이 늘어나는 영역에 들어가지 않도록 지정했습니다.

Pixels Per Unit은400입니다. 기본 Canvas의 Reference Pixels Per Unit100에서 모서리 고정 영역은60 UI 단위가 됩니다. Mipmap은 끄고 Bilinear·Clamp·압축 없음·투명 알파를 사용합니다.

프리팹에는 RectTransform·CanvasRenderer·Image만 있습니다. Image는 **Sliced**, Fill Center 켬, Raycast Target 끔, 흰색 Tint이며 기본 크기는320×720입니다. Canvas 아래에 배치하고 RectTransform의 Width/Height를 조절하세요. Transform Scale은1을 유지하면 테두리 두께를 유지하기 쉽습니다. 별도 Canvas나 선택 유닛 정보·전투 연결은 아직 포함하지 않습니다.

일반 Image에 직접 사용할 경우 Source Image에 위 Sprite를 넣고 **Image Type을 Sliced**로 바꿉니다. Simple에서 Preserve Aspect를 켜는 방식과는 다릅니다. 기본 Canvas에서 가로·세로를120보다 작게 만들면 고정 Border 영역도 축소될 수 있습니다.

검증: Unity 복제 프로젝트에서 임포트된 Sprite Border와 저장 프리팹의 Sliced 설정을 확인하고, 실제 uGUI 생성 메시를320×720·600×320으로 렌더링해 모서리 보존을 확인했습니다. 기록과 미리보기는 `Artifacts/OliveFrameVerification/`에 있습니다. 기존 씬·전투 코드에는 연결하지 않았습니다.

## 디버그창 시범 적용

후속 작업으로 네 전투 씬의 Battle Systems → BattleDebugUI → **디버그창 프레임**에 이 Sprite를 연결했습니다. 현재 디버그창은 IMGUI이므로 프리팹을 사용하지 않고 같은 Border와 PPU로9영역을 직접 그립니다. 프레임을 None으로 비우면 기존 남색 배경으로 돌아갑니다. 내부 정보·선택·스크롤은 유지하며 모서리와 내용이 겹치지 않도록 여백을 둡니다. 실제 통신소 씬 캡처와 관련 그래픽 테스트8/8을 확인했습니다. 기록: `Artifacts/DebugFrameVerification/`.

## 내부 반투명

내부 올리브 영역의 알파만 원래 값의65%로 낮췄습니다(중앙252→164). 원본 RGB·금속 테두리/장갑판 알파·외부 투명 영역과 Sprite GUID/Border/PPU는 유지합니다. 같은 Sprite를 쓰는 프리팹과 네 씬에 함께 적용됩니다. 원본은 `Artifacts/FrameTransparencyBefore/`에 보관했습니다.

반투명 변경 당시 관련 그래픽 테스트8/8과 실제 캡처를 확인했습니다. 기록: `Artifacts/FrameTransparencyVerification/`.

## 전체 화면 전장

후속 요청에 따라 네 저장 씬의 Battle Camera → Viewport Rect를 X0·Y0·W1·H1로 바꿨습니다. 이제 디버그창 뒤에도 게임 화면이 그려지고 올리브색 반투명 내부로 비칩니다. 카메라 Transform·줌·이동 속도·그림자 설정은 유지합니다. 디버그창은 전장 위의 오버레이이며 창 위 클릭은 뒤 유닛에 전달하지 않습니다.

BattleViewportBackground는 부분 뷰포트 호환용으로 남겨두지만 전체 화면에서는 검은 여백을 그릴 영역이 없습니다. 이전 배너 잔상 회귀 테스트는 테스트 개체에서만 임시 부분 뷰포트를 사용합니다. 기록: `Artifacts/FullViewportVerification/`.
