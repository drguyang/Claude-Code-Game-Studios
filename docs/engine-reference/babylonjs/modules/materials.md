# Babylon.js Materials & Textures - Quick Reference

Last verified: 2026-08-30 | Engine: Babylon.js 9.23.0

## Material Families

- **StandardMaterial** - cheap, legacy-style, good base.
- **PBRMaterial** - physically based; preferred for hero/realistic assets. Set
  `metallic`/`roughness`, and the environment texture for reflections.
- **OpenPBRMaterial** - the newer spec-superset added in recent v9 versions.

Import deep:
```ts
import { PBRMaterial } from "@babylonjs/core/Materials/PBR/pbrMaterial";
import { StandardMaterial } from "@babylonjs/core/Materials/standardMaterial";
```

## Texture Channels

Recommended channel mapping for a clean pipeline:

| Channel      | Format | Notes |
|--------------|--------|-------|
| baseColor / albedo | JPEG q85 | Lossy ok; small |
| normal        | PNG | Lossless channels |
| ORM (occlusion/roughness/metallic) | PNG | Lossless, packed RGB |
| emissive / lightmap | PNG | Lossless |

- Bind textures via `material.albedoTexture`, `.normalTexture`, `.metallicRoughness`, etc.
- Overlay/texture bindings can **leak into shared PBR materials** (#18828); bind
  explicitly and dispose textures you no longer need.

## Alpha

Set alpha mode deliberately:
```ts
material.alphaMode = Constants.ALPHA_COMBINE;      // opaque-ish
material.alphaMode = Constants.ALPHA_PREMULTIPLIED;
material.alphaMode = Constants.ALPHA_DISABLE;
```
v9 changed alpha defaults (#16144/#18818); verify transparency is what you expect.

## Loading & Compression

- glTF meshes: **Draco** compressed geometry via `@babylonjs/loaders` + `dracoDecoders`.
- **KTX2/Basis** is supported for GPU-compressed textures; enable only when you need
  it (WebGL2/WebGPU targets). Prefer JPEG/PNG for DOM-simple deployments.
- Always load via `AssetContainer`; `addAllToScene()` returns `void` in v9, so read
  `container.meshes` to apply transforms or clean up.

## Shader Loading

- Shader loading state is now **shared across material instances** (#18830):
  compile once, not per-material. Avoid creating N materials with identical shaders.
- Node Material Editor bundles are built per-backend; test both WebGL and WebGPU.

## References

- PBR: `doc.babylonjs.com/divingDeeper/materials/using/physicallyBasedRendering`
