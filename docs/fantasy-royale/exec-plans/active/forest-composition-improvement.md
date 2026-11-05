# 森林マップ自動生成の空間構成改善

## 目的

ForestRestartの自動制作パイプラインで、中央の不規則な広場、曲がる道、水路と橋、崖の縁、森の密集、ランドマークを組み合わせたマップをSeedから再現する。Unity Sceneを配置の正本に保ち、本番の固定マップ＋ランダムEvent方針は変更しない。

## 背景と根拠

2026-10-03の引き継ぎ依頼。従来の48×32Kitは3〜4楕円広場をL字道路で結び、草地全域へ3種の森林部品を散布する。道路をさらに1Cell保護するため縁に大きな空白ができ、水、崖、橋、屋台、噴水の表示は含まれていない。数値検査の成功だけでは依頼された見た目を満たさない。

Library指定画像2枚のHomePC受取りは正規手順で1回ずつ限定再試行したが、Windowsの拡張属性保存互換性エラーで完了しなかった。指定画像の実ピクセルはこのPCでは未確認。既存v005コンセプトと従来QA画像は実ピクセルを確認した。v005と指定画像1はサイズが異なるため同一扱いにせず、最終一致判断は保留する。

## 対象範囲

既存ForestRestartLayout、AssetBuilderと追加の描画部品、関連検査と資料。水路・橋・崖・中央広場・小広場・路地の役割を地形生成時に分け、保護域と移動不可域を先に決める。森を境界と地形縁へ寄せ、小物で縁をつなぐ。全景と同程度のキャラ倍率の構図を別々に撮影する。

## 対象外

プロジェクト全体の置換、既存素材の削除、Character/Event基盤の設計変更、新規購入、外部公開、push、本番の毎試合ランダム地形採用。

## 実装計画

1. 資料、現状コード、画像、未コミット変更の診断とバックアップ。
2. 48×32の意味を持つ構成生成と通行検査。曲線状通路、中央広場、左水路と2交差、崖、森縁。
3. v005を様式参照とする追加部品を候補として保存し、Scene生成で再利用。
4. 代表Seedの実Unity生成と撮影を早期レビュー。必要な修正を反映。
5. 複数Seed論理検査、実Scene検査、EditMode/PlayMode回帰、資料と再現手順の更新。

## 進捗

- 完了: AGENTS、関連資料、.agents確認（空、checkout SKILLなし）、生成処理と既存検査の診断。
- 完了: 501File、52,211,056bytesの復元用ZIPと各File Hash照合、Git HEAD・Working Tree/Index Patch・未追跡含むstatus保存。保存先は作業Workspaceのbackup-before-map-composition。
- 完了: 中央の不規則な広場、曲がる周回路、左水路と2本の橋、段丘、森林のまとまりと地形縁の小物を生成する規則を実装。Source7点、追加Sprite20点、水面Tile、SeedとCamera・任意ランドマークの設定Assetを追加した。
- 完了: 最初の代表Seed20261003をSibling Sceneへ実Unity生成して結果を共有。その後、橋Deckと崖Frontを修正し、Seed20261003〜20261006の全4Seedを生成した。全件で論理検査と実Scene Validatorに成功し、全景・構図比較・ゲーム縮尺・拡大・橋上の5画像ずつ計20画像を保存した。
- 完了: スタンドアロン環境の27論理Test Case。Unity Scene・Physics・Test Runnerの結果とは区別する。
- 完了: 修正後の全景、橋上、ゲーム縮尺の実ピクセルを確認し、橋の歩行面と崖Frontの表示問題を解消した。全4Seedの実Physicsで橋を除く水域90Cell、橋21Cell、崖、全歩行Cellと木の接地点を確認してError0。
- 完了: 修正済みSeed20261003を公開`BuildScene(seed, ReferenceScenePath)`で既存Referenceへ反映し、探索Previewへ接続。ReferenceのPath/GUID、既存76SpriteとGUID、元のEditor作業状態を維持した。未参照の試作Water08〜31とmetaは、自分が追加した48Fileだけを参照0・復元ZIP確認後に整理した。
- 完了: 代表SeedのOverview / Game Scale / Bridge Standing3点をLibraryへ保存。保存後のWindows拡張属性によるローカルmetadata追記は失敗したが、Library保存は成功した。
- 完了: Reference移行後の最終Unity回帰。EditMode113 / 113成功、PlayMode4 / 4成功、Failed / Skipped / Inconclusiveは0。平面装飾のTestへbridge / cobblesを加え、MapDetail / Order0 / Pivotの厳密な検査を維持した。
- 完了: 実Gameplay PreviewのReady、Reference読込み、歩行、Camera追従、HUDの画面Capture2点と実ピクセルを確認。生成20画像とRuntime2画面の計22PNGを検証集計へ保存した。Console対象区間のError0、Editorを元の保存済みTemplate Sceneと実行設定へ復元した。
- 完了: 制作・再現手順、Map Plan、生成Asset台帳、Indexをcheckoutへ反映。正式Knowledge検証は66 Markdownすべて成功し、最終保護監査は155FileのHash一致、バックアップ時に存在したFileの欠落0、想定外変更0を確認した。
- 継続: 指定入力画像の実ピクセル比較、道と芝の階段状輪郭、水岸、森に隠れる上段段差の見た目改善。計画はこの評価を残してactiveに維持する。

## 判断記録

- 同じSeedからの再現性と、全歩行領域、4入口、6Event目的地の連結を維持する。
- WaterCellsには橋の下の水も含め、橋を除いた水と崖は移動不可。地形Collisionと部品接地Collisionを分離する。
- 追加素材は候補として別Directoryに保存し、現行素材の内容とGUIDを保護する。
- 既存Sceneは未保存変更を確認してから操作する。最初の結果はSibling Sceneへ生成する。
- 原寸の全景とFocused画像を保存し、カメラ倍率だけの改善を達成とは扱わない。
- 店と泉は設定で切り替える構図候補とし、商取引や回復処理を新設しない。コンセプトは様式と密度の補助参照であり、指定画像の全モチーフを必須仕様にしない。
- 保存SceneのCamera Sizeは設定Assetから読み、QA撮影の全景16・構図比較9・ゲーム縮尺6・拡大6・橋上4とは区別する。
- 初回Bridge Deckが歩行域より狭く見えたため、Sourceと正規化規格を修正した。修正前画像と最終画像を混同しない。
- 初回比較では既存Referenceを保持し、修正後の代表SeedとPhysicsを確認してからReferenceへ移行した。探索PreviewのPath読込みを維持し、別のRuntime生成実装を増やさない。
- 残る見た目は道と芝の階段状輪郭、水岸の薄さ、森に隠れる上段の段差である。生成・Physics検査の成功と指定目標との見た目の最終一致を分け、今回の入力画像取得失敗を隠さない。

## 検証

- スタンドアロンの27論理Test Caseが成功。作業Workspaceの`geometry-check/layout-test-results.txt`へ記録した。これはUnity Engine実行、Scene描画、Physicsの検証ではない。
- `Assets/Art/Map/ForestRestart/QA/composition-seed-{seed}-validation.json`: Seed20261003〜20261006すべて`logicalValid=true`、`sceneValidated=true`、`sceneValid=true`、Error / Scene Issueなし。対応Sibling Sceneを同じSeed名で保存した。
- 実画像は同QA Directoryの`composition-seed-{seed}-overview.png`、`-focused.png`、`-game-scale.png`、`-detail.png`、`-bridge-standing.png`。修正後の橋Deckと崖Frontで全4Seedを再撮影し、代表の全景、橋上、ゲーム縮尺の実ピクセルを確認した。
- 全4SeedのPhysics検査: 橋を除く水域90Cell、橋21Cell、崖、全歩行領域、木の接地点を確認してErrorなし。
- Reference移行後の最終Unity Test Runner: EditMode113 / 113成功（10.42秒）、PlayMode4 / 4成功（2.2秒）、Failed / Skipped / Inconclusiveは0。完全な応答は`Logs/MapComposition/20261003-editmode-tests.json`と`20261003-playmode-tests.json`。これらはGit対象外の生成証跡で、永続集計は[composition-verification.json](../../../../Assets/Art/Map/ForestRestart/QA/composition-verification.json)。
- 実Gameplay: Preview Ready=true、失敗Message空、Referenceの同Pathを読込み。Frame85348から88979へ進行し、Body位置(24.5,13.5)から(37.1,13.5)、Cameraは(33.78,13.5,-10)へ追従した。既存の青緑Debug Markerを操作し、静止した比較Heroの正式採用変更はしていない。移動後の樹冠背面表示も実画面で確認した。
- 実画面: `composition-runtime-start.png`と`composition-runtime-after-move.png`はsource=screenでHUDを含む。4Seed×5画像と合わせて22PNGのPath/Hashを検証集計へ保存した。
- 復元: `isPlaying=false`、元のTemplate Scene1点はclean、`runBackground=false`、tickは元のtrue / 16ms / persistInterval=-1へ戻した。Console cursor94〜138のError0。
- 保護監査: 既存76Spriteとmeta152File、Reference.meta1File、Packages2Fileの計155Fileは完全一致、baselineの欠落0。最終資料反映後に再確認する。Knowledgeの正式Validatorも資料反映後に実行する。
- 修正済みSeed20261003の画像Hash: Overview `ac5d83cc247c6ee3a4c6bb03993345d2fe0feafeeae26932517286b4374a70f3`、Game Scale `e70ccae65279cd1120c41e5c0d199f902a33c66aee8032537d89c3f3cf121607`、Bridge Standing `cc7f552081ad478ea24b80324c8a572933278b4d07cbff5a99eceffed272252d`。
- Library出力保存: Overview `libfile_5ec81357c9c48191bb2aff13a17b8288`、Game Scale `libfile_5732f5fb7184819189f4afa4ff7584a0`、Bridge Standing `libfile_df312ddda4a8819186288375a5e9d4ec`、すべてVersion0。Library保存成功と保存後のローカルmetadata失敗を区別する。
- 指定Library画像のHomePCローカル化は未完了で、正確な参照ピクセルとの最終一致判断は保留する。

## 完了結果

構成生成、4Seedの実Scene/Physics検査、Referenceへの移行、設定からの再現手順、最終Unity回帰と実Gameplayの画面確認を実施した。実装と動作検証の結果は検証集計へ保存済みで、Knowledge検証と資料反映後の保護監査も成功した。指定入力画像はHomePCで未確認で、見た目には道路・芝の輪郭、水岸、上段の段差の課題が残る。目標との最終一致を達成したとは扱わず、この見た目の評価を継続するため計画をactiveに維持する。
