# Forest Production Atlas Contract

## 位置づけ

森Themeの画像素材とUnity参照方法を固定する契約。

状態: **v4.3実装・Unity再構築・検証済み**

ユーザー指定画像を、色、陰影、材質、輪郭、GBA風Pixel表現の変更不可なデザイン正本とする。

- 正本: `Assets/Art/Map/Source/Forest/forest-gba-design-master.png`
- SHA-256: `D90F51F33EFB470728924B8088035E777AAE7E0DF133AB3A799522114A50B705`
- Production Atlas: `Assets/Art/Map/Production/Forest/forest-gba-production-atlas.png`
- Sprite Catalog: `Assets/Art/Map/Production/Forest/forest-gba-production-atlas.json`

正本は展示用の不均一なCellを含むため、Unityから直接参照しない。正本準拠で作画したSourceを規格化し、単一Production Atlasへ格納する。Codeは新しい色や輪郭を発明せず、切り出し、Magenta Key除去、規格化、承認済みDonorによる道路接続形状の決定的合成、承認済みAI Surface差分の格納、Packing、Catalog生成、検査だけを担当する。

## v4.3 Production契約

| 項目 | 契約 |
| --- | --- |
| Catalog schema | `fantasyroyale.map-authoring-atlas.v4.3` |
| Named Sub-Sprite | 205点 |
| 基準Tile | 32x32 pixel |
| Pixels Per Unit | 32 |
| Import | Multiple、Point、Compressionなし、Clamp、Mipmapなし |
| Sprite Mesh | Full Rect |
| Tile Pivot | 中央 |
| Decoration / Prop / Obstacle Pivot | 足元中央 |
| 光源 | 左上 |
| 回転・反転・色替え | 禁止 |

- Production Atlas SHA-256: `412C63A03CD84472A9928DA5BF687903DB47AD3FF75DAECD246222EDD50D5FF4`。
- Sprite Catalog SHA-256: `EA6A6D111FF41DE5081D7537C5A20D0F74F2854D1C492D747FF94F9070B68600`。

## Family

| Family | Topology | Sprite数 | Unityでの配置先 |
| --- | --- | ---: | --- |
| Grass | Coordinate Hash Variation | 8 | GroundTilemap |
| Dirt | Cardinal-16 x 4輪郭差分 | 64 | TerrainTilemap |
| Stone | Cardinal-16 x 4輪郭差分 | 64 | TerrainTilemap |
| Water | Blob-47 + `ff` Surface 4差分 | 50 | TerrainTilemap |
| Detail | Static | 4 | Decorations配下のPrefab |
| Props | Static | 10 | ObstacleVisualsまたはProps |
| Obstacle Visual | Static / free placement | 5 | ObstacleVisuals |
| **合計** |  | **205** |  |

Collision Paint Tileは表示Spriteを持たないためAtlasへ含めない。ForestWall / Cliff Blob-47もv4.3 Atlasへ含めない。

## Surface Source

正本の色、陰影、材質、左上光源へ準拠し、地面の32px反復を抑えるために生成した次のAI Surface Sheetをv4.3の地面質感Sourceとする。

| Family | Source画像 | 差分数 | 用途 |
| --- | --- | ---: | --- |
| Grass | `Assets/Art/Generated/MapAuthoring/Source/Surfaces/grass-surface-variants-source.png` | 8 | `GroundVariationTile`の全面芝 |
| Dirt | `Assets/Art/Generated/MapAuthoring/Source/Surfaces/dirt-surface-variants-source.png` | 4 | Cardinal-16の路面質感Donor |
| Stone | `Assets/Art/Generated/MapAuthoring/Source/Surfaces/stone-surface-variants-source.png` | 4 | Cardinal-16の石畳質感Donor |
| Water | `Assets/Art/Generated/MapAuthoring/Source/Surfaces/water-surface-variants-source.png` | 4 | Blob Mask `ff`の全面水面 |

Grass 8枚は花、背高草、石などの独立した装飾を含まない低コントラストの草面とする。装飾を地面差分へ焼き込まず、TallGrass、FlowerPatch、Wildflowers、Reedsの自由配置Prefabと責務を分ける。Surface差分はShader、回転、反転、色替えで作らず、作画済みSpriteをそのまま使う。

## Road Edge Source

Dirt / Stoneの露出端には、正本に準拠した次の4輪郭差分を使う。内部の旧草地はPackerで透過化し、GroundTilemapの草地を下地として見せる。

| Family | Source画像 | 輪郭差分数 |
| --- | --- | ---: |
| Dirt | `Assets/Art/Generated/MapAuthoring/Source/Edges/dirt-edge-profile-variants-source.png` | 4 |
| Stone | `Assets/Art/Generated/MapAuthoring/Source/Edges/stone-edge-profile-variants-source.png` | 4 |

## Obstacle Visual Source

正本を参照して生成した次の5画像を、v4.3の自由配置Obstacle Visual Sourceとする。

| Sprite名 | Source画像 |
| --- | --- |
| `obstacle_forest_mass_wide` | `Assets/Art/Generated/MapAuthoring/Source/ObstacleVisuals/obstacle-forest-mass-wide.png` |
| `obstacle_forest_mass_deep` | `Assets/Art/Generated/MapAuthoring/Source/ObstacleVisuals/obstacle-forest-mass-deep.png` |
| `obstacle_forest_front_strip` | `Assets/Art/Generated/MapAuthoring/Source/ObstacleVisuals/obstacle-forest-front-strip.png` |
| `obstacle_cliff_straight_wide` | `Assets/Art/Generated/MapAuthoring/Source/ObstacleVisuals/obstacle-cliff-straight-wide.png` |
| `obstacle_cliff_outer_corner` | `Assets/Art/Generated/MapAuthoring/Source/ObstacleVisuals/obstacle-cliff-outer-corner.png` |

5画像はbuilt-in `imagegen`で生成し、デザイン正本を見た目の参照にしてMagenta Keyを透明化した初期素材。分割したBody / Canopy素材ではなく、それぞれ一体の完成表示モジュールとして扱う。正式素材へ差し替える可能性は残すが、Sprite名、足元Pivot、左上光源、役割は維持する。

## Decoration Source

正本を参照してbuilt-in `imagegen`で生成したTallGrass、FlowerPatch、Wildflowers、Reedsの4画像を、v4.3の自由配置Decoration Sourceとする。地面の矩形を含めず、Magenta Keyを透明化し、足元中央PivotでPrefab化する。保存先、プロンプト概要、Hash、元出力は[[../Dev/Assets/GeneratedAssets|Generated Assets]]を正とする。

## 接続素材

RuleTileとして接続するFamilyはDirt、Stone、Waterだけとする。

- Dirt / StoneはN/E/S/Wを1/2/4/8 bitとするCardinal-16。
- Waterは条件付き対角を含むBlob-47。
- Dirt / StoneはCardinal-16の全16 Maskそれぞれに4 Spriteを持つ。`RoadConnectionRuleTile`がCellのXY座標、Seed、Rule IDのhashから差分を決定し、同じSceneとSeedでは常に同じ輪郭を返す。
- Waterは全面Mask `ff`だけ4枚を`Random`出力し、他の46 Maskは一枚を`Single`出力する。
- Rule TransformとVariation TransformはFixed。回転、反転を使わない。
- Dirt / Stoneは`RoadConnectionRuleTile`とし、双方へ同じ`road`接続グループを設定する。
- Dirt / Stoneの各接続辺は中央`[6,26)`の20pxを固定Socketとする。辺の残り両端6pxは、その辺と直交する2方向のbitが両方とも接続する場合に限り埋める。
- 直線Mask `05` / `0a`は20px Socketだけとし、周期的なタブを付けない。太い道路の内角は直交bitのAND条件でのみ埋め、Groundの芝穴を残さない。
- 非接続の露出辺だけを輪郭Sourceから不規則に作り、路面外は透過とする。透過部分はGroundTilemapの草地をそのまま見せ、道端にCell単位の焼き込み草地を重ねない。
- 4輪郭差分は路面の色・材質と接続Topologyを保ちつつ、露出端の凹凸だけを変える。`0f`は草縁やCell外周陰影を持たない全面Surfaceとする。
- `RoadConnectionRuleTile`は横4 Cell区間内で4差分を1回ずつ使い、座標、Seed、Rule IDのhashで24通りの順列を切り替える。Unity標準のPerlin選択に依存しない。
- ForestWall / Cliffの連続形状はRuleTileで作らず、Obstacle Visualを重ねて構成する。

Grass 8枚は`GroundVariationTile`へまとめ、Cell座標とSeedを32bit avalanche hashへ混合して決定的に選択する。短周期の剰余式、Shader差分、回転、反転を使わない。Detail、Props、Obstacle VisualはStatic SpriteとしてCatalogへ収録する。Detail 4種はTile Assetへ変換せず、TallGrass / FlowerPatch / Wildflowers / ReedsのDecoration Prefabへ割り当てる。

Sample道路はWaypoint列を先に定義し、Radius 1の正方形Brushで3 Cell幅へRasterizeする。全道路Cellが4近傍の単一Componentであることを検証してからDirt / Stone材質を割り当てる。接続形状と材質選択を分け、Dirt / Stone境界でも共通`road`グループによってCardinal Maskを継続する。

30x20 SampleのGrass選択は8差分を全て使用し、同一差分のCardinal隣接率12%、水平・垂直の最大連続4 Cell、offset 4 / 8 / 16の一致率はいずれも14%未満を実測済みとする。

## 表示と判定の分離

Unity Sceneでの移動Physicsの正本は、不可視の`CollisionTilemap`とObstacle Prefab直下の`CollisionBody`を合わせた`MapCollision` Layerとする。

- `Decorations`、`ObstacleVisuals`、`Props`はGrid Componentを持たないplain Transformとする。
- 森、崖、木、低木、岩、切株、倒木は`ObstacleVisuals`配下へ自由配置する。
- Tall Grass、Flower、Wildflowers、Reedsは`Decorations`配下へ自由配置する。
- Mushroom、Chest、Signは`Props`配下へ自由配置する。
- 表示RootはDefault Physics Layerとし、Collider2DとRigidbody2Dを持たない。
- 森、木、低木、岩、切株、倒木は、表示Root直下の非表示`CollisionBody`へ幹・接地点に寄せたCollider2Dを一つ持つ。CollisionBodyはMapCollision Layer、Rendererなし、Rigidbody2Dなし、Triggerなしとする。
- 崖とCell地形はCollisionTilemapを使い、Obstacle Prefab側へ重複Colliderを持たせない。
- Positionは1 / 32 Unity unit単位、Rotationは0、Scaleは1とする。
- `MapObstacleVisualMarker`は生成時の占有Footprintと、Tilemap Footprint / CollisionBodyの物理方式・形状を保持する制作契約。RuntimeはMarkerからColliderを生成せず、Sceneへ保存済みのMapCollisionをそのまま使う。

## 許可する生成処理

- 承認済みSource Rectの切り出し。
- 外周と連続するMagentaのAlpha化。
- Nearest Neighborによる規格化。
- 承認済みAI Surface差分の切り出しと規格化。
- 承認済みRoad Edge Sourceの輪郭抽出と、外側に焼き込まれた旧草地のAlpha化。
- 透明Padding。
- 正本とSourceの閉路・縦路・横路・全面素材Donorだけを使う、Cardinal-16道路形状の決定的合成。
- 決定的なAtlas Packing。
- Sprite名、サイズ、Alpha、Pivot、Family、出典の検査。

## 禁止する生成処理

- 承認済みDonorを組み合わせる道路形状補正を除く、Programによる新規の地形輪郭、岸、泡、浅瀬、崖面、樹冠、幹、根、陰影の作画。
- Programによる色替え、変形、回転、反転差分の作成。
- RuntimeでのPixel合成。
- Runtime Shaderによる地面差分の生成。
- 花、背高草、石などの独立した装飾をGrass Surfaceへ焼き込むこと。
- v3のForestWall / Cliff Spriteをv4.3へ名前だけ変えて混入すること。

## v4.3 QA契約

- Catalog schemaがv4.3で、Family内訳と合計205点が完全一致する。
- Catalogの全SpriteがAtlas内の有効なRectと正しいPivotを持つ。
- Import済みSub-Spriteの名前、個数、Rect、PivotがCatalogと一致する。
- 全Tile / RuleTile / Prefabの表示Spriteが同一Production Atlasを参照する。
- RuleTileがDirt / Stone / Waterの3点だけで、Dirt / Stoneの全16 Maskが各4 Spriteの座標hash差分 / Fixed、Waterは`ff`だけが4 SpriteのRandom / Fixedである。
- Dirt / Stoneの接続辺が20px固定Socketを持ち、両端6pxが直交bitのANDでのみ埋まる。`05` / `0a`に周期タブがなく、太道内角に芝穴がない。
- `RoadConnectionRuleTile`の差分選択が横4 Cellごとの4差分順列で、XY座標に対して決定的、かつZ座標、描画順、UnityのPerlin選択に依存しない。
- `GroundVariationTile`が装飾なしGrass 8枚を参照し、旧Grass 4 Tileを残さない。
- Dirt / Stoneが同じ`road`接続グループを持ち、材質境界で相互接続する。
- `ForestWall`、`Cliff`、`ObstacleTilemap`、`DetailTilemap`、Detail Palette、GameObject Brushを現行Asset契約へ含めない。
- Detail 4、Obstacle Visual 5、Prop系10の計19 Prefabが、Decorations / ObstacleVisuals / Propsへ分類される。
- Tile PaletteはGround / Collisionの2点、標準表示用Tile AssetはGrassVariationとStone Variantの計2点だけとする。
- SceneのGrid配下はGround / Terrain / Collision / Foregroundの4 Tilemapだけとする。
- 表示RootにCollider2D / Rigidbody2Dがなく、Default Physics Layerである。CollisionBody方式だけが規定の子Colliderを一つ持つ。
- 自由配置PrefabのPosition、Rotation、Scaleがv4.3契約を満たす。
- CollisionTilemapと規定のCollisionBodyだけが移動Physics判定を保持する。
- Design MasterとSource画像をUnityの表示Assetから直接参照しない。

上記v4.3契約はUnity Rebuild、Validator、**61 passed / 0 failed / 0 skipped**のEditMode Test、**4 passed / 0 failed / 0 skipped**のPlayMode Test、目視QAで検証済み。最新のUnity Sample QAは`Assets/Art/Generated/MapAuthoring/QA/gba-forest-sample-unity.png`。Production Atlas QA SHA-256は`34923AEC3E2721EA1D2491D18A8CE15C6AAF65E5FF2799001367080838B848F1`、Unity Sample QA SHA-256は`DD256066AC7DBD5C5A4BC798F750908A8C44D54A8DFE1483A01D9E3DE1E4275E`。

## v3 / v4 / v4.1履歴

v3は2026-08-02にUnity 6000.4.3f1で検証済みだった旧契約。191 Sprite、ForestWall / Cliff Blob-47、6 Tilemap、Props Grid、GameObject Brush、Prefab Colliderを使用していた。

ForestWallには共通Socket Crop `(x=20, y=16, width=110, height=110)` を適用し、向かい合うSocketのOpaque重なり12px以上、実測最小で東西13px・南北15pxを確認していた。この値とv3 Atlas / Catalog / QAのHash、18 passedという結果は履歴であり、v4.3の完了根拠にはしない。詳細は[[../Dev/Assets/GeneratedAssets|Generated Assets]]を参照する。

v4はschema v4 / 102 Spriteを保ちながら、Detail 4種をTileとDetail Paletteで扱い、SceneにDetailTilemapを持っていた。v4.1では同じ102 Spriteを維持しつつDetailをDecoration Prefabへ移し、旧Test数とHashは再利用しない。

v4.1はGrass 4、Dirt 16、Stone 16、Water 47を含む102 Spriteで、Grassを4つの標準Tileとして保存し、Dirt / Stone / Waterの全MaskをSingle / Fixed出力していた。v4.2では装飾なしGrass 8枚と全面Surface差分を追加し、115 Sprite、`GroundVariationTile`、全面MaskだけのRandom / Fixed出力へ置換した。v4.3でDirt / Stoneの全Cardinal Maskを4輪郭差分とし、透過路面端、20px固定Socket、AND条件の角埋め、4 Cell順列選択へ置換した。v4.1 / v4.2のTestとHashは履歴としてのみ保持する。
