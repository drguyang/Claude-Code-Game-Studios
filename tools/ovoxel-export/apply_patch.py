#!/usr/bin/env python3
"""Apply / revert the local nvdiffrast-free patch on an installed ``o_voxel``.

The patch replaces only ``o_voxel/postprocess.py`` (the single file that
imports nvdiffrast) and adds ``o_voxel/uv_raster.py``. Everything else in the
package is untouched.

Usage
-----
    python3 tools/ovoxel-export/apply_patch.py --check
    python3 tools/ovoxel-export/apply_patch.py --apply
    python3 tools/ovoxel-export/apply_patch.py --revert

    # development / tests against a fake package dir:
    python3 tools/ovoxel-export/apply_patch.py --target /tmp/fake_o_voxel --apply --force

The installer refuses to patch an unknown upstream version: it compares the
installed ``postprocess.py`` SHA-256 against the version this patch was written
for. If upstream changes, re-audit and update the constants instead of forcing.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import pathlib
import shutil
import sys

HERE = pathlib.Path(__file__).resolve().parent
PATCHED = HERE / "postprocess_patched.py"
UV_RASTER = HERE / "uv_raster.py"

UPSTREAM_SHA256 = "ef51a1ba0f2748ffb4c265b47d382cee956f23c6a52d0f3587e6d8beccb7e54a"
MARKER = "PATCHED COPY — o_voxel/postprocess.py"


def sha256(path: pathlib.Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def find_package(override: str | None) -> pathlib.Path:
    if override:
        p = pathlib.Path(override).resolve()
        if not p.is_dir():
            raise SystemExit(f"[patch] not a directory: {p}")
        return p
    spec = importlib.util.find_spec("o_voxel")
    if spec is None or not spec.submodule_search_locations:
        raise SystemExit(
            "[patch] o_voxel is not installed in this interpreter. "
            "Install the upstream wheel first, or pass --target."
        )
    return pathlib.Path(list(spec.submodule_search_locations)[0])


def is_patched(text: str) -> bool:
    return MARKER in text


def check(target: pathlib.Path) -> int:
    post = target / "postprocess.py"
    uv = target / "uv_raster.py"
    if not post.exists():
        print(f"[patch] MISSING {post}")
        return 2
    text = post.read_text(encoding="utf-8", errors="ignore")
    patched = is_patched(text)
    digest = sha256(post)
    # Only executable lines count: the provenance header in the patched file
    # names the removed calls on purpose.
    code = "\n".join(
        line for line in text.splitlines() if not line.lstrip().startswith("#")
    )
    imports = code.count("import nvdiffrast")
    calls = (
        code.count("RasterizeCudaContext")
        + code.count("dr.rasterize")
        + code.count("dr.interpolate")
    )
    print(f"[patch] package : {target}")
    print(f"[patch] postprocess sha256: {digest}")
    print(f"[patch] state   : {'PATCHED' if patched else 'UPSTREAM/UNKNOWN'}")
    print(f"[patch] uv_raster.py      : {'present' if uv.exists() else 'missing'}")
    print(f"[patch] nvdiffrast import : {imports} (expected 0 when patched)")
    print(f"[patch] nvdiffrast calls  : {calls} (expected 0 when patched)")
    if patched and imports == 0 and calls == 0 and uv.exists():
        print("[patch] CHECK: OK")
        return 0
    if not patched and digest == UPSTREAM_SHA256:
        print("[patch] CHECK: upstream, patch not applied")
        return 1
    print("[patch] CHECK: unknown state")
    return 2


def apply_patch(target: pathlib.Path, force: bool) -> int:
    post = target / "postprocess.py"
    if not PATCHED.exists() or not UV_RASTER.exists():
        raise SystemExit("[patch] vendored patch files are missing")
    if not post.exists():
        raise SystemExit(f"[patch] {post} not found")

    text = post.read_text(encoding="utf-8", errors="ignore")
    already = is_patched(text)
    digest = sha256(post)
    if not already and digest != UPSTREAM_SHA256 and not force:
        raise SystemExit(
            "[patch] installed postprocess.py does not match the audited upstream version.\n"
            f"         expected sha256 {UPSTREAM_SHA256}\n"
            f"         actual   sha256 {digest}\n"
            "         Re-audit before patching (or --force for a throwaway target)."
        )

    backup = target / "postprocess.py.orig"
    if not already:
        if not backup.exists():
            shutil.copy2(post, backup)
            print(f"[patch] backup written: {backup}")
        else:
            print(f"[patch] backup kept   : {backup}")

    shutil.copy2(PATCHED, post)
    shutil.copy2(UV_RASTER, target / "uv_raster.py")
    print(f"[patch] patched {post}")
    print(f"[patch] added   {target / 'uv_raster.py'}")
    return check(target)


def revert(target: pathlib.Path) -> int:
    post = target / "postprocess.py"
    backup = target / "postprocess.py.orig"
    if not backup.exists():
        raise SystemExit(f"[patch] no backup at {backup}; cannot revert")
    shutil.copy2(backup, post)
    backup.unlink()
    uv = target / "uv_raster.py"
    if uv.exists():
        uv.unlink()
    print(f"[patch] reverted {post}")
    digest = sha256(post)
    expected = UPSTREAM_SHA256
    ok = digest == expected
    print(f"[patch] postprocess sha256: {digest} ({'matches upstream' if ok else 'MISMATCH'})")
    return 0 if ok else 2


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    g = ap.add_mutually_exclusive_group()
    g.add_argument("--check", action="store_true", help="report patch state (default)")
    g.add_argument("--apply", action="store_true", help="copy the patch into the package")
    g.add_argument("--revert", action="store_true", help="restore postprocess.py.orig")
    ap.add_argument("--target", help="package directory (default: installed o_voxel)")
    ap.add_argument("--force", action="store_true", help="skip the upstream hash check")
    args = ap.parse_args(argv)

    target = find_package(args.target)
    if args.apply:
        return apply_patch(target, args.force)
    if args.revert:
        return revert(target)
    return check(target)


if __name__ == "__main__":
    sys.exit(main())