# Third-party components

- Mono runtime and class libraries: 6.8.0.105 ARMHF packages from the Mono project's Debian Buster distribution. The packaged runtime's copyright/license notice is included under `Roms/PORTS/Games/KingdomNewLands/mono/COPYRIGHT`. Source: https://github.com/mono/mono .
- stb_image, stb_image_write and stb_vorbis: source commit f58f558c, https://github.com/nothings/stb . Dual public-domain/MIT terms appear in the included source headers in this project's source repository.
- The preparer installs UnityPy, TypeTreeGeneratorAPI, dnfile and Pillow using `tools/requirements.txt`. Their distributions carry their own notices. FFmpeg is a separately supplied prerequisite; this candidate does not bundle FFmpeg.
- SigmaStar MI libraries are linked against the device's firmware-provided libraries; they are not included in the candidate archive.

The original game's assemblies and assets are supplied by the player and excluded from release archives.

Box2D 2.3.1 is used for the pouch's polygon contacts. Source: https://github.com/erincatto/box2d/tree/v2.3.1 (commit 7e633c4fb86a68bf072fb8ae67ea2c060114750e). The bridge is separate from the unmodified upstream library.

Copyright (c) 2006-2013 Erin Catto http://www.gphysics.com

This software is provided 'as-is', without any express or implied
warranty. In no event will the authors be held liable for any damages
arising from the use of this software.

Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it
freely, subject to the following restrictions:

1. The origin of this software must not be misrepresented; you must not
   claim that you wrote the original software. If you use this software
   in a product, an acknowledgment in the product documentation would be
   appreciated but is not required.
2. Altered source versions must be plainly marked as such, and must not be
   misrepresented as being the original software.
3. This notice may not be removed or altered from any source distribution.

The ARM native library statically links portions of GCC 8.2 libstdc++/libgcc to avoid an incompatible device C++ runtime. The GNU GPL version 3 and GCC Runtime Library Exception 3.1 are included under `licenses/`. Upstream source: https://gcc.gnu.org/pub/gcc/releases/gcc-8.2.0/ .
