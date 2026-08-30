# Babylon.js GUI & HUD - Quick Reference

Last verified: 2026-08-30 | Engine: Babylon.js 9.23.0

## Two UI Layers - Recommended Split

| Layer | Use for | Tech |
|-------|---------|------|
| React/DOM HUD overlay | Menus, HUD, state panels, dialogs, complex layouts | React over the canvas |
| Babylon GUI | World-space labels, crosshairs, on-mesh tags | `AdvancedDynamicTexture` |

**Rule:** keep complex UI in React; keep Babylon GUI limited to things that must
track the world (names, health bars over entities, crosshair).

## React HUD Overlay

```css
/* app.css */
#root { position: absolute; inset: 0; background: transparent; pointer-events: none; }
#root > .hud { pointer-events: auto; }
```

```ts
// engine creation
const engine = new Engine(canvas, true, { alpha: false }); // transparent composite
```

- React reads state from data (Zustand). Never store `Mesh`/`Scene` in the store - 
  that breaks undo/replay and future networking sync.
- Enable `pointer-events: auto` only on interactive HUD nodes so the canvas still
  receives pointer input.

## Babylon GUI (World-Space)

```ts
import { AdvancedDynamicTexture } from "@babylonjs/gui/2D/advancedDynamicTexture";
const gui = AdvancedDynamicTexture.CreateFullscreenUI("hud", true, scene);
const label = new TextBlock("lbl", "DOOR");
label.color = "white"; label.fontSize = 24;
gui.addControl(label);
```

## Input

- Pointer/camera input via `scene.onPointerObservable`, `camera.inputs`, and
  `engine.runRenderLoop`.
- Do NOT use the removed MouseWheel `pollInput` path (#12397); use pointer wheel
  observables.
- Gamepad: use `scene.onKeyboardObservable` / `NPad` inputs, or the
  `@babylonjs/core/Inputs` classes.

## References

- GUI: `doc.babylonjs.com/features/gui`
- React HUD pattern: see `current-best-practices.md` (React HUD Overlay).
