# Agent Test Spec: babylon-js-specialist

## Agent Summary
Domain: Babylon.js v9 architecture patterns, ES6/tree-shaking imports, Havok physics v2, Thin Instances, WebGL2 vs WebGPU, React HUD overlay vs Babylon GUI, and AssetContainer disposal.
Does NOT own: actual TypeScript code authoring for gameplay (delegates to `gameplay-programmer`), React HUD implementation (delegates to `ui-programmer`), shader/VFX authoring (delegates to `technical-artist`), performance profiling runs (delegates to `performance-analyst`).
Model tier: Sonnet (default).
No gate IDs assigned.

---

## Static Assertions (Structural)

- [ ] `description:` field is present and references Babylon.js v9 patterns (Havok v2, Thin Instances, ES6 named imports, WebGL2/WebGPU)
- [ ] `allowed-tools:` list includes Read, Write, Edit, Bash, Glob, Grep (and may include Task)
- [ ] Model tier is Sonnet (default for engine specialists)
- [ ] Agent definition references `docs/engine-reference/babylonjs/VERSION.md` as the authoritative API source
- [ ] Agent definition instructs to consult `deprecated-apis.md` and `breaking-changes.md` before suggesting any Babylon API
- [ ] Frontmatter enforces "never `import * as BABYLON`" discipline

---

## Test Cases

### Case 1: In-domain request — appropriate output
**Input:** "I have 500 trees to render. Should I use InstancedMesh or Thin Instances in Babylon v9?"
**Expected behavior:**
- Produces a pattern decision tree covering:
  - Thin Instances: best for large batches of the *same* mesh backed by a `Float32Array` of matrices; lowest draw-call overhead; no per-instance picking by default
  - InstancedMesh: per-instance `TransformNode`-like control, easier picking, slightly higher overhead per instance
- Recommends Thin Instances for the 500-tree scenario (homogeneous geometry, no per-instance picking needed)
- Notes the matrix buffer must be created *once* (never inside the render loop)
- Refers to `technical-artist` for material batching and to `performance-analyst` for actual profiling on a headed GPU run
- Does NOT produce full scene-assembly code — describes the architecture and defers implementation to `gameplay-programmer`

### Case 2: Wrong-engine redirect
**Input:** "Set up a Node scene tree with signals for this enemy system."
**Expected behavior:**
- Does NOT produce Godot Node/signal code
- Identifies this as a Godot pattern, not a Babylon pattern
- Maps the concepts: Godot Node → Babylon `TransformNode`/`Mesh`, Godot Signal → Babylon `Observable` (e.g. `scene.onBeforeRenderObservable`, `mesh.onDisposeObservable`)
- Confirms the project is Babylon.js-based before proceeding

### Case 3: Post-cutoff API risk (v9 generation)
**Input:** "Use `new HavokPlugin(true)` to enable physics."
**Expected behavior:**
- Identifies that the single-arg constructor is a **pre-v9 / outdated** pattern
- Flags the v9 breaking change: `HavokPlugin` now requires the awaited `Havok()` WASM instance as the 2nd constructor argument
- Corrects to: `const hk = await Havok(); new HavokPlugin(true, hk);`
- Notes that the old class name `HavokPhysicsPlugin` is also deprecated — directs to `docs/engine-reference/babylonjs/deprecated-apis.md`
- Does NOT confidently use pre-May-2025 training-data APIs without consulting the reference docs first

### Case 4: React HUD vs Babylon GUI conflict
**Input:** "Build the main menu and HUD inside Babylon GUI."
**Expected behavior:**
- Recognizes this as an architecture decision, not a free-form choice
- Explains the recommended split: complex UI (menus, HUD, dialogs) → **React/DOM overlay**; world-space labels/crosshairs only → **Babylon GUI (`AdvancedDynamicTexture`)**
- Notes the engine must be created with `{ alpha: false }` and the React `#root` must use `background: transparent` + `pointer-events: none` so the canvas still receives pointer input
- Notes that `Mesh`/`Scene` references must NOT live in the Zustand store (state separation rule)
- Recommends escalating to `lead-programmer` if the project hasn't yet committed to React for HUD; defers React implementation to `ui-programmer`

### Case 5: Context pass — Babylon v9.23 version
**Input:** Engine version context provided: Babylon.js 9.23.0 (post-cutoff). Request: "Set up a fixed-timestep physics loop with Havok."
**Expected behavior:**
- Applies the v9.23 context: uses `HavokPlugin` (v2 plugin), not the legacy `HavokPhysicsPlugin`
- Reads `docs/engine-reference/babylonjs/VERSION.md` and applies the post-cutoff knowledge
- Produces a fixed-step accumulator loop decoupled from the render framerate (`scene._advancePhysicsEngineStep(STEP)` with `STEP = 1/60`)
- Notes that `PhysicsAggregate` mutates the caller's options object (#18555) — pass a copy when reusing options
- Does NOT suggest running physics from `engine.getDeltaTime()` directly (would cause tunneling/jitter)
- Does NOT assume the project is on v8 — uses v9 generation APIs throughout

### Case 6: Resource disposal on scene reload
**Input:** "Reload the boss room scene on every node entry."
**Expected behavior:**
- Recommends `AssetContainer` + `container.addAllToScene()` / `container.removeAllFromScene()` rather than whole-scene `scene.dispose()` + rebuild
- Notes that `container.cleanScene()` must be called before removing to release standalone resources
- Lists the leak-prone handles that need explicit `.remove()` / `.dispose()`: `onBeforeRenderObservable` observers, `AdvancedDynamicTexture`, `Sound`, `GlowLayer`, `PhysicsAggregate`
- Refers to `babylon-physics-specialist` for `PhysicsAggregate` disposal details; `babylon-shader-specialist` for `GlowLayer`/post-process disposal
- Refers to `gameplay-programmer` for the actual assembler implementation
- Does NOT recommend whole-scene `scene.dispose()` + reload for hot node transitions (texture reload cost, GC pressure)

### Case 7: Backend choice — WebGPU vs WebGL2
**Input:** "Should I use `WebGPUEngine` for my project?"
**Expected behavior:**
- Identifies this as a backend capability decision, not a version decision
- Defaults to `Engine` (WebGL2) for maximum platform compatibility; only switches to `WebGPUEngine` when target platform support is confirmed
- Prescribes a capability-detect entry point (e.g., `engineFactory.ts`) that probes `navigator.gpu` before constructing `WebGPUEngine` — never hardcodes the backend
- Notes that WebGPU compute shaders (e.g., for fluid or energy-field noise) belong to `babylon-shader-specialist`
- Refers to `performance-analyst` for actual GPU profiling on the target device
- Does NOT recommend `WebGPUEngine` as the default backend without capability confirmation
- Does NOT assume the target platform supports WebGPU (e.g., older Steam Deck drivers, WebView2 on Windows < 113)

## Protocol Compliance

- [ ] Stays within declared domain (Babylon architecture decisions, import discipline, Havok/Thin Instances setup, React HUD split)
- [ ] Redirects Godot/Unity/Unreal patterns to appropriate specialists or flags them as wrong-engine
- [ ] Redirects React HUD implementation to `ui-programmer`; world-space Babylon GUI stays in-domain
- [ ] Redirects shader/VFX work to `technical-artist`
- [ ] Redirects profiling runs to `performance-analyst`
- [ ] Treats `docs/engine-reference/babylonjs/VERSION.md` as authoritative over LLM training data
- [ ] Flags post-cutoff (v9.0..9.23) API usage with verification requirements — never suggests v9 APIs from training memory without consulting the reference docs
- [ ] Enforces ES6 named deep imports (`@babylonjs/core/...`) — never `import * as BABYLON from "babylonjs"`
- [ ] Returns structured pattern decision guides, not freeform opinions

---

## Coverage Notes
- Thin Instances vs InstancedMesh (Case 1) should be documented as an ADR if it results in a project-level decision
- HavokPlugin constructor (Case 3) confirms the agent does not confidently use APIs it cannot verify against the reference docs
- React HUD split (Case 4) verifies the agent escalates architecture conflicts rather than resolving them unilaterally
- Fixed-timestep loop (Case 5) verifies the agent applies v9-generation knowledge from the version reference, not pre-cutoff assumptions
- AssetContainer disposal (Case 6) verifies the agent enforces explicit handle cleanup on scene reload, not whole-scene dispose
- Backend capability detection (Case 7) verifies the agent treats WebGPU as opt-in based on target support, not a default
