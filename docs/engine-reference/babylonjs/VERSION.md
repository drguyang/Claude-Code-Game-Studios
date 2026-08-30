# Babylon.js - Version Reference

| Field | Value |
|-------|-------|
| **Engine Version** | Babylon.js 9.23.0 (`@babylonjs/core`) |
| **Release Date** | 2026-08-27 |
| **Project Pinned** | 2026-08-30 |
| **Last Docs Verified** | 2026-08-30 |
| **LLM Knowledge Cutoff** | May 2025 |

## Knowledge Gap Warning

Babylon.js v9 launched in March 2026 - **well beyond the May 2025 training cutoff**.
The model does NOT know the v9 generation APIs (Havok physics v2 plugin, WebGL2
baseline, `AbstractMesh` as an abstract class, the WebGPU engine split, glTF
serializer changes). **Always cross-reference this directory before suggesting
Babylon.js API calls.** See `breaking-changes.md` and `deprecated-apis.md`.

## Post-Cutoff Version Timeline

| Version | Release | Risk Level | Key Theme |
|---------|---------|------------|-----------|
| 9.0.0 | 2026-03-26 | HIGH | v9 generation: WebGL2 baseline, `AbstractMesh` truly abstract, WebGPUEngine decoupled from Engine, deprecated WebVR removed |
| 9.9.0 | 2026-05-21 | HIGH | Texturing/alpha pipeline rework, node material updates |
| 9.18.0 | 2026-07-23 | HIGH | Thin instances default changes, morph target manager refactor, OBJ/MTL defaults |
| 9.22.0 | 2026-08-20 | HIGH | Gaussian Splatting streaming LOD, OBJ encoding detection |
| 9.23.0 | 2026-08-27 | HIGH | Current latest dist-tag. WebXR room capture, budget-driven Gaussian Splat LOD, shader-loading state sharing |

All 9.x releases are HIGH risk relative to the May 2025 cutoff.

## Verified Sources

- NPM registry (dist-tags.latest): `https://registry.npmjs.org/@babylonjs/core`
- Official docs: `https://doc.babylonjs.com/`
- Changelog: `https://github.com/BabylonJS/Babylon.js/blob/master/CHANGELOG.md`
- Core package: `https://github.com/BabylonJS/Babylon.js/tree/master/packages/dev/core`
- Example playgrounds: `https://playground.babylonjs.com/`
