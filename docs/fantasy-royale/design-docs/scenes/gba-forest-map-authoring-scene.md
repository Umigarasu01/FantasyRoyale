# GBA Forest Map Authoring Scene

状態: **ForestRestart検証用素材へ移行・48x32の4Scene生成・40Seed論理検証済み・正式素材採用は未承認**

## 現行ForestRestart

2026-10-03の仕切り直しにより、コンセプトv005を基準とした少量の森林・道・広場素材を使う。既存のReference Sceneのpath/GUIDとPlaytest接続を維持して内容を置換した。旧SceneTemplate、Sample、旧Forest Atlasと表示Prefabは撤去済み。空のAuthoring Template Sceneは保持する。

| Seed | Scene |
| --- | --- |
| 20261003 | `Assets/Scenes/MapAuthoring/GbaForestBattleRoyaleReference.unity` |
| 20261004 | `Assets/Scenes/MapAuthoring/ForestRestart/ForestRestartSeed20261004.unity` |
| 20261005 | `Assets/Scenes/MapAuthoring/ForestRestart/ForestRestartSeed20261005.unity` |
| 20261006 | `Assets/Scenes/MapAuthoring/ForestRestart/ForestRestartSeed20261006.unity` |

各Sceneは48x32Cell。Ground / Terrain / Collision / Foregroundの4Tilemap、Decorations / ObstacleVisuals / Propsのplain Transform、Sockets、単一Cameraという責務を維持する。現在の検証セットには水、橋、商店、泉の完成表示素材は含めない。

## 現行素材と判定

- 元画像6点: Grass Surface、Dirt Surface、oak、grove、undergrowth、比較用hero。保存先は`Assets/Art/Map/ForestRestart/Source/`。
- 正規化Sprite76点: Grass8、Dirt Cardinal-16 x 4差分の64点、表示Object4点。保存先は`Assets/Art/Map/ForestRestart/Sprites/`。
- Tile Asset3点: `Assets/Data/MapAuthoring/Tiles/ForestRestart/`のGrassVariation、DirtRoad、不可視Collision。
- 表示Prefab4点: `Assets/Prefabs/MapAuthoring/ForestRestart/`のoak、grove、undergrowth、hero。
- 基準は32px / Cell、PPU32、Point Filter、Compression None、Mipmap Off。Spriteと配置Anchorは整数pixelに整列し、Rotation0、Scale1、反転なしを守る。
- oak / grove / undergrowthの表示Boundsと配置用Footprintを分け、枝葉の通路への張り出しは許容する。CollisionBodyはさらに小さい幹・接地点のBoxCollider2Dとし、MapCollisionへ配置する。heroは縮尺比較用で、Colliderを持たない。
- CollisionTilemapは入口を除く外周だけを保存し、障害物の表示Footprintを不可視Tileへ焼き込まない。
- Ground / TerrainはMapGround、ObjectはWorldObjects / Order0 / Pivot。URP 2DはCustom Axis / Vector3.upを維持する。

## 自動生成と検証

`ForestRestartLayout`がEditor内の一時PlanをSeedから作り、検査後に`ForestRestartAssetBuilder`がUnity Sceneへ保存する。独自Runtime Map Serializerは追加しない。

1. 3〜4広場、幅3Cellの曲がる道、四辺の入口、Spawn、6Event目的地を確保する。
2. 道路と目的地の保護域を除いて森林3種類を散布する。
3. 配置用Footprintで重複を避け、接地Collisionが交差するCellから全歩行地をBFS検査する。接続を失う候補は採用しない。
4. SceneにはPrefabとColliderを保存し、`MapAuthoringValidator`で構造・表示・Socket・判定の契約を検査する。

`FantasyRoyale/Map Authoring/Rebuild Forest Restart Kit`は素材を再生成し、Seed20261003〜20261042の40論理Planを検証する。先頭4SeedだけをSceneへ保存し、各SceneのOverview / Detail計8画像を出力する。記録は[generation-validation.json](../../../../Assets/Art/Map/ForestRestart/QA/generation-validation.json)。`sceneValidated=false`の36SeedはScene・Physics・描画を検査した扱いにしない。

初回QAの草の細かすぎる粒・明るさと矩形に見える道端を受けてGrass / Dirtを再生成し、露出辺の起伏を最大4pxへ調整した。接続角の透明穴は修正・実PNG Testで確認済み。撤去後のUnity Test RunnerはEditMode 98件、PlayMode 4件成功。元画像・Prompt・Hashと目視QAの限界は[生成Asset台帳](../../development/assets/generated-assets.md)を参照する。正式採用は別の判断とする。

再生成は保存済みSceneをPlanから組み直す保守操作であり、手編集を維持する日常編集コマンドではない。対象Sceneに未保存変更がある場合は書込みを拒否する。

## 現行Playtestとの境界

Reference Sceneのpath/GUIDは維持し、[Exploration Preview Debug Scene](exploration-preview-debug-scene.md)からAdditiveで読み込む。MapへGameplay Controllerや操作Playerを保存せず、PlayerStart、6PoolCandidate EventSocket、MapCollision、Ground範囲、単一CameraをPlaytestへ渡す。Scene内`ConceptScaleHero`は画面比較用で、Runtime Playerとは別である。

48x32とSeedによる地形差分は素材の自動配置検証用設定であり、本番の固定Map + ランダム配置方針を変更しない。正式な試合ごとの地形生成は別のProduct Decisionとする。

## 履歴: v4.3の旧素材とScene契約

以下は仕切り直し前の記録であり、現行ForestRestartの数量・寸法・Asset契約として扱わない。旧素材・QA・Template・Sampleのpathは整理により存在しなくなる場合がある。過去のTest結果も今回の変更後の成功数へ流用しない。

旧状態: **v4.3実装・Unity再構築・描画順を含め検証済み**

## 対象

- `Assets/Scenes/MapAuthoring/GbaForestMapAuthoringTemplate.unity`
- `Assets/Scenes/MapAuthoring/GbaForestMapAuthoringTemplate.scenetemplate`
- `Assets/Scenes/MapAuthoring/GbaForestMapSample.unity`
- `Assets/Scenes/MapAuthoring/GbaForestBattleRoyaleReference.unity`

## v4.3標準Hierarchy

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

`Decorations`、`ObstacleVisuals`、`Props`はGrid Componentを持たないplain Transform。`DetailTilemap`、`ObstacleTilemap`、Props Gridはv4.3標準Hierarchyへ含めず、旧構造として禁止する。

## 入力経路

日常の制作では独自Controllerを通さない。

- Ground PaletteからGround / Terrain / Foreground TilemapへPaintする。
- Collision PaletteからCollisionTilemapへ水、外周、崖などCell地形の移動不可範囲をPaintする。
- Project BrowserからTall Grass、Flower、Wildflowers、Reeds PrefabをDecorationsへDragする。
- Project Browserから森、崖、木、低木、岩、切株、倒木PrefabをObstacleVisualsへDragする。
- Mushroom、Chest、Sign PrefabをPropsへDragする。
- 自由配置PrefabはPositionを1 / 32 unitへSnapし、Rotation 0、Scale 1とする。
- InspectorからMapSocketMarkerを設定する。Event中心はTransformをScene Viewで自由に移動し、選択LabelとRadius Handleで個別半径を調整する。Cell中心への固定は不要。
- MapAuthoringValidatorで完成前検査を行う。

GameObject Brushは使用しない。

Tile PaletteはGround / Collisionの2点だけとし、旧Detail Paletteを削除して再生成しない。標準表示用Tile Assetは`GrassVariation`とStone Variantの計2点。`GrassVariation`は装飾なしGrass Surface 8枚をCell座標の強い2D hashで選ぶ。Detail 4種はTile AssetではなくDecoration Prefabとして扱う。

## Component契約

- Grid: Rectangle、Cell Size 1x1。
- Ground / Terrain / Foreground: Tilemap、TilemapRenderer。
- CollisionTilemap: Renderer disabled、表示Spriteを持たないCollisionTile、TilemapCollider2D、CompositeCollider2D、Static Rigidbody2D、MapCollision Layer。
- Decorations / ObstacleVisuals / Props: plain Transform。
- 表示Prefab Root: SpriteRenderer、Default Layer、Collider2Dなし、Rigidbody2Dなし。立体表示はWorldObjects / Order 0 / Pivot、平面装飾はMapDetail / Order 0 / Pivot。
- CollisionBody: 森、木、低木、岩、切株、倒木Prefab直下の非表示子。MapCollision Layer、Collider2D一つ、Renderer / Rigidbody2D / Triggerなし。幹・接地点へ寄せる。
- MapObstacleVisualMarker: 必要なObstacle Visual Prefabだけが、生成時の占有FootprintとTilemapFootprint / CollisionBody方式・形状を保持する。RuntimeではColliderを生成しない。
- MapSocketMarker: 候補地点のSocket ID、用途、占有範囲、向き、Tagを保持する。Event種別は`PoolCandidate` / `FixedDefinition`を明示し、Pool候補は固定定義なしの正の個別半径、固定配置はEventDefinition参照 / IDと任意半径上書きを持つ。Transform位置と半径は独立し、抽選・近接判定はGameplay / Playtest側へ分離する。
- MapEventDefinition / MapEventCatalog: Eventの不変設定をScriptableObjectへ保存し、CatalogはListをInspectorの正本、DictionaryをRuntime検索索引として扱う。
- Main Camera: Orthographic、Transparency Sort Mode Custom Axis、Y軸。URP 2Dの正本はRenderer2D DataのCustom Axis / `Vector3.up`。

## 描画順

Player、ObstacleVisuals、Props、TallGrass、Reedsは同じ`WorldObjects` Sorting Layer、Order 0、`SpriteSortPoint.Pivot`を使う。Sprite Pivotのworld Yが低い足元ほど手前になり、Playerが木や宝箱の上下を歩くと位置関係に応じて前後が入れ替わる。

FlowerPatchとWildflowersは地面へ張り付く平面装飾なので`MapDetail`へ固定する。Ground / Terrainは`MapGround`の固定背面、Foregroundは`MapForeground`の固定前面とし、足元Yソートへ混ぜない。旧`Characters` Sorting Layerはunique IDを維持したまま`WorldObjects`へ改名する。

## 表示と判定

移動Physicsの正本はMapCollision Layerとする。水・外周・崖はCollisionTilemap、森・木・低木・岩・切株・倒木はPrefab内CollisionBodyを使う。見た目のSprite輪郭や生成用Footprintをゲーム判定へ流用せず、樹冠や枝の下は歩ける。Decorations / Propsは引き続き判定を持たない。

## v4.3 Sample Scene

GbaForestMapSampleはゲーム本番Sceneではなく、制作Kitの回帰確認用。30x20 Cellへ次を配置し、一画面で確認する。

- Grass Surface 8差分を参照する単一`GroundVariationTile`、Stone Variant、Dirt / Stone Cardinal-16、Water Blob-47。
- Tall Grass、Flower、Wildflowers、ReedsのDecoration Prefab 4種。
- 5 Obstacle Visualを、継ぎ目が機械的に見えないよう重ねた森壁と崖。
- Tree 3差分、BlockingBush、MossyRock、Stump、FallenLogをObstacleVisualsへ自由配置。
- MushroomPatch、Chest、SignpostをPropsへ自由配置。
- 水・崖の不可視Collision Tileと、幹・接地点へ寄せたPrefab内CollisionBody。
- Position 1 / 32 unit、Rotation 0、Scale 1。
- PlayerStart / Enemy / Loot Socket。
- 単一v4.3 Production Atlas以外の表示Texture参照がないこと。
- Main CameraはPosition `(-0.5, -0.25)`、Orthographic Size `10.6`。

Sample道路は次の順序で生成する。

1. 入口、広場、水辺を結ぶ複数の直交Waypoint列を定義する。
2. `MapRoadPathRasterizer`が各区間を両端込みで1 CellずつRasterizeする。
3. Radius 1の正方形Brushを重ね、角に穴のない3 Cell幅へ膨張する。
4. 全道路Cellが4近傍で単一Componentかを検証する。
5. 形状確定後にDirt / Stone材質を割り当てる。
6. Dirt / Stone双方の`RoadConnectionRuleTile`へ同じ`road`グループを設定し、広場境界を接続する。

Groundは全600 Cellへ同一`GroundVariationTile`を保存し、表示時に8差分を座標Hashで選ぶ。同一差分のCardinal隣接率は12%、水平・垂直の最大連続は4 Cell、offset 4 / 8 / 16の一致率はいずれも14%未満。短周期の剰余式、Shader、回転、反転を使わない。

Dirt / Stoneは全16 Cardinal Maskそれぞれに4 Spriteを持ち、`RoadConnectionRuleTile`が横4 Cell区間で各差分を1回ずつ使う順列を、XY座標、Seed、Rule IDのhashで決める。各接続辺は中央`[6,26)`の20px固定Socketとし、両端6pxは直交する2方向のbitがともに接続する場合だけ埋める。`05` / `0a`は20pxのみ、太道内角はAND条件で角埋めする。非`0f`の路面外は透過で、GroundTilemapの草地を下地として見せる。Waterは全面Mask `ff`だけ4 SpriteのRandom / Fixed、他MaskはSingle / Fixedとする。

表示PrefabはDetail 4、Obstacle Visual 5、Prop系10の計19点。Unity Rebuild、Sample Validator、Event Socket操作を含むMap Authoring EditMode Test 63 passed / 0 failed / 0 skipped、Gameplay Character / Eventを含む全EditMode Suite 83 passed、PlayMode Test 4 passed / 0 failed / 0 skippedで検証済み。最新のSample SSは`Assets/Art/Generated/MapAuthoring/QA/gba-forest-sample-unity.png`。目視QAでDecorationの地面矩形、道路内の草穴、Dirt / Stone境界の閉じた草縁、接続辺の分断、暗いCell格子、規則的なGrass / Road Edge反復がないことを確認済み。Sample QA SHA-256は`DD256066AC7DBD5C5A4BC798F750908A8C44D54A8DFE1483A01D9E3DE1E4275E`。

## 96x72 Battle Royale Reference Scene

`GbaForestBattleRoyaleReference`は、同じHierarchyとAsset契約を実ゲーム規模へ適用した最初の完成基準Scene。

- Ground 6,912 Cell、Road 1,479 Cell、Water 352 Cell、CollisionTilemap 684 Cell。森・木・岩などは別途Prefab内CollisionBodyを持つ。
- Obstacle Visual 232 Instance、Decoration 115 Instance、Props 16 Instance、Socket 60点。
- Player 1 + CPU 5を外周へ分散し、中央、北、東水辺、南のStone広場と西草地、北東、南東のLandmarkを複数Loopで接続する。
- 表示と判定の分離、Prefabの1 / 32 unit Snap、Rotation 0、Scale 1をTemplateと同じまま維持する。表示RootとDecoration / Propsには物理Componentを置かず、規定のCollisionBodyだけを許可する。
- Builder内の連結・到達性検査、`MapAuthoringValidator`、Event Socket操作を含むMap Authoring 63件のEditMode回帰Test、4件のPlayMode Testが成功済み。
- 6個のEventSocketは配置固有の`Event_01`〜`Event_06`を保ち、すべて固定定義なしの`PoolCandidate`とする。Builder再生成時も既存EventSocketのTransform位置と個別半径をSocket IDで復元する。

QA画像は`Assets/Art/Generated/MapAuthoring/QA/gba-forest-battle-royale-reference-overview.png`、`-central.png`、`-waterfront.png`。

## Playtestとの境界

`GbaForestBattleRoyaleReference`を引き続き96x72 Map配置の正本とする。探索確認は`GbaForestBattleRoyaleExplorationPreviewDebug`からAdditive読込し、Reference SceneへPlayerやGameplay Controllerを保存しない。

Playtest側は`PlayerStart`、EventSocket、Ground範囲、単一Camera、MapCollision Layerを参照する。判定はScene保存済みのCollisionTilemapとCollisionBodyを使用し、Event Poolで有効化したSocketへの接近はTransform間距離で求める。RuntimeでObstacle Visual、Decoration、Prop、SocketへCollider / Triggerを追加しない。Reference Builderを再実行してもPlaytest固有Objectを失わない境界とする。

詳細: [Exploration Preview Debug Scene](exploration-preview-debug-scene.md)

## v3 / v4 / v4.1 / v4.2履歴

v3 Sampleは30x20 Cell、6 Tilemap、ForestWall / Cliff Blob-47、Props Grid、10 GameObject Brushを使っていた。v4移行で旧Hierarchyと旧配置方式を削除済み。

v4 Sampleは5 Tilemap、Detail Tile / Detail Palette、表示Prefab 15点を使用していた。v4.1ではDetail 4種をDecorationsへ移し、4 Tilemap、Palette 2点、表示Prefab 19点へ変更した。

v4.1 SampleはGrass 4 Tileと全面差分なしのRuleTileを使用し、39 EditMode Testと旧QA Hashで完了していた。v4.2ではHierarchyと自由配置方式を維持したまま、GrassVariationと全面Mask差分へ置換した。v4.3で道端の全Mask差分、透過下地、20px固定Socket、AND角埋め、4 Cell順列選択へ置換した。v3 / v4 / v4.1 / v4.2の検証値は履歴であり、v4.3の完成結果として扱わない。
