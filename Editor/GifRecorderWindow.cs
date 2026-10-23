using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Astral.EditorTools.GifRecorder
{
    public sealed class GifRecorderWindow : EditorWindow
    {
        const string LayoutPath = "Packages/com.ren112-lotus.gif-recorder/Editor/GifRecorderWindow.uxml";
        VisualElement settings;
        Label status, estimate;
        ProgressBar progress;
        Button reveal;

        [MenuItem("Tools/Astral/GIF Recorder")]
        public static void Open() { GetWindow<GifRecorderWindow>("GIF録画設定").Show(); }
        void OnEnable() { minSize = new Vector2(380, 380); }
        public void CreateGUI()
        {
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            if (layout == null) { rootVisualElement.Add(new Label("GIF RecorderのUXMLが見つかりません。")); return; }
            layout.CloneTree(rootVisualElement);
            settings = rootVisualElement.Q("settings"); status = rootVisualElement.Q<Label>("status");
            estimate = rootVisualElement.Q<Label>("estimate"); progress = rootVisualElement.Q<ProgressBar>("progress");
            reveal = rootVisualElement.Q<Button>("reveal");
            var fps = rootVisualElement.Q<SliderInt>("fps"); fps.value = GifRecorderControls.FramesPerSecond;
            fps.RegisterValueChangedCallback(evt => GifRecorderControls.FramesPerSecond = evt.newValue);
            var edge = rootVisualElement.Q<IntegerField>("maximumEdge"); edge.value = GifRecorderControls.MaximumEdge;
            edge.RegisterValueChangedCallback(evt => GifRecorderControls.MaximumEdge = evt.newValue);
            var repeat = rootVisualElement.Q<Toggle>("loop"); repeat.value = GifRecorderControls.Loop;
            repeat.RegisterValueChangedCallback(evt => GifRecorderControls.Loop = evt.newValue);
            var directory = rootVisualElement.Q<TextField>("directory"); directory.value = GifRecorderControls.OutputDirectory;
            directory.RegisterValueChangedCallback(evt => GifRecorderControls.OutputDirectory = evt.newValue);
            rootVisualElement.Q<Button>("browse").clicked += () =>
            {
                string initial;
                try { initial = GifRecorderControls.AbsoluteDirectory(); } catch { initial = Path.GetDirectoryName(Application.dataPath); }
                string chosen = EditorUtility.OpenFolderPanel("GIFの保存先", initial, "");
                if (!string.IsNullOrEmpty(chosen)) directory.value = chosen;
            };
            reveal.clicked += () => EditorUtility.RevealInFinder(GifRecorderControls.Session.OutputPath);
            rootVisualElement.schedule.Execute(Refresh).Every(100); Refresh();
        }
        void Refresh()
        {
            if (status == null) return;
            var session = GifRecorderControls.Session;
            int maximumEdge = GifRecorderControls.MaximumEdge;
            bool valid = maximumEdge >= 160 && maximumEdge <= 1280;
            if (valid)
            {
                estimate.text = session.IsRecording && session.Width > 0
                    ? $"録画中: {session.Width} × {session.Height} px / 一時データ {session.BufferedBytes / 1048576d:0.0} MiB"
                    : $"出力最大辺: {maximumEdge} px（Gameビューの描画サイズを基準に縮小）";
            }
            else estimate.text = "最大辺は160～1280 pxで指定してください。";
            settings.SetEnabled(!session.IsBusy);
            reveal.SetEnabled(!session.IsBusy && !string.IsNullOrEmpty(session.OutputPath) && File.Exists(session.OutputPath));
            progress.value = session.IsEncoding ? session.Progress * 100 : 0;
            progress.title = session.IsEncoding ? $"GIF保存: {session.Progress:P0}" : $"{session.FrameCount} frames / {session.ElapsedSeconds:0.0} 秒";
            status.text = session.Status;
        }
    }
}
