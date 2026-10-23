using System;
using System.IO;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Astral.EditorTools.GifRecorder
{
    [InitializeOnLoad]
    public static class GifRecorderControls
    {
        static readonly string PreferencePrefix = "Astral.GifRecorder." + Application.dataPath + ".";
        public static GifRecordingSession Session { get; } = new GifRecordingSession();
        public static int FramesPerSecond { get => EditorPrefs.GetInt(PreferencePrefix + "FPS", 15); set => EditorPrefs.SetInt(PreferencePrefix + "FPS", value); }
        public static int MaximumEdge { get => EditorPrefs.GetInt(PreferencePrefix + "Size", 640); set => EditorPrefs.SetInt(PreferencePrefix + "Size", value); }
        public static bool Loop { get => EditorPrefs.GetBool(PreferencePrefix + "Loop", true); set => EditorPrefs.SetBool(PreferencePrefix + "Loop", value); }
        public static string OutputDirectory { get => EditorPrefs.GetString(PreferencePrefix + "Directory", "Recordings"); set => EditorPrefs.SetString(PreferencePrefix + "Directory", value); }
        public static bool ValidSettings => FramesPerSecond >= 5 && FramesPerSecond <= 30 && MaximumEdge >= 160 && MaximumEdge <= 1280 && !string.IsNullOrWhiteSpace(OutputDirectory);
        public static bool CanStart => !Session.IsBusy && EditorApplication.isPlaying && !EditorApplication.isPaused && ValidSettings;
        [Shortcut("Astral/GIF Recording", KeyCode.F9)]
        public static void ToggleRecording()
        {
            if (Session.IsRecording) Session.StopAndSave();
            else if (CanStart)
            {
                try { Session.Start(AbsoluteDirectory(), FramesPerSecond, MaximumEdge, Loop); }
                catch (Exception error) { Session.Cancel(error.Message); Debug.LogError(error.Message); }
            }
        }
        public static string AbsoluteDirectory()
        {
            if (string.IsNullOrWhiteSpace(OutputDirectory)) throw new ArgumentException("保存先を入力してください。");
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), OutputDirectory));
        }
    }
}
