# Counter and original-pouch calibration

The Miyoo launcher now defaults to `KNL_WALLET=counter` and `KNL_VISUALS=lite`. Setting `KNL_WALLET=physical` restores the experimental physical pouch; setting `KNL_VISUALS=full` restores the prior reflection/particle work. These modes are independent. See [lite visuals](LITE-VISUALS.md).

The counter reads the original player's actual Wallet.coins. Physical UI bag objects stay inactive before Awake; no BagCoin objects, contact solver or bag animation coroutines are needed. Gameplay world coins, other characters' wallets, pickup/payment, taxes, upgrades and saving continue through the original game code. The inactive tagged bag remains available to the original Player's initialization and selection references.

## Original measurements

Twenty completed, error-free trials ran in an instrumented private copy of the original **Windows x86 Unity 2017.1.2p1 player**, using its native Physics2D implementation. The installed original files were not modified. REA managed evidence identified the original assembly and call relationships; independent CIL inspection and runtime hooks established the measured spill path. REA static evidence IDs and artifact hashes are retained in [provenance](evidence/original-pouch-20.json).

One coin was added every 0.24 simulation seconds with a new seeded, empty bag each trial. Physics ran at the original 0.02-second step. After first overflow, the pile settled for one second. Unrelated world logic was frozen, while original pouch/coin scripts, pooling, spawning, collision and drop callbacks remained active. Preliminary runs with pool initialization errors were discarded.

First-spill wallet counts were:

`46, 44, 41, 44, 41, 42, 43, 49, 45, 42, 41, 48, 45, 46, 46, 42, 44, 43, 41, 41`.

Range **41–49**, mean **43.7**, median **43.5**. The original sometimes knocked several coins out after the first spill; retained counts after settling ranged from 39 to 47. [All 20 rows](evidence/original-pouch-20.csv) and [reproduction tools](../tools/reference_pouch/README.md) are retained.

## Counter approximation

A fill samples one retained limit from those observations minus one: **40–48 coins**, with each observation carrying equal weight. This models the measured variability without running pouch physics. It does not reproduce the original geometry, every collision, or pile avalanches.

A confirmed new pickup above that limit remains visible for 0.25 scaled seconds, then the original Player.CoinFellFromBag method drops a real coin and decrements the real wallet. It also retains the game's original chance of losing the dropped coin in water. Bursts drain at most one excess coin per interval. Spending cancels or postpones a pending spill; pausing stops the timer. Returning below the policy's 40-coin safe region allows a new capacity sample on the next fill. No balance of 40 or fewer is dropped by this policy.

Saved/restored balances and player replacement are baselines, not pickups; excess saved coins are preserved. Only original pickup events arm new overflow. The debug test-coin route explicitly marks injected income for QA. The same-frame restore-plus-pickup case preserves the restored portion.

This is an intentional performance/behavior compromise for one supported build and one fill protocol, not a claim of exact original capacity or a universal probability distribution. Movement, rapid pickups and other game builds require separate calibration. Nightly acceptance should include pickup/spend, overflow world drops, save/reload above the sampled limit and normal gameplay.
