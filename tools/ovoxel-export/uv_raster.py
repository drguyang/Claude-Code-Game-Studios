"""UV-space triangle rasterizer — drop-in replacement for the nvdiffrast calls in
``o_voxel.postprocess.to_glb``.

Why this exists
---------------
TRELLIS.2's ``o_voxel`` package (MIT) imports ``nvdiffrast.torch`` **at module
level** and uses it in the only GLB export path. nvdiffrast is licensed under
NVIDIA's 1-Way Commercial License (§3.3: non-commercial only), so the export
cannot run in a commercial pipeline. This module replaces the three calls
(``RasterizeCudaContext`` / ``rasterize`` / ``interpolate``) with plain NumPy.

Conventions (match nvdiffrast 0.4 as used by TRELLIS.2)
------------------------------------------------------
* Input ``uvs``: either per-vertex ``(V, 2)`` (same layout nvdiffrast takes as
  ``pos``, as returned by ``cumesh.uv_unwrap``) or per-triangle-corner
  ``(T, 3, 2)``. UV nominally in [0, 1]; per-vertex UVs are gathered by ``faces``.
* Input ``faces``: ``(T, 3)`` integer, local indices into the vertex array.
* Output ``rast``: ``(1, R, R, 4)`` float32, where

  - ``rast[..., 0]`` = barycentric u for vertex 0
  - ``rast[..., 1]`` = barycentric v for vertex 1
  - ``rast[..., 2]`` = 0 (depth; unused by the export)
  - ``rast[..., 3]`` = triangle index + 1 (0 = background)

* Pixel centers are at ``(x + 0.5, y + 0.5)``; ``uv=(0,0)`` maps to pixel-space
  ``(0, 0)``, i.e. ``pixel = uv * R``.
* On overlap the **highest triangle index wins**. This matches the net effect of
  the original code's ``torch.where(mask_chunk, rast_chunk, rast)`` merge across
  chunks and is deterministic regardless of processing order.
* Degenerate (zero-area) triangles are skipped.

The rasterizer is CPU/NumPy only. For an authoring-time export that is fine; a
4096² bake is a one-off offline step, not a per-frame cost. The torch adapters
transfer tensors to CPU, run, and transfer the result back.
"""

from __future__ import annotations

import numpy as np

__all__ = [
    "rasterize_uv",
    "interpolate_uv",
    "rasterize_uv_torch",
    "interpolate_uv_torch",
]

# Maximum number of (triangle, sample) pairs evaluated in one vectorized batch.
# Lower it if memory is tight; higher is faster but uses more peak RAM.
_CHUNK_SAMPLES = 1_000_000

_EPS = 1e-12


def _as_inputs(uvs, faces, resolution):
    faces = np.asarray(faces)
    if faces.ndim != 2 or faces.shape[1] != 3:
        raise ValueError(f"faces must be (T,3), got {faces.shape}")
    if not np.issubdtype(faces.dtype, np.integer):
        faces = faces.astype(np.int64)

    uvs = np.asarray(uvs, dtype=np.float64)
    if uvs.ndim == 2:
        # Per-vertex UVs (cumesh.uv_unwrap / nvdiffrast pos layout) -> per-corner.
        if uvs.shape[1] != 2:
            raise ValueError(f"vertex uvs must be (V,2), got {uvs.shape}")
        if faces.size:
            lo, hi = int(faces.min()), int(faces.max())
            if lo < 0 or hi >= uvs.shape[0]:
                raise ValueError(
                    f"faces index out of range: [{lo}, {hi}] vs {uvs.shape[0]} vertices"
                )
        uvs = uvs[faces]
    elif uvs.ndim != 3 or uvs.shape[2] != 2:
        raise ValueError(f"uvs must be (V,2) or (T,3,2), got {uvs.shape}")

    if uvs.shape[0] != faces.shape[0]:
        raise ValueError(
            f"uvs/faces triangle count mismatch: {uvs.shape[0]} vs {faces.shape[0]}"
        )
    resolution = int(resolution)
    if resolution <= 0:
        raise ValueError(f"resolution must be positive, got {resolution}")
    return uvs, faces, resolution


def rasterize_uv(uvs, faces, resolution, face_offset=0, chunk_samples=_CHUNK_SAMPLES):
    """Rasterize UV-space triangles into a nvdiffrast-shaped buffer.

    Parameters
    ----------
    uvs : array-like, (T, 3, 2)
    faces : array-like, (T, 3)
    resolution : int
        Texture side length ``R``; output is ``(1, R, R, 4)``.
    face_offset : int
        Added to every stored triangle id (id = ``face_offset + t + 1``). The
        original code used this to merge 100k-face chunks; this implementation
        handles all faces at once, so the default 0 is normally used.
    chunk_samples : int
        Vectorization batch size (triangle × sample pairs).

    Returns
    -------
    numpy.ndarray, (1, R, R, 4), float32
    """
    uvs, faces, R = _as_inputs(uvs, faces, resolution)
    T = faces.shape[0]
    rast = np.zeros((1, R, R, 4), dtype=np.float32)
    if T == 0:
        return rast

    # Pixel-space vertex coordinates.
    px = uvs[:, :, 0] * R
    py = uvs[:, :, 1] * R

    # Conservative pixel bbox: pixel x is covered only if x+0.5 is inside.
    x0 = np.ceil(px.min(axis=1) - 0.5).astype(np.int64)
    x1 = np.floor(px.max(axis=1) - 0.5).astype(np.int64)
    y0 = np.ceil(py.min(axis=1) - 0.5).astype(np.int64)
    y1 = np.floor(py.max(axis=1) - 0.5).astype(np.int64)

    np.clip(x0, 0, R - 1, out=x0)
    np.clip(x1, 0, R - 1, out=x1)
    np.clip(y0, 0, R - 1, out=y0)
    np.clip(y1, 0, R - 1, out=y1)

    bw = x1 - x0 + 1
    bh = y1 - y0 + 1
    keep = (bw > 0) & (bh > 0)

    # Twice the signed area; zero-area triangles cannot produce valid barycentrics.
    denom = (py[:, 1] - py[:, 2]) * (px[:, 0] - px[:, 2]) + (px[:, 2] - px[:, 1]) * (
        py[:, 0] - py[:, 2]
    )
    keep &= np.abs(denom) > _EPS
    if not keep.any():
        return rast

    tids = np.nonzero(keep)[0]
    sizes = np.maximum(bw[keep], bh[keep])

    powers = 1 << np.arange(0, max(1, int(np.ceil(np.log2(R))) + 1), dtype=np.int64)
    bin_of = np.searchsorted(powers, sizes, side="left")

    # Flat output buffers (faster than 2-D fancy indexing).
    face_flat = np.zeros(R * R, dtype=np.int64)
    u_flat = np.zeros(R * R, dtype=np.float32)
    v_flat = np.zeros(R * R, dtype=np.float32)

    for bi in np.unique(bin_of):
        S = int(powers[bi])
        ids = tids[bin_of == bi]
        # Cap the batch so a single vectorized block stays near chunk_samples.
        per_batch = max(1, int(chunk_samples) // (S * S))
        for start in range(0, ids.size, per_batch):
            batch = ids[start : start + per_batch]
            n = batch.size

            bx0 = x0[batch]
            by0 = y0[batch]
            bx1 = x1[batch]
            by1 = y1[batch]

            offs = np.arange(S, dtype=np.int64)
            X = np.broadcast_to(bx0[:, None, None] + offs[None, :, None], (n, S, S))
            Y = np.broadcast_to(by0[:, None, None] + offs[None, None, :], (n, S, S))
            in_bbox = (X <= bx1[:, None, None]) & (Y <= by1[:, None, None])

            sx = X.astype(np.float64) + 0.5
            sy = Y.astype(np.float64) + 0.5

            p0x = px[batch, 0][:, None, None]
            p0y = py[batch, 0][:, None, None]
            p1x = px[batch, 1][:, None, None]
            p1y = py[batch, 1][:, None, None]
            p2x = px[batch, 2][:, None, None]
            p2y = py[batch, 2][:, None, None]
            d = denom[batch][:, None, None]

            w0 = ((p1y - p2y) * (sx - p2x) + (p2x - p1x) * (sy - p2y)) / d
            w1 = ((p2y - p0y) * (sx - p2x) + (p0x - p2x) * (sy - p2y)) / d
            w2 = 1.0 - w0 - w1

            inside = in_bbox & (w0 >= -_EPS) & (w1 >= -_EPS) & (w2 >= -_EPS)
            sel = inside.ravel()
            if not sel.any():
                continue

            # Match nvdiffrast's boundary sanitation (rasterize.cu):
            # __saturatef(b0/b1) then clamp the sum to 1.0 diagonally.
            w0 = np.clip(w0, 0.0, 1.0)
            w1 = np.clip(w1, 0.0, 1.0)
            norm = np.maximum(w0 + w1, 1.0)
            w0 /= norm
            w1 /= norm

            pix = (Y * R + X).ravel()[sel]
            tri_ids = (batch + 1 + int(face_offset)).astype(np.int64)
            tri_ids_b = np.broadcast_to(tri_ids[:, None, None], (n, S, S)).ravel()[sel]

            # Scatter-max: after this, face_flat[pix] is the highest triangle id
            # that covers the pixel (order-independent).
            np.maximum.at(face_flat, pix, tri_ids_b)

            # The triangle that currently owns the pixel writes its barycentrics.
            take = face_flat[pix] == tri_ids_b
            if take.any():
                u_flat[pix[take]] = w0.ravel()[sel][take]
                v_flat[pix[take]] = w1.ravel()[sel][take]

    rast[0, :, :, 0] = u_flat.reshape(R, R)
    rast[0, :, :, 1] = v_flat.reshape(R, R)
    rast[0, :, :, 3] = face_flat.reshape(R, R).astype(np.float32)
    return rast


def interpolate_uv(vertices, rast, faces, chunk=1_000_000):
    """Interpolate per-vertex attributes at rasterized texels.

    Parameters
    ----------
    vertices : array-like, (V, C)
    rast : array-like, (1, R, R, 4)
        Output of :func:`rasterize_uv` (or nvdiffrast with the same layout).
    faces : array-like, (T, 3)
    chunk : int
        Pixels processed per vectorized block.

    Returns
    -------
    numpy.ndarray, (R, R, C), float32
    """
    vertices = np.asarray(vertices, dtype=np.float32)
    rast = np.asarray(rast, dtype=np.float32)
    faces = np.asarray(faces)
    if vertices.ndim != 2:
        raise ValueError(f"vertices must be (V,C), got {vertices.shape}")
    if rast.ndim != 4 or rast.shape[0] != 1 or rast.shape[3] != 4:
        raise ValueError(f"rast must be (1,R,R,4), got {rast.shape}")
    if rast.shape[1] != rast.shape[2]:
        raise ValueError(f"rast must be square, got {rast.shape}")
    if faces.ndim != 2 or faces.shape[1] != 3:
        raise ValueError(f"faces must be (T,3), got {faces.shape}")
    if not np.issubdtype(faces.dtype, np.integer):
        faces = faces.astype(np.int64)

    R = rast.shape[1]
    face_of_pixel = rast[0, :, :, 3].astype(np.int64) - 1
    flat_face = face_of_pixel.ravel()
    covered = np.nonzero(flat_face >= 0)[0]

    out = np.zeros((R * R, vertices.shape[1]), dtype=np.float32)
    if covered.size == 0:
        return out.reshape(R, R, vertices.shape[1])

    if flat_face.max() >= faces.shape[0]:
        raise ValueError(
            f"rast references triangle {int(flat_face.max())}, but only "
            f"{faces.shape[0]} triangles were provided"
        )

    u_flat = rast[0, :, :, 0].ravel()
    v_flat = rast[0, :, :, 1].ravel()

    for start in range(0, covered.size, int(chunk)):
        idx = covered[start : start + int(chunk)]
        tri = faces[flat_face[idx]]
        u = u_flat[idx][:, None]
        v = v_flat[idx][:, None]
        w = 1.0 - u - v
        out[idx] = (
            vertices[tri[:, 0]] * u
            + vertices[tri[:, 1]] * v
            + vertices[tri[:, 2]] * w
        )

    return out.reshape(R, R, vertices.shape[1])


# ---------------------------------------------------------------------------
# Torch adapters (torch is imported lazily so NumPy-only tests can run without it)
# ---------------------------------------------------------------------------


def rasterize_uv_torch(uvs, faces, resolution, face_offset=0):
    """Torch wrapper around :func:`rasterize_uv`.

    Tensors are moved to CPU for rasterization and the result is returned on the
    original device as float32. ``uvs`` is expected to be a CUDA/CPU float tensor.
    """
    import torch

    rast = rasterize_uv(
        uvs.detach().cpu().numpy(),
        faces.detach().cpu().numpy(),
        int(resolution),
        face_offset=face_offset,
    )
    return torch.from_numpy(rast).to(device=uvs.device)


def interpolate_uv_torch(vertices, rast, faces):
    """Torch wrapper around :func:`interpolate_uv` (CPU compute, original device out)."""
    import torch

    out = interpolate_uv(
        vertices.detach().cpu().numpy(),
        rast.detach().cpu().numpy(),
        faces.detach().cpu().numpy(),
    )
    return torch.from_numpy(out).to(device=vertices.device)