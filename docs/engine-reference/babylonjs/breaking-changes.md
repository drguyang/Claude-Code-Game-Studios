# Babylon.js - Breaking Changes (v9)

Last verified: 2026-08-30 | Engine: Babylon.js 9.23.0 | Baseline: v8.x (May 2025 cutoff)

Entries below are pulled from the official Babylon.js CHANGELOG (v9 series) and
are grouped by severity. When migrating an existing v8 project, work top-to-bottom.

## High - Will Break Existing Code

### `AbstractMesh` is now a real abstract class (#15160)
You can no longer `new AbstractMesh(...)` directly. Use a concrete mesh
(`Mesh`, `InstancedMesh`, `TransformNode`, etc.) or the `MeshBuilder`,
or subclass `AbstractMesh`.

```ts
// WRONG (v8): new AbstractMesh("m", scene)
// RIGHT (v9): MeshBuilder.CreateBox("box", { size: 1 }, scene)
```

### Deprecated WebVR removed (#14439)
`WebVR` support is gone. Use `WebXR` for any VR work.

```ts
// WRONG: new WebVRDefaultExperience(engine)
// RIGHT: const defaultVR = await WebXRDefaultExperience.CreateAsync(scene)
```

### `ThinInstanceBuffer` `staticBuffer` default changed (#14679)
The default value for the `staticBuffer` argument to `ThinInstanceBuffer`
changed. Always pass the buffer explicitly; do not rely on the default.

### WebGPUEngine decoupled from Engine (#14931)
`WebGPUEngine` no longer inherits from the WebGL `Engine`. Construct and configure
it directly (it is now a standalone `AbstractEngine` implementation). Do not pass
WebGL options to a WebGPU engine.

### OBJ/MTL loader default changes (#17168 / #18817)
- Missing mesh attributes now fall back to new defaults (e.g. when normals are
  missing, the loader generates them instead of leaving them undefined).
- Text encoding detection for OBJ/MTL was added; existing exports with non-UTF8
  encodings may now be read differently.

### glTF serialization changes (#13909 / #16468)
- The glTF serializer no longer bakes left-handed (LH) to right-handed (RH);
  transform conventions changed.
- Missing node metadata handling changed (#16468). Re-export/re-import fixtures
  that relied on the old LH-to-RH baking.

### NPE texture block `.texture` updates (#18058)
Node Material Editor: the `Texture` block's `.texture` behavior changed; code that
manually assigns the block texture may need updating.

## Medium - Behavior Changes

### Animation relationships now use unique IDs (#17757)
Animation relationship lookups moved from names to `uniqueId`. Code that cached
animation references by name should switch to unique IDs.

### Morph target manager refactor (#16014)
Morph targets refactored and now support disabling position morphing. The manager
API surface changed; verify morph callers after upgrade.

### `BabylonFileParser` moved out of the scene (#15619)
Scene serialization parsing was restructured. Custom scene-loading logic that
touched `scene.xxxParser` needs to import the parser from its new module location.

### Rendering engine alpha support fix (#16144)
Several `ArcRotateCamera`/alpha rendering defaults changed. If you relied on the
previous alpha behavior, verify transparency rendering and `camera.alpha`.

### Reset of ideal width/height on serialization (#16084)
Certain cameras reset `idealWidth`/`idealHeight` during serialization. Re-check any
serialization round-trips that set these values explicitly.

### Device input: MouseWheel pollInput removed from EventFactory (#12397)
Mouse wheel events are no longer routed through the `pollInput` path. Use the
standard `Scene.onPointerObservable` / pointer wheel events instead of the old
`pollInput` wheel handling.

## Notes

- `xrCompatible` is now `false` by default; the canvas is made XR-compatible on
  demand (#15027). Enable it explicitly when using WebXR.
- `ArrayBufferView` is now accepted as an input type for loading scene content
  (#13946) - an additive change, not breaking.

## References

- Full changelog: https://github.com/BabylonJS/Babylon.js/blob/master/CHANGELOG.md
- PRs link to specific breaking commits above.
