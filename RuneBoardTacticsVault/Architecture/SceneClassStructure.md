# Scene / Class Structure

## Map Authoring v4.3

状態: **v4.3実装・描画順を含め検証済み**

対象Scene:

- `Assets/Scenes/MapAuthoring/GbaForestMapAuthoringTemplate.unity`
- `Assets/Scenes/MapAuthoring/GbaForestMapSample.unity`

~~~mermaid
flowchart TD
    Scene["Map Scene (.unity) / 配置の正本"]
    Root["MapRoot"]
    Grid["Grid"]
    Tilemaps["4 Tilemaps / Ground・Terrain・Collision・Foreground"]
    Collision["CollisionTilemap / 水・外周・崖"]
    Decorations["Decorations / plain Transform"]
    Visuals["ObstacleVisuals / plain Transform"]
    Props["Props / plain Transform"]
    Sockets["Sockets / MapSocketMarker"]
    EventSocket["EventSocket / Socket ID・Transform位置・Definition・radius"]
    EventDefinition["MapEventDefinition / ScriptableObject設定"]
    HealingDefinition["HealingFountainEventDefinition / heal amount"]
    EventCatalog["MapEventCatalog / List保存・Dictionary索引"]
    EventPreview["PreviewDebug / 近接・E・回復"]
    EventState["Dictionary Socket ID to used state"]
    VisualPrefab["Obstacle Visual Prefab / 表示Root"]
    CollisionBody["CollisionBody / 幹・接地点"]
    Marker["MapObstacleVisualMarker / 占有Footprintと判定方式"]
    Palette["Ground / Collision Palette 2点"]
    Tiles["GroundVariationTile / Tile / RuleTile Assets"]
    DecorationPrefab["Detail Decoration Prefab 4点"]
    RoadRasterizer["MapRoadPathRasterizer / Radius 1"]
    RoadRule["RoadConnectionRuleTile / road group / coordinate hash"]
    Catalog["Production Atlas Catalog v4.3 / 205"]
    Importer["GbaMapSpritePostprocessor"]
    Builder["MapAuthoringKitBuilder"]
    Validator["MapAuthoringValidator"]

    Scene --> Root
    Root --> Grid
    Grid --> Tilemaps
    Tilemaps --> Collision
    Root --> Decorations
    Root --> Visuals
    Root --> Props
    Root --> Sockets
    Sockets --> EventSocket
    EventSocket --> EventDefinition
    EventDefinition --> HealingDefinition
    EventCatalog --> EventDefinition
    EventPreview --> EventSocket
    EventPreview --> EventCatalog
    EventPreview --> EventState
    Visuals --> VisualPrefab
    VisualPrefab --> CollisionBody
    Decorations --> DecorationPrefab
    VisualPrefab --> Marker
    Palette --> Tiles
    Tiles --> Tilemaps
    Catalog --> Importer
    Importer --> Tiles
    Builder --> Tiles
    Builder --> DecorationPrefab
    Builder --> VisualPrefab
    Builder --> Scene
    RoadRasterizer --> Tilemaps
    RoadRule --> Tilemaps
    Validator --> Scene
    Validator -.照合のみ.-> Marker
    Validator -.正本を検査.-> Collision
    Validator -.正本を検査.-> CollisionBody
~~~

## 責務

- Unity Sceneがマップ配置の正本。
- `CollisionTilemap`とObstacle Prefab直下の`CollisionBody`を合わせた`MapCollision` Layerが移動Physicsの正本。
- Ground / Terrain / Foreground TilemapはCell座標と表示Tile参照を保持する。Grid配下の表示・判定TilemapはCollisionを含めて4点だけとする。
- DecorationsはTall Grass、Flower、Wildflowers、Reedsの通行可能な表示Prefabを保持する。
- ObstacleVisualsは森、崖、木、低木、岩、切株、倒木の表示Prefabを保持する。
- PropsはMushroom、Chest、Signの表示Prefabを保持する。
- 表示RootはCollider2D / Rigidbody2Dを持たず、Default Physics Layerを使う。森、木、低木、岩、切株、倒木は直下の非表示`CollisionBody`だけにMapCollision LayerのCollider2Dを一つ持つ。
- Player、ObstacleVisuals、Props、TallGrass、Reedsは`WorldObjects` / Order 0 / Pivotで共通比較し、world Yが低い足元ほど手前に描画する。
- FlowerPatch / Wildflowersは`MapDetail`の固定背面、Ground / Terrainは`MapGround`、Foregroundは`MapForeground`の固定順とする。URP 2Dの正本はRenderer2D DataのCustom Axis / `Vector3.up`。
- `MapObstacleVisualMarker`は生成時の広めの占有Footprintと、TilemapFootprint / CollisionBodyの物理方式・形状を保持する。Runtimeで判定を生成・更新しない。
- `MapSocketMarker`は候補地点の制作情報だけを保持する。
- `MapSocketMarker`のEvent種別はSocket ID、自由なTransform位置、EventDefinition参照または定義ID、接近半径上書きを保持する。自身は近接判定やE操作を実行しない。
- `MapEventDefinition`はEventの不変設定をScriptableObjectとして共有し、`HealingFountainEventDefinition`は回復量を追加する。`MapEventCatalog`はInspector編集用ListからRuntime検索Dictionaryを作る。使用済みなどの対戦状態はAssetへ保存しない。
- `GbaMapSpritePostprocessor`は単一Production AtlasのImport設定とCatalog由来のMultiple Sprite分割を統一する。
- `MapAuthoringKitBuilder`はGit管理Assetを再構築する保守コマンド。日常編集UIは提供しない。
- `GroundVariationTile`は装飾なしGrass 8枚から、Cell座標とSeedを強い32bit 2D hashへ混合して決定的にSpriteを選ぶ。Shader、回転、反転は使わない。
- `MapRoadPathRasterizer`は直交WaypointをRadius 1の正方形Brushで3 Cell幅へ変換し、4近傍の単一Component判定を提供する。
- Dirt / Stoneの`RoadConnectionRuleTile`は同じ`road`グループを使い、材質境界でも接続を継続する。16 Cardinal Maskそれぞれの4 Spriteから、XY座標、Seed、Rule IDのhashで決定的に輪郭差分を選ぶ。
- Dirt / Stoneの接続辺は中央`[6,26)`の20px固定Socketを使い、両端6pxは直交する2方向のbitがともに接続する場合だけ埋める。`05` / `0a`は20pxのみ、太道内角はAND条件で角埋めする。露出辺のみを不規則輪郭とし、非`0f`の路面外は透過にしてGroundTilemapを見せる。
- Water RuleTileは全面Mask `ff`だけ4 SpriteのRandom / Fixed、他MaskはSingle / Fixedとする。
- `MapAuthoringValidator`はSceneを変更せず、階層、Layer、表示Prefab、Transform、Collision、Sprite、Socketを検査する。

道路処理順は、直交Waypoint定義、Radius 1正方形Brushによる3 Cell幅化、4近傍の単一Component検証、Dirt / Stone材質割当の順とする。形状確定後に両材質を共通`road`グループで接続する。

`DetailTilemap`、Detail Palette、`ObstacleTilemap`はv4.3構造へ含めず、Validatorで旧構造として検出する。Tile PaletteはGround / Collisionの2点、標準表示用Tile Assetは`GrassVariation`とStone Variantの計2点、表示PrefabはDetail 4を含む計19点とする。

## ゲーム側との境界

独自Map DTOやSerializerは置かない。ゲーム側は必要なSceneを読み込み、Tilemap、Prefab Instance、MapSocketMarkerをUnity APIで参照する。Event設定だけはScriptableObject Catalogを参照し、Listを保存の正本、DictionaryをRuntime索引とする。

ゲームの移動判定はMapCollision Layer上にScene保存されたTilemap ColliderとCollisionBodyを参照する。SpriteやMapObstacleVisualMarkerから判定を復元しない。Runtime処理が必要になった場合も制作Sceneを正本として維持し、ゲーム固有Controllerは別Assemblyへ追加する。

## Exploration Preview Debug

状態: **Step 2の回復の泉縦切りを検証中 / 一時Playtest足場**

対象Scene:

- `Assets/Scenes/Playtest/GbaForestBattleRoyaleExplorationPreviewDebug.unity`
- Additive読込: `Assets/Scenes/MapAuthoring/GbaForestBattleRoyaleReference.unity`

`FantasyRoyale.Playtest.Runtime`は`FantasyRoyale.MapAuthoringKit.Runtime`と`Unity.InputSystem`だけを参照する。Reference Sceneを変更・複製せず、Playtest SceneのBootstrapが次をRuntimeで接続する。

- `Player/Move` ActionのRuntime複製 → 最新の移動入力値（Vector2） → Dynamic `Rigidbody2D.MovePosition`。
- 仮Player足元`CapsuleCollider2D` → Reference SceneのMapCollision Layer（CollisionTilemap + CollisionBody）。
- Reference Sceneの単一Camera → Player追従、Map端Clamp、1 / 32 unit Snap。
- 仮PlayerのSpriteRenderer → `WorldObjects` / Order 0 / Pivot。Map内の立体表示と同じ足元Yソート。
- Reference Sceneの単一`PlayerStart` → 仮Player Spawn位置。
- `Player/Interact` → 最寄りEventSocket → `HealingFountainEventDefinition` → 仮HP回復。
- 成功したOneShot → `Dictionary<SocketId, State>`。HP満タン時は更新しない。

仮Player、Runtime生成Sprite、仮HP、確認HUD、Test用入力上書き、Event状態Dictionaryは`BattleRoyaleExplorationPreviewDebug`だけが持つ。本番Character、戦闘、試合進行へ昇格する前に別責務へ分ける。

詳細: [[Scenes/PrototypeSoloScene|Exploration Preview Debug Scene]]

## v3 / v4 / v4.1 / v4.2履歴

v3は6 Tilemap、ObstacleTilemap、Props Grid、Collider付きPrefabを前提としていた。v4移行でScene / Builder / Validatorから削除済み。

v4は5 Tilemap、Detail Tile / Detail Palette、表示Prefab 15点を使用していた。v4.1ではDetailをDecorations配下のPrefabへ変更し、道路形状生成と材質接続を分離する。

v4.1は102 Sprite、Grass 4 Tile、全Mask Single / Fixedで完了していた。v4.2では115 Sprite、Grass 8枚の`GroundVariationTile`、全面MaskだけのRandom / Fixedへ置換した。v4.3では205 Sprite、Dirt / Stoneの全16 Mask x 4輪郭差分、4 Cell順列選択、透過路面端、20px固定SocketとAND角埋めへ置換した。Scene Hierarchyと自由配置Prefab、Collision分離はv4.1から継続する。

v4.3 Template / Sample / 96x72 Reference再構築とValidatorは成功し、Event Socket操作を含むEditMode Testは61 passed / 0 failed / 0 skipped、PlayMode Testは4 passed / 0 failed / 0 skipped。30x20 Sampleの最新SSは`Assets/Art/Generated/MapAuthoring/QA/gba-forest-sample-unity.png`。Hashは[[../Dev/Assets/GeneratedAssets|Generated Assets]]へ記録する。

詳細: [[Scenes/GbaForestMapAuthoringScene|GBA Forest Map Authoring Scene]]
