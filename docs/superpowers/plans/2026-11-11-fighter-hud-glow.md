# Fighter HUD Variant Three Implementation Plan

> Execute inline with superpowers:executing-plans and a fresh read-only final review. No Git/worktree actions; use existing isolated Unity test clone.

**Approved design:** Detailed fighter HUD variant2 + solid green text with Outer Glow (corrected variant3). Preserve the fixed model-center anchor and existing command behavior. Entry: converging rails, tick illumination, counter-rotating sector ring, inner/outer brackets and scan; existing simultaneous line/typewriter starts at0.44s; traveling line tips; four action channels lock sequentially; final pulse and settled state at1.38s. No continuous idle animation or strobing. No text color gradient.

**Implementation:** Extend CommandWheelAnimation with display-only progress values and channel order. Add CommandWheelHud for decorative rendering using the existing IMGUI primitives. UnitCommandWheel retains geometry/hitbox APIs and renders solid text over layered low-alpha radial glyph copies for soft outer glow; expose intensity/radius in UnitActionUI. Keep all decorative elements non-interactive. Demo-only labels/replay controls stay out of the actual battle view.

- [x] RED timing tests: detail progress before channels; channel order; words complete before final lock; settled final state.
- [x] Implement clock and HUD decorations, moving tips/typewriter cursor, text glow and Inspector controls.
- [x] Verify exact timing plus fixed anchor, click-through center, HUD placement, main actions/secondary menus, disabled-state readability. Capture entry/verification/settled actual scene frames.
- [x] Fresh final review, appropriate regression suites, authored-data preservation and guide/ledger.

No edits to gameplay data, scenes, camera zoom/pan/shadows, model/weapon placement or movement tuning.

