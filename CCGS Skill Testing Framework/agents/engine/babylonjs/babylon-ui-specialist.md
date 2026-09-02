# Agent Test Spec: babylon-ui-specialist

## Agent Summary
Domain: Babylon.js v9 in-canvas UI — world-space Babylon GUI (billboards, NPC labels, crosshairs, damage numbers), screen-space `AdvancedDynamicTexture` overlay (when no React HUD is used), `JsonStyleAsset` theming, control pooling, gamepad focus management. Also owns the **integration boundary** with the React HUD overlay: defines which UI is React vs. Babylon GUI.
Does NOT own: actual React HUD implementation (DOM, React components, Zustand selectors for HUD state) — delegates to `ui-programmer`. Does NOT own: world-space UI custom shaders — delegates material concerns to `babylon-shader-specialist`. Does NOT own: overall Babylon engine configuration (`{ alpha: false }`) — delegates to `babylon-js-specialist`.
Model tier: Sonnet (default for engine specialists).
No gate IDs assigned.

---

## Static Assertions (Structural)

- [ ] `description:` field is present and references Babylon GUI v9 concepts (`AdvancedDynamicTexture`, world-space vs screen-space, billboards, React HUD integration boundary)
- [ ] `allowed-tools:` list includes Read, Write, Edit, Bash, Glob, Grep (and may include Task)
- [ ] Model tier is Sonnet (default for engine specialists)
- [ ] Agent definition references `docs/engine-reference/babylonjs/VERSION.md` as the authoritative API source
- [ ] Agent definition references `docs/engine-reference/babylonjs/modules/gui.md` for the current GUI surface
- [ ] Frontmatter enforces "never `import * as BABYLON`" discipline (deep ES6 imports)
- [ ] Agent definition explicitly describes the React HUD vs. Babylon GUI integration boundary

---

## Test Cases

### Case 1: In-domain request — world-space UI
**Input:** "I need health bars floating above each enemy in my Babylon v9 scene."
**Expected behavior:**
- Identifies this as world-space Babylon GUI — this specialist's primary domain
- Describes: create a `Plane` mesh parented to the enemy, attach an `AdvancedDynamicTexture` via `CreateForMesh`, set `billboardMode = BillboardMode.BILLBOARDMODE_ALL`
- Notes `mesh.isPickable = false` to prevent blocking scene picking unless the bar itself is interactive
- Notes pooling: do not create/destroy per frame — pool the planes and reuse
- Notes `mesh.isVisible = false` when off-screen (RTT cost is per-texture, not per-visible-pixel)
- Refers to `babylon-shader-specialist` if the health bar needs a custom shader (e.g. gradient based on fill amount)
- Does NOT produce a full React HUD implementation — defers React to `ui-programmer`

### Case 2: Architecture decision — React HUD vs. Babylon GUI
**Input:** "Should the main menu be in Babylon GUI or React?"
**Expected behavior:**
- Recommends React HUD for complex screen-space UI (menus, HUD, inventory, settings) — belongs to `ui-programmer`
- Recommends Babylon GUI only for world-space UI (billboards, in-scene labels) — this specialist's domain
- Notes the boundary rules:
  - Engine `{ alpha: false }`; React `#root` transparent with `pointer-events: none` (children opt back in)
  - When React modal is open, block canvas pointer handling
  - Both UI systems read from the same Zustand store — no direct UI-to-UI coupling
- Escalates to `lead-programmer` if the project hasn't committed to React for HUD
- Defers React implementation to `ui-programmer`; this specialist only defines the boundary

### Case 3: Wrong-engine redirect
**Input:** "Build this UI in UMG widget blueprints."
**Expected behavior:**
- Does NOT produce Unreal UMG/CommonUI widget code
- Identifies this as an Unreal pattern, not a Babylon pattern
- Maps the concepts: UMG widget → Babylon GUI `Control`; CommonUI `UCommonActivatableWidget` → Babylon `Container` screen management pattern; UMG `FText` localization → Babylon `JsonStyleAsset` + localization keys
- Confirms the project is Babylon.js-based before proceeding

### Case 4: Performance anti-pattern
**Input:** "I have 200 damage number popups spawning and disposing per frame."
**Expected behavior:**
- Identifies this as the create/destroy anti-pattern
- Prescribes pooling: pre-create N damage-number planes at scene load, reuse them, set `isVisible = false` on release
- Notes that each unique `AdvancedDynamicTexture` is an RTT pass — minimize unique textures; share atlases
- Notes that the `Observer` handle on `onPointerObservable` must be kept for `.remove()` on teardown — leak across scene reloads otherwise
- Refers to `performance-analyst` for actual UI render cost profiling with Spector.js
- Does NOT suggest a different UI system (React HUD) for damage numbers — those are world-space, this specialist's domain

### Case 5: Gamepad focus management
**Input:** "The player is using a gamepad but the UI doesn't respond."
**Expected behavior:**
- Identifies that Babylon GUI does not auto-handle gamepad navigation — explicit focus management is required
- Describes: track `manager.focusedControl`, map D-pad / left-stick to focus changes, map A button to `onPointerClickObservable` of the focused control
- Notes `DeviceSourceManager` (see `input.md`) for active device detection
- Notes that the React HUD side has its own focus management — coordinate via the Zustand store, not direct calls
- Refers to `accessibility-specialist` for full accessibility compliance
- Does NOT recommend the legacy `ActionManager` scene-wide array (deprecated per #12620)

### Case 6: State separation violation
**Input:** "I'm storing a reference to the AdvancedDynamicTexture in my Zustand store."
**Expected behavior:**
- Identifies this as the state separation violation
- Explains: Zustand store must hold plain serializable data only; never `Mesh`/`Scene`/`AdvancedDynamicTexture`/`Sound` references
- Prescribes: store UI state as plain data (which screen is open, what health value to display), let the UI subscribe and reconcile
- Notes this rule is the foundation for undo/replay and future networking sync (see `networking.md`)
- Escalates to `lead-programmer` if the state architecture is broken at a fundamental level

### Case 7: Migration to React HUD — diagnostic path, not hard threshold
**Input:** "I have 50 health bars in my scene — should I migrate them from Babylon GUI to React HUD?"
**Expected behavior:**
- Does NOT recommend migration based on the control count alone (50 is not a threshold)
- Walks the diagnostic path: profile first, migrate only if a trigger fires
- Triggers to check (any one suffices):
  - Spector.js shows > 2 RTT passes/frame attributable to Babylon GUI
  - Babylon GUI frame time exceeds the 2ms budget on the target device
  - Per-frame `Observer` fan-out on N health bars is measurably slower than React batched DOM updates (verify with a stress test, not intuition)
  - UI requires rich text editing, complex a11y trees, or browser-native widgets
- If none fire → keep Babylon GUI; do NOT migrate speculatively
- If a trigger fires → confirm with `babylon-js-specialist` that the issue is UI-layer (not a scene-graph or shader bottleneck) before proposing migration
- Migration implementation itself is deferred to `ui-programmer`; this specialist provides the profiling evidence and defines the boundary
- Does NOT cite a fixed node count threshold (e.g. "50 controls") as the trigger

---

## Protocol Compliance

- [ ] Stays within declared domain (in-canvas UI: world-space + screen-space Babylon GUI, theming, focus, pooling)
- [ ] Owns the integration boundary definition but defers React implementation to `ui-programmer`
- [ ] Redirects Unity/Godot/Unreal UI patterns to appropriate specialists or flags them as wrong-engine
- [ ] Redirects world-space UI custom shaders to `babylon-shader-specialist`
- [ ] Redirects overall Babylon engine configuration to `babylon-js-specialist`
- [ ] Redirects accessibility compliance to `accessibility-specialist`
- [ ] Treats `docs/engine-reference/babylonjs/VERSION.md` and `modules/gui.md` as authoritative over LLM training data
- [ ] Flags post-cutoff (v9.0..9.23) API usage with verification requirements — never suggests v9 APIs from training memory without consulting the reference docs
- [ ] Enforces ES6 named deep imports (`@babylonjs/core/...`) — never `import * as BABYLON from "babylonjs"`
- [ ] Enforces state separation: no `Mesh`/`Scene`/`Sound` references in the Zustand store
- [ ] Returns structured decision guides (React vs. Babylon GUI, pooling patterns, focus management), not freeform opinions

---

## Coverage Notes
- World-space UI (Case 1) verifies the agent uses `CreateForMesh` + `billboardMode` + pooling, not a hand-rolled approximation
- React HUD boundary (Case 2) verifies the agent escalates architecture decisions rather than resolving them unilaterally
- Create/destroy anti-pattern (Case 4) verifies the agent recognizes pooling as the fix, not a UI system swap
- Gamepad focus (Case 5) verifies the agent knows Babylon GUI does not auto-handle gamepad navigation
- State separation (Case 6) verifies the agent enforces the no-engine-refs-in-store rule
- Migration diagnostic path (Case 7) verifies the agent uses profiling-driven triggers (RTT passes, frame budget, stress test), not a hard control-count threshold, to decide React HUD migration
