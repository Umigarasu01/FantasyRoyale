# RuneBoardTactictics Codex 作業方針

> **Legacy:** 2026-08-10に新`AGENTS.md`と`docs/fantasy-royale/`へ移行した旧Rule。現行指示として使用しない。

このリポジトリで Codex が作業するときの参照順、判断基準、更新ルールをまとめる。
ゲーム仕様、設計メモ、マイルストーン詳細は `RuneBoardTacticsVault/` を正とする。

## 最初に確認するもの

1. `RuneBoardTacticsVault/Index.md`
2. 進行中タスク: `RuneBoardTacticsVault/Roadmap/CurrentWork.md`
3. 実装状況: `RuneBoardTacticsVault/Roadmap/ImplementationStatus.md`
4. 仕様変更や駒効果を扱う場合: `RuneBoardTacticsVault/GameDesign/`
5. C# スクリプトを変更する場合: `RuneBoardTacticsVault/Dev/Scripts/Index.md`

## 仕様の扱い

- 仕様を勝手に確定しない。
- 未確定の内容は「未確定」「仮案」「有力案」「要検討」など、確定度が分かる言葉を残す。
- 確定仕様、採用仕様、旧ドラフトは混ぜない。必要なら新しい仕様ノートを作り、旧ノートは履歴として残す。
- 既存仕様と矛盾する内容を見つけた場合は、片方を勝手に正とせず「差分・要確認」として報告する。
- Unity 実装と Vault の内容が矛盾する場合も、どちらかを勝手に正としない。

## Vault 更新ルール

- `RuneBoardTacticsVault/` は Obsidian Vault として使う。Vault 内ファイルは Git 管理対象として扱う。
- Codex は必要に応じて Vault 内ノートを検索、読み取り、作成、更新してよい。
- ノート全体を不用意に再生成しない。可能なら対象見出しだけを追記、更新する。
- 大きな仕様追加は、既存ノートへ無理に詰め込まず、専用ノートを作って `Index.md` からリンクする。
- Vault を変更した場合は、変更したノート名と理由を作業報告に含める。

## 実装ルール

- ルール処理は Unity の `GameObject` や `Transform` に依存させない。
- Core は純 C# のデータとロジックとして作る。
- Unity 側は入力、表示、アニメーション、サウンド、Scene 遷移に責務を寄せる。
- Core の状態更新で複数の盤面操作を伴う場合は、先に検証を済ませてから一括更新する。途中失敗で盤面や対局状態が壊れる `Remove` / `Place` の分割呼び出しは避ける。
- 新規作成する C# スクリプトには、クラス、struct、enum など主要な型の役割が分かる日本語コメントを付ける。
- コメントは逐語説明ではなく、「なぜ存在するか」「どの責務を持つか」「なぜこの順序で検証・更新するか」を短く説明する。
- 分かりにくい状態更新、入力/表示生成、無効値の除外、フォールバック、テストの狙いには本文中にも簡潔な日本語コメントを残す。
- 関数コメントは関数宣言の直前に置き、「何を受け取り、何を判断・生成・更新する役割か」を短く書く。単純なプロパティや演算子は必要な場合だけでよい。

## スクリプト解説ルール

- C# スクリプトを新規追加した場合は、対応する解説 md を `RuneBoardTacticsVault/Dev/Scripts/` に作成する。
- 新規スクリプトの解説 md は `RuneBoardTacticsVault/Dev/Scripts/Index.md` からリンクする。
- 既存スクリプトの責務、公開 API、関連仕様が変わった場合は、対応する解説 md も更新する。
- スクリプトを削除またはリネームした場合は、対応する解説 md と `Dev/Scripts/Index.md` のリンクも整理する。
- Unity 実装を変更した場合は、関連する Vault 更新が必要か必ず確認する。
- Scene 構造、入力経路、Controller 間の依存、Core Flow への接続を変えた場合は、`RuneBoardTacticsVault/Architecture/SceneClassStructure.md` と `RuneBoardTacticsVault/Architecture/Scenes/` 配下の該当クラス図も更新する。

## 命名ルール

- 確認用、検証用、テスト用、一時的な足場スクリプトは、本番用と混同しない名前にする。
- 確認用 Scene やプレイ確認だけで使う `MonoBehaviour` は、原則として `PreviewDebug` を名前に含める。
- 自動テスト専用の補助型や Fixture は、原則として `Test`、`Fake`、`Stub`、`Mock` など用途が分かる語を名前に含める。
- 本番実装へ昇格する場合は、責務を整理したうえで `PreviewDebug` などの一時用途名を外し、対応する Vault のスクリプト解説と Index も更新する。
- 一時スクリプトを削除する場合は、Scene 参照、`.meta`、スクリプト解説 md、`Dev/Scripts/Index.md` のリンクを合わせて整理する。

## 素材導入ルール

- Unity Asset Store などの無料素材を取り込む場合は、事前にユーザーへ素材名、用途、提供元、ライセンス上の注意点を共有し、確認を取る。
- 取り込んだ素材は用途に応じて `Assets/Art/`、`Assets/Audio/`、`Assets/Prefabs/` など設計済みフォルダへ整理する。
- AI 生成画像をプロジェクトで使う場合は、原則として `Assets/Art/Generated/` または用途別の `Assets/Art/` 配下へ保存する。
- AI 生成素材は初期実装ではプレースホルダーとして扱い、正式素材へ差し替える可能性を残す。
- AI 生成素材を追加した場合は `RuneBoardTacticsVault/Dev/Assets/GeneratedAssets.md` に用途、保存先、生成日、生成方法、プロンプト概要を記録する。
- Unity から参照する AI 生成素材は Git 管理対象に含める。

## 進行ルール

- 特別な指示がない限り、実装やファイル変更を勝手に進めない。
- ユーザーが「実装して」「進めて」「修正して」「更新して」「洗い出して」など明示した場合は、合意済みの範囲でファイル変更してよい。
- 次のマイルストーンへ進む前に、実装予定の Step をチャット上で書き出してユーザー確認を取る。
- 実装は Step ごとに進める。複数 Step をまとめて実装しない。
- 各 Step の完了内容と確認結果を報告してから次の Step に進む。
- 進行中作業メモは `RuneBoardTacticsVault/Roadmap/CurrentWork.md` に、細かい実装ログではなくマイルストーン単位の方針として記述する。
- 実装状況は `RuneBoardTacticsVault/Roadmap/ImplementationStatus.md` に記録し、実装・テスト・確認状況が進んだら必要に応じて更新する。
- 実装状況を更新した場合は、変更した項目と理由を作業報告に含める。

## FantasyRoyale UnityMCP 作業ルール

- この作業では `unity-mcp-fantasyroyale` の UnityMCP だけを使い、他のUnityMCPは使わない。
- FantasyRoyale のUnity Editor確認、Scene確認、スクリーンショット取得、Editor内修正は `unity-mcp-fantasyroyale` のUnityMCP経由で行う。
- `unity-mcp-fantasyroyale` が利用できない場合は、他のUnityMCPへ切り替えず、通常のファイル編集やUnity batchmodeで確認する。
