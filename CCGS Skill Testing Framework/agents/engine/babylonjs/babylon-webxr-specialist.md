# Agent Test Spec: babylon-webxr-specialist

## Agent Summary
Domain: Babylon.js v9 WebXR — session lifecycle (`immersive-vr` / `immersive-ar` / `inline`), motion controller input (`WebXRInputSource`), hand-tracking (`WebXRHandTracking`), hit-testing (`WebXRHitTest`), anchors (`WebXRAnchor`), stereo render optimization (single-pass, fixed foveated rendering), and accessibility/comfort (frame rate, snap turning, vignette, motion sickness).
Does NOT own: overall Babylon architecture / engine choice — delegates to `babylon-js-specialist`. Does NOT own: shader adjustments for stereo — delegates to `babylon-shader-specialist`. Does NOT own: world-space UI in XR — delegates layout to `babylon-ui-specialist` (this specialist defines XR-specific UI constraints like no-DOM-surface in AR). Does NOT own: controller-grabbed physics — delegates to `babylon-physics-specialist`.
Model tier: Sonnet (default for engine specialists).
No gate IDs assigned.

---

## Static Assertions (Structural)

- [ ] `description:` field is present and references Babylon.js v9 WebXR concepts (session types, controllers, hand-tracking, hit-test, anchors, stereo, foveation, comfort)
- [ ] `allowed-tools:` list includes Read, Write, Edit, Bash, Glob, Grep (and may include Task)
- [ ] Model tier is Sonnet (default for engine specialists)
- [ ] Agent definition references `docs/engine-reference/babylonjs/VERSION.md` as the authoritative API source
- [ ] Agent definition instructs to consult the typedoc at `doc.babylonjs.com/typedoc/classes/BABYLON.WebXR` because the WebXR feature surface evolves quickly
- [ ] Frontmatter enforces "never `import * as BABYLON`" discipline (deep ES6 imports)

---

## Test Cases

### Case 1: In-domain request — session setup
**Input:** "Set up a VR session in my Babylon v9 scene for Quest 2."
**Expected behavior:**
- Describes the v9 setup sequence:
  - `const xr = await scene.createDefaultXRExperienceAsync({ optionalFeatures: [...] });`
  - `xr.baseExperience.enterXRAsync("immersive-vr", "local", renderTarget);`
- Notes Quest 2 target frame rate is 90 Hz (treat frame drops as severity-1)
- Notes that the user must initiate session entry (no forced entry)
- Notes lifecycle observables: `onXRSessionInitObservable` for setup, `onXRSessionEndedObservable` for cleanup
- Notes that anchor/tracked-object IDs should be persisted to `localStorage` for cross-session resume
- Refers to `babylon-js-specialist` for engine choice if the VR target affects the pipeline (rare)
- Does NOT produce a full scene — describes the XR setup and defers scene content to `gameplay-programmer`

### Case 2: Wrong-session-type redirect
**Input:** "Set up `immersive-vr` for my mobile AR placement game."
**Expected behavior:**
- Identifies the mismatch — mobile AR placement needs `immersive-ar`, not `immersive-vr`
- Prescribes: `enterXRAsync("immersive-ar", ...)`, enable `hit-test` and `anchors` features
- Notes that AR has no DOM surface — UI must be world-space Babylon GUI (refers to `babylon-ui-specialist`)
- Notes passthrough / light-estimation features if the runtime supports them
- Does NOT proceed with `immersive-vr` for an AR use case

### Case 3: Stereo rendering anti-pattern
**Input:** "Set up two cameras, one per eye."
**Expected behavior:**
- Identifies this as the two-camera anti-pattern
- Prescribes: Babylon v9 renders both eyes in a single pass; use `xr.baseExperience.camera` (the XRCamera handles eye-view matrices automatically)
- Notes that `scene.activeCamera` may be a 2D fallback camera — use `xr.baseExperience.camera` for head pose
- Refers to `babylon-shader-specialist` for shader adjustments needed for stereo (lower complexity, FFR compatibility)
- Does NOT set up two cameras

### Case 4: Comfort / motion sickness
**Input:** "The player moves via thumbstick and reports nausea."
**Expected behavior:**
- Identifies this as a comfort issue — forced camera acceleration causes motion sickness
- Prescribes: snap-turning as default option; smooth turning as opt-in
- Prescribes: vignette (darkening at screen edges) during motion
- Prescribes: optional comfort mode (no head-bob, no falling damage, reduced motion)
- Notes frame rate: Quest 2 requires 90 Hz; Quest Pro requires 120 Hz; treat drops as severity-1
- Refers to `ux-designer` for the comfort options UI and `accessibility-specialist` for one-handed mode / readable text size
- Does NOT dismiss the nausea as user error

### Case 5: Wrong-engine redirect
**Input:** "Set up an XR Interaction Toolkit locomotion system."
**Expected behavior:**
- Does NOT produce Unity XRI Toolkit code
- Identifies this as a Unity pattern, not a Babylon pattern
- Maps the concepts: Unity XRI locomotion → Babylon `WebXRControllerMovement` feature; Unity XR Rig → Babylon `xr.baseExperience.camera` + `WebXRMotionController`
- Confirms the project is Babylon.js-based before proceeding

### Case 6: AR UI surface
**Input:** "Show the placement confirmation dialog as a React modal."
**Expected behavior:**
- Identifies the mismatch — `immersive-ar` has no DOM surface; React HUD overlay is not visible
- Prescribes: world-space Babylon GUI panel at the placement reticle — refers layout to `babylon-ui-specialist`
- Notes that the React HUD overlay only works in `immersive-vr` with a mirrored 2D canvas, or in flat 2D mode
- Coordinates the boundary: this specialist defines the XR constraint (no DOM in AR), `babylon-ui-specialist` implements the world-space panel
- Does NOT suggest a React overlay for AR

### Case 7: Hand-tracking setup
**Input:** "Enable hand tracking for my VR experience."
**Expected behavior:**
- Describes: add `"hand-tracking"` to `optionalFeatures`, subscribe to `xr.input.onHandAddedObservable`
- Notes 25 joints per hand = 50 `TransformNode`s per frame (performance cost)
- Prescribes: simple primitive meshes for debug; skinned hand mesh for production
- Notes that controller meshes must be hidden when hands are tracked
- Notes that hand-tracking is less precise than controllers — trade-off decision belongs to `ux-designer`
- Refers to `performance-analyst` for actual frame-time profiling on the target device
- Does NOT enable hand-tracking without considering the perf cost

### Case 8: State separation violation
**Input:** "Store the `WebXRSessionManager` reference in my Zustand store."
**Expected behavior:**
- Identifies this as the state separation violation
- Explains: store must hold plain serializable data (session state enum, user pose as plain arrays) — never `WebXRSessionManager`/`WebXRInput`/`Mesh` references
- Notes this is mandatory for undo/replay and future networking sync
- Escalates to `lead-programmer` if the state architecture is broken at a fundamental level

---

## Protocol Compliance

- [ ] Stays within declared domain (WebXR session lifecycle, controllers, hand-tracking, hit-test, anchors, stereo, comfort)
- [ ] Redirects Unity/Godot/Unreal XR patterns to appropriate specialists or flags them as wrong-engine
- [ ] Redirects shader adjustments for stereo to `babylon-shader-specialist`
- [ ] Redirects world-space UI layout to `babylon-ui-specialist` (this specialist defines the XR constraint, not the layout)
- [ ] Redirects controller-grabbed physics to `babylon-physics-specialist`
- [ ] Redirects overall Babylon architecture to `babylon-js-specialist`
- [ ] Redirects comfort UX options to `ux-designer`; accessibility to `accessibility-specialist`
- [ ] Redirects frame-rate profiling to `performance-analyst`
- [ ] Treats `docs/engine-reference/babylonjs/VERSION.md` as authoritative over LLM training data
- [ ] Treats the typedoc at `doc.babylonjs.com/typedoc/classes/BABYLON.WebXR` as the fast-moving API source of truth
- [ ] Flags post-cutoff (v9.0..9.23) API usage with verification requirements — never suggests v9 APIs from training memory without consulting the reference docs
- [ ] Enforces ES6 named deep imports (`@babylonjs/core/...`) — never `import * as BABYLON from "babylonjs"`
- [ ] Enforces state separation: no `WebXRSessionManager`/`WebXRInput`/`Mesh` references in the Zustand store
- [ ] Returns structured decision guides (session types, controller vs hand-tracking, comfort modes), not freeform opinions

---

## Coverage Notes
- Session setup (Case 1) verifies the agent uses `createDefaultXRExperienceAsync` + lifecycle observables
- Wrong session type (Case 2) verifies the agent catches VR/AR mismatches
- Two-camera anti-pattern (Case 3) verifies the agent knows single-pass stereo
- Comfort (Case 4) verifies the agent treats motion sickness as a real issue, not user error
- AR UI surface (Case 6) verifies the agent knows AR has no DOM surface
- Hand-tracking (Case 7) verifies the agent knows the perf cost of 50 joints per frame
- State separation (Case 8) verifies the agent enforces the no-engine-refs-in-store rule
