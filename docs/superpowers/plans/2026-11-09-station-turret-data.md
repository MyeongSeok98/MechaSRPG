# Station And Turret Data Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline with TDD and one fresh read-only final review.

**Goal:** 단일 HP 시설 정의와 고정포대의 전투 설정 데이터를 분리해 제공한다.

**Architecture:** Battle/DestructibleObjectData → Unit/TurretData. Editor의 파생 형식 지원 Inspector가 한글 필드·검증 안내를 표시한다. Data/Objects에 두 샘플을 둔다.

**Tech Stack:** Unity6000.5.5f1, C#, ScriptableObject, NUnit/EditMode.

**Spec:** docs/superpowers/specs/2026-11-09-station-turret-data-design.md

## Global Constraints

기존 자산/씬/튜닝/Git 보존. Native 연속 실행, 단계 승인 중단 없음. 런타임 연결 없음.

- [x] 기존 데이터·검증 패턴 조사 및 기준 해시 보관.
- [x] 샘플/왕복 직렬화·상속 공통 정의·필수 HP/ID·포대 무장/AP 검증 RED.
- [x] 데이터2종·한글 Inspector·샘플2개 구현 GREEN.
- [x] 전체 EditMode·최종 읽기 전용 검토·새 메타와 기존 자산 보존 확인.
- [x] 사용 가이드와 구현 범위 설명, 계획/검증 기록 갱신.
