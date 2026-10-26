# Critical Hits Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline, without implementation subagents.

**Goal:** 기량 차이에 따른 치명타 확률과 피해 1.25배를 실제 공격에 적용한다.
**Architecture:** CombatMath가 순수 계산, BattleUnit이 파일럿 기량 초기화, CombatSystem이 명중 이후 공통 판정, Preview/Result와 기존 UI가 표시를 담당한다.
**Tech Stack:** Unity 6000.5.5f1, C#, NUnit Unity Test Framework.
**Spec:** docs/superpowers/specs/2026-10-24-critical-hits-design.md

## Global Constraints
Git 사용 금지. 씬/기체/무기 에셋과 사용자 카메라/이동 설정 보존. 테스트는 Artifacts/MeleeRangedTestProject만 사용.

## Review Focus
소수 확률의 경계, 0% 강제 실패, 난수 호출 순서/미리보기, 파괴 판정 중복 방지, 파일럿 미지정과 데이터 공유.

## Tasks
- [x] 치명타 공식/실제 공격 테스트를 먼저 추가하고 기능 부재로 실패하는 것을 확인한다.
- [x] CombatMath 계산/반올림, BattleUnit 파일럿 연결, Preview/Result 필드, ExecuteAttack 통합을 구현한다.
- [x] 기존 테스트의 치명타 추첨을 고정하고 직전 승인된 데이터 수치와 관련 에셋 테스트를 일치시킨다.
- [x] UI에 확률/배율과 실제 치명타 표시를 연결한다.
- [x] 격리 프로젝트 EditMode와 graphics PlayMode 전체 테스트를 실행하고 실패를 모두 조사한다.
- [x] 최종 읽기 전용 검토와 보호 파일 해시 검증 후 결과를 보고한다.
