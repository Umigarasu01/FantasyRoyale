# GeneratedAssets

## 2026-08-03 96x72 Battle Royale Reference QA

状態: **Unity Camera生成・目視QA・Validator・回帰テスト完了**

- 生成方法: `GbaForestBattleRoyaleMapBuilder.CaptureReferencePreviews()`が保存済みUnity SceneをRenderTextureへ描画する。AI画像生成は使用しない。
- Scene: `Assets/Scenes/MapAuthoring/GbaForestBattleRoyaleReference.unity`。
- 用途: 96x72全体のルート、森林密度、水域、広場と、中央・東水辺の局所品質を確認する。
- Overview: `Assets/Art/Generated/MapAuthoring/QA/gba-forest-battle-royale-reference-overview.png`、1600x1200、SHA-256 `071780971D588C6808960909350136E04BAAD2819CC131DC465C4A5791CAF801`。
- Central: `Assets/Art/Generated/MapAuthoring/QA/gba-forest-battle-royale-reference-central.png`、1280x960、SHA-256 `A60CC1A11982E5411852150407C731D62FE6B13683AAEBDE395D954AEFC8CDFF`。
- Waterfront: `Assets/Art/Generated/MapAuthoring/QA/gba-forest-battle-royale-reference-waterfront.png`、1280x960、SHA-256 `D1DA39C6E04ADEA7A0C12671996608EF5116E83FCEB8D31CB6723083E6E50438`。
- 目視QA: 初版の長い矩形Loopと等間隔森林を修正し、短い段差を連ねた道路、密度差のある森林島、不規則な湖岸へ更新した。

## 2026-08-03 Map Authoring Kit v4.3 Road Edge Sources

状態: **Source画像2点生成・v4.3 Atlas収録・Unity検証済み**

- 用途: Dirt / Stone の境界が32pxグリッドごとに同じ輪郭を反復する問題を抑え、地面のGrassと自然につながる道路端を作る。
- 生成日: 2026-08-03。
- 生成方法: Codex built-in `imagegen`。OpenAI APIや外部画像サービスは使用していない。
- 参照方針: `Assets/Art/Map/Source/Forest/forest-gba-design-master.png` を色、材質、陰影、輪郭、GBA風Pixel表現の正本として維持した。
- プロンプト概要: 2x2配置の独立したEdge Donorを4点作り、各輪郭へ1～3pxの不規則差分を与えた。均等間隔の歯形や反復を禁止し、最終編集で外周背景を均一な`#ff00ff`へ置換した。
- Atlas変換: 非Full Maskの外側Grassを透明化して`GroundTilemap`を表示し、Dirt / StoneともCardinal-16の全Maskへ4差分を持たせる。Unityでは4 Cell区間内で4差分を各1回使い、座標・Rule・SeedのHashで24通りの順列から選ぶ。区間境界を含む同一差分の連続は最大2 Cellとする。
- 扱い: AI生成の初期素材。正本の方向性を維持した正式素材への差し替え余地を残す。

| Source | Workspace保存先 | SHA-256 | Built-in imagegen出力 |
| --- | --- | --- | --- |
| Dirt Edge Profile 4 Variants | `Assets/Art/Generated/MapAuthoring/Source/Edges/dirt-edge-profile-variants-source.png` | `AE2FF1BF7DAE734D29B60240CCD33EF2BC45BCF2ADA552C5D6CE6F6C5DC33C65` | `C:/Users/umiga/.codex/generated_images/019f4a24-bd4d-7230-9508-ea496856a00d/exec-2335bff3-2177-47af-9dde-286f22bbcb0e.png` |
| Stone Edge Profile 4 Variants | `Assets/Art/Generated/MapAuthoring/Source/Edges/stone-edge-profile-variants-source.png` | `26F64D7E9C7519058DE6DF33D68A5424CC879E08D5A16DC2B0A3DFD93B2EE541` | `C:/Users/umiga/.codex/generated_images/019f4a24-bd4d-7230-9508-ea496856a00d/exec-89e32e33-f681-40cb-b0a3-35183f9b0478.png` |

### v4.3 Production Atlas

- Atlas: `Assets/Art/Map/Production/Forest/forest-gba-production-atlas.png`。
- Catalog: `Assets/Art/Map/Production/Forest/forest-gba-production-atlas.json`。
- Schema: `fantasyroyale.map-authoring-atlas.v4.3`。
- Canvas: 1024x1024。
- Named Sub-Sprite: 205点。
- 内訳: Grass 8、Dirt 64、Stone 64、Water 50、Detail 4、Props 10、Obstacle Visual 5。
- Dirt / Stone: Cardinal-16の16 Maskそれぞれに4差分。非Full Maskの外側は透明なGround Underlayとし、4 Cell単位の差分順列を座標・Rule・SeedのHashで固定する。
- Dirt / Stone接続Socket: 各辺の接続範囲は`[6, 26)`の20px、Collar Depthは2px。Corner Fillは対象辺Bitと直交辺Bitが両方ある場合だけ適用し、Straight Mask `05` / `0a`にはCorner Tabを付けない。
- Production Atlas SHA-256: `412C63A03CD84472A9928DA5BF687903DB47AD3FF75DAECD246222EDD50D5FF4`。
- Sprite Catalog SHA-256: `EA6A6D111FF41DE5081D7537C5A20D0F74F2854D1C492D747FF94F9070B68600`。

### v4.3 QA Images

- Production Atlas QA: `Assets/Art/Generated/MapAuthoring/QA/forest-gba-production-atlas-v4-3.png`。
- Production Atlas QA SHA-256: `34923AEC3E2721EA1D2491D18A8CE15C6AAF65E5FF2799001367080838B848F1`。
- Unity Sample Preview: `Assets/Art/Generated/MapAuthoring/QA/gba-forest-sample-unity.png`。
- Unity Sample Preview SHA-256: `DD256066AC7DBD5C5A4BC798F750908A8C44D54A8DFE1483A01D9E3DE1E4275E`。
- Unity EditMode Test: 43 passed / 0 failed / 0 skipped。

## 2026-08-02 GBA Forest Design Master

- 用途: 森Themeの色、陰影、材質、輪郭、GBA風Pixel表現を固定する唯一のデザイン正本。
- 保存先: `Assets/Art/Map/Source/Forest/forest-gba-design-master.png`。
- 指定日: 2026-08-02。
- 指定方法: ユーザーが提示した画像を正本として採用。旧v2 CandidateとはPNG保存表現が異なるが、展開後Pixelが一致することを確認したうえで変更不可のMasterへ保存した。
- SHA-256: `D90F51F33EFB470728924B8088035E777AAE7E0DF133AB3A799522114A50B705`。
- 扱い: Unityが直接表示するAtlasではない。見た目の判断、Source Sheet作画、Production Atlas出典の基準として保持する。

## 2026-08-02 Map Authoring Kit v4 Obstacle Visual Sources

状態: **Source画像5点生成、v4 Atlas収録、Unity検証済み**

- 用途: v4でForestWall / Cliff Blob-47を置き換える、自由配置の森・崖表示モジュール。
- 保存先: `Assets/Art/Generated/MapAuthoring/Source/ObstacleVisuals`。
- 生成日: 2026-08-02。
- 生成方法: Codex built-in `imagegen`。OpenAI APIや外部画像サービスは使用していない。
- 参照方法: `forest-gba-design-master.png`を色、陰影、材質、輪郭、GBA風Pixel表現のデザイン正本として参照。
- 後処理: 外周と連続するMagenta Keyを透明化。元の非背景PixelをProgramで再作画しない。
- 共通指定: トップダウンGBA / 16-bit風、hard pixel、左上光源、正本と同じPalette・材質・輪郭、文字・ロゴなし。
- 扱い: AI生成の初期素材。正式素材へ差し替える可能性を残すが、Sprite名、足元Pivot、役割、左上光源は維持する。

| Sprite名 | 保存先 | プロンプト概要 |
| --- | --- | --- |
| `obstacle_forest_mass_wide` | `Assets/Art/Generated/MapAuthoring/Source/ObstacleVisuals/obstacle-forest-mass-wide.png` | 横に長い密な森塊。樹冠、露出する幹、根元影を一体の完成モジュールとして作る。 |
| `obstacle_forest_mass_deep` | `Assets/Art/Generated/MapAuthoring/Source/ObstacleVisuals/obstacle-forest-mass-deep.png` | 奥行きのある密な森塊。外周や角を厚く見せられる完成モジュールとして作る。 |
| `obstacle_forest_front_strip` | `Assets/Art/Generated/MapAuthoring/Source/ObstacleVisuals/obstacle-forest-front-strip.png` | 森壁の手前縁を構成する横長表示。樹冠、幹、根元影を一体化する。 |
| `obstacle_cliff_straight_wide` | `Assets/Art/Generated/MapAuthoring/Source/ObstacleVisuals/obstacle-cliff-straight-wide.png` | 草付き上面、暖色の岩面、下端影を持つ横長の直線崖モジュール。 |
| `obstacle_cliff_outer_corner` | `Assets/Art/Generated/MapAuthoring/Source/ObstacleVisuals/obstacle-cliff-outer-corner.png` | 草付き上面、岩面、下端影が外角へ折れる崖モジュール。 |

5点はBody / Canopyへ分割せず、それぞれ一体の表示Prefabとして使う。Production Atlasへ収録するSprite名は上表のunderscore名に固定する。

## 2026-08-02 Decoration Sources（v4.1導入・v4.2継続）

状態: **Source画像4点生成、Prefab化、Unity検証済み**

- 用途: DetailTilemapへ焼き込んでいた通行可能装飾を、グリッドに拘束されない`Decorations`配下の表示Prefabへ置き換える。
- 保存先: `Assets/Art/Generated/MapAuthoring/Source/Decorations`。
- 生成日: 2026-08-02。
- 生成方法: Codex built-in `imagegen`。OpenAI APIや外部画像サービスは使用していない。
- 参照方法: `forest-gba-design-master.png`を色、陰影、輪郭、左上光源、GBA風Pixel表現の正本として参照。
- 後処理: Skill付属のChroma Key Helperで外周Magentaを透明化し、soft matte、despill、1px edge contractを適用。Unity用Spriteは足元中央Pivotへ固定する。
- 共通指定: トップダウンGBA / 16-bit風、hard pixel、単体の透過装飾、地面の矩形を描かない、文字・ロゴなし。
- 扱い: AI生成の初期素材。正式素材へ差し替える可能性を残すが、Sprite名、足元Pivot、役割、左上光源は維持する。

| Sprite名 | 保存先 | SHA-256 | プロンプト概要 |
| --- | --- | --- | --- |
| `detail_tall_grass` | `Assets/Art/Generated/MapAuthoring/Source/Decorations/decoration-tall-grass.png` | `568D6A30DE288E9281E434263EF633C9BDDA77308D62CF0D1038D9BFFF73DEFF` | 横長で疎密のある背高草。地面板を含めず、草葉のSilhouetteだけを作る。 |
| `detail_flower_patch` | `Assets/Art/Generated/MapAuthoring/Source/Decorations/decoration-flower-patch.png` | `D756CAC592282D7989FBF75FD52B6BA87167E6FC53C58016E67E7C4F95B1224A` | 白・黄・青を中心にした低い花群。Magentaと衝突する花色を避ける。 |
| `detail_wildflowers` | `Assets/Art/Generated/MapAuthoring/Source/Decorations/decoration-wildflowers.png` | `379367BBF076EE13DB41E45ED4CBAC5041823656BBB801A750C1F89348752138` | 疎らな野花と短い草の小群。周囲のGrassへ輪郭だけで馴染ませる。 |
| `detail_reeds` | `Assets/Art/Generated/MapAuthoring/Source/Decorations/decoration-reeds.png` | `413900FF4050BF917107872743480AC846D50C2B4A6F9DC915FE84BB8D6095F3` | 水際用の葦と蒲。縦長で足元を狭くし、岸へ自由配置できる形にする。 |

### Built-in imagegen元出力

| Source | Built-in imagegen出力 |
| --- | --- |
| Tall Grass | `C:/Users/umiga/.codex/generated_images/019f4a24-bd4d-7230-9508-ea496856a00d/exec-9ba30614-c973-4080-9a2b-693d2c974e31.png` |
| Flower Patch | `C:/Users/umiga/.codex/generated_images/019f4a24-bd4d-7230-9508-ea496856a00d/exec-e791326e-5a69-4c58-bd9d-14ad12f5c43a.png` |
| Wildflowers | `C:/Users/umiga/.codex/generated_images/019f4a24-bd4d-7230-9508-ea496856a00d/exec-727241d3-0085-4e5b-a6d0-1faf1d36f6d8.png` |
| Reeds | `C:/Users/umiga/.codex/generated_images/019f4a24-bd4d-7230-9508-ea496856a00d/exec-8f1b6f4d-0d4b-4744-a4e6-7466d8681549.png` |

## 2026-08-03 Map Authoring Kit v4.2 Surface Sources

状態: **Source画像4点生成、v4.2 Atlas収録、Unity検証済み**

- 用途: 広い地面、道路内部、水面で同一Spriteや短周期模様が目立つ問題を解消し、通行可能装飾を地面から完全に分離する。
- 保存先: `Assets/Art/Generated/MapAuthoring/Source/Surfaces/`。
- 生成日: 2026-08-03。
- 生成方法: Codex built-in `imagegen`。OpenAI APIや外部画像サービスは使用していない。
- 参照方法: `Assets/Art/Map/Source/Forest/forest-gba-design-master.png`を色、陰影、材質、輪郭、左上光源、GBA風Pixel表現の正本として参照。
- 共通指定: トップダウンGBA / 16-bit風、hard pixel、正本と同じPaletteと材質、平坦な`#ff00ff` gutter、文字・ロゴなし、花・草束・葦・睡蓮などの埋め込み装飾なし。
- 構成: Grassは4列x2行のquiet grass 8差分。Dirt / Stone / Waterは各2列x2行の全面Surface 4差分。
- 後処理: `Tools/MapAuthoring/build_forest_gba_atlas.py`がMagenta gutterでCellを分離し、32x32へNearest Neighborで正規化する。接続境界は既存Mask SourceとPackerが決定し、AI画像へTopology判断を委ねない。
- 扱い: AI生成の初期素材。正式素材へ差し替える可能性を残すが、差分数、Sprite名、32px規格、左上光源、装飾分離を維持する。

| Surface | 保存先 | SHA-256 | プロンプト概要 |
| --- | --- | --- | --- |
| Grass 8 Variants | `Assets/Art/Generated/MapAuthoring/Source/Surfaces/grass-surface-variants-source.png` | `E0EE4BC62090E3457B6E4EF36892DA4BA09CDE74D128B5C3FC13DC5D2A980EC0` | Design Master準拠のquiet grassを8面作る。低コントラストの葉と陰影だけに限定し、花、背高草、草束、地面装飾を描かない。 |
| Dirt 4 Variants | `Assets/Art/Generated/MapAuthoring/Source/Surfaces/dirt-surface-variants-source.png` | `5957838B73439D3ADDCA10D2CF81159FF6ED00A6AEE7435CDCB6FB2BFC649690` | Design Master準拠の全面土Surfaceを4面作る。小石と轍の密度だけを控えめに変え、草縁や装飾を埋め込まない。 |
| Stone 4 Variants | `Assets/Art/Generated/MapAuthoring/Source/Surfaces/stone-surface-variants-source.png` | `E8DEC1D7E9E8B94B1DBCB35DA808EDDDE55E6B1C4EF9876E33DA03AB3DA8BBAE` | Design Master準拠の全面石畳Surfaceを4面作る。暖色Grayの丸石と苔の配置を変え、草縁や独立Propを描かない。 |
| Water 4 Variants | `Assets/Art/Generated/MapAuthoring/Source/Surfaces/water-surface-variants-source.png` | `15C883CE055DD9DAF44FDC7B3993F4098BDAEC7BDB60BD91DA8F27F0B69C8DDD` | Design Master準拠の全面Turquoise水面を4面作る。さざ波と明暗だけを変え、岸、葦、睡蓮、泡の塊を埋め込まない。 |

### Built-in imagegen元出力

| Surface | Built-in imagegen出力 |
| --- | --- |
| Grass 8 Variants | `C:/Users/umiga/.codex/generated_images/019f4a24-bd4d-7230-9508-ea496856a00d/exec-65c6af54-89ca-4737-a8f7-07bcb44adefb.png` |
| Dirt 4 Variants | `C:/Users/umiga/.codex/generated_images/019f4a24-bd4d-7230-9508-ea496856a00d/exec-204b1b61-269f-4399-accb-a5c381008ebe.png` |
| Stone 4 Variants | `C:/Users/umiga/.codex/generated_images/019f4a24-bd4d-7230-9508-ea496856a00d/exec-9a51aa4c-9808-4133-9047-c26dc2cfe652.png` |
| Water 4 Variants | `C:/Users/umiga/.codex/generated_images/019f4a24-bd4d-7230-9508-ea496856a00d/exec-c57f401c-a993-4bb7-99d5-5dd47ee5e538.png` |

### v4.2 Production Atlas

- Atlas: `Assets/Art/Map/Production/Forest/forest-gba-production-atlas.png`。
- Catalog: `Assets/Art/Map/Production/Forest/forest-gba-production-atlas.json`。
- Schema: `fantasyroyale.map-authoring-atlas.v4.2`。
- Canvas: 1024x1024。
- Named Sub-Sprite: 115点。
- 内訳: Grass 8、Dirt 19、Stone 19、Water 50、Detail 4、Props 10、Obstacle Visual 5。
- ForestWall / Cliff Blob-47は収録しない。
- 生成方法: `Tools/MapAuthoring/build_forest_gba_atlas.py`による決定的ローカル変換。
- 入力: Design Master、既存Mask / Tree Variants Source、Surface Source 4点、Obstacle Visual Source 5点、Decoration Source 4点。
- Grass: `ground_grass_01`から`ground_grass_08`までの装飾なし8差分。Unityでは`GroundVariationTile`が座標Hashで選ぶ。
- 道路と水面: Dirt / Stoneの全面Mask `0f`、Waterの全面Mask `ff`へ各3差分を追加し、各4 Spriteとする。他Maskは1 Spriteを維持する。
- 表示方法: 頻出全面MaskだけRuleTileのRandom出力、全RuleのTransformはFixed。Shader、回転、反転、Runtime Texture生成は使わない。
- Production Atlas SHA-256: `13A2D2C6AD63A377CE6080F8AE4BD03093B8B1BA9161BBA84F3A2154951C96E9`。
- Sprite Catalog SHA-256: `DFEB397B73C7784D6C6EE7203B841AA0213E230F729EA529AD769898AB5648DE`。

## 2026-08-03 Map Authoring QA Images v4.2

### Production Atlas QA

- 保存先: `Assets/Art/Generated/MapAuthoring/QA/forest-gba-production-atlas-v4-2.png`。
- 生成方法: `build_forest_gba_atlas.py`。
- 用途: 115 SpriteのFamily、Grass 8差分、全面Maskの各4差分、透明縁、色調、接続形状、PropとObstacle Visualのサイズを一覧確認する。
- SHA-256: `598216621F672A61BD7DA430C6B44BA5CF71807093410C06605553874BEBC213`。

### Unity Sample Preview

- 保存先: `Assets/Art/Generated/MapAuthoring/QA/gba-forest-sample-unity.png`。
- 生成方法: v4.2のGbaForestMapSampleをUnity CameraからRenderTextureへ描画。
- 構成: 30x20 Cell、4 Tilemap、自由配置Decorations / ObstacleVisuals / Props、不可視Collision、Socketを収録する。道路はWaterの論理Cellを先に確定し、Waypointから3 Cell幅の単一Componentを作り、重複と分断を検証してからDirt / Stoneを配置する。
- Ground確認: 600 CellでGrass 8差分を全使用。出現数`72 / 74 / 75 / 78 / 82 / 70 / 80 / 69`、Cardinal同一差分率12.00%、最大連続長は横3 / 縦4、Offset 16一致率は横11.79% / 縦12.50%。
- 頻出全面Mask確認: Dirt `0f` 46 Cell、Water `ff` 10 Cell。各4差分のRandom出力、Fixed Transform。
- SHA-256: `A49438073B9AD04434A44D62F23B42A8097AF3C712813E36A17FD73850B93218`。
- 確認結果: Unity Rebuild、Camera Capture、EditMode Testが成功。41 passed / 0 failed / 0 skipped。

## 履歴: 2026-08-02 Map Authoring Kit v4.1 Production Atlas

- 当時のAtlas保存先: `Assets/Art/Map/Production/Forest/forest-gba-production-atlas.png`。同Pathの現行ファイルはv4.2へ更新済み。
- 当時のCatalog保存先: `Assets/Art/Map/Production/Forest/forest-gba-production-atlas.json`。同Pathの現行ファイルはv4.2へ更新済み。
- Schema: `fantasyroyale.map-authoring-atlas.v4.1`。
- Canvas: 1024x1024。
- Named Sub-Sprite: 102点。
- 内訳: Grass 4、Dirt 16、Stone 16、Water 47、Detail 4、Props 10、Obstacle Visual 5。
- ForestWall / Cliff Blob-47は収録しない。
- 生成方法: `Tools/MapAuthoring/build_forest_gba_atlas.py`による決定的ローカル変換。
- 入力: Design Master、Dirt / Stone / Water / Tree Variants Source、Obstacle Visual Source 5点、Decoration Source 4点。
- 道路形状: AI Source Sheetは質感Donorとして使い、Mask形状はPackerで決定する。隣り合う2方向が開く角を正本の全面素材で埋め、`0f`を全面路面にする。Dirt全面素材は外周陰影を除いた中央70%を使用し、3 Cell幅で草穴や暗い格子線を作らない。
- Production Atlas SHA-256: `6E4CCC929B386BEBE9E3A3AAAC5B1F1373A9E3C4E5C36B3A0AB13ED95B6030C6`。
- Sprite Catalog SHA-256: `405F14B1A0D2B9081170A9FBC4981967C5B9BACC5F1C650B270620756E9B6F2E`。

## 履歴: 2026-08-02 Map Authoring QA Images v4.1

### Production Atlas QA

- 保存先: `Assets/Art/Generated/MapAuthoring/QA/forest-gba-production-atlas-v4-1.png`。
- 生成方法: `build_forest_gba_atlas.py`。
- 用途: 102 SpriteのFamily、透明縁、色調、接続形状、PropとObstacle Visualのサイズを一覧確認する。
- SHA-256: `8F18CE22C6D31B917BF0E6C49901FBB995BD0EFE43CE4899A1BBEAB03F9821D1`。

### Unity Sample Preview

- 当時の保存先: `Assets/Art/Generated/MapAuthoring/QA/gba-forest-sample-unity.png`。同Pathの現行ファイルはv4.2へ更新済み。
- 生成方法: GbaForestMapSampleをUnity CameraからRenderTextureへ描画。
- 構成: 30x20 Cell。4 Tilemap、Decorations 16 Instance、5 Obstacle Visual、Tree 3差分、BlockingBush、MossyRock、Stump、FallenLog、MushroomPatch、Chest、Signpost、不可視Collision、Socketを収録する。道路はWaypointからRadius 1で3 Cell幅へ生成し、DirtからStone広場まで単一Componentとする。
- Camera: Position `(-0.5, -0.25)`、Orthographic Size `10.6`。
- SHA-256: `EC6672D49B9D3F6B3E5135E1122F355C312E3B07F429500B43AC3DAC091BC0F2`。
- 確認結果: Unity Rebuild、Validator、Camera Capture、目視QA、EditMode Testが成功。39 passed / 0 failed / 0 skipped。Dirt内部の草穴・暗いCell境界とDirt / Stone境界の草縁がないことを確認した。

## 履歴: 2026-08-02 Map Authoring Kit v3 Extension Source Sheets

Dirt、Stone、Water、Tree Variantsの4点はv4でも継続使用する。ForestWall / Cliff Sourceはv4で削除済み。以下は生成当時の記録。

- 用途: Design Masterに不足するCardinal-16、Blob-47、Tree 3差分を、正本と同じデザインで完成形として作画する。
- 生成日: 2026-08-02。
- 生成方法: Codex `imagegen` built-in image generation。OpenAI APIや外部画像サービスは使用していない。
- 参照方法: Design Masterを見た目の参照、旧Topology Guideを配置だけの参照として与えた。Guideは最終素材へ使用せず削除した。
- 共通指定: トップダウンGBA / 16-bit風、hard pixel、アンチエイリアスなし、左上光源、正本と同じPalette・材質・輪郭、平坦な `#ff00ff` gutter、文字・ロゴなし。
- 扱い: AI生成の初期素材。正式素材へ差し替える可能性を残すが、Family名、Mask順、接続Socket、Pivot、左上光源は維持する。

### 保存先と最終プロンプト要約

| Source Sheet | 保存先 | 最終プロンプト要約 |
| --- | --- | --- |
| Dirt Cardinal-16 | `Assets/Art/Map/Source/Forest/Extensions/dirt-cardinal16-source.png` | 4x4の16接続を厳密に維持し、草へ馴染む上質な土道として、終端、直線、角、T字、交差と中央Socketを作画する。 |
| Stone Cardinal-16 | `Assets/Art/Map/Source/Forest/Extensions/stone-cardinal16-source.png` | 4x4の16接続を厳密に維持し、苔のある暖色Grayの丸石道として、全接続形状と中央Socketを作画する。 |
| Water Blob-47 | `Assets/Art/Map/Source/Forest/Extensions/water-blob47-source.png` | 8x6のBlob-47と空Cellを厳密に維持し、Turquoiseの水面、草と土の岸、浅瀬、泡を、内角、外角、島、入り江を含めて作画する。 |
| ForestWall Blob-47 | `Assets/Art/Map/Source/Forest/Extensions/forest-wall-blob47-source.png` | 8x6のBlob-47と空Cellを厳密に維持し、密な樹冠、露出する南辺の幹・根、側面Depthを統合した連続森壁を作画する。 |
| Cliff Blob-47 | `Assets/Art/Map/Source/Forest/Extensions/cliff-blob47-source.png` | 8x6のBlob-47と空Cellを厳密に維持し、草付き上面、暖色の岩面、下端影を統合した全接続形状を作画する。 |
| Tree Variants | `Assets/Art/Map/Source/Forest/Extensions/tree-variants-source.png` | 横3Cellへ、丸形、横広、縦長の3種の広葉樹を、共通の幹・根元Footprintと足元中央基準で作画する。 |

### Imagegen元出力

| Source Sheet | Built-in imagegen出力 |
| --- | --- |
| Dirt Cardinal-16 | `C:/Users/umiga/.codex/generated_images/019f4a24-bd4d-7230-9508-ea496856a00d/exec-387e176b-6e49-4791-ac3b-c28bf08370f7.png` |
| Stone Cardinal-16 | `C:/Users/umiga/.codex/generated_images/019f4a24-bd4d-7230-9508-ea496856a00d/exec-64857fb7-89f2-4fac-9569-f9f75f361e99.png` |
| Water Blob-47 | `C:/Users/umiga/.codex/generated_images/019f4a24-bd4d-7230-9508-ea496856a00d/exec-b7f5b84c-3013-4141-973b-322a1f532e30.png` |
| ForestWall Blob-47 | `C:/Users/umiga/.codex/generated_images/019f4a24-bd4d-7230-9508-ea496856a00d/exec-cd8adccf-a4b1-40fd-8924-b3afddfde06a.png` |
| Cliff Blob-47 | `C:/Users/umiga/.codex/generated_images/019f4a24-bd4d-7230-9508-ea496856a00d/exec-8a718e40-a68d-4608-940f-a8d180096789.png` |
| Tree Variants | `C:/Users/umiga/.codex/generated_images/019f4a24-bd4d-7230-9508-ea496856a00d/exec-04c20c91-9b9b-4a99-bf30-049273539ff5.png` |

## 履歴: 2026-08-02 Forest Production Atlas v3

- 用途: Unity Tilemap、RuleTile、Prefab、GameObject Brushが実際に参照する唯一の表示Texture。
- Atlas: `Assets/Art/Map/Production/Forest/forest-gba-production-atlas.png`。
- Catalog: `Assets/Art/Map/Production/Forest/forest-gba-production-atlas.json`。
- Schema: `fantasyroyale.map-authoring-atlas.v3`。
- Canvas: 1024x1024。
- Named Sub-Sprite: 191点。
- 生成方法: `Tools/MapAuthoring/build_forest_gba_atlas.py` による決定的ローカル変換。
- 入力: Design Master 1点とExtension Source Sheet 6点。
- 許可処理: 承認済みRectの切り出し、透明背景が必要なSpriteだけに行う外周接続の狭義Magenta Alpha化、Nearest Neighbor規格化、透明Padding、決定的Packing、CatalogとQA画像生成、検査。
- 禁止処理: Programによる輪郭、岸、泡、陰影、幹、根の作画、色替え、回転、反転、変形、Runtime Pixel合成。
- 再現情報: CatalogへSprite名、Atlas Rect、Pivot、Family、Mask、Source Path、Source Rectを記録する。
- Alpha契約: 広義MagentaはCrop境界検出だけに使い、元RGBを保持する。Water 47 Spriteは全32x32 / Alpha 255をbuild時にfail-fast検査する。
- ForestWall補正: 140x140 Cell全体の縮小で残っていたMagenta / Alpha insetを除外し、全47 Maskへ共通の正方形Socket Crop `(x=20, y=16, width=110, height=110)` を適用する。縦横比を維持したままNearest 32px化する。
- ForestWall自己検査: 接続辺136 / 136が境界到達、非接続辺52 / 52が境界非到達、`ff` は1024 / 1024 Opaqueかつ4辺が各32 / 32 Opaque。32px出力の接続辺Opaque幅は最小17px、平均26.07px。向かい合う全SocketのOpaque重なりは12px以上（実測最小は東西13px、南北15px）。Mask / Edge / Socket逸脱時はbuildをfail-fastする。
- Production Atlas SHA-256: `75D5560B1E68CD970C9F5B7895C4C5B54CA57C7826B4631D10EA8CE9F639BE2A`。
- Sprite Catalog SHA-256: `21DD3E7D0E042DD3E76B71F59C34E8579EF89A4851160D6C70B5224DE1657D42`。
- 扱い: AI生成Sourceを組み合わせた初期Production素材。正式素材へ差し替え可能だが、Atlas名、Sprite名、Family、Topology、32px契約を維持する。

内訳:

| Family | 構成 | Sprite数 |
| --- | --- | ---: |
| Grass | Static | 4 |
| Dirt | Cardinal-16 | 16 |
| Stone | Cardinal-16 | 16 |
| Water | Blob-47 | 47 |
| ForestWall | Blob-47 | 47 |
| Cliff | Blob-47 | 47 |
| Detail | Static | 4 |
| Props | Tree 3差分とその他7種 | 10 |

Collision Paint Tileは表示Spriteを持たないためAtlasへ含めない。旧366 Loose PNG、旧 `asset-manifest.json`、旧Candidate Atlas、旧Program作画Scriptは削除済み。

## 履歴: 2026-08-02 Map Authoring QA Images v3

### Production Atlas QA

- 当時の保存先: `Assets/Art/Generated/MapAuthoring/QA/forest-gba-production-atlas-v3.png`。現行ではv4 QAへ置換済み。
- 生成方法: `build_forest_gba_atlas.py`。
- 用途: 191 SpriteのFamily、透明縁、色調、接続形状、Propサイズを一覧確認する。
- SHA-256: `53834EFA45C55C2FFC50312D13BA05947E58E690F831682AFA083EF4F8762CD2`。
- 確認結果: Family内訳、透明縁、色調、接続形状、PropサイズとForestWallの境界到達を目視確認済み。

### Unity Sample Preview

- 当時の保存先: `Assets/Art/Generated/MapAuthoring/QA/gba-forest-sample-unity.png`。同Pathの現行画像はv4 Sample QA。
- 生成方法: Unity 6000.4.3f1でGbaForestMapSampleをRenderTextureへ描画。
- 構成: 30x20 Cell。北西の段状ForestWall、南西から中央と池へ続くS字Dirt、5x3 Stone祠広場、島と南向き水路を持つWater、南東のCliff段丘、Tree 3種8本と全Prop種、Water Collision、手置きDetailを収録する。
- Camera: Position `(-0.5, -0.25)`、Orthographic Size `10.6`。
- 用途: v3の新素材だけで、RuleTile接続、Layer、Prefab反復配置、Full Rect Sprite Mesh、Collision分離を確認する。
- SHA-256: `9258F7F79DDD0B4966782FDDE37EA3389DB5AB3C69062E64D00C0EC4A343572C`。
- 確認結果: 再構築、Validator、Camera Captureが成功。展示要素、全Prop種、反復ForestWallに背景露出がないことを目視確認済み。

## 履歴: 2026-08-01 Map Authoring Kit v2素材

- Candidate Atlas v2とNormalized Forest Production Set v2は、道と水際の改善、ForestWall / Cliff Blob-47、Tree 3差分を検証するために使用した。
- 当時はCandidate Atlas v1 / v2と `build_forest_gba_assets.py` から366 Loose PNGを生成した。
- Dirtは各Mask 3 Random差分、Water / ForestWall / Cliffは各Mask 2 Random差分であった。
- v3がDesign Masterと6 Source Sheetを使う単一Atlasへ置換したため、v2素材は現行ではなく履歴。旧Candidate画像、366 Loose PNG、旧QA画像、旧Builder Scriptは削除済み。

## 履歴: 2026-07-10 Map Authoring Kit v1素材

- Candidate Atlas v1から71 Loose PNGを生成した。
- Dirt / Stone / WaterはCardinal-16、森壁と崖は単一Tile、Propは8点であった。
- v2を経てv3へ置換済み。旧CandidateとLoose PNGは削除済み。

## 2026-06-05 Concept Images

次の画像は現在も見た目の参考として保持する。

- `Assets/Art/Concept/forest-biome-sample-map.png`
- `Assets/Art/Concept/volcano-biome-sample-map.png`

森画像はGBA風、高密度なトップダウン2DアクションRPGの方向確認用。火山画像は同じ構造を別Biomeへ展開する将来案。

## 旧Biome Atlas

2026-06-05のBiome Atlas本体、Runtime切り出し、Preview、Tile Asset、Prefabは2026-07-10に削除した。新Map Authoring Kitとデータ役割が重複し、32px契約も満たしていなかったため。

削除前の状態はGit snapshot ce2d1d3で参照できる。
