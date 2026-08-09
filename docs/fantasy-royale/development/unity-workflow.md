# Unity開発Workflow

## Project前提

- Unity Version: `6000.4.3f1`
- Source of Truth: `Assets/`、`Packages/`、`ProjectSettings/`
- Generated Output: `Library/`、`Temp/`、`Logs/`。完了根拠やDurable Knowledgeの正本にしない。
- `.meta`とAsset GUIDを維持し、Taskと無関係なProject Settings / Packageを変更しない。

## Editor確認

- FantasyRoyaleのEditor、Scene、Screenshot、Editor内修正にUnityMCPを使う場合は`unity-mcp-fantasyroyale`だけを使用する。
- `unity-mcp-fantasyroyale`が利用できない場合は別Project用UnityMCPへ切り替えず、通常のFile編集とUnity batchmodeを使用する。
- Unity batchmode実行前に同じProjectを開いているEditorがないことを確認する。Editorが開いている場合はProject Lockで失敗する。
- Scene Builderを対話実行するときは、開いているSceneの未保存変更を保護する。

## ProductionとPreview

- Production Gameplayの確認に一時SceneやRuntime生成表示を使う場合、型名とScene名へ`PreviewDebug`を含める。
- PreviewはProduction Core / Actorの経路を検証するための足場とし、独自のRule実装を増やさない。
- Scene構成を変更した場合は関連するDesign DocとPlayMode Testを更新する。

BuildとTestのCommandは[Build・Test手順](testing.md)を参照する。

