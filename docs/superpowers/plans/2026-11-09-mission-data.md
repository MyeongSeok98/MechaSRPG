# MissionData Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline with TDD and one fresh read-only final review.

**Goal:** 임무 설정을 Inspector에서 편집하는 에셋을 제공한다.

**Architecture:** Battle/MissionData는 공유 정의만, MissionController 및 후속 목표 실행 상태는 분리. Editor/MissionDataEditor는 검증 안내만 담당한다.

**Tech Stack:** Unity6000.5.5f1, C#, ScriptableObject, NUnit/EditMode.

**Spec:** docs/superpowers/specs/2026-11-09-mission-data-design.md

## Global Constraints

현재 전투·씬·기존 자산·Git 변경 없음. Native 연속 실행. Unity 테스트는 Artifacts/MeleeRangedTestProject에서만 실행한다.

- [x] 기존 데이터/Inspector 패턴 조사, 기준 해시 기록.
- [x] 데이터 왕복 직렬화·필수 ID·이름 대체·샘플 검증 테스트 RED.
- [x] MissionData.cs/Editor/MissionDataEditor.cs와 샘플 에셋 구현 GREEN.
- [x] 전체 EditMode, 읽기 전용 검토, 신규 메타/소스 일치와 기존 자산 보존 검증.
- [x] 사용 가이드 작성, 구현 완료와 후속 섬멸 목표 연결 방식을 사용자에게 설명.
