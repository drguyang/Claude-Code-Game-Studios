# Babylon.js Animation - Quick Reference

Last verified: 2026-08-30 | Engine: Babylon.js 9.23.0

Babylon.js v9 animation is built around the `Animation` class (a keyframe track)
attached to a target property, and an `Animatable` (the runtime player) per
target. `AnimationGroup` sequences multiple tracks; the **Morph Target Manager**
handles blendshape-style animation. Use deep ES6 imports - never `import * as BABYLON`.

## Core Classes

```ts
import { Animation } from "@babylonjs/core/Animations/animation";
import { Animatable } from "@babylonjs/core/Animations/animation";
import { AnimationGroup } from "@babylonjs/core/Animations/animationGroup";
```

## Creating a Keyframe Track

```ts
const slide = new Animation(
  "slide",                       // name
  "position.x",                  // target property path on the mesh
  30,                            // fps
  Animation.ANIMATIONTYPE_FLOAT,
  Animation.ANIMATIONLOOPMODE_CYCLE,
);

slide.setKeys([
  { frame: 0,  value: 0 },
  { frame: 60, value: 10 },
  { frame: 120, value: 0 },
]);

scene.beginAnimation(box, 0, 120, true, 1.0); // target, from, to, loop, speed
```

- `ANIMATIONLOOPMODE_CYCLE` / `RELATIVE` / `CONSTANT` cover the three loop modes.
- `scene.beginDirectAnimation(target, [anim], from, to, loop, speed, onComplete)`
  is the shortcut for one-shot tracks.

## AnimationGroup (Cinematics, Sequences)

```ts
import { AnimationGroup } from "@babylonjs/core/Animations/animationGroup";

const group = new AnimationGroup("intro", scene);
group.addTargetedAnimation(slide, box);
group.addTargetedAnimation(rotate, camera);
group.normalize(0, 120);          // lock timeline bounds
group.play();
```

- Use `AnimationGroup` for cutscenes / multi-target sequences.
- `group.onAnimationGroupEndObservable` for completion callbacks.

## Skeletal / Skin Animation

- Import skin + skeletons via `@babylonjs/loaders/glTF`; the loader attaches
  `skeleton` and `animationRanges` to each skinned mesh.
- Play: `scene.beginAnimation(skeleton, 0, 100, true, 1.0)`.
- Blend skeletons with `skeleton.copyRange` or attach multiple `SkeletonViewer`.

## Morph Targets (Blendshapes)

```ts
import { MorphTargetManager } from "@babylonjs/core/Morph/morphTargetManager";
import { MorphTarget } from "@babylonjs/core/Morph/morphTarget";

const mgr = new MorphTargetManager(scene);
const smile = MorphTarget.CreateFromMesh(mesh, "smile", 0.5);
mgr.addTarget(smile);
mesh.morphTargetManager = mgr;
smile.influence = 0.8; // 0..1
```

## v9 Breaking Changes (verify before writing anim code)

- **Morph Target Manager refactored (#16014)** - the manager API surface
  changed and now supports *disabling position morphing*. Verify any pre-v9
  callers of `MorphTargetManager` after upgrade.
- **Animation relationships now use `uniqueId` (#17757)** - lookups moved from
  names to `uniqueId`. Code that cached animation refs by **name** must switch
  to `uniqueId`.
- **`BabylonFileParser` moved out of the scene (#15619)** - custom scene-loading
  logic that touched `scene.xxxParser` should import the parser from its new
  module location.

## What to Never Do

- Cache animation references by **name**; use `uniqueId` (#17757).
- Run every animation on the render delta if you need deterministic playback -
  drive tracks from a fixed timestep (see `physics.md` for the accumulator pattern).
- Mutate a shared `Animation` instance across targets expecting isolated state;
  one `Animation` is a *track definition* - clone or use `AnimationGroup` for
  per-target playback.
- Construct a fresh `Animatable` per frame for a loop; reuse the one returned by
  `scene.beginAnimation` and call `.pause()` / `.restart()` on it.

## References

- Animation: `doc.babylonjs.com/features/diving_deeper/animation`
- AnimationGroup: `doc.babylonjs.com/typedoc/classes/BABYLON.AnimationGroup`
- Morph Targets: `doc.babylonjs.com/features/diving_deeper/mesh/morphTargets`
