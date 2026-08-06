# BattleRoyaleExplorationPreviewDebugSceneBuilder

対象: `Assets/Editor/Playtest/BattleRoyaleExplorationPreviewDebugSceneBuilder.cs`

状態: **実装・Unity生成・検証済み**

## 役割

`FantasyRoyale/Playtest/Build Exploration Preview Debug Scene`から、探索確認専用Sceneを決定的に再生成するEditor補助。

出力:

- `Assets/Scenes/Playtest/GbaForestBattleRoyaleExplorationPreviewDebug.unity`

生成Sceneには`PlaytestRoot/BattleRoyaleExplorationPreviewDebug`だけを保存し、次を設定する。

- Reference Map: `Assets/Scenes/MapAuthoring/GbaForestBattleRoyaleReference.unity`
- Input Actions: `Assets/InputSystem_Actions.inputactions`
- Event Catalog: `Assets/Data/MapAuthoring/Events/MapEventCatalog.asset`
- 仮移動速度: 5 unit/s
- Camera Orthographic Size: 8
- 仮HP: 50 / 100

Reference MapとPlaytest SceneをBuild Settingsへ重複なく追加し、既存Sceneの順序は維持する。保存後にBootstrap、入力Asset、Event Catalog、Map Path、Build Settings登録を再読込検査する。生成前に`Player/Move`と`Player/Interact`の両Actionが存在することも確認する。

このBuilderはReference Mapを複製・編集しない。Map制作の正本とGameplay確認の足場を分離するために存在する。

対話実行時は、現在開いているSceneの未保存変更を先に確認する。生成と再読込検査が終わった後は、元の保存済みScene構成へ戻すため、Scene制作中の内容を黙って閉じない。

関連:

- [[../../Unity/Playtest/BattleRoyaleExplorationPreviewDebug|Runtime Bootstrap]]
- [[BattleRoyaleExplorationPreviewDebugPlayModeTests|PlayMode Tests]]
