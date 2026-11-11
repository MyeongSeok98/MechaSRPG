# Command Wheel Phosphor Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline. User approved the animation preview and requested Unity application. No Git operations.

**Goal:** Apply the approved phosphor preview to the existing circular action menu.

**Architecture:** Replay a small fixed set of earlier geometry poses relative to the current model anchor and current menu bounds. Exponential 120 ms persistence, pale sharp text, faint scanlines/grain on HUD marks only. No screen-history buffer, world postprocess, packages, or gameplay changes.

**Tech Stack:** Unity 6000.5, existing IMGUI renderer, NUnit.

**Spec:** Approved `unit-selection-phosphor.html` preview in this conversation.

## Constraints and review focus
- Preserve command hitboxes, enabled rules, selection fill, animation timing and camera/model anchor.
- Deselect and unit switches must leave no historical screen pixels.
- Inspector controls: persistence seconds (default .12), texture strength (default .04); zero disables each effect.
- Text remains legible and has no historical copies. World and portraits receive no effect.
- Preserve scene/data/camera settings and all user tuning. Test only in Artifacts/MeleeRangedTestProject.

## Task 1: temporal sampling and rendering
- [x] Add failing temporal tests: no pre-selection history, decay with age, configurable persistence, bounded tail after animation.
- [x] Run targeted EditMode tests and confirm missing feature failure.
- [x] Add `CommandWheelPhosphor` temporal sampling; extract geometry rendering from labels; draw a bounded number of earlier poses at the current anchor.
- [x] Add subtle raster texture to geometry and clipped text raster bands, preserving sharp glyphs and outer glow.
- [x] Expose controls in UnitActionUI; dispose any owned texture with the component.
- [x] Run EditMode suite, relevant graphical PlayMode suite, and scene captures; inspect screenshots.
- [x] Review source and document controls and actual validation results. No staging, commits, or pushes.

