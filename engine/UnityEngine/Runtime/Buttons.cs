// Shared per-frame button state (desktop keyboard / handheld buttons / scripted input).
using UnityEngine;

namespace KNL
{
    /// Button state shared with UnityEngine.Input (same bits as knlcore's knl_buttons()).
    public static class Buttons
    {
        public const uint Left = 1, Right = 2, Up = 4, Down = 8, A = 16, B = 32, X = 64, Y = 128, Start = 256, Select = 512, L = 1024, R = 2048, Menu = 4096;
        /// Gamepad mode (device default; KNL_CONTROLS=gamepad on desktop): Rewired reports one joystick, so the
        /// game shows controller glyphs, and the D-pad is not visible as keyboard arrow keys.
        public static bool GamepadMode = System.Environment.GetEnvironmentVariable("KNL_CONTROLS") == "gamepad";
        internal static uint s_Now, s_Prev;
        static readonly float[] s_PressTime = new float[16], s_PrevPressTime = new float[16], s_DownSince = new float[16];
        static void Sync() { }
        /// Called once per frame by the engine loop.
        public static void BeginFrame()
        {
            s_Prev = s_Now; s_Now = Native.knl_buttons() | InputScript.Next();
            for (int i = 0; i < 16; i++)
            {
                uint m = 1u << i;
                if ((s_Now & m) != 0 && (s_Prev & m) == 0) { s_PrevPressTime[i] = s_PressTime[i]; s_PressTime[i] = Time.unscaledTime; s_DownSince[i] = Time.unscaledTime; }
            }
        }
        public static bool Held(uint m) { Sync(); return (s_Now & m) != 0; }
        public static bool Pressed(uint m) { Sync(); return (s_Now & m) != 0 && (s_Prev & m) == 0; }
        public static bool Released(uint m) { Sync(); return (s_Now & m) == 0 && (s_Prev & m) != 0; }
        public static bool DoublePressed(uint m)
        {
            Sync();
            for (int i = 0; i < 16; i++)
            {
                uint b = 1u << i;
                if ((m & b) != 0 && (s_Now & b) != 0 && (s_Prev & b) == 0 && s_PressTime[i] - s_PrevPressTime[i] < 0.3f) return true;
            }
            return false;
        }
        public static float TimeHeld(uint m)
        {
            Sync(); float t = 0f;
            for (int i = 0; i < 16; i++) { uint b = 1u << i; if ((m & b) != 0 && (s_Now & b) != 0) t = Mathf.Max(t, Time.unscaledTime - s_DownSince[i]); }
            return t;
        }
    }
}
