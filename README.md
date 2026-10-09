# Digital Manometer Pressure Monitor

デジタルマノメータの0-10 Vアナログ出力をNI USB-6000で読み取り、圧力をリアルタイム表示、グラフ表示、CSV保存するWindowsデスクトップソフトです。

PCを買ったばかりでVisual Studioも入っていない状態から使えるように、必要なソフト、装置、配線、ビルド、操作手順をこのREADMEにまとめています。

## 何ができるか

- NI USB-6000のアナログ入力を自動検出する
- 圧力を常時リアルタイム表示する
- 1000 Hzのハードウェアタイミングで測定する
- 0-10 Vから圧力への2点直線換算を設定できる
- 手動停止またはタイマー停止でCSV保存する
- 測定中の圧力をグラフ表示する
- 測定停止後もライブ監視へ自動復帰する

CSVには測定条件のメタデータと、1 ms間隔の圧力データが保存されます。

```text
# Digital Manometer Pressure Measurement
# MeasurementStartLocal,2026-10-05T14:13:00.0000000+09:00
# AIChannel,Dev1/ai0
# TerminalConfiguration,RSE
# SampleRate_Hz,1000
ElapsedTime_s,Pressure_Pa
0.000000,123.456000
0.001000,124.001000
```

既存ファイルは上書きしません。

## 専用の装置か

このソフトは、0-10 Vのアナログ電圧を出力するデジタルマノメータを、NI USB-6000シリーズのアナログ入力で読む構成を想定しています。

準備するもの:

- 0-10 Vアナログ出力付きのデジタルマノメータ
- NI USB-6000シリーズ、またはNI-DAQmxで同等に扱えるアナログ入力デバイス
- Windows 10またはWindows 11の64 bit PC
- マノメータ出力とNI端子をつなぐ信号線
- CSV保存先フォルダ

このソフトはNI-DAQmxを使います。NI以外のDAQや、シリアル通信式のマノメータにはそのままでは使えません。

## 新品PCで最初に入れるもの

1. Windows Updateを実行する。
2. Visual Studio 2022をインストールする。
3. Visual Studio Installerで `.NET desktop development` ワークロードを選ぶ。
4. .NET 8 SDKをインストールする。Visual Studioのワークロードに含まれていれば追加作業は不要です。
5. NI-DAQmxをインストールする。
6. NI MAXを起動し、USB-6000が認識されていることを確認する。

NI MAXは必須ではありませんが、装置名、AIチャンネル、配線確認に便利なので強く推奨します。

## 配線

このソフトはRSE入力を使います。

```text
マノメータのアナログ出力信号  -> NI USB-6000のAIx端子 例: AI0
マノメータのアナログ出力GND   -> NI USB-6000のAI GND
```

DAQの入力レンジはソフト側で -10 Vから+10 Vに固定しています。圧力換算はGUIの `Linear Pressure Scale` で設定します。

初期値:

- Voltage Low = 0 V
- Voltage High = 10 V
- Pressure at Low = 0 Pa
- Pressure at High = 100000 Pa

圧力計の仕様が違う場合は、この4項目を実機の校正値に合わせてください。

## ダウンロードして開く

1. GitHubページの `Code` からZIPをダウンロードする、またはGitでcloneする。
2. ZIPの場合は任意の作業フォルダへ展開する。
3. `DigitalManometerMonitor.sln` をVisual Studio 2022で開く。
4. 画面上部の構成を `Release`、プラットフォームを `x64` にする。
5. スタートアッププロジェクトが `DigitalManometerMonitor` であることを確認する。
6. `Build` -> `Build Solution` を実行する。
7. `Start` またはF5で起動する。

PowerShellでビルドする場合:

```powershell
.\build-release.ps1
```

ビルド後の実行ファイルは通常、次の場所に作成されます。

```text
src\DigitalManometerMonitor\bin\x64\Release\net8.0-windows\DigitalManometerMonitor.exe
```

## 使い方

1. NI USB-6000をPCに接続する。
2. NI MAXでデバイスが見えることを確認する。
3. マノメータの0-10 V出力をNI USB-6000へ配線する。
4. ソフトを起動する。
5. 起動時にAIチャンネルが自動検出され、利用可能ならライブ監視が始まる。
6. 必要に応じて `Detect / Refresh DAQ` を押す。
7. `DAQ Input` で使うAIチャンネルを選ぶ。
8. `Linear Pressure Scale` をマノメータの仕様に合わせる。
9. CSVの保存先フォルダとファイル名を設定する。
10. `Manual` または `Timer (s)` を選ぶ。
11. `Start Measurement` を押してCSV保存を開始する。
12. Manualの場合は `Stop Measurement` で終了する。

測定中は同じAIチャンネルを測定タスクが使います。測定が止まると、ライブ監視タスクが自動で再開します。

## 実験前の確認

- NI MAXでUSB-6000が認識されている
- 選んだAIチャンネルとAI GNDの配線が正しい
- 既知の0 V、5 V、10 V入力に対して圧力表示が期待通り変わる
- 1000 S/sで安定して測定できる
- CSVの時刻が0.001 s刻みになっている
- マノメータの校正値をGUIへ正しく入力している

## テスト

実機なしで、圧力換算、DAQレンジ、タイマー、CSV形式、上書き防止、ライブ監視の自己テストを実行できます。

```powershell
.\run-self-tests.ps1
```

または:

```powershell
dotnet run --project .\tests\DigitalManometerMonitor.SelfTests\DigitalManometerMonitor.SelfTests.csproj
```

Pythonが入っている場合は静的チェックも実行できます。

```powershell
python .\tests\static_source_checks.py
```

## トラブルシュート

- DAQが検出されない場合: NI-DAQmx、USB接続、NI MAXでの認識を確認してください。
- 圧力が0付近から動かない場合: 信号線とAI GNDの配線、マノメータ側のアナログ出力設定を確認してください。
- 値が大きくずれる場合: `Linear Pressure Scale` の4項目がマノメータ仕様と合っているか確認してください。
- CSVが作れない場合: 保存先フォルダの書き込み権限と、同名ファイルが既に存在しないか確認してください。
- 測定開始時にエラーが出る場合: 他のソフトやNI MAXのテストパネルが同じAIチャンネルを使用中でないか確認してください。
