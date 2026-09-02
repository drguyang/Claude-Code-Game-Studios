# Agent Test Spec: babylon-shader-specialist

## Agent Summary
Domain: Babylon.js v9 rendering customization — Node Material Editor (NME) graphs, custom GLSL/WGSL `Effect`/`ShaderMaterial`, PBR material setup (ORM channel packing), post-process pipelines, and rendering performance (draw calls, overdraw, shader complexity) under WebGL2 and WebGPU.
Does NOT own: actual Babylon architecture decisions / engine choice (`Engine` vs `WebGPUEngine`) — delegates to `babylon-js-specialist`. Does NOT own: world-space Babylon GUI materials — delegates material concerns to this specialist but UI control layout to `babylon-ui-specialist`. Does NOT own: GPU profiling runs — delegates to `performance-analyst`.
Model tier: Sonnet (default for engine specialists).
No gate IDs assigned.

---

## Static Assertions (Structural)

- [ ] `description:` field is present and references Babylon.js v9 shader/material concepts (NME, GLSL/WGSL, PBR, post-process, WebGL2/WebGPU)
- [ ] `allowed-tools:` list includes Read, Write, Edit, Bash, Glob, Grep (and may include Task)
- [ ] Model tier is Sonnet (default for engine specialists)
- [ ] Agent definition references `docs/engine-reference/babylonjs/VERSION.md` as the authoritative API source
- [ ] Agent definition instructs to consult `deprecated-apis.md` and `breaking-changes.md` before suggesting any shader/material API
- [ ] Frontmatter enforces "never `import * as BABYLON`" discipline (deep ES6 imports)

---

## Test Cases

### Case 1: In-domain request — appropriate output
**Input:** "I need a water shader for the ocean in my Babylon v9 scene. Should I use NME or a custom Effect?"
**Expected behavior:**
- Produces a decision tree covering:
  - **NME**: artist-editable, supports visual editing, reusable blocks via Frames; better for materials with many artist-tunable parameters (wave amplitude, foam cutoff, refraction depth)
  - **Custom `Effect` / `ShaderMaterial`**: lighter-weight, full GLSL control; better for full-screen post-processes or VFX requiring loops that NME built-ins can't express
- Recommends NME for an ocean water material (artist will want to tune wave parameters)
- Notes that the NME graph must be saved as `NME_Env_Water.json` per project naming convention
- Notes ORM channel packing rule (AO/Roughness/Metallic in one texture) if relevant
- Refers to `babylon-js-specialist` for engine (`Engine` vs `WebGPUEngine`) choice if the water shader uses compute-only features (WebGPU only)
- Refers to `performance-analyst` for actual GPU profiling on the target device
- Does NOT produce full scene-assembly code — describes the material architecture and defers implementation to `gameplay-programmer`

### Case 2: Wrong-engine redirect
**Input:** "Build this as a Shader Graph with HLSL for URP."
**Expected behavior:**
- Does NOT produce Unity Shader Graph / HLSL code
- Identifies this as a Unity pattern, not a Babylon pattern
- Maps the concepts: Unity Shader Graph → Babylon Node Material Editor (NME); HLSL → GLSL/WGSL (`Effect`/`ShaderMaterial`)
- Confirms the project is Babylon.js-based before proceeding

### Case 3: Post-cutoff API risk (v9 generation)
**Input:** "Use `HavokPhysicsPlugin` for the water interaction physics."
**Expected behavior:**
- Identifies this as outside the shader domain (it's a physics concern) — refers to `babylon-physics-specialist`
- Does NOT attempt to answer the physics question directly
- Notes that `HavokPhysicsPlugin` is deprecated/removed in v9 and `HavokPlugin` is the v2 class — directs to `docs/engine-reference/babylonjs/deprecated-apis.md`
- Does NOT confidently use pre-May-2025 training-data APIs without consulting the reference docs

### Case 4: Post-process architecture decision
**Input:** "Build a custom Gaussian bloom post-process."
**Expected behavior:**
- Recognizes that `DefaultRenderingPipeline` already provides bloom — checks whether built-in suffices first
- If built-in does not suffice (e.g. selective bloom on specific objects), describes a custom `PostProcess` two-pass blur (horizontal then vertical — never single-pass)
- Notes that each post-process adds a full-screen pass; profile with **Spector.js**
- Refers to `performance-analyst` for actual frame-time profiling
- Does NOT hand-roll a bloom pass that duplicates `DefaultRenderingPipeline` built-ins

### Case 5: Texture format choice
**Input:** "I'm loading my baseColor and normal maps as PNG."
**Expected behavior:**
- Corrects: baseColor should be JPEG q85 (lossy acceptable for color), normal/ORM should be PNG (lossless required)
- Notes KTX2/Basis as optional for ~70% texture memory savings on WebGL2/WebGPU targets
- Refers to the materials.md reference for the full texture format table
- Does NOT recommend uncompressed PNG for baseColor when JPEG q85 is visually identical

---

## Protocol Compliance

- [ ] Stays within declared domain (shaders, materials, post-process, texture formats, draw-call optimization for rendering)
- [ ] Redirects Unity/Godot/Unreal shader patterns to appropriate specialists or flags them as wrong-engine
- [ ] Redirects overall Babylon architecture / engine choice to `babylon-js-specialist`
- [ ] Redirects physics concerns to `babylon-physics-specialist`
- [ ] Redirects UI material effects coordination to `babylon-ui-specialist`
- [ ] Redirects GPU profiling runs to `performance-analyst`
- [ ] Treats `docs/engine-reference/babylonjs/VERSION.md` as authoritative over LLM training data
- [ ] Flags post-cutoff (v9.0..9.23) API usage with verification requirements — never suggests v9 APIs from training memory without consulting the reference docs
- [ ] Enforces ES6 named deep imports (`@babylonjs/core/...`) — never `import * as BABYLON from "babylonjs"`
- [ ] Returns structured decision guides (NME vs Effect, built-in vs custom post-process, texture formats), not freeform opinions

---

## Coverage Notes
- NME vs custom Effect (Case 1) should be documented as an ADR if it results in a project-level decision
- `HavokPhysicsPlugin` (Case 3) verifies the agent does not answer out-of-domain questions; refers physics to the physics specialist
- Post-process built-in-first rule (Case 4) verifies the agent checks `DefaultRenderingPipeline` before hand-rolling
- Texture format rule (Case 5) verifies the agent enforces the JPEG/PNG split documented in materials.md
