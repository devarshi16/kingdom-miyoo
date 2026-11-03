import importlib.util
import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest
import subprocess
import zipfile
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT), str(ROOT / 'tools')]
import prepare_data as prep
from data_checks import GAME_DLLS, SERIALIZED, validate_pack, validate_source


def pack_bytes(missing=None):
    entries = [('objects/' + n, b'KNLB') for n in SERIALIZED]
    entries.append(('sprites', b'KNLS' + struct.pack('<II', 1, 1)))
    entries = [(n, b) for n, b in entries if n != missing]
    offset = 12 + sum(2 + len(n) + 12 for n, _ in entries)
    directory = b''
    for n, b in entries:
        directory += struct.pack('<H', len(n)) + n.encode() + struct.pack('<QI', offset, len(b))
        offset += len(b)
    return b'KNLP' + struct.pack('<II', 1, len(entries)) + directory + b''.join(b for _, b in entries)


class PreparationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)

    def test_explicit_invalid_path_does_not_autodetect(self):
        self.assertIsNone(prep.find_kingdom_data(str(self.root / 'missing')))

    def test_incomplete_game_fails_before_dependencies_or_output(self):
        p = self.root / 'Kingdom_Data'; (p / 'Managed').mkdir(parents=True)
        with self.assertRaisesRegex(ValueError, 'Incomplete'):
            validate_source(p)

    def test_unverified_build_requires_opt_in(self):
        p = self.root / 'Kingdom_Data'; (p / 'Managed').mkdir(parents=True)
        for n in list(SERIALIZED) + ['Managed/' + x for x in GAME_DLLS] + ['Managed/UnityEngine.dll', 'Managed/Rewired_Core.dll']:
            (p / n).write_bytes(b'unverified')
        with self.assertRaisesRegex(ValueError, 'not been verified'):
            validate_source(p)
        self.assertEqual(validate_source(p, True)['profile'], 'UNVERIFIED')

    def test_missing_and_truncated_pack_entries_rejected(self):
        p = self.root / 'pack'
        p.write_bytes(pack_bytes())
        self.assertEqual(len(validate_pack(p)), len(SERIALIZED) + 1)
        p.write_bytes(pack_bytes('objects/level1'))
        with self.assertRaisesRegex(ValueError, 'missing required'):
            validate_pack(p)
        p.write_bytes(pack_bytes()[:-1])
        with self.assertRaises(ValueError):
            validate_pack(p)

    def make_install(self):
        target = self.root / 'sd/Roms/PORTS/Games/KingdomNewLands'
        for name in ['knl.sh', 'managed/UnityEngine.dll', 'managed/Rewired_Core.dll', 'managed/knl.dll', 'managed/knl.exe',
                     'managed/Rewired_Windows_Lib.dll', 'mono/bin/mono-sgen', 'lib/libknlcore.so', 'save/sentinel']:
            p = target / name; p.parent.mkdir(parents=True, exist_ok=True); p.write_bytes(b'preserved')
        pack = self.root / 'pack'; (pack / 'music').mkdir(parents=True)
        (pack / 'kingdom.knlpack').write_bytes(pack_bytes())
        source = self.root / 'Kingdom_Data'; (source / 'Managed').mkdir(parents=True)
        for name in GAME_DLLS:
            (source / 'Managed' / name).write_bytes(b'game')
        return target, pack, source

    def test_install_preserves_saves_and_removes_stale_assemblies(self):
        target, pack, source = self.make_install()
        prep.install_candidate(pack, source, self.root / 'sd', {})
        self.assertEqual((target / 'save/sentinel').read_bytes(), b'preserved')
        self.assertFalse((target / 'managed/Rewired_Windows_Lib.dll').exists())
        self.assertFalse((target / 'managed/knl.exe').exists())
        backup = next(target.parent.glob('KingdomNewLands.backup-*'))
        self.assertEqual((backup / 'save/sentinel').read_bytes(), b'preserved')
        self.assertTrue((target / 'installation.json').exists())

    def test_corrupt_pack_leaves_existing_installation_untouched(self):
        target, pack, source = self.make_install()
        (pack / 'kingdom.knlpack').write_bytes(b'bad')
        with self.assertRaises(ValueError):
            prep.install_candidate(pack, source, self.root / 'sd', {})
        self.assertEqual((target / 'save/sentinel').read_bytes(), b'preserved')
        self.assertFalse(list(target.parent.glob('KingdomNewLands.backup-*')))

    def test_pc_preparation_replaces_native_receipt_and_retains_rollback(self):
        target, pack, source = self.make_install()
        (target / '.native-ready').write_text('prior native receipt')
        cache = target / '.prepare-v1/old'; cache.mkdir(parents=True)
        (cache / 'checkpoint').write_text('prior native checkpoint')
        prep.install_candidate(pack, source, self.root / 'sd', {})
        self.assertFalse((target / '.native-ready').exists())
        self.assertFalse((target / '.prepare-v1').exists())
        backup = next(target.parent.glob('KingdomNewLands.backup-*'))
        self.assertEqual((backup / '.native-ready').read_text(), 'prior native receipt')
        self.assertEqual((target / 'save/sentinel').read_bytes(), b'preserved')

    def test_pc_preparation_refuses_unrecovered_native_transaction(self):
        target, pack, source = self.make_install()
        (target / '.install-journal').write_text('pending activation')
        with self.assertRaisesRegex(ValueError, 'recover'):
            prep.install_candidate(pack, source, self.root / 'sd', {})
        self.assertEqual((target / 'save/sentinel').read_bytes(), b'preserved')
        self.assertFalse(list(target.parent.glob('KingdomNewLands.backup-*')))

    def test_failed_commit_rolls_back_existing_directory(self):
        target, pack, source = self.make_install()
        original = Path.rename
        def fail_stage(path, dest):
            if path.parent.name.startswith('.knl-install-'):
                raise OSError('simulated SD rename failure')
            return original(path, dest)
        with mock.patch.object(Path, 'rename', fail_stage):
            with self.assertRaises(OSError):
                prep.install_candidate(pack, source, self.root / 'sd', {})
        self.assertEqual((target / 'save/sentinel').read_bytes(), b'preserved')
        self.assertTrue((target / 'managed/Rewired_Windows_Lib.dll').exists())

    def test_release_is_deterministic_and_excludes_owned_data(self):
        import package_release as release
        build = self.root / 'build'
        for n in release.RUNTIME + ['managed/Assembly-CSharp.dll', 'data/kingdom.knlpack', 'data/ui-fontdata.json', 'mono/mono-sgen']:
            p = build / release.GAME / n
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_bytes(b'fixture')
        command = [sys.executable, str(ROOT / 'tools/package_release.py'), '--build', str(build), '--out', str(self.root / 'dist')]
        subprocess.run(command, check=True, capture_output=True)
        archive = next((self.root / 'dist').glob('*.zip'))
        original = archive.read_bytes()
        subprocess.run(command, check=True, capture_output=True)
        self.assertEqual(original, archive.read_bytes())
        with zipfile.ZipFile(archive) as z:
            self.assertNotIn(release.GAME + 'managed/Assembly-CSharp.dll', z.namelist())
            self.assertNotIn(release.GAME + 'data/kingdom.knlpack', z.namelist())
            self.assertNotIn(release.GAME + 'data/ui-fontdata.json', z.namelist())
            self.assertNotIn(release.GAME + 'mono/mono-sgen', z.namelist())
            self.assertIn('prepare_data.py', z.namelist())
            manifest = json.loads(z.read('release-manifest.json'))
            import hashlib
            for n, sha in manifest['files'].items():
                self.assertEqual(hashlib.sha256(z.read(n)).hexdigest(), sha)


if __name__ == '__main__':
    unittest.main()
