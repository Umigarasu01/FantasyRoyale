# BiomeAtlasMapSceneBuilder

## 役割

`Assets/Art/Generated/BiomeAtlas/biome-tile-object-atlas.png` から、探索検証用のTilemap Sceneを生成するEditor専用ビルダー。

森・火山・雪が同じ構造で差し替えられるかを確認するため、アトラス内の必要セルをPNGとして切り出し、Tile、Prefab、Sceneをまとめて作る。

## 生成対象

- Tile切り出し: `Assets/Art/Generated/BiomeAtlas/Runtime/Tiles/`
- Object切り出し: `Assets/Art/Generated/BiomeAtlas/Runtime/Objects/`
- Tile asset: `Assets/Data/BiomeAtlas/Tiles/`
- Prefab: `Assets/Prefabs/BiomeAtlas/`
- Scene: `Assets/Scenes/BiomeAtlasExplorationMapScene.unity`

## Scene構成

- `Ground`: 歩行可能地形のTilemap。
- `Details`: 装飾用Tilemap。
- `Collision`: 水、溶岩、氷、崖、壁などの当たり判定Tilemap。
- `Objects`: 木、岩、切り株、倒木、結晶などのPrefab配置。
- `PlayerStart_DebugMover`: キーボード移動確認用の仮プレイヤー。

## マップ方針

- 96x72 の広めマップ。
- 左側を森、右上を火山、右下を雪として配置する。
- 中央に縦の石畳道、左に曲がる森道、右下に雪道を置く。
- 森の池、火山の溶岩池、雪の凍結池を配置する。
- 外周と一部の区画境界に壁/崖を置き、自然なルート制限を作る。

## 注意

仮アトラスが完全な32x32グリッドではないため、地形タイルはセル外周を少し落として切り出す。
これはアトラスの黒い区切りや強いセル境界がTilemap上に出すぎるのを避けるため。

現在、Unity Editorが同じプロジェクトを開いているためbatchmodeでは生成できず、`unity-mcp-fantasyroyale` も `Unity not detected` でEditor内実行できなかった。
Editor接続が復旧したら、メニュー `FantasyRoyale/Build Biome Atlas Exploration Map` から生成する。
