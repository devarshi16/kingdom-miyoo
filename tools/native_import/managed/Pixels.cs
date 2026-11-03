using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
namespace KNL.Import
{
    sealed class Image
    {
        public int Width, Height;
        public byte[] Pixels;
        public Image(int w, int h)
        {
            if (w < 1 || h < 1 || w > 8192 || h > 8192 ||
                (long)w * h >
                16777216) {
                throw new InvalidDataException("Texture dimensions out of range");
            }
            Width = w;
            Height = h;
            Pixels = new byte[checked (w * h * 4)];
        }
    }
    static class Pixels
    {
        public static Dictionary<string, object> D(object v)
        {
            return (Dictionary<string, object>)v;
        }
        public static int I(object v)
        {
            return Convert.ToInt32(v);
        }
        public static double F(object v)
        {
            return Convert.ToDouble(v);
        }
        static byte[] Bytes(object v)
        {
            var b = v as byte[];
            return b ?? ((List<object>)v).Select(x => (byte)Convert.ToInt32(x)).ToArray();
        }
        public static byte[] Resource(string source, object stream, byte[] embedded)
        {
            var d = D(stream);
            long n = Convert.ToInt64(d.ContainsKey("size")?d["size"]:d["m_Size"]);
            if (n == 0) {
                return embedded;
            }
            if (n < 0 || n > 512 * 1024 * 1024) {
                throw new InvalidDataException("Resource size out of range");
            }
            string name = Convert.ToString(d.ContainsKey("path")?d["path"]:d["m_Source"]);
            long offset = Convert.ToInt64(d.ContainsKey("offset")?d["offset"]:d["m_Offset"]);
            name = Path.GetFileName(name.Replace('\\', '/'));
            if (string.IsNullOrEmpty(name)) {
                throw new InvalidDataException("Empty resource path");
            }
            using (var r = File.OpenRead(Path.Combine(source, name))){
                if (offset < 0 || offset + n > r.Length) {
                    throw new InvalidDataException(
                        "Resource is missing or truncated: " + name);
                }
                r.Position = offset;
                var b = new byte[(int)n];
                int pos = 0;
                while (pos < b.Length) {
                    int got = r.Read(b, pos, b.Length - pos);
                    if (got == 0) {
                        throw new EndOfStreamException();
                    }
                    pos += got;
                }
                return b;
            }
        }
        public static Image Texture(string source, Dictionary<string, object> d)
        {
            int w = I(d["m_Width"]), h = I(d["m_Height"]), format = I(d["m_TextureFormat"]);
            var im = new Image(w, h);
            var streamData = D(d["m_StreamData"]);
            // RGBA atlases dominate peak memory. Read their first mip directly into the
            // output buffer instead of retaining a second full atlas (plus mip levels).
            if (format == 4 && Convert.ToInt64(streamData["size"]) > 0)
            {
                long offset = Convert.ToInt64(streamData["offset"]);
                long declared = Convert.ToInt64(streamData["size"]);
                string name = Path.GetFileName(((string)streamData["path"]).Replace('\\', '/'));
                using (var input = File.OpenRead(Path.Combine(source, name)))
                {
                    if (offset < 0 || declared < im.Pixels.Length || offset + declared > input.Length)
                        throw new InvalidDataException("Texture stream is missing or truncated: " + name);
                    input.Position = offset;
                    int at = 0;
                    while (at < im.Pixels.Length)
                    {
                        int got = input.Read(im.Pixels, at, im.Pixels.Length - at);
                        if (got == 0) throw new EndOfStreamException();
                        at += got;
                    }
                }
                return im;
            }
            var b = Resource(source, streamData, Bytes(d["image data"]));
            int n = w * h;
            if (format == 4) {
                if (b.Length < n * 4) {
                    throw new EndOfStreamException();
                }
                Buffer.BlockCopy(b, 0, im.Pixels, 0, n * 4);
            }
            else if (format == 1 || format == 3) {
                int step = format == 1?1:3;
                if (b.Length < n * step) {
                    throw new EndOfStreamException();
                }
                for (int i = 0; i < n; i++) {
                    int j = i * 4;
                    im.Pixels[j] = format == 1?(byte)0:b[i * 3];
                    im.Pixels[j + 1] = format == 1?(byte)0:b[i * 3 + 1];
                    im.Pixels[j + 2] = format == 1?(byte)0:b[i * 3 + 2];
                    im.Pixels[j + 3] = format == 1?b[i]:(byte)255;
                }
            }
            else if (format == 10 || format == 12) {
                Blocks(im, b, format == 12);
            }
            else{
                throw new InvalidDataException("Unsupported texture format " + format);
            }
            return im;
        }
        static void Color(byte[,] c, int row, int bits)
        {
            int red = (bits >> 11) & 31, green = (bits >> 5) & 63, blue = bits & 31;
            c[row, 0] = (byte)((red << 3) | (red >> 2));
            c[row, 1] = (byte)((green << 2) | (green >> 4));
            c[row, 2] = (byte)((blue << 3) | (blue >> 2));
            c[row, 3] = 255;
        }
        static void Blocks(Image im, byte[] b, bool alpha)
        {
            int off = 0, block = alpha?16:8;
            for (int y = 0; y < im.Height; y += 4) {
                for (int x = 0; x < im.Width; x += 4) {
                    if (off + block > b.Length) {
                        throw new EndOfStreamException();
                    }
                    var a = new byte[8];
                    ulong ab = 0;
                    int cb = off;
                    if (alpha) {
                        a[0] = b[off];
                        a[1] = b[off + 1];
                        for (int k = 2; k < 8; k++) {
                            if (a[0] > a[1]) {
                                a[k] = (byte)(((8 - k) * a[0] + (k - 1) * a[1]) / 7);
                            }
                            else if (k < 6) {
                                a[k] = (byte)(((6 - k) * a[0] + (k - 1) * a[1]) / 5);
                            }
                            else{
                                a[k] = (byte)(k == 6?0:255);
                            }
                        }
                        for (int k = 0; k < 6; k++) {
                            ab |= (ulong)b[off + 2 + k] << (8 * k);
                        }
                        cb += 8;
                    }
                    int c0 = b[cb] | b[cb + 1] << 8, c1 = b[cb + 2] | b[cb + 3] << 8;
                    uint bits = BitConverter.ToUInt32(b, cb + 4);
                    var c = new byte[4, 4];
                    Color(c, 0, c0);
                    Color(c, 1, c1);
                    for (int ch = 0; ch < 3; ch++) {
                        if (c0 > c1 || alpha) {
                            c[2, ch] = (byte)((2 * c[0, ch] + c[1, ch]) / 3);
                            c[3, ch] = (byte)((c[0, ch] + 2 * c[1, ch]) / 3);
                        }
                        else{
                            c[2, ch] = (byte)((c[0, ch] + c[1, ch]) / 2);
                        }
                    }
                    c[2, 3] = 255;
                    c[3, 3] = (byte)((c0 > c1 || alpha)?255:0);
                    for (int k = 0; k < 16; k++) {
                        int xx = x + k % 4, yy = y + k / 4;
                        if (xx >= im.Width || yy >= im.Height) {
                            continue;
                        }
                        int dst = (yy * im.Width + xx) * 4, index = (int)((bits >> (2 * k)) & 3);
                        for (int ch = 0; ch < 4; ch++) {
                            im.Pixels[dst + ch] = c[index, ch];
                        }
                        if (alpha) {
                            im.Pixels[dst + 3] = a[(int)((ab >> (3 * k)) & 7)];
                        }
                    }
                    off += block;
                }
            }
        }
        public static void Flip(Image im)
        {
            int stride = im.Width * 4;
            var row = new byte[stride];
            for (int y = 0; y < im.Height / 2; y++) {
                int a = y * stride, b = (im.Height - y - 1) * stride;
                Buffer.BlockCopy(im.Pixels, a, row, 0, stride);
                Buffer.BlockCopy(im.Pixels, b, im.Pixels, a, stride);
                Buffer.BlockCopy(row, 0, im.Pixels, b, stride);
            }
        }
        public static Image Sprite(Image atlas, Dictionary<string, object> d)
        {
            var rd = D(d["m_RD"]);
            int settings = I(rd["settingsRaw"]);
            if (((settings >> 2) & 15) != 0 ||
                Convert.ToInt64(D(rd["alphaTexture"])["m_PathID"]) != 0) {
                throw new InvalidDataException(
                    "Unsupported sprite packing/alpha texture");
            }
            var r = D(rd["textureRect"]);
            int x = (int)Math.Round(F(r["x"])), y = (int)Math.Round(F(r["y"])), w = (int)Math.Round(F(
                r[
                    "x"
                ]) + F(
                        r[
                            "width"
                        ]))
                - x,
                h = (int)Math.Round(F(r["y"]) + F(r["height"])) - y;
            var im = new Image(w, h);
            for (int row = 0; row < h; row++) {
                if (x < 0 || y + row < 0 || x + w > atlas.Width ||
                    y + row >= atlas.Height) {
                    throw new InvalidDataException(
                        "Sprite rectangle outside atlas");
                }
                Buffer.BlockCopy(atlas.Pixels, ((y + row) * atlas.Width + x) * 4, im.Pixels, row * w * 4, w * 4);
            }
            if ((settings & 2) == 0) {
                Mask(im, rd, F(d["m_PixelsToUnits"]));
            }
            Flip(im);
            return im;
        }
        static void Mask(Image im, Dictionary<string, object> rd, double factor)
        {
            var vd = D(rd["m_VertexData"]);
            var channels = ((List<object>)vd["m_Channels"]).Select(D).ToArray();
            var position = channels[0];
            if (I(position["stream"]) != 0 || I(position["format"]) != 0 ||
                I(position["dimension"]) != 3) {
                throw new InvalidDataException(
                    "Unsupported sprite vertex layout");
            }
            int stride = 0;
            foreach (var c in channels) {
                if (I(c["stream"]) == 0) {
                    if (I(c["format"]) != 0) {
                        throw new InvalidDataException("Unsupported vertex format");
                    }
                    stride += 4 * I(c["dimension"]);
                }
            }
            var raw = Bytes(vd["m_DataSize"]);
            int count = I(vd["m_VertexCount"]);
            double[] xs = new double[count], ys = new double[count];
            for (int i = 0; i < count; i++) {
                int off = i * stride + I(position["offset"]);
                xs[i] = BitConverter.ToSingle(raw, off);
                ys[i] = BitConverter.ToSingle(raw, off + 4);
            }
            double minX = xs.Min(), minY = ys.Min();
            int[] vx = xs.Select(v => (int)((v - minX) * factor)).ToArray(),
            vy = ys.Select(v => (int)((v - minY) * factor)).ToArray();
            var indices = Bytes(rd["m_IndexBuffer"]);
            var mask = new bool[im.Width * im.Height];
            foreach (var sm in ((List<object>)rd["m_SubMeshes"]).Select(D)) {
                int offset = I(sm["firstByte"]), n = I(sm["indexCount"]);
                if (I(sm["topology"]) != 0 || n % 3 != 0) {
                    throw new InvalidDataException(
                        "Unsupported sprite topology");
                }
                for (int i = 0; i < n; i += 3) {
                    int a = BitConverter.ToUInt16(indices, offset + i * 2), b = BitConverter.ToUInt16(
                        indices, offset + (i + 1) * 2), c = BitConverter.ToUInt16(indices, offset + (i + 2) * 2);
                    Triangle(mask, im.Width, im.Height, new[] {vx[a], vx[b], vx[c]}, new[] {vy[a], vy[b],
                                                                                            vy[c]});
                }
            }
            for (int i = 0; i < mask.Length;
                i++) {
                if (!mask[i]) {
                    for (int c = 0; c < 4; c++) {
                        im.Pixels[i * 4 + c] = 0;
                    }
                }
            }
        }
// Filled triangle scan conversion follows Pillow's inclusive integer polygon convention.
// Adapted from Pillow Draw.c (HPND); see THIRD_PARTY_NOTICES.md and the retained license.
        sealed class Edge
        {
            public int X, Y, MinY, MaxY, MinX, MaxX;
            public float Dx;
        }
        static int Round(float x)
        {
            return (int)Math.Round(x, MidpointRounding.AwayFromZero);
        }
        static int Up(float x)
        {
            return x >= 0?(int)Math.Floor(x + .5f):-(int)Math.Floor(Math.Abs(x) + .5f);
        }
        static int Down(float x)
        {
            return x >= 0?(int)Math.Ceiling(x - .5f):-(int)Math.Ceiling(Math.Abs(x) - .5f);
        }
        static void Line(bool[] mask, int w, int h, int y, int x0, int x1)
        {
            if (y < 0 || y >= h) {
                return;
            }
            for (int x = Math.Max(0, x0); x <= Math.Min(w - 1, x1); x++) {
                mask[y * w + x] = true;
            }
        }
        static void Triangle(bool[] mask, int w, int h, int[] x, int[] y)
        {
            var edges = new List<Edge>();
            int minY = h - 1, maxY = 0;
            for (int i = 0; i < 3; i++) {
                int next = (i + 1) % 3;
                var e = new Edge {
                    X = x[i], Y = y[i], MinY = Math.Min(y[i], y[next]), MaxY = Math.Max(y[i], y[next]),
                    MinX = Math.Min(x[i], x[next]), MaxX = Math.Max(x[i], x[next])
                };
                e.Dx = y[next] == y[i]?0:(float)(x[next] - x[i]) / (y[next] - y[i]);
                minY = Math.Min(minY, e.MinY);
                maxY = Math.Max(maxY, e.MaxY);
                if (e.MinY == e.MaxY) {
                    Line(mask, w, h, e.MinY, e.MinX, e.MaxX);
                }
                else{
                    edges.Add(e);
                }
            }
            var crossings = new List<float>(6);
            for (int row = Math.Max(0, minY); row <= Math.Min(h, maxY); row++) {
                crossings.Clear();
                for (int i = 0; i < edges.Count; i++) {
                    var e = edges[i];
                    if (row < e.MinY || row > e.MaxY) {
                        continue;
                    }
                    float p = (row - e.Y) * e.Dx + e.X;
                    crossings.Add(p);
                    if (row == e.MaxY && row < maxY) {
                        crossings.Add(p);
                    }
                    else if ((row == e.MinY || row == e.MaxY) && e.Dx != 0) {
                        for (int k = 0; k < i; k++) {
                            var other = edges[k];
                            if ((row != other.MinY && row != other.MaxY) || other.Dx == 0) {
                                continue;
                            }
                            if (Round(p) != Round((row - other.Y) * other.Dx + other.X)) {
                                continue;
                            }
                            int delta = row == e.MaxY?-1:1;
                            float adjacent = (row + delta - e.Y) * e.Dx + e.X;
                            if (row + delta >= other.MinY && row + delta <= other.MaxY) {
                                float second = (row + delta - other.Y) * other.Dx + other.X;
                                if (p > adjacent + 1 &&
                                    p > second + 1) {
                                    p = Round(Math.Max(adjacent, second)) + 1;
                                }
                                else if (p < adjacent - 1 &&
                                    p < second - 1) {
                                    p = Round(Math.Min(adjacent, second)) - 1;
                                }
                                crossings[crossings.Count - 1] = p;
                                break;
                            }
                        }
                    }
                }
                crossings.Sort();
                for (int i = 1; i < crossings.Count; i += 2) {
                    Line(mask, w, h, row, Up(crossings[i - 1]), Down(crossings[i]));
                }
            }
        }
        public static void WriteRaw(string path, Image im)
        {
            using (var w = new BinaryWriter(File.Create(path))){
                w.Write(im.Width);
                w.Write(im.Height);
                w.Write(im.Pixels);
            }
        }
    }
}
