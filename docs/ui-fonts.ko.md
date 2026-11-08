# 전투 자원 표시 폰트

HP·탄약/에너지 팩 잔량·예비 개수·AP 표기에는 Oxanium Regular를 사용합니다. 이름·한글 설명·명중률·피해량은 기존 폰트를 사용합니다. 같은 줄에서도 자원 표기만 별도 폰트로 그립니다. 버튼 영역과 전투 규칙은 변경하지 않습니다.

- 폰트: `Assets/_Game/Art/UI/Fonts/Resources/Oxanium-Regular.ttf`
- 라이선스: `Assets/_Game/Art/UI/Fonts/OFL.txt` (SIL Open Font License 1.1)
- 표시 공통 처리: `Assets/_Game/Scripts/Presentation/ResourceTypography.cs`
- 적용 UI: 행동 메뉴, 부위 조준/경계사격, 엄폐물 조준, 곡사 조준, 왼쪽 디버그 창

폰트는 Resources에서 자동으로 읽으므로 저장 씬마다 연결할 필요가 없습니다. Oxanium이 제공하지 않는 한글은 기존 폰트로 그립니다. 현재 문자열의 HP/AP/Ammo 표기, HP·잔탄 비율, 예비 개수를 구분하며 게임의 수치나 텍스트 내용은 바꾸지 않습니다.

원본 배포처: [Severin Meyer — Oxanium](https://sev.dev/fonts/oxanium/). Regular 정적 TTF와 배포 라이선스를 포함했습니다.

검증: 테스트 복제본의 그래픽 PlayMode에서 폰트 임포트/라틴·숫자 글리프 확인 및 기존 조준·메뉴·탄약·경계 입력 검사 합계 84개 통과. 배치 모드에서는 화면 캡처를 얻지 못했으므로 시각 캡처 검증으로 계산하지 않습니다. 임시 진단 코드는 원본 Assets에 추가하지 않았습니다.
