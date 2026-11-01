// Capacity approximation calibrated from repeated first-spill measurements in the original
// Unity player. This class does no game object work and never invents a capacity or world coin.
using System;

namespace KNL {
public sealed class CounterOverflowPolicy {
    readonly int[] retained;
    readonly Func<int, int> choose;
    readonly System.Random rng = new System.Random();
    // Observed in the finite calibration sample, not a universal physical-pouch guarantee.
    public readonly int ObservedSafeMinimum;
    public const float SpillInterval = 0.25f;
    public int RetainedLimit { get; private set; }
    int previous;
    bool initialized, refillPending, pending;
    float elapsed;

    public CounterOverflowPolicy(int[] firstSpillCounts, Func<int, int> chooseIndex = null) {
        if (firstSpillCounts == null || firstSpillCounts.Length == 0) throw new ArgumentException("Original first-spill measurements are required", "firstSpillCounts");
        retained = new int[firstSpillCounts.Length];
        int min = int.MaxValue;
        for (int i = 0; i < retained.Length; i++) {
            if (firstSpillCounts[i] < 1) throw new ArgumentException("First-spill counts must be positive", "firstSpillCounts");
            retained[i] = firstSpillCounts[i] - 1;
            min = Math.Min(min, retained[i]);
        }
        ObservedSafeMinimum = min;
        choose = chooseIndex;
    }
    int Sample() {
        int index = choose == null ? rng.Next(retained.Length) : choose(retained.Length);
        if (index < 0 || index >= retained.Length) throw new ArgumentOutOfRangeException("chooseIndex");
        return retained[index];
    }
    // A restored/replaced wallet is a baseline, not a pickup. Keep every existing coin even
    // when an old save holds more than the sampled capacity; later pickups may overflow.
    public void Reset(int count) {
        RetainedLimit = Math.Max(count, Sample());
        previous = count; initialized = true;
        refillPending = pending = false; elapsed = 0;
    }
    public void PreserveBalance(int count) {
        if (!initialized) { Reset(count); return; }
        RetainedLimit = Math.Max(RetainedLimit, count);
        previous = count; refillPending = pending = false; elapsed = 0;
    }
    // Returns at most one original-drop request. The caller must apply one real Wallet debit
    // when true; a failed original drop should Reset with the still-authoritative balance.
    public bool Advance(int count, float scaledDeltaTime, int confirmedPickups = int.MaxValue) {
        if (!initialized) { Reset(count); return false; }
        // Persistence, tutorial grants and character replacement also set Wallet.coins.
        // Only the original pickup event authorizes an increase to overflow. If a restore
        // and pickup land in the same frame, protect the unexplained part of the increase.
        if (count > previous && confirmedPickups < count - previous) {
            PreserveBalance(count - Math.Max(0, confirmedPickups));
            if (confirmedPickups <= 0) return false;
        }
        bool increase = count > previous, decrease = count < previous;
        if (decrease && (count == 0 || (previous >= ObservedSafeMinimum && count < ObservedSafeMinimum)))
            refillPending = true;
        // Sample once at the next fill, after crossing below the safe region or emptying.
        // Low balances and ordinary frames do not repeatedly roll a more favorable limit.
        if (increase && refillPending) { RetainedLimit = Sample(); refillPending = false; }
        bool wasPending = pending;
        if (increase && count > RetainedLimit) pending = true;
        if (count <= RetainedLimit) { pending = false; elapsed = 0; }
        if (decrease) elapsed = 0; // spending invalidates the old deadline
        if (pending && wasPending && !decrease && scaledDeltaTime > 0) elapsed += scaledDeltaTime;
        previous = count;
        if (!pending || scaledDeltaTime <= 0 || elapsed < SpillInterval || count <= ObservedSafeMinimum) return false;
        // Account for the debit requested here so it isn't mistaken for spending next frame.
        previous = count - 1; elapsed = 0; pending = previous > RetainedLimit;
        return true;
    }
}
}
