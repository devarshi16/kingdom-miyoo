using System;
using KNL;
class CounterOverflowRegression {
    static int passed;
    static void Check(bool value, string label) { if (!value) throw new Exception("FAIL: "+label); passed++; Console.WriteLine("PASS: "+label); }
    static void Main() {
        // Synthetic test measurements exercise the policy, never shipped calibration data.
        int[] observations = { 41, 44, 41, 48 };
        var p = new CounterOverflowPolicy(observations, n => 0);
        Check(p.ObservedSafeMinimum == 40, "Operational floor comes from the finite minimum observed first spill minus one");
        p.Reset(0); Check(p.RetainedLimit == 40, "Sampled retained limit excludes the first spilled coin");
        observations[0] = 1; p.Reset(0); Check(p.RetainedLimit == 40, "Caller cannot alter a configured calibration array");
        Check(!p.Advance(40, 5f), "No drop anywhere in measured safe region");
        Check(!p.Advance(41, .1f), "New coin remains visible before overflow deadline");
        Check(!p.Advance(41, .2f), "Overflow waits for the complete delay");
        Check(!p.Advance(41, 0f), "Pause cannot advance an almost-expired deadline");
        Check(p.Advance(41, .051f), "One original debit is requested after 0.25 scaled seconds");
        Check(!p.Advance(40, 5f), "Settled capacity never keeps spilling");
        p.Reset(40); p.Advance(41, .02f); p.Advance(41, .2f);
        Check(!p.Advance(40, .1f) && !p.Advance(40, 3f), "Spending below capacity cancels pending overflow");
        p.Reset(40); p.Advance(43, .02f); p.Advance(43, .2f);
        Check(!p.Advance(42, .1f) && !p.Advance(42, .2f), "Spending while above capacity restarts deadline");
        Check(p.Advance(42, .051f), "Remaining genuine excess can spill after its fresh delay");
        Check(!p.Advance(41, .2f) && p.Advance(41, .051f), "Burst excess drops at most once per interval");
        Check(!p.Advance(40, 10f), "Final excess debit reaches capacity and stops");
        p.Reset(40); p.Advance(80, .01f);
        Check(p.Advance(80, 10f) && !p.Advance(79, .01f), "Long frame cannot request an unbounded drop burst");
        p.Reset(90); Check(p.RetainedLimit == 90 && !p.Advance(90, 50f), "Loaded balance above measured capacity is preserved indefinitely");
        Check(!p.Advance(91, .01f) && p.Advance(91, .25f), "Only fresh pickup can overflow a preserved loaded balance");
        p.Reset(0);
        Check(!p.Advance(90, .1f, 0) && !p.Advance(90, 50f, 0) && p.RetainedLimit == 90, "Asynchronous save restoration without a pickup event is protected");
        Check(!p.Advance(91, .01f, 1) && p.Advance(91, .25f, 0), "Original pickup event can overflow a protected restored balance");
        p.Reset(0);
        Check(!p.Advance(91, .01f, 1) && p.RetainedLimit == 90 && p.Advance(91, .25f, 0), "Save restore and real pickup in one frame preserve the restored portion");
        int rolls = 0;
        var cycling = new CounterOverflowPolicy(new[] { 41, 44 }, n => (rolls++) % n);
        cycling.Reset(0); cycling.Advance(20, .1f); cycling.Advance(21, .1f); cycling.Advance(30, .1f);
        Check(rolls == 1 && cycling.RetainedLimit == 40, "Capacity is sampled once across low-balance pickup frames");
        cycling.Advance(42, .1f); cycling.Advance(30, .1f);
        Check(rolls == 1, "Crossing below safe region arms resampling without rolling every spent coin");
        cycling.Advance(31, .1f); cycling.Advance(32, .1f);
        Check(rolls == 2 && cycling.RetainedLimit == 43, "Next fill after low balance samples the measured distribution once");
        cycling.Advance(0, .1f); cycling.Advance(0, .1f); cycling.Advance(1, .1f); cycling.Advance(2, .1f);
        Check(rolls == 3 && cycling.RetainedLimit == 40, "Empty wallet starts one new fill rather than rerolling per frame");
        var edge = new CounterOverflowPolicy(new[] { 1 }, n => 0); edge.Reset(0); edge.Advance(1, .01f);
        Check(edge.Advance(1, .25f) && !edge.Advance(0, 100f), "Minimum valid profile still cannot spill from an empty balance");
        bool rejectsEmpty = false, rejectsInvalid = false;
        try { new CounterOverflowPolicy(new int[0]); } catch (ArgumentException) { rejectsEmpty = true; }
        try { new CounterOverflowPolicy(new[] { 0 }); } catch (ArgumentException) { rejectsInvalid = true; }
        Check(rejectsEmpty && rejectsInvalid, "Missing or invalid original measurements are rejected instead of guessed");
        Console.WriteLine("Overflow policy regressions passed: "+passed);
    }
}
