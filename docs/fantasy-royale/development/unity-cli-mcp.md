# Unity CLI MCP

## 構成

2026-09-07に公式Unity CLI方式へ移行。CLIは `1.0.0-beta.8`、Packageは `com.unity.pipeline` `0.6.0-exp.1`。
CLI実体: `C:\Users\umiga\AppData\Local\Unity\bin\unity.exe`。
Codex設定: `C:\Users\umiga\.codex\config.toml`。旧設定のバックアップは同Directoryの `config.toml.unity-migration-20260907-000452.bak`。

接続名は `unity-mcp-fantasyroyale`。`mcp --project-path C:\Users\umiga\Repository\FantasyRoyale` で対象Projectを固定する。他Project用の接続で操作しない。
Unityを開いてPackage解決・Compileが終わった後に利用する。既存のCodexタスクでToolが出ない場合はアプリ再起動後に確認する。

## 確認コマンド

PowerShellで実行する。PATH未反映の場合は上記CLI絶対Pathを使用する。

```powershell
unity status --json
unity list --project-path 'C:\Users\umiga\Repository\FantasyRoyale' --json
unity command editor_status --project-path 'C:\Users\umiga\Repository\FantasyRoyale' --json
```

`projectPath`が対象と一致し、`compiling=false`かつ`status=ready`であることを確認してから操作する。
`unity mcp configure codex`は標準名 `unity` を使うため、3プロジェクトの固定設定を無条件で上書きしない。

## 移行時の注意

旧 `relay_win.exe --mcp` は使用しない。旧AI Assistant 2.6.0-pre.1とPipeline 0.6.0-exp.1の同居では `System.Reflection.Metadata.dll` 等の重複でCS0246が発生したため、旧Assistant依存を対象2プロジェクトから除去した。旧Assistant固有のEditor内機能を再導入する場合は互換性を別途確認する。

CLIとPipelineはBeta/Experimental版。検証範囲は接続と読み取り操作であり、ゲーム全体のTestを実行した意味ではない。

## 2026-10-03のCLI互換性確認

実体は引き続き`1.0.0-beta.8`。`self-update --yes`およびbeta.12を指定した更新は成功応答を返したが、実行FileのVersionはbeta.8のままだった。更新済みとは扱わない。

この実体ではSkillの`--caller plugin --skill unity-cli`ラベルを`command eval`へ付けるとUnknown parameterで拒否された。今回の互換呼出しはラベルを省き、`--project-path`と`--format json`を維持した。`command eval`、`run_tests --async_tests true`、`test_status`を実行確認した。新しいCLIへ更新できた場合はラベル対応を再確認する。

Forest Restartの再生成・Test手順は[Build・Test手順](testing.md)を参照する。

## 公式資料

- [Unity公式移行ガイド](https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli)
- [Unity CLI導入手順](https://docs.unity.com/en-us/unity-cli/use-unity-cli)

## 検証結果（2026-09-07）

MCP stdioのinitialize、tools/list（149件）、tools/callのeditor_statusに成功。対象projectPath一致、status=ready、compiling=falseを確認。Codex設定はCLIのmcp getでも読み取り済み。ゲーム全体のTestは未実施。
