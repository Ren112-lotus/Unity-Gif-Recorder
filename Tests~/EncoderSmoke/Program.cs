using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Astral.EditorTools.GifRecorder;
static class Program
{
    static string DirectoryPath;
    static void Main(string[] args)
    {
        DirectoryPath = Path.GetFullPath(args.Length == 0 ? "TestResults" : args[0]);
        Directory.CreateDirectory(DirectoryPath);
        const int width = 257, height = 129;
        var frames = new List<GifFrame>(); var random = new System.Random(892);
        foreach (int delay in new[] { 7, 11, 19 })
        {
            var pixels = new byte[width * height * 3];
            for (int offset = 0; offset < pixels.Length; offset += 3)
            {
                int index = random.Next(256);
                pixels[offset] = (byte)((index >> 5) * 32 + 16);
                pixels[offset + 1] = (byte)(((index >> 2) & 7) * 32 + 16);
                pixels[offset + 2] = (byte)((index & 3) * 64 + 32);
            }
            File.WriteAllBytes(Path.Combine(DirectoryPath, $"expected-{frames.Count}.rgb"), pixels);
            frames.Add(new GifFrame(pixels, delay));
        }
        using (var file = File.Create(Path.Combine(DirectoryPath, "exact.gif"))) GifEncoder.Write(file, width, height, frames, true);
        using (var file = File.Create(Path.Combine(DirectoryPath, "single.gif")))
            GifEncoder.Write(file, 1, 1, new[] { new GifFrame(new byte[] { 31, 137, 241 }, 12) }, false);
        var gradient = new byte[128 * 64 * 3];
        for (int y = 0; y < 64; y++) for (int x = 0; x < 128; x++)
        { int offset = (y * 128 + x) * 3; gradient[offset] = (byte)(x * 2); gradient[offset + 1] = (byte)(y * 4); gradient[offset + 2] = (byte)((x + y) & 255); }
        File.WriteAllBytes(Path.Combine(DirectoryPath, "gradient.rgb"), gradient);
        using (var file = File.Create(Path.Combine(DirectoryPath, "gradient.gif")))
            GifEncoder.Write(file, 128, 64, new[] { new GifFrame(gradient, 10) }, false);
        bool cancellation = false, invalid = false;
        using (var memory = new MemoryStream())
        {
            try { GifEncoder.Write(memory, width, height, frames, true, new CancellationToken(true)); }
            catch (OperationCanceledException) { cancellation = true; }
            try { GifEncoder.Write(memory, 2, 2, new[] { new GifFrame(new byte[3], 0) }, false); }
            catch (ArgumentException) { invalid = true; }
        }
        if (!cancellation || !invalid) throw new Exception("Encoder validation failed.");
        Console.WriteLine("Encoder cancellation and invalid-frame checks passed. Fixtures: " + DirectoryPath);
    }
}
