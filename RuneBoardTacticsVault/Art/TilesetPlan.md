# TilesetPlan

## 基本方針

- タイルサイズは32x32を基本にする。
- すべての地面タイルは単色禁止。草線、石、小花、影、色ムラなどの意味あるディテールを含める。
- 地形切り替えは必ず境界タイルを使う。
- 森、火山、雪は同じタイル役割を持ち、見た目だけ差し替えられる構造にする。
- 優先度は `High` が最初の仮素材化対象、`Medium` が密度改善対象、`Low` が後続の装飾拡張。

## 必須境界タイル

| 境界 | 用途 | 優先度 |
| --- | --- | --- |
| 草地 ↔ 土道 | 道を自然に草へ馴染ませる | High |
| 草地 ↔ 石畳 | 中央広場や小道の縁 | High |
| 草地 ↔ 水 | 池、水路、岸辺 | High |
| 草地 ↔ 森壁 | 通行不能な森の縁 | High |
| 草地 ↔ 崖 | 段差、地形制限 | High |
| 道 ↔ 石畳 | 土道から広場への接続 | High |
| 水 ↔ 崖 | 崖付き水辺、池の高低差 | Medium |

## 森・草原タイル

### 地面

| タイル | 内容 | 用途 | 優先度 |
| --- | --- | --- | --- |
| Grass_Base | 通常草。短い草線と細かな色ムラ入り | 基本地面 | High |
| Grass_Light | 明るい草。日当たりや広場の抜け感 | 地面バリエーション | High |
| Grass_Dark | 暗い草。森壁付近や影 | 地面バリエーション | High |
| Grass_Grain | 草粒、葉片が多い草 | 密度追加 | High |
| Grass_Flower | 小花が散った草 | 通行可能装飾地面 | High |
| Grass_Pebble | 小石混じりの草 | 道や崖近く | High |
| TallGrass_Center | 背の高い草むら | 通行可能な密度表現 | High |
| TallGrass_Edge_8way | 草むら境界 | 草むらの自然な輪郭 | High |

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

### 森壁・崖

| タイル | 内容 | 用途 | 優先度 |
| --- | --- | --- | --- |
| ForestWall_CanopyTop | 樹冠上部。葉房と影 | 通行不能な森壁 | High |
| ForestWall_CanopyMid | 樹冠中央 | 森壁連結 | High |
| ForestWall_Trunk | 幹と根元 | 森壁の足元 | High |
| ForestWall_GrassEdge_8way | 草地との境界 | 草地 ↔ 森壁 | High |
| Cliff_TopGrass | 草付き崖上 | 草地 ↔ 崖 | High |
| Cliff_Face | 土と岩の側面 | 段差 | High |
| Cliff_Shadow | 崖下影 | 高低差表現 | Medium |

## 火山タイル

森・草原と同じ役割を維持して差し替える。

| 森・草原の役割 | 火山差し替え | ディテール | 優先度 |
| --- | --- | --- | --- |
| Grass_Base | Ash_Base | 火山灰、黒い粒、灰色ムラ | High |
| Grass_Light | Ash_GlowCrack | 赤熱した細い亀裂 | High |
| Grass_Dark | Ash_Dark | 黒い火山灰、影 | High |
| Grass_Flower | Ash_Crystal | 赤い結晶、火花 | Medium |
| Dirt_Center | BurntPath_Center | 焦げた土道、黒い石 | High |
| Stone_Center | BasaltStone_Center | ひび割れた黒石 | High |
| Water_Center | Lava_Center | 溶岩流、明るい筋 | High |
| Water_Edge | Lava_RockEdge_8way | 黒岩の溶岩岸 | High |
| ForestWall | BurntTreeWall | 枯れ木、炭化した幹 | High |
| Cliff | VolcanoCliff | 黒岩、赤い亀裂、影 | High |

## 雪タイル

森・草原と同じ役割を維持して差し替える。

| 森・草原の役割 | 雪差し替え | ディテール | 優先度 |
| --- | --- | --- | --- |
| Grass_Base | Snow_Base | 雪粒、薄い青影 | High |
| Grass_Light | Snow_Packed | 踏み荒らされた雪 | High |
| Grass_Dark | Snow_Shadow | 青みの影雪 | High |
| Grass_Flower | Snow_Crystal | 小さな氷結晶 | Medium |
| Dirt_Center | PackedSnow_Path | 足跡、圧雪 | High |
| Stone_Center | SnowStone_Center | 雪に埋もれた石畳 | High |
| Water_Center | Ice_Center | 薄氷、白いハイライト | High |
| Water_Edge | Ice_SnowEdge_8way | 雪岸、薄氷境界 | High |
| ForestWall | SnowPineWall | 雪をかぶった針葉樹壁 | High |
| Cliff | IceCliff | 凍った崖、氷壁 | High |
