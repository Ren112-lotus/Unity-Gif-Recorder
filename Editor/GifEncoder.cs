using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Astral.EditorTools.GifRecorder
{
    public sealed class GifFrame
    {
        // RGB24, bottom row first, matching Unity ReadPixels.
        public readonly byte[] Pixels;
        public readonly int DelayCentiseconds;
        public GifFrame(byte[] pixels, int delayCentiseconds)
        { Pixels = pixels; DelayCentiseconds = delayCentiseconds; }
    }

    // GIF89a, per-frame median-cut palettes and LZW. No Unity or external encoder dependency.
    public static class GifEncoder
    {
        const int PaletteSize = 256, HistogramSize = 32768;
        public static void Write(Stream output, int width, int height, IReadOnlyList<GifFrame> frames,
            bool loop, CancellationToken cancellation = default, Action<int> frameWritten = null)
        {
            if (output == null || !output.CanWrite) throw new ArgumentException("A writable stream is required.", nameof(output));
            if (width < 1 || height < 1 || width > ushort.MaxValue || height > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(width));
            int pixelBytes = checked(width * height * 3);
            if (frames == null || frames.Count == 0) throw new ArgumentException("At least one frame is required.", nameof(frames));
            using (var writer = new BinaryWriter(output, Encoding.ASCII, true))
            {
                writer.Write(Encoding.ASCII.GetBytes("GIF89a"));
                writer.Write((ushort)width); writer.Write((ushort)height);
                writer.Write((byte)0x70); writer.Write((byte)0); writer.Write((byte)0);
                if (loop)
                {
                    writer.Write(new byte[] { 0x21, 0xff, 11 }); writer.Write(Encoding.ASCII.GetBytes("NETSCAPE2.0"));
                    writer.Write(new byte[] { 3, 1, 0, 0, 0 });
                }
                for (int i = 0; i < frames.Count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    // A recording can load one frame at a time from disk.
                    var frame = frames[i];
                    if (frame == null || frame.Pixels == null || frame.Pixels.Length != pixelBytes || frame.DelayCentiseconds < 1 || frame.DelayCentiseconds > ushort.MaxValue)
                        throw new ArgumentException("Invalid RGB24 frame size or GIF delay.", nameof(frames));
                    var indexed = Quantize(frame.Pixels, width, height, cancellation, out var palette);
                    writer.Write(new byte[] { 0x21, 0xf9, 4, 4 }); // Keep opaque, full-size frames until replaced.
                    writer.Write((ushort)frame.DelayCentiseconds); writer.Write((byte)0); writer.Write((byte)0);
                    writer.Write((byte)0x2c); writer.Write((ushort)0); writer.Write((ushort)0);
                    writer.Write((ushort)width); writer.Write((ushort)height); writer.Write((byte)0x87);
                    writer.Write(palette); writer.Write((byte)8);
                    WriteLzw(writer, indexed, cancellation);
                    frameWritten?.Invoke(i + 1);
                }
                writer.Write((byte)0x3b);
            }
        }
        static int Key(byte[] pixels, int offset) => (pixels[offset] >> 3) << 10 | (pixels[offset + 1] >> 3) << 5 | pixels[offset + 2] >> 3;
        sealed class ColorBin
        {
            public int Key, Count;
            public long Red, Green, Blue;
            public int Channel(int channel) => (Key >> (10 - channel * 5)) & 31;
        }
        sealed class ColorBox
        {
            public readonly List<ColorBin> Colors;
            public readonly int Channel, Count, Range;
            public ColorBox(List<ColorBin> colors)
            {
                Colors = colors;
                int[] min = { 31, 31, 31 }, max = { 0, 0, 0 };
                foreach (var color in colors)
                {
                    Count += color.Count;
                    for (int channel = 0; channel < 3; channel++)
                    { int value = color.Channel(channel); min[channel] = Math.Min(min[channel], value); max[channel] = Math.Max(max[channel], value); }
                }
                for (int channel = 0; channel < 3; channel++)
                    if (max[channel] - min[channel] > Range) { Range = max[channel] - min[channel]; Channel = channel; }
            }
        }
        static byte[] Quantize(byte[] pixels, int width, int height, CancellationToken cancellation, out byte[] palette)
        {
            var histogram = new ColorBin[HistogramSize]; var colors = new List<ColorBin>();
            for (int offset = 0; offset < pixels.Length; offset += 3)
            {
                if ((offset & 16383) == 0) cancellation.ThrowIfCancellationRequested();
                int key = Key(pixels, offset); var bin = histogram[key];
                if (bin == null) { bin = new ColorBin { Key = key }; histogram[key] = bin; colors.Add(bin); }
                bin.Count++; bin.Red += pixels[offset]; bin.Green += pixels[offset + 1]; bin.Blue += pixels[offset + 2];
            }
            var boxes = new List<ColorBox> { new ColorBox(colors) };
            while (boxes.Count < PaletteSize)
            {
                cancellation.ThrowIfCancellationRequested();
                int selected = -1; long score = -1;
                for (int i = 0; i < boxes.Count; i++)
                {
                    var box = boxes[i]; long candidate = (long)box.Range * box.Count;
                    if (box.Colors.Count > 1 && candidate > score) { selected = i; score = candidate; }
                }
                if (selected < 0) break;
                var split = boxes[selected]; int channel = split.Channel;
                split.Colors.Sort((a, b) => a.Channel(channel).CompareTo(b.Channel(channel)));
                int accumulated = 0, middle = 0;
                do { accumulated += split.Colors[middle++].Count; } while (middle < split.Colors.Count - 1 && accumulated < split.Count / 2);
                boxes[selected] = new ColorBox(split.Colors.GetRange(0, middle));
                boxes.Add(new ColorBox(split.Colors.GetRange(middle, split.Colors.Count - middle)));
            }
            palette = new byte[PaletteSize * 3]; var lookup = new byte[HistogramSize];
            for (int i = 0; i < boxes.Count; i++)
            {
                long red = 0, green = 0, blue = 0;
                foreach (var bin in boxes[i].Colors) { red += bin.Red; green += bin.Green; blue += bin.Blue; lookup[bin.Key] = (byte)i; }
                palette[i * 3] = (byte)(red / boxes[i].Count); palette[i * 3 + 1] = (byte)(green / boxes[i].Count); palette[i * 3 + 2] = (byte)(blue / boxes[i].Count);
            }
            var indexed = new byte[width * height];
            for (int y = 0; y < height; y++)
            {
                cancellation.ThrowIfCancellationRequested();
                for (int x = 0; x < width; x++) indexed[y * width + x] = lookup[Key(pixels, ((height - y - 1) * width + x) * 3)];
            }
            return indexed;
        }
        static void WriteLzw(BinaryWriter writer, byte[] pixels, CancellationToken cancellation)
        {
            const int clear = 256, end = 257, dictionaryLimit = 4096;
            var dictionary = new Dictionary<int, int>(dictionaryLimit);
            var bits = new GifBitWriter(writer); int codeSize = 9, next = 258, prefix = pixels[0];
            bits.Write(clear, codeSize);
            for (int i = 1; i < pixels.Length; i++)
            {
                if ((i & 16383) == 0) cancellation.ThrowIfCancellationRequested();
                int color = pixels[i], key = prefix << 8 | color;
                if (dictionary.TryGetValue(key, out int code)) { prefix = code; continue; }
                bits.Write(prefix, codeSize);
                // The encoder's table is one entry ahead of the decoder's table.
                if (next == 1 << codeSize && codeSize < 12) codeSize++;
                if (next < dictionaryLimit) dictionary.Add(key, next++);
                else { bits.Write(clear, codeSize); dictionary.Clear(); next = 258; codeSize = 9; }
                prefix = color;
            }
            bits.Write(prefix, codeSize);
            if (next == 1 << codeSize && codeSize < 12) codeSize++;
            bits.Write(end, codeSize); bits.Finish();
        }
        sealed class GifBitWriter
        {
            readonly BinaryWriter writer;
            readonly byte[] block = new byte[255];
            int count, pendingBits; uint pending;
            public GifBitWriter(BinaryWriter writer) { this.writer = writer; }
            public void Write(int code, int width)
            {
                pending |= (uint)code << pendingBits; pendingBits += width;
                while (pendingBits >= 8) { Put((byte)pending); pending >>= 8; pendingBits -= 8; }
            }
            void Put(byte value) { block[count++] = value; if (count == block.Length) Flush(); }
            void Flush() { if (count == 0) return; writer.Write((byte)count); writer.Write(block, 0, count); count = 0; }
            public void Finish() { if (pendingBits > 0) Put((byte)pending); Flush(); writer.Write((byte)0); }
        }
    }
}
