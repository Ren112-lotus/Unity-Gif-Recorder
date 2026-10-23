# GameビューのGIF録画

録画は **Gameビューの既存ツールバーと同じ行**、Gizmosの右側にある **● GIF録画 / ■ GIF（経過秒数）** から操作する。**F9** でも開始・終了を切り替えられる。設定は **Tools → Astral → GIF Recorder** またはボタン横の **⋮** メニューから開く。Editor専用で、ゲームのビルドには含まれない。外部ツールや追加パッケージは不要。

1. シーンをPlayモードで再生し、Gameビューを表示する。
2. 必要に応じてFPS（5～30）、最大辺（160～1280px）、ループ、保存先を設定する。縦横比はGameビューに合わせ、元映像より拡大しない。設定はプロジェクト別にEditorPrefsへ保存する。
3. ツールバーにある **● GIF録画** または **F9** を押してゲームを操作する。録画時間は指定せず、手動で終了するまで録画を続ける。設定画面は閉じてもよい。
4. **■ GIF（経過秒数）** または **F9** で終了し、バックグラウンドでGIFを保存する。保存中は同じボタンに進捗を表示する。
5. **⋮ → 保存したGIFを表示** で保存したファイルを開く。録画・保存を破棄するときは **⋮ → 録画・保存を中止**。

録画ツールバーはGameビューのEditor UIとして表示し、ゲームの描画領域へUIを追加しない。ツールバーはGIFに写り込まない。Gameビューを開き直しても自動で表示する。

保存先の初期値はプロジェクト直下の `Recordings`。必要に応じてプロジェクトのGit除外設定へ追加する。相対パスはプロジェクトルート基準。ファイル名には日時と一意の識別子を付け、既存ファイルを上書きしない。保存途中は同じフォルダーの一時ファイルへ書き、完了時だけ `.gif` として公開する。

Gameビューの描画完了後に取得するため、Canvas・デバッグメニューなどのゲーム内UIも含む。録画サイズは最初の描画完了時に決定する。Editorのボタンコールバック内では `Screen.width/height` がボタンのあるウィンドウのサイズを返すため、録画開始時には参照しない。ゲームの時間・フレームレート設定は変更しない。録画中だけバックグラウンド実行を有効にし、停止時に元の値へ戻す。ゲームやEditorの負荷で実際の取得FPSが下がった場合も、各フレームの実時間差をGIFの表示時間へ反映する。

GIFは音声なし・各フレーム最大256色。各フレームの色に合わせたパレットを生成し、LZWで圧縮する。録画フレームはOSの一時フォルダー内の専用ディレクトリへ保存し、変換時は1枚ずつ読み込む。録画時間に比例して画像をメモリへ保持しない。時間制限・従来の192 MiB制限は設けず、利用可能なディスク容量の範囲で録画する。設定画面には一時データの量を表示する。一時フレームは保存完了・中止時に削除する。

録画中はGameビューを表示しておく。Gameビューが描画されない間は取得できない。最大化・復元・解像度変更でも録画を続け、映像を最初に決めた出力サイズへ拡縮する。画面比率も変えると縦横の拡縮率が変わるため、比率は維持する。一時停止では、その時点までの映像を保存する。Playモード終了・スクリプトの再読み込みでは、未完了の録画と保存を中止し、一時オブジェクト・フレーム・一時ファイルを解除する。設定画面の開閉は録画に影響しない。Playモードは保存完了後に終了する。

## 構成と再検証

- `Editor/GifGameViewControls`：Gameビューの既存ツールバーと同じ行に表示する操作ボタン。既存操作はUnity内部の `DoToolbarGUI` をIMGUIContainerへバインドしてそのまま使い、右端にUI ToolkitのGIFボタンとメニューを並べる。公開の拡張口がないため、この部分はUnity内部APIに依存する。6000.4.1f1で確認済み。Unity更新で内部APIが変わった場合は追加UIを表示せず、既存ツールバーを維持する。ゲーム画像の外側のEditorレイヤーに表示する。
- `Editor/GifRecorderControls`：F9・共有する録画セッションと設定。設定画面から独立して録画を継続する。
- `Editor/GifRecorderWindow`：UI ToolkitのEditor設定画面。FPS・最大辺・ループ・保存先・進捗・ファイル表示。
- `Editor/GifRecordingSession` / `Capture/GifCapturePump`：画面取得・実時間・一時ファイルとUnityオブジェクトの後始末。PumpはEditorでだけ定義し、録画時に一時生成する。
- `Editor/GifEncoder`：Unityに依存しないRGB24→GIF89a変換。下から並ぶ画素を上からの行順へ変換する。
- `Tests~/EncoderSmoke` / `Tests~/verify_encoder.py`：Unityに依存しないエンコーダーの合成画像をPillowで独立にデコードし、色・上下方向・LZW・フレーム時間・ループを検証する。実行方法はREADMEを参照。

形式は [GIF89a仕様](https://www.w3.org/Graphics/GIF/spec-gif89a.txt)、画面取得はUnityの [ScreenCapture](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ScreenCapture.CaptureScreenshotIntoRenderTexture.html) と [WaitForEndOfFrame](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/WaitForEndOfFrame.html) を参照。
