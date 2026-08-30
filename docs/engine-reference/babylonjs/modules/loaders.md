# Babylon.js Loading & Assets - Quick Reference

Last verified: 2026-08-30 | Engine: Babylon.js 9.23.0

## Scene & Asset Loading

Use `@babylonjs/loaders` for glTF/OBJ/STL/KTX2 etc. All loaders are async.

```ts
import "@babylonjs/loaders/glTF";
import { SceneLoader } from "@babylonjs/core/Loading/sceneLoader";
import { AssetContainer } from "@babylonjs/core/assetContainer";
```

### AssetContainer

```ts
const container = new AssetContainer(scene);
await SceneLoader.ImportMeshAsync("", baseUrl, "file.glb", scene, (m) => {
  container.meshes.push(...m);
});
// v9: addAllToScene() returns void - inspect container.meshes first
container.addAllToScene();
for (const mesh of container.meshes) { /* move, scale, disable collision */ }
```

## Serialized Scene (.babylon / asset)

- `SceneLoader.AppendAsync` / `LoadAsync` for `.babylon` files.
- **Scene-content input now accepts `ArrayBufferView`** (#13946) - you can load
  from typed arrays, not just strings/URLs.

## glTF (GLB/GLTF)

- Draco: `import "@babylonjs/loaders/glTF/2.0/Extensions/..."` and configure
  `dracoDecoders` (WASM path) so compressed meshes load.
- **glTF serializer no longer bakes LH to RH** (#13909). Coordinate conventions
  changed. Re-export/import fixtures that relied on the old hand conversion.
- **glTF 1.0 modules are deprecated** (#12648) - standardize on glTF 2.0.

## OBJ / MTL

- v9 added **text-encoding detection** for OBJ/MTL (#18817) and changed the default
  fallbacks for missing mesh attributes (#17168). Existing assets may read slightly
  differently - verify after upgrade.

## Disposal / Memory

- Use `container.dispose()` and `PhysicsAggregate.dispose()` on teardown.
- Prefer pooling + `AssetContainer` reuse over creating/destroying meshes in loops.
- Never rely on the old sync `import * as BABYLON` barrel in bundles - it defeats
  tree-shaking and bloats the JS.

## References

- SceneLoader: `doc.babylonjs.com/features/importers`
