# Babylon.js Rendering - Quick Reference

Last verified: 2026-08-30 | Engine: Babylon.js 9.23.0

## Baseline

- **WebGL2 is the baseline.** The typings are `WebGL2RenderingContext`. WebGL1 is
  effectively legacy; browser support for WebGL2 is now assumed.
- WebGPU is an **optional separate engine** (`WebGPUEngine`), decoupled from the
  WebGL `Engine` (#14931). Do not assume it by default.

## Engine Creation

```ts
const engine = new Engine(canvas, true, {
  alpha: false,                 // avoids compositor black-screen on Win/Chromium
  stencil: true,
  antialias: true,
});
engine.hardwareScalingLevel = 1; // native res; lower (0.5+) for perf fallback
```

### Alpha / transparency
- `alpha: false` on the Engine is the most robust for desktop Windows/Chromium.
- Do NOT give the React `#root` an opaque background - it occludes the canvas.
  Use `background: transparent` and control the clear color via `scene.clearColor`.

## Scene & Camera

```ts
const scene = new Scene(engine);
const camera = new ArcRotateCamera("cam", Math.PI / 2, Math.PI / 2, 8, target, scene);
camera.attachControl(canvas, true);
```

- `ArcRotateCamera`/realtime alpha behavior changed in v9 (#16144/#16084); pass
  `alpha`/`beta`/`radius` explicitly rather than relying on constructor defaults.

## Performance

- **Draw calls:** read `engine._drawCalls.current` (a PerfCounter). `engine.drawCalls`
  is not a public getter.
- **Thin Instances** for repeated geometry; create the matrix buffer once
  (`Float32Array`), never create meshes in a hot loop.
- **FPS**: `engine.getFps()` returns the real render-loop FPS.
- **GPU instancing / LOD** are native; use `Mesh.LODLevel` for distant copies.

## Coordinate & Picking

- Right-handed coordinate system is the default (v9 kept this).
- Picking: `scene.pick(scene.pointerX, scene.pointerY)`; unidirection via
  `scene.createPickingRay`.

## Shading

- Define materials in `@babylonjs/core/Materials/standardMaterial` or the PBR
  family; shader variants are compiled per-backend (WebGL/WebGPU/WGSL).
- Use the Node Material Editor (`@babylonjs/mcp-servers`) for node-based materials;
  the `Texture` block `.texture` behavior changed (#18058).

## References

- Engine: `doc.babylonjs.com/features/rendering`
- Thin instances: `doc.babylonjs.com/divingDeeper/mesh/optimize_your_scene_with_thin_instances`
