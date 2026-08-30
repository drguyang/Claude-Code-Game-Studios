# Babylon.js Physics - Quick Reference

Last verified: 2026-08-30 | Engine: Babylon.js 9.23.0

Physics in v9 uses the **v2 plugin architecture** with **Havok** as the primary
backend. Ammo.js is legacy - do not suggest it for new code.

## Plugin Class

The class name is `HavokPlugin` - **not** the older `HavokPhysicsPlugin`.

```ts
import { HavokPlugin } from "@babylonjs/core/Physics/v2/Plugins/havokPlugin";
import Havok from "@babylonjs/havok";

const hk = await Havok();               // must be awaited - it's a WASM module
const plugin = new HavokPlugin(true, hk); // 2nd arg = the Havok instance
const scene = new Scene(engine);
scene.enablePhysics(Vector3.Zero(), plugin);
```

## Enabling Physics

```ts
scene.enablePhysics(new Vector3(0, -9.81, 0), plugin);
```

## PhysicsAggregate (v2)

Body types are set via `PhysicsBody`/`PhysicsShape` and the
`PhysicsAggregate` helper.

```ts
const box = MeshBuilder.CreateBox("box", { size: 1 }, scene);
const agg = new PhysicsAggregate(box, PhysicsShapeType.BOX, {
  mass: 1,
  restitution: 0.2,
}, scene);
```

- `PhysicsAggregate` now **mutates the caller's options object** - pass a copy if
  you re-use the options across entities (#18555).
- Havok floating-origin world regions can be disabled (#18750) for games that need
  a fixed origin.

## Fixed Timestep (decouple from render)

Physics must run on a fixed step, not the render delta:

```ts
const STEP = 1 / 60;
engine.runRenderLoop(() => {
  scene._advancePhysicsEngineStep(STEP);
  scene.render();
});
```

Or use an accumulator for non-fixed hardware:
```ts
let acc = 0;
engine.runRenderLoop(() => {
  acc += engine.getDeltaTime() / 1000;
  while (acc >= STEP) { scene._advancePhysicsEngineStep(STEP); acc -= STEP; }
  scene.render();
});
```

## What to Never Do

- Use `PhysicsImpostor`/legacy `HavokPhysicsPlugin` from old tutorials.
- Run physics from the render delta (tunneling/jitter).
- Construct a `HavokPlugin` without passing the awaited `Havok()` instance.
- Create a new mesh per physics body in a hot loop - use thin instances + a single
  physics body per batch, or pool bodies.

## References

- Havok: `doc.babylonjs.com/divingDeeper/physics/usingHavok`
- Physics v2: `doc.babylonjs.com/divingDeeper/physics`
