# Local candidate validation — 2026-11-01

Status: experimental; full gameplay acceptance is pending. No public release was published.

- Candidate: `KingdomNewLands-MiyooMini-1.1.0-rc1.zip` (6.3 MiB).
- Packaged source: `5f3aceb111daff73c18dfae6499a9f6b2901eb2d`, clean working tree at packaging.
- ZIP SHA-256: `09c381c78b90008ff563aad6685ab6ac99ee75f5132164057aec615bb89f13a2`.
- Input profile: owned Kingdom: New Lands 1.2.8, Unity 2017.1.2p1. Other builds require separate verification.
- Related skill repository: branch `codex/repair-porting-skill`, commit `1fe2d70`.

## Checks completed

- Preparation regressions: 8 passed, including invalid input, pack validation, save preservation and failed replacement rollback.
- Managed runtime regressions: 20 assertions passed, covering coroutine phases/lifecycle, collider cache invalidation, clocks and shutdown.
- Skill regressions: 6 passed; skill quick validation, desktop template compilation and ARM staging also passed.
- Clean checkout extraction, packing, desktop/ARM builds and release packaging succeeded. The extracted player ZIP completed conversion and installation against the owned input.
- Desktop runtime booted for 1,200 frames without logged exceptions; the packaged managed runtime also completed a 900-frame boot.
- Miyoo Mini Plus: separate `KingdomNewLandsTest` installation, separate QA saves. A 600-frame headless run and a 600-frame framebuffer/audio run completed without logged exceptions. The graphical run recorded 30 audio plays and zero missing clips; a captured frame showed the rendered introduction.
- Graphical run after GC: 47.4 MB managed, 71.4 MB RSS. Last sampled worker drawing was 64.2 ms and framebuffer flip 5.5 ms. These are diagnostic samples, not a controlled FPS comparison or an improvement claim.
- All 77 files from the final prepared installation matched their local hashes on the device after QA.
- Onion MainUI/audio returned, CPU governor was restored to `ondemand`, and temporary QA launch/service overrides were removed. Original game installation and saves were preserved.

## Still required

Follow [TESTING.md](TESTING.md), especially tutorial completion/prompt dismissal, menu navigation and save/reload, building upgrades, pouch capacity, ghost positioning and extended progression. Boot success and zero logged exceptions do not establish correct gameplay.

Windows and macOS player setup have not been tested on those operating systems. The current player route still needs Python, dependencies and a host conversion step. A standalone installer and an on-device importer are not implemented. The bounded native header probe establishes only that header inspection can fit; it does not establish full conversion feasibility.

No claim is made that the community's stuck fire hint, skipped save option or upgrade failures are resolved. Coroutine/physics corrections are relevant candidates, but these symptoms need direct reproduction and gameplay verification. Pouch, ghost and further FPS work remain open.
