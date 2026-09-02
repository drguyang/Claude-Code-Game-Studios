# Babylon.js Networking - Quick Reference

Last verified: 2026-08-30 | Engine: Babylon.js 9.23.0

**Babylon.js does not ship networking.** For multiplayer, pair Babylon with an
external transport. The recommended default for this project is **Colyseus**
(server-authoritative, room-based state sync). The non-negotiable rule: keep
game state as serializable data (Zustand / Colyseus state) and **never** store
`Mesh` / `Scene` references in the store - that survives undo/replay and lets
the server-authoritative state drive the client scene graph.

## Recommended Stacks

| Stack | Transport | When |
|-------|-----------|------|
| **Colyseus** | WebSocket | Default; server-authoritative rooms, schema diff sync |
| **Socket.IO** | WebSocket | Quick prototypes / chat / lobby; weaker authority story |
| **Nanostores/Colyseus + WebRTC** | DataChannel | PvP low-latency, peer-relayed |
| Plain `WebSocket` | custom binary | When you already have a server and a custom protocol |

## State Separation Pattern (mandatory)

```ts
// store holds PLAIN DATA only
type PlayerState = { id: string; pos: [number, number, number]; hp: number };
const usePlayers = create<PlayerState[]>((set) => ({}));
```

The Babylon scene graph reads the store and reconciles:

```ts
usePlayers.subscribe((players) => {
  for (const p of players) {
    const mesh = meshes.get(p.id) ?? spawnMesh(p.id);
    mesh.position.set(...p.pos); // apply server-authoritative state
  }
});
```

This pattern is the foundation for client prediction: render predicted local
state, snap to authoritative state on reconcile.

## Client Prediction + Reconciliation

```ts
// local: apply input immediately (predicted)
playerMesh.position.addInPlace(inputVec.scale(dt * SPEED));

// send input to server with a sequence number
send({ type: "move", seq, input: inputVec });

// server replies with authoritative state
onSnapshot((state) => {
  if (state.seq > lastAckedSeq) {
    playerMesh.position.set(...state.pos);   // snap on mis-prediction
    lastAckedSeq = state.seq;
  }
});
```

- For `TransformNode` interpolation between snapshots, use linear or
  Hermite interpolation - do not snap authoritative state every tick (jitter).
- Run server ticks at a fixed rate (e.g. 20 Hz), interpolate between received
  snapshots on the render frame.

## Driving Babylon from Server State

- Server sends `Map<entityId, Transform>` deltas.
- Client maintains `meshes: Map<string, TransformNode>` and reconciles.
- Never `scene.removeMesh` inside the snapshot handler - defer disposal to the
  end of frame, or pool meshes to avoid GC churn.

## Colyseus Wiring (recommended)

```ts
import { Client } from "colyseus.js";
const client = new Client("wss://game.example.com");
const room = await client.joinOrCreate("main", { name });

room.state.players.onAdd = (player, id) => {
  usePlayers.getState().addPlayer(id, player);
};
room.onMessage("snapshot", (snap) => usePlayers.getState().applySnapshot(snap));
room.state.players.onChange = (player, id) =>
  usePlayers.getState().updatePlayer(id, player);
```

## WebXR / Co-located Multiplayer

- For co-located XR, use `@babylonjs/core/XR/webXRFeaturesBuilder`'s
  `WebXRHitFeature` / anchor sharing via your transport - Babylon has no
  built-in networking for XR either.

## v9 Notes

- No networking-specific v9 breaking changes; the v9 rules still apply (deep
  imports, never barrel `import * as BABYLON`).
- When using WebTransport / WebRTC, run on a `Worker` to keep the render loop
  free of GC pauses from message deserialization.

## What to Never Do

- Put `Mesh` / `Scene` / `Sound` references in the Zustand store - state must
  be JSON-serializable for replay / undo / networking sync.
- Send transform data every frame; send input + occasional authoritative
  snapshots (Colyseus schema diff handles the rest).
- Snap authoritative state to the mesh every snapshot; interpolate between
  received transforms to keep motion smooth.
- Trust client state for gameplay-affecting values (hp, ammo, position) -
  server is authoritative; the client only predicts for responsiveness.

## References

- Colyseus: `docs.colyseus.io`
- Babylon multiplayer overview: `doc.babylonjs.com/community/extensions`
- See `current-best-practices.md` "State Separation" for the data/scene rule.
