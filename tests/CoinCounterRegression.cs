using System;
using KNL;
using UnityEngine;

// Independent stand-ins expose the same narrow surface used by the engine adapter. No game
// assembly or proprietary data is needed for these selection/lifecycle/accounting assertions.
class Wallet : MonoBehaviour {
    public bool showCoinBag = true, bagLookupInAwake;
    public int payTaxesAbove = 17, maxCoins = 1000000;
    public int _coins;
    public event Action OnPickupCoin;
    public int coins { get { return _coins; } set { _coins = Math.Min(maxCoins, value); } }
    public void GrabCoin() { coins++; if (OnPickupCoin != null) OnPickupCoin(); }
    void Awake() { bagLookupInAwake = showCoinBag; }
    void OnEnable() { _coins = 0; }
}
class Player : MonoBehaviour {
    public int drops;
    public void CoinFellFromBag() { GetComponent<Wallet>().coins--; drops++; }
}
class CoinBag : MonoBehaviour { public static bool DebugHideCoinBag { get; set; } public bool awoke; void Awake() { awoke = true; } }
class BagCoin : MonoBehaviour { public bool awoke; void Awake() { awoke = true; } }
class Coin : MonoBehaviour { public bool awoke; void Awake() { awoke = true; } }
class CoinCounterRegression {
    static int passed;
    static void Check(bool value, string label) { if (!value) throw new Exception("FAIL: "+label); passed++; Console.WriteLine("PASS: "+label); }
    static GameObject MakePlayer(string name, out Player p, out Wallet w) {
        var go = new GameObject(name); go.SetActive(false);
        p = go.AddComponent<Player>(); w = go.AddComponent<Wallet>();
        go.SetActive(true); return go;
    }
    static void Main(string[] args) {
        World.ActiveScene = new SceneTag("counter regression");
        bool counter = args.Length == 0;
        var bagGo = new GameObject("CoinBag"); bagGo.tag = "CoinBag"; var bag = bagGo.AddComponent<CoinBag>();
        var npc = new GameObject("tax collector").AddComponent<Wallet>();
        Player p; Wallet w; var player = MakePlayer("player", out p, out w);
        Check(CoinCounter.Enabled == counter, "Counter is opt-in through KNL_WALLET");
        Check(npc.showCoinBag && npc.bagLookupInAwake && npc.payTaxesAbove == 17, "NPC wallet and taxes are unchanged");
        Check(w.showCoinBag == !counter && w.bagLookupInAwake == !counter, "Player presentation flag is configured before Wallet.Awake");
        Check(bag.awoke == !counter && bagGo.activeSelf == !counter, "Physical bag never awakens in counter mode");
        Check(GameObject.FindGameObjectWithTag("CoinBag") == bagGo && GameObject.FindGameObjectsWithTag("CoinBag").Length == 1, "Original Player.Awake can still resolve its nonvisual bag reference");
        Check(CoinBag.DebugHideCoinBag == counter, "Original direct bag Show calls are gated before starting fades");
        var unrelated = new GameObject("unrelated inactive tag"); unrelated.tag = "NPC"; unrelated.SetActive(false);
        Check(GameObject.FindGameObjectWithTag("NPC") == null && GameObject.FindGameObjectsWithTag("NPC").Length == 0, "Inactive tag semantics remain unchanged for unrelated objects");
        int count;
        if (!counter) { Check(!CoinCounter.TryRead(out count), "Physical mode does not expose counter or overflow"); Console.WriteLine("Coin counter regressions passed: "+passed); return; }
        Check(CoinCounter.TryRead(out count) && count == 0, "Select actual player's initially empty wallet");
        npc.coins = 90; w.coins = 7; Check(CoinCounter.TryRead(out count) && count == 7, "NPC balances cannot replace player counter");
        w.coins = 4; Check(CoinCounter.TryRead(out count) && count == 4, "Spending is read directly from authoritative wallet");
        w.coins = 23; Check(CoinCounter.TryRead(out count) && count == 23, "Save restore and coin pickup writes are reflected");
        w.coins = 250; Check(CoinCounter.TryRead(out count) && count == 250 && w.maxCoins == 1000000, "Display has no guessed capacity clamp");
        Check(CoinCounter.SpillOne() && w.coins == 249 && p.drops == 1, "Overflow adapter calls original drop exactly once");
        w.coins = 0; Check(!CoinCounter.SpillOne() && p.drops == 1, "Empty wallet cannot create an overflow drop");
        var visualCoin = new GameObject("BagCoin").AddComponent<BagCoin>(); Check(!visualCoin.awoke && !visualCoin.gameObject.activeSelf, "Stray physical UI coin is inactive before Awake");
        var worldCoin = new GameObject("world coin").AddComponent<Coin>(); Check(worldCoin.awoke && worldCoin.gameObject.activeSelf, "Gameplay coins remain enabled");
        Player p2; Wallet w2; var replacement = MakePlayer("replacement", out p2, out w2); w2.coins = 12;
        Check(CoinCounter.TryRead(out count) && count == 12, "Replacement player owns displayed balance");
        UnityEngine.Object.DestroyImmediate(replacement); w.coins = 6; Check(CoinCounter.TryRead(out count) && count == 6, "Destroyed player binding is discarded");
        player.SetActive(false); Check(!CoinCounter.TryRead(out count) && !CoinCounter.SpillOne(), "Inactive scene cannot show or debit stale player");
        player.SetActive(true); w.coins = 9; Check(CoinCounter.TryRead(out count) && count == 9, "Reactivated player resumes original wallet");
        CoinCounter.ConfigureOverflow(new[] { 41 }); Lifecycle.Frame(.1f);
        int beforeDrops = p.drops;
        w.coins = 90; for (int i = 0; i < 10; i++) Lifecycle.Frame(.1f);
        Check(w.coins == 90 && p.drops == beforeDrops, "Late save restore without original pickup event loses no coins");
        w.GrabCoin(); Lifecycle.Frame(.1f); Lifecycle.Frame(.2f);
        Check(w.coins == 91 && p.drops == beforeDrops, "Original event pickup shows temporary increment");
        Lifecycle.Frame(.1f);
        Check(w.coins == 90 && p.drops == beforeDrops+1, "Original event subscription triggers one delayed real drop");
        w.coins = 0; Lifecycle.Frame(.1f); w.coins = 60;
        CoinCounter.NotifyTestPickups(w, 60);
        for (int i = 0; i < 70; i++) Lifecycle.Frame(.1f);
        Check(w.coins == 40 && p.drops == beforeDrops+21, "Explicit QA pickup injection settles through separate original debits");
        w.GrabCoin(); Lifecycle.Frame(.1f); Time.timeScale = 0;
        for (int i = 0; i < 10; i++) Lifecycle.Frame(.1f);
        Check(w.coins == 41, "Paused game retains pending coin without overflow");
        Time.timeScale = 1; Lifecycle.Frame(.2f); Lifecycle.Frame(.1f);
        Check(w.coins == 40, "Resuming completes pending delay using scaled time");
        w.coins = 90; w.GrabCoin(); Lifecycle.Frame(.1f); Lifecycle.Frame(.2f); Lifecycle.Frame(.1f);
        Check(w.coins == 90, "Combined restore and pickup protects restored ninety coins");
        npc.GrabCoin(); CoinCounter.NotifyTestPickups(npc, 1);
        for (int i = 0; i < 5; i++) Lifecycle.Frame(.1f);
        Check(w.coins == 90, "NPC pickup events and QA notifications cannot debit player balance");
        Console.WriteLine("Coin counter regressions passed: "+passed);
    }
}
