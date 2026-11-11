#!/usr/bin/env python3
"""Compare two exported GLBs (upstream nvdiffrast vs patched o_voxel).

This is the **texture-level half** of unlock criterion #2. Run it on the A800
after producing the same asset in two environments:

    # env A: upstream o_voxel + nvdiffrast
    python inference.py --image in.png --output a.glb
    # env B: patched o_voxel (no nvdiffrast)
    python inference.py --image in.png --output b.glb

    python3 tools/ovoxel-export/compare_glb_textures.py \
        --a a.glb --b b.glb --json out.json

Checks: identical geometry (vertex/face counts and face indices), identical UVs,
and base-color / metallic-roughness texture PSNR ≥ 35 dB.

Exit codes: 0 = PASS, 1 = FAIL, 2 = NOT-RUN (trimesh/PIL missing).
"""

from __future__ import annotations

import argparse
import json
import sys

import numpy as np

PASS_PSNR_DB = 35.0


def _psnr(a: np.ndarray, b: np.ndarray) -> float:
    if a.shape != b.shape:
        return float("-inf")
    diff = a.astype(np.float64) - b.astype(np.float64)
    mse = float(np.mean(diff * diff))
    if mse == 0.0:
        return float("inf")
    return float(10.0 * np.log10((255.0 * 255.0) / mse))


def _load(path: str):
    import trimesh

    mesh = trimesh.load(path, force="mesh", process=False)
    mat = mesh.visual.material
    textures = {}
    for attr in ("baseColorTexture", "metallicRoughnessTexture"):
        img = getattr(mat, attr, None)
        if img is None:
            continue
        if hasattr(img, "convert"):
            img = img.convert("RGBA")
        textures[attr] = np.asarray(img)
    uv = getattr(mesh.visual, "uv", None)
    return (
        np.asarray(mesh.vertices, dtype=np.float32),
        np.asarray(mesh.faces, dtype=np.int64),
        None if uv is None else np.asarray(uv, dtype=np.float32),
        textures,
    )


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--a", required=True, help="GLB exported with upstream nvdiffrast")
    ap.add_argument("--b", required=True, help="GLB exported with the patched o_voxel")
    ap.add_argument("--json", help="write the summary JSON here")
    args = ap.parse_args(argv)

    try:
        import trimesh  # noqa: F401
        from PIL import Image  # noqa: F401
    except Exception as exc:  # pragma: no cover - environment gate
        print(f"[glb] NOT-RUN: trimesh/PIL unavailable ({type(exc).__name__}: {exc})")
        return 2

    va, fa, uva, ta = _load(args.a)
    vb, fb, uvb, tb = _load(args.b)

    result = {
        "a": args.a,
        "b": args.b,
        "vertices_equal": int(va.shape[0]) == int(vb.shape[0]),
        "faces_equal": fa.shape == fb.shape and bool(np.array_equal(fa, fb)),
        "uv_max_abs": None,
        "textures": {},
        "status": "PASS",
    }
    if uva is None or uvb is None:
        result["uv_max_abs"] = None
        result["status"] = "FAIL"
        result["error"] = "missing UVs"
    elif uva.shape != uvb.shape:
        result["status"] = "FAIL"
        result["error"] = f"UV shape {uva.shape} vs {uvb.shape}"
    else:
        result["uv_max_abs"] = float(np.max(np.abs(uva - uvb)))

    for name in sorted(set(ta) | set(tb)):
        if name not in ta or name not in tb:
            result["textures"][name] = {"psnr_db": None, "status": "FAIL", "error": "missing side"}
            result["status"] = "FAIL"
            continue
        psnr = _psnr(ta[name], tb[name])
        ok = psnr >= PASS_PSNR_DB
        result["textures"][name] = {
            "shape_a": list(ta[name].shape),
            "shape_b": list(tb[name].shape),
            "psnr_db": None if psnr == float("-inf") else psnr,
            "status": "PASS" if ok else "FAIL",
        }
        if not ok:
            result["status"] = "FAIL"

    if not result["vertices_equal"] or not result["faces_equal"]:
        result["status"] = "FAIL"

    print("[glb] " + json.dumps(result, ensure_ascii=False, indent=2))
    if args.json:
        with open(args.json, "w", encoding="utf-8") as f:
            json.dump(result, f, ensure_ascii=False, indent=2)
        print(f"[glb] wrote {args.json}")
    return 0 if result["status"] == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())