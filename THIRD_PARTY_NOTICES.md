# Third-party components

- Mono runtime and class libraries: 6.8.0.105 ARMHF packages from the Mono project's Debian Buster distribution. The packaged runtime's copyright/license notice is included under `Roms/PORTS/Games/KingdomNewLands/mono/COPYRIGHT`. Source: https://github.com/mono/mono .
- stb_image, stb_image_write and stb_vorbis: source commit f58f558c, https://github.com/nothings/stb . Dual public-domain/MIT terms appear in the included source headers in this project's source repository.
- The preparer installs UnityPy, TypeTreeGeneratorAPI, dnfile and Pillow using `tools/requirements.txt`. Their distributions carry their own notices. FFmpeg is a separately supplied prerequisite; this candidate does not bundle FFmpeg.
- SigmaStar MI libraries are linked against the device's firmware-provided libraries; they are not included in the candidate archive.

The original game's assemblies and assets are supplied by the player and excluded from release archives.
