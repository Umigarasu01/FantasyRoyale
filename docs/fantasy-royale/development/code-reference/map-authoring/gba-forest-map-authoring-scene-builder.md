# GbaForestMapAuthoringSceneBuilder

状態: **v4.1実装・Unity Template再構築 / Test検証済み**

## 役割

GBA森林マップ用の空Sceneを、素材参照なしで再現可能に構築するEditor専用Builder。

- Script: `Assets/Editor/MapAuthoring/GbaForestMapAuthoringSceneBuilder.cs`
- 出力: `Assets/Scenes/MapAuthoring/GbaForestMapAuthoringTemplate.unity`
- メニュー: `FantasyRoyale/Map Authoring/Create or Refresh GBA Forest Template`

## v4.1生成内容

    MapRoot
    ├─ Grid
    │  ├─ GroundTilemap
    │  ├─ TerrainTilemap
    │  ├─ CollisionTilemap
    │  └─ ForegroundTilemap
    ├─ Decorations
    ├─ ObstacleVisuals
    ├─ Props
    ├─ Sockets
    └─ Main Camera

- `Decorations`、`ObstacleVisuals`、`Props`はGrid配下へ置かないplain Transform。
- 装飾と配置物はCellではなく1 / 32 unit、つまり1px単位で自由配置する。
- `DetailTilemap`と`ObstacleTilemap`は生成しない。
- CollisionTilemapへTilemapCollider2D、CompositeCollider2D、Static Rigidbody2Dを設定し、Rendererを無効化する。
- TilemapではCollisionTilemapだけをMapCollision Physics Layerへ置く。Obstacle Prefab内の規定CollisionBodyも同Layerを使う。
- Ground / Terrainを固定背面、Foregroundを固定前面にするSorting LayerとOrthographic Cameraを設定する。
- CameraへTransparency Sort Custom Axis / `Vector3.up`をFallbackとして設定する。URP 2Dで使うRenderer2D Dataの設定は`MapAuthoringKitBuilder`が正本として統一する。

## 再実行契約

同名Objectと必要Componentを再利用し、v4.1標準Hierarchyへ収束させる。旧`DetailTilemap`、`ObstacleTilemap`はGrid直下の標準外Objectとして削除する。

`Decorations`、`ObstacleVisuals`、`Props`のRoot自身に残ったGrid、Tilemap、TilemapRenderer、Collider2D、Rigidbody2Dも除去し、自由配置表示とCell・物理責務を分離する。Templateの3表示Root配下は空へ戻す。

利用者が別Sceneを開いている場合はadditiveに処理し、終了後にActive Sceneを戻す。未保存Untitled Sceneを対話時に破棄しない。

## 責務外

- Tile、RuleTile、Palette、Prefabの生成。
- Obstacle Visualの配置やCollision Cellの自動生成。
- Sample Mapの配置。
- Player、Enemy、ゲーム進行。

制作Kit Assetは`MapAuthoringKitBuilder`が担当する。

## 旧構造履歴

- v3はGround、Terrain、Detail、Obstacle、Collision、Foregroundの6 TilemapとProps Gridを生成していた。
- v4はGround、Terrain、Detail、Collision、Foregroundの5 TilemapとObstacleVisuals / Propsを生成していた。
- v4.1ではDetail表示も自由配置へ移し、4 TilemapとDecorations / ObstacleVisuals / Propsへ変更した。
