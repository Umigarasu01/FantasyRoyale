# Milestone1ExplorationScene

## 2026-06-05 更新

Milestone 1 のSceneは、森・草原ベースの探索確認マップとして再生成する方針。

- 固定マップサイズは 96x72。
- 中央石畳広場、南の主道、西の草むら通路、北側の回遊路、北東の池を持つ。
- 木、低木、岩、倒木、崖、池で進路を区切る。
- 草むら、花、キノコ、葦は通行可能な密度表現として扱う。
- 商人屋台、宝箱、祭壇、看板は今後のインタラクト検証用に配置候補として残す。

Unity Editorで `FantasyRoyale/Build Milestone 1 Exploration Scene` を実行すると、生成器の最新内容がSceneへ反映される。

Milestone 1の探索感確認用Scene。

## 主要オブジェクト

- `Grid`
  - `GroundTilemap`: 基本地形。
  - `DetailTilemap`: 通行可能な草むらなどの演出。
  - `CollisionTilemap`: 水、溶岩、崖などの通行不可地形。
- `ExplorationMapBuilder`
  - `MapDefinitionAsset`、`MapTilePaletteAsset`、`MapObjectSpawnTableAsset` を参照する。
  - `FR_MapPlacementResolver` でスロット抽選を行い、`GeneratedObjects` 配下へPrefabを生成する。
- `GeneratedObjects`
  - 商人、木、低木、岩、遺跡柱などのランダム配置結果。
- `Player`
  - `KeyboardPlayerInput` と `PlayerMotor2D` でキーボード移動する。
- `Main Camera`
  - `CameraFollow2D` でPlayerを追従する。

## 当たり判定

- 低木、木、岩、商人屋台、遺跡柱などのPrefabには `BoxCollider2D` を付ける。
- 草むらは `DetailTilemap` に置き、通行可能。
- 水、溶岩、崖は `CollisionTilemap` に置き、`TilemapCollider2D + CompositeCollider2D` で通行不可。
