# Pilot Combat Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline, TDD, and one fresh final read-only review. No Git operations.

**Goal:** 파일럿 사격·근접·회피 및 근접 피해 보정을 모든 전투 경로에 적용한다.
**Architecture:** BattleUnit이 초기 능력치를 복사하고, CombatMath가 배율을 계산하며, CombatSystem이 공통 미리보기/실행에 전달한다. 기존 Presentation이 보정된 결과를 표시한다.
**Tech Stack:** Unity6000.5.5f1, C#, NUnit / Unity Test Framework.
**Spec:** docs/superpowers/specs/2026-10-26-pilot-combat-design.md

## Global Constraints
전투 능력치 0~50, 파일럿 없으면0. 사격 차이 ×.01, 근접 차이 ×.005, 근접 피해 능력치 ×.005. 최종 한 번 반올림. Git 금지, 원본 에셋/씬/사용자 설정 보존, 기존 복제본에서 Unity 검증.

## Review Focus
능력치가 같은 경우 명중 배율1; 미지정/잘못된 능력치와 공유 에셋 독립성; 중간 반올림/제한 금지; 원거리 피해에 근접 능력치가 섞이지 않음; AI와 경계가 실제 공격 및 UI와 같은 경로를 사용함.

### Task 1: 공통 파일럿 전투 보정
**Files:** BattleUnit.cs, CombatMath.cs, CombatSystem.cs, AttackPreview.cs, AttackPreviewText.cs, BattleDebugUI.cs; Tests/Shared/MechaAccuracyTestBattle.cs, Tests/EditMode/PilotCombatTests.cs, Tests/PlayMode/PilotCombatPlayTests.cs; 밸런싱/CharacterData 안내.

**Interfaces:** BattleUnit.PilotShooting/PilotMelee/PilotEvasion, CombatMath.CalculatePilotHitMultiplier(AttackType,int,int), CalculatePilotDamageMultiplier(AttackType,int), CalculateModifiedAttackDamage(int,bool,float,float); AttackPreview.PilotHitMultiplier/PilotDamageMultiplier. 기존 3인수 피해 API 유지.

- [x] 실패 테스트: 배율 표·경계값·초기화 스냅샷·실제 명중/피해·피해 미리보기·AI·경계. 사격50/회피0→1.5, 근접50/회피0→1.25, 근접50피해→1.25, 동일 능력치→1.
- [x] 복제본에서 새 EditMode/PlayMode 실패를 확인한다.
- [x] 초기화/공통 수식/미리보기와 실행/디버그를 최소 수정한다.
- [x] 전체 EditMode 및 그래픽 PlayMode 검증: 모두 통과해야 한다.
- [x] 최종 읽기 전용 검토, 문서 갱신, 보호 파일 해시와 복제본 소스 일치를 확인한다.
