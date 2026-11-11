#!/usr/bin/env python3
"""Compare the local UV rasterizer against nvdiffrast (ground truth).

This harness requires ``torch`` and ``nvdiffrast`` with a CUDA device. It is the
**unlock criterion #2** in ``design/art/art-bible.md`` §8.10.6.11: the patched
export must reproduce the dr result. Run it on the A800:

    # synthetic disjoint grid (fast smoke)
    python3 tools/ovoxel-export/compare_with_nvdiffrast.py \
        --resolution 4096 --grid 256 --json out-grid.json

    # real assets (the ≥3-asset criterion)
    python3 tools/ovoxel-export/compare_with_nvdiffrast.py \
        --resolution 4096 --mesh a.glb --mesh b.glb --mesh c.glb --json out-assets.json

A grid mesh of disjoint quads (each split into two triangles) is used in
synthetic mode so the only expected differences are shared edges, where the
top-left rasterization rule may assign a pixel to either neighbour. In mesh
mode, real xatlas UV layouts are used as-is.

For every case the script reports:

  * covered-pixel face-id mismatch rate
  * max |Δbary| on pixels where both agree on the triangle
  * max |Δposition| after interpolation

Exit codes: 0 = PASS, 1 = FAIL, 2 = NOT-RUN (deps/device missing).
"""

from __future__ import annotations

import argparse
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from uv_raster import interpolate_uv, rasterize_uv  # noqa: E402

PASS_FACE_MISMATCH_RATE = 0.01
PASS_BARY_MAX_ABS = 1e-4
PASS_POS_MAX_ABS = 1e-4


def grid_mesh(n: int, margin: float = 0.02):
    """Disjoint quads in a n×n grid, each split into two triangles."""
    edges = np.linspace(0.0, 1.0, n + 1)
    tris = []
    for j in range(n):
        for i in range(n):
            x0, x1 = edges[i] + margin, edges[i + 1] - margin
            y0, y1 = edges[j] + margin, edges[j + 1] - margin
            if x1 <= x0 or y1 <= y0:
                continue
            tris.append([[x0, y0], [x1, y0], [x0, y1]])
            tris.append([[x1, y0], [x1, y1], [x0, y1]])
    per_corner = np.asarray(tris, dtype=np.float32)
    T = per_corner.shape[0]
    uvs = per_corner.reshape(-1, 2)
    faces = np.arange(T * 3, dtype=np.int64).reshape(T, 3)
    return uvs, faces


def random_mesh(t: int, seed: int):
    per_corner = random_per_corner(t, seed)
    uvs = per_corner.reshape(-1, 2)
    faces = np.arange(t * 3, dtype=np.int64).reshape(t, 3)
    return uvs, faces


def random_per_corner(t: int, seed: int):
    rng = np.random.default_rng(seed)
    base = rng.random((t, 2)).astype(np.float32)
    off = rng.normal(scale=0.02, size=(t, 2, 2)).astype(np.float32)
    uvs = np.concatenate(
        [base[:, None, :], base[:, None, :] + off[:, 0:1, :], base[:, None, :] + off[:, 1:2, :]],
        axis=1,
    )
    return np.clip(uvs, 0.0, 1.0)


def load_mesh_case(path: str):
    import trimesh

    mesh = trimesh.load(path, force="mesh", process=False)
    uv = getattr(mesh.visual, "uv", None)
    if uv is None:
        raise SystemExit(f"[compare] {path}: no UV coordinates")
    uv = np.asarray(uv, dtype=np.float32).reshape(-1, 2)
    verts = np.asarray(mesh.vertices, dtype=np.float32)
    faces = np.asarray(mesh.faces, dtype=np.int64).reshape(-1, 3)
    if uv.shape[0] != verts.shape[0]:
        raise SystemExit(
            f"[compare] {path}: UV count {uv.shape[0]} != vertex count {verts.shape[0]}"
        )
    if faces.size and (faces.min() < 0 or faces.max() >= verts.shape[0]):
        raise SystemExit(f"[compare] {path}: face index out of range")
    return uv, faces, verts


def run_case(name, uvs, faces, verts, R, torch, dr):
    V = uvs.shape[0]
    faces_t = torch.from_numpy(faces.astype(np.int32)).cuda()
    uvs_t = torch.from_numpy(uvs.astype(np.float32)).cuda()

    pos_t = torch.cat(
        [uvs_t * 2 - 1, torch.zeros_like(uvs_t[:, :1]), torch.ones_like(uvs_t[:, :1])],
        dim=-1,
    ).unsqueeze(0)
    ctx = dr.RasterizeCudaContext()
    rast_dr = dr.rasterize(ctx, pos_t, faces_t, resolution=[R, R])[0]

    ours = rasterize_uv(uvs, faces, R)

    id_dr = rast_dr[0, :, :, 3].detach().cpu().numpy().astype(np.int64)
    id_us = ours[0, :, :, 3].astype(np.int64)
    dr_np = rast_dr.detach().cpu().numpy()

    union = (id_dr > 0) | (id_us > 0)
    both = (id_dr > 0) & (id_us > 0)
    same = id_dr == id_us
    mismatch = int(np.sum(union & ~same))
    mismatch_rate = mismatch / max(int(union.sum()), 1)

    agree = both & same
    bary_max = 0.0
    if agree.any():
        bary_max = float(
            max(
                np.max(np.abs(ours[0, :, :, 0][agree] - dr_np[0, :, :, 0][agree])),
                np.max(np.abs(ours[0, :, :, 1][agree] - dr_np[0, :, :, 1][agree])),
            )
        )

    attr_t = torch.from_numpy(verts.astype(np.float32)).cuda().unsqueeze(0)
    pos_dr = dr.interpolate(attr_t, rast_dr, faces_t)[0][0]
    pos_us = interpolate_uv(verts, ours, faces)
    pos_img = pos_dr.detach().cpu().numpy()
    pos_max = 0.0
    if agree.any():
        pos_max = float(np.max(np.abs(pos_us[agree] - pos_img[agree])))

    status = (
        "PASS"
        if mismatch_rate <= PASS_FACE_MISMATCH_RATE
        and bary_max <= PASS_BARY_MAX_ABS
        and pos_max <= PASS_POS_MAX_ABS
        else "FAIL"
    )
    return {
        "name": name,
        "vertices": int(V),
        "triangles": int(faces.shape[0]),
        "covered_dr": int((id_dr > 0).sum()),
        "covered_ours": int((id_us > 0).sum()),
        "face_mismatch_pixels": mismatch,
        "face_mismatch_rate": mismatch_rate,
        "bary_max_abs": bary_max,
        "pos_max_abs": pos_max,
        "status": status,
    }


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--resolution", type=int, default=512)
    ap.add_argument("--grid", type=int, default=16, help="synthetic grid cells per side")
    ap.add_argument("--random", type=int, default=0, help="synthetic random triangle count")
    ap.add_argument("--mesh", action="append", default=[], help="GLB/OBJ path (repeatable)")
    ap.add_argument("--seed", type=int, default=0)
    ap.add_argument("--json", help="write the summary JSON here")
    ap.add_argument("--npz-prefix", help="write raw buffers to <prefix>.<case>.npz")
    args = ap.parse_args(argv)

    try:
        import torch
        import nvdiffrast.torch as dr
    except Exception as exc:  # pragma: no cover - environment gate
        print(f"[compare] NOT-RUN: torch/nvdiffrast unavailable ({type(exc).__name__}: {exc})")
        return 2
    if not torch.cuda.is_available():
        print("[compare] NOT-RUN: CUDA is not available")
        return 2

    R = int(args.resolution)
    rng = np.random.default_rng(args.seed + 1)
    cases = []
    if args.mesh:
        for path in args.mesh:
            uvs, faces, verts = load_mesh_case(path)
            cases.append((os.path.basename(path), uvs, faces, verts))
    else:
        if args.random > 0:
            uvs, faces = random_mesh(args.random, args.seed)
            name = f"random-{args.random}"
        else:
            uvs, faces = grid_mesh(int(args.grid))
            name = f"grid-{args.grid}"
        verts = rng.standard_normal((uvs.shape[0], 3)).astype(np.float32)
        cases.append((name, uvs, faces, verts))

    results = []
    for name, uvs, faces, verts in cases:
        print(f"[compare] case={name} R={R} V={len(uvs)} T={len(faces)}")
        res = run_case(name, uvs, faces, verts, R, torch, dr)
        res["resolution"] = R
        results.append(res)
        print(
            f"[compare]   status={res['status']} mismatch={res['face_mismatch_rate']:.4%} "
            f"bary={res['bary_max_abs']:.2e} pos={res['pos_max_abs']:.2e}"
        )
        if args.npz_prefix:
            # Recompute for the raw dump only (kept simple; no effect on verdict).
            rast_ours = rasterize_uv(uvs, faces, R)
            np.savez_compressed(
                f"{args.npz_prefix}.{name}.npz",
                rast_ours=rast_ours,
                uv=uvs,
                faces=faces,
            )

    overall = "PASS" if all(r["status"] == "PASS" for r in results) else "FAIL"
    summary = {
        "resolution": R,
        "gpu": torch.cuda.get_device_name(0),
        "thresholds": {
            "face_mismatch_rate": PASS_FACE_MISMATCH_RATE,
            "bary_max_abs": PASS_BARY_MAX_ABS,
            "pos_max_abs": PASS_POS_MAX_ABS,
        },
        "cases": results,
        "status": overall,
    }
    print("[compare] " + json.dumps(summary, ensure_ascii=False, indent=2))

    if args.json:
        with open(args.json, "w", encoding="utf-8") as f:
            json.dump(summary, f, ensure_ascii=False, indent=2)
        print(f"[compare] wrote {args.json}")

    return 0 if overall == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())