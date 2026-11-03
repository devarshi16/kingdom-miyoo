#!/usr/bin/env python3
"""Author build only: portable importer IL plus an ARM/host native codec helper.

No game directory is read. Players receive the built importer and need no compiler,
Python or network connection. Use --cache on a data volume for dependency builds.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tarfile
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
PINS = {
    'ogg': ('xiph/ogg', '06a5e0262cdc28aa4ae6797627a783b5010440f0'),
    'vorbis': ('xiph/vorbis', '1b75110b5a2754ba1931d82dd83cb822b266a21d'),
    'vgmstream': ('vgmstream/vgmstream', 'd29ffa6e19a41efb54bfd9f68bd65f93c3d2ab25'),
}


def run(command, **kwargs):
    subprocess.run([str(x) for x in command], check=True, **kwargs)


def digest(path):
    with Path(path).open('rb') as stream:
        h = hashlib.sha256()
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
        return h.hexdigest()


def checkout(cache, name, provided):
    repository, commit = PINS[name]
    if provided and (provided / name).is_dir():
        path = provided / name
        actual = subprocess.check_output(['git', '-C', str(path), 'rev-parse', 'HEAD'], text=True).strip()
        if actual != commit:
            raise ValueError(f'{name}: expected pinned {commit}, got {actual}')
        return path
    path = cache / (name + '-' + commit)
    if path.is_dir():
        return path
    archive = cache / (name + '-' + commit + '.tar.gz')
    if not archive.exists():
        with urllib.request.urlopen(f'https://codeload.github.com/{repository}/tar.gz/{commit}') as response:
            with archive.open('wb') as output:
                shutil.copyfileobj(response, output)
    with tarfile.open(archive) as bundle:
        # Python 3.10 is supported by the author tools too. Dependency source
        # archives need only regular files/directories, never links or devices.
        for member in bundle.getmembers():
            target = (cache / member.name).resolve()
            if not target.is_relative_to(cache.resolve()) or not (member.isfile() or member.isdir()):
                raise ValueError('Unsafe dependency archive member: ' + member.name)
        bundle.extractall(cache)
    if not path.is_dir():
        raise ValueError('Dependency archive has unexpected root: ' + name)
    return path


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--target', choices=['host', 'miyoo'], default='miyoo')
    p.add_argument('--out', required=True, type=Path, help='port game folder with managed/ and lib/')
    p.add_argument('--cache', required=True, type=Path)
    p.add_argument('--sources', type=Path, help='optional existing checkouts at the exact pinned commits')
    p.add_argument('--cecil', type=Path, default=Path('/usr/lib/mono/gac/Mono.Cecil/0.11.0.0__0738eb9f132ed756/Mono.Cecil.dll'))
    args = p.parse_args()
    cache = args.cache.resolve(); cache.mkdir(parents=True, exist_ok=True)
    out = args.out.resolve(); (out / 'lib').mkdir(parents=True, exist_ok=True)
    resources = out / 'importer'; resources.mkdir(exist_ok=True)
    if not args.cecil.is_file():
        p.error('Mono.Cecil 0.11 is required for the author build (libmono-cecil-private-cil)')
    deps = {name: checkout(cache, name, args.sources) for name in PINS}
    build = cache / args.target; build.mkdir(exist_ok=True)
    prefix = build / 'prefix'
    cmake_args = ['-DCMAKE_POSITION_INDEPENDENT_CODE=ON', '-DBUILD_TESTING=OFF', '-DBUILD_SHARED_LIBS=OFF']
    cc = os.environ.get('CC', 'gcc')
    flags = ['-O3', '-fPIC']
    if args.target == 'miyoo':
        cc = os.environ.get('MIYOO_CC', str(Path.home() / 'Recompilation/tools/prebuilt/bin/arm-linux-gnueabihf-gcc'))
        flags += ['-mcpu=cortex-a7', '-mfpu=neon-vfpv4', '-mfloat-abi=hard', '-marm']
        toolchain = build / 'toolchain.cmake'
        toolchain.write_text('set(CMAKE_SYSTEM_NAME Linux)\nset(CMAKE_SYSTEM_PROCESSOR arm)\n' +
            f'set(CMAKE_C_COMPILER "{cc}")\nset(CMAKE_C_FLAGS_INIT "{" ".join(flags)}")\n')
        cmake_args += ['-DCMAKE_TOOLCHAIN_FILE=' + str(toolchain)]
    for name in ['ogg', 'vorbis']:
        directory = build / name
        run(['cmake', '-S', deps[name], '-B', directory, '-DCMAKE_BUILD_TYPE=Release',
             '-DCMAKE_INSTALL_PREFIX=' + str(prefix), '-DCMAKE_PREFIX_PATH=' + str(prefix),
             '-DINSTALL_DOCS=OFF', *cmake_args])
        run(['cmake', '--build', directory, '-j', '4'])
        run(['cmake', '--install', directory])
    header = (deps['vgmstream'] / 'src/coding/libs/vorbis_codebooks_fsb.h').read_text()
    old = '#include "../../util/vorbis_codebooks.h"'
    if header.count(old) != 1:
        raise ValueError('Pinned codec table include changed')
    (build / 'fsb_codebooks.h').write_text(header.replace(old,
        'typedef struct { uint32_t id; uint32_t size; const uint8_t *codebooks; } vcb_info_t;'))
    native = ROOT / 'tools/native_import/native'
    run([cc, *flags, '-std=gnu99', '-shared', native / 'audio.c', native / 'hash.c',
         '-I' + str(build), '-I' + str(prefix / 'include'), '-L' + str(prefix / 'lib'),
         '-lvorbisenc', '-lvorbis', '-logg', '-lm', '-o', out / 'lib/libknlimport.so'])
    managed = sorted((ROOT / 'tools/native_import/managed').glob('*.cs'))
    production = [p for p in managed if p.name not in ['Inspect.cs', 'PixelInspect.cs', 'AudioInspect.cs']]
    parser = ROOT / 'engine/UnityEngine/Core/JsonParser.cs'
    run(['mcs', '-optimize+', '-main:KNL.Import.MainProgram', '-out:' + str(resources / 'NativeImporter.exe'),
         '-r:' + str(args.cecil), parser, *production])
    shutil.copy2(args.cecil, resources / 'Mono.Cecil.dll')
    run([os.sys.executable, ROOT / 'tools/native_import/export_schemas.py', resources / 'unity2017.knlt'])
    shutil.copy2(ROOT / 'tools/supported_build.json', resources / 'supported_build.json')
    shutil.copytree(ROOT / 'licenses/native-import', resources / 'licenses', dirs_exist_ok=True)
    identity = hashlib.sha256()
    for path in [parser, *production, *sorted(native.glob('*.c')), ROOT / 'tools/native_import/export_schemas.py']:
        identity.update(path.read_bytes())
    identity.update(json.dumps(PINS, sort_keys=True).encode())
    identity.update((resources / 'unity2017.knlt').read_bytes())
    (resources / 'build-id.txt').write_text(identity.hexdigest() + '\n')
    runtime_files = ['managed/UnityEngine.dll', 'managed/Rewired_Core.dll', 'managed/knl.dll',
        'lib/libknlcore.so', 'lib/libknlimport.so', 'importer/Mono.Cecil.dll',
        'importer/unity2017.knlt', 'importer/NativeImporter.exe']
    (resources / 'runtime.json').write_text(json.dumps({name: digest(out / name) for name in runtime_files}, indent=2) + '\n')
    print('Standalone first-launch importer built:', resources)


if __name__ == '__main__':
    main()
