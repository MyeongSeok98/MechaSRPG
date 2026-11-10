# Character Portraits Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline; TDD and one fresh read-only final review.

**Goal:** CharacterData에서 폴더를 지정하고 Default.png, Laugh.png, Damaged.png를 자동 연결한다.

**Architecture:** 폴더 참조는 에디터 전용, Sprite 참조와 표정 조회는 런타임 데이터이다. 폴더 변경/새로고침 시 직계 파일만 검색하고 일반 PNG를 Sprite Single로 가져온다. 기존 Multiple Sprite 파일은 자동 변경하지 않고 경고한다. 선택 표정 누락은 Default로 반환한다. Default 누락은 경고하되 파일럿 전투 데이터 유효성에 영향을 주지 않는다. 폴더 해제/변경은 오래된 초상화 참조를 제거한다. 피격 이벤트/표정 UI 연결은 이번 범위에 포함하지 않는다.

**Tech Stack:** Unity C#, AssetDatabase, SerializedObject, NUnit EditMode.

사용자 승인: 이전 답변의 폴더 자동 연결/새로고침 구조와 최신 요청의 세 파일명 및 Default 대체 규칙.

- [x] 기존 캐릭터 데이터와 Inspector 확인, 테스트에서 폴더 검색/누락/변경/저장/잘못된 입력 RED.
- [x] CharacterData에 PortraitExpression, DefaultPortrait/GetPortrait 및 숨긴 Sprite/에디터 폴더 필드 추가.
- [x] Editor 전용 CharacterPortraitLoader와 폴더 선택·새로고침·읽기 전용 미리보기·경고 구현.
- [x] 테스트 GREEN, 기존 CharacterData 회귀 및 전체 EditMode 검증, 별도 최종 읽기 전용 검토.
- [x] 메타/사용 안내 정리 및 결과 보고. Git/기존 씬·캐릭터 데이터·이미지 변경 없음.

검증: RED8/8 → focused21/21 → final full EditMode1039/1039. 최종 검토의 다중 선택 누락 경고 수정 후 전체 재검증 통과. 독립 플레이어 빌드/실제 HUD 표정 전환은 이번 검증 범위 밖. 안내: docs/character-portraits.ko.md.
