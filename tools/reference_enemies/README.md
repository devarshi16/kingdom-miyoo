# Investigating crowded enemy visibility

These QA harnesses intentionally change the scene, clock and player wallet. Run
them only against a **copied save and copied game/runtime**, outside an installation
used for play. No owned DLLs, assets, screenshots, saves or Wine runtime are supplied.
The tools do not establish that an intermittent player report has been reproduced.

Both harnesses hold the original Director clock at 22:00, use an existing wall,
place the player inside it, and spawn 24 original pooled trolls outside it. Twelve
receive the original `MakeToughTroll` call. A large player wallet keeps this short
combat experiment running. Original AI, wall combat, animations and damage remain
active. `KNL_ENEMY_WALL_X=-8.5` selects the same existing wall in both runtimes;
without it the harness selects the wall nearest the initial player position.

At selected frames, capture the crowd, then hide each enemy's renderers in turn
for a second capture. No simulation frame advances between these captures. A
zero pixel difference can mean offscreen, a dead transparent sprite, another
enemy covering it, or world geometry covering it. It **does not by itself prove
a broken renderer**. Also capture the scene with all QA enemy renderers hidden,
then restore each enemy's original renderer-enable flags alone. This distinguishes
crowd overlap from a failure to draw that enemy in the same scene. Inspect actual
sprite bounds and exclude unrelated animated water/background differences.

## Replacement engine

`python3 tools/reference_enemies/make_port_probe.py /NEW/PRIVATE/Program.cs`
writes a private instrumented host and refuses to replace an existing output.
Compile it as `knl.dll` against a privately copied matching `UnityEngine.dll`,
alongside the engine's normal owned game assemblies and native library. The
assembly name preserves the existing host's access to diagnostic engine fields.
The production host source is never modified.

Set `KNL_ENEMY_SHOTS` to an existing private output directory. Run the private host
with `--data /OWNED/PREPARED_DATA --boot --frames 1300 --save /COPIED/SAVE` and the
normal launcher's `KNL_WALLET=counter KNL_VISUALS=lite`. Night starts at frame 200,
the QA wave at frame 600; screenshots run every 120 frames through frame 1200.
Logs record actor pose, sprite, alpha, damage state, shader, texture handle and
material overrides. This is a diagnostic workload, **not an FPS benchmark**.

## Original Unity player

Compile `OriginalEnemyVisibility.cs` as a .NET 2.0-compatible library against the
owned original `UnityEngine.dll`, in a copied original player's Managed folder.
Compile `Instrument.cs` against Mono.Cecil 0.11. Invoke it with the original
Managed directory, harness DLL and copied output `Assembly-CSharp.dll`. It adds
only `OriginalEnemyVisibility.Ensure()` at the beginning of `Loader.Awake` and
refuses an output path equal to the original assembly. The installed game stays
untouched.

Run the copied player with graphics enabled. The recorded Linux experiment used
the already-available private Wine 6.0.3 prefix, Xvfb and `-batchmode -force-d3d9
-screen-fullscreen 0 -screen-width 640 -screen-height 480`. Do not use `-nographics`
for a rendering comparison. Windows needs no Wine. The harness logs the original
Unity version, camera sort settings and ambient color, and exits after the short
trial. Images are private files in `enemy-shots` beside the copied player.

The original harness waits for the active player and managers, sets the night
clock for 180 frames, then spawns the wave. Captures are at wave frames 0/120/240;
the solo-enemy controls are at frame 120. Compare actor states and geometry,
rather than assuming the different runtime frame counts produce identical
physics, weather, initial world state or pixels. Reject runs with harness or
pool errors; an incomplete run is not evidence of renderer behavior.

Observed results and limits are indexed in
[`docs/evidence/enemy-visibility-20261102.json`](../../docs/evidence/enemy-visibility-20261102.json).
