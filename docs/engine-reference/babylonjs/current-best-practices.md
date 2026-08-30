# Babylon.js - Current Best Practices (v9)

Last verified: 2026-08-30 | Engine: Babylon.js 9.23.0

Practices that differ from the model's pre-May-2025 training data, for a modern
Vite + TypeScript + Babylon.js v9 + React HUD stack.

## Module Imports - ES6, Tree-Shakeable

Use named deep imports, never `import * as BABYLON`. ESLint `no-restricted-imports`
for `babylonjs` barrel is recommended.

```ts
// good
import { Engine } from "@babylonjs/core/Engines/engine";
import { Scene } from "@babylonjs/core/scene";
import { MeshBuilder } from "@babylonjs/core/Meshes/meshBuilder";
import { HavokPlugin } from "@babylonjs/core/Physics/v2/Plugins/havokPlugin";
```

Pin every `@babylonjs/*` package to the exact same version (all `9.23.0`).

## Engine & Scene Creation

```ts
const engine = new Engine(canvas, true, {
  alpha: false,                 // avoids Win/Chromium compositor black-screen
  preserveDrawingBuffer: false,
  stencil: true,
});
engine.hardwareScalingLevel = 1; // crisp at native res; use 0.5+ for perf fallback
const scene = new Scene(engine);
scene.clearColor = Color4.FromHexString("#10131a");
```

#### WebGPU with WebGL2 fallback

Babylon v9 supports WebGPU well, but on older devices/some browsers it falls back to WebGL2. Always handle engine creation with an explicit fallback (note: `WebGPUEngine.CreateAsync` returns a **Promise that can reject** - it is NOT a nullable value, so a `??` fallback will not catch a missing-WebGPU failure; use try/catch):

```ts
import { WebGPUEngine } from "@babylonjs/core/Engines/webgpuEngine";

let engine: AbstractEngine;
if (WebGPUEngine.IsSupported) {
  try { engine = await WebGPUEngine.CreateAsync(canvas, { antialias: true }); }
  catch { engine = new Engine(canvas, true); }
} else {
  engine = new Engine(canvas, true);
}
```

> Verify this against the project's actual bootstrap pattern before shipping.

- **WebGL2 is the baseline.** Typings use `WebGL2RenderingContext`. Feature-detect
  WebGL2; fall back to WebGPU only if you explicitly configure the WebGPU engine.
- Build for **Web GPU as an optional engine**, not a requirement, to keep desktop
  (Electron/WebGL) parity simple.

## Fixed Timestep + Physics (Havok v2)

Decouple physics from render framerate. Use a fixed step (1/60) and accumulate.

```ts
const hk = await Havok();
const plugin = new HavokPlugin(true, hk); // 2nd arg = the awaited Havok instance
scene.enablePhysics(Vector3.Zero(), plugin);
```

```ts
// fixed-step loop
let acc = 0; const STEP = 1 / 60;
engine.runRenderLoop(() => {
  acc += engine.getDeltaTime() / 1000;
  while (acc >= STEP) { scene._advancePhysicsEngineStep(STEP); acc -= STEP; }
  scene.render();
});
```

## Perf: Thin Instances over Spawned Meshes

Never create meshes in a hot loop. Batch repeated geometry with thin instances
(backed by a `Float32Array` of matrices), and reuse `AssetContainer` on teardown.

```ts
const matrices = new Float32Array(count * 16); // fill once
const source = MeshBuilder.CreateBox("src", {}, scene);
const instances = scene.createThinInstances("batch", "shader", source, matrices, false);
```

## React HUD Overlay

Use React + DOM over the canvas for complex UI (menus, HUD, state panels).
Babylon GUI is reserved for world-space labels/crosshairs.

```css
/* #root must be transparent, or it occludes the canvas */
#root { position: absolute; inset: 0; background: transparent; pointer-events: none; }
/* re-enable pointer events on interactive children */
```

Set the React root above the canvas with `z-index`, and keep engine
`{ alpha: false }` so the WebGL buffer composites cleanly.

## State Separation

Keep game state in plain data (e.g. Zustand), never store `Mesh`/`Scene` references
inside store objects. This preserves serialization, undo/replay, and future
networking (Colyseus-style sync).

## Asset Pipeline

- glTF mesh: use **Draco** compression (`@babylonjs/loaders` + `dracoDecoders`).
- Textures: baseColor → **JPEG q85**; normal/ORM → **PNG** (lossless channels).
- KTX2/Basis is supported and optional; prefer it only when WebGL2/WebGPU target
  and you need GPU-compressed textures.
- Asset import via `AssetContainer`; inspect `container.meshes`, then
  `container.addAllToScene()` (returns `void` in v9) or transform manually.

## Testing / QA On This Host

- **Headless Chromium uses SwiftShader (software rasterizer): FPS is NOT real.**
  Do not gate QA on headless FPS. Use a headed GPU run for real numbers.
- QA script: engine `_drawCalls.current` for draw-call counts (not `drawCalls`)
  and a real FPS measurement via `engine.getFps()`.

## Node Material / Editor MCP (optional)

- The official Babylon editor MCP (`@babylonjs/mcp-servers`, stdio) is for the
  **visual editors only** (NME/NGE/NRGE/NPE/GUI/FlowGraph/SmartFilters), not docs.
- On Windows run via full `npx.cmd` path. Node-material docs come from
  `doc.babylonjs.com` / GitHub directly.

## Version Awareness

- Babylon 9.23 is beyond the May 2025 cutoff. Before suggesting any API, check
  `deprecated-apis.md`, then `breaking-changes.md`. Use WebSearch/WebFetch to
  verify anything uncertain against `doc.babylonjs.com`.
