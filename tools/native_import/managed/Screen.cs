using System;
using System.Text;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace KNL.Import
{
    sealed class Screen : IDisposable
    {
        const string Lib = "knlcore";
        [DllImport(Lib)] static extern int knl_init(int w, int h, int scale, int window);
        [DllImport(Lib)] static extern void knl_shutdown();
        [DllImport(Lib)] static extern void knl_clear(uint color);
        [DllImport(Lib)] static extern int knl_present();
        [DllImport(Lib)] static extern uint knl_buttons();
        [DllImport(Lib)] static extern int knl_tex_create(int w, int h, byte[] data);
        [DllImport(Lib)] static extern void knl_draw(int texture,
            float a,
            float b,
            float c,
            float d,
            float tx,
            float ty,
            uint color,
            int mode);
        [DllImport(Lib)] static extern int knl_save_png(string path, int scale);
        readonly Dictionary<char, int> glyphs = new Dictionary<char, int>();
        readonly Stopwatch timer = Stopwatch.StartNew();
        int white;
        long last = -1000;
        string previous = "";
        bool enabled, cancelRequested;
        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.:/%+-_?";
// Small, original block-letter alphabet; independent of the game's font/assets.
        static readonly string[] Shapes = {
            "01110/10001/10001/11111/10001/10001/10001", "11110/10001/10001/11110/10001/10001/11110",
            "01111/10000/10000/10000/10000/10000/01111",
            "11110/10001/10001/10001/10001/10001/11110",
            "11111/10000/10000/11110/10000/10000/11111",
            "11111/10000/10000/11110/10000/10000/10000",
            "01111/10000/10000/10111/10001/10001/01111",
            "10001/10001/10001/11111/10001/10001/10001",
            "11111/00100/00100/00100/00100/00100/11111",
            "00111/00010/00010/00010/10010/10010/01100",
            "10001/10010/10100/11000/10100/10010/10001",
            "10000/10000/10000/10000/10000/10000/11111",
            "10001/11011/10101/10101/10001/10001/10001",
            "10001/11001/10101/10011/10001/10001/10001",
            "01110/10001/10001/10001/10001/10001/01110",
            "11110/10001/10001/11110/10000/10000/10000",
            "01110/10001/10001/10001/10101/10010/01101",
            "11110/10001/10001/11110/10100/10010/10001",
            "01111/10000/10000/01110/00001/00001/11110",
            "11111/00100/00100/00100/00100/00100/00100",
            "10001/10001/10001/10001/10001/10001/01110",
            "10001/10001/10001/10001/10001/01010/00100",
            "10001/10001/10001/10101/10101/11011/10001",
            "10001/10001/01010/00100/01010/10001/10001",
            "10001/10001/01010/00100/00100/00100/00100",
            "11111/00001/00010/00100/01000/10000/11111",
            "01110/10001/10011/10101/11001/10001/01110",
            "00100/01100/00100/00100/00100/00100/01110",
            "01110/10001/00001/00010/00100/01000/11111",
            "11110/00001/00001/01110/00001/00001/11110",
            "00010/00110/01010/10010/11111/00010/00010",
            "11111/10000/10000/11110/00001/00001/11110",
            "01110/10000/10000/11110/10001/10001/01110",
            "11111/00001/00010/00100/01000/01000/01000",
            "01110/10001/10001/01110/10001/10001/01110",
            "01110/10001/10001/01111/00001/00001/01110",
            "00000/00000/00000/00000/00000/00110/00110",
            "00000/00100/00100/00000/00100/00100/00000",
            "00001/00010/00010/00100/01000/01000/10000",
            "11001/11010/00100/01000/10110/00110/00000",
            "00000/00100/00100/11111/00100/00100/00000",
            "00000/00000/00000/11111/00000/00000/00000",
            "00000/00000/00000/00000/00000/00000/11111", "01110/10001/00001/00010/00100/00000/00100"
        };
        public Screen(bool window)
        {
            if (!window) {
                return;
            }
            if (knl_init(320, 240, 2, 1) != 0) {
                throw new InvalidOperationException(
                    "Cannot initialize installation display");
            }
            enabled = true;
            white = knl_tex_create(1, 1, new byte[] {255, 255, 255, 255});
            for (int i = 0; i < Alphabet.Length; i++) {
                var data = new byte[5 * 7 * 4];
                string bits = Shapes[i].Replace("/", "");
                for (int p = 0; p < bits.Length;
                    p++) {
                    if (bits[p] == '1') {
                        for (int c = 0; c < 4; c++) {
                            data[p * 4 + c] = 255;
                        }
                    }
                }
                glyphs[Alphabet[i]] = knl_tex_create(5, 7, data);
            }
        }
        void Rect(float x, float y, float w, float h, uint color)
        {
            knl_draw(white, w, 0, 0, h, x, y, color, 0);
        }
        void Text(string text, int x, int y, uint color = 0xffe6edf6, float scale = 1)
        {
            foreach (char ch in text.ToUpperInvariant()) {
                int handle;
                if (glyphs.TryGetValue(ch, out handle)) {
                    knl_draw(handle, scale, 0, 0, scale, x, y, color, 0);
                }
                x += (int)(6 * scale);
            }
        }
        public void Progress(string text, double fraction)
        {
            if (text != previous)
                Console.Error.WriteLine("STATUS " + fraction.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " " + text);
            if (!enabled) {
                previous = text;
                return;
            }
            uint buttons = knl_buttons();
            // The last two percent include the durable directory transaction.
            // Once it begins, finish recovery/commit before accepting a pause.
            if (fraction < .98 && (buttons & 32) != 0) {
                cancelRequested = true;
            }
            if (cancelRequested) {
                throw new OperationCanceledException(
                    "Preparation paused. Launch again to resume.");
            }
            if (timer.ElapsedMilliseconds - last < 100 && text == previous) {
                return;
            }
            last = timer.ElapsedMilliseconds;
            previous = text;
            knl_clear(0xff211c18);
            Text("KINGDOM: NEW LANDS", 22, 22, 0xffa9cce9, 2);
            Rect(20, 46, 280, 1, 0xff6f5942);
            Text("PREPARING GAME FILES", 22, 68, 0xfff3f6f9, 2);
            Text("THIS IS A ONE-TIME PROCESS", 22, 93, 0xffb5bcc6);
            Text(text.Length > 46?text.Substring(0, 46):text, 22, 126);
            Rect(22, 150, 276, 12, 0xff504336);
            Rect(24, 152, (float)(272 * Math.Max(0, Math.Min(1, fraction))), 8, 0xffa9cce9);
            Text(((int)(100 * fraction)) + "%", 22, 176);
            Text(fraction < .98 ? "B TO PAUSE - RESUMES NEXT LAUNCH" : "FINISHING - PLEASE WAIT", 22, 213, 0xffb5bcc6);
            if (knl_present() == 0 && fraction < .98) {
                throw new OperationCanceledException(
                    "Preparation paused. Launch again to resume.");
            }
        }
        public bool Error(Exception error)
        {
            Console.Error.WriteLine(error);
            if (!enabled) {
                return false;
            }
            cancelRequested = false;
            knl_clear(0xff211c18);
            Text("PREPARATION STOPPED", 20, 20, 0xffa9cce9, 2);
            var words = error.Message.Split(' ');
            string line = "";
            int y = 60;
            foreach (var word in words) {
                if (line.Length + word.Length + 1 > 46) {
                    Text(line, 20, y);
                    y += 13;
                    line = "";
                }
                line += (line.Length == 0?"":" ") + word;
                if (y > 175) {
                    break;
                }
            }
            if (line.Length > 0) {
                Text(line.Substring(0, Math.Min(46, line.Length)), 20, y);
            }
            Text("A RETRY     B EXIT", 20, 212, 0xffb5bcc6);
            knl_present();
            uint lastButtons = knl_buttons();
            while (true) {
                Thread.Sleep(50);
                uint b = knl_buttons(), edge = b & ~lastButtons;
                lastButtons = b;
                if ((edge & 16) != 0) {
                    previous = "";
                    return true;
                }
                if ((edge & 32) != 0 || knl_present() == 0) {
                    return false;
                }
            }
        }
        public void Snapshot(string path)
        {
            if (enabled) {
                knl_save_png(path, 2);
            }
        }
        public void Dispose()
        {
            if (enabled) {
                knl_shutdown();
                enabled = false;
            }
        }
    }
}
