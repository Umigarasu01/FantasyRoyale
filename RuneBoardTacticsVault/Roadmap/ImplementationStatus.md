# Implementation Status

## 2026-06-05 旧マップ生成物削除

状態: 削除済み

削除内容:
- 旧Milestone 1マップ生成用Editorメニュー。
- 旧Milestone 1 / Organic生成アセット、Prefab、Scene。
- 対応するEditorスクリプト解説リンク。

目的:
- 参考画像の方向性に合わないフラットな生成マップを残さず、まず1枚のコンセプト画像で見た目を固め直す。

## 2026-06-05 有機的ピクセルマップ生成器

状態: 実装更新済み / 修正後のUnity生成確認待ち

追加内容:
- `Milestone1OrganicSceneBuilder`
- メニュー: `FantasyRoyale/Build Milestone 1 Organic Pixel Map`
- 生成先:
  - `Assets/Art/Generated/Milestone1Organic/`
  - `Assets/Data/Milestone1Organic/`
  - `Assets/Prefabs/Milestone1Organic/`
  - `Assets/Scenes/Milestone1OrganicMapScene.unity`

確認結果:
- `dotnet build Assembly-CSharp.csproj --no-restore`: 成功。既存の `System.Net.Http` 競合警告あり。
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`: 成功。既存の `System.Net.Http` 競合警告あり。
- 初回MCP生成後にTilemapが空であることを確認。
- Tile/PrefabをAssetDatabaseからロードし直して描画するよう修正。
- 修正後のUnity生成は、MCP接続断とEditor起動中のbatchmode不可により未確認。

未確認:
- 修正後の `Milestone1OrganicMapScene` の実表示。
- Play Modeでの移動感、当たり判定、探索密度。

## 2026-06-05 森・草原マップ再設計

状態: 実装更新済み / Unity Editor反映は未実行

更新内容:
- `Milestone1SceneBuilder` を採用済みアート方針に合わせて更新。
- Milestone 1 のマップ構成を、森・草原ベースの 96x72 固定マップに変更。
- 中央石畳広場、土道/石畳の通路、西側草むら、北側回遊路、北東の池、崖/樹木による進路区切りを追加。
- 生成タイルを簡易ピクセルアート風に変更。
- 生成オブジェクトに木、低木、岩、商人屋台、キノコ、切り株、倒木、花、看板、宝箱、祭壇、葦を追加。
- 通行不可: 低木、木、岩、倒木、池、崖。
- 通行可能: 草むら、花、キノコ、葦などの軽装飾。

確認結果:
- `dotnet build Assembly-CSharp.csproj --no-restore`: 成功。既存の `System.Net.Http` 競合警告あり。
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`: 成功。既存の `System.Net.Http` 競合警告あり。
- Unity batchmode: 同じプロジェクトをUnity Editorが開いていたため未実行。
- `unity-mcp-fantasyroyale`: `Unity not detected` のため未実行。

未確認:
- Editor上の実際の見た目。
- Play Modeでの移動感、当たり判定、探索密度。

## Milestone 1: ビジュアル・探索マップ基盤

状態: 実装済み / Unity batch生成確認済み

実装内容:
- Core配置抽選
  - `FR_MapDefinition`
  - `FR_MapSlot`
  - `FR_MapObjectDefinition`
  - `FR_MapPlacementResolver`
  - `FR_PlacementResult`
- Unity探索基盤
  - `MapDefinitionAsset`
  - `MapObjectDefinitionAsset`
  - `MapObjectSpawnTableAsset`
  - `MapTilePaletteAsset`
  - `ExplorationMapBuilder`
  - `PlayerMotor2D`
  - `KeyboardPlayerInput`
  - `CameraFollow2D`
- Editor生成
  - `Milestone1SceneBuilder`
  - `Assets/Scenes/Milestone1ExplorationScene.unity`
  - `Assets/Art/Generated/Milestone1/`
  - `Assets/Data/Milestone1/`
  - `Assets/Prefabs/Milestone1/`

確認結果:
- `dotnet build Assembly-CSharp.csproj --no-restore`: 成功。既存の `System.Net.Http` バージョン競合警告あり。
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`: 成功。既存の `System.Net.Http` バージョン競合警告あり。
- Unity batchmodeで `FantasyRoyale.Editor.Milestone1SceneBuilder.BuildScene` 実行済み。
- Unity batchmodeで `FantasyRoyale.Editor.Milestone1SceneBuilder.RebuildGeneratedMap` 実行済み。
- Scene YAML上でTilemapデータとランダム配置オブジェクト生成を確認済み。

未確認:
- Editor画面上での実プレイ操作感。
- 実機Play Modeでの当たり判定の体感。
## 2026-06-05 森構造ベースの火山/雪バイオーム画像生成とTilemap分解案

状態: 設計ノート作成済み / 火山画像は生成済み / 雪画像は `$imagegen` レート制限により待機

追加内容:
- `Art/BiomeTilemapAtlasPlan.md` を追加。
- 森サンプルマップの構図を維持した火山/雪バイオーム展開方針を整理。
- Unity Tilemap 用の共通タイルID、森/火山/雪の差し替え対応、オブジェクトアトラス案を整理。
- `Dev/Assets/GeneratedAssets.md` に火山/雪画像の生成予定と保存予定先を記録。
- `Assets/Art/Concept/volcano-biome-sample-map.png` を保存。

未完了:
- `$imagegen` による `snow-biome-sample-map.png` の生成。
- 生成結果を見た上でのタイルセット案/オブジェクトアトラス案の微修正。

## 2026-06-05 バイオーム仮アトラス作成

状態: 生成・保存済み

追加内容:
- `Assets/Art/Generated/BiomeAtlas/biome-tile-object-atlas.png` を追加。
- 森・火山・雪の地形タイル候補とオブジェクト候補を1枚の仮アトラスに整理。
- `Art/BiomeTilemapAtlasPlan.md` に生成済みアトラスの保存先、サイズ、扱いを追記。
- `Dev/Assets/GeneratedAssets.md` に生成方法、保存先、プロンプト概要を記録。

注意:
- 生成画像は 1254x1254 で、完全な32x32自動グリッドスライス用ではない。
- 次段ではこの仮アトラスを切り出し元として、Unity用の正規グリッド版タイルアトラスと透明PNGオブジェクトアトラスに再整形する。
## 2026-06-05 バイオームアトラス探索マップ生成導線

状態: Editorビルダー実装済み / C#ビルド成功 / Unity Editor実行は環境接続待ち

追加内容:
- `Assets/Editor/BiomeAtlasMapSceneBuilder.cs` を追加。
- 仮アトラスから森・火山・雪の地形タイル、オブジェクトSprite、Prefab、探索Sceneを生成する導線を追加。
- 生成予定Scene: `Assets/Scenes/BiomeAtlasExplorationMapScene.unity`
- 生成予定メニュー: `FantasyRoyale/Build Biome Atlas Exploration Map`
- 地形タイルはセル外周を少し落として切り出し、Tilemap上のグリッド感を軽減する。
- プレビュー用マップ画像 `Assets/Art/Generated/BiomeAtlas/PreviewMaps/biome-atlas-generated-map-preview.png` を生成。

確認結果:
- `dotnet build Assembly-CSharp.csproj --no-restore`: 成功。既存の `System.Net.Http` 警告あり。
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`: 成功。既存の `System.Net.Http` 警告あり。
- Unity batchmode: 同じプロジェクトをUnity Editorが開いているため実行不可。
- `unity-mcp-fantasyroyale`: `Unity not detected (no fresh discovery files found)` のため実行不可。

未完了:
- Unity Editor上でのメニュー実行。
- 実Sceneの2Dキャプチャ確認。
- 当たり判定と移動感のPlay Mode確認。
