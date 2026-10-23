using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Astral.EditorTools.GifRecorder
{
    // The native Game view toolbar is IMGUI and has no public extension callback.
    // Reuse that toolbar's own method in a clipped, flex-sized slot, then append GIF controls.
    // This Editor layer is outside the rendered game image and is never included in the GIF.
    [InitializeOnLoad]
    public static class GifGameViewControls
    {
        const string LayoutPath = "Packages/com.ren112-lotus.gif-recorder/Editor/GifGameViewControls.uxml";
        const string StylePath = "Packages/com.ren112-lotus.gif-recorder/Editor/GifGameViewControls.uss";
        const string RootName = "astral-gif-game-controls";
        static readonly Type GameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        static readonly MethodInfo NativeToolbar = GameViewType?.GetMethod("DoToolbarGUI", BindingFlags.Instance | BindingFlags.NonPublic);
        static VisualTreeAsset layout;
        static StyleSheet style;
        static double nextUpdate;

        static GifGameViewControls()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += RemoveControls;
        }
        static void Update()
        {
            if (NativeToolbar == null || EditorApplication.timeSinceStartup < nextUpdate) return;
            nextUpdate = EditorApplication.timeSinceStartup + .1;
            foreach (EditorWindow view in Resources.FindObjectsOfTypeAll(GameViewType))
            {
                var root = view.rootVisualElement.Q(RootName);
                if (root == null)
                {
                    if (layout == null) layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
                    if (style == null) style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
                    if (layout == null || style == null) continue;
                    root = layout.CloneTree().Q(RootName);
                    var native = root?.Q<IMGUIContainer>("native-toolbar");
                    var button = root?.Q<Button>("record");
                    var more = root?.Q<UnityEditor.UIElements.ToolbarMenu>("more");
                    // Scripts can reload before the matching UXML has finished importing.
                    if (native == null || button == null || more == null) continue;
                    root.styleSheets.Add(style);
                    // Bind once: native popups and GUIUtility.ExitGUI retain their normal behaviour.
                    native.onGUIHandler = (Action)NativeToolbar.CreateDelegate(typeof(Action), view);
                    button.clicked += GifRecorderControls.ToggleRecording;
                    var menu = more.menu;
                    menu.AppendAction("録画・保存を中止", _ => GifRecorderControls.Session.Cancel(), _ => GifRecorderControls.Session.IsBusy ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                    menu.AppendAction("保存したGIFを表示", _ => EditorUtility.RevealInFinder(GifRecorderControls.Session.OutputPath), _ => HasSavedFile ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                    menu.AppendSeparator();
                    menu.AppendAction("GIF録画設定…", _ => GifRecorderWindow.Open());
                    view.rootVisualElement.Add(root);
                }
                var session = GifRecorderControls.Session;
                var record = root.Q<Button>("record");
                record.text = session.IsRecording ? $"■ GIF {session.ElapsedSeconds:0}s" : session.IsEncoding ? $"GIF {session.Progress:P0}" : "● GIF録画";
                record.tooltip = session.IsRecording ? "録画終了・保存（F9）" : session.IsEncoding ? session.Status : "録画開始（F9）";
                record.SetEnabled(session.IsRecording || GifRecorderControls.CanStart);
                root.Q<UnityEditor.UIElements.ToolbarMenu>("more").tooltip = session.Status + "\n中止・保存したGIF・設定";
            }
        }
        static bool HasSavedFile => !GifRecorderControls.Session.IsBusy && !string.IsNullOrEmpty(GifRecorderControls.Session.OutputPath) && File.Exists(GifRecorderControls.Session.OutputPath);
        static void RemoveControls()
        {
            if (GameViewType == null) return;
            foreach (EditorWindow view in Resources.FindObjectsOfTypeAll(GameViewType)) view.rootVisualElement.Q(RootName)?.RemoveFromHierarchy();
        }
    }
}
