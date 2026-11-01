# Candidate acceptance checks

Record candidate ZIP SHA-256, source commit, data profile, Onion version and whether each item passed, failed or was not tested. Test the downloaded candidate on a clean installation as well as an update; the developer's working SD folder is insufficient.

## Automated checks

- Python preparation regressions: incomplete/wrong input, malformed pack, preserved saves, rollback after failed SD rename.
- Runtime regressions: next-frame/physics/end-of-frame waits, cancellation, disabled scripts, nested iterators, changing collider parent/body, unscaled/real clocks and quit callback.
- Clean build → device build → release packaging, without preexisting host DLLs.
- Extract candidate; verify its release manifest and run the included preparer against owned input.
- Boot a fresh save and a COPY of an existing save. Never use the only copy of a save for QA.
- Collect logic, drawing, presentation, RSS/managed memory and exception counters. Keep benchmark scenes and input sequences identical when comparing changes.

## Human gameplay checks

1. Start a new game. Finish the ghost tutorial, including standing at the indicated location, lighting the fire and subsequent hints. Confirm completed prompts disappear.
2. Pick up and spend coins; check pouch capacity/overflow against the original game. Verify the numerical wallet and visual bag agree.
3. Recruit workers and archers; upgrade camp, walls and towers. Repeat after pausing/resuming and after save/reload.
4. Open the menu with single D-pad presses and with holds. Visit every visible option; confirm save can be selected and activated.
5. Save, quit normally, relaunch and verify day, coins, buildings, recruited units and position. Repeat at least twice. Also load a backed-up v1.0 save separately.
6. Play through day/night combat and island transitions; check progression, audio, increasing entity counts and memory growth.
7. Use Menu + Start only after saving: it is an emergency exit without an automatic save request. Verify return to Onion, menu audio and a subsequently launched port/emulator.
8. Repeat fresh installation on actual Windows and Linux/macOS where claimed. Include a non-default game path, paths with spaces, missing FFmpeg, interrupted setup and an existing installation.

A candidate remains experimental until these checks have evidence. Do not label it stable based only on boot screenshots or zero exceptions.
