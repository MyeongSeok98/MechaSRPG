# Armor Damage Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline.

**Goal:** 장갑 상성 .8/1.1을 모든 공통 공격 경로에 적용한다.
**Architecture:** BattleUnit이 장갑 스펙을 초기화하고 CombatMath가 상성 및 최종 반올림을 계산한다. CombatSystem이 미리보기와 실제 실행에 같은 배율을 사용하며 기존 UI가 보정 피해를 표시한다.
**Tech Stack:** Unity6000.5.5f1, C#, NUnit Unity Test Framework.
**Spec:** docs/superpowers/specs/2026-10-25-armor-damage-design.md

## Global Constraints
Git/GitHub 사용 금지. Unity 테스트는 Artifacts/MeleeRangedTestProject에서만 실행한다. 기존 사용자 에셋/씬/속도/카메라/렌더 설정은 보존한다.

## Review Focus
치명타와 이중 반올림, 미리보기 피해를 정수 추첨에 재사용하는 오류, 근접/폭발물의 잘못된 상성, 잘못된 공격자 장갑 참조, 초기화 후 공유 데이터 변경.

## Tasks
- [x] 장갑 상성/실제 공격 테스트를 작성하고 기능 부재에 의한 실패를 확인한다.
- [x] CombatMath.CalculateArmorMultiplier와 CalculateAttackDamage를 구현하고 기존 CalculateCriticalDamage 호환을 유지한다.
- [x] BattleUnit 장갑 복사, AttackPreview.ArmorMultiplier, CombatSystem 공통 피해 적용을 연결한다.
- [x] 피해 UI/Inspector 안내와 밸런싱 문서를 갱신한다.
- [x] 전체 EditMode 및 graphics PlayMode 테스트를 실행한다.
- [x] 읽기 전용 최종 검토, 보호 파일 해시/테스트 소스 동등성을 확인하고 보고한다.
