# rc6 combat and first-launch validation

Status: experimental candidate. Full gameplay and a natural winter playthrough remain pending. No 30 FPS crowded-combat claim is made.

## Combat cost and exact optimizations

The original Troll FixedUpdate uses an upward collider cast with infinite distance and then ancestor Damageable lookups. In a controlled winter horde, that method consumed 29.60 ms/frame before the cast change. The replacement now hoists direction/layer checks, uses collision-mask candidates, rejects impossible vertical slabs and reuses hit storage. It preserves the same intersection calculation and sort/tie behavior. Local hierarchy/body cache invalidation avoids invalidating unrelated island colliders when pooled actors change. Generic component lookup preserves the existing Type API's ordering and destroyed-first behavior.

The original 50 Hz physics step, forces, arrows, world coins, unit coin health, siege AI and solver iterations remain unchanged. A spatial-grid prototype was slower on the Miyoo and was removed. Exact returned hit IDs/order, distance, points and normals match rc5 across 2,064 differential casts, including same-frame movement, changing layers/triggers, infinite/near-vertical directions, truncation and extreme coordinates. Warm casts allocate no managed bytes. Another 3,200 randomized component/body lookups match an independent Type API oracle.

## Stock-clock controlled comparisons

All measurements use maximum 1200 MHz, performance governor during the test, an isolated immutable copied save, fixed seed, muted audio, framebuffer window, profiler 0 and 1,800 frames. The render timestep is fixed to 1/30 second, while physics remains 50 Hz. These control simulation work for causal comparisons; ordinary real-time FPS can differ. Screenshots were taken at identical frames. Boot frames are excluded below.

| Scenario and window | rc5 FPS | rc6 FPS | Change |
| --- | ---: | ---: | ---: |
| Winter horde, 601–900 | 10.48 | 13.73 | +31.0% |
| Winter horde, 901–1200 | 14.15 | 18.70 | +32.2% |
| Winter horde, 1201–1500 | 19.14 | 19.86 | +3.8% |
| Winter horde, 1501–1800 | 22.89 | 22.88 | effectively unchanged |
| Portal assault, 601–900 | 15.85 | 16.97 | +7.1% |
| Portal assault, 901–1200 | 18.50 | 19.64 | +6.2% |

The winter fixture uses the original winter state and red moon, 79 original enemies simultaneously at a wall, four knights and 16 archers. The portal fixture uses eight knights and 32 archers through original charge behavior. Only the isolated QA king is invulnerable to prevent game-over from invalidating the benchmark. Original AI, damage and physics run normally. Setup uses original game APIs plus private reflection-based positioning; no fixture code enters the player host.

All 20 sampled combat rows per scenario match rc5 exactly: enemy/army/arrow/coin counts, unit coin balances, hits/damage, pickups, portal HP, weather and exceptions. Friendly hits include wall hits. There are zero exceptions. The wall holds in the winter fixture. A separate open-ground coin-health fixture uses two knights, eight archers and 60 enemies positioned on both sides. Its 20 sampled states also match rc5 exactly: unit coins fall from 14 to 11, six pickups occur, and 346 hits are recorded by the final sample, with zero exceptions. This exercises natural damage-driven coin loss and recovery through the original game logic. These tests do not establish exact original Unity physics parity. Original output files, screenshots and saves remain private.

Raw aggregate results: `evidence/rc6-device-stress.csv`, `evidence/rc6-combat-regression.json`. The performance workbook retains the earlier ordinary-scene budget and adds a separate combat sheet. Profile-2 scope timings are labeled separately from profiler-off FPS and inclusive scopes must not be added together.

## Ordinary real-time timing

The same stock clock, muted output and profiler 0 were also tested with ordinary elapsed time and `--max-dt 0.05`. There were zero exceptions in each run. These observations use different simulated histories from the fixed-step comparisons above and must not be treated as a direct causal before/after comparison.

| Scenario | Steady frame windows | Observed FPS |
| --- | --- | ---: |
| Fresh start | 301–1200 | 45.71–58.74 |
| 79-enemy winter horde | 601–1800 | 11.13–19.25 |
| 8 knights / 32 archers at a portal | 601–1800 | 12.99–17.75 |

The fresh start exceeds the 30 FPS target in this muted test. Heavy combat remains below it. Real-time timing can run up to 2.5 physics steps per slow frame, whereas the fixed-step comparison averages 1.67; lowering simulation frequency merely to inflate FPS would change the mechanics budget. Audio-enabled performance remains unmeasured. Individual windows are in `evidence/rc6-realtime-throughput.csv` and the workbook.

## Installation

The full first-launch converter is implemented, with progress/error UI, original-build verification, metadata-only schema reconstruction, graphics/text/audio conversion, hashed checkpoints and recoverable activation. The interrupted/resumed device import completed successfully and preserved saves. A deliberately corrupted checkpoint was rebuilt. The fresh extracted-package route passed through Onion’s real launcher in 737.92 seconds at stock 1200 MHz. Peak RSS was 81,924 KiB; swap at completion was 36,220 KiB, with Onion swap enabled. It automatically booted 1,200 game frames with zero exceptions, 30 audio plays and no missing clips, preserving a save sentinel. Audio output was muted. Full figures and limits are in `ON_DEVICE_IMPORT.md` and `evidence/rc6-native-import-device.json`.

## Automated checks

The rebuilt engine passes all 19 runtime/native regression scripts, including pooling, weather, UI/day HUD, hinge support, physics, transforms, visibility and cast/component equivalence. Thirteen Python preparer tests include deterministic public packaging, PC/native installation interoperability and failed-commit rollback. Twenty-six synthetic native-import checks cover recovery/locks and resource-range streaming. The porting skill passes its six tool regressions and frontmatter validation.

## Remaining limits

Crowded combat remains below 30 FPS. Audio-enabled FPS is not established by the muted benchmark. A complete winter economy, long-session progression, save/reload and all late-island behaviors still require playtesting. Existing hinge/contact approximations and catapult post-arrival drift remain open. The moving ground-tile contrast report is deferred behind mechanics/performance. No external release was published by this work.
