# Isolated combat fixtures

Developer-only instrumentation; never ship the generated host, original assemblies,
assets, saves or screenshots. The production host contains no fixture switches.

After a normal desktop engine build, create a private directory on a data volume:

```sh
python3 tools/probes/combat_stress/make_host.py /private/qa/Program.cs
mcs -optimize+ -target:exe -out:/private/qa/knl.dll \
  -r:engine/build/UnityEngine.dll -r:System.Core.dll /private/qa/Program.cs
```

Keep the assembly name `knl`, so reflection/expression delegates have the same
access context as the production host. Put it in a separate installation with its
own managed dependencies, data and disposable copied save. Never point its
`--save` argument at normal saves. Back up the source save before copying it.

Set `KNL_STRESS_SCENARIO` to `winter-horde`, `portal-assault` or `coin-health`.
Run for 1,800 frames with `--boot --window --mute --max-dt 0.05 --fixed-dt`, an
explicit private `--data` and `--save`, `KNL_PROF=0`, `KNL_VISUALS=lite` and
`KNL_WALLET=counter`. Scenarios construct the battle at frame 600 and emit a
`STRESS sample` every 60 frames. Only the QA king is invulnerable. Original unit
AI, wallets, damage, projectiles and collision callbacks remain active.

The health fixture puts two knight squads on open ground and enemies on both
sides to exercise coin loss and pickup rather than only shield/wall hits. All
fixtures depend on the starting save; compare the same immutable save, host,
seed and native core while changing only the engine under test. Compare every
sample field, including simulation time and exception count.

For hardware timing, ensure no other game runs, pause Onion's MainUI, record the
clock/governor and restore its prior state afterward. Keep stock 1200 MHz results
separate from overclocked results. Compare profiler-off fixed-step windows for
controlled changes. Run separately without `--fixed-dt` for ordinary elapsed-time
throughput; do not equate their simulated histories. Audio-enabled throughput
needs a separate run. See `docs/VALIDATION-1.1.0-rc6.md` for measured limits.
