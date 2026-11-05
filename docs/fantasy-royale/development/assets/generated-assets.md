# GeneratedAssets

## 2026-11-05 森マップの画作り部品

状態: **森マップの基準で使用。Repositoryの既存AssetやSceneからは参照していない**。検証用Projectでのみ使用した

[森マップの画作り基準](../../exec-plans/completed/forest-look-lock.md)のために5枚生成した。生成方法はOpenAI Image APIの`gpt-image-2`（`images/edits`、1536x1024、quality high）で、デザイン案からラベルを除いた範囲を参照画像として送信した。手順と`river-bank-s-curve-01`以外のPromptは`Tools/MapAuthoring/generate_forest_asset.py`にあるが、同じ画素は再生成できない。各生成の使用量は約6,300トークンである。

| Source | 保存先 | Prompt概要 | SHA-256 |
| --- | --- | --- | --- |
| river-bank-s-curve-01 | `Assets/Art/Generated/ForestLook/river-bank-s-curve-01.png` | 土の崩れた岸、葦、小さな岩を持つS字の川 | `8a8bd5c9b917adf95048fe93bce03e230958a77b1433aaee623dc92cfe77a65f` |
| river-bank-gentle-01 | 同Directoryの`river-bank-gentle-01.png` | 緩やかな曲線の川と岸 | 同名JSONを参照 |
| forest-trees-01 | 同Directoryの`forest-trees-01.png` | マゼンタ背景に異なる6種の木 | 同名JSONを参照 |
| forest-understory-01 | 同Directoryの`forest-understory-01.png` | マゼンタ背景のシダ、低木、花、岩、草など30種 | 同名JSONを参照 |
| forest-features-01 | 同Directoryの`forest-features-01.png` | マゼンタ背景の泉、薬草畑、魔物の巣、岩崖 | 同名JSONを参照 |

`river-bank-s-curve-01`のPrompt全文と使用量は`generation-manifest.json`、他の4枚は同名のJSONで確認する。`Tools/MapAuthoring/build_forest_parts.py`がシートを切り出し、`Parts/`へ40種の部品と`parts.json`を書き出す。川の2枚は検証に使ったが、現在の地図生成は岸と水面をデザイン案から作るため使用していない。

## 2026-10-03 Forest Composition追加素材と構成改善

状態: **候補Source7点・追加Sprite20点と水面Tile・代表4Seedの実Scene/Physics検査・Reference移行・EditMode113件/PlayMode4件・実Gameplay確認済み**。指定画像との最終比較と見た目の課題は継続中。

ForestRestartの空間構成改善用に、水路、橋、段丘、石の小道、道や岸の小物を追加する。生成方法はCodex built-in `image_gen`。店と泉は切替可能な構図候補であり、商取引や回復のGameplay機能は持たない。既存76Spriteと従来QAを保持した別Pathで比較し、その後、修正済みSeed20261003をReference Sceneへ同Path/GUIDで反映した。旧Referenceはバックアップから復元できる。

指定Library画像はHomePCの正規受取り手順でローカル化を完了できず、指定画像の実ピクセルは未確認。ローカルの`a-classic-adventure-texture-v005.png`を様式・密度の補助参照として使用したが、指定画像1との同一性は主張しない。画像中の店・泉の正確な形や配置を採用した扱いにもせず、既存素材は置き換えない。

| Source | 保存先 | Prompt概要 | SHA-256 |
| --- | --- | --- | --- |
| bridge | `Assets/Art/Map/ForestRestart/Composition/Source/bridge.png` | 広い木製Deck、3Cellの歩行域と欄干、透明背景の独立した橋。初回の浅いDeckから幅を修正した候補 | `434b1f7d70d2981cd3803db4d07bfe8a045a19b138e51a52742920aed6b5e513` |
| cliff | 同Source Rootの`cliff.png` | 草付き上面、岩面と接地影の連続した段丘Front | `0e8cef332282cb64d486fbbfee49df50b7d0a751f7040d3988030d8fc0dd2953` |
| shop | 同Source Rootの`shop.png` | 木材、布、商品のある小さな商人屋台の構図候補 | `1d482c89b3c84339f3863eab7e5b9921de5b2eb54a000a9d4ccf0223b6f741fc` |
| fountain | 同Source Rootの`fountain.png` | 石と水面、接地影を持つ小さな泉の構図候補 | `f17b02d49337f94adc5c41b9222df7e66edf8d6c5468c979ad70632bf13ff511` |
| water | 同Source Rootの`water.png` | 青緑の波と陰影を持つ不透明な水面Surface | `43a618beef7f1d96e9a210b0df3f71dfa003a228ae032defc103418853eb723e` |
| cobbles | 同Source Rootの`cobbles.png` | 苔や草の侵食を持つ透過した石の小道 | `847324f1c4927aa63a2601e162afeb6d46c0526d00b7041d2acbc9592a4ecfc2` |
| verge | 同Source Rootの`verge.png` | 道端や岸へ散らす草、花、小石の透過小物 | `ae34bdcc3e36dba6b782cc44da59c89daa9a29afdff1c75350cc999ac0374ddf` |

正確な保存Path、Byte数、SourceのHashは[Composition generation-manifest.json](../../../../Assets/Art/Map/ForestRestart/Composition/Source/generation-manifest.json)で確認する。Prompt全文は同Source Directoryの`landmark-prompts.json`、`terrain-prompts.json`、橋の修正分は`bridge-deep-prompt.json`へ保存する。上表は修正版橋Sourceの値であり、最初の描画時の旧橋Hashへ戻さない。

`ForestCompositionVisuals`はNearest Neighbor規格化、透明余白と足元の調整、水面の切り出しと対向辺共有、崖の中央列切り出しを行う。追加Sprite20点はbridge / cliff / shop / fountain / cobbles / verge6点、幅1〜6Cellの`cliff_01`〜`cliff_06`6点、`water_00`〜`water_07`8点。Spriteは`Assets/Art/Map/ForestRestart/Composition/Sprites/`、水面Tileは`Assets/Data/MapAuthoring/Tiles/ForestRestart/Composition/WaterVariation.asset`へ保存する。PPU32、Point、非圧縮、Mipmapなし、Full Rect、整数pixel Pivotを守り、色変更・回転・反転で差分を作らない。

配置は`ForestRestartLayout.Generate(seed, includeLandmarkCandidates)`から生成し、Unity Sceneを正本に保存する。代表生成は`ForestCompositionSettings.asset`とMenuから再現できる。[制作手順](../../design-docs/map-authoring/forest-restart-kit.md#現在の構成改善)と[CLI再現手順](../testing.md#forest-compositionの再現)を参照する。新しいSibling Sceneは`Assets/Scenes/MapAuthoring/ForestRestart/ForestCompositionSeed{seed}.unity`。

実画像は`Assets/Art/Map/ForestRestart/QA/composition-seed-{seed}-overview.png`、`-focused.png`、`-game-scale.png`、`-detail.png`、`-bridge-standing.png`。画像AIによるScene再作画はせず、Unity CameraでPlanとSceneの表示を撮影する。Seed20261003〜20261006は全件で論理検査と実Scene Validatorが成功し、5画像ずつ計20画像を保存した。橋Deckと崖Frontの修正後の全景、橋上、ゲーム縮尺の実ピクセルを確認し、実Physicsも全4SeedでError0。初回の橋幅修正前画像は最終結果として扱わない。[構成改善ExecPlan](../../exec-plans/active/forest-composition-improvement.md)を最終検証の記録先とする。

Reference移行後の実Gameplay PreviewもReady、失敗Message空で起動し、既存の青緑Debug Markerの移動とCamera追従を確認した。実HUDを含む`composition-runtime-start.png`と`composition-runtime-after-move.png`はscreen Captureであり、画像AI生成や静止した比較Heroの操作ではない。Runtime2画面を加えた計22PNGのHash、4SeedのPlan、Physics、最終Test結果は[composition-verification.json](../../../../Assets/Art/Map/ForestRestart/QA/composition-verification.json)へ保存した。

修正済みSeed20261003の代表出力は次のHashで確認できる。これら3点はLibraryへ保存成功した。Windowsの拡張属性処理は保存後のローカルmetadata追記に失敗したが、Libraryへの保存成功と区別する。指定入力2枚の受取り失敗は解消していない。

| 出力 | SHA-256 |
| --- | --- |
| `composition-seed-20261003-overview.png` | `ac5d83cc247c6ee3a4c6bb03993345d2fe0feafeeae26932517286b4374a70f3` |
| `composition-seed-20261003-game-scale.png` | `e70ccae65279cd1120c41e5c0d199f902a33c66aee8032537d89c3f3cf121607` |
| `composition-seed-20261003-bridge-standing.png` | `cc7f552081ad478ea24b80324c8a572933278b4d07cbff5a99eceffed272252d` |

試作途中に生成した未参照の`water_08`〜`water_31`と付随metaは、自分が追加したFileだけをGUID参照0・バックアップ確認後に整理した。現行追加Sprite契約は20点である。道と芝の階段状輪郭、水岸の薄さ、森に隠れる上段の段差は残り、動作検査の成功とは別に見た目の改善を続ける。

以下のForestRestart40論理Plan / 4Scene / 98EditMode / 4PlayModeは前段階の履歴で、今回の変更後の検証数へ流用しない。今回のReference移行後の最終回帰はEditMode113 / 113、PlayMode4 / 4成功、Failed / Skipped / Inconclusiveは0。既存Characterの正式表示採用や試合ごとの地形生成は変更していない。目標画像との最終比較と素材の見た目の評価は継続する。

## 2026-10-03 ForestRestart素材と複数Seed検証

状態: **検証用素材6点生成・76Sprite / 3Tile / 4Prefab・40論理Plan / 4Scene / 8QA画像を生成・正式素材採用は未承認**

- ユーザー指定: コンセプトアートを基準に素材制作と複数パターン検証を行い、仕切り直しとして既存素材を整理・削除する。
- 作画基準: `Assets/Art/Concept/style-comparison-2026-10-03/a-classic-adventure-texture-v005.png`。Character頭身はv004の肯定を引き継ぎ、表面質感は初版Aへ沿わせる。
- 生成方法: Codex built-in `image_gen`。Grass / Dirtの不透明な地表2点と、oak / grove / undergrowth / heroの透明背景4点を個別に生成した。
- 修正履歴: grove / heroは外周のGlowとHazeを除くalpha編集を実施。Grass / Dirtは初回Unity QAで目立った粗い粒・明るさを抑え、コンセプトの色と小さな材質描写へ寄せて再生成した。
- 完全なPrompt、初回出力、refinement出力、現在のSource SHA-256: [generation-manifest.json](../../../../Assets/Art/Map/ForestRestart/generation-manifest.json)。Schemaは`fantasyroyale.forest-restart-generation.v1`、Statusは`prototype-validation-assets`。
- Sourceの現行Hashはmanifestの`assets[].sha256`と保存PNGを照合し、6点すべて一致を確認した。下表はGrass / Dirt再生成後の値であり、初回SourceのHashへ戻さない。

| Source | 現行保存先 | SHA-256 |
| --- | --- | --- |
| Grass Surface | `Assets/Art/Map/ForestRestart/Source/grass-surface.png` | `0AA5FD7816AFBBA7CFE6011824BBD5AFCB6600A2326842974576130EEA7D4697` |
| Dirt Surface | `Assets/Art/Map/ForestRestart/Source/dirt-surface.png` | `6CAE0930406660A3B6762C72DBC61046DD4589CE1D7741C03956B16BC8D33658` |
| oak | `Assets/Art/Map/ForestRestart/Source/oak.png` | `4F6CBEF5E7D9F48A1A81302D8D815D490DD17D6C69E516166A823B8DCFA77EA1` |
| grove | `Assets/Art/Map/ForestRestart/Source/grove.png` | `EE370EF5890A84A0B2EF0724F6F893537712EB5ED4DBAC190E99D6AA53E87EF0` |
| undergrowth | `Assets/Art/Map/ForestRestart/Source/undergrowth.png` | `D6CFE2C5E167068D099483216C6AF89D1D68844C8A0B061A82398DDA3CAA651B` |
| hero | `Assets/Art/Map/ForestRestart/Source/hero.png` | `2366396DEC20E0D0876E8EEC08A25058C20E24B8A8574FAF47DC7EC6EFB52FDE` |

### 正規化とUnity生成

`ForestRestartAssetBuilder`が元画像をNearest Neighborで正規化し、透明Objectの外周余白と足元位置を揃える。SpriteはPPU32、Point Filter、Compression None、Mipmap Off。ObjectのFoot Pivotはpixel整数位置へ丸める。

| 生成物 | 内容と保存先 |
| --- | --- |
| Sprite76点 | Grass8、Dirt Cardinal-16 x 4差分64、oak / grove / undergrowth / hero4。`Assets/Art/Map/ForestRestart/Sprites/` |
| Tile3点 | GrassVariation、DirtRoad、不可視Collision。`Assets/Data/MapAuthoring/Tiles/ForestRestart/` |
| Prefab4点 | oak128x128px、grove192x128px、undergrowth96x96px、hero32x48px。`Assets/Prefabs/MapAuthoring/ForestRestart/` |
| Scene4点 | Seed20261003は既存Reference Sceneへ保存し、20261004〜20261006は`Assets/Scenes/MapAuthoring/ForestRestart/`へ保存 |
| QA画像8点 | 4SeedそれぞれOverview1536x1024とDetail768x768。`Assets/Art/Map/ForestRestart/QA/seed-{seed}-overview.png` / `-detail.png` |

画像AIがScene全体の地形や通行判定を生成する構成ではない。`ForestRestartLayout`が48x32Cellの地表、曲がる幅3Cell道路、3〜4広場、四辺入口、Spawn、6Event目的地をSeedから生成する。配置用Footprint、表示Bounds、小さい接地Collisionを分け、森林候補の採用ごとに全歩行地の4近傍連結を検査する。Sceneがマップ配置の正本で、JSONはQA結果だけを記録する。

### 検証と品質の限界

- [generation-validation.json](../../../../Assets/Art/Map/ForestRestart/QA/generation-validation.json)は20261003〜20261042の40論理Planを記録し、論理Errorは0。先頭4Seedは保存SceneのValidatorでもError0だった。残る36Seedは`sceneValidated=false`で、Physicsや描画まで確認した扱いにはしない。
- 保存した4Sceneと8画像は手作業による配置修正を挟まず、同じBuilderで生成した。画像QAで調べる範囲は、見た目の反復・前後関係・足元・通路の可読性である。
- 初回QAで草の細かすぎる粒・明るさと矩形に見える道端を検出した。Grass / Dirtを再生成し、露出する道端を最大4pxの起伏へ調整した。道路接続角の透明穴は修正済みで、全Mask / 差分の接続角を実PNG Testで確認した。4全景・拡大画像を目視し、透明合成、足元と通路の可読性を確認した。直交的な道の骨格、同じ部品の反復、水・崖・建物の未制作は残る。
- 旧素材撤去後のUnity Test RunnerはEditMode 98 passed / PlayMode 4 passed、Failed / Skipped 0。実移動・幹衝突と樹冠側通行・Camera追従・Event操作の回帰を確認した。人間による見た目の評価と正式採用、本番生成仕様は未確定。
- [4 Seed比較画像](../../../../Assets/Art/Map/ForestRestart/QA/four-seed-comparison.png)はUnity全景4枚を等倍で並べた3072x2048 PNG。左上20261003、右上20261004、左下20261005、右下20261006。画像AIによる再作画やResamplingは行っていない。
- heroは静止・単方向の縮尺比較用Spriteで、正式Character PrefabやAnimationではない。水・橋・商店・泉の完成表示素材は今回の検証セットへ含めない。
- 本番の固定Map + ランダム配置方針は維持する。48x32とSeedによる地形差分は検証用設定であり、試合ごとの地形生成を採用した扱いにはしない。

旧`Assets/Art/Map/Source/Forest/`、`Production/Forest/`、`Assets/Art/Generated/MapAuthoring/`、旧Forest Tile / Palette / Prefab、旧SceneTemplate / Sampleなど131 Asset Fileを明示削除し、Unityが付随.meta4 Fileを削除した。計135 Fileと19空Directoryを撤去済み。Reference Sceneのpath/GUID、空のAuthoring Template Scene、Event設定、Character / Gameplay基盤、今回の比較コンセプト画像を保持した。旧Builderは互換入口、旧Python Tool / Contract JSONは履歴用に保管する。[削除台帳](../../references/art-reset/retired-forest-files.csv)のDispositionが実際の処置を示す。以下の旧日付の項目は当時の生成・検証履歴として保持し、削除済みpathを現行素材の契約として読み替えない。

## 2026-10-03 A Original Texture Refinement v005

状態: **v004のCharacter頭身を肯定・初版質感候補生成・Agent目視比較・全体の正式採用未定**

- ユーザー指定: 「キャラの頭身はいい感じ、全体的な質感だけ一枚目に寄せる」。「一枚目」は最初のA v001として参照した。
- 生成方法: Codex built-in `image_gen`。v004をEdit Target、A v001を表面の質感と陰影だけの参照として使用した。
- 保存先: `Assets/Art/Concept/style-comparison-2026-10-03/a-classic-adventure-texture-v005.png`。
- 元出力: `C:/Users/umiga/.codex/generated_images/01a0fdf3-547e-71e2-b851-62b2ee5429a9/exec-14a00c1e-5448-4240-8986-fa00f520cf62.png`。
- 出力: 1536x1024 PNG、SHA-256 `C080D93EB8741988F2EED900E21B83042D03FD19FD63F14BEE4359F50C6F65EF`。
- Prompt概要: v004のCharacter頭身、姿勢、外形、配置と各地点の形を維持する。A v001の細かい葉房、意味のある草・土の描写、石の欠けと苔、木目、布、水面の細かな色差・陰影へ戻す。泉の形はv004の石の吐水口を維持する。
- Prompt全文: [generation-manifest.json](../../../../Assets/Art/Concept/style-comparison-2026-10-03/generation-manifest.json)の`refinements`。
- 目視確認: Characterと主要地点の配置・形を保ち、葉・地面・木・石・水面の描き込みが増えた。Pixel単位の同一性は検査していない。
- 採用範囲: ユーザーの肯定はv004のCharacter頭身。最新の全体Artworkの正式採用、Design Masterの置換、Production素材化、Unity・ゲーム縮尺での検証は未実施。

## 2026-10-03 A GBA Refinement v004

状態: **かわいさ調整候補生成・Agent目視比較・正式採用未定**

- ユーザー指定: 「かわいくしすぎかな」「GBAぐらいのイメージ」。
- 生成方法: Codex built-in `image_gen`。v003をEdit Target、v002を材質と造形の補助参照として使用した。
- 保存先: `Assets/Art/Concept/style-comparison-2026-10-03/a-classic-adventure-gba-v004.png`。
- 元出力: `C:/Users/umiga/.codex/generated_images/01a0fdf3-547e-71e2-b851-62b2ee5429a9/exec-ad7797b4-2932-424b-afaa-d4e8c2700a7d.png`。
- 出力: 1536x1024 PNG、SHA-256 `03ACA0C0A78058925E4A47599B87A269B9AAC90E15BCBBF518A1A8F27F6FB9E5`。
- Prompt概要: AのPaletteと粗いドット、低頭身を保つ。Characterは2.5〜3頭身程度の落ち着いた冒険者を指示し、過度に丸い葉・岩・建物を少し角のある形へ寄せる。顔の泉を無表情な石の吐水口へ置き換える。
- Prompt全文: [generation-manifest.json](../../../../Assets/Art/Concept/style-comparison-2026-10-03/generation-manifest.json)の`refinements`。
- 目視確認: 顔の泉が石の吐水口となり、葉と岩に角が戻った。明るい色、主要地点、中央の移動空間は維持した。かわいさの調整量はユーザーと引き続き確認する。
- 扱い: GBA風という画風の方向を試すCandidate。実機の解像度・Palette・描画制約へ適合する素材を意味しない。最新の正式採用、現行Design Masterの置換、Unity・ゲーム縮尺での検証は未実施。

## 2026-10-03 A Chibi World Refinement v003

状態: **低頭身・世界デフォルメ候補生成・Agent目視比較・正式採用未定**

- ユーザー指定: 「もっとキャラの頭身も低くして世界観をデフォルメするイメージかな」。
- 生成方法: Codex built-in `image_gen`でA粗いドット版v002をEdit Targetとして参照し、Characterと環境の両方を描き直した。
- 保存先: `Assets/Art/Concept/style-comparison-2026-10-03/a-classic-adventure-chibi-world-v003.png`。
- 元出力: `C:/Users/umiga/.codex/generated_images/01a0fdf3-547e-71e2-b851-62b2ee5429a9/exec-2a66d3cb-8e95-41ee-b8e6-3a82ab367912.png`。
- 出力: 1536x1024 PNG、SHA-256 `66740ED9267A67F71C30D6B42D1220C7849DDF3E7741F261D6BD8604B056BABB`。
- Prompt概要: 大きい頭と短い胴・手足による2頭身程度のCharacter、丸い葉房と短い幹、太い店の柱・橋脚、大きく単純な岩・花・小物、丸く厚い石の泉へ揃える。Aの鮮やかなPalette、粗いPixel cluster、主要地点と中央の移動空間を維持する。
- Prompt全文: [generation-manifest.json](../../../../Assets/Art/Concept/style-comparison-2026-10-03/generation-manifest.json)の`refinements`。
- 目視確認: v002よりCharacterの頭が大きく手足が短い。環境も輪郭と部材が大ぶりで丸く、低頭身Characterと同じ造形へ寄せられた。
- 扱い: 見た目を相談するCandidate。頭身とSprite寸法はまだProduction規格へ確定していない。現行Design Master / Atlas / Scene参照を維持し、Unity・ゲーム縮尺での検証は未実施。

## 2026-10-03 A Coarse Pixel Refinement v002

状態: **Aを修正基準に選択・粗いドット候補生成・Agent目視比較・正式採用未定**

- ユーザー指定: 「Aをベースにもう少し荒くするイメージかな」。
- 生成方法: Codex built-in `image_gen`でA v001をEdit Targetとして参照し、構図と色を保ちながら描き直した。
- 保存先: `Assets/Art/Concept/style-comparison-2026-10-03/a-classic-adventure-coarse-v002.png`。
- 元出力: `C:/Users/umiga/.codex/generated_images/01a0fdf3-547e-71e2-b851-62b2ee5429a9/exec-a2cb5251-d26e-419f-94ba-45b0095d6bc5.png`。
- 出力: 1536x1024 PNG、SHA-256 `30416FE8F8224E7A0E5CE3E37BBB0050AF8971E6DDA9D6C786D95F0EC1D4602D`。
- Prompt概要: 仮想384x256を4倍に拡大するような粗いPixel clusterを指示した。葉、草、石、水面、木目、小物の細線をまとめ、鮮やかな黄緑・青緑・暖色の土、左上光源、川・橋・店・泉・主人公の主要配置を維持する。ぼかしや単純なフィルター処理は使わない。
- Prompt全文: [generation-manifest.json](../../../../Assets/Art/Concept/style-comparison-2026-10-03/generation-manifest.json)の`refinements`。
- 目視確認: A v001より葉・地面・水面のPixel clusterが大きく、細かな粒が減った。中央の移動空間と主人公は読み取れる。Characterと店には背景より細かい描き込みが残る。
- 扱い: ユーザーはAを修正の基準に選択したが、このv002の最終採用は未定。Prompt上の仮想解像度は作画指示であり、正確な4倍Pixel grid適合は検査していない。Unity・Production Atlasへの組込みとゲーム縮尺での検証は未実施。

## 2026-10-03 Concept Style Comparison

状態: **比較候補3点生成・Agent目視確認・正式採用未定・Scene未組込**

- 用途: デザインを固めて素材制作へ進むため、森林の小広場を3種類の画風で比較する。
- 生成日: 2026-10-03。
- 生成方法: Codex built-in `image_gen`。各案を独立したPromptで1回ずつ生成した。
- 共通指定: 見下ろし寄りの3/4視点、森林の分岐広場、土道と石畳、水辺と木橋、草付き崖、商人の小屋、回復の泉、小さな仮冒険者。中央の移動・戦闘空間を読みやすくする。左上光源、Pixel表現、文字・HUDなし。
- 比較軸: Aは鮮やかな王道16bit、Bは丸みと暖かさ、Cは青緑と古い石造物による神秘性。
- 出力: 全3点が1536x1024 PNG。Project保存画像のSHA-256を確認した。
- Prompt全文と元出力: [generation-manifest.json](../../../../Assets/Art/Concept/style-comparison-2026-10-03/generation-manifest.json)。
- 目視確認: 地形境界、樹冠・幹・接地影、中央の空間、仮冒険者の視認性、3案の色と形の差を確認した。ユーザーはAを修正の基準に選択し、上記v002で粗いドット表現を比較する。
- 制約: 共通Scene motifを指定したが、配置の厳密一致はしていない。3/4の奥行きが強めであり、Production素材化の前に既存のトップダウン投影、32px基準、足元Pivot、地形接続、照明を再確認する。画像内のCharacter、商人、泉、遺跡の形は見た目の候補であり、仕様採用を表さない。
- 扱い: デザイン比較用Candidate。現行Art Direction、変更不可のForest Design Master、Production Atlas、Scene / Prefab参照は変更していない。ユーザーの選定後にPaletteと形を絞り、最初の草地・道・木・小物から素材化する。UnityのImport、ゲーム縮尺での確認、接続検証は未実施。

| 候補 | Workspace保存先 | SHA-256 | Prompt概要 |
| --- | --- | --- | --- |
| A 王道16bit | `Assets/Art/Concept/style-comparison-2026-10-03/a-classic-adventure-v001.png` | `309D43946E165B9932A43960666E6EB8088FFF325DBC74BA8AFE5993A855FF50` | 鮮やかな黄緑と深緑、青緑の水、暖色の土。細かい葉房と陰影による王道16bit冒険表現。 |
| B 絵本風 | `Assets/Art/Concept/style-comparison-2026-10-03/b-storybook-grove-v001.png` | `FFAE8E9E8A28E9CFC3F5C65F7F3B9352D5F5D4696901EE93BDBC5E19059C95CE` | 丸い樹冠と小物、暖色の光、Sage / Mint系の緑、Terracottaの店。親しみやすさと広場の見やすさを重視。 |
| C 神秘的な冒険 | `Assets/Art/Concept/style-comparison-2026-10-03/c-enchanted-ruins-v001.png` | `F226C76EEE5E7617BA3494BEF1FFE25464FFB5BDF5C090FC628A4FD3CD2EB4D2` | 青緑の森林、苔むした石造物、光る回復の泉、控えめな紫の花。古い遺跡と探索の雰囲気を重視。 |

| 候補 | Built-in imagegen元出力 |
| --- | --- |
| A 王道16bit | `C:/Users/umiga/.codex/generated_images/01a0fdf3-547e-71e2-b851-62b2ee5429a9/exec-0a39c7cb-2c39-40f0-817c-70009b0852d7.png` |
| B 絵本風 | `C:/Users/umiga/.codex/generated_images/01a0fdf3-547e-71e2-b851-62b2ee5429a9/exec-4d365c13-c998-4ce1-b990-76832b4896e9.png` |
| C 神秘的な冒険 | `C:/Users/umiga/.codex/generated_images/01a0fdf3-547e-71e2-b851-62b2ee5429a9/exec-2a294a94-7fdf-4213-9e05-93776ba672af.png` |

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

## 2026-10-04 A画風の木1本と限定地表pilot

状態: **供給候補の正規受領・原本Hash・Native保存・通常画像・所有・限定地表・前後表示・有限移動と独立監査の技術PASS、美術の最終レビュー待ち、方向承認のみ、全素材の正式採用未承認**。

- 用途: 既存2ブロックPairの西側 `nw_main` 1本と足元の限定地表で、A参照に寄せた着色、接地と通常Cameraでのピクセルの粗さを比較する。14本へ展開しない。
- 供給受領日: 2026-10-04。生成方法は供給側のCodex built-in `image_gen`。正確な生成日時は供給資料に記載がない。HomePCでは新しい作画・着色・sampleを行わず、提供された最終PNG bytesを保持する。
- 元package: `FantasyRoyale_ConceptStyle_Pilot_v1_OFFLINE.zip`、Library `libfile_e3a1c04fb15881918581d60372f7058b`、5,060,901B、SHA256 `2e5c04e488117ff68d67fb40e11ab841abe7915db9e803f1b54f1943d8706b02`。公式WSL経路で受領し、CRC・23file安全展開・Windows/WSLのbytes／SHA／Library属性を確認した。
- Repo Source: `Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/ConceptStylePilot-v1-20261004-181826149/Sources/`。入力workspace `style-pilot-input-20261004/extracted/runtime/` と同じ最終PNG bytesを新GUIDで保存する。供給[manifest](../../../../Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/ConceptStylePilot-v1-20261004-181826149/style_pilot_manifest.json)はSHA256 `d99cd421ab4f694289aa93ace2e013f6924956d572ddf3b251f17049cfbef892`。

| 使用Source | 原本canvas px | SHA256 |
| --- | --- | --- |
| `canopy.png` | 256×256 | `a47d0f70d4d2415d18a5d8481af09243b610aac57134020dd630a54cd017d62f` |
| `trunk.png` | 100×132 | `24608bed38cabf8b8d12a94abe33876261230aca471bcc02fe4e2a4b443ff0a8` |
| `owned_shadow.png` | 128×44 | `33ae476ff05b8839fb2eda1056ca2fa4db3fd6702294f8134b3d6fe90a63a819` |
| `contact_vegetation.png` | 128×48 | `a7898615673e87b28d2842e68047bec670999f86bbc7bdf3ec8aa50c088c07d3` |
| `clean_ground_patch.png` | 512×512 | `971699b01118d351425aa68dc3aadb80d188f9f0f893abd39cf49a81df7a5542` |

Prompt全文は新rootのProvenance配下へ原本と同じbytesで保存した。[tree](../../../../Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/ConceptStylePilot-v1-20261004-181826149/Provenance/tree_prompt.txt)はactual A concept cropと草／土paletteを画風参照とし、旧canopy／trunkは置換対象として示す。[ground](../../../../Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/ConceptStylePilot-v1-20261004-181826149/Provenance/ground_prompt.txt)はactual A concept cropと新tree partsを画風参照とし、treeとは別の完全なground underlayを生成する指定。[grass refinement](../../../../Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/ConceptStylePilot-v1-20261004-181826149/Provenance/grass_refinement_prompt.txt)は512×512groundを編集し、草だけを短い低contrastの筆致へ寄せて土の全pixelを維持する指定である。供給READMEも同Provenanceへ保存した。生成reference cropの正確なfile identity、model／generation IDは供給資料にないため推測しない。

供給後処理は部品抽出、nearest-neighbor寸法正規化、alpha閾値128、offlineの独立layer合成。これは最終package作成時の来歴であり、今回のHomePC importで再実行しない。元offline QAの `engine_verified=false`／`user_approved=false`、binary alpha・ground完全opaque・soil不変・grass変更156,405px・canopy16px上昇の記録を保持する。offline tree removal差32,192pxをUnityの証明へ流用しない。

全Spriteはcenter pivot `(0.5,0.5)`／64PPU／Point／FullRect／Mipmapなし／非圧縮RGBA32／sRGB／physics outline生成なし。木の親scale0.87、canopy child1／localY2.1623563218390807、trunk child0.75／localY0.75、shadow child1.0574712643678161／localXY `(0.015804597701149472,-0.0402298850574713)`／opacity0.36、vegetation child0.9770114942528736／localXY `(0.007183908045976986,-0.09698275862068963)` を固定する。trunk／canopyは共通foot sort、影・vegetationは木ownerに所有され、旧contact2個はOFF、新vegetation1個だけを表示する。`style-pilot-runtime-20261004/native-style-build-third-report.json` は5PNG原本SHA・Map／入口の保存readback・他31owner／source Prefab links／geometry／旧Scene／PlayerSettingsの保持を確認する。生成時の実歩行・所有操作・source-alpha・実画素レビューfalseは元報告に残し、後続の実画像と実挙動の証拠を分ける。

地表は原本512×512px／8×8unitを伸ばさず、原TextureのsourceRect `[128,0,304,512]` を `Sprite.Create` で参照し、WorldRect `[0,1,4.75,8]`／center `(2.375,5)`／scale1／MapGround order1で限定表示する。両Rectは `[x,y,width,height]`、sourceのXは `[128,432)`、WorldはX `[0,4.75)`／Y `[1,9)`。材質比較用であり、tileability・全地形置換・geometry採用は承認されていない。未使用の128×128swatch2点は供給資料として保持し、groundへ反復配置しない。

64PPUは幾何尺度で、描いたクラスターの粗さや厳密な共通2×2／4×4gridを保証しない。1024px高／ortho8で1source texelはcanopy0.87、trunk0.6525、shadow0.92、vegetation0.85、ground1screen px。緑5〜7／茶3〜4のvalue familiesも知覚上の目標で、厳密indexed paletteの主張ではない。通常Cameraの実静止／移動画面で評価し、ユーザーの方向承認を完成品質の承認に広げない。

画風contractのsemantic read-exportは6,432B／SHA256 `4635d7665df6ef9c553726df6b687ae7e27b4f9beba698eb5835303c655507e1`。Library報告は6,433Bでsource exact bytes未確認。新rootの `asset-style-contract-read-export.txt` に保存し、画像／ZIPの正規受領と区別する。

新Map／通常入口は `ConceptStylePilot-v1-20261004-181826149`。実Camera PNGはworkspace `style-pilot-proof-20261004/normal-style-minimal-first-1536x1024.png`、1,917,792B／SHA256 `f37fd4f383431a1d7d0c94a59c13a174d9c2229a859cbe7a330515630ecbd811`。camera `(9,5,-10)`／ortho8／Capture aspect1.5／Hero足 `(.65,4.640625)`、targetTextureとlive aspect1.77777779への復元を確認し、Rootと独立担当が実画素を閲覧した。地表x4.75の境界は見えており、完成artの承認ではない。

新しい `phase-native-owner-first.json` はoriginal-materialの4raw f32 alpha>0 union32,113px、通常ON/OFF差31,149px・支持外0、復元bytes／float完全一致、実所有操作によるnav25→24→25を確認する。全6renderer中4active、旧contacts2個OFFとcounter0→2を保持。独立Owner83照合／失敗0。`phase-native-ground-first.json` は限定地表の差52,955px・crop外0・元water／bank／bridge GPU支持上0・復元一致、独立Ground40照合／失敗0。7guardのraw f32はNative report由来、display alphaとRGBA／semantic waterの照合は独立実施と区別する。前後poseは実OS WASDで到達しactual PNG／same-pose controlsを保存し、独立Depth93照合／失敗0。北側の不透明重なり1,506pxのうちHero寄与がcontextで見える1,393pxで遮蔽を確認し、残る113pxを確定判定から除く。全Hero不可視とは主張しない。

`phase-native-moving-first.json` は実Win32 Dキー入力での5frame観測、入力保持1frame／Body変位2frame、32owner・counter2・nav25・static287・固定camera・自然進行する水の保護を確認する。元PNG5枚を正本とし、lossless APNG `phase-native-moving-first-lossless-apng.png`（3,193,048B／SHA256 `a188048ad0fb98db996115d26ada63169f68062d3a2d54fdbcc8ad1b68521421`）は全frame decoded RGBA一致の閲覧用である。開始間隔を丸めたduration671／667／663／678／537msであり、連続real-time動画やpan／zoom shimmer試験ではない。独立 `actual-phase-moving-cleanup-first-independent-audit.json` は96照合／失敗0。各frameのHero不透明支持1,688pxの元Source RGBA不一致0、隣接差4,966／5,098／1,585／1,406pxのsemantic water＋両Hero支持外0、OS／owner／nav／static／cleanup／APNGを確認した。証跡はworkspace `style-pilot-proof-20261004/` と `style-pilot-independent-native-review-20261004/`。以前のGPU／CPU判定は凍結し、新候補の証明として転用しない。


## 2026-10-04 A画風の代表木1本のラスタ登録派生

状態: **新Scene保存・実登録／通常表示／所有復元・64px有限位相の技術検証済み。FullHDのfractional phaseには内部pattern差が残る。美術の最終承認・全素材採用は未確定。**

用途は既存A画風pilotの `nw_main` 1本を実効world scale1／64PPUへ登録する別実験。元の生成出典・Promptは前節と新rootのProvenanceに原本bytesで保持する。新しいImageGen呼出、作画・着色は行わず、供給最終4PNGをPillow12.2.0 `Image.resize(target_dimensions, Image.Resampling.NEAREST)` で一度だけ指定寸法へ変換した。binary alphaを保持し、alpha閾値／shadow opacity焼込みなし。全派生RGBA色がsourceに含まれ、元source bytesは不変。sourceと派生の寸法・SHA・RGBA SHA・opaque bboxは [派生manifest](../../../../Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/RasterRegistration-v1-20261004-201454860/raster-bake-manifest.json)（SHA256 `867151675e9a4f04be5a8ee1c9fa101619a7ae11d73ac97d05107d2748a49848`）に保存する。

新Source root: `Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/RasterRegistration-v1-20261004-201454860/Sources/`。

| 部品 | 派生canvas px | pivot px（左下原点） | 足からのoffset px | tint alpha |
| --- | --- | --- | --- | ---: |
| canopy | 223×223 | `(111,111)` | `(0,120)` | 1 |
| trunk | 65×86 | `(32,43)` | `(0,42)` | 1 |
| owned shadow | 118×40 | `(59,20)` | `(1,-2)` | 0.36 |
| contact vegetation | 109×41 | `(54,20)` | `(0,-6)` | 1 |

| 派生Source | SHA256 |
| --- | --- |
| `canopy.png` | `fb87f55cf9082cb119b0c427eb69ba9f712f9ac67a364801140f65e2ad789cfb` |
| `trunk.png` | `baca1a4bbf2749656e29ffbcbd880c7a3b712e77fa029310568eec419ed94d62` |
| `owned_shadow.png` | `39a7996d67e26a9bb484f72ced92374f6725b4367b5f074bbbd6d30959f8ce85` |
| `contact_vegetation.png` | `ab996a7d2c2ff912d392d8cc828cc29f7d6f158844ac605a5d567361132457c8` |


全派生Spriteは64PPU／Point／sRGB／非圧縮RGBA32／Mipmapなし／FullRect／physics outlineなし。custom normalized pivotをimportし、Native保存reopen／実Playで表の整数pixel pivot、実効world matrix1、world mesh corner×64の整数一致を確認した。owner0.87／足 `(2.03125,5.953125)`／Body/navは維持し、所有子孫のinverse carrier `PresentOwned/RasterRegistrationCarrier` とleaf scale1で描画だけを相殺する。旧6Rendererをdisabledで保持、新4active。canopy／trunkは共通足sort、shadowはRenderer tint0.36を一度だけ適用する。旧地表cropと他31owner・Prefab links・Runtime4／Camera・ProjectSettingsはNative readbackで保持した。

選定Map／入口は `RasterRegistration-v1-20261004-201454860`。通常 [入口](../../../../Assets/Scenes/MapAuthoring/OverheadStudyA/StudyARasterRegistrationNormalPlay-RasterRegistration-v1-20261004-201454860.unity) を開く→Playで再現する。固定recipeでseed引数なし。Rootと独立担当が実通常1536×1024／FullHD1920×1080を確認し、画風と近いfootprintを維持する。旧／新RGBA差18,138px、isolated display-alpha bbox境界の最大変化1px。新canopy不透明28,367pxと露出trunk806px／vegetation1,820pxの通常source RGBA不一致0（blendされたshadowを除く）。新original-material alpha union32,213px、実owner OFF差32,211px／支持外0、復元完全一致、nav25→24→25／counter+2保持。独立通常・所有72照合PASS。

64screen px/unitでx／y各1・.25・.5・.75pixelを実GPU採取し、新assemblyの最良整数位置合わせ後のRGBA／alpha支持／Native float残差0。旧のfractionalでは内部差が残る。FullHDは67.5screen px/unit＝1.0546875screen px/texelで、指定x0.5の実位相0.499985218では新にもRGBA14,057px／alpha支持417px／Native float16,546pxの残差が残る。指定x1の実位相0.999970436で両assemblyの残差0は有限観測として記録する。FullHDのshimmer解消は未達。全10caseのcapture／厳密restoration、独立位相177照合PASSを全美術・連続動作の承認へ広げない。 最終限定独立freezeは6監査・369照合PASSで、以前のfail報告を保持する。実数はworkspace `raster-registration-proof-20261004/actual-ten-phase-table.json`、監査は `raster-registration-independent-20261004/actual-native-ten-camera-phases-independent-audit.json`。full phase RGBAFloatは未exportのためfloat値はNative report、PNG比較は同linear-targetの表示RGBA8同士だけで行う。

代表木1本の登録・有限位相・所有の検証まで。描いたgrainの完全一致、厳密な共通2×2grid、美術の最終承認、連続pan／zoomの知覚評価、全14本／全地形／汎用mapgeneratorの完了は主張しない。 地表sourceX432／worldX4.75の硬い境界は元pilotの人工cropでありtile seamと呼ばない。地表と周囲の旧素材の細かなgrainは今回変更しない。今回backup manifestはSHA256 `914c51edc88db8592cf0b47dad3932136c5bdbcf6c0e579903209fddffbd2783`／ZIP1,315entry、台帳の元61,792B全文prefixを保持して追記する。再生成時は現source／入力のfresh backupを作り、凍結builderを未使用の別helperへcopyして `ApprovedBackupSHA` と固定件数1,315の照合をそのbackupの実SHA／実件数へ更新する。source／bake pinと各保全条件は維持し、新helperをレビュー→SHA freeze→compileしてfresh root／Scene／reportへ実行する。引数のmanifest／SHAだけを差し替えて旧builderを再Runしたり、guardを緩めて通したりしない。 追記後のKnowledge／保全はRootの別closureへ記録予定で、未実行PASSを予告しない。

## 2026-10-04: Isolated Style Family v2

用途は疎な3本のStyle検証。保存先は`Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/IsolatedStyleFamily-v2-20261004-215942621/Sources/`。新規13PNGをPoint/64PPU/RGBA32/sRGB、mip/compressionなし、FullRect/physics outline0で導入した。grass/soil swatchは比較素材でseamless認定なし。

冠3枚は223²、幹65×86、影118×40（opacity .36）、接触草109×41。中央pivotの半pixel中心offsetを使用し、art親子scale1で整数top-leftを保持。アンカー4素材のdecoded RGBAは前回bakeと完全一致し、PNG圧縮bytesのみ異なる。幹の配置は旧bakeより1px左を契約通り採用。岩96×64/葦64×80は独立した足元sort proxyを持つ非blocking所有群。

clean_ground/bank2枚は各768×640。岸alphaは実既存v3と差0。RGBが変わる可視画素は西26,127/東25,775、RGBのみ変更。既存water/base/mask/64frames、橋3層とgeometryはsource bytesを維持し、新しい背景合成にはしない。5owners/22renderers/7ownedNav/static287の実Sceneを生成した。

受領README記載の生成方法はproducerのimagegenによる冠/材料/岸小物と限定的な葉・岸RGB修正、nearestで一度だけ最終canvas化、岸alphaを原本へ復元。prompt原文は95ファイルの受領packageにない。README/ContractsをProvenanceへ原bytesで保存し、promptを推測補完しない。

受領ZIP SHA256 `1806cfea2cb83978b285a21b9095ca04ebc2a80b2f9ceda4fdf4a7cfcc02c20d`。grain contract SHA256 `5b3b33fbfcba23f1b2ce652b9119d1e009732524540ea849dda7378150e6b05d`。個別source hashは保存ProvenanceのSHA256SUMSとNative build report。自己行だけstale、他94項目は一致。実表示/所有制御と実Heroの5足元×前後10方向は有限PASS。既存14本・camera policyの変更、最終art承認は含まない。
