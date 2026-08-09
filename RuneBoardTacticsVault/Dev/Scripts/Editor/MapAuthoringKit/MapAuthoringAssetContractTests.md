# MapAuthoringAssetContractTests

状態: **v4.3 + Event Step 2 Test実装 / Unity EditMode検証済み**

## 役割

Map Authoring Kit v4.3の単一Atlas、GroundVariationTile、道路RuleTileと道端差分、Decoration Prefab、Scene Sample、Collision分離、道路連結が保守用再生成で失われないことを確認するEditMode Test。

- Script: `Assets/Scripts/MapAuthoringKit/Tests/Editor/MapAuthoringAssetContractTests.cs`
- Test method: 21件。
- `MapAuthoringValidatorTests`と合わせたMap Authoring EditMode Suite: **63 passed / 0 failed / 0 skipped**。

## v4.3確認内容

### Production Atlas

- Catalog schemaが`fantasyroyale.map-authoring-atlas.v4.3`。
- Family内訳がGrass 8、Dirt 64、Stone 64、Water 50、Detail 4、Props 10、Obstacle Visual 5の計205 Sprite。
- Catalog名とImport済みSub-Spriteが完全一致する。
- Production Folder内の表示Textureが単一Atlasだけ。
- ForestWall / Cliff Familyと旧Loose PNGがない。
- Atlas build入力はGrass、Dirt、Stone、Waterの4 Surface Source SheetとDirt / Stoneの2 Road Edge Profile Sheet。Testはその生成結果である205 named SpriteとCatalogのSource出典を契約として扱う。

### GroundVariationTile

- `Ground/GrassVariation.asset`が`GroundVariationTile`で、`ground_grass_01`〜`08`を順に参照する。
- 8 Spriteがすべて単一Production Atlas由来。
- 旧`Grass01.asset`〜`Grass04.asset`が存在しない。
- Sample Groundの600 Cellがすべて同じ`GrassVariation.asset`を保存し、保存Tileの種類が一つだけ。
- `GetVariantIndex`が全8差分を使用する。
- 同一差分のCardinal隣接率が0.2未満、縦横の最大runが4 Cell以下。
- offset 4 / 8 / 16 Cellの一致率がそれぞれ0.3未満。
- 単一差分だけで埋まる行または列がない。

### RuleTile

- Dirt / Stoneは同じ`road`接続groupを持つ`RoadConnectionRuleTile`のCardinal-16、WaterはBlob-47。
- DirtからStone、StoneからDirtの双方を同一道路として接続できる。
- RuleTileはこの3点だけ。
- Dirt / Stoneは全16 Maskが4 SpriteのRandom出力、Waterは全面Mask `ff`だけ4 SpriteのRandom出力。
- 上記3 Ruleの基準Sprite名は従来名を維持し、追加差分は`_v02`〜`_v04`。Rule TransformとRandom TransformはFixed。
- Waterの他46 Maskは一枚のSingle出力、TransformはFixed。
- ForestWall / Cliff RuleTileがない。
- 全表示Spriteが単一Production Atlasを参照する。

### Road Variation Hash

- Runtime契約では、接続Maskを基底`RuleTile`で選び、4 Sprite内の実表示差分を`RoadConnectionRuleTile.GetTileData`が座標、Rule ID、Seedから決定する。
- Runtimeは横4 Cellを一つの区間とし、24順列から選んだ並びで4差分を各1回使う。負のX座標も床除算補正し、区間境界を含む同一差分の横連続を2 Cell以下にする。
- Testは全16 MaskについてX=-32〜31の横64 Cellを走査し、各4 Cell区間で4差分が一度ずつ使われ、全体では各差分が16回、同一差分の最大runが2 Cell以下になることを検査する。
- 同じXYとRule IDではZ座標を変えても同じIndexを返すことを検査する。
- これにより標準RuleTileのPerlin選択へ退行せず、短い道路で特定輪郭へ偏る回帰を検出する。

### Road Atlas Socket

- `RoadAtlasSprites_UseTwentyPixelTopologyBoundedSockets`はAssetDatabase経由の表示結果ではなく、Git管理されたProduction Atlas PNGを`ImageConversion.LoadImage`で直接読む。
- Dirt 64枚とStone 64枚の全128 Spriteについて、4辺それぞれのdepth 0 / 1、各32px座標を実Alpha Pixelで検査する。
- 接続bitが開く辺は中央`[6, 26)`の20pxだけを基本Socketとし、接続bitが閉じる辺は透明とする。
- 各辺の角6pxは、その辺と対応する直交辺のbitが両方開く場合だけ不透明とする。曲がり・分岐・全面Maskは必要な角を埋めるが、直線Mask `05` / `0a`は角を埋めない。
- これにより、Atlas build後の実PNGへ全幅32px接続や直線端の角タブが再混入する回帰を検出する。

### Prefab分類

- 表示Prefabは5 Obstacle Visual、既存Props 10点、Decoration 4点の計19点。
- ObstacleVisuals向けは、5 Obstacle Visual、Tree 3差分、BlockingBush、MossyRock、Stump、FallenLogの12点。
- Decorations向けはTallGrass、FlowerPatch、Wildflowers、Reedsの4点。
- Props向けはMushroomPatch、Chest、Signpostの3点。
- 全Prefabの表示RootがDefault Physics Layerで、Collider2D / Rigidbody2Dを持たない。
- Obstacle 12点のうち森3種、Tree 3種、BlockingBush、MossyRock、Stump、FallenLogの10点が規定のCollisionBodyを持ち、崖2種がTilemapFootprint方式で子Colliderを持たない。
- CollisionBodyはMapCollision Layer、Renderer / Rigidbody2D / Triggerなし、Collider一つで、素材ごとの幹・接地点Size / Offset / Rotation / Capsule Directionと一致する。
- Decoration PrefabがGrid、Tilemap、`MapObstacleVisualMarker`を持たず、Detail familyの足元中央Pivot Spriteを参照する。
- SpriteRendererが単一Production Atlasを参照する。
- GameObject Brush、Detail Tile Folder、Detail Paletteをv4.3出力へ含めない。
- Ground PaletteがGroundVariationTile、Dirt、Stone、Water、StoneVariantを収録し、Collision Paletteと分離される。
- Obstacle 12点、Props 3点、TallGrass、ReedsがWorldObjects / Order 0 / Pivotを使う。
- FlowerPatchとWildflowersが平面装飾としてMapDetail / Order 0 / Pivotを使う。

### Renderer2D描画順

- `Assets/Settings/Renderer2D.asset`がTransparency Sort Mode Custom Axis、軸`Vector3.up`を保存する。
- 同じWorldObjects / Order 0内ではSprite Pivotのworld Yが低いものほど手前になる契約を維持する。

### Event Asset / Reference Scene

- `HealingFountainBasic.asset`が`HealingFountainEventDefinition`で、ID、OneShot、標準半径、回復量30を持つ。
- `MapEventCatalog`の保存Listから`healing-fountain-basic`を検索できる。
- Reference SceneのEventSocketが6点あり、配置固有Socket IDを重複なく持つ。
- 6点がすべて`PoolCandidate`であり、固定Event定義参照を持たず、正の個別接近半径を持つ。

### Road Rasterizer

- 水平・垂直Segmentの始点と終点を含む。
- Radius 1の正方形Brushが中心経路全体を3Cell幅へ膨張する。
- L字の内角を埋め、斜めの穴を残さない。
- 交点を共有する複数経路を一つの分岐道路へ統合する。
- 斜めSegmentを暗黙補間せず入力違反として拒否する。
- 空集合や分断集合を単一4近傍成分として受理しない。

### Scene Sample

- Grid配下のTilemapがGround、Terrain、Collision、Foregroundの4点。
- Ground全600 Cellが単一GroundVariationTileを保存し、Sprite差分はTile Assetの座標Hashで決まる。
- DetailTilemap / ObstacleTilemapがなく、Decorations / ObstacleVisuals / Propsがplain Transform。
- Decorationsに4種のPrefabが含まれ、少なくとも一つが1 / 32 unit単位の非Cell位置へ配置される。
- CollisionTilemapが不可視CollisionTileと必要なCollider Componentを持つ。
- 自由配置PrefabがPosition 1 / 32 unit、Rotation 0、Scale 1を守る。
- TilemapFootprint方式の崖だけがMarker範囲をCollisionTilemapへ明示配置する。
- CollisionBody方式の障害物は広い論理FootprintをTilemapへ焼かず、Prefabと一緒に動く幹・接地点Colliderを使う。
- Tree 3差分は別Prefabで、それぞれ幹幅に合わせた水平Capsuleを持つ。
- Dirt / Stoneを合わせた全道路Cellが単一の4近傍成分を作る。
- Dirt / Stoneの異材質Cellが実際に隣接し、材質境界でも道路網が途切れない。

## Test隔離

Sample確認は対象SceneをAdditiveで読み、既に開かれていなかった場合だけ終了時に閉じる。SceneやProduction Assetは変更・保存しない。Validator単体の配置確認は保存対象外のPreview Sceneを使う。

## 旧方式の履歴

- v3 Testは191 Sprite、5 RuleTile、ObstacleTilemap、Props Grid、10 GameObject Brushを期待し、当時18 passedだった。
- v4 Testは5 Tilemap、3 Palette、15表示Prefabを期待し、道路Rasterizerと異材質接続を検証していなかった。
- v4.1 Testではschema v4.1 / 102 Sprite、Grass標準Tile 4点、各Mask 1 Sprite、4 Tilemap、2 Palette、19表示Prefab、13 test methods、道路形状と材質境界の連結契約を検査していた。
- v4.2 Testではschema v4.2 / 115 Sprite、GroundVariationTile、全面Mask差分、Sample地面の反復率検査を追加し15 test methodsへ更新した。v4.1以前の値は履歴としてのみ保持する。
- v4.3 Testではschema v4.3 / 205 Sprite、Dirt / Stone全Maskの4差分、座標Hashの分布と短周期run検査を追加した。さらにchecked-in PNGの128道路Spriteを実Pixel走査する20px Socket検査を加えて17 test methodsとなり、その後Renderer2D設定と表示Prefabの描画順契約を加えて19 test methodsへ更新した。Runtime側は4 Cell順列と負座標補正を採用する。v4.2以前の値は履歴としてのみ保持する。
