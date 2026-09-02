# Agent Test Spec: babylon-physics-specialist

## Agent Summary
Domain: Babylon.js v9 Havok v2 physics — `HavokPlugin` integration (WASM init), `PhysicsAggregate`/`PhysicsBody` setup, collision filtering (groups + masks), fixed-timestep simulation decoupled from render, ray casting, sleeping, and client-side prediction coordination with `network-programmer`.
Does NOT own: actual Babylon architecture / engine choice — delegates to `babylon-js-specialist`. Does NOT own: gameplay logic (character controllers, damage rules) — delegates to `gameplay-programmer`. Does NOT own: VFX synced to physics events — delegates material/shader concerns to `babylon-shader-specialist`. Does NOT own: networking protocol — delegates to `network-programmer` (this specialist implements the client-side prediction portion that bridges physics and networking).
Model tier: Sonnet (default for engine specialists).
No gate IDs assigned.

---

## Static Assertions (Structural)

- [ ] `description:` field is present and references Babylon.js v9 Havok v2 concepts (`HavokPlugin`, `PhysicsAggregate`/`PhysicsBody`, collision filtering, fixed-timestep)
- [ ] `allowed-tools:` list includes Read, Write, Edit, Bash, Glob, Grep (and may include Task)
- [ ] Model tier is Sonnet (default for engine specialists)
- [ ] Agent definition references `docs/engine-reference/babylonjs/VERSION.md` as the authoritative API source
- [ ] Agent definition references `docs/engine-reference/babylonjs/modules/physics.md` for the current physics surface
- [ ] Agent definition explicitly mentions the v9 breaking changes: `HavokPhysicsPlugin` removed, `HavokPlugin` requires awaited WASM instance, `PhysicsAggregate` options mutation (#18555)
- [ ] Frontmatter enforces "never `import * as BABYLON`" discipline (deep ES6 imports)

---

## Test Cases

### Case 1: In-domain request — Havok setup
**Input:** "Set up Havok physics in my Babylon v9 scene."
**Expected behavior:**
- Describes the v9 initialization sequence:
  - `import Havok from "@babylonjs/core/Physics/v2/Plugins/havok.wasm";`
  - `const hk = await Havok();` (async WASM init)
  - `scene.enablePhysics(new Vector3(0, -9.81, 0), new HavokPlugin(true, hk));`
- Notes that `HavokPhysicsPlugin` (pre-v9) is removed; `HavokPlugin` (v2) is the correct class
- Notes that the single-arg `new HavokPlugin(true)` constructor is a pre-v9 pattern — the awaited WASM instance is required as the 2nd arg
- Notes that scene assembly must be gated on the `await Havok()` promise — no body creation before init
- Refers to `babylon-js-specialist` for engine (`Engine` vs `WebGPUEngine`) choice if WebGPU affects physics
- Does NOT produce a full scene — describes the physics setup and defers body layout to `gameplay-programmer`

### Case 2: Fixed-timestep implementation
**Input:** "Run physics on `engine.getDeltaTime()`."
**Expected behavior:**
- Identifies this as the variable-timestep anti-pattern (jitter, tunneling, stacking instability)
- Prescribes the fixed-timestep + accumulator pattern:
  - `const STEP = 1/60;`
  - `let acc = 0;`
  - `scene.onBeforeRenderObservable.add(() => { acc += dt; while (acc >= STEP) { scene._advancePhysicsEngineStep(STEP); acc -= STEP; } });`
- Notes the cap on the while loop (spiral of death on slow devices)
- Notes optional interpolation between physics ticks for smoothness at high render framerates
- Notes that physics runs in `onBeforeRenderObservable`, NOT `onAfterRenderObservable` (one-frame lag otherwise)
- Refers to `performance-analyst` for actual frame-time profiling
- Does NOT suggest running physics in the render delta directly

### Case 3: Post-cutoff API risk (v9 generation)
**Input:** "Reuse this `PhysicsAggregate` options object across multiple aggregates."
**Expected behavior:**
- Identifies the #18555 mutation hazard — `PhysicsAggregate` mutates the caller's options object
- Prescribes passing a copy: `new PhysicsAggregate(mesh, { ...opts })` for each aggregate
- Notes that this is a v9-specific gotcha documented in `breaking-changes.md`
- Does NOT confidently use pre-May-2025 training-data behavior without consulting the reference docs

### Case 4: Wrong-domain redirect — VFX
**Input:** "Make the crate spawn impact sparks when it hits the ground."
**Expected behavior:**
- Identifies the VFX portion as outside the physics domain
- Describes the physics side: subscribe to `body.onCollisionObservable`, detect the impact event, extract impact position/velocity
- Refers to `babylon-shader-specialist` for the actual VFX (particle system, spark shader, damage vignette)
- Notes that the physics specialist provides the *event* and *data*; the shader specialist implements the *visual*
- Does NOT implement the particle system directly

### Case 5: Wrong-engine redirect
**Input:** "Set up a `PhysicsImpostor` with cannon.js."
**Expected behavior:**
- Does NOT produce cannon.js / `PhysicsImpostor` code
- Identifies cannon.js as a pre-v9 / legacy backend; v9 standardizes on Havok v2
- Maps the concepts: `PhysicsImpostor` (v1) → `PhysicsAggregate` / `PhysicsBody` (v2); cannon.js → Havok
- Notes that `PhysicsImpostor` is deprecated for new work — only use when porting legacy code
- Confirms the project is Babylon.js v9 + Havok before proceeding

### Case 6: State separation violation
**Input:** "Store the `PhysicsAggregate` reference in my Zustand store."
**Expected behavior:**
- Identifies this as the state separation violation
- Explains: store must hold plain serializable data (positions, velocities as arrays) — never `PhysicsAggregate`/`Mesh`/`Scene` references
- Notes this is mandatory for client-side prediction + server reconciliation (see `networking.md`)
- Prescribes: read `TransformNode.position` into the store as a plain `[x, y, z]` tuple; let the physics specialist and `network-programmer` sync the `TransformNode` directly, not via the store
- Escalates to `lead-programmer` if the state architecture is broken at a fundamental level

### Case 7: Client-side prediction coordination
**Input:** "How do I do client-side prediction for multiplayer?"
**Expected behavior:**
- Describes the physics-side portion: run the same Havok step on the same inputs locally (predicted), compare to server snapshot, snap on mis-prediction and replay buffered inputs
- Notes that `TransformNode` is what you sync — read it on snapshot, set it on reconcile
- Refers to `network-programmer` for the actual networking protocol (Colyseus snapshot frequency, input sequencing)
- Notes the state separation rule — no `Mesh`/`Scene` references in the store
- Does NOT implement the networking protocol — only the client-side prediction portion that bridges physics and networking

---

## Protocol Compliance

- [ ] Stays within declared domain (Havok v2 setup, body types, collision filtering, fixed-timestep, ray casting, sleeping, client-side prediction physics-side)
- [ ] Redirects Unity/Godot/Unreal physics patterns to appropriate specialists or flags them as wrong-engine
- [ ] Redirects gameplay logic (character controllers, damage rules) to `gameplay-programmer`
- [ ] Redirects VFX synced to physics events to `babylon-shader-specialist`
- [ ] Redirects networking protocol to `network-programmer` (only implements the prediction-side bridge)
- [ ] Redirects overall Babylon architecture to `babylon-js-specialist`
- [ ] Redirects frame-time profiling to `performance-analyst`
- [ ] Treats `docs/engine-reference/babylonjs/VERSION.md` and `modules/physics.md` as authoritative over LLM training data
- [ ] Flags post-cutoff (v9.0..9.23) API usage with verification requirements — never suggests v9 APIs from training memory without consulting the reference docs
- [ ] Enforces ES6 named deep imports (`@babylonjs/core/...`) — never `import * as BABYLON from "babylonjs"`
- [ ] Enforces state separation: no `Mesh`/`Scene`/`PhysicsAggregate` references in the Zustand store
- [ ] Returns structured decision guides (body types, collision matrix, timestep strategy), not freeform opinions

---

## Coverage Notes
- Havok v2 setup (Case 1) verifies the agent uses the awaited WASM instance, not the single-arg constructor
- Fixed timestep (Case 2) verifies the agent rejects variable-timestep physics
- #18555 mutation (Case 3) verifies the agent knows the v9-specific gotcha
- VFX boundary (Case 4) verifies the agent delegates visuals to the shader specialist
- `PhysicsImpostor` (Case 5) verifies the agent recognizes v1 vs v2 distinction
- State separation (Case 6) verifies the agent enforces the no-engine-refs-in-store rule
- Client prediction (Case 7) verifies the agent knows the physics/networking boundary
