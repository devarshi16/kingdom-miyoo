// Optional small wallet display. The original player's Wallet remains authoritative for all
// pickup, spending, tax and persistence code; only the physical interface bag is bypassed.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace KNL {
public static class CoinCounter {
    public static readonly bool Enabled = string.Equals(Environment.GetEnvironmentVariable("KNL_WALLET"), "counter", StringComparison.OrdinalIgnoreCase);
    const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    sealed class Binding {
        public MonoBehaviour player, wallet;
        public PropertyInfo coins;
        public FieldInfo count;
        public MethodInfo spill;
        public int pendingPickups;
    }
    static readonly List<Binding> players = new List<Binding>();
    static readonly HashSet<GameObject> hiddenBags = new HashSet<GameObject>();
    static Binding current;
    static Binding overflowPlayer;
    static CounterOverflowPolicy overflow;
    static int shownCount = int.MinValue;
    static string shownText;

    // Called before a script's first Awake, while all serialized components are already attached.
    // Setting this flag afterwards is too late: Wallet.Awake resolves the physical bag reference.
    public static void BeforeAwake(MonoBehaviour mb) {
        if (!Enabled || mb == null) return;
        string name = mb.GetType().Name;
        if (name == "CoinBag" || name == "BagCoin") {
            if (name == "CoinBag") {
                hiddenBags.RemoveWhere(g => g == null);
                hiddenBags.Add(mb.gameObject);
                // Player.SelectPayable still calls Show directly on its bag reference. The
                // game's own debug gate returns before any fade/physics UI coroutine starts.
                var hide = mb.GetType().GetProperty("DebugHideCoinBag", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (hide != null && hide.CanWrite && hide.PropertyType == typeof(bool)) hide.SetValue(null, true, null);
            }
            mb.gameObject.SetActive(false);
            return;
        }
        if (name != "Wallet" && name != "Player") return;
        var player = mb.gameObject.GetComponent("Player") as MonoBehaviour;
        var wallet = mb.gameObject.GetComponent("Wallet") as MonoBehaviour;
        // NPC and tax-collector wallets must retain every original setting.
        if (player == null || wallet == null) return;
        var wt = wallet.GetType();
        var bag = wt.GetField("showCoinBag", Instance);
        if (bag != null && bag.FieldType == typeof(bool)) bag.SetValue(wallet, false);
        foreach (var p in players) if (ReferenceEquals(p.player, player) && ReferenceEquals(p.wallet, wallet)) return;
        var binding = new Binding { player = player, wallet = wallet,
            coins = wt.GetProperty("coins", Instance), count = wt.GetField("_coins", Instance),
            spill = player.GetType().GetMethod("CoinFellFromBag", Instance, null, Type.EmptyTypes, null) };
        // Original TryToGrabCoin calls SuckCoin then raises this Action synchronously. Direct
        // coins setters from persistence/tutorials do not raise it and must remain baselines.
        Action pickedUp = () => { if (binding.pendingPickups < int.MaxValue) binding.pendingPickups++; };
        var pickupEvent = wt.GetEvent("OnPickupCoin", Instance);
        if (pickupEvent != null && pickupEvent.EventHandlerType == typeof(Action)) pickupEvent.AddEventHandler(wallet, pickedUp);
        else {
            var pickupField = wt.GetField("OnPickupCoin", Instance);
            if (pickupField != null && pickupField.FieldType == typeof(Action))
                pickupField.SetValue(wallet, Delegate.Combine((Action)pickupField.GetValue(wallet), pickedUp));
            else Console.Error.WriteLine("[wallet-counter] Pickup event unavailable; positive balance writes remain protected baselines");
        }
        players.Add(binding);
    }

    // Player.Awake also resolves this tagged object, independently of Wallet.showCoinBag.
    // Preserve that one nonvisual reference while retaining ordinary inactive-tag semantics
    // for every other object and for the complete physical-pouch mode.
    internal static bool IsHiddenBag(GameObject go) { return Enabled && hiddenBags.Contains(go); }

    static bool SelectPlayer() {
        current = null;
        // A new/replacement player takes priority; no GameObject.Find or scene-wide reflection
        // occurs per frame. Inactive/dead objects never supply a stale balance after a scene load.
        for (int i = players.Count - 1; i >= 0; i--) {
            var p = players[i];
            if (p.player == null || p.wallet == null) { players.RemoveAt(i); continue; }
            if (current == null && p.player.gameObject.activeInHierarchy && p.wallet.gameObject.activeInHierarchy)
                current = p;
        }
        return current != null;
    }

    public static bool TryRead(out int count) {
        count = 0;
        if (!Enabled || !SelectPlayer()) return false;
        // This getter has no side effects in the unmodified game. Reading it each frame also
        // handles ApplyData, direct script writes and coroutine pickups without mirror bookkeeping.
        if (current.coins != null) count = (int)current.coins.GetValue(current.wallet, null);
        else if (current.count != null) count = (int)current.count.GetValue(current.wallet);
        else return false;
        return true;
    }

    // The calibrated overflow policy can call this later. There is deliberately no guessed
    // capacity here. Original Player code decrements the real wallet and creates the world drop,
    // including its original randomized water loss, exactly once.
    public static bool SpillOne() {
        int count;
        if (!TryRead(out count) || count <= 0 || current.spill == null) return false;
        try { current.spill.Invoke(current.player, null); return true; }
        catch (Exception e) { Lifecycle.Fail(current.player, "CounterOverflow", e); return false; }
    }

    // Host supplies the measured original-game profile. With no measurements configured,
    // the optional display never guesses a capacity or silently debits the player's money.
    public static void ConfigureOverflow(int[] firstSpillCounts) {
        overflow = new CounterOverflowPolicy(firstSpillCounts);
        overflowPlayer = null;
    }
    // Call after an explicit asynchronous save restore if it occurs after the first Tick.
    // Restoring data establishes a baseline; it must never masquerade as a fresh pickup.
    public static void PreserveCurrentBalance() {
        if (overflow == null) return;
        int count;
        if (!TryRead(out count)) { overflowPlayer = null; return; }
        overflowPlayer = current; current.pendingPickups = 0; overflow.Reset(count);
    }
    // QA host injection bypasses original TryToGrabCoin. Mark only its actual positive delta.
    public static void NotifyTestPickups(Component wallet, int amount) {
        int count;
        if (amount <= 0 || !TryRead(out count) || !ReferenceEquals(wallet, current.wallet)) return;
        current.pendingPickups = (int)Math.Min(int.MaxValue, (long)current.pendingPickups + amount);
    }
    public static void Tick() {
        if (overflow == null) return;
        int count;
        if (!TryRead(out count)) { overflowPlayer = null; return; }
        int pickedUp = current.pendingPickups; current.pendingPickups = 0;
        if (!ReferenceEquals(current, overflowPlayer)) {
            overflowPlayer = current; overflow.Reset(count); return;
        }
        if (overflow.Advance(count, Time.deltaTime, pickedUp) && !SpillOne()) overflow.Reset(count);
    }

    static readonly string[] Digits = {
        "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
        "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111"
    };
    const int AtlasWidth = 42, AtlasHeight = 11;
    static int atlas = -1;
    static void AtlasRect(byte[] pixels, int x, int y, int w, int h, uint color) {
        for (int row = y; row < y+h; row++) for (int col = x; col < x+w; col++) {
            int p = (row*AtlasWidth+col)*4;
            pixels[p] = (byte)color; pixels[p+1] = (byte)(color>>8);
            pixels[p+2] = (byte)(color>>16); pixels[p+3] = (byte)(color>>24);
        }
    }
    static void PrepareAtlas() {
        if (atlas >= 0) return;
        var pixels = new byte[AtlasWidth*AtlasHeight*4];
        for (int digit = 0; digit < 10; digit++)
            for (int row = 0; row < 5; row++) for (int col = 0; col < 3; col++)
                if (Digits[digit][row*3+col] == '1') AtlasRect(pixels, digit*3+col, row, 1, 1, 0xFFFFFFFFu);
        // The same hand-authored nine by eleven icon, composed once rather than five native
        // gradient calls per frame. The adjacent white dash supports negative diagnostic values.
        AtlasRect(pixels, 32, 0, 5, 11, 0xFF25466Bu); AtlasRect(pixels, 30, 2, 9, 7, 0xFF25466Bu);
        AtlasRect(pixels, 32, 1, 5, 9, 0xFF47BCEAu); AtlasRect(pixels, 31, 3, 7, 5, 0xFF47BCEAu);
        AtlasRect(pixels, 33, 2, 1, 7, 0xFFB5EFFFu); AtlasRect(pixels, 39, 0, 3, 1, 0xFFFFFFFFu);
        atlas = Native.knl_tex_create(AtlasWidth, AtlasHeight, pixels);
    }
    static void Glyph(int x, int y, int w, int h, int u, int v, int tw, int th, uint color) {
        Native.knl_draw_quad(atlas, x, y, x+w, y, x, y+h,
            u, v, u+tw, v, u, v+th, color, 0, 0, 0, RenderSystem.Width, RenderSystem.Height);
    }
    static void Rect(int x, int y, int w, int h, uint c) {
        Native.knl_quad_gradient(x, y, x+w, y+h, c, c, c, c, 0);
    }
    static void DrawWithoutAtlas(int left, int top) {
        // Keep the wallet readable even if the tiny native texture allocation fails.
        Rect(left+5, top+4, 5, 11, 0xFF25466Bu); Rect(left+3, top+6, 9, 7, 0xFF25466Bu);
        Rect(left+5, top+5, 5, 9, 0xFF47BCEAu); Rect(left+4, top+7, 7, 5, 0xFF47BCEAu);
        Rect(left+6, top+6, 1, 7, 0xFFB5EFFFu);
        for (int i = 0; i < shownText.Length; i++) {
            int x = left+16+i*8; char c = shownText[i];
            if (c == '-') { Rect(x, top+9, 6, 2, 0xFFE7F1FFu); continue; }
            for (int row = 0; row < 5; row++) for (int col = 0; col < 3; col++)
                if (Digits[c-'0'][row*3+col] == '1') Rect(x+col*2, top+4+row*2, 2, 2, 0xFFE7F1FFu);
        }
    }
    public static void Draw() {
        int count;
        if (!TryRead(out count)) return;
        PrepareAtlas();
        if (shownText == null || shownCount != count) { shownCount = count; shownText = count.ToString(System.Globalization.CultureInfo.InvariantCulture); }
        int width = 19 + shownText.Length * 8, left = RenderSystem.Width - 5 - width, top = 5;
        Rect(left, top, width, 19, 0xB8201B16u);
        if (atlas < 0) { DrawWithoutAtlas(left, top); return; }
        Glyph(left+3, top+4, 9, 11, 30, 0, 9, 11, 0xFFFFFFFFu);
        for (int i = 0; i < shownText.Length; i++) {
            int x = left+16+i*8;
            char c = shownText[i];
            if (c == '-') Glyph(x, top+9, 6, 2, 39, 0, 3, 1, 0xFFE7F1FFu);
            else Glyph(x, top+4, 6, 10, (c-'0')*3, 0, 3, 5, 0xFFE7F1FFu);
        }
    }
}
}
