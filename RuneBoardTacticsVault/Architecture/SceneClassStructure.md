# Scene / Class Structure

## Milestone 1 Exploration

Scene: `Assets/Scenes/Milestone1ExplorationScene.unity`

```mermaid
flowchart TD
    Scene["Milestone1ExplorationScene"]
    Builder["ExplorationMapBuilder"]
    MapAsset["MapDefinitionAsset"]
    Palette["MapTilePaletteAsset"]
    SpawnTable["MapObjectSpawnTableAsset"]
    Resolver["FR_MapPlacementResolver"]
    Tilemaps["Ground / Detail / Collision Tilemaps"]
    Objects["GeneratedObjects"]
    Player["Player: KeyboardPlayerInput + PlayerMotor2D"]
    Camera["CameraFollow2D"]

    Scene --> Builder
    Builder --> MapAsset
    Builder --> Palette
    Builder --> SpawnTable
    Builder --> Resolver
    Builder --> Tilemaps
    Builder --> Objects
    Scene --> Player
    Scene --> Camera
    Camera --> Player
```

## 責務

- Coreの `FR_` 型は、スロット抽選と配置結果の決定だけを担当する。
- Unity側Assetは、Tilemap表示、Prefab参照、Inspector編集用のデータを担当する。
- `ExplorationMapBuilder` はCore結果をTilemap/Prefab生成へ変換する。
- `PlayerMotor2D` は本番想定の移動処理として扱い、入力取得は `KeyboardPlayerInput` に分ける。
