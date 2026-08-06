# MapAuthoringKitBuilder

状態: **v4.3実装・Unity Rebuild / Validator / EditMode Test成功**

## 役割

単一Production Atlasのnamed Sub-Spriteから、Unity標準のマップ制作に必要なGit管理Assetを再構築する保守用Editor Builder。

- Script: `Assets/Editor/MapAuthoring/MapAuthoringKitBuilder.cs`
- メニュー: `FantasyRoyale/Map Authoring/Rebuild Complete Kit`

日常のマップ編集UIではない。Pixelも作画しない。

## 入力

- `Assets/Art/Map/Production/Forest/forest-gba-production-atlas.png`
- `Assets/Art/Map/Production/Forest/forest-gba-production-atlas.json`
- v4.3 schema `fantasyroyale.map-authoring-atlas.v4.3`、205 named Sprite。
- ProjectSettingsのSorting LayerとPhysics Layer。
- `Assets/Settings/Renderer2D.asset`の透明描画設定。
- `GbaForestMapAuthoringSceneBuilder`。

Source画像からAtlas / Catalogを作る責務はAtlas build処理に置く。このBuilderは完成AtlasをImportし、Unity Assetの参照を設定する。

Atlas build処理は次の4枚のSurface Source Sheetと2枚のRoad Edge Profile Sheetを正規化する。Builder自身はこれらを直接読まず、Catalogへ記録された完成Sub-Spriteだけを使う。

- `grass-surface-variants-source.png`: 4 x 2、Grass 8差分。
- `dirt-surface-variants-source.png`: 2 x 2、全面Dirt 4差分。
- `stone-surface-variants-source.png`: 2 x 2、全面Stone 4差分。
- `water-surface-variants-source.png`: 2 x 2、全面Water 4差分。
- `dirt-edge-profile-variants-source.png`: Dirt道端の不規則な輪郭4差分。
- `stone-edge-profile-variants-source.png`: Stone道端の不規則な輪郭4差分。

## v4.3出力

- Grass 8 Spriteを座標Hashで選ぶ`Ground/GrassVariation.asset`一つと、Stone Variant 1点の標準Tile。
- Dirt / Stone Cardinal-16 `RoadConnectionRuleTile`。
- Water Blob-47 RuleTile。
- 表示Spriteを持たないCollision Tile。
- Ground、Collisionの2 Tile Palette。Ground PaletteはGroundVariationTile、Dirt、Stone、Water、StoneVariantの5点を収録する。
- MapSocketMarkerのSample配置はSocket IDを明示する。Event Socketを追加する場合はMapEventDefinition参照または定義IDを渡し、Builderはイベント処理を実行しない。
- 5 Obstacle Visual、既存Props 10点、Decoration 4点の計19表示Prefab。うち10障害物は幹・接地点CollisionBody、崖2種はTilemap Footprint方式。
- Renderer2D DataのTransparency Sort ModeをCustom Axis、軸を`Vector3.up`へ統一する。
- Ground、Terrain、Collision、Foregroundの4 Tilemapとplain Decorations / ObstacleVisuals / Propsを持つAuthoring SceneとScene Template。
- 表示と判定の分離、地面差分、装飾の自由配置、道路連結、道端輪郭差分を確認するv4.3 GbaForestMapSample。

ForestWall / Cliff RuleTile、Obstacle Palette、Detail Tile Asset、Detail Palette、GameObject Brushは出力しない。再構築時に旧Detail Folder、旧Detail Palette、旧`Grass01.asset`〜`Grass04.asset`も削除する。

### GroundVariationTile

`ground_grass_01`〜`ground_grass_08`を一つの`GroundVariationTile`へ設定する。Sceneへ保存するTile参照は全Cellで同じ`GrassVariation.asset`とし、表示SpriteだけをCell座標と固定Seedから決定的に選ぶ。短周期の剰余表ではなく32bit avalanche hashを使うため、再ImportやScene再生成後も同じ座標へ同じ差分を返しながら、旧16 Cell周期を持ち込まない。

## RuleTile生成

### Cardinal-16

DirtとStoneのMask bitはNorth 1、East 2、South 4、West 8。全16 Maskが基準名と`_v02`〜`_v04`の計4 Spriteを`Random`出力する。Rule TransformとRandom Transformはどちらも`Fixed`とし、回転や反転で光源方向を変えない。

Dirt / Stoneは`RoadConnectionRuleTile`として生成し、両方へ`road`接続groupと表示差分Seedを設定する。Rule判定では同じgroupの異なるTileも接続扱いにし、Dirt / Stone境界で草付き終端を描かない。既存Assetが通常RuleTileの場合は型契約を更新して作り直す。

Rule AssetではUnity標準と互換な`Random / Fixed`として4 Spriteを保存するが、実表示は`RoadConnectionRuleTile.GetTileData`が選ぶ。基底RuleTileで接続Maskを決めた後、横4 Cell区間ごとに24順列の一つを座標、Rule ID、SeedのHashで選び、区間内で4差分を各1回使う。Z座標は無視し、負のX座標も床除算補正で同じ4 Cell区切りへ揃える。これにより標準Perlinの局所的な偏りを避け、区間境界を含む同一差分の横連続を最大2 Cellへ抑える。

### Blob-47

WaterだけがN/NE/E/SE/S/SW/W/NWの8近傍を使う。条件付き対角から47 Canonical Maskを選び、全面Mask `ff`だけが`water_ff`と`water_ff_v02`〜`v04`の計4 Spriteを`Random`出力する。他46 MaskはCatalog名に対応する一枚を`Single`出力する。Rule TransformとRandom Transformは`Fixed`とし、回転と反転は使わない。

## Prefab生成

Decorations向け:

- TallGrass / FlowerPatch / Wildflowers / Reeds

ObstacleVisuals向け:

- `obstacle_forest_mass_wide`
- `obstacle_forest_mass_deep`
- `obstacle_forest_front_strip`
- `obstacle_cliff_straight_wide`
- `obstacle_cliff_outer_corner`
- TreeMedium01 / 02 / 03
- BlockingBush / MossyRock / Stump / FallenLog

Props向け:

- MushroomPatch / Chest / Signpost

全Prefabの表示RootはDefault Physics Layer、Collider2Dなし、Rigidbody2Dなしとする。Obstacle、Props、TallGrass、Reedsは`WorldObjects` / Order 0 / `SpriteSortPoint.Pivot`へ揃え、world Yが低い足元ほど手前になる共通ソートへ参加させる。FlowerPatchとWildflowersは平面装飾として`MapDetail` / Order 0 / Pivotへ固定する。Decorationはplain Decorationsへ1 / 32 unit自由配置し、`MapObstacleVisualMarker`を持たない。

Obstacle 12点へMarkerを設定する。森3種、Tree 3種、BlockingBush、MossyRock、Stump、FallenLogには、表示Root直下へ非表示`CollisionBody`を生成し、素材の幹・根元・接地点に合わせたBox / Capsuleを一つ置く。子はMapCollision Layer、Renderer / Rigidbody2D / Triggerなし。崖2種だけはTilemap Footprint方式とし、Sample生成時にMarker範囲をCollisionTilemapへ保存する。

生成時の占有Footprintは道路や素材同士の間隔確保へ残し、物理Colliderとは分離する。森林の樹冠や単木の表示矩形全体を1 Cell Colliderへ戻さない。

## Sample道路生成

1. 入口、広場、分岐を結ぶ直交Waypoint列を定義する。
2. `MapRoadPathRasterizer`で中心経路をRadius 1の正方形Brushへ膨張し、基本3Cell幅へする。
3. 複数経路、曲がり角、分岐を一つのCell集合へ統合する。
4. `IsSingleCardinalComponent`で4近傍の単一連結を確認する。
5. 連結済み道路集合へ最後にDirt / Stone材質を割り当てる。

形状生成と材質割当を分けることで、曲がり角の穴、1Cell道路の途切れ、Dirt / Stone境界の分断を防ぐ。

Atlas側のCardinal-16は各辺の中央`[6, 26)`、すなわち20pxを基本Socketとする。角6pxは、その辺と直交辺のCardinal bitが両方開く場合だけ路面で埋める。直線Mask `05` / `0a`では角を透過させ、曲がり・分岐・全面Maskだけ必要な角を接続する。非全面Maskの外側は透過させてGroundを見せるため、内部継ぎ目の草穴を防ぎながら、Cell境界へ32px全幅の規則的な張り出しを作らない。4枚の道端Profile差分を全Maskへ展開し、同じ輪郭がCell境界ごとに反復する印象も抑える。

## 生成時検査

- 単一Production Atlasとv4.3 Catalogの存在、205 Spriteの完全一致。
- GroundVariationTileが`ground_grass_01`〜`08`だけを持ち、旧Grass01〜04 Tile Assetがないこと。
- Dirt / Stone / Water以外のRuleTileを生成しないこと。
- Dirt / Stoneが同じroad接続groupを持つ`RoadConnectionRuleTile`であること。
- Dirt / Stoneの全16 Maskが4 SpriteのRandom / Fixed、Waterは`ff`だけが4 SpriteのRandom / Fixedで他46 Maskが1 SpriteのSingle / Fixedであること。
- Dirt / Stoneの座標差分が4 Cell区間内で4種を各1回使用し、区間境界を含む同一差分の横連続を2 Cell以下に保つこと。
- Dirt / Stone全128 Spriteの4辺・外周2pxが中央20pxと条件付き角のSocket topologyに一致し、直線Mask `05` / `0a`へ角タブが再混入しないこと。
- 全Tile / RuleTile / Prefabの表示Spriteが単一Production Atlasを参照すること。
- CollisionTileの表示Spriteが`null`。
- 19表示Prefabの表示RootがDefault LayerかつCollider2D / Rigidbody2Dなしであること。10点のCollisionBodyだけがMapCollision Layerの規定Colliderを一つ持ち、崖2種はColliderなしであること。
- Renderer2D DataがCustom Axis / `Vector3.up`で、立体表示PrefabがWorldObjects / Order 0 / Pivot、平面装飾がMapDetail / Order 0 / Pivotであること。
- Decoration 4点にMarkerがなく、旧Detail Tile AssetとDetail Paletteがないこと。
- 4 Tilemap標準Hierarchy、Ground全600 Cellの単一Tile参照、自由配置Transform、Tilemap / CollisionBody判定分離、道路の単一連結をSampleが満たすこと。
- Sample保存後のMapAuthoringValidator Errorが0。

Graphics DeviceのないbatchmodeではCaptureを安全に省略する。v4.3 + Event Socket操作 EditMode Suiteは**61 passed / 0 failed / 0 skipped**。

## 旧方式の履歴

- v3 Builderは191 Spriteから5 RuleTile、Obstacle Palette、Collider付き10 Prop Prefab、10 GameObject Brush、6 Tilemap Sampleを生成していた。
- v4 Builderは5 Tilemap、3 Palette、Detail Tile 4点、15表示Prefab、同一Tileだけを接続するDirt / Stone RuleTileを生成していた。
- v4.1ではschema v4.1 / 102 Sprite、Grass標準Tile 4点、各Mask 1 SpriteのSingle出力、4 Tilemap、2 Palette、19表示Prefab、共通road接続groupとRasterizerを使用していた。
- v4.2ではSurface Source 4 Sheet、115 Sprite、GroundVariationTile、全面MaskだけのRandom差分へ更新した。v4.1以前は履歴であり、現行出力として使わない。
- v4.3ではRoad Edge Profile 2 Sheetを追加し、Dirt / Stoneの全Maskを各4差分へ展開して205 Spriteとした。表示差分は座標Hashと4 Cell区間内の順列で決定し、v4.2以前の道路差分契約は履歴としてのみ扱う。
