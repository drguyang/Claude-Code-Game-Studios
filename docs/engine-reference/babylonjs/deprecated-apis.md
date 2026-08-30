# Babylon.js - Deprecated APIs (v9 -> Use This Instead)

Last verified: 2026-08-30 | Engine: Babylon.js 9.23.0

Use these tables as a quick "don't use X, use Y" lookup. These are the APIs most
likely to be suggested by a model trained before May 2025.

## Rendering / Engine

| Don't use      | Use instead | Notes |
|----------------|-------------|-------|
| `WebVR*`       | `WebXR*`    | WebVR removed in v9 (#14439). `WebXRDefaultExperience.CreateAsync(scene)` for VR. |
| `engine.drawCalls` | `engine._drawCalls.current` | `drawCalls` is not a public getter; read the PerfCounter from `_drawCalls`. |
| `new AbstractMesh(name, scene)` | a concrete `Mesh`/`TransformNode` or `MeshBuilder` | `AbstractMesh` is now abstract (#15160). |
| `import * as BABYLON from "babylonjs"` | deep ES6 named imports | `import { Engine } from "@babylonjs/core/Engines/engine";` - enables tree-shaking. |

## Physics (v2)

| Don't use      | Use instead | Notes |
|----------------|-------------|-------|
| `HavokPhysicsPlugin` | `HavokPlugin` | Correct class in v9 is `HavokPlugin` from `@babylonjs/core/Physics/v2/Plugins/havokPlugin`. |
| `new HavokPlugin(true)` | `new HavokPlugin(true, await Havok())` | Pass the awaited `Havok()` instance as the 2nd constructor arg. |
| Ammo.js physics | Havok physics (v2 plugin) | Havok is the supported v2 path; Ammo is legacy. |

## Assets / Loading

| Don't use      | Use instead | Notes |
|----------------|-------------|-------|
| Pyramidal/legacy glTF 1.0 loader | glTF 2 `@babylonjs/loaders` | glTF 1.0 modules deprecated (#12648). |
| `container.addAllToScene()` for meshes | iterate `container.meshes` | `addAllToScene()` returns `void` in v9; read `container.meshes` (AbstractMesh[]) to add/transform. |
| `Resources.Load`-style sync asset access | `AssetContainer`/`@babylonjs/loaders` async loaders | Use async `SceneLoader`, not sync assumptions. |

## Input / GUI

| Don't use      | Use instead | Notes |
|----------------|-------------|-------|
| scene-wide action manager array | per-node `ActionManager` / observable-based input | scene action-manager array deprecated (#12620). |
| wheel via `pollInput` path | `Scene.onPointerObservable` wheel events | MouseWheel pollInput removed from EventFactory (#12397). |

## Texturing / Materials

| Don't use      | Use instead | Notes |
|----------------|-------------|-------|
| `PBRMaterial` default without explicit config | call `pbr.alphaMode` / texture channel setup explicitly | overlay textures can leak into shared PBR materials (#18828); bind carefully. |
| Assigning `texture` block `.texture` manually in NME | use the node's input socket | NPE texture block `.texture` behavior changed (#18058). |

## Camera / Scene

| Don't use      | Use instead | Notes |
|----------------|-------------|-------|
| Constructor `alpha` defaults for ArcRotateCamera | pass `alpha`/`beta` explicitly (see breaking-changes) | Rendering alpha fix changed defaults (#16144). |

## Conventions

- Peer `@babylonjs/*` packages MUST be pinned to the SAME version as `@babylonjs/core`
  (e.g. all `9.23.0`). Mismatched minor versions cause silent runtime breakage.
