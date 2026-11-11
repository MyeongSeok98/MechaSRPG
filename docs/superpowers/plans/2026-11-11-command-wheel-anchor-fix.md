# Command Wheel Anchor Fix

> Execute inline with superpowers:executing-plans, one fresh final read-only review, no Git operations. Existing isolated Unity test clone only.

**Bug evidence:** UnitActionUI.TryGetPanelRect clamps the whole600×350 menu and moves it away from HUD overlays. UnitCommandWheel.Draw derives ring center from that adjusted rectangle. Thus the screen edge/HUD pushes the ring away from the selected unit. Projection also uses the logic root instead of visible body height.

**Fix contract:** Keep the ring center at the current camera projection of the selected unit's visual body center (root X/Z plus model renderer bounds center Y). HUD/viewport avoidance may reposition buttons and auxiliary menus only. Connectors start from the fixed ring boundary toward those relocated buttons. Original animation, chosen-action colors, action hitboxes and existing battle rules remain. Mesh bounds are read from the current UnitMechaVisual.ModelInstance, with primitive renderer/collider/root fallback. Cache renderer references per selected model, not their world bounds. Do not change camera settings or saved scenes.

- [x] RED graphics regression: model center projection at top/left HUD/right edge and after camera rotation/zoom must equal ring center.
- [x] Separate fixed ring anchor from movable menu layout and draw connecting lines from the fixed circle.
- [x] GREEN targeted tests and actual saved-scene captures near HUD/top edge. Full relevant/whole regression as warranted.
- [x] Read-only final review, authored-data preservation check and concise result.

