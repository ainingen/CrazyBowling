# CLAUDE.md — CrazyBowling

## ★★ 応答・報告は必ず日本語で書く ★★
- 応答・報告・質問・途中経過の説明・C# のコメント・コミットメッセージは、**すべて日本語**で書く
- 会話が長くなって要約されたあとも、日本語を保つ。途中から英語に切り替えない
  （以前、報告が途中から英語になったことがある）

## プロジェクト概要
Unity 6.3 LTS（URP / 3D）で作るボウリングゲーム。
10種類の変わったレーンを1フレームずつ回り、合計点を競う。
仕様は プロジェクト直下の `仕様.md` を必ず読むこと。

## 環境
- Windows / Unity 6000.3.24f1 / URP（Universal 3D テンプレート）
- MCP for Unity（com.coplaydev.unity-mcp）経由でエディタを操作する
- 入力はプロジェクト設定（Active Input Handling）を確認し、Input System パッケージが有効ならそれを使う
- Unity CLI（`unity` コマンド）と Unity Pipeline パッケージも導入済み（CLIは実験版）

## エディタ操作の使い分け
- **Unity CLI を優先する**（PowerShellで実行）
  - `unity command`：実行中のエディタで使えるコマンド一覧の確認と実行
  - `unity eval`：エディタ内でC#を実行する。状態の確認、Console の取得、テスト実行、シーン上のオブジェクト操作に使う
  - 接続先が曖昧なときは `--project-path=C:\dev\CrazyBowling` を付ける
- CLIで失敗した操作は MCP for Unity で試してよい
- `unity eval` のルール
  - 実行前に、何を実行するかを日本語で説明する
  - ファイル（.unity、.meta、ProjectSettings、manifest.json）を直接書き換える処理は書かない。エディタのAPI経由で行う
  - 一時的な確認用コードをプロジェクト内のファイルとして残さない
- `unity mcp configure` や `unity self-update` など、CLI自体の設定を変えるコマンドは実行しない（ユーザーが行う）

## 絶対に守るルール
- **CLIもMCPも使えないときは代替実装をせず、報告して止まること**
  - Editorスクリプトを自作してMCPの代わりにしない
  - .unity ファイルを直接編集しない
- **自動実行されるコードを作らない**
  - [InitializeOnLoad]、[InitializeOnLoadMethod]、エディタ起動時に走る処理は禁止
- **手で書き換えてはいけないファイル**
  - Assets/Scenes/*.unity
  - *.meta
  - ProjectSettings/ 配下
  - Packages/manifest.json
  - 変更が必要な場合は、ユーザーにUnityエディタでの操作手順を伝える
- **実装前に、何をどう変更するつもりかを日本語で説明してから着手する**
- **変更後は必ずUnityのConsoleを確認し、エラー・警告を報告する**
- **段階を分けて実装し、各段階ごとに報告して指示を待つ**
  - 指示された段階より先の実装はしない
- ユーザーはUnity初心者。手作業が必要な場合は、メニューの場所とクリック順まで具体的に書く

## コーディング規約
- 応答は日本語、C#のコメントも日本語
- スクリプトは Assets/Scripts/ 配下に機能別フォルダで置く
  （Core / Ball / Pins / Lanes / UI / Data）
- ロジック（スコア計算など）は MonoBehaviour に依存しない純粋なC#クラスにし、EditModeテストを書く
- 調整用の数値は [SerializeField] で Inspector に出す（ユーザーが自分で調整する）
- 数値はコードに直書きせず、ScriptableObject か SerializeField に置く
- **動くオブジェクト（ボール・ピンなど）は「物理の親」と「見た目の子」に分ける**
  - 親：Rigidbody / Collider / スクリプト。**Scale は 1 のままにし、Collider には実寸を直接入れる**
  - 子：`Visual` という名前で、MeshFilter / MeshRenderer だけを持つ。Collider は付けない
  - 理由：後で見た目を市販モデルに差し替えても、物理と当たり判定に影響させないため
  - 例：`Ball`（Rigidbody / SphereCollider radius 0.11 / BallController）＋ `Ball/Visual`（Scale 0.22 の球）

## 既知のハマりどころ
- スクリプト作成後、Unityが裏にいるとコンパイルされない。
  「Component type not found」が出たら、ユーザーにUnityウィンドウをクリックしてもらうよう伝える
- manage_components で型が見つからないときは、Undo.AddComponent で直接アタッチして回避する
- 再生中にInspectorで変えた値は停止すると戻る
