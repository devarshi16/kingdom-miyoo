# Design decisions

Short records of choices that shape the port. Each has a status; "open" items get
settled by measurement in a later milestone.

Reference build: Kingdom New Lands **1.2.8**, Unity **2017.1.2p1**, Mono scripting
backend (.NET 3.5 profile), Windows x86-64 (Epic Games Store install).
Target: Miyoo Mini Plus — SigmaStar SSD202D, 2× Cortex-A7 @ 1.2 GHz (ARMv7-A, NEON,
hard-float), 128 MB DDR3, no 3D GPU (2D blitter only), 640×480 IPS, OnionOS.

---

## D1 — Runtime: original assemblies on Mono + replacement `UnityEngine.dll` (accepted)

The real Unity player cannot run on this device (no GLES-capable GPU; Unity 2017 has no
ARM32 Linux player). Instead:

- The user's own `Assembly-CSharp.dll`, `Assembly-CSharp-firstpass.dll`,
  `UnityEngine.UI.dll` and `DOTween*.dll` run **unmodified** on Mono for armhf.
- We provide a clean-room `UnityEngine.dll` that implements exactly the API surface the
  game references (see `unity_api_surface.md`), backed by a small native core
  (software sprite renderer, Box2D, audio mixer, input).
- `Rewired_*`, Sony/Nintendo/XInput assemblies and the `CSteamworks` /
  `NintendoSDKPlugin` / `sdkencryptedappticket` native imports are replaced by stubs.
- Preferred execution mode on device: Mono full-AOT compiled on the PC at install time
  (less RAM than JIT, faster start). JIT is the fallback while bringing up.

Why not a ground-up rewrite: more work, and it means redistributing re-authored
game logic. This path keeps the repo free of game code and data.

## D2 — Display: 320×240 internal, exact 2× upscale, crop the sides (accepted)

- Sprites are 32 px per world unit; the original orthographic camera shows 8 units of
  height (256 px) at 16:9 — about 455×256 px of art.
- We render at **320×240** and blit 2× to the 640×480 panel (integer scale, sharp
  pixels, cheapest possible path).
- Visible area becomes **10 × 7.5 world units** (original ≈ 14.2 × 8): ~30% less
  horizontally, 16 px less vertically.
- Vertical: render with orthographic size 3.75 instead of 4, centred as the game
  centres it (8 px trimmed top and bottom). *Tune in M1* — if the water reflection at
  the bottom matters more than the sky, bias the crop upward.
- Horizontal: crop symmetric around the game's camera position. The game's own camera
  look-ahead logic is left untouched.

## D3 — Off-screen enemy indicators (accepted, implement in M2)

Compensates for D2's narrower view.

- **When:** an enemy is outside our 10-unit-wide view but *would* be visible in the
  original 16:9 view, i.e. `5.0 < |enemy.x − camera.x| ≤ 7.1` world units. Optionally
  extend to a slightly larger look-ahead later if playtesting says so.
- **What:** a tiny purple chevron (≈4×6 px at internal res, colour sampled from the
  enemy sprite palette in M1) on the left or right edge, at the enemy's clamped
  height. Several enemies on one side → one chevron with a brighter/pulsing state, no
  counters (keep the HUD-less look).
- **How:** drawn by our renderer as an overlay after the game's frame, so no game
  code is patched. The overlay finds live, active components deriving from the game's
  `Enemy : MonoBehaviour` class (subclasses include `Troll`), resolved by reflection.

## D4 — Bring-your-own-data, converted on the PC at install time (accepted)

- Nothing from the game is committed or shipped. `tools/convert.py` reads the user's
  `Kingdom_Data`, writes `extracted/` (inspection format: PNG, WAV/OGG, JSON), and a
  later packer step produces the compact device format.
- MonoBehaviour data in this build has **no embedded type trees**; the converter
  rebuilds them from the user's own DLLs (TypeTreeGeneratorAPI 0.0.10). Two generator
  quirks are patched in `PatchedTypeTreeGenerator` (MonoBehaviour header alignment;
  `List<string>` typed as `string`). Heavy lifting stays on the PC; the device never
  parses Unity serialized files.
- **M0 result (1.2.8):** 16,880 objects in 9 files, 0 decode failures; 4,357/4,357
  MonoBehaviours resolve to their script class. Full run ≈ 35 s on the dev PC.

## D5 — Textures: 8-bit indexed, one palette per sprite (accepted after M0)

- Raw: 96 textures ≈ 38 MB as RGBA8888, dominated by one 2048×2048 and one 2048×1024
  sprite atlas.
- Measured: the big atlas pages have 2,041 and 814 unique RGBA colours, so one palette
  per *page* is lossy. Per *sprite*, 2,202 of 2,205 sprites have ≤ 256 colours.
- Decision: re-pack sprites at install time into 8-bit indexed pages with a palette per
  sprite (≈ 4× smaller than RGBA, cheap indexed blits). The 3 exceptions
  (`NG_Logo2048`, `logo_with_scroll`, `blossom_tree`) stay RGBA8888.
- 2,037 of the 2,205 sprites are tight-packed (mesh outlines, not rectangles); none are
  rotated. The packer can treat them as rectangles with transparent padding.

## D6 — Audio (music settled; SFX residency open)

- 297 clips; all decode. Measured with M0 defaults (22.05 kHz):
  - music: 43 tracks → 22.7 MB of Ogg Vorbis (q2, stereo). Streamed from SD one track
    at a time (stb_vorbis on device). **Accepted.**
  - SFX: 254 clips → 27.9 MB as mono 16-bit PCM. That is too much to keep resident in
    128 MB alongside Mono + textures. **Open:** IMA-ADPCM (≈ 7 MB, cheap decode) vs.
    load-on-demand LRU cache. Decide in M3 with real memory numbers.

## D7 — Timing (open)

- Game uses a fixed timestep of 0.02 s (50 Hz physics). Keep 50 Hz first for
  fidelity, render target 30 fps, measure on device in M4 before changing either.

## D8 — Develop on desktop first (accepted)

The replacement engine is built and debugged on desktop Linux (Mono + SDL2, same 320×240
renderer) and compared frame-by-frame against the real game. The device build swaps
only the platform layer (framebuffer/MI_GFX blit, audio out, buttons).

## D9 — Shape of our UnityEngine.dll (accepted, M1)

- **Hand-written** (`engine/UnityEngine/Core`, `Runtime`): Unity API semantics we implement
  (object model with destroyed-equals-null, Transform hierarchy, math types, curves/gradients,
  coroutines, invokes, SendMessage) plus the KNL runtime (JSON scene loader, field
  deserializer, render system, P/Invoke to `knlcore`).
- **Generated at build time** (`tools/gen_shim.py`, git-ignored output): declarations for every
  Unity type the game touches (kinds, bases, enums with values, delegates, interfaces) and a
  `NotImplementedException("Type::Member")` stub for every referenced member the hand-written
  code does not provide yet. Two-stage build: compile hand-written → read that DLL's metadata →
  stub only what is missing. Generated from the user's own DLLs, so nothing Unity-derived is
  committed.
- Effect: the game's assemblies always JIT-bind; an unimplemented API surfaces as a named
  exception, and `run.sh --smoke ...` ranks them. M2 works down that list.
- Game MonoBehaviours are created with their real constructors (field initialisers run), then
  serialized fields are applied by name. Static constructors that hit a stub poison their type
  for the session, so anything used from a static initializer (e.g. `Animator.StringToHash`,
  CRC32 like Unity) must be hand-written early.

## D10 — M1 render scope (accepted)

- Sprites: drawn from per-sprite PNGs exported by `convert.py` (atlas cut-outs, mesh-masked),
  one affine nearest-neighbour blit each; sorted by order-in-layer, then far-to-near along the
  camera axis (single sorting layer `Default` in this game). 1 world unit = 32 px at
  orthographic size 3.75 → sprites are pixel-exact at scale 1.
- Not yet drawn: gradient meshes (sky/haze/fill/water) and celestial bodies — their colours and
  positions are produced at runtime by game scripts (`HorizontalGradient` →
  `BlenderColorReference`, `CelestialBody`, `Parallax2D`) and come with M2's update loop.
  Custom shaders (recolour/ambient/wind) are also M2+; M1 draws unlit with SpriteRenderer tint.
- Desktop cost today ≈ 4–6 ms/frame (C# traversal + software blits). The A7 is roughly an
  order of magnitude slower, so M5 must add cached world matrices, culling by layer, indexed
  8-bit blits and NEON before 30 fps is realistic.

## D11 — Rewired is replaced, not run (accepted, M2)

The shipped `Rewired_Core.dll` refuses to initialise outside its supported platforms
("Platform Unknown") and needs native helpers (`Rewired_Windows_Lib.dll`). We build our own
`Rewired_Core.dll` the same way as `UnityEngine.dll`: hand-written core in `engine/Rewired/`
(`ReInput`, `Player`, `InputManager_Base`, controllers) plus generated logging stubs
(`gen_shim.py --target Rewired_Core --hand Rewired`). Actions are read from the game's own
`InputManager` user data (12 actions) and mapped by name to the device's buttons:

| Action | Buttons (device / desktop key) |
|---|---|
| Horizontal | D-pad ←/→ (arrows, A/D) |
| Vertical | D-pad ↑/↓ |
| Pay | D-pad ↓ or X (S/↓, X) |
| Gallop | B (Shift, C) |
| Submit | A (Enter, Space) |
| Start | Start (Esc) |
| Cancel | B or Select (Tab) |

## D12 — Unity lifecycle semantics we reproduce (accepted, M2)

- Script execution order from each MonoScript's `m_ExecutionOrder` (ScreenManager/InputManager
  −32000, Managers −600, Level −500, Director −110, Game −60, MainCamera 100, Water 200 …),
  then creation order.
- Awake/OnEnable on activation, Start before the first Update, FixedUpdate at 0.02 s,
  Update → Invoke → coroutines → Animator → LateUpdate, destroys flushed at end of frame.
- Coroutines: `WaitForSeconds`, `WaitForSecondsRealtime`, `AsyncOperation`,
  `CustomYieldInstruction`, end-of-frame. Async scene loads honour `allowSceneActivation`.
- `Instantiate` deep-copies serialized fields and remaps references inside the cloned
  hierarchy; `MeshFilter.mesh` gives each filter its own copy; `Resources.FindObjectsOfTypeAll`
  includes inactive objects (island generation depends on it).
- Serialized `AnimationCurve` wrap modes use Unity's internal enum (0 ping-pong, 1 repeat,
  2 clamp) — not `WrapMode`. Getting this wrong made every default curve loop (camera lead
  oscillated).

## D13 — Physics and animation subset (accepted, M2)

- **2D physics:** our own, not Box2D yet. Box/circle/polygon/edge colliders, dynamic and
  kinematic bodies, gravity (0, −15), layer collision matrix from `Physics2DSettings`,
  x-axis sweep-and-prune (the world is a long thin strip), trigger and collision
  Enter/Stay/Exit messages, raycasts/overlaps. Enough for the game's gameplay triggers
  (e.g. `TriggerGameStart` starts the clock, tutorial walls, pickups). Revisit if behaviour
  differs from the real game in playtests.
- **Animator:** the build contains only compiled data, so we decode it: AnimationClips as
  MuscleClip streamed/dense/constant curves, generic bindings by CRC32 path/attribute hash
  (sprite swaps via `pptrCurveMapping`, transforms, floats, MonoBehaviour fields); the
  AnimatorController's compiled state machine (states, transitions, conditions, triggers)
  with `Animator.StringToHash` = CRC32. Animation events are dispatched.

## D14 — Rendering additions (accepted, M2)

- Gradient meshes (sky/haze/fill) drawn as vertex-coloured quads × material `_Color`.
- Water: a reflection pass mirrors the rows above the waterline with the game's
  `reflectionScale` and tint, plus a small wave offset. PostFX passes are skipped.
- Off-screen enemy indicators (D3) implemented as an overlay: purple (0.56, 0.25, 0.66)
  7-row chevron on the edge, pulsing when more than one enemy is on that side.
- **Not yet:** the ambient/recolour shaders that darken the world at night (sky and moon are
  correct, sprites stay day-lit), UI/text drawing, particles. M3.

## D15 — Stubs record before they throw (accepted, M2)

Generated stubs are `throw KNL.Log.Missing("Type::Member")`: the hit is counted before the
exception, so calls the game swallows in its own `try/catch` still appear in the `needs:`
list (this found `Matrix4x4.SetTRS`, hit 41× per session and silently ignored).

## D16 — Sprite shading: PowerSprite2 per sprite, vertex lighting per corner (accepted, M3)

Only shader work that matters for play was done (user brief: "most needed for game mechanics first").
The game's sprite shader `Custom/PowerSprite2` was read from its shipped Direct3D 9 bytecode
(`tools/sm3dis.py`, a small SM2/SM3 disassembler) and reimplemented:

- `out = tex·color·(1 − overlay.a)·lerp(1, ambient + Σ fakeLight, _AmbientResponse) + overlay.rgb·overlay.a`
- ambient = `RenderSettings.ambientSkyColor` (flat ambient mode, gamma space) — the day/night darkness;
  fake lights = `LightFaker`'s `_Light_PositionArray`: `(1 − min(d/3, 1))·intensity·(0.49, 0.425, 0.34)` —
  the warm pools around torches and fires that show where it is safe at night.
- `_Overlay` = SpriteFX hit/selection flash; recolour keys live in texture alpha
  (≥ 0.875 keep, ≥ 0.75 red·RecolorA, ≥ 0.5 red·RecolorB, below discarded) — skin and banner colours.
- Lighting is evaluated at the 4 sprite corners and interpolated (like the GPU's per-vertex lighting),
  one multiply-add per pixel; no per-pixel light loops.
- `Renderer.material` now returns a per-renderer copy (Unity semantics) so flashes don't leak.

Not done (visual only): wind/wave vertex animation, sparkle, font wobble/shine, PostFX, bilinear glow
filtering, particles. Revisit in M5 if the device has headroom.

## D17 — UI: the game's own UnityEngine.UI.dll, our CanvasRenderer/TextGenerator (accepted, M3)

`UnityEngine.UI.dll` (Unity's managed UI, shipped with the game) runs unmodified. We provide its native
side: RectTransform layout (anchors/pivot/parent rect, localPosition as source of truth),
`Canvas.willRenderCanvases`, `CanvasRenderer` (mesh/colour/clip capture), `TextGenerator` for the
game's baked bitmap fonts, `Sprites.DataUtility`, Animator bindings for UI (`m_Sprite`, `m_Color.r`,
RectTransform properties + `OnDidApplyAnimationProperties`), UnityEvent persistent calls (buttons).
Canvas meshes are drawn as UV-mapped quads: world-space canvases through the camera, screen-space
canvases mapped onto 320×240 with the CanvasScaler's factor. UI sprites draw from their cut-outs.

Glyphs: `KNL_CONTROLS=gamepad` (device default) makes Rewired report one joystick and hides the D-pad
from keyboard queries, so tutorial hints show stick/controller glyphs instead of keyboard keys.

## D18 — Audio (accepted, M3; supersedes the open part of D6)

Native mixer (`native/knlaudio.c`): 32 voices, 22050 Hz stereo s16 out (SDL audio on desktop),
linear resampling for pitch, equal-power pan, soft knee above −2.5 dBFS (only samples that would clip).
Music and long loops: Vorbis streamed with stb_vorbis. SFX: IMA ADPCM kept in RAM (~7 MB), decoded
block-wise (1024 samples) inside the mixer; silent voices only advance.
Managed side reproduces the game's AudioMixers from their compiled data: groups (parent chain),
snapshots + `TransitionTo`, exposed parameters (`SetFloat` names hash with CRC32), nested mixers;
3D sources use the custom rolloff curve on distance/maxDistance from the AudioListener (a child of
the camera at the play plane) and pan by horizontal angle. `--wav` captures the mix offline.

## D19 — Device data pack (accepted, M3)

`tools/pack.py` turns `extracted/` into `pack/kingdom.knlpack` (+ `pack/music/*.ogg`):

| content | format | size (this build) |
|---|---|---|
| objects | binary tree per serialized file, string table, decoded lazily per object | 27 MB JSON → 9.5 MB |
| sprites | 8-bit indexed, one RGBA palette per sprite (2 of 2,205 need RGBA) | 22 MB RGBA → 8.5 MB |
| UI textures | RGBA (fonts, logos, map art; sprite atlases dropped) | ~11 MB |
| SFX | IMA ADPCM, 1024-sample blocks | 28 MB PCM → ~7 MB |
| music | Vorbis files, streamed | 22 MB (unchanged) |

Why lazy objects: keeping parsed JSON trees cost 226 MB of managed heap; the live game itself is
~29 MB. With the pack the desktop run sits at 38 MB managed (was 256 MB) and boots in 0.7 s (was 2.0 s);
frames are pixel-identical to the JSON path. Textures load on first draw.

## D20 — Stage-2 stub generation reads stage 1 through Mono (accepted, M3)

dnfile 0.18 mis-sizes metadata coded indexes once an assembly passes 2048 MethodDefs (our
hand-written UnityEngine.dll did in M3), which silently produced a stub-less DLL. `gen_shim.py` now
lists stage-1 members with `tools/stage1dump.cs` (compiled with mcs, run with mono; same key format);
dnfile remains the fallback. `build.sh` aborts if stage 2 fails.

## D21 — Device runtime: stock Mono 6.8 armhf (JIT), own fb/evdev/MI_AO layer, no SDL (accepted, M4)

Device survey (Miyoo Mini Plus, OnionOS 4.3.1-1): 2× Cortex-A7 (NEON, VFPv4, idiva) at 1.2 GHz
(`ondemand`; Onion's `PerformanceMode` switches to `performance`), 101 MB RAM visible (≈ 74 MB free
with MainUI running) + Onion's 128 MB swap file on the SD card, kernel 4.9.84, **glibc 2.28** (built
with ARM GNU Toolchain 8.2-2018-08). `/dev/fb0` 640×480×32, virtual 640×960 (two pages); the panel
is mounted upside down, so frames are written rotated 180°. Buttons: one evdev device
(`/dev/input/event0`, `gpio-keys`): ↑103 ↓108 ←105 →106, A 57 (SPACE), B 29 (LCTRL), X 42 (LSHIFT),
Y 56 (LALT), L1 18, R1 20, L2 15, R2 14, Select 97 (RCTRL), Start 28 (ENTER), Menu 1 (ESC), power 116,
vol −/+ 114/115 (matches the device's `KEY=` capability bitmap). SigmaStar MI libraries
(`libmi_sys/ao/gfx…`) live in `/config/lib`; Onion ships SDL 1.2 only (SDL2 copies exist per app).
No `scp` on the device: copy with `tar | ssh`.

**Mono.** Other Miyoo ports don't help: the Stardew Valley port keeps its device runtime private.
Building Mono for armhf ourselves is not needed: mono-project.com publishes **Mono 6.8.0.105 for
Debian 10 (buster) armhf** (`stable-buster/snapshots/6.8.0.105`, `0xamarin3+debian10b1`), the same
version as the desktop and the same glibc as the device. We take `mono-sgen` (runtime statically
linked, 3 MB), `libmono-native.so` and only the BCL the game loads (mscorlib, System, System.Core,
System.Xml, System.Configuration, Mono.Security ≈ 9 MB). `libmono-native.so` links
`libgssapi_krb5.so.2`, which Onion lacks; a 5 KB stub with the same symbol version (all calls fail)
satisfies the loader (`engine/miyoo/gssapi_stub.c`). The managed DLLs are the desktop build, byte for
byte. JIT for now: first headless boot 8.5 s to the loader scene, 29 MB managed, 60 MB RSS.
Full AOT (D1) moves to M5 together with the renderer work, measured against these numbers.

**Platform layer** (`knlcore.c` / `knlaudio.c`, `-DKNL_MIYOO`): no SDL on the device. The Mini SDL2
build (steward-fu) pulls in a 22 MB `libGLESv2.so` and its own input handling, too much for
128 MB when we only need a blit. Instead: 2× blit straight into the fb back page + `FBIOPAN_DISPLAY`;
buttons read from evdev (non-blocking, state kept across reads); audio: a mixer thread feeding
`MI_AO_SendFrame` (22050 Hz stereo, 1024-frame periods, blocking), linked against the device's own
`libmi_ao`/`libmi_sys`. Menu + Start quits to Onion. Build: `engine/build_miyoo.sh [--push]`
(ARM GNU Toolchain 8.2-2018-08, gcc 8.2.1, `-mcpu=cortex-a7 -mfpu=neon-vfpv4 -mfloat-abi=hard -marm`;
MI headers from steward-fu/sdl2 `mini/inc` at commit 0631abc).

**Layout** (copy `Roms/` to the SD card root): `Roms/PORTS/Games/KingdomNewLands/` with `knl.sh`,
`mono/{bin,lib,etc}`, `lib/` (libknlcore.so, gssapi stub), `managed/` (our engine + your DLLs),
`data/` (your pack), `save/`, `logs/knl.log`; shortcut `Roms/PORTS/Shortcuts/Strategy/Kingdom New
Lands.port` (Onion's standalone template: stops Onion's audioserver, performance governor).
`managed/` and `data/` are built from your game files: never share them.

## D22 — M5 performance work: measured, exact, two cores (in progress, M5)

Method: `KNL_PROF=1` prints per-phase ms/frame, calls and KB allocated per frame plus GC counts
(`KNL_PROF=2` adds every script type's Update/LateUpdate/FixedUpdate and physics sub-phases). Every
change is checked against a deterministic desktop run (fresh save folder, scripted input, PNG every
200 frames): frames must stay byte-identical unless the change is a bug fix.

Measured on the device at the start of M5 (stock 1200 MHz, JIT, headless 600 frames): 201 ms/frame
(render 128, logic 73). Managed code ran ~70× slower than on the desktop PC, C code ~30×, so both
the managed engine and the blitters needed work. `-O=float32` made no difference.

Changes (all exact; output unchanged):
- **Transform world matrices cached.** Local fields are written directly in many places, so the cache
  is validated by comparing the local values, parent reference and parent stamp, and recomputed with
  the same expression on any difference. The inverse is cached too (it allocated two arrays).
- **Blitters:** `x/255` via an exact shift form (checked for every input by `knl_selftest`);
  unrotated sprites (most of the game) get per-column texel tables built with the same float
  expressions, so the inner loop has no float math; gradient quads precompute per-column edge colours.
- **Rasteriser on the second core (Miyoo):** draw calls are recorded into a command list and
  executed by a worker thread at present time while the game thread runs the next frame
  (`KNL_SYNC=1` turns it off). Screenshots flush the list first.
- **Fewer allocations** (GC pauses are long on the A7): per-message behaviour snapshots instead of
  three `ToArray()` per frame, a candidate list for `Start`, Unity messages called through cached
  open-instance delegates instead of `MethodInfo.Invoke`, pooled physics entries, small sort keys.
- **activeInHierarchy memoised per pass** (physics gather, render collect).

Behaviour change: **frame step cap.** At low frame rates the 50 Hz fixed step ran up to 5 physics
steps per frame (≈ 15 ms each on the device), which made slow frames slower. The device launcher
passes `--max-dt 0.05`: below 20 fps the game runs slower than real time instead (desktop default
stays 0.1 s). Revisit once the device holds 20+ fps.

Bug fixed on the way: `Transform.Find` / `GameObject.Find` did not skip empty path segments
(Unity does). Saved objects are re-parented by paths like `Level/GameLayer/`, so a continued game
put them outside their layer (wrong draw order, the king hidden behind scenery) and the next save
threw a NullReferenceException.

## D23 — Compatibility repairs must preserve original data and mechanisms (accepted, rc4)

Restore typed UI.Text font-layout metadata rather than override individual broken labels. Existing packs may use a source-verified, object-hash-bound repair sidecar; fresh conversions retain layouts inline. Bitmap glyph metrics stay in baked pixels and UI.Text applies its pixelsPerUnit division once.

Honor camera culling masks across main/interface/reflection passes and compute MeshRenderer bounds from transformed vertices. Read original Damageable death state for threat indicators, separately from active/fading sprite state.

Catapult chassis displacement was a missing free-wheel hinge constraint, not a missing image. Import explicit wheel anchors/connected bodies, preserve independent world poses of parented rigidbodies, solve support and axle contacts together, and suppress collisions between connected bodies when configured. This is a bounded hinge subset with the same approximate inertia as the existing adapter, not full Unity 2D physics. Unsupported active motor/limit/automatic-anchor use is logged.

Exact performance changes preserve pair/query order, same-frame query semantics and hierarchy invalidation. Test later-island copies and report fixed-timestep diagnostic throughput separately from normal gameplay. See `docs/VALIDATION-1.1.0-rc4.md` for measured gains and unresolved reports; the project remains experimental.
