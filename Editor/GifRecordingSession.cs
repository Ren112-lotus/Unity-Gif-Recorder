using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Astral.EditorTools.GifRecorder
{
    public sealed class GifRecordingSession : IDisposable
    {
        public bool IsRecording { get; private set; }
        public bool IsEncoding => encoding != null;
        public bool IsBusy => IsRecording || IsEncoding;
        public int FrameCount { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public long BufferedBytes => (long)FrameCount * Width * Height * 3;
        public float Progress => totalFrames == 0 ? 0 : (float)Volatile.Read(ref writtenFrames) / totalFrames;
        public string Status { get; private set; } = "PlayモードでGameビューを表示して録画してください。";
        public string OutputPath { get; private set; }
        public double ElapsedSeconds => IsRecording ? EditorApplication.timeSinceStartup - startedAt : elapsed;
        readonly List<double> timestamps = new List<double>();
        string frameDirectory;
        RenderTexture screenTexture, scaledTexture;
        Texture2D readback;
        GifCapturePump pump;
        GameObject captureHost;
        Task encoding;
        CancellationTokenSource cancellation;
        int sourceWidth, sourceHeight, maximumEdge, fps, totalFrames, writtenFrames;
        double startedAt, nextCaptureAt, elapsed;
        bool loop, previousBackground;

        public GifRecordingSession()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            EditorApplication.pauseStateChanged += PauseChanged;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        }
        public static Vector2Int OutputSize(int sourceWidth, int sourceHeight, int maximumEdge)
        {
            if (sourceWidth < 1 || sourceHeight < 1 || maximumEdge < 160 || maximumEdge > 1280) throw new ArgumentOutOfRangeException(nameof(maximumEdge));
            float scale = Mathf.Min(1, (float)maximumEdge / Mathf.Max(sourceWidth, sourceHeight));
            return new Vector2Int(Mathf.Max(1, Mathf.RoundToInt(sourceWidth * scale)), Mathf.Max(1, Mathf.RoundToInt(sourceHeight * scale)));
        }
        public void Start(string directory, int framesPerSecond, int maximumEdge, bool repeat)
        {
            if (IsBusy) throw new InvalidOperationException("録画・保存が完了するまで待ってください。");
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) throw new InvalidOperationException("Playモードを開始し、一時停止を解除してください。");
            if (framesPerSecond < 5 || framesPerSecond > 30) throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
            if (maximumEdge < 160 || maximumEdge > 1280) throw new ArgumentOutOfRangeException(nameof(maximumEdge));
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("保存先を指定してください。", nameof(directory));
            directory = Path.GetFullPath(directory); Directory.CreateDirectory(directory);
            OutputPath = Path.Combine(directory, "GameView_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".gif");
            Width = Height = sourceWidth = sourceHeight = 0;
            this.maximumEdge = maximumEdge; fps = framesPerSecond; loop = repeat;
            timestamps.Clear(); FrameCount = totalFrames = writtenFrames = 0; elapsed = 0;
            startedAt = nextCaptureAt = EditorApplication.timeSinceStartup;
            previousBackground = Application.runInBackground; Application.runInBackground = true;
            IsRecording = true; Status = "Gameビューの描画を待っています…";
            try
            {
                frameDirectory = Path.Combine(Path.GetTempPath(), "AstralGifRecorder-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(frameDirectory);
                captureHost = new GameObject("GIF Recorder (temporary)") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(captureHost); pump = captureHost.AddComponent<GifCapturePump>();
                if (pump == null) throw new InvalidOperationException("録画用のフレーム取得コンポーネントを作成できませんでした。");
                pump.Capture = CaptureFrame;
            }
            catch { Cancel("録画を開始できませんでした。"); throw; }
        }
        void CaptureFrame()
        {
            if (!IsRecording || EditorApplication.isPaused) return;
            double now = EditorApplication.timeSinceStartup;
            if (now < nextCaptureAt) return;
            try
            {
                // Screen dimensions inside an Editor button callback describe that GUI window.
                // Read the Game view size only here, after its rendered frame has completed.
                if (Screen.width < 1 || Screen.height < 1) return;
                if (screenTexture == null || Screen.width != sourceWidth || Screen.height != sourceHeight)
                    PrepareCapture(Screen.width, Screen.height);
                var previous = RenderTexture.active;
                try
                {
                    ScreenCapture.CaptureScreenshotIntoRenderTexture(screenTexture);
                    // Screen captures follow the graphics API's UV origin; ReadPixels is bottom-up.
                    if (SystemInfo.graphicsUVStartsAtTop)
                        Graphics.Blit(screenTexture, scaledTexture, new Vector2(1, -1), new Vector2(0, 1));
                    else Graphics.Blit(screenTexture, scaledTexture);
                    RenderTexture.active = scaledTexture;
                    readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                    File.WriteAllBytes(FramePath(frameDirectory, FrameCount), readback.GetRawTextureData<byte>().ToArray());
                    timestamps.Add(now - startedAt);
                }
                finally { RenderTexture.active = previous; }
                FrameCount = timestamps.Count; nextCaptureAt = now + 1d / fps;
            }
            catch (Exception error) { Cancel("録画に失敗: " + error.Message); Debug.LogException(error); }
        }
        void PrepareCapture(int width, int height)
        {
            sourceWidth = width; sourceHeight = height;
            if (screenTexture != null) { screenTexture.Release(); Object.DestroyImmediate(screenTexture); }
            screenTexture = new RenderTexture(sourceWidth, sourceHeight, 0, RenderTextureFormat.ARGB32);
            screenTexture.Create();
            if (scaledTexture == null)
            {
                var size = OutputSize(sourceWidth, sourceHeight, maximumEdge);
                Width = size.x; Height = size.y;
                scaledTexture = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
                readback = new Texture2D(Width, Height, TextureFormat.RGB24, false); scaledTexture.Create();
            }
            Status = "録画中 — 終了ボタンで保存します。";
        }
        public void StopAndSave()
        {
            if (!IsRecording) return;
            elapsed = EditorApplication.timeSinceStartup - startedAt; IsRecording = false; ReleaseCapture();
            if (FrameCount == 0)
            {
                DeleteFrames(frameDirectory); frameDirectory = null;
                Status = "フレームを取得できませんでした。Gameビューを表示して再録画してください。"; return;
            }
            var delays = new int[FrameCount];
            double first = timestamps[0]; int previousBoundary = 0;
            for (int i = 0; i < FrameCount; i++)
            {
                double end = i + 1 < FrameCount ? timestamps[i + 1] : elapsed;
                int boundary = (int)Math.Round((end - first) * 100);
                delays[i] = Mathf.Clamp(boundary - previousBoundary, 2, ushort.MaxValue);
                previousBoundary = boundary;
            }
            string spool = frameDirectory; frameDirectory = null;
            var captured = new DiskFrames(spool, delays);
            timestamps.Clear(); totalFrames = captured.Count; writtenFrames = 0;
            cancellation = new CancellationTokenSource(); var token = cancellation.Token;
            string path = OutputPath; int width = Width, height = Height; bool repeat = loop;
            Status = "GIFを保存中…（Playモードを終了しないでください）";
            encoding = Task.Run(() =>
            {
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        GifEncoder.Write(output, width, height, captured, repeat, token, count => Volatile.Write(ref writtenFrames, count));
                    token.ThrowIfCancellationRequested(); File.Move(temporary, path);
                }
                finally { try { if (File.Exists(temporary)) File.Delete(temporary); } finally { DeleteFrames(spool); } }
            }); // Always run the finally block, even when cancelled before the worker starts.
        }
        void Update()
        {
            if (encoding == null || !encoding.IsCompleted) return;
            try { encoding.GetAwaiter().GetResult(); Status = "保存完了: " + OutputPath; }
            catch (OperationCanceledException) { Status = "保存をキャンセルしました。"; }
            catch (Exception error) { Status = "保存に失敗: " + error.Message; Debug.LogException(error); }
            finally { encoding = null; cancellation.Dispose(); cancellation = null; }
        }
        public void Cancel(string reason = "録画・保存をキャンセルしました。")
        {
            bool recording = IsRecording; IsRecording = false;
            if (recording) { elapsed = EditorApplication.timeSinceStartup - startedAt; ReleaseCapture(); }
            cancellation?.Cancel();
            try { encoding?.GetAwaiter().GetResult(); } catch (OperationCanceledException) { } catch (Exception error) { Debug.LogException(error); }
            encoding = null; cancellation?.Dispose(); cancellation = null;
            DeleteFrames(frameDirectory); frameDirectory = null;
            timestamps.Clear(); Status = reason;
        }
        static string FramePath(string directory, int index) => Path.Combine(directory, index.ToString("D8") + ".rgb");
        static void DeleteFrames(string directory)
        {
            if (string.IsNullOrEmpty(directory)) return;
            string fullPath = Path.GetFullPath(directory);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(fullPath).StartsWith("AstralGifRecorder-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected GIF frame directory.");
            if (Directory.Exists(fullPath)) Directory.Delete(fullPath, true);
        }
        sealed class DiskFrames : IReadOnlyList<GifFrame>
        {
            readonly string directory;
            readonly int[] delays;
            public DiskFrames(string directory, int[] delays) { this.directory = directory; this.delays = delays; }
            public int Count => delays.Length;
            public GifFrame this[int index] => new GifFrame(File.ReadAllBytes(FramePath(directory, index)), delays[index]);
            public IEnumerator<GifFrame> GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        void ReleaseCapture()
        {
            if (pump != null) { pump.Capture = null; pump.StopAllCoroutines(); pump = null; }
            if (captureHost != null) { Object.DestroyImmediate(captureHost); captureHost = null; }
            if (screenTexture != null) { screenTexture.Release(); Object.DestroyImmediate(screenTexture); screenTexture = null; }
            if (scaledTexture != null) { scaledTexture.Release(); Object.DestroyImmediate(scaledTexture); scaledTexture = null; }
            if (readback != null) { Object.DestroyImmediate(readback); readback = null; }
            Application.runInBackground = previousBackground;
        }
        void PlayModeChanged(PlayModeStateChange state)
        { if (state == PlayModeStateChange.ExitingPlayMode && IsBusy) Cancel("Playモード終了のため中止しました。次回は先に「録画停止・保存」を押してください。"); }
        void PauseChanged(PauseState state) { if (state == PauseState.Paused && IsRecording) StopAndSave(); }
        void BeforeReload() { Cancel("スクリプト再読み込みのため中止しました。"); }
        public void Dispose()
        {
            Cancel(); EditorApplication.update -= Update; EditorApplication.playModeStateChanged -= PlayModeChanged;
            EditorApplication.pauseStateChanged -= PauseChanged; AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
        }
    }
}
