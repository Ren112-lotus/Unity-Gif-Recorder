# Unity GIF Recorder

UnityのGameビューを、開始・停止ボタンだけでアニメーションGIFとして保存するEditor拡張です。

- **Gameビューの既存ツールバーと同じ行**、Gizmosの右側に録画ボタンを追加。
- **F9**でも録画開始・終了。録画時間の事前設定は不要。
- ゲーム内のCanvas・HUDを含めて録画。Editorの録画ボタンは映りません。
- FPS・出力サイズ・ループ・保存先を設定可能。
- 外部エンコーダーや追加のUnityパッケージは不要。ゲームのビルドには含まれません。

## インストール

対応確認済み: **Unity 6000.4.1f1 / Windows**。このバージョンを最低対応バージョンに設定しています。他のOS・Unityバージョンは未検証です。

1. **Window → Package Management → Package Manager** を開きます。
2. **＋ → Install package from git URL…** を選択します。
3. 次のURLを貼り付けて **Install** を押します。

```text
https://github.com/Ren112-lotus/Unity-Gif-Recorder.git#v1.0.0
```

Git URLでの導入には、Unityから実行できる[Git](https://git-scm.com/downloads)が必要です。Gitを使わない場合は[Releases](https://github.com/Ren112-lotus/Unity-Gif-Recorder/releases)の `.tgz` をダウンロードし、Package Managerの **＋ → Install package from tarball…** から選択してください。導入後はシーンへのコンポーネント追加などの設定は不要です。

既に `Assets/Tools/GifRecorder` 版を導入している場合は、重複する型・GUIDを避けるため、UnityのProjectウィンドウからそのフォルダーを削除してから導入してください。

## 使い方

1. Playモードに入り、Gameビューを表示します。
2. Gizmosの右隣にある **● GIF録画** または **F9** で開始します。
3. 同じボタン **■ GIF（経過秒数）** または **F9** で停止・保存します。
4. 保存完了後、隣の **⋮ → 保存したGIFを表示** からファイルを確認します。

設定は **Tools → Astral → GIF Recorder** または **⋮ → GIF録画設定…** から開きます。設定画面を閉じても録画は継続します。中止は **⋮ → 録画・保存を中止** です。

| 設定 | 範囲 / 初期値 |
| --- | --- |
| FPS | 5～30 / 15 |
| 出力最大辺 | 160～1280 px / 640 px |
| ループ | ON |
| 保存先 | プロジェクト直下の `Recordings` |

保存先・設定はプロジェクトごとに保持します。録画ファイルはUnityアセットではないため、必要に応じてプロジェクトの `.gitignore` に `/Recordings/` を追加してください。

## 録画時の挙動と制約

- 録画中はGameビューを表示してください。描画されていない間はフレームを取得できません。
- **保存完了を待ってからPlayモードを終了してください。** Playモード終了・アセンブリのリロードでは、未完了の録画・保存を中止します。一時停止時は、その時点までの映像を保存します。
- GIFは音声なし、フレームごとに最大256色です。画質・取得FPSはGameビューのサイズやEditorの負荷に影響されます。
- フレームはOSの一時フォルダーにRGB24で保存し、GIF変換時に1枚ずつ読み込みます。録画時間に比例してディスクを使用します。例: 640×360・15 FPSでは約9.9 MiB/秒の一時データです。保存完了・中止時に削除します。EditorやOSの異常終了では一時データが残る場合があります。
- 出力サイズは最初の描画完了時に決めます。最大化・復元後も録画を継続し、最初のサイズへ拡縮します。画面比率は維持してください。
- 同じ行への配置にはUnity内部の `GameView.DoToolbarGUI` を使用します。Unity更新で内部APIが変わった場合、ツールバーへの追加表示が使えなくなる可能性があります。既存Gameビューには公開のツールバー拡張APIがありません。
- 横幅が狭いGameビューでは、既存のツールバー操作が収まらない場合があります。ウィンドウを広げるか最大化してください。

詳しい実装・ライフサイクルは [GifRecorderUsage.md](GifRecorderUsage.md) を参照してください。

## 開発と検証

パッケージはリポジトリ直下にあります。ローカルで編集する場合はPackage Managerの **Install package from disk…** で、このフォルダーの `package.json` を選択してください。

Unity非依存のエンコーダー検証は .NET 8 SDK とPython/Pillowで実行できます。Pillowは検証専用です。

```sh
dotnet run --project Tests~/EncoderSmoke -- TestResults
python -m pip install Pillow
python Tests~/verify_encoder.py TestResults
```

色・上下方向・LZW辞書の拡張とリセット・フレーム時間・ループ・中止・不正データを確認します。Unity上では、インストール後のコンパイル、実クリックによる開始・停止・GIF保存、5秒を超える継続、最大化と復元、設定画面を閉じた後の継続、既存Statsボタンの動作を確認しています。

## English quick start

Record the Game view as an animated GIF using a start/stop button beside **Gizmos**, in the existing Game view toolbar. No external encoder or runtime setup is needed.

In Unity Package Manager, choose **Install package from git URL…** and paste:

```text
https://github.com/Ren112-lotus/Unity-Gif-Recorder.git#v1.0.0
```

Tested on **Unity 6000.4.1f1, Windows**. Enter Play mode, keep the Game view visible, click **● GIF録画** (or F9) to start, then click again to stop and save. Wait for encoding to finish before exiting Play mode. Settings: **Tools → Astral → GIF Recorder**. Default output: `<project>/Recordings`. The toolbar integration uses a private Unity API and may require updates for future Editor versions.

## License

[MIT](LICENSE.md)

Unity package installation follows Unity's [Git URL installation instructions](https://docs.unity3d.com/6000.4/Documentation/Manual/upm-ui-giturl.html). GIF output follows the [GIF89a specification](https://www.w3.org/Graphics/GIF/spec-gif89a.txt).
