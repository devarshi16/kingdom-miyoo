# Local candidate validation — 2026-11-02

Status: experimental; human gameplay acceptance remains pending. Nothing was pushed or published.

## Candidate and device

- Runtime source: `8175e9ec310ae8316434bdd1793ddff371c76ade` on `codex/stabilize-port`.
- Archive: `KingdomNewLands-MiyooMini-1.1.0-rc2.zip`; SHA-256 `c6b2a2c6d6b07b88b6e1b86fbb92b23e9954f965954ec26c740390f91c1ff424`.
- Owned input: Kingdom: New Lands 1.2.8, Unity 2017.1.2p1. Other game builds remain unverified.
- Target: Miyoo Mini Plus, Onion 4.3.1-1, stock 1.2 GHz performance mode while playing; 320×240 source, exact 2× display copy; audio enabled.
- Installed separately as **Kingdom New Lands Test**. QA used separate save directories. Production installation was untouched. Complete staged replacements verified runtime and copied-save hashes before directory activation, with previous installations retained as backups.

## Changes exercised

Indexed sprite lighting/tint palettes, vertical gradients and axis-aligned UI use exact fast paths. Transform/activity caches, matrix multiplication, planar rotation and image-bound culling reduce managed work. The final display conversion prepares a cached row and copies complete contiguous rows to mapped display memory; rotation, color channels, alpha and scale remain unchanged.

The pouch uses original serialized coin polygons, concave walls, triggers, layer masks, mass, gravity, damping, materials and motion limits with a dedicated Box2D 2.3.1 compatibility solver. Game coin spawning and overflow scripts remain in control. The vendored solver has a marked integration change to apply original per-second motion caps during integration. This is not proof of bit-identical behavior to the original Unity engine.

## Automated evidence

- Managed runtime: 34 assertions passed, including transform/cache invalidation, clone/reparent behavior, matrix equivalence, planar rotations, thin polygon contacts and coroutine/clock/shutdown regressions.
- Renderer: 3,000 full-frame pixel comparisons passed against reference paths, including translucent palettes, recoloring, cached shading, clipping/flips, UI and gradients.
- Display conversion: 2,880 comparisons cover channel layouts, source dimensions, alpha modes and padded row strides; all also passed on the target ARM CPU.
- Pouch: six native tests passed: concave opening/floor, sensors, 32-bit masks, pooled disable/reactivation, mass restoration and angular-speed integration cap.
- Preparation: eight tests passed, including save preservation and failed replacement rollback. Desktop and ARM builds succeeded.
- Culling: deterministic desktop PNGs before/after the culling change were byte-identical at three camera locations.
- Thirty injected coins remained in the bag through a 1,800-frame desktop run and a 1,200-frame real-clock device run with movement; no logged exceptions. Injecting 120 coins still produced natural overflow (47 remained after 1,800 desktop frames). These numbers are observations, not an invented or validated original capacity.

## Performance evidence and limits

A controlled 900-frame fixed-timestep comparison reduced the final 300-frame mean from 71.6 ms (about 14 FPS) in rc1 to 33.8 ms (about 29.6 FPS) in an intermediate optimized build. Fixed simulation timing isolates costs; it does not establish real-time gameplay FPS. Native replay of one captured 429-command frame reduced desktop raster time from 2.742 to 0.572 ms with the same image hash. Captures contain owned pixels and are excluded from distribution.

Real-clock runs use a fresh save, profiling disabled, 1,200 frames and scripted right movement at frames 500–560 followed by down at 650–656. Each FPS value covers 300 frames. First-window values include blocking scene loading and are not steady-state FPS. Fresh games can generate different terrain, so separate fresh-game runs are not pixel-identical controlled comparisons.

| Build / workload | Frames 301–600 | Frames 601–900 | Frames 901–1200 |
| --- | ---: | ---: | ---: |
| Original display copy, ordinary start | 26.61 | 26.93 | 26.56 |
| Original display copy, 30 injected coins | 24.04 | 26.83 | 28.57 |
| Contiguous display copy, ordinary start | 28.99 | 26.02 | 27.62 |

The coin stress run also captured a frame and screenshot in earlier windows; its final window excludes that work. It reported zero exceptions, 62 audio plays with zero missing clips, 47.7 MB managed after GC and 70.9 MB RSS. Ordinary-start runs disable captures and screenshots. Physics remains at the original 50 Hz; no new frame-step cap, reduced rendering resolution or overclock was introduced. The preexisting launcher `--max-dt 0.05` remains.

## Final verification and remaining acceptance

The final ordinary-start run completed 1,200 frames with zero logged exceptions, 32 audio plays and no missing clips; after GC it reported 47.5 MB managed and 70.3 MB RSS. Observed steady-window FPS was 26.02–28.99, so sustained 30 FPS remains unachieved. The first 300-frame window including loading was 12.31 FPS.

The final ZIP passed CRC and every manifest SHA-256 check. Its 33 payload files exclude owned game assemblies, pack and music. All four installed runtime binaries match the packaged hashes. Test/save matches its pre-deployment inventory (the test's default save directory was empty; QA used separate directories). MainUI and audioserver returned, no game process remained, the governor returned to `ondemand`, and temporary service-retention/launch overrides were absent. The previous complete test installation remains available as a backup.

Follow [TESTING.md](TESTING.md) tonight: compare the pouch lip/stacking and natural overflow directly with the owned original, complete the tutorial, navigate/save/reload, recruit and upgrade, then test progression and heavier scenes. Boot success and zero exceptions do not establish complete gameplay correctness. Windows/macOS setup and alternate game builds remain untested. The candidate must not be described as stable or as sustaining 30 FPS everywhere.

The related `miyoo-game-port` skill (branch `codex/repair-porting-skill`, commit `7146de3fe75d582c6905f7ed5853297e2297cd69`) was updated with profiling/pixel-equivalence requirements, physics fidelity and motion-limit lessons, and complete-runtime staging on unreliable Wi-Fi.
