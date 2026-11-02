# 1.1.0-rc5 validation

Experimental local candidate. Full gameplay, winter economy, long-session stability and all reported visibility failures are not certified.

## Repairs and limits

| Report | Change and evidence | Remaining limit |
| --- | --- | --- |
| Enemy bodies sometimes disappear, masks remain visible | A revived pooled Troll retained its terminal `Die` state at normalized time above four. The body was opaque/enabled but selected a 32×5 death sprite with only 43 opaque pixels. Original Unity 2017 clears this state on GameObject deactivation. The adapter now clears evaluated Animator state after disable callbacks and discards a cloned Animator's source runtime. The same actor's death/reuse comparison returns to a normal 16×23 body and recreated mask after the first animator step, through 20 cycles. | This is a proven compatibility defect and targeted repair. It does not establish that every rare invisible-body report has this cause. Crowd/world occlusion can completely hide actors in both runtimes. Component-only Animator disable behavior was kept separate. |
| Day number disappears after day one | Persistent glyph generation copied the original announcement's faded alpha. A local settings copy now forces opacity while preserving tint, original Roman formatting and authoritative day/time. Ten focused assertions pass; later-day copied-save captures show the label beside the clock. | Longer playthrough and later islands remain player validation. |
| Low FPS | Reuse bounded collider-mask candidate lists, retain exact query/pair ordering, reduce redundant gather writes and closed-type body lookup, cache exact sprite bounds, compile live particle-consumer readers and cache process visual configuration. Lite cloud budgets are now 4 cumulus / 1 stratus. Lite point lights use one image-center sample instead of four corner samples. Hidden gradient pixels beneath opaque water are clipped exactly. | Physics, input, AI, grass/economy and the original fixed-step rate remain active. Lite point lighting intentionally simplifies spatial gradients; full mode retains four-corner lighting. Per-feature FPS gains have not been isolated. |
| Rain, snow and water look absent/flat | Up to 40 rain strokes, 20 snowflakes and 24 faint water glints. Original Blender rain/snow intensity, the >0.1 precipitation threshold, wind and scaled time drive presentation. It draws below interface/UI, independently of the wallet display, without consuming game RNG or modifying weather/season state. | Forced rain/snow captures validate rendering, not winter gameplay. Water has no reflections. `KNL_DETAILS=off` is a developer comparison switch. |
| Catapult moves away from a wall | Original AI and worker assignment were not rewritten. Actual workshop payment/construction followed by natural pushing was traced for 4,201 frames in a copied save. Both sides reached the original strict threshold `Kingdom.border[side] - side*2.5`. The right target wall was at x=15.5; the nearer inner wall at x=8.5 was not its destination. | The approximate joint/contact solver still allows slow chassis drift after arrival. No paired original dynamic-physics comparison establishes full catapult parity. This remains open. |
| Installation is convoluted | Native C prototype verifies nine serialized-file fingerprints and indexes all 16,880 objects on the Miyoo in 0.506 s, peak RSS 1,288 KiB. Output matches the host index byte-for-byte. Progress/error messages are explicit. | Only verification/indexing exists. Texture/audio/font decoding, pack generation, an on-screen first-launch progress/error interface and interrupted-import recovery are still required. The normal launcher continues to require the desktop preparer. |

Details: [enemy evidence](evidence/enemy-visibility-20261102.json), [catapult rule](evidence/catapult-position-findings.json), [natural pushing trace](evidence/catapult-natural-placement-port.json), [visual mode](LITE-VISUALS.md), [native import](ON_DEVICE_IMPORT.md).

## Stock-speed measurements

Miyoo Mini Plus, Onion, maximum 1200 MHz, performance governor while the game runs, no overclock. Normal real-time frame clock, launcher's `--max-dt 0.05`, `KNL_PROF=0`, audio muted, same saved-state copy and scripted input route. The first 300 frames include loading and are excluded from steady-state claims. Windows are 300 rendered frames each. Real-time runs can follow different simulation trajectories when their frame rates differ; these are controlled workload comparisons, not pixel-identical simulation traces.

| Scene | Window ending at frame | rc4 FPS | rc5 final FPS |
| --- | ---: | ---: | ---: |
| Copied second island | 600 | 22.16 | 25.20 |
| Copied second island | 900 | 22.52 | 25.77 |
| Copied second island | 1200 | 25.59 | 29.06 |
| Fresh start | 600 | 36.17 | 36.71 |
| Fresh start | 900 | 38.26 | 47.61 |
| Fresh start | 1200 | 36.89 | 44.23 |

All baseline and candidate runs completed 1,200 frames with zero runtime exceptions. Second-island values use the final pooled-animation repair build; fresh-start rc5 values were captured immediately before that repair. An earlier second-island rc5 pass measured 26.60 / 27.24 / 29.13 FPS, illustrating run-to-run variation. They are not audio-enabled results or a guarantee of 30 FPS in dense combat. A sound-enabled benchmark attempt failed device audio initialization and ran headless; it was excluded from these comparisons.

The [workbook](PERFORMANCE-BUDGET.xlsx) ranks measured exclusive CPU phases and records their usefulness to game mechanics. Its older Prof2 phase timings locate costs; profiling overhead and overlapping wrappers make those timings unsuitable as ordinary FPS claims. It explicitly distinguishes a combined gain from an isolated feature gain. [CSV measurements](evidence/rc5-device-throughput.csv) preserve the window data and run conditions.

## Checks

- 11 Animator pooling lifecycle/clone checks, plus the original-game paired death/reuse trial. On the Miyoo, a 1,600-frame diagnostic reacquired the exact actor after original damage/fade/despawn and passed 20 reuse cycles: normal body and enabled original mask after each first animator step, zero runtime exceptions.
- 13 physics candidate/query checks, 6 component lookup and 6 sprite-bound checks. Independent saved-game physics traces and screenshots match before/after the exact changes.
- 35 sprite-lighting assertions against independent calculations and reference pixels, 21 visual-policy checks, 17 weather checks, 10 persistent-label and 5 real InputField keyboard checks.
- Existing runtime, hinges, UI layout, enemy indicators, transform, counter/overflow and native renderer/framebuffer/pouch regressions pass. Native opaque-fill clipping has 1,000 randomized comparisons, including hidden gradients, with identical output.
- 11 existing Python tests and 8 native-index fixtures pass. ARM build succeeds. No original DLL, asset, pack, save or capture is included in tracked release payloads.
- Copied-save forced rain then snow ran 1,200 frames on the Miyoo with zero exceptions. Source values read as 40 drops / 20 flakes. Rain, snow, day label and water captures were visually inspected. This fixed-step Prof2 diagnostic is not an ordinary-play FPS comparison.

## Device installation

Promoted to the normal `Kingdom New Lands` shortcut after isolated hardware checks. Runtime source commit: `ebd4f829f0d0965ed861b5451d70fffe54e8d997`.

- Engine DLL SHA-256: `08a225a75d6164e50c9d9d6064fa5ab69c29f3507fc4d57ef2e09dae5976d108`.
- ARM native SHA-256: `4bf39810fb65d5096e5bf94a46039acacbe21447b66a505b8b95e55f76f565fa`.
- SD rollback: `Roms/PORTS/Games/KingdomNewLands.backup-before-rc5-20261102T163317Z`.
- PC save backup: `/home/devarshi/Recompilation/work/knl-rc5/promotion-20261102T163317Z`.
- Full staged/post-promotion manifests matched. All four latest save files were byte-identical; all other game/music/data files were preserved. Only engine DLL, native library, version and installation metadata changed. No public release or GitHub push is implied by this local candidate.
