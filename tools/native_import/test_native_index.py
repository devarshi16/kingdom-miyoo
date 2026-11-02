#!/usr/bin/env python3
"""Private format fixtures and an optional comparison against local extraction output."""
import json
import os
from pathlib import Path
import struct
import subprocess
import tempfile
import unittest

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
FILES = ("globalgamemanagers", "globalgamemanagers.assets", "level0", "level1", "level2",
         "resources.assets", "sharedassets0.assets", "sharedassets1.assets", "sharedassets2.assets")


def fixture_file(path, *, declared_size=None, payload=b"data", offset=128, tree=False):
    meta = bytearray(b"2017.1.2p1\0")
    meta += struct.pack("<iBi", 5, int(tree), 1)  # platform, type-tree flag, type count
    meta += struct.pack("<iBh", 1, 0, -1) + bytes(16)  # Object type, v17 type metadata
    if tree:
        meta += struct.pack("<ii", 1, 0) + bytes(24)  # one flattened 24-byte node
    meta += struct.pack("<i", 1)  # object count
    while (20 + len(meta)) % 4:
        meta.append(0)
    meta += struct.pack("<qIii", 42, 0, len(payload), 0)  # PathID, data offset, size, type index
    if offset < 20 + len(meta):
        offset = (20 + len(meta) + 15) & ~15
    total = offset + len(payload)
    declared = len(meta) if declared_size is None else declared_size
    header = struct.pack(">IIII", declared, total, 17, offset) + bytes((0, 0, 0, 0))
    path.write_bytes(header + meta + bytes(offset - len(header) - len(meta)) + payload)


class NativeIndexTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.TemporaryDirectory(prefix="knl-native-index-test-")
        cls.bin = Path(cls.tmp.name) / "native_index"
        subprocess.run([os.environ.get("CC", "cc"), "-O2", "-Wall", "-Wextra", "-Werror",
                        str(HERE / "native_index.c"), "-o", str(cls.bin)], check=True)

    @classmethod
    def tearDownClass(cls):
        cls.tmp.cleanup()

    def fixture_dir(self):
        d = Path(self.tmp.name) / "fixture"
        d.mkdir(exist_ok=True)
        for n in FILES:
            fixture_file(d / n)
        return d

    def run_fixture(self, d):
        return subprocess.run([str(self.bin), "fixture-index", str(d)], text=True,
                              stdout=subprocess.PIPE, stderr=subprocess.PIPE)

    def test_valid_records_and_restart(self):
        d = self.fixture_dir()
        fixture_file(d / "level0", tree=True)
        first = self.run_fixture(d)
        again = self.run_fixture(d)
        self.assertEqual(first.returncode, 0, first.stderr)
        self.assertEqual(again.returncode, 0, again.stderr)
        self.assertEqual(first.stdout, again.stdout)
        lines = [json.loads(line) for line in first.stdout.splitlines()]
        self.assertEqual(len(lines), 9)
        self.assertTrue(all(len(x["objects"]) == 1 and x["objects"][0]["path_id"] == "42" for x in lines))

    def test_truncated_metadata_rejected(self):
        d = self.fixture_dir()
        p = d / "level0"
        p.write_bytes(p.read_bytes()[:-18])
        result = self.run_fixture(d)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("ERROR:", result.stderr)

    def test_declared_file_size_mismatch_rejected(self):
        d = self.fixture_dir()
        p = d / "level0"
        b = bytearray(p.read_bytes())
        b[4:8] = struct.pack(">I", len(b) + 1)
        p.write_bytes(b)
        self.assertNotEqual(self.run_fixture(d).returncode, 0)

    def test_payload_range_past_end_rejected(self):
        d = self.fixture_dir()
        p = d / "level0"
        b = bytearray(p.read_bytes())
        # Object byteStart is at the aligned object record after metadata header/type table.
        meta_size = struct.unpack_from(">I", b, 0)[0]
        record = 20 + meta_size - 20
        record = (record + 3) & ~3
        struct.pack_into("<I", b, record + 8, 0x7fffffff)
        p.write_bytes(b)
        self.assertNotEqual(self.run_fixture(d).returncode, 0)

    def test_missing_source_fails_clearly(self):
        d = self.fixture_dir()
        (d / "level0").unlink()
        result = self.run_fixture(d)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("missing level0", result.stderr)

    def test_path_length_is_bounded(self):
        result = self.run_fixture(Path("x" * 4100))
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("path too long", result.stderr)

    def test_unknown_source_fingerprint_stops_before_indexing(self):
        d = self.fixture_dir()
        result = subprocess.run([str(self.bin), "index", str(d)], text=True,
                                stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("unsupported or changed source fingerprint", result.stderr)
        self.assertEqual(result.stdout, "")

    def test_local_source_index_matches_existing_unitypy_object_ids(self):
        source = Path(os.environ.get("KNL_GAME_DATA", "/home/devarshi/Games/KingdomNewLands/Kingdom_Data"))
        extraction = Path(os.environ.get("KNL_EXTRACTION", ROOT / "extracted")) / "objects"
        if not source.is_dir() or not extraction.is_dir():
            self.skipTest("local owned source/extraction not present")
        result = subprocess.run([str(self.bin), "index", str(source)], text=True,
                                stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        self.assertEqual(result.returncode, 0, result.stderr)
        lines = [json.loads(line) for line in result.stdout.splitlines()]
        self.assertEqual(len(lines), 9)
        for record in lines:
            extracted = json.loads((extraction / (record["file"] + ".json")).read_text())
            self.assertEqual({o["path_id"] for o in record["objects"]}, set(extracted["objects"]))


if __name__ == "__main__":
    unittest.main(verbosity=2)
