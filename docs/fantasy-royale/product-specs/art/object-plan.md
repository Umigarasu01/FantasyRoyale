# ObjectPlan

## 基本方針

- オブジェクトは透過PNG想定。
- 単純な円、四角、アイコン風の記号は禁止。
- 各オブジェクトは輪郭、暗部、中間色、明部、接地影を持つ。
- 通行不能オブジェクトは足元や影で障害物だと分かるようにする。
- 通行可能装飾は小さく、移動ルートを誤認させない形にする。
- 森、火山、雪で同じ役割のオブジェクトを用意し、バイオーム差し替えできるようにする。

## 共通カテゴリ

| カテゴリ | 用途 | v4.3配置・判定方針 | 優先度 |
| --- | --- | --- | --- |
| 大型障害物 | 外周、区画分け、森壁 | ObstacleVisualsへ自由配置。森は前縁CollisionBody、崖はCollisionTilemap | High |
| 中型障害物 | 通路縁取り、戦闘広場の外周 | ObstacleVisualsへ自由配置。幹・根元・接地点CollisionBody | High |
| 小型障害物 | ルート制限、小さな遮蔽 | ObstacleVisualsへ自由配置。接地点CollisionBody | Medium |
| 通行可能装飾 | 密度、雰囲気 | Decorationsへ1px単位で自由配置。Collisionなし | High |
| インタラクト候補 | 宝箱、看板、商人、祭壇 | Propsへ表示だけを配置。Interactionは別設計 | Medium |
| 危険装飾 | 溶岩、薄氷、毒など | 表示PrefabはTriggerなし。判定は別のTile / Gameplay実装 | Low |

## 森Production v4.3のPrefab契約

状態: **v4.3 Prefab・Sceneへ反映、描画順を含め検証済み**

v4.3 Production Atlas 205 Spriteのうち、5 Obstacle Visual、Props系10点、Decoration Sprite 4点から、計19の表示Prefabを作る。Dirt / Stoneの道路端差分増加によってPrefab数と分類は変更しない。

### ObstacleVisuals配下: 12点

- `obstacle_forest_mass_wide`
- `obstacle_forest_mass_deep`
- `obstacle_forest_front_strip`
- `obstacle_cliff_straight_wide`
- `obstacle_cliff_outer_corner`
- TreeMedium01 / TreeMedium02 / TreeMedium03
- BlockingBush / MossyRock / Stump / FallenLog

### Props配下: 3点

- MushroomPatch
- Chest
- Signpost

### Decorations配下: 4点

- TallGrass
- FlowerPatch
- Wildflowers
- Reeds

全表示PrefabのRootはDefault Physics Layer、Collider2Dなし、Rigidbody2Dなしとする。Positionは1 / 32 unitへSnapし、Rotation 0、Scale 1で自由配置する。Decorations / ObstacleVisuals / PropsはGridを持たないplain Transformとし、GameObject Brushは使わない。

奥行きを持つObstacleVisuals、Props、TallGrass、Reedsは`WorldObjects` / Order 0 / 足元Pivotへ揃え、world Yが低いものほど手前に描画する。FlowerPatchとWildflowersは背丈のない平面装飾なので`MapDetail` / Order 0へ固定し、立体表示との前後入れ替えを行わない。Playerも同じ`WorldObjects`契約へ参加する。

Decoration Prefabは背景芝を含まない透過素材とし、足元中央Pivotを使う。地面の正方形をSpriteへ含めず、Cellの中央や境界に拘束されない重ね方を可能にする。Ground grassは装飾なしAI Surface 8枚を参照する`GroundVariationTile`としてTilemapへ残し、花、背高草、明瞭な石などをGrass Surfaceへ焼き込まない。

必要なObstacle Visual Prefabだけ`MapObstacleVisualMarker`を持てる。DecorationsへMarkerを付けない。Markerは自動配置・間隔確保に使う広めのFootprintと、物理判定方式・足元形状を別々に保持する。RuntimeはMarkerから判定を復元せず、Scene保存済みのMapCollisionを使う。

森3種、Tree 3種、BlockingBush、MossyRock、Stump、FallenLogは、Prefab直下の`CollisionBody`へBoxまたはCapsuleを一つ持つ。判定は樹冠や表示矩形全体へ広げず、幹、根元、岩の接地点、倒木の接地軸へ寄せる。崖2種だけはTilemap Footprint方式を維持する。

AI生成素材は初期素材として扱い、正式素材へ差し替える余地を残す。差し替え時もSprite名、足元Pivot、左上光源、Prefab分類を維持する。

## 森・草原オブジェクト

| オブジェクト | 見た目 | 用途 | 優先度 |
| --- | --- | --- | --- |
| BroadleafTree | 複数の葉房、濃い影、幹、根元を持つ通常木 | 大型障害物 | High |
| BroadleafTree_Large | 2x2以上の大きな樹冠。森壁にも混ぜる | 外周、ランドマーク | High |
| ForestMass_Wide | 樹冠、幹、根元影を一体化した横長モジュール | 通行不能な森壁 | High |
| ForestMass_Deep | 奥行きのある密な森塊 | 外周、区画分け | High |
| ForestFront_Strip | 手前側の森縁と足元影 | 継ぎ目隠し、前縁 | High |
| Cliff_StraightWide | 草付き上面、岩面、下端影 | 直線崖 | High |
| Cliff_OuterCorner | 外角の上面、岩面、下端影 | 崖の折れ | High |
| LowBush_Blocking | 横に広い低木。葉の房と影 | 通路縁取り | High |
| Bush_Round | 丸く密な茂み。単純な円ではなく葉房で構成 | 中型障害物 | High |
| TallGrass_Walkable | 細い草線が重なった草むら | 通行可能装飾 | High |
| Mushroom_Red | 傘、柄、斑点、影を持つ赤系キノコ | 通行可能装飾 | High |
| Mushroom_Brown | 落ち着いた茶系キノコ | 通行可能装飾 | Medium |
| Stump | 年輪、割れ、根元影のある切り株 | 小型障害物 | Medium |
| FallenLog | 横倒しの丸太。断面、苔、影 | 中型障害物 | High |
| Rock_Mossy | 苔付き岩。明部と暗部あり | 中型障害物 | High |
| PebbleCluster | 小石の群れ | 通行可能装飾 | Medium |
| FlowerPatch | 小花と葉のまとまり | 通行可能装飾 | High |
| Reeds | 水辺の葦。細い茎と穂 | 水辺装飾 | Medium |
| Signpost | 木製看板。支柱、板、影 | インタラクト候補 | Medium |
| Chest | 木製宝箱。金具、明暗、接地影 | 報酬候補 | Medium |
| MerchantStall | 布屋根、木箱、商品影 | 商人 | Medium |
| ForestAltar | 石と草の小祭壇 | イベント候補 | Low |

## 火山差し替えオブジェクト

| 森・草原の役割 | 火山オブジェクト | 見た目 | 優先度 |
| --- | --- | --- | --- |
| BroadleafTree | BurntTree | 葉のない焦げ木、赤い炭火点 | High |
| ForestWall | CharredTreeWall | 黒い幹の密集、灰の影 | High |
| LowBush | BasaltLowRock | 黒い低岩、角張った影 | High |
| Bush_Round | LavaCrystalCluster | 赤い結晶と黒岩 | Medium |
| TallGrass | AshTuft | 灰だまり、熱気線 | High |
| Mushroom | EmberMushroom | 焼けたキノコ、橙の縁 | Low |
| Stump | CharredStump | 炭化した切り株 | Medium |
| FallenLog | BurntLog | 焦げた倒木、割れ目の赤熱 | Medium |
| Rock | BasaltRock | 黒い火山岩、灰色ハイライト | High |
| FlowerPatch | SparkCrystal | 火花、赤い結晶粒 | Medium |
| Reeds | SteamVent | 熱気の噴出口 | Low |
| Chest | IronChest | 黒鉄と焦げ跡の宝箱 | Medium |
| Altar | VolcanoAltar | 黒岩と赤い光の祭壇 | Low |

## 雪差し替えオブジェクト

| 森・草原の役割 | 雪オブジェクト | 見た目 | 優先度 |
| --- | --- | --- | --- |
| BroadleafTree | SnowPine | 雪をかぶった針葉樹、青い影 | High |
| ForestWall | SnowPineWall | 連続した雪樹冠と幹 | High |
| LowBush | SnowBush | 丸い雪だまり付き低木 | High |
| Bush_Round | FrostBush | 凍った低木、薄青の影 | Medium |
| TallGrass | SnowGrass | 雪から出た低い草 | High |
| Mushroom | FrostMushroom | 白青のキノコ、氷影 | Low |
| Stump | SnowStump | 雪をかぶった切り株 | Medium |
| FallenLog | FrozenLog | 凍った倒木、雪の乗った上面 | Medium |
| Rock | SnowRock | 雪の乗った岩、青い接地影 | High |
| FlowerPatch | IceCrystalSmall | 小さな氷結晶 | Medium |
| Reeds | FrozenReeds | 凍った葦、薄氷の足元 | Low |
| Chest | FrostChest | 霜付き宝箱 | Medium |
| Altar | IceAltar | 氷と石の祭壇 | Low |

## 作画チェック

- 木は円に見えないか。
- キノコは赤い丸に見えないか。
- 低木と草むらの通行可否が見た目で分かるか。
- 宝箱や看板は画面内で小さくても読めるか。
- 通行可能装飾が移動ルートを塞いでいるように見えないか。
- 花、背高草、水草などの独立した装飾がGrass Surfaceへ焼き込まれず、Decorationsへ自由配置されているか。
- 同じ木Prefabだけが規則的に反復していないか。
- 個別木と森・崖の大型モジュールの役割が見た目と配置方法の両方で分かれているか。
- 表示Root、Decorations、PropsにCollider2D / Rigidbody2Dが混入していないか。Obstacleは規定のCollisionBody以外に物理Componentがないか。
- 木、崖、Props、TallGrass、Reedsがカテゴリ別Orderではなく、足元YでPlayerと正しく前後するか。
- FlowerPatchとWildflowersがPlayerより前へせり出さず、平面装飾として地面側に留まるか。
- 崖のFootprintがCollisionTilemapへ明示Paintされ、森・木・低木・岩・切株・倒木のCollisionBodyが幹・接地点へ一致しているか。

## 旧方式の履歴

- v3は10 Prop PrefabをProps Gridへ1Cell GameObject Brushで配置し、用途別Collider / TriggerをPrefabへ持たせていた。森壁は個別PrefabではなくForestWall Blob-47が担当した。
- v4はObstacleVisuals / Propsを自由配置へ移したが、TallGrass、FlowerPatch、Wildflowers、ReedsはDetail Tileのままだった。
- v4.1では4 Decorationもplain Decorationsへ移し、表示Prefab 19点をObstacleVisuals / Decorations / Propsへ分類した。Grassは4つの標準Tileであった。
- v4.2では表示Prefab 19点と自由配置方針を継続し、Groundだけを装飾なしGrass 8差分の`GroundVariationTile`へ置換する。旧方式は現行契約として使わない。
- v4.3では表示Prefab 19点を維持し、Dirt / Stone全Maskの道路端差分だけを増やした。表示・判定分離と自由配置方針は現行契約として継続する。
