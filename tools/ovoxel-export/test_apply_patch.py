"""Tests for apply_patch.py using a throwaway fake o_voxel package.

Run:
    python3 -m unittest tools/ovoxel-export/test_apply_patch.py -v
"""

from __future__ import annotations

import hashlib
import os
import pathlib
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import apply_patch  # noqa: E402


def _sha(text: str) -> str:
    return hashlib.sha256(text.encode()).hexdigest()


class TestApplyPatch(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.pkg = pathlib.Path(self.tmp.name)
        self.orig = "from typing import *\n# fake upstream\n"
        (self.pkg / "postprocess.py").write_text(self.orig, encoding="utf-8")

    def tearDown(self):
        self.tmp.cleanup()

    def test_refuses_unknown_upstream_version(self):
        with self.assertRaises(SystemExit) as cm:
            apply_patch.main(["--target", str(self.pkg), "--apply"])
        self.assertNotEqual(cm.exception.code, 0)
        # Nothing must have been written.
        self.assertEqual(
            (self.pkg / "postprocess.py").read_text(encoding="utf-8"), self.orig
        )
        self.assertFalse((self.pkg / "uv_raster.py").exists())

    def test_apply_force_then_check_then_revert(self):
        rc = apply_patch.main(["--target", str(self.pkg), "--apply", "--force"])
        self.assertEqual(rc, 0)
        patched = (self.pkg / "postprocess.py").read_text(encoding="utf-8")
        self.assertIn(apply_patch.MARKER, patched)
        self.assertEqual(patched.count("import nvdiffrast"), 0)
        self.assertTrue((self.pkg / "uv_raster.py").is_file())

        self.assertEqual(apply_patch.main(["--target", str(self.pkg), "--check"]), 0)

        # Revert restores the exact original bytes.
        apply_patch.revert(self.pkg)
        self.assertEqual(
            (self.pkg / "postprocess.py").read_text(encoding="utf-8"), self.orig
        )
        self.assertFalse((self.pkg / "uv_raster.py").exists())
        self.assertFalse((self.pkg / "postprocess.py.orig").exists())

    def test_check_reports_upstream(self):
        with self.assertRaises(SystemExit):
            apply_patch.main(["--target", str(self.pkg), "--apply"])
        # For a fake upstream the state is unknown (exit 2), not "upstream".
        self.assertEqual(apply_patch.main(["--target", str(self.pkg), "--check"]), 2)

    def test_patch_source_hash_is_pinned(self):
        # The vendored patched file must still carry the audited upstream hash.
        text = apply_patch.PATCHED.read_text(encoding="utf-8")
        self.assertIn(apply_patch.UPSTREAM_SHA256, text)


if __name__ == "__main__":
    unittest.main(verbosity=2)