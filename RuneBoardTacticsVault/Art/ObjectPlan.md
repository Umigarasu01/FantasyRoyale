# ObjectPlan

## 基本方針

- オブジェクトは透過PNG想定。
- 単純な円、四角、アイコン風の記号は禁止。
- 各オブジェクトは輪郭、暗部、中間色、明部、接地影を持つ。
- 通行不能オブジェクトは足元や影で障害物だと分かるようにする。
- 通行可能装飾は小さく、移動ルートを誤認させない形にする。
- 森、火山、雪で同じ役割のオブジェクトを用意し、バイオーム差し替えできるようにする。

## 共通カテゴリ

| カテゴリ | 用途 | Collider方針 | 優先度 |
| --- | --- | --- | --- |
| 大型障害物 | 外周、区画分け、森壁 | 足元中心のBox/Capsule | High |
| 中型障害物 | 通路縁取り、戦闘広場の外周 | BoxCollider | High |
| 小型障害物 | ルート制限、小さな遮蔽 | 小さめBoxCollider | Medium |
| 通行可能装飾 | 密度、雰囲気 | Colliderなし | High |
| インタラクト | 宝箱、看板、商人、祭壇 | 小Collider + Interaction | Medium |
| 危険装飾 | 溶岩、薄氷、毒など | TriggerまたはTile判定 | Low |

## 森・草原オブジェクト

| オブジェクト | 見た目 | 用途 | 優先度 |
| --- | --- | --- | --- |
| BroadleafTree | 複数の葉房、濃い影、幹、根元を持つ通常木 | 大型障害物 | High |
| BroadleafTree_Large | 2x2以上の大きな樹冠。森壁にも混ぜる | 外周、ランドマーク | High |
| ForestWall_Canopy | 連続した樹冠パーツ | 通行不能な森壁 | High |
| ForestWall_Trunk | 幹、根、暗い足元 | 森壁の下段 | High |
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
