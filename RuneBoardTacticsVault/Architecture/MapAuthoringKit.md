# Map Authoring Kit

## 結論

FantasyRoyaleの固定マップは、Unity標準のTilemap、Prefab、Scene Templateを使って制作する。v4.3では、地形判定を不可視Grid、木などの接地判定を表示とは別の子Collider、装飾と障害物の見た目を自由配置Prefabへ分離し、地面と道端の反復抑制をTile Asset内の決定的な差分選択へ委ねる。

状態: **v4.3実装・Unity再構築・描画順を含め検証済み**

専用EditorWindow、独自マップファイル、独自Serializerは作らない。日常の編集はUnity標準画面で完結し、追加コードはAsset再生成、Atlas Import統一、道路形状生成、制作情報表示、Validatorに限定する。

## 正本

| 対象 | 正本 |
| --- | --- |
| マップ配置 | Unity Scene（`.unity`） |
| 移動可能 / 不可 | `MapCollision` Layer上の`CollisionTilemap`と`CollisionBody` |
| 森Themeの画像デザイン | `Assets/Art/Map/Source/Forest/forest-gba-design-master.png` |

移動Physicsの正本は、地形を表す`CollisionTilemap`と、幹・岩・切株などの接地点を表す`CollisionBody`を合わせた`MapCollision` Layerとする。Spriteの輪郭や論理FootprintからRuntimeで判定を復元しない。

Sceneには次をUnity標準形式で保存する。

| 情報 | 保存場所 |
| --- | --- |
| 地面・接続地形・前景 | TilemapのCell座標とTile Asset参照 |
| Tall Grass、Flower、Wildflowers、Reeds | Decorations配下のPrefab InstanceとTransform |
| 水・外周・崖などCell地形の移動不可領域 | CollisionTilemapのCell |
| 森、木、低木、岩、切株、倒木の移動不可領域 | Prefab直下`CollisionBody`のCollider2D |
| 森、崖、木、低木、岩、切株、倒木の見た目 | ObstacleVisuals配下のPrefab InstanceとTransform |
| Mushroom、Chest、Sign | Props配下のPrefab InstanceとTransform |
| Player Start・Enemy・Loot・Merchant・Landmark候補 | Sockets配下のMapSocketMarker |
| Eventの配置 | Sockets配下のMapSocketMarker（Socket ID、自由なTransform位置、Event定義参照 / ID、個別半径） |
| Eventの不変設定 | `MapEventDefinition` Assetと`MapEventCatalog`のList |
| Eventの使用済み・Cooldown状態 | ゲーム実行中のSocket ID単位Runtimeデータ |
| 描画順 | Renderer2Dの透明描画設定、Sorting Layer、Order in Layer、Sprite Pivot |

`.frmap` やJSON Mapを別途同期しない。将来、Unity外から同じデータを読む必要が生じた時点でSceneからExportする。

Event定義のCatalogはMap配置の正本ではなく、Event処理が参照する設定Assetの索引である。InspectorではListを編集し、ゲーム実行時だけID検索用Dictionaryを構築する。EventSocketは配置固有の`SocketId`と、手動配置では直接参照、生成データでは`EventDefinitionId`で同じ定義へ到達できる。Socket中心はScene上のTransformを正本とし、Cell中心へ固定しない。接近円は論理距離で、MapCollisionや表示とは独立する。

## Scene構造 v4.3

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

| 要素 | 用途 | 契約 |
| --- | --- | --- |
| GroundTilemap | 全Cellの草地土台 | MapGround / 0 |
| TerrainTilemap | Dirt、Stone、Water RuleTile | MapGround / 10 |
| CollisionTilemap | 水・外周・崖の占有・移動判定 | Renderer disabled、MapCollision |
| ForegroundTilemap | World Objectより手前のTile装飾 | MapForeground / 0 |
| Decorations | Tall Grass、Flower、Wildflowers、Reedsの自由配置表示 | plain Transform / WorldObjectsまたはMapDetail |
| ObstacleVisuals | 森、崖、Blocking Propの自由配置表示と分離済み接地判定 | plain Transform / WorldObjects / 0 |
| Props | Mushroom、Chest、Signの自由配置表示 | plain Transform / WorldObjects / 0 |

`ObstacleTilemap` と `DetailTilemap` はv4.3標準Hierarchyへ含めず、検出した場合は旧構造として扱う。`Decorations`、`ObstacleVisuals`、`Props`はGrid Componentを持たないplain Transformとし、PrefabをScene Viewで自由配置する。

CollisionTilemapはTilemapCollider2D、CompositeCollider2D、Static Rigidbody2Dを持つ。Decoration / PropsはDefault Physics LayerでCollider2D、Rigidbody2Dを持たない。Obstacle Prefabの表示RootもDefault Layerとし、`CollisionBody`方式だけが直下の非表示子へMapCollision LayerのCollider2Dを一つ持つ。子にRenderer、Rigidbody2D、Triggerは置かない。

## Project Layer

Sorting LayerはMapGround、MapDetail、WorldObjects、MapForeground、Effects、UIを維持する。旧`Characters`は同じunique IDのまま`WorldObjects`へ改名し、Playerだけでなく立体表示を共通比較する用途を明確にする。Physics LayerはCollisionTilemapとObstacle Prefab直下の`CollisionBody`だけがMapCollisionを使用する。MapInteractionは将来のInteraction実装用に残すが、見た目PrefabへTriggerを持たせない。

自由配置するPixel Artは次を守る。

- Positionは1 / 32 Unity unit単位へSnapする。
- Rotationは0。
- Scaleは1。
- Sprite PivotとY軸Sortingで足元基準の前後関係を保つ。

## 描画順契約

URP 2Dの描画順は`Assets/Settings/Renderer2D.asset`を正本とし、Transparency Sort ModeをCustom Axis、軸を`Vector3.up`へ固定する。同じSorting LayerとOrderではSprite Pivotのworld Yが低いものほど手前に描画する。

- Player、ObstacleVisuals、Props、TallGrass、Reedsは`WorldObjects` / Order 0 / `SpriteSortPoint.Pivot`とする。カテゴリ別の固定Orderを使わず、足元の位置関係で互いに前後を入れ替える。
- FlowerPatchとWildflowersは背丈のない平面装飾として`MapDetail` / Order 0 / `SpriteSortPoint.Pivot`へ固定し、Playerや立体表示より背面に置く。
- GroundTilemapとTerrainTilemapは`MapGround`の固定背面、ForegroundTilemapは`MapForeground`の固定前面とする。これらはworld Y比較の対象にしない。
- Camera側にもCustom Axis / `Vector3.up`を設定するが、URP 2Dで実描画に使う設定の正本はRenderer2D Dataとする。

## GBA素材契約 v4.3

| 項目 | 設定 |
| --- | --- |
| Production Texture | 単一Atlas PNG |
| Sprite Catalog | `fantasyroyale.map-authoring-atlas.v4.3` |
| Named Sub-Sprite | 205点 |
| 基準Tile | 32x32 pixel |
| Pixels Per Unit | 32 |
| Filter Mode | Point |
| Compression | None |
| Mipmap | Off |
| Wrap Mode | Clamp |
| Sprite Mode | Multiple |
| Sprite Mesh | Full Rect |
| Alpha | Alpha Is Transparency On |
| Tile Pivot | 中央 |
| Decoration / Prop / Obstacle Pivot | 足元中央 |
| 光源 | 左上 |
| 回転・反転・任意変形 | 禁止 |

Sprite内訳:

| Family | 構成 | Sprite数 |
| --- | --- | ---: |
| Grass | Coordinate Hash Variation | 8 |
| Dirt | Cardinal-16 x 4輪郭差分 | 64 |
| Stone | Cardinal-16 x 4輪郭差分 | 64 |
| Water | Blob-47、`ff`のみ4差分 | 50 |
| Detail | Static | 4 |
| Props | Static | 10 |
| Obstacle Visual | Static / free placement | 5 |
| **合計** |  | **205** |

ForestWall / Cliff Blob-47はv4.3へ持ち込まない。新しいObstacle Visualは次の5点。

- `obstacle_forest_mass_wide`
- `obstacle_forest_mass_deep`
- `obstacle_forest_front_strip`
- `obstacle_cliff_straight_wide`
- `obstacle_cliff_outer_corner`

## RuleTileと道路生成契約

v4.3でRuleTileとして残すのはDirt、Stone、Waterだけ。

- Dirt / Stone: N/E/S/Wを1/2/4/8 bitとするCardinal-16。
- Water: 条件付き対角を含むBlob-47。
- Dirt / StoneはCardinal-16の全16 Maskそれぞれに4 Spriteを持つ。`RoadConnectionRuleTile`がCellのXY座標、Seed、Rule IDのhashから差分を決定する。
- Waterは全面Mask `ff`だけ4 SpriteをRandom出力し、他の46 MaskをSingle出力する。
- Rule TransformとVariation TransformはFixed。回転、反転を使わない。
- Dirt / Stoneは`RoadConnectionRuleTile`とし、双方へ同じ`road`接続グループを設定する。
- Dirt / Stoneの各接続辺は中央`[6,26)`の20px固定Socketを使う。残り両端6pxはその辺と直交する2方向のbitが両方とも接続する場合に限り埋める。
- 直線Mask `05` / `0a`は20px Socketだけで周期的なタブを持たない。太い道路の内角は直交bitのANDで条件付き角埋めし、芝穴を防ぐ。
- 非接続の露出辺のみを4つのRoad Edge Sourceで不規則にし、路面外を透過にしてGroundTilemapの草地を見せる。焼き込み草地をTerrainTilemapから重ねない。
- `0f`は草縁やCell外周陰影を持たない全面路面とし、太い道路内部の草穴と暗いCell格子を防ぐ。
- `RoadConnectionRuleTile`は横4 Cellの各区間で4差分を一度ずつ使い、座標、Seed、Rule IDのhashで24順列を切り替える。

Groundは装飾なしのAI Grass Surface 8枚を一つの`GroundVariationTile`へ設定する。TileはCell座標とSeedを強い32bit 2D hashへ混合し、再生成後も同じCellへ同じSpriteを返す。短周期の剰余式、Shader、回転、反転は使わない。

ForestWall / Cliffの接続はRuleTileで表現せず、ObstacleVisual Prefabを重ねて自然な境界を作る。

Sample道路は次の順序を固定する。

1. 複数の直交Waypoint列を定義する。
2. `MapRoadPathRasterizer`が両端込みで1 Cellずつ中心線をRasterizeする。
3. Radius 1の正方形Brushを各中心Cellへ重ね、3 Cell幅へ膨張する。
4. 全道路Cellが4近傍で単一Componentかを検証する。
5. 形状を変更せずDirt / Stone材質を割り当て、共通`road`グループで境界を接続する。

## Prefab分類

v4.3の表示Prefabは計19点。全ての表示RootはDefault Layer、Position 1 / 32 unit、Rotation 0、Scale 1を共通契約とする。Rigidbody2Dは持たず、物理判定が必要なPrefabだけが表示と分けた直下子`CollisionBody`を持つ。

### Decorations

- TallGrass。
- FlowerPatch。
- Wildflowers。
- Reeds。

Detail 4種はTileではなく自由配置Decoration Prefabとする。通行可能な見た目であり、CollisionTilemapを変更しない。

### ObstacleVisuals

自由配置する表示Prefab:

- Obstacle Visual 5種。
- TreeMedium01 / 02 / 03。
- BlockingBush、MossyRock、Stump、FallenLog。

各Prefabの表示RootはDefault Layer、Collider / Rigidbodyなし。12点が`MapObstacleVisualMarker`を持ち、生成時の広めの占有Footprintと物理方式を別々に保持する。森3種、Tree 3種、BlockingBush、MossyRock、Stump、FallenLogの10点は`CollisionBody`方式で、幹・根元・接地点へ寄せたBoxまたはCapsuleを持つ。崖2種だけは`TilemapFootprint`方式で、CollisionTilemapへ明示Paintする。

### Props

- MushroomPatch。
- Chest。
- Signpost。

PropsもCollider / Rigidbodyなし、Default Layer、自由配置とする。v3の1Cell GameObject BrushとProps Gridは使用しない。

## Tile Palette

- Ground用: Grass、Dirt、Stone、Water。
- Collision用: 表示Spriteを持たないCollision Paint Tile。

PaletteはGround / Collisionの2点だけとする。旧Detail Paletteを削除し、再生成もしない。Detail 4種はProject BrowserからDecorationsへ、ForestWall / CliffはObstacleVisualsへPrefabをDragして配置する。

標準表示用Tile Assetは`GrassVariation`とStone Variantの計2点。`GrassVariation`はAtlas内のGrass 8 Spriteを座標Hashで選ぶ。これとは別にDirt / Stone / Water RuleTile 3点と、表示Spriteを持たないCollision Tile 1点を保持する。

## 制作手順

1. UnityのNew SceneからGBA Forest Mapを選ぶ。またはTemplate Sceneを複製する。
2. Ground / Terrain / Foreground Tilemapへ必要な表示Tileを描く。
3. 水、外周、崖などCell地形の移動不可範囲をCollisionTilemapへ塗る。
4. Tall Grass、Flower、Wildflowers、Reeds PrefabをDecorationsへDragする。
5. 森、崖、木、低木、岩、切株、倒木PrefabをObstacleVisualsへDragする。
6. Mushroom、Chest、Sign PrefabをPropsへDragする。
7. 自由配置Prefabを1 / 32 unitへSnapし、Rotation 0、Scale 1を確認する。`CollisionBody`はPrefab内で見た目と一緒に移動するためScene上で別調整しない。
8. MapSocketMarkerをSocketsへ配置する。EventはTransformを目的物の接点へ動かし、Radius Handleで必要な接近半径を調整する。
9. Event中心が障害物上にある場合も、接近円内にPlayerが立てるGroundがあることを確認する。
10. `FantasyRoyale/Map Authoring/Validate Active Scene` を実行する。

## 保守用再生成

`FantasyRoyale/Map Authoring/Rebuild Complete Kit` のv4.3出力:

- v4.3 Production Atlasの再ImportとCatalog Slice。
- GrassVariationとStone Variant、Dirt / Stone / Water RuleTile、不可視Collision Tile。
- Ground / Collision用Palette 2点。
- Detail 4、Obstacle Visual 5、Prop系10の表示Prefab計19点。
- 4 Tilemapと3 plain表示Rootを持つ空のv4.3 Authoring SceneとScene Template。
- 3 Cell幅道路、Tilemap地形判定と足元CollisionBodyの分離、自由配置を確認するSample Scene。

これは日常の制作UIではなく、Git管理Assetを揃えるための保守コマンド。

## v4.3検証状況

- schema v4.3、205 named Sub-Spriteを契約とする。
- 4 Tilemap、Decorations / ObstacleVisuals / Props plain Transformを契約とする。
- DetailTilemap、Detail Palette、ObstacleTilemap、GameObject Brushを現行構造として禁止する。
- 表示Prefab 19点、Palette 2点、標準表示用Tile Asset 2点を契約とする。
- GrassVariationが8 Spriteを全て使い、30x20 Sampleで同一差分Cardinal隣接率12%、最大連続4 Cell、offset 4 / 8 / 16一致率14%未満であることを確認済み。
- Dirt / Stoneの全16 Maskが各4 Spriteの座標hash差分 / Fixedで、Waterは`ff`だけが4 SpriteのRandom / Fixedである。
- Dirt / Stoneの接続辺は20px固定Socket、両端6pxは直交bitのAND条件、露出辺は不規則輪郭、非`0f`の路面外は透過である。
- `05` / `0a`に周期タブがなく、太道内角に芝穴がないことを自動検査する。Road差分は4 Cell区間で各差分を1回ずつ使う決定的順列とする。
- Shader、回転、反転による地面差分を使わない。
- Unity Rebuild、Validator、Camera Captureに成功。
- 最新のUnity Sample QA: `Assets/Art/Generated/MapAuthoring/QA/gba-forest-sample-unity.png`。
- EditMode Test: **61 passed / 0 failed / 0 skipped**。
- PlayMode Test: **4 passed / 0 failed / 0 skipped**。
- Production Atlas SHA-256: `412C63A03CD84472A9928DA5BF687903DB47AD3FF75DAECD246222EDD50D5FF4`、Catalog SHA-256: `EA6A6D111FF41DE5081D7537C5A20D0F74F2854D1C492D747FF94F9070B68600`。
- Production Atlas QA SHA-256: `34923AEC3E2721EA1D2491D18A8CE15C6AAF65E5FF2799001367080838B848F1`、Unity Sample QA SHA-256: `DD256066AC7DBD5C5A4BC798F750908A8C44D54A8DFE1483A01D9E3DE1E4275E`。
- 目視QAで装飾の矩形地面、道路内の草穴、Dirt / Stone境界の草縁、暗いCell格子がないことを確認済み。

## v3 / v4 / v4.1履歴

v3は2026-08-02にUnity 6000.4.3f1で検証済みだった実装履歴。191 Sprite、ForestWall / Cliff Blob-47、6 Tilemap、Props Grid、GameObject Brush、Prefab Colliderを使用した。v4では判定Gridと自由配置表示を分けるため、現行仕様として扱わない。

v4は5 Tilemap、Detail Tile / Detail Palette、表示Prefab 15点を使用していた旧契約。v4.1ではDetail 4種をDecorations配下のPrefabへ移し、道路接続基盤を追加した。

v4.1は4つのGrass Tileと全Mask Single / FixedのDirt 16、Stone 16、Water 47を使用し、102 Sprite、39 EditMode Testで完了していた。地面の32px反復を抑えるため、v4.2で装飾なしGrass Surface 8枚、全面路面・水面差分、`GroundVariationTile`を導入した。

v3 / v4 / v4.1の検証値は履歴としてのみ保持する。v4.2は115 Spriteと全面Maskだけの差分選択で完了していたが、道端の焼き込み草地と輪郭反復を解消するためv4.3へ置換した。v4.3のHashとTest結果にはv4.3実物から取得した値を使う。
