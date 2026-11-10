# Destroy Communication Station Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline with TDD and one fresh read-only final review.

**Goal:** 아군2/적2로 실제 통신소를 파괴하면 성공하는 샘플 전투를 만든다.

**Architecture:** MissionData→MissionTarget→DestroyTargetObjective(IMissionObjective)→MissionController→TurnManager. 시설은 DestructibleObjectData→기존 CoverDurability→폭발 공격을 사용한다.

**Tech Stack:** Unity6000.5.5f1, C#, ScriptableObject, IMGUI, NUnit/UnityTest.

**Spec:** docs/superpowers/specs/2026-11-09-destroy-station-mission-design.md

## Global Constraints

기존 씬/에셋/튜닝/Git 보존. Native 연속 실행. 테스트 복제본 재사용, 씬 생성은 새로운 경로에만 수행한다.

- [x] 기존 시설 공격/내구도/목표 경로 조사, 기준 해시 보관.
- [x] 지정 파괴 목표·데이터 HP·기체 지원·잘못된 등록·실패 우선·수명 테스트 RED.
- [x] 목표 타입 연결, 시설 데이터 HP와 Inspector/최소 UI 구현 GREEN.
- [x] 새 임무/테스트 무기/2대2 통신소 씬 생성.
- [x] 실제 선택→통신소 조준→Fire→팝업→성공 및 실패/재시작 회귀 검증.
- [x] 최종 읽기 전용 검토·전체 EditMode/그래픽 PlayMode·자산 보존 확인.
- [x] 사용 안내·README·계획/검증 기록 갱신과 완료 보고.
