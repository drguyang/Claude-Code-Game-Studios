# Babylon.js Navigation - Quick Reference

Last verified: 2026-08-30 | Engine: Babylon.js 9.23.0

**Babylon.js does not ship a navigation system.** There is no built-in NavMesh,
pathfinding, or crowd simulation. Choose a third-party library and wire it into
the scene graph as data - keep navigation state in plain data (Zustand), never
store `Mesh`/`Scene` references in the navigation store.

## Recommended Libraries

| Library | Backend | Notes |
|---------|---------|-------|
| **`recast-dectree` / `@recast-navigation/core`** | Recast + Detour (WASM) | Drop-in NavMesh + path queries; pairs cleanly with Babylon meshes |
| **`three-pathfinding`** | port of Detour | Mesh-agnostic; feed Babylon vertex/index arrays |
| **`pathfinding-js`** (obsolete) | grid A* | Simple 2D grids only; do not pick for production 3D |
| Custom A* on a tilemap | JS | Viable for top-down 2D / grid games |

## Building a NavMesh from a Babylon Mesh

```ts
import { Mesh } from "@babylonjs/core/Meshes/mesh";
// example: @recast-navigation/core
import { NavMesh } from "@recast-navigation/core";

const positions = navgroundMesh.getVerticesData(VertexBuffer.PositionKind);
const indices   = navgroundMesh.getIndices();
const nav = new NavMesh();
nav.build(positions, indices, { cs: 0.2, ch: 0.2, walkableRadius: 0.5 });
const path = nav.computePath(start, end); // returns Vector3[] in world space
```

- Build the NavMesh once at scene load, not per frame.
- Rebuild on level changes; pool NavMesh instances per dungeon/level.

## Driving Agents Along a Path

Babylon has no `NavMeshAgent`. Drive `TransformNode.position` manually:

```ts
let i = 0;
const STEP = 4; // m/s
scene.onBeforeRenderObservable.add(() => {
  if (!path || i >= path.length - 1) return;
  const next = path[i + 1];
  const dir = next.subtract(agent.position);
  const dist = dir.length();
  if (dist < 0.05) { i++; return; }
  agent.position.addInPlace(dir.normalize().scale(STEP * engine.getDeltaTime() / 1000));
  agent.lookAt(next);
});
```

## Crowd Simulation

For local-avoidance crowds, use **RVO2** (`@roboflow/rvo2-wasm` or vanilla
`rvo2-js`). Feed the planned path as a "preferred velocity" per agent; the
simulator returns adjusted velocities you apply to `TransformNode`:

```ts
const sim = new RVOSimulator();
sim.processObstacles(navgroundMesh);
const agentId = sim.addAgent(position, { neighborDist: 5, maxSpeed: 4 });
sim.setAgentPrefVelocity(agentId, desiredDirection.scale(4));
// each frame:
sim.doStep(STEP);
agent.position.copyFrom(sim.getAgentPosition(agentId));
```

## v9 Notes

- No v9-specific changes to navigation (it's not in Babylon). The general v9
  rules still apply: deep imports, never `import * as BABYLON`, fixed-timestep
  sim if you also use Havok physics on the same agents.
- When NavMesh generation runs in WASM, run it off the render thread
  (`requestIdleCallback` or a Worker) to avoid frame hitches.

## What to Never Do

- Assume a Babylon `Mesh` is "walkable" - mark a dedicated ground mesh and feed
  *only that mesh's* vertex/index data to the NavMesh builder.
- Call `computePath` every frame for a stationary agent - cache the path and
  invalidate on destination change.
- Drive agent movement from the **render delta** when Havok physics is also
  acting on the agent - run navigation on the same fixed step as physics (see
  `physics.md`).
- Store `Mesh` or `Scene` references inside the navigation data store - that
  breaks serialization / undo / future networking sync (see
  `current-best-practices.md` "State Separation").

## References

- `@recast-navigation/core`: `github.com/isaac-mason/recast-navigation`
- RVO2 (Recast/Detour crowd): `github.com/snape/RVO2`
- Discussion of Babylon + Recast: `doc.babylonjs.com/community/extensions` (search
  "navigation")
