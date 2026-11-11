"""Unit tests for tools/ovoxel-export/uv_raster.py.

Run:
    python3 -m unittest tools/ovoxel-export/test_uv_raster.py -v

NumPy-only tests run everywhere. The torch adapter test reports an explicit
skip when torch is not installed (do not read that skip as a pass).
"""

from __future__ import annotations

import math
import os
import sys
import unittest

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from uv_raster import interpolate_uv, rasterize_uv  # noqa: E402


def _bary(p, tri):
    """Barycentric (w0, w1, w2) of point p in pixel coords for triangle tri (3,2)."""
    (x0, y0), (x1, y1), (x2, y2) = tri
    den = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
    if abs(den) < 1e-12:
        return None
    w0 = ((y1 - y2) * (p[0] - x2) + (x2 - x1) * (p[1] - y2)) / den
    w1 = ((y2 - y0) * (p[0] - x2) + (x0 - x2) * (p[1] - y2)) / den
    return w0, w1, 1.0 - w0 - w1


def _brute_force(uvs, faces, R):
    """Reference: per-pixel test against every triangle, ascending index (later wins)."""
    ids = np.zeros((R, R), dtype=np.int64)
    bary = np.zeros((R, R, 2), dtype=np.float64)
    T = len(faces)
    for t in range(T):
        p = uvs[t].astype(np.float64) * R
        x0 = max(0, int(math.floor(p[:, 0].min())))
        x1 = min(R - 1, int(math.ceil(p[:, 0].max())))
        y0 = max(0, int(math.floor(p[:, 1].min())))
        y1 = min(R - 1, int(math.ceil(p[:, 1].max())))
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                b = _bary((x + 0.5, y + 0.5), p)
                if b is None:
                    continue
                if min(b) >= -1e-12:
                    ids[y, x] = t + 1
                    bary[y, x] = (b[0], b[1])
    return ids, bary


class TestRasterizeUv(unittest.TestCase):
    def test_single_triangle_coverage(self):
        R = 16
        # Pixel coords (2,2), (14,2), (2,14) -> UV = coords / R.
        uvs = np.array([[[2, 2], [14, 2], [2, 14]]], dtype=np.float32) / R
        faces = np.array([[0, 1, 2]], dtype=np.int64)
        rast = rasterize_uv(uvs, faces, R)

        self.assertEqual(rast.shape, (1, R, R, 4))
        self.assertEqual(rast.dtype, np.float32)
        self.assertEqual(float(rast[0, 0, 0, 3]), 0.0)
        self.assertEqual(float(rast[0, 15, 15, 3]), 0.0)
        self.assertEqual(float(rast[0, 3, 3, 3]), 1.0)

        b = _bary((3.5, 3.5), uvs[0] * R)
        self.assertAlmostEqual(float(rast[0, 3, 3, 0]), b[0], places=6)
        self.assertAlmostEqual(float(rast[0, 3, 3, 1]), b[1], places=6)
        # Barycentrics sum to one at every covered texel.
        covered = rast[0, :, :, 3] > 0
        s = rast[0, :, :, 0] + rast[0, :, :, 1]
        self.assertTrue(np.all(s[covered] <= 1.0 + 1e-6))
        self.assertTrue(np.all(s[covered] >= -1e-6))

    def test_affine_attribute_reconstruction(self):
        """An affine attribute in UV must be reproduced at every covered texel."""
        R = 32
        uvs = np.array([[[0.1, 0.1], [0.9, 0.1], [0.1, 0.9]]], dtype=np.float32)
        faces = np.array([[0, 1, 2]], dtype=np.int64)
        # z = 1 + 3u + 2v evaluated at the triangle's actual UVs.
        verts = np.array(
            [[0.0, 0.0, 1.5], [1.0, 0.0, 3.9], [0.0, 1.0, 3.1]], dtype=np.float32
        )
        rast = rasterize_uv(uvs, faces, R)
        pos = interpolate_uv(verts, rast, faces)
        covered = rast[0, :, :, 3] > 0
        ys, xs = np.nonzero(covered)
        u = (xs + 0.5) / R
        v = (ys + 0.5) / R
        expected = 1.0 + 3.0 * u + 2.0 * v
        self.assertGreater(len(xs), 100)
        self.assertLess(float(np.max(np.abs(pos[ys, xs, 2] - expected))), 1e-4)

    def test_random_triangles_match_brute_force(self):
        rng = np.random.default_rng(1234)
        R = 24
        T = 12
        uvs = rng.random((T, 3, 2)).astype(np.float32)
        faces = np.arange(T * 3, dtype=np.int64).reshape(T, 3)
        rast = rasterize_uv(uvs, faces, R)
        ids_ref, bary_ref = _brute_force(uvs, faces, R)

        ids = rast[0, :, :, 3].astype(np.int64)
        self.assertTrue(
            np.array_equal(ids, ids_ref),
            msg=f"face-id mismatch at {int(np.sum(ids != ids_ref))} pixels",
        )
        both = (ids > 0) & (ids_ref > 0)
        du = np.abs(rast[0, :, :, 0][both] - bary_ref[both, 0])
        dv = np.abs(rast[0, :, :, 1][both] - bary_ref[both, 1])
        self.assertLess(float(du.max()), 1e-5)
        self.assertLess(float(dv.max()), 1e-5)

    def test_overlap_highest_index_wins(self):
        R = 16
        # Two identical triangles; the second one (id 2) must win.
        tri = [[4, 4], [12, 4], [4, 12]]
        uvs = np.array([tri, tri], dtype=np.float32) / R
        faces = np.array([[0, 1, 2], [0, 1, 2]], dtype=np.int64)
        rast = rasterize_uv(uvs, faces, R)
        self.assertEqual(float(rast[0, 6, 6, 3]), 2.0)

    def test_face_offset(self):
        R = 8
        uvs = np.array([[[1, 1], [6, 1], [1, 6]]], dtype=np.float32) / R
        faces = np.array([[0, 1, 2]], dtype=np.int64)
        rast = rasterize_uv(uvs, faces, R, face_offset=100)
        self.assertEqual(float(rast[0, 3, 3, 3]), 101.0)

    def test_degenerate_triangle_skipped(self):
        R = 8
        uvs = np.array([[[1, 1], [2, 2], [3, 3]]], dtype=np.float32) / R
        faces = np.array([[0, 1, 2]], dtype=np.int64)
        rast = rasterize_uv(uvs, faces, R)
        self.assertEqual(float(np.max(rast[0, :, :, 3])), 0.0)

    def test_empty_input(self):
        R = 8
        rast = rasterize_uv(np.zeros((0, 3, 2)), np.zeros((0, 3), np.int64), R)
        self.assertEqual(float(np.max(rast[0, :, :, 3])), 0.0)

    def test_chunking_is_exactly_equivalent(self):
        rng = np.random.default_rng(99)
        R = 32
        T = 40
        uvs = rng.random((T, 3, 2)).astype(np.float32)
        faces = np.arange(T * 3, dtype=np.int64).reshape(T, 3)
        a = rasterize_uv(uvs, faces, R, chunk_samples=10_000_000)
        b = rasterize_uv(uvs, faces, R, chunk_samples=64)
        self.assertTrue(np.array_equal(a, b))

    def test_interpolate_empty_background(self):
        R = 8
        rast = np.zeros((1, R, R, 4), dtype=np.float32)
        verts = np.zeros((3, 3), dtype=np.float32)
        faces = np.zeros((0, 3), dtype=np.int64)
        out = interpolate_uv(verts, rast, faces)
        self.assertEqual(out.shape, (R, R, 3))
        self.assertEqual(float(np.max(np.abs(out))), 0.0)

    def test_uv_origin_is_top_left_row0_col0(self):
        """Locks the dr orientation: uv(0,0) -> pixel (0,0), v grows with row."""
        R = 16
        uvs = np.array([[[0, 0], [8, 0], [0, 8]]], dtype=np.float32) / R
        faces = np.array([[0, 1, 2]], dtype=np.int64)
        rast = rasterize_uv(uvs, faces, R)
        self.assertEqual(float(rast[0, 0, 0, 3]), 1.0)
        self.assertEqual(float(rast[0, 15, 15, 3]), 0.0)

    def test_vertex_uv_input_matches_per_corner(self):
        """(V,2) vertex UVs + faces must equal the gathered (T,3,2) form."""
        rng = np.random.default_rng(7)
        R = 32
        T, V = 20, 40
        vert_uvs = rng.random((V, 2)).astype(np.float32)
        faces = rng.integers(0, V, size=(T, 3), dtype=np.int64)
        a = rasterize_uv(vert_uvs, faces, R)
        b = rasterize_uv(vert_uvs[faces], faces, R)
        self.assertTrue(np.array_equal(a, b))

    def test_vertex_uv_out_of_range_rejected(self):
        uvs = np.zeros((4, 2), dtype=np.float32)
        faces = np.array([[0, 1, 9]], dtype=np.int64)
        with self.assertRaises(ValueError):
            rasterize_uv(uvs, faces, 8)

    def test_torch_adapter(self):
        try:
            import torch  # noqa: F401
        except Exception:
            self.skipTest("torch 未安装 — torch 适配器 NOT-RUN（不得读作通过）")
        import torch
        from uv_raster import interpolate_uv_torch, rasterize_uv_torch

        R = 16
        uvs = np.array([[[2, 2], [14, 2], [2, 14]]], dtype=np.float32) / R
        faces = np.array([[0, 1, 2]], dtype=np.int64)
        verts = np.array([[0.0, 0.0, 1.0], [1.0, 0.0, 4.0], [0.0, 1.0, 3.0]], dtype=np.float32)

        t_uvs = torch.from_numpy(uvs)
        t_faces = torch.from_numpy(faces)
        t_verts = torch.from_numpy(verts)

        rast_np = rasterize_uv(uvs, faces, R)
        rast_t = rasterize_uv_torch(t_uvs, t_faces, R).cpu().numpy()
        self.assertTrue(np.array_equal(rast_np, rast_t))

        pos_np = interpolate_uv(verts, rast_np, faces)
        pos_t = interpolate_uv_torch(t_verts, torch.from_numpy(rast_np), t_faces).cpu().numpy()
        self.assertTrue(np.array_equal(pos_np, pos_t))


if __name__ == "__main__":
    unittest.main(verbosity=2)