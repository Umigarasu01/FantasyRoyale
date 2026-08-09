# MapPlan

## 基本方針

サンプルマップは、同じレイアウト骨格を森・草原、火山、雪で差し替える。
まずは森・草原マップを完成基準にし、火山と雪は同じ通路、広場、水辺、障害物の位置関係を維持してバイオーム変換する。

四角いエリア塗り分けは禁止。通路、池、森壁、崖は自然な曲線状または段差状の輪郭にする。

想定サイズ:
- 初期検証: 64x48
- バトロワ探索検証: 96x72

## 森・草原マップ構成

### 全体構造

- 中央に石畳混じりの小道と小広場を置く。
- 左右に草地、草むら、低木、岩を密に配置する。
- 右側に池または細い水路を置く。
- 下側は森の壁で縁取り、プレイヤーを自然に上方向へ誘導する。
- 小さな戦闘広場を2〜3個作る。
- 木、水辺、崖、低木で自然にルートを制限する。
- マップ全体を四角く区切らず、道と障害物を曲線状に配置する。

### エリア

| エリア | 役割 | 配置方針 |
| --- | --- | --- |
| 中央石畳小道 | 主導線、戦闘広場、宝箱候補 | 石畳と土道を混ぜ、端を草で侵食する |
| 南の森壁 | 外周制限、開始地点の誘導 | 森のObstacle Visualを重ね、機械的でない連続壁を作る |
| 西の草むら回廊 | 探索感、待ち伏せ、サブ導線 | 背の高い草と低木で細い曲がり道を作る |
| 東の池/水路 | ランドマーク、危険境界 | 水面、浅瀬、草岸、土岸、葦、岩を組み合わせる |
| 北の木陰広場 | 小戦闘、祭壇候補 | 周囲に木と倒木、中央は歩ける余白を残す |
| 中央南の小広場 | 商人/看板候補 | 道の分岐点。完全な空白にせず小石や花を置く |
| 東水辺広場 | 宝箱候補、視覚的目的地 | 池の岸に宝箱、岩、葦を配置する |

### 96x72 Battle Royale Reference Map

`Assets/Scenes/MapAuthoring/GbaForestBattleRoyaleReference.unity`を、上記構成を実装した最初の完成基準Sceneとする。

- 範囲は`x=-48..47`、`y=-36..35`の96x72 Cell。
- 参加者開始地点はPlayer 1 + CPU 5の計6点を外周へ分散し、開始地点同士を18 Cell以上離す。
- 中央石畳、北木陰、東水辺、南広場をStoneの小戦闘広場とし、西草地、北東見晴らし、南東崖下を副Landmarkにする。
- 道路は短い水平・垂直区間を交互につなぎ、長い矩形Loopを作らない。全道路はRadius 1の3 Cell幅かつ単一4近傍Componentとする。
- 東の池は352 Water Cellの不規則なRow Spanと小島で作り、道路と重ねない。
- Obstacle Visual 232 Instance、Decoration 115 Instance、Props 16 Instanceを既存Prefabから配置する。大型森林Stampは等間隔に並べず、単木・低木・岩・倒木を混在させる。
- Socketは6 Start、7 Landmark、16 Enemy、22 Loot、3 Merchant、6 Eventの計60点。全Start、Landmark、MerchantはCollisionを除く4近傍経路で到達可能にする。

このSceneは約20分の試合を目指す空間基準であり、実時間は移動速度、Camera、エリア収縮を接続したPlayModeで別途計測する。プレイ結果が良好なら、道路Graph、水域Row Span、POI保護領域、森林密度、Socket制約を自動生成規則へ抽出する。

### 通路

- 主道は直線ではなく、中心経路からRadius 1で膨張した3Cell幅を基本として緩く曲げる。
- 土道の露出端は透過輪郭からGroundのGrassを見せ、草束や花は自由配置Decorationでなじませる。
- 石畳は中央広場と一部の分岐に限定し、割れ石や草付き石で古びた雰囲気を出す。
- サブ通路は草地の濃淡、低木、倒木の切れ目で自然に読ませる。

道路は次の順序で生成する。

1. 入口、広場、目的地を結ぶ中心経路を作る。
2. 中心経路をRadius 1で膨張し、基本幅3Cellの道路領域にする。
3. 曲がり角と分岐で膨張領域を統合し、内側の穴や1Cellの切れ目を埋める。
4. 入口、分岐、広場が同じ4近傍成分に属するか連結検証する。
5. 連結済み道路領域へ最後にDirt / Stone材質を割り当てる。

DirtとStoneは同じroad接続groupとしてMaskを判定する。材質境界で互いをNotThis扱いせず、草付き終端を描いて道路を閉じない。材質の切替と道路形状の連結を別責務として扱う。

Cardinal-16の接続辺は中央`[6,26)`の20pxを固定Socketとし、両端6pxは対象辺と直交辺のbitが両方開く場合だけ埋める。直線Mask `05` / `0a`には角タブを付けず、太い道路の内角だけをAND条件で埋めて芝穴を防ぐ。露出辺は4輪郭差分で不規則にし、路面外を透過にしてGroundTilemapの草地を見せる。`0f`は草縁やCell外周陰影を持たない全面路面とする。

### v4.3制作Assetの使い分け

- Groundは装飾なしGrass Surface 8枚を参照する単一`GroundVariationTile`で塗る。Cell座標とSeedの強い2D hashで差分を決定し、短周期の剰余式を使わない。
- DirtとStoneは同じroad接続groupを持つCardinal-16 RuleTileとして、連結済み道路領域へ材質を割り当てる。
- Stoneは中央広場と分岐へ限定するが、Dirtとの境界でも道路接続を維持する。
- WaterはBlob-47 RuleTileで斜め岸、内角、外角、入り江を作る。大きな長方形の池だけで済ませない。
- 森壁は`obstacle_forest_mass_wide`、`obstacle_forest_mass_deep`、`obstacle_forest_front_strip`をObstacleVisualsへ重ねて作る。
- 崖は`obstacle_cliff_straight_wide`と`obstacle_cliff_outer_corner`をObstacleVisualsへ組み合わせ、上面・岩面・下端影が連続して見える段差を作る。
- 個別木、低木、岩、倒木、切株もObstacleVisualsへ自由配置する。Mushroom、Chest、SignはPropsへ配置する。
- TallGrass、FlowerPatch、Wildflowers、Reedsは透過・足元中央PivotのPrefabとしてplain Decorationsへ配置する。Ground grassはGroundTilemapへ残す。
- ObstacleVisuals、Props、TallGrass、ReedsとPlayerは`WorldObjects` / Order 0 / 足元Pivotへ揃え、world Yが低いものほど手前に描画する。FlowerPatch / Wildflowersは平面装飾として`MapDetail`固定背面にする。
- Ground / Terrainは`MapGround`固定背面、Foregroundは`MapForeground`固定前面とし、足元Yソートの対象にしない。URP 2DはRenderer2D DataのCustom Axis / `Vector3.up`を使う。
- 自由配置PrefabはPositionを1 / 32 unitへSnapし、Rotation 0、Scale 1とする。
- 表示RootはCollider2D / Rigidbody2Dを持たない。水・外周・崖はCollisionTilemapへPaintし、森・木・低木・岩・切株・倒木はPrefab直下の非表示CollisionBodyで幹・接地点だけを塞ぐ。
- 自動配置用Footprintは道路や他素材との間隔を守る広めの論理範囲とし、実際のPhysics Colliderとは分ける。
- Dirt / Stoneは全16 Cardinal Maskそれぞれに4 Spriteを持ち、`RoadConnectionRuleTile`がXY座標、Seed、Rule IDのhashで決定的に差分を選ぶ。
- Waterは全面Mask `ff`だけ4 SpriteをRandom / Fixed出力し、他MaskはSingle / Fixedとする。
- Shader、回転、反転による地面差分は使わない。Road Edge差分は接続Topologyを変えず、露出辺の凹凸だけを変える。
- 見た目の周期感はGrassの座標Hash、Road Edgeの座標Hash、全面Surface差分、道幅、曲がり、Obstacle Visualの重なり、Props、Decorationsの自由配置で崩す。

### 密度

- 歩行可能地面のSurfaceには草線、粒、低コントラストの色ムラを広く持たせ、花、小石、背高草などの明瞭なアクセントは自由配置Decorationで散らす。
- 広場中央は戦闘のために空けるが、端には小物を置く。
- 画面端には森壁、低木、岩を多く置き、空白を作らない。
- 水辺は葦、泡、浅瀬、岩で密度を上げる。

### 通行可否

| 要素 | 通行 | 理由 |
| --- | --- | --- |
| 草地、花、小石 | 可 | 地面ディテール |
| 背の高い草 | 可 | 探索密度、視覚変化 |
| 低木、木、森壁 | 不可 | ルート制限 |
| 岩、倒木、切り株 | 不可または一部不可 | 通路縁取り |
| 水、深い池 | 不可 | 危険/境界 |
| 浅瀬、岸装飾 | 原則不可または装飾のみ | 水辺の判読性 |

## 火山マップ派生

森・草原と同じレイアウトを使い、素材と色を差し替える。

| 森・草原要素 | 火山変換 |
| --- | --- |
| 草地 | 火山灰、黒土、灰粒 |
| 明るい草 | 赤熱した地割れ、熱い石 |
| 暗い草 | 黒い灰だまり |
| 土道 | 焦げた土道 |
| 石畳 | ひび割れた黒石 |
| 池/水路 | 溶岩池、溶岩水路 |
| 水辺の葦 | 熱気孔、赤い結晶 |
| 木/森壁 | 焦げ木、枯れ木壁 |
| 低木 | 火山岩、黒い低岩 |
| 花 | 火花、赤い結晶粒 |
| 宝箱周辺 | 焦げた遺跡風 |

火山でも通れる場所は溶岩より暗く落ち着いた灰色にし、危険地形は高彩度の橙、黄色、赤で目立たせる。
黒一色、赤一色の広い面は禁止。

## 雪マップ派生

森・草原と同じレイアウトを使い、素材と色を差し替える。

| 森・草原要素 | 雪変換 |
| --- | --- |
| 草地 | 雪原、雪粒 |
| 明るい草 | 踏み荒らされた雪 |
| 暗い草 | 青みのある影雪 |
| 土道 | 圧雪された道、足跡 |
| 石畳 | 雪に埋もれた石畳 |
| 池/水路 | 凍った池、薄氷 |
| 水辺の葦 | 凍った葦、氷粒 |
| 木/森壁 | 雪をかぶった針葉樹壁 |
| 低木 | 雪だまり、凍った低木 |
| 花 | 小さな氷結晶 |
| 崖 | 凍った崖、氷壁 |

雪でも白一色の広い面は禁止。
薄青の影、足跡、氷のハイライト、小さな雪粒を散らす。

## サンプルマップ作成手順

1. GroundTilemapへ`GroundVariationTile`で連続面の草地を描き、装飾なしGrass 8差分が座標Hashで表示されることを確認する。
2. 入口、広場、目的地を結ぶ道路中心経路を作る。
3. 中心経路をRadius 1へ膨張し、曲がりと分岐を統合して4近傍の連結を確認する。
4. 連結済み道路領域へDirt / Stone材質を割り当て、同じroad接続groupとして境界が閉じないことを確認する。
5. Terrain / Foreground Tilemapへ水辺と必要な地形表現を描く。
6. CollisionTilemapへ水、外周、崖の移動不可Cellを明示的にPaintする。森、木、低木、岩、切株、倒木はPrefab内CollisionBodyを使い、重複TileをPaintしない。
7. 森と崖のObstacle Visualを重ね、外周とルートを自然に見せる。
8. Tree 3差分、低木、岩、倒木、切り株をObstacleVisualsへ自由配置して通路を縁取る。
9. TallGrass、FlowerPatch、Wildflowers、ReedsをDecorationsへ1 / 32 unitで自由配置する。Mushroom、Chest、SignはPropsへ置く。
10. 自由配置PrefabのRotation 0、Scale 1、物理Componentなしを確認する。
11. PlayerStart / Enemy / Loot Socketを配置する。
12. Validatorで表示とCollisionの分離、旧Detail / Obstacle Tilemapの不在、Atlas参照、Hierarchyを確認する。

## 品質チェック

- 一目でトップダウン2DアクションRPG風に見えるか。
- 地面が単色面になっていないか。
- Grass 8差分を全て使用し、同一差分のCardinal隣接率20%未満、水平・垂直の最大連続4 Cell以下、offset 4 / 8 / 16の一致率30%未満を満たすか。30x20 Sampleの実測はそれぞれ12%、4 Cell、14%未満。
- 道が巨大な長方形になっていないか。
- 主道が基本3Cell幅で曲がり、曲がり角と分岐に穴や1Cellの切れ目がないか。
- 入口、分岐、広場が同じ4近傍の道路成分に属するか。
- Dirt / Stone境界で草付き終端が向き合わず、一続きのroad接続groupとして見えるか。
- 池に斜め岸、内角、外角、浅瀬があり、水面だけの四角い塗りになっていないか。
- 森壁で樹冠、幹、根元影が連続し、同じ単木や同じモジュールだけを並べた見た目になっていないか。
- 崖で上面、岩面、下端影が連続し、直線と外角モジュールの継ぎ目が見えないか。
- Tree 3差分が混在し、同じPrefabの複数Instanceが正しく保存されているか。
- 歩行可能エリアと障害物が判別しやすいか。
- 表示RootとDecoration / PropsにCollider2D / Rigidbody2Dがなく、CollisionTilemapと規定のCollisionBody以外が移動Physicsを持たないか。
- 木の幹中央では衝突し、樹冠側では同じColliderに阻まれないか。
- 見た目とCollisionに意図しない隙間や過剰な塞ぎがないか。
- 自由配置Prefabが1 / 32 unit Snap、Rotation 0、Scale 1を守るか。
- Decorationsが地面の正方形を含まない透過素材で、Cell中央へ規則的に並んでいないか。
- Playerが木、崖、Props、TallGrass、Reedsの上側では背面、下側では前面に切り替わるか。
- FlowerPatch / Wildflowersが平面装飾としてPlayerの背面に留まり、Foregroundは常に前面に留まるか。
- 画面のどこを切り取っても草、道、水、木、装飾が密に存在するか。
- 森、火山、雪が同じゲーム内の別バイオームに見えるか。
- Tile、RuleTile、Prefabが単一Production Atlas以外の表示素材を参照していないか。
- Dirt / Stoneの全16 Cardinal Maskが各4 Spriteの決定的差分を持ち、Waterは`ff`だけ4 Sprite Random / Fixed、他MaskがSingle / Fixedか。
- Shader、回転、反転による地面差分を使っていないか。

## 旧方式の履歴

- v3ではForestWall / Cliff Blob-47をObstacleTilemapへPaintし、木などをProps GridへGameObject Brushで配置していた。Prefab Colliderも判定へ使っていた。
- v4では森・崖・障害物を自由配置へ移したが、通行可能装飾はDetailTilemap、Sample道路は手書きの1Cell列と材質ごとの接続判定だった。
- v4.1ではDetailTilemapを現行手順から外してDecorationsへ移し、道路を中心経路、3Cell幅化、統合、連結検証、材質割当の順で生成した。Grass 4 Tileと全Mask Single / Fixedの102 Sprite Atlasを使用していた。
- v4.2では道路生成と自由配置方式を継続し、装飾なしGrass 8差分の`GroundVariationTile`と全面MaskだけのRandom / Fixedを持つ115 Sprite Atlasへ置換する。旧方式は現行制作方法として使わない。
