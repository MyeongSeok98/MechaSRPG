# Weapon Ammo Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline. One final fresh read-only review, no implementation delegation.

**Goal:** 무기별 유한 탄약과 AP 재장전을 실제 플레이에 연결한다.
**Architecture:** WeaponData가 초기 스펙, UnitWeaponAmmo가 각 장착 무기 잔탄/예비 상태, CombatSystem이 행동 권한/AP/사격·재장전 처리를 맡는다. 기존 Input/UI/AI/Overwatch가 공통 기능을 사용한다.
**Tech Stack:** Unity6000.5.5f1, C#, NUnit/Unity Test Framework.
**Spec:** docs/superpowers/specs/2026-10-25-weapon-ammo-design.md

## Global Constraints
Git/GitHub 금지. Unity 실행은 Artifacts/MeleeRangedTestProject만 사용. 기존 씬/에셋 수치/배치/모델/속도/카메라 보존. 원거리 전체 공통, 근접 무탄약. 재장전 잔탄 폐기 방식.

## Review Focus
동일 정의를 공유하는 두 유닛/두 손 독립성; Either 무기 손 이동과 잔탄 보존; 마지막 경계 탄약에 의한 이동 잠김 방지; 빈 탄창 재장전 AP와 조준 경로 독립; 실패/콜백 재진입 자원 중복 차감.

## Tasks
- [x] 정의/탄약 상태/사격/재장전의 실패 테스트를 먼저 추가하고 확인한다.
- [x] WeaponData와 Inspector, UnitWeaponAmmo, BattleUnit 초기화를 구현한다.
- [x] CombatSystem 사격/재장전, Overwatch 탄약 권한, 적 AI 재장전을 연결한다.
- [x] Reload 무기 선택 메뉴와 탄약 표시, Input 취소/행동 잠금을 연결한다.
- [x] UI/AI/경계/팔 파괴 회귀를 추가하고 전체 EditMode/graphicsPlayMode를 실행한다.
- [x] 읽기 전용 최종 검토 후 필요한 수정을 검증하고 보호 파일/테스트 소스 일치를 확인한다.
