# 森林素材再制作Kit

## 現在の構成改善

2026-10-03の引き継ぎ依頼に合わせ、森林部品の散布だけだった検証Kitを、中央広場、曲がる周回路、水路と橋、段丘、森縁を組み合わせる制作パイプラインへ拡張する。少量の共通部品をSeedから配置し、通行検査の後にUnity Sceneと実描画画像を保存する。[構成改善ExecPlan](../../exec-plans/active/forest-composition-improvement.md)へ進捗と最終検証を記録する。

状態: **代表4Seedの実Unity生成・Scene/Physics検査・Reference移行・EditMode113件/PlayMode4件・実Gameplay歩行確認済み**。指定画像との最終比較と残る見た目の改善は継続中で、本番の毎試合ランダム地形生成は確定していない。

指定されたLibrary画像2枚はHomePCの正規受取り経路で取得を試みたが、通常Downloadの失敗と限定再試行時のWindows拡張属性互換性エラーにより、ローカル化を完了できなかった。このPCでは指定画像の実ピクセルを確認できていない。既存v005コンセプトは様式・密度の補助参照として確認したが、指定画像1とはByte数が異なるため同一画像と扱わず、最終的な見た目の一致判断は保留する。画像中の店や泉を必須ゲーム要件として追加しない。

### 生成規則とSceneの境界

`ForestRestartLayout.Generate(seed, includeLandmarkCandidates)`は48x32Cellを維持し、中央の不規則な広場、短い曲がりを連ねる周回路、左側の水路と2本の橋、通行を制限する段丘、地形縁へ寄せた森林のまとまりを生成する。全歩行Cell、四辺の入口、Spawn、6Event目的地を4近傍経路で検査し、部品のFootprintと実際の接地Collisionを分ける。

水路は`WaterCells`として橋の下まで連続させる。`BridgeCells`だけを歩行可能にし、橋以外の水域、段丘の`CliffCells`と外周を不可視CollisionTilemapへ保存する。木と任意のランドマーク候補は既存方式の`CollisionBody`で接地部分を塞ぐ。Sceneに保存した`MapCollision`が移動判定の正本で、Runtimeへ別のMap Serializerを追加しない。

新しい比較SceneはGround / Water / Terrain / Collision / Foregroundの5Tilemapを持つ。Waterは`MapGround` / Order5、道は同Order10へ置き、橋の歩行面は`MapDetail`へ置いてCharacterより背面に描画する。立体部品は従来どおり`WorldObjects` / Order0 / 足元PivotとCustom Y Axisで前後関係を保つ。Transformは1/32unit Snap、Rotation0、Scale1とする。

### 追加素材と保存先

既存76Sprite、Prefab4点と従来QA画像を保持し、追加素材と結果は別Pathへ保存する。初回はSibling Sceneで比較した後、Seed20261003の修正済み構成を既存Reference Sceneへ反映した。ReferenceのPath/GUIDは維持し、旧Sceneを復元できるバックアップを作成済みである。Source7点はbridge、cliff、shop、fountain、water、cobbles、verge。生成PromptとSourceのHashは[Composition manifest](../../../../Assets/Art/Map/ForestRestart/Composition/Source/generation-manifest.json)を正とし、[生成Asset台帳](../../development/assets/generated-assets.md)にも用途を記録する。

| 内容 | 保存先 |
| --- | --- |
| Source7点と生成記録 | `Assets/Art/Map/ForestRestart/Composition/Source/` |
| 正規化Sprite20点 | 同Composition Rootの`Sprites/`。表示部品6点、崖の幅1〜6Cellの切り出し6点、水面8差分 |
| 水面Tile | `Assets/Data/MapAuthoring/Tiles/ForestRestart/Composition/WaterVariation.asset` |
| Editor設定 | `Assets/Art/Map/ForestRestart/Composition/ForestCompositionSettings.asset` |
| 比較Scene | `Assets/Scenes/MapAuthoring/ForestRestart/ForestCompositionSeed{seed}.unity` |
| 探索Previewが使用するMap | `Assets/Scenes/MapAuthoring/GbaForestBattleRoyaleReference.unity`。Seed20261003を同Path/GUIDへ反映 |
| 実描画と検査記録 | `Assets/Art/Map/ForestRestart/QA/composition-seed-{seed}-*.png`と`-validation.json` |

店と泉は`includeLandmarkCandidates`で切り替える構図候補で、商取引、回復、Event Handlerとの接続は持たない。既存Gameplayの回復Eventや操作Playerを置き換えない。`ConceptScaleHero`は従来どおり縮尺比較用である。

### 再現と検証の扱い

設定Assetの`seed`、`focusOrthographicSize`、`includeLandmarkCandidates`を保存し、Menuの`FantasyRoyale/Map Authoring/Generate Configured Forest Composition`を実行する。既定値はSeed20261003、保存SceneのCamera Size8.5、ランドマーク候補あり。CLI手順は[Build・Test手順](../../development/testing.md)を参照する。Sceneを手編集した場合、同Seedの再生成はその比較Sceneを組み直すため、未保存変更を保存または別Sceneへ退避してから実行する。

QAは全景、構図比較、ゲーム縮尺、Spawn付近の拡大、橋上の比較Characterをそれぞれ実Unity Cameraで撮影する。全景・構図比較・ゲーム縮尺は1536x1024、拡大は768x768、橋上は1024x768。保存SceneのCamera倍率設定とQA撮影の倍率は別で、現行QAは全景16、構図比較9、ゲーム縮尺6、拡大6、橋上4を使う。AIでScene画像を再作画しない。

Seed20261003〜20261006を実生成し、全4Seedで論理検査と実Scene Validatorが成功した。各Seedの5画像、計20画像を保存し、修正後の全景、橋上、ゲーム縮尺の実ピクセルを確認した。橋Deckの表示幅と崖Frontの幅を修正し、Physics検査でも各Seedの橋を除く水域90Cell、橋21Cell、崖、全歩行領域、木の接地点を確認してError0だった。Reference移行後のUnity Test RunnerはEditMode113 / 113、PlayMode4 / 4成功、Failed / Skipped / Inconclusiveは0。スタンドアロン27論理Test CaseとUnityのScene/Physics検査を区別し、従来の98/4成功を今回の変更後の結果として流用しない。

探索Previewは従来のReference PathをAdditiveで読み、新しいSeed20261003の地形でReadyと失敗Message空を確認した。既存の青緑Debug Markerを操作し、位置(24.5,13.5)から(37.1,13.5)への移動とCamera追従を確認した。実HUDを含む画面Capture2枚を`composition-runtime-start.png` / `composition-runtime-after-move.png`へ保存し、実ピクセルを確認した。静止した`ConceptScaleHero`は操作Playerとは別で、正式Character表示へ採用変更していない。

検証集計は[composition-verification.json](../../../../Assets/Art/Map/ForestRestart/QA/composition-verification.json)。4SeedのHash、実Physics、Test結果、生成20画像とRuntime2画像の計22PNGのHash、Library保存結果を記録する。既存Character/Event基盤と素材GUIDを維持し、終了後はPlay停止、元の保存済みTemplate SceneとEditor実行設定へ復元した。Console対象区間でErrorは0だった。

道と芝の階段状輪郭、水岸の薄さ、上段の段差が森に隠れる箇所は残る。実装と動作検査の成功を目標画像に対する見た目の達成と区別し、指定画像との最終一致評価は保留する。

## 最初のForestRestart検証の記録

以下は構成改善前の素材制作、40論理Plan、代表4Sceneと旧素材整理の履歴である。記載された数量・画像・成功数は初回Kitの証跡として保持する。

2026-10-03の依頼に従い、最新コンセプトv005を素材制作の基準として、森林・道・広場の少量の部品から複数の配置を自動生成する。旧v4.3素材は参照を切り替えた後に撤去する。

状態: **素材制作・40 Seed論理検証・4 Scene描画・Unity回帰検証・旧素材撤去完了**。本番ランダム地形の採用や、新素材全点の正式採用を意味しない。

基準画像: `Assets/Art/Concept/style-comparison-2026-10-03/a-classic-adventure-texture-v005.png`。
Characterの低頭身、初版Aに近い葉・草・石・木材の質感を引き継ぐ。最初の範囲は草地、土道、単木、2本の木の塊、岩と下草、比較用Characterである。水辺、崖、橋、店、泉の表示素材は今回の小規模Kitに含めない。

## 保存先と正本

| 内容 | 保存先 |
| --- | --- |
| imagegen元画像6点 | `Assets/Art/Map/ForestRestart/Source/` |
| 生成Prompt・元画像Path・SHA-256 | 同Rootの`generation-manifest.json` |
| 表示用PNG | `Assets/Art/Map/ForestRestart/Sprites/` |
| Tile Assets | `Assets/Data/MapAuthoring/Tiles/ForestRestart/` |
| Sprite / Collision Prefabs | `Assets/Prefabs/MapAuthoring/ForestRestart/` |
| 最初の配置Scene | `Assets/Scenes/MapAuthoring/GbaForestBattleRoyaleReference.unity` |
| その他の配置Scene | `Assets/Scenes/MapAuthoring/ForestRestart/` |
| Unityの全景・拡大画像、Seed検証結果 | `Assets/Art/Map/ForestRestart/QA/` |

配置の正本はUnity Scene、移動Physicsの正本は`MapCollision` Layerの外周CollisionTilemapと各部品の接地CollisionBodyである。JSONは素材生成記録とQA結果であり、Runtime用の別Map SerializerやSceneとの二重管理を導入しない。

## 素材の加工と配置

- imagegenを使用し、v005を参照して6点の候補画像を制作する。元画像は保存して加工結果と区別する。
- Codeによる加工はnearestの規格化、透明本体のBounding Box、alphaの0/255化、Tileの共通接続境界とPackingに限定する。色変更、回転、反転で差分を増やさない。
- 地面と道は32px Tile、PPU 32。Point、Mipmapsなし、非圧縮、Full Rectとする。
- 草8差分、道路16 Cardinal Mask×4差分、立体表示4点の計76 PNGを生成する。草差分の対向辺と道路接続辺の色を共有し、道路の2px接続Collarと20px Socketを固定する。露出辺は最大4pxの不規則な起伏を持たせる。
- 単木128x128、森林塊192x128、岩と下草96x96、Character32x48へaspectを維持して規格化する。足元Pivotを整数pixelへ揃え、Transformを1/32unitへSnapする。
- 立体表示は`WorldObjects` / Order 0 / 足元Pivot、既存Renderer2DのCustom Y Axisでソートする。地面と道は`MapGround`へ置く。
- 配置用Footprintは広めの幹・足元の占有域、Physics Colliderはさらに小さい接地範囲とする。樹冠全体は通行判定にせず、枝葉の通路への張り出しを許容する。
- 比較用Characterは素材の大きさ・前後関係の確認用Prefabであり、Gameplayの正式Character表示へは採用しない。

## 自動配置と検査

`ForestRestartLayout.Generate(seed)`が48x32の一時Planを作る。3〜4広場、幅3の曲がる道、四辺の入口、Spawn、6 Event目的地を配置し、道路と目的地を保護したうえで森林部品を散布する。

全歩行Cellが4近傍の単一成分へ接続する候補のみ採用する。Planと実Sceneの責務を分け、PlanのCollisionRectをSceneの接地Colliderへ直接反映する。

`ForestRestartAssetBuilder.BuildCompleteKit()`は40 SeedのPlan検査と、代表4 Seed（20261003〜20261006）のScene作成・実Scene検査・Unity撮影を行う。Scene未生成のSeedはQAで明示し、40 Sceneを描画したとは扱わない。

検査対象:

- 同Seedの再現性、異Seedの道路・広場・障害物配置の差。
- 道路連結、全歩行領域・入口・Spawn・Event目的地の到達可能性。
- Footprint重複、道路保護、範囲外、接地Collisionとの包含関係。
- 実PNGのalpha、道路Socket、地面の対向辺、Import・Pivot。
- 実Sceneの標準Hierarchy、Collision分離、描画順、Event設定。
- 撮影時は他SceneのRendererを一時抑制し、別Mapを画像へ混入させない。

一時Sceneで構築・検査してから保存先へ反映する。未保存の対象Sceneは上書きせず、例外時は開いている別SceneのRendererやActive Sceneを復元する。

## 旧素材の整理

旧Source、Production Atlas、生成素材とQA、Forest Tile / Palette / Prefab、不要なSample SceneとScene Templateの131 Asset Fileを明示削除し、Unityが付随.meta4 Fileも削除した。合計135 Fileと空になった19 Directoryを撤去した。[削除一覧](../../references/art-reset/retired-forest-files.csv)へPath・Hash・Dispositionを記録する。旧Python Atlas ToolとContract JSONは履歴用に保管し、現行生成では使用しない。

削除前に残存Assets / ProjectSettingsへの旧GUID・Path参照0件を確認した。元の135 FileをZIPへ保存し、全EntryのByte数・SHA-256一致を確認した。自動承認レビューの拒否を受け、互換Builderと実行Toolの削除を外した素材Assetのみの範囲で実施した。

Reference SceneのPathとGUID、Character / Event基盤、Event Catalog / Pool、InputActions、URP / Layersは保持する。旧Builderの呼出し入口は新Builderへ委譲する短い互換コードとする。

復元用控えは`Logs/ArtReset/retired-forest-2026-10-03.zip`。これはGit対象外の作業用控えである。旧素材の履歴はGitにも残る。過去のAtlas仕様Documentは履歴として残し、現行契約へ適用しない。

当初台帳から漏れた4付随.metaはGit HEADのBlobを`Logs/ArtReset/retired-forest-sidecars-2026-10-03.zip`へ保存し、台帳へ追記した。削除直前のWorking Tree byteを取得したものではない。これら4 GUIDも撤去後の参照0件を確認した。

## 検証結果と見た目の比較

2026-10-03: 40論理Planすべて成功、Layout Hashは40種類。代表4 SceneのValidatorは全件Errorなし。撤去後のUnity Test RunnerはEditMode 98 passed、PlayMode 4 passed、Failed / Skippedとも0。

[4 Seed比較画像](../../../../Assets/Art/Map/ForestRestart/QA/four-seed-comparison.png)は左上から20261003、20261004、20261005、20261006。Unityの4全景を等倍で並べ、画像AIで再作画していない。[検証集計](../../../../Assets/Art/Map/ForestRestart/QA/verification-summary.json)に結果と画像Hashを保存した。

初回の細かすぎる草粒を検出し、Grass / Dirtをimagegenで再生成した。道路の透明な角穴を修正し、全Mask / 差分の接続角を実PNG Testで確認した。画像QAでは4全景と拡大画像を確認し、素材合成、足元、通路の読み取りが成立した。道の骨格はまだ直交的で、同じ木・下草の反復が見える。コンセプトの密な森林縁や水・崖・店を含む完成風景の再現まで検証したものではない。

| Seed | 障害物 | 道路Cell | 到達可能Cell | 全景 | 拡大 |
| --- | --- | --- | --- | --- | --- |
| 20261003 | 56 | 435 | 1149 | [画像](../../../../Assets/Art/Map/ForestRestart/QA/seed-20261003-overview.png) | [画像](../../../../Assets/Art/Map/ForestRestart/QA/seed-20261003-detail.png) |
| 20261004 | 55 | 424 | 1144 | [画像](../../../../Assets/Art/Map/ForestRestart/QA/seed-20261004-overview.png) | [画像](../../../../Assets/Art/Map/ForestRestart/QA/seed-20261004-detail.png) |
| 20261005 | 65 | 394 | 1115 | [画像](../../../../Assets/Art/Map/ForestRestart/QA/seed-20261005-overview.png) | [画像](../../../../Assets/Art/Map/ForestRestart/QA/seed-20261005-detail.png) |
| 20261006 | 67 | 353 | 1088 | [画像](../../../../Assets/Art/Map/ForestRestart/QA/seed-20261006-overview.png) | [画像](../../../../Assets/Art/Map/ForestRestart/QA/seed-20261006-detail.png) |

この検証は少量の共通素材から手配置修正なしで複数Mapを組めることを確認した。見た目の正式採用とゲームの楽しさは別の判断とする。

関連: [Map Plan](../../product-specs/art/map-plan.md)、[Asset Workflow](../../development/asset-workflow.md)、[実行計画](../../exec-plans/completed/forest-art-restart.md)。
