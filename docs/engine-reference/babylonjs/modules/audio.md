# Babylon.js Audio - Quick Reference

Last verified: 2026-08-30 | Engine: Babylon.js 9.23.0

Babylon.js ships a thin **Web Audio API** wrapper (`@babylonjs/core/Audio`).
For complex mixing / ducking / music sequencing, prefer **Howler.js** or a
custom Web Audio graph - Babylon's audio layer is intentionally minimal and is
mainly used for 3D spatial sound tied to scene objects.

## Engine & Scene Setup

```ts
import { Engine } from "@babylonjs/core/Engines/engine";
import { Scene } from "@babylonjs/core/scene";
import { AudioEngine } from "@babylonjs/core/Audio/audioEngine";

const engine = new Engine(canvas, true, { stencil: true });
const scene = new Scene(engine);
// scene.audioEngine is auto-created; do NOT construct AudioEngine manually.
```

> The browser autoplay policy requires a user gesture before audio can play.
> Resume the context on first interaction:

```ts
canvas.addEventListener("pointerdown", () => scene.audioEngine.unlock(), { once: true });
```

## Sound & StaticSound

```ts
import { Sound } from "@babylonjs/core/Audio/sound";

const sfx = new Sound("hit", "/audio/hit.wav", scene, null, {
  volume: 0.6,
  autoplay: false,
  loop: false,
  spatialSound: true,
  distanceModel: "exponential",
  maxDistance: 50,
  rolloffFactor: 0.8,
});
sfx.play();
```

- **Streaming music** uses `new Sound(name, url, scene, callback, { streaming: true })`.
- Spatial sound attaches to a `TransformNode` via `sound.attachToMesh(mesh)` -
  the engine reads `mesh.getWorldMatrix()` each frame to update the source position.

## 3D Spatial Sound

```ts
sfx.spatialSound = true;
sfx.distanceModel = "exponential"; // "linear" | "inverse" | "exponential"
sfx.maxDistance = 50;
sfx.rolloffFactor = 0.8;
sfx.attachToMesh(box);
```

The listener is the **active camera** by default; the audio engine reads
`scene.activeCamera.position` / `.getForwardRay()` each frame.

## Volume & Ducking

Babylon has no built-in mixer / snapshots / duck-volume. For ducking and
crossfades, drive `sound.setVolume(value, time)` from your React HUD state
or use Howler's `Howl.fade`:

```ts
scene.mainSoundTrack.setVolume(0.5);     // master
sfx.setVolume(0.8);
```

For music ducking during dialogue, animate `setVolume` from a timeline - there
is no native snapshot primitive.

## v9 Notes (verify against your build)

- The audio module is **stable across v9** - no high-risk breaking changes in
  the 9.0..9.23 timeline touch `Sound` / `AudioEngine`.
- `AudioEngine` fingerprinting surfaced in late v9.x; verify spatial falloff on
  real devices after upgrade.
- `import * as BABYLON from "babylonjs"` is still forbidden - use deep imports
  from `@babylonjs/core/Audio/...`.

## What to Never Do

- Construct `new AudioEngine()` directly - it is created by the `Scene`. Use
  `scene.audioEngine`.
- Call `sound.play()` before the user has gestured (autoplay policy) - resume
  via `scene.audioEngine.unlock()` on a `pointerdown`.
- Store `Sound` references inside a Zustand-style store - keep audio handles in
  a service module keyed by string id; never put `Sound` instances in serialized
  state (see `current-best-practices.md` "State Separation").
- Pre-create one `Sound` per SFX event in a hot loop - pool N `Sound` instances
  per clip, or use Howler which pools internally.

## References

- Audio: `doc.babylonjs.com/features/diving_deeper/audio`
- Sound: `doc.babylonjs.com/typedoc/classes/BABYLON.Sound`
