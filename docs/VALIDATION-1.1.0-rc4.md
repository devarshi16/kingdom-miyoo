# 1.1.0-rc4 local validation — 2026-11-02

Experimental local candidate. Full gameplay validation is still pending. Do not describe this build as a stable release or recommend buying the game for the port.

## Reported issues

| Report | Change or current evidence |
|---|---|
| Catapult shows only wheels | Reproduced through original workshop payment and construction. Chassis fell to y=0.1 while wheels stayed at y=1.0. Implemented the shipped free-wheel hinge subset, connected-body import/cloning, independent world integration and joint collision exclusions. Chassis now remains near y=0.9. Both directions tested; original animation releases real projectiles. |
| Second island / forest FPS | Reduced broadphase sorting, static/static pair traversal and query allocations. Replaced global transform-cache invalidation with descendant invalidation. Stock-device measurements below. Thirty FPS on this island has **not** been achieved. |
| Purple arrow after enemy death | Indicator now reads the original Damageable.isDead state and component activation. Previously an active fading corpse remained eligible. Live transparent spawns remain eligible. |
| Torch streak below flame | Restored serialized main/interface/reflection camera masks and transformed mesh bounds. Reflection-only sprites no longer draw on land through the main camera. Actual torch screenshots checked in lite and full modes; hand flame retained. |
| Many ship repair logs | General physics changes apply to this workload. A 64-log/600-static-collider regression retained identical positions, velocities and query traces over 300 steps. The actual player repair-input sequence and its device FPS still need reproduction. |
| Pause menu day | Original Kingdom.day and Roman numeral formatter reused. |
| Save+Quit / Continue wrapping | Converter had stripped UI.Text layout dictionaries. Baked glyph metrics were also scaled twice. Both causes repaired; actual pause and map screenshots checked. |
| Sun behind coin counter | Original sun/day art appears beside the coin counter, including during gameplay. A small progress track reads Director.currentTime; it does not advance the game clock. |
| Enemies invisible in groups | **Open.** An eight-troll controlled probe found no opaque live sprite disappearing; one dead fading enemy was correctly transparent. This does not resolve the reported intermittent failure. |

## Data compatibility

Fresh conversions retain structured UI.Text.m_FontData while stripping binary font payloads. Existing prepared packs can use `tools/recover_ui_fontdata.py ORIGINAL_KINGDOM_DATA --data-dir PREPARED_DATA`; it requires the verified owned build and refuses to overwrite an existing output. Its locally generated `ui-fontdata.json` is bound to hashes of the exact packed object entries. The current device update uses this repair without replacing music, pack data or game assemblies. The sidecar is owned metadata and is excluded from the public archive.

`engine/build_miyoo.sh` copies an optional local sidecar with its matching pack. Rebuilding a fresh pack should not reuse a sidecar for a different pack.

## Stock-device diagnostic comparison

Miyoo Mini Plus, OnionOS 4.3.1-1, performance governor at stock 1200 MHz. Separate copied installation and copied second-island save, lite visuals/counter mode, 320×240 rendering, direct framebuffer display. No overclock.

The following runs use **fixed 1/30 simulation steps, muted audio and KNL_PROF=2** to compare the same simulation frames. These are diagnostic throughput measurements, not a claim about ordinary real-time gameplay FPS. First 300 frames include boot/loading and are excluded here. Input: right frames300–700, left750–1150.

| Window | Installed rc3 | Physics changes | Combined rc4 |
|---|---:|---:|---:|
| Frames301–600 | 18.59 | 20.79 | 22.34 |
| Frames601–900 | 17.68 | 20.04 | 21.50 |
| Frames901–1200 | 18.98 | 21.90 | 23.39 |

All three 1200-frame runs: zero logged lifecycle exceptions; 4602 objects / 8662 live scripts at completion. Last-window physics gather: 10.61 → 7.30 ms/frame; combined logic: 39.8 → 30.1 ms/frame. UI and rendering repairs intentionally change displayed pixels; the physics-only and transform-only equivalence checks were run separately.

A separate pair of runs used the normal real-time timestep with the launcher’s `--max-dt 0.05` cap, still muted and profiled. The same copied save and frame-based route produced:

| Window | Installed rc3 real-time | rc4 real-time |
|---|---:|---:|
| Frames301–600 | 11.50 | 17.09 |
| Frames601–900 | 12.48 | 19.91 |
| Frames901–1200 | 11.36 | 18.53 |

Both ran 1200 frames with zero logged lifecycle exceptions. Real-time runs advance the simulation according to elapsed/capped time, so their final object counts and simulated times differ; use the fixed-step runs for equivalence. These remain **muted, instrumented** measurements; audio-enabled ordinary gameplay and 30 FPS on the second island are unverified. The cap intentionally slows simulation when frames take longer than 50 ms.

A separate hardware construction/firing probe bought and completed both catapults, positioned them at their original valid firing borders, invoked original Ready/Fire behavior, and observed launched boulders with nonzero horizontal/upward velocity. It then called original Save+Quit successfully; 1001 frames, zero logged exceptions. Its camera overrides, screenshots and detailed inspection make its FPS unsuitable as a normal-game benchmark. Desktop reload of the generated copied save retained both supported catapults at y≈0.9, zero exceptions.

## Regression evidence

- 11 Python preparation/converter/package tests; archive excludes original DLLs, pack and recovered metadata.
- 34 runtime checks; 8 physics-performance checks, including same-frame point queries and dense trigger/pair ordering.
- 6 transform cases containing 6000 randomized mutation/read comparisons against fresh matrix composition; inverse, clone and reparent coverage.
- 9 hinge checks: gravity support, axle attachment, independent rotation, pushed movement, cloned connected bodies, disabled and world-anchor behavior.
- 9 enemy-indicator checks; 10 mesh-bounds/camera checks; 16 UI checks.
- Existing counter/overflow/visual/native/occlusion suites pass: 28 counter + 8 physical-mode + 26 overflow + 7 HUD + 18 visual + 3000 renderer + 2880 framebuffer + 6 pouch + 1000 occlusion comparisons.
- Dense log regression before/after trace SHA-256: `c22863033c14d05a025b44dc862c2ec1f51128c30b8e7c423d696b0b615deb1b`.
- Transform-only real-save 1800-frame PNG before/after SHA-256: `10bd8ec55d69414b17c1ce20a0e7d2039f65dac2da1568606b1074a40a1e0a1d`.

Private logs, probe sources, saves and screenshots are retained under the local `Recompilation/work/knl-gameplay-issues/` directory; no owned data is included in this source change. Original camera/prefab evidence is indexed in `docs/evidence/render-camera-findings.json`.

## Remaining limits

The engine still implements a subset of Unity physics/rendering. Hinge motors, angle limits, break thresholds and automatic connected-anchor configuration are not implemented; the active shipped catapult wheel joints do not use them. The disabled motor test joint is ignored. Full water rendering remains an approximation. Long gameplay, clustered enemy visibility, actual repair bursts, later islands and ordinary audio-enabled frame rates remain release gates.
