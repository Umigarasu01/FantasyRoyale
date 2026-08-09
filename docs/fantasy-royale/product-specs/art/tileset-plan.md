# TilesetPlan

## 基本方針

- タイルサイズは32x32を基本にする。
- すべての地面タイルは単色禁止。草線、細かな粒、影、低コントラストの色ムラなど、面として連続して読めるディテールを含める。
- 花、背高草、明瞭な石などの独立した装飾をGrass Surfaceへ焼き込まず、自由配置Decorationと責務を分ける。
- 地形切り替えは必ず境界タイルを使う。
- 森、火山、雪は同じタイル役割を持ち、見た目だけ差し替えられる構造にする。
- 優先度は `High` が最初の仮素材化対象、`Medium` が密度改善対象、`Low` が後続の装飾拡張。

## 必須境界タイル

| 境界 | 用途 | 優先度 |
| --- | --- | --- |
| 草地 ↔ 土道 | 道を自然に草へ馴染ませる | High |
| 草地 ↔ 石畳 | 中央広場や小道の縁 | High |
| 草地 ↔ 水 | 池、水路、岸辺 | High |
| 草地 ↔ 森障害物 | 地面と自由配置の森表示を馴染ませる | High |
| 草地 ↔ 崖障害物 | 地面と自由配置の崖表示を馴染ませる | High |
| 道 ↔ 石畳 | 土道から広場への接続 | High |
| 水 ↔ 崖 | 崖付き水辺、池の高低差 | Medium |

## 森Production v4.3の接続契約

状態: **v4.3 Atlas・Unity Assetへ反映、検証済み**

v4.3で接続RuleTileとして扱うのはDirt、Stone、Waterだけ。森壁と崖はTileではなく、完成形のObstacle VisualをScene Viewで自由配置して構成する。TallGrass、FlowerPatch、Wildflowers、ReedsもDetail TileとしてPaintせず、Decoration Prefabとして自由配置する。

| Family | Topology | Sprite数 | 表現責務 |
| --- | --- | ---: | --- |
| Grass | Coordinate Hash Variation | 8 | 装飾を含まない低コントラストの連続草面 |
| Dirt | Cardinal-16 x 4輪郭差分 | 64 | 透過下地、草侵食、踏み跡、小石を含む土道 |
| Stone | Cardinal-16 x 4輪郭差分 | 64 | 透過下地、石畳の端、直線、曲がり、交差 |
| Water | Blob-47、`ff`のみ4差分 | 50 | 草岸、土岸、浅瀬、水面、内角・外角 |

- Cardinal-16はN/E/S/W、Blob-47は条件付き対角を含む8近傍で接続する。
- DirtとStoneは同じroad接続groupに属し、材質境界でも道の接続を閉じない。Mask判定は同一Tileかではなく、同じroad接続groupかで行う。
- Cardinal-16の接続辺は中央`[6,26)`の20pxを固定Socketとし、両端6pxは対象辺と直交辺のbitが両方開く場合だけ埋める。直線Mask `05` / `0a`は角を透過させ、太い道路の内角だけをAND条件で埋める。露出辺は4輪郭Sourceで不規則にし、路面外は透過にしてGroundの草地を見せる。
- `0f`は草縁やCell外周陰影を持たない全面路面とし、3Cell幅の内部へ草穴や暗い格子線を残さない。
- Grass 8枚は一つの`GroundVariationTile`へ設定し、Cell座標とSeedを強い2D hashへ混合して選択する。短周期の剰余式を使わない。
- Dirt / Stoneは全16 Maskそれぞれに4 Spriteを持ち、`RoadConnectionRuleTile`がXY座標、Seed、Rule IDのhashで決定的に選ぶ。
- Waterは全面Mask `ff`だけ4 SpriteをRandom出力し、他MaskはSingle出力する。
- Rule TransformとVariation TransformはFixedとし、Shader、回転、反転による差分を使わない。
- 光源を反転させないため、回転・反転で向き差分を代用しない。
- ForestWall / Cliff Blob-47は廃止し、v4.3 Atlasへ含めない。
- Unityが参照する表示素材は205点の単一Production Atlasへ集約する。

下記一覧はバイオーム共通の役割計画であり、v3で未収録の追加Blendや装飾は将来拡張として残す。

## 森・草原タイル

### 地面

| タイル | 内容 | 用途 | 優先度 |
| --- | --- | --- | --- |
| Grass_Surface_01〜08 | 装飾を含まない草線、粒、低コントラストの色ムラ | `GroundVariationTile`が座標Hashで選ぶ基本地面 | High |

Grass Surface 8枚による連続した地面はGroundTilemapに残す。地面そのものをPrefabへ分解せず、花、背高草、水草、明瞭な小石などの局所アクセントをDecorationsへ分離する。30x20 Sampleでは8差分を全て使用し、同一差分Cardinal隣接率12%、最大連続4 Cell、offset 4 / 8 / 16一致率14%未満を確認済み。

### 土道

| タイル | 内容 | 用途 | 優先度 |
| --- | --- | --- | --- |
| Dirt_Center | 土道中央。小石、踏み跡入り | 通路 | High |
| Dirt_Edge_N/S/E/W | 草に侵食された土道端 | 道境界 | High |
| Dirt_Corner_Inner | 内角 | 曲がり道 | High |
| Dirt_Corner_Outer | 外角 | 曲がり道 | High |
| Dirt_GrassInvasion | 草が入り込んだ道 | 自然な道の変化 | High |
| Dirt_Narrow | 細い土道 | サブ通路 | Medium |

### 石畳

| タイル | 内容 | 用途 | 優先度 |
| --- | --- | --- | --- |
| Stone_Center | 丸みある石畳 | 中央広場、小道 | High |
| Stone_Broken | 割れた石畳 | 古びた印象 | High |
| Stone_Grass | 草が生えた石畳 | 草地との接続 | High |
| Stone_Edge_8way | 石畳の端 | 広場境界 | High |
| Stone_DirtBlend | 土道との接続 | 道 ↔ 石畳 | High |

### 水辺

| タイル | 内容 | 用途 | 優先度 |
| --- | --- | --- | --- |
| Water_Center | 水面。波とハイライト入り | 池、水路 | High |
| Water_Dark | 深い水面 | 水面バリエーション | High |
| Water_Shallow | 浅瀬 | 岸辺近く | High |
| Water_GrassEdge_8way | 草岸 | 草地 ↔ 水 | High |
| Water_DirtEdge_8way | 土岸 | 道や崖近く | High |
| Water_Corner_Inner | 内角 | 池の曲線 | High |
| Water_Corner_Outer | 外角 | 池の曲線 | High |
| Water_CliffEdge | 崖付き水辺 | 水 ↔ 崖 | Medium |
| Water_BubbleDetail | 泡、波の装飾 | 水面密度 | Medium |

### 森壁・崖のObstacle Visual

| 表示モジュール | 内容 | 用途 | 優先度 |
| --- | --- | --- | --- |
| `obstacle_forest_mass_wide` | 横長の樹冠、幹、根元影を一体化 | 森の外周、長い区間 | High |
| `obstacle_forest_mass_deep` | 奥行きのある密な森塊 | 外周の厚み、角の充填 | High |
| `obstacle_forest_front_strip` | 手前側の樹冠・幹・根元影 | 森壁の前縁と継ぎ目隠し | High |
| `obstacle_cliff_straight_wide` | 草付き上面、岩面、下端影を一体化 | 長い直線崖 | High |
| `obstacle_cliff_outer_corner` | 外角の上面、岩面、下端影 | 崖の折れと端部 | High |

これらはTile Paletteへ入れず、Prefabとして`ObstacleVisuals`へ自由配置する。Positionは1 / 32 unitへSnapし、Rotation 0、Scale 1を守る。通行判定は見た目から生成せず、CollisionTilemapへ別途Paintする。

### 通行可能なDecoration Prefab

| Prefab | 作画契約 | 配置先 | 優先度 |
| --- | --- | --- | --- |
| TallGrass | 背景芝を含まない透過草むら | Decorations | High |
| FlowerPatch | 花と葉だけの透過まとまり | Decorations | High |
| Wildflowers | 地面の正方形を含まない透過野花 | Decorations | High |
| Reeds | 足元と細い茎が読める透過水草 | Decorations | High |

- 4素材は足元中央Pivotを持つSpriteRenderer Prefabとする。
- `MapRoot/Decorations`はGrid Componentを持たないplain Transformとする。
- Positionは1 / 32 unitへSnapするが、1Cell単位には拘束しない。Rotation 0、Scale 1を守る。
- Collider2D、Rigidbody2D、`MapObstacleVisualMarker`を持たない。
- 旧Detail Tile AssetとDetail Paletteを現行制作手順に使用しない。

## 火山タイル

森・草原と同じ役割を維持して差し替える。

| 森・草原の役割 | 火山差し替え | ディテール | 優先度 |
| --- | --- | --- | --- |
| Grass_Base | Ash_Base | 火山灰、黒い粒、灰色ムラ | High |
| Grass_Light | Ash_GlowCrack | 赤熱した細い亀裂 | High |
| Grass_Dark | Ash_Dark | 黒い火山灰、影 | High |
| Grass Surface差分 | Ash_Surface差分 | 装飾を含まない灰粒、低コントラストの濃淡 | High |
| FlowerPatch | Ash_Crystal | 赤い結晶、火花の自由配置装飾 | Medium |
| Dirt_Center | BurntPath_Center | 焦げた土道、黒い石 | High |
| Stone_Center | BasaltStone_Center | ひび割れた黒石 | High |
| Water_Center | Lava_Center | 溶岩流、明るい筋 | High |
| Water_Edge | Lava_RockEdge_8way | 黒岩の溶岩岸 | High |
| 森障害物モジュール | BurntTreeWall | 枯れ木、炭化した幹 | High |
| 崖障害物モジュール | VolcanoCliff | 黒岩、赤い亀裂、影 | High |

## 雪タイル

森・草原と同じ役割を維持して差し替える。

| 森・草原の役割 | 雪差し替え | ディテール | 優先度 |
| --- | --- | --- | --- |
| Grass_Base | Snow_Base | 雪粒、薄い青影 | High |
| Grass_Light | Snow_Packed | 踏み荒らされた雪 | High |
| Grass_Dark | Snow_Shadow | 青みの影雪 | High |
| Grass Surface差分 | Snow_Surface差分 | 装飾を含まない雪粒、薄青の濃淡 | High |
| FlowerPatch | Snow_Crystal | 小さな氷結晶の自由配置装飾 | Medium |
| Dirt_Center | PackedSnow_Path | 足跡、圧雪 | High |
| Stone_Center | SnowStone_Center | 雪に埋もれた石畳 | High |
| Water_Center | Ice_Center | 薄氷、白いハイライト | High |
| Water_Edge | Ice_SnowEdge_8way | 雪岸、薄氷境界 | High |
| 森障害物モジュール | SnowPineWall | 雪をかぶった針葉樹壁 | High |
| 崖障害物モジュール | IceCliff | 凍った崖、氷壁 | High |

## 旧方式の履歴

- v3はForestWall / Cliffを各47 SpriteのBlob-47 RuleTileとして扱っていた。
- v4はTallGrass、FlowerPatch、Wildflowers、ReedsをDetail TileとしてDetailTilemapへPaintしていた。
- v4.1では森・崖に続いて通行可能装飾も自由配置へ移し、Grass 4枚と全Mask Single / Fixedの102 Sprite Atlasを使用していた。
- v4.2では自由配置方針を継続し、装飾なしGrass Surface 8枚、Dirt / Stone / Waterの全面Surface差分を追加した115 Sprite Atlasへ置換した。
- v4.3ではDirt / Stoneの全16 Maskを4輪郭差分にし、透過路面外、中央20px Socketと条件付き角埋め、座標hash選択を持つ205 Sprite Atlasへ置換した。旧方式は現行制作方法として使わない。
