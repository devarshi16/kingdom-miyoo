# Local candidate validation — 2026-11-02

Status: experimental. Human gameplay acceptance and heavier scenes remain pending. No branch, release or community message was published.

## Candidate and installation

Runtime source: `ba26a0b67dc66753edcfd79f95f0e58732588182` on `codex/stabilize-port`. Archive: `KingdomNewLands-MiyooMini-1.1.0-rc3.zip` (6.4 MiB), SHA-256 `0f5e09fc40bd1b2425795535339ffdcf3aa75e734d85d395b91cb46e793ccc2d`. Supported owned data remains Kingdom: New Lands 1.2.8 / Unity 2017.1.2p1. Target: Miyoo Mini Plus, Onion 4.3.1-1, stock 1.2 GHz performance mode while playing, original 320×240 source and exact 2× display scaling, audio enabled. Both Cortex-A7 cores were online; frequency was verified at 1200000 kHz during diagnosis.

The complete candidate is installed separately as **Kingdom New Lands Test**. The production port and its saves were not modified. QA uses separate save directories. Each update stages a complete port, verifies runtime/launcher hashes and copied save inventory, then activates by directory rename with the previous installation retained for rollback.

## Behavior changes and evidence

See [counter calibration](POUCH-COUNTER.md), [all 20 original-trial rows](evidence/original-pouch-20.csv), [artifact/protocol provenance](evidence/original-pouch-20.json) and [visual scope](LITE-VISUALS.md).

The original Windows x86 Unity player completed 20 instrumented stationary-pouch fills. First spills occurred at 41–49 coins (mean 43.7, median 43.5). The 20 accepted trials had no logged exceptions; preliminary pool-initialization failures were excluded. The supplied original installation's assembly hash remained unchanged. This is original-engine evidence, not a run of the replacement Box2D solver. The original headless player reported a 1914×1007 screen and 382×202 internal render size; its original pouch shapes and 50 Hz solver were retained. The calibration is scoped to this stationary fill protocol.

The counter samples a retained limit of 40–48 from the measured observations, waits 0.25 scaled seconds above that limit, then invokes the original world-drop/wallet-debit method once per interval. Forty or fewer coins never overflow through this policy. Real pickup events distinguish income from save restore, grants and player replacement; imported balances are preserved. The simplified policy does not reproduce every physical collision or multi-coin avalanche.

The lite visual mode removes the dynamic water mirror/ripples and reduces decorative cloud/fog/mist density. It bypasses computation of particles that the compatibility renderer does not draw, while retaining initialization, emitter calls and clocks and falling back for event/tracking consumers. Gameplay sprites, selection/tutorial UI, lighting, physics frequency and weather/season rules remain active. Full visuals and experimental physical-pouch modes remain selectable independently.

## Checks

- Desktop and ARM builds succeeded.
- Final full build checks: 28 counter assertions, 8 physical-mode assertions, 26 overflow-policy assertions, 7 HUD pixel/memory cases, 18 visual assertions, 34 existing runtime assertions and 1,000 occlusion command-list comparisons passed. Native renderer/framebuffer/pouch coverage also passed: 3,000 pixel comparisons, 2,880 display layouts and 6 pouch cases. The latter mode remains available but is not used by the counter.
- Eight preparation tests passed.
- Desktop 60-pickup injection completed 1,800 frames, settled to 40 coins, with zero exceptions and 94 audio plays/zero missing clips. The physical bag remained inactive and uninitialized. Final integration tests separately exercise real pickup event subscription, late/mixed save restoration, pause, spending and NPC isolation.
- Device diagnostic 60-pickup run completed 1,200 frames, settled to 41 coins, with zero exceptions and 86 audio plays/zero missing clips. Profiling level 2 was enabled, so its FPS is not an acceptance measurement. Managed after GC 48.3 MB; RSS 71.3 MB.
- Original instrumenter was compiled from the checked-in source. Its produced DLL was byte-identical to the instrumented assembly used for the completed original trials.
- Local screenshots were inspected; owned graphics/captures and original or instrumented game assemblies are excluded from the candidate ZIP.

## FPS measurements

Each value is a 300-frame wall-clock window from a 1,200-frame fresh starting route with profiling disabled and scripted right at 500–560/down at 650–656. The first 300 frames include blocking loading and are excluded from steady-window claims. Fresh real-time runs can differ in terrain/timing/state, so these are workload samples, not pixel-identical controlled comparisons.

| Candidate/workload | Frames 301–600 | Frames 601–900 | Frames 901–1200 |
| --- | ---: | ---: | ---: |
| rc2, ordinary start |28.99|26.02|27.62|
| Initial rc3 lite/counter before cloud/HUD refinements |25.86|24.33|26.20|
| Final rc3, ordinary start |30.62|32.74|28.25|
| Final rc3, 60 injected pickups |30.26|31.15|28.69|

The initial visual cuts alone did not establish a device improvement; they prompted further profiling and the cloud/HUD/occlusion refinements. The final ordinary-start run completed 1,200 frames without logged exceptions, 58 audio plays/zero missing clips, 48.0 MB managed after GC and 72.0 MB RSS. Its steady windows were 28.25–32.74 FPS: closer to 30, but not a sustained 30 FPS guarantee. The loading-inclusive first 300 frames were 15.50 FPS. The final 60-pickup stress run also completed 1,200 frames with zero logged exceptions. The original Player Wallet settled at 41, while the original CoinBag stayed inactive with its coin list/pool/container uninitialized. It reported 89 audio plays/zero missing clips, 48.1 MB managed after GC and 70.2 MB RSS. Its first loading-inclusive window was 15.30 FPS. A final screenshot was inspected.

The final ZIP passed CRC and all 40 manifest-payload SHA-256 checks. Its five installed runtime/launcher files match the package. No owned game assemblies, pack, music or captures are included. Test/save still matches the pre-deployment inventory (the default test save directory was empty; QA used separate directories). MainUI and audioserver returned, no game process remained, the CPU governor returned to `ondemand`, and temporary service-retention/launch overrides were absent. Previous complete test installations remain available for rollback. No change reduces physics frequency, rendering resolution or adds overclocking. The preexisting `--max-dt 0.05` launcher cap remains.

## Still required

Use [TESTING.md](TESTING.md): finish the tutorial, pick up/spend and overflow coins, test save/reload including an older high-balance save, recruit/upgrade, and progress through night combat and island transitions. Inspect water/cloud presentation during different weather/seasons. This candidate must not be called stable from boot success or zero exceptions alone. Native Windows/macOS setup and alternate owned game builds remain unverified.

The related porting skill (`codex/repair-porting-skill`, commit `1c966c6`) was updated with original-runtime measurement, explicitly accepted UI approximations, save-versus-pickup separation and scoped cosmetic simulation budgets.
