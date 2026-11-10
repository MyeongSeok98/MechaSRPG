# All Weapon Facility Damage Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline, TDD and one fresh read-only final review.

**Goal:** 기존 시설 조준과 피해 처리에서 모든 무기를 지원한다.

**Architecture:** 시설 분류와 피해 배율 분리, 공유 공격 검증, Grid 근접 말단 점유 허용 도달 판정, 기존 Cover 공격/곡사/팝업/목표 경로 유지.

**Tech Stack:** Unity6000.5.5f1, C#, IMGUI, NUnit/UnityTest.

**Spec:** docs/superpowers/specs/2026-11-09-all-weapon-facility-damage-design.md

## Global Constraints

기존 씬/무기/기체/설정/Git 변경 없음. Native 연속 실행, 테스트 복제본 재사용.

- [x] 기존 공격·근접 경로·리소스·UI 확인 및 기준 해시 보관.
- [x] 무기 종류별 시설 피해·탄약/AP·도달·벽/골짜기·배율0·일반 엄폐 호환 RED.
- [x] 공유 검증/시설 피해/근접 도달/선택/UI/곡사 구분 구현 GREEN.
- [x] 실제 통신소 씬의 일반 사격·근접·실제 클릭·팝업/성공 회귀 검증.
- [x] 최종 검토, 전체 EditMode/그래픽 PlayMode 및 자산 보존 확인.
- [x] 사용 안내·검증 기록·계획 갱신 후 완료 보고.

검증: EditMode1029/1029, 그래픽 PlayMode470/470, 실패·건너뜀0. 읽기 전용 최종 코드 검토 지적0. 기존 씬·데이터·설정 해시 변경0, 루트/복제본 C# 불일치0. 기록: Artifacts/AllWeaponFacilityVerification 및 Artifacts/all-weapon-facility-progress.md.
