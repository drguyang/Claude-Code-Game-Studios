# Babylon.js Input - Quick Reference

Last verified: 2026-08-30 | Engine: Babylon.js 9.23.0

Babylon.js input is **observable-driven**: pointers, keyboard, and gamepad
expose `*Observable` streams on `scene`. Lower-level device state is provided
by the **DeviceInputSystem** (`@babylonjs/core/Inputs/`). Never use the legacy
`ActionManager` scene-wide array - it is deprecated (#12620).

## Core Observables

```ts
import { PointerEventTypes } from "@babylonjs/core/Events/pointerEvents";
import { KeyboardEventTypes } from "@babylonjs/core/Events/keyboardEvents";

scene.onPointerObservable.add((pi) => {
  if (pi.type === PointerEventTypes.POINTERDOWN) { /* pick */ }
  if (pi.type === PointerEventTypes.POINTERWHEEL) { /* zoom */ }
});

scene.onKeyboardObservable.add((ki) => {
  if (ki.type === KeyboardEventTypes.KEYDOWN && ki.event.key === " ") {
    /* jump */
  }
});
```

- Pointer info carries `pickInfo` already resolved - no need to call
  `scene.pick()` again.
- Camera input is wired by `camera.attachControl(canvas, true)`; do NOT
  duplicate pointer handling if a camera is attached.

## DeviceInputSystem (Polled State)

```ts
import { DeviceSourceManager } from "@babylonjs/core/DeviceInput/internal/deviceSourceManager";
import { DeviceType } from "@babylonjs/core/DeviceInput/InputDevices/deviceTypes";

const dsm = new DeviceSourceManager(scene);
const kb = dsm.getDeviceSource(DeviceType.Keyboard);
const gamepad = dsm.getDeviceSource(DeviceType.Generic);
// poll each frame
const space = kb?.getInput(32); // 32 = Space keycode
```

- Use this when you need continuous-axis state (analog sticks, held keys) rather
  than discrete events.
- `DeviceSourceManager.onDeviceConnectedObservable` for hot-plug events.

## Gamepad

```ts
import { GenericPad } from "@babylonjs/core/gamepad/genericPad";
import { GamepadManager } from "@babylonjs/core/gamepad/gamepadManager";

const mgr = new GamepadManager(scene);
mgr.onGamepadConnectedObservable.add((gp) => {
  if (gp instanceof GenericPad) {
    gp.onleftstickchanged((v) => { /* x/y -1..1 */ });
    gp.onbuttondown((b) => { /* b: number */ });
  }
});
```

Standard gamepads (Xbox/PS) subclass `Gamepad`; use the specific class for
named buttons (`buttonA` etc.). For cross-vendor mapping, prefer the
`GenericPad` numeric button API.

## Touch

- Pointer observables cover single/multi-touch on mobile browsers - `pi.event`
  is a `PointerEvent`; read `pointerId` and `pressure`.
- For gesture recognition (pinch/rotate), use `@babylonjs/core/Events/pointerEvents`
  + your own state machine, or a third-party lib like Hammer.js.

## Picking

```ts
const pick = scene.pick(scene.pointerX, scene.pointerY);
if (pick.hit && pick.pickedMesh?.name === "door") { /* open */ }
```

For ray casts not tied to the pointer, use `scene.createPickingRay` /
`RayHelper`.

## v9 Breaking Changes (verify before writing input code)

- **MouseWheel `pollInput` removed from EventFactory (#12397)** - the old
  `pollInput` wheel path is gone. Use `PointerEventTypes.POINTERWHEEL` on
  `scene.onPointerObservable` instead.
- **Scene-wide `ActionManager` array deprecated (#12620)** - register
  `ActionManager` *per-node*, or move logic to observables.

## WebXR Input

```ts
import { WebXRInputSource } from "@babylonjs/core/XR/webXRInputSource";
const xr = await scene.createDefaultXRExperienceAsync({});
xr.input.onControllerAddedObservable.add((c) => {
  c.onMotionControllerInitObservable.add((mc) => { /* mc.handness */ });
});
```

## What to Never Do

- Use `scene.actionManager` (the legacy scene-wide array) - register per-node
  or use observables (#12620).
- Read mouse wheel via the removed `pollInput` path (#12397) - use
  `PointerEventTypes.POINTERWHEEL`.
- Hardcode key codes in gameplay logic; expose bindings through a data store so
  rebind works (matches the React HUD + Zustand pattern).
- Subscribe to observables without keeping the `Observer` handle - call
  `observer.remove()` on teardown or you leak handlers across scene reloads.

## References

- Input: `doc.babylonjs.com/features/diving_deeper/input`
- DeviceInputSystem: `doc.babylonjs.com/typedoc/classes/BABYLON.DeviceSourceManager`
- Gamepads: `doc.babylonjs.com/features/diving_deeper/input/gamepads`
