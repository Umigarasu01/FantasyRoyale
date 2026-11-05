# A基準の俯瞰2D一画面自動構成Study

## 目的

Aの2Dドット表現を基準に、品質を確認した地形ブロックPrefabを接続規則で組み合わせ、一画面の森林風景を自動構成する。ユーザーの「ブロック方式で行きましょう。テストを進めて。」を今回の独立Study内の実装とテストの承認として扱う。単体ブロックを確認してから2ブロック接続へ進み、代表の実Camera Renderと歩行結果を親側へ共有する。代表2ブロックのレビュー前は3ブロック以上のScene生成を保留し、レビュー後に追加Seedと規模を判断する。

2Dドットのデザインは確定事項とする。ピクセルの粗さと見下ろし角度は製品としての検討事項で、A64はこのStudyの暫定比較基準である。今回の視覚検証の主Referenceは`A-overhead-vivid`に固定し、当初のimage(8).pngの斜め視点へ戻さない。固定マップを自動制作する方針、ランダムEvent、既存のゲームルールとArchitectureを維持する。

## 背景と根拠

ユーザーは先に参考画像のような画を自動生成する方法を検討し、品質を下げずに進めることを指定した。Aの共有説明は、広い上面と短い前面を持つ俯瞰2D、小さなHeroと土の小広場、自然に曲がる細い川と横橋、円形の泉と屋台、密な木・低木・岩・花、鮮やかな緑と青、淡い土色である。画像の配置をそのまま複製する条件にはしない。

[Prototype方針](../../product-specs/prototype-direction.md)の固定地形とランダム配置、[Art Direction](../../product-specs/art/art-direction.md)の意味のあるドット描写、自然な境界、密度と可読性を引き継ぐ。[Map Plan](../../product-specs/art/map-plan.md)は大型Spriteと複合Prefabの規則配置を認めている。既存76Spriteは初回Kitの内訳と保護対象であり、新しいStudyの素材数上限にはしない。

初回素材ZIPは親側で生成し、ユーザーが手動で保存する分担とした。当初のLibrary参照2画像とground R2 ZIPは、その後に担当側が正規WSL経路で取得し実画素を確認した。ground R2の実Unity導入とSource/Derivedレビューは完了した。HomePCから新しい画像は生成せず、提供されたR2スクリプトは実行していない。

## 対象範囲

- 新規の独立したOverheadStudyA領域のInput案内、Plan、Import、ブロックPrefabの保存と接続、一画面構成のBuilder、実表示と歩行テスト。
- 入力先は`Assets/Art/Map/OverheadStudyA/Input/`。PNG原本と提供されたmanifestを保持し、読取り診断、Hash記録、実ピクセル確認を行う。
- 派生素材とmanifestはOverheadStudyAの新規専用領域へ保存する。元PNG、切り出し範囲、共通の整数downscale factor、Padding、Pivot、World寸法、Import設定を追跡可能にする。
- 比較画面の暫定基準は24x16world、1536x1024、Orthographic Size8。同じ構図とWorld寸法で粒度を比較する。
- 既存Scene、素材、GUID、Character/Event基盤、Packages、ProjectSettingsを保護する。元の689Fileを保持し、直前の703File checkpointを復元基準として新Study内の許可された更新を追跡する。

## 対象外

既存Fileの置換、従来の構成改善6資料の再編集、既存Referenceへの反映、3D方式、ゲームルールやArchitectureの変更、本番の毎試合Runtime地形生成、素材数を76点へ制限すること、新規画像生成・取得、購入、公開、push。

## 実装計画

1. 新規専用Pathと準備Builderを用意する。素材未受領時は`visuallyReviewed=false`とし、BuildはAssetとSceneを変更する前に拒否する。Inputの閲覧と診断は可能にする。
2. ユーザーが保存したZIPを確認し、PNGと任意manifestの存在、読取り、実ピクセル、原本Hash、部品の役割と不足を確認する。ファイル名やschemaを推測して素材を採用しない。
3. 全部品の相対寸法と粒を確認して、aspectを維持する共通の整数downscale factorを決める。Nearestで元画素の色を保ち、必要なPaddingと足元Pivotを記録する。提供manifestのPivotを優先し、未指定の場合は整数pixelを基準にする。整数比で成立しない素材は歪ませず、問題を報告する。A64の暫定設定を派生manifestへ残す。
4. 確認済みInputとHashに対してレビュー状態を有効にし、単体ブロックを新規専用領域へ保存・表示する。接続口、通行域、表示Bounds、接地Collisionと可変配置領域を分ける。実Camera Renderと歩行で単体を確認してから、2ブロックの接続口、境界表示、Collisionの重複・隙間、通行と可読性を実検証する。
5. 代表2ブロックの実画像と歩行結果を親側へ共有する。レビュー前は3ブロック以上のScene生成を行わない。レビュー後に追加Seedと規模を判断し、同じ設定の再現、別構成、採用する粒度と不足素材を記録する。
6. PASS5では、編集可能な閉じた輪郭を8～16pixel単位のmaskへ落とし、既存の土textureを使用して大きな64pixel矩形の土面を置き換える。ぼかしは使わず、PPU64と既存素材を維持する。広場を丸くしWorld X11付近の首で林道へ接続、林道を下側から上側へ緩く曲げる。新規のSettingsだけに適用し、旧保存Settings・Scene・PNGを保護する。Details検証後のQA保存と変更前backupを担当Rootが終えてから、Unity導入・実生成・歩行・比較レビューへ進む。

## 進捗

- 完了: A基準の独立した一画面Prototype段階と、親側で素材ZIPを作りユーザーが保存する分担を確認した。
- 完了: 専用Inputフォルダーを用意し、`FantasyRoyale-A-starter-assets.zip`を受領した。3,698,657bytesとSHA-256が親側の期待値に一致し、`Input/StarterPackage/`へ10Fileを安全に展開した。
- 完了: 新規専用領域へSettings、Assets、Plan、Builderの4FileとInput案内、本ExecPlanを追加した。既存Fileは変更していない。
- 完了: 起動中Unity 6000.4.3f1でC#再compileを実施し、completed / failed=false / errors=[]を確認した。
- 完了: 空Inputの実Unity診断でImportとPlanが停止し、確認済み状態false、Plan未生成、Scene未生成、File一覧不変、Derived/QA未作成を確認した。EditorはPlay停止、元のTemplate Scene、dirty=falseを維持した。
- 完了: 受領したReference Aと素材review sheetの実ピクセルをHomePCで確認した。初回Starterは大木、中木、低い泉、屋台、低木、岩の6点で、当時は地面・水・橋・Hero・花が未提供だった。後の補足地形受領と区別する。
- 完了: ブロック方式の実装とテストをユーザーが明示承認した。元の固定マップ制作とランダムEvent方針を維持する。
- 完了: 実装直前の703File、91,641,083bytesを`backup-before-block-study/source-checkpoint.zip`へ保存し、全EntryのSHAを検証した。
- 完了: Starter6点を原本と同じPNG bytesでDerivedStarterへ導入した。仮PPU64、Point、Mipmapなし、非圧縮、Full Rectと個別の足元Pivotを確認し、原本と派生6画像の実レビューを記録した。`StarterPropsReady=true`、`FullTargetReady=false`、Errorなし。
- 完了: 区画の接続口、Tilemap通行集合、主導線、固定配置と可変配置候補をPrefabへ保存し、readback後の契約を確認するBuilderを追加した。
- 完了: `StudyABlockGeneratorTests`を対象とするUnity Editor Test17件が成功した。Failed / Skipped / Inconclusiveは0。向き、幅、地形、高さの不適合接続を拒否する論理検査で、実歩行の成功数とは区別する。
- 完了: 旧診断地形でSeed20261003のPlaza、ForestPath、BridgeBank、PlazaAndPath、PathAndBridgeBankの5代表Caseを実生成した。全件`Generated=true`、Errorなしで、Scene、Settings、Prefab、1536x1024 PNGとreportを保存した。各Sceneは最大2ブロックである。
- 完了: 初回2ブロックの診断PNGを親側が実ピクセルで確認した。A基準の画質には未達と判断され、構造検査の成功とは分けて記録する。
- 完了: PlazaAndPathの本物CharacterActor2DによるEditor Playの自動経路走行。ゲーム時間20.0011444秒で経路端へ5回到達し、総移動99.60557unit、Failure空を確認した。Foot Capsule0.48x0.55unit、Offset(0,0.22)、Frame23062から48182への進行を確認した。
- 完了: 旧診断地形5CaseのPhysics QAを修正済みreportで確認した。全件Physics・cleanup成功、Errorなし。2接続Caseは追加して3laneを両方向に通す6走行をそれぞれ確認した。本物Actorと同じ寸法・移動条件の隔離Physics検査で、CharacterActor2D本体の走行とは分ける。
- 完了: 旧PathAndBridgeBankも本物CharacterActor2Dで20.00098秒、端への到達5回、99.6055unit、Failure空を確認した。旧2接続Caseの実Actor成功を、新しい素材・森林配置の成功へ流用しない。
- 完了: ユーザーが手動保存した`FantasyRoyale-A-terrain-supplement.zip`を受領した。310,992bytes、SHA256 `6ee259e4b370f0d880f2769fe56a212dd6dd871ec0bc54512c300d5131625802`が期待値に一致し、21Fileを`Input/TerrainSupplement/`へ安全に展開した。
- 完了: 補足の地形16PNGとHero idle原型1PNGを元と同じbytesで`DerivedTerrain/`へ導入した。native寸法、PPU64、Point、Mipmapなし、非圧縮RGBA32、Full Rectを確認し、SourceとDerivedの実ピクセルレビューを記録した。`ApprovedTerrainReady=true`、`HeroReady=true`、Errorなし。その段階の全体は`FullTargetReady=false`で、水・橋・花が不足していた。
- 完了: 新しい地形とHero原型を用い、Seed20261003のPlazaAndPathを新規生成し、保存したSettingsから再生成した。2件とも`Generated=true`、Errorなし。Pixelと素材選択が一致し、先の出力を保持した。Custom地形Cellは0で、既存の手塗り地形を引き継ぐ検証とは扱わない。
- 完了: 新しい森林表示の実Camera画像で泉・屋台を確認した。固定配置は木19本、森林の塊4群、低木15点、岩5点。最初の成果画像は同じLibrary File IDのversion1へ更新し、WSL経由のlocal metadata適用成功を確認した。
- 完了: PASS1の新しい地形・森林SceneのPhysics。1,070 simulation steps、Map Collider28個で成功、Errorなし。接続口の3laneを両方向へ通す6走行と、小物26個の停止判定を確認した。16個は他Colliderを無効にした隔離shape検査で、実マップ内の到達可能性を証明しない。
- 完了: PASS1の本物Actor走行。Ready、Failure空、20.16832秒、端への到達5回、100.405556unit、Frame4282、FixedUpdate4896回を確認した。足元Capsuleと本物CharacterActor2Dの移動を維持した。
- 完了: PASS1の足元Yソートを北側(4.375,11.5)の背面表示と南側(4.25,9.5)の前面表示で確認した。両位置の実Physicsは`collisionFree=true`で、担当側が両方の撮影PNGを実ピクセルで確認した。
- 完了: PASS1の親側比較レビューでAとの差が大きいと確認された。矩形の土面、孤立した泉・屋台、4つに分かれた森林、高頻度のchecker状地面を次の修正対象とする。Physics成功を画質合格へ置き換えない。
- 完了: PASS1の新しい生成Run内QAへPhysics・実Actor・Yソート・集約Editor/PlayModeの6証跡JSONを保存した。変更前checkpoint604files、17,872,380bytesを全SHA検証した。既存689保護ファイルは不変。
- 完了: ground R2 ZIPは正規WSL経路でHomePC受信、401,540bytes、SHA256 `0116ce1878e884e072b4d6c0155593a920d09c8ce2d86b1fcdec035600dd3cc6`一致。別WSLプロセスでLibrary ID保持を確認した。Source16PNG/atlas実画素、SHA、binary alpha、R1と同じ境界alpha、草/土variant縁一致を確認し、新Input/GroundR2へ別版配置した。
- 完了: R2地形16点と継続使用するHero1点、計17Derivedを実Unityへ導入した。地形は原本とbytes一致、native寸法、Point、PPU64、非圧縮RGBA32、Full Rect。Source/Derivedの実画素レビュー済み、Terrain/Hero ready、Error0を確認した。R2 fingerprintは`0cb4be7e4851a2f62815486bd13943677dd761a6bda6049dff526d5ba9e90e0d`。R1 fingerprintと原本の履歴を残し、Heroは同じPathとPivot(32,11)で維持する。
- 完了: R2地形だけを変更して旧PASS1構図を実Unityで生成・撮影した。出力`terrain-plazaandpath-seed-20261003-20261003-154424663`は生成成功、Errorなし、Geometryと素材選択はPASS1と同じである。実画像でcheckerは解消したが、4つの森林の島と帯状の構図は残ると確認した。PASS2の構図改善結果とは扱わない。
- 完了: 当初Library参照のimage(8).pngとimage(9).pngも正規WSL経路でローカル化し実ピクセル確認。前者2,935,077bytes/1536x1024、後者6,949,982bytes/2048x1365。正式な取得前の失敗を実確認済みに読み替えず、この追加取得で解消した。
- 完了: PASS2のRuntime側接続規則を独立環境で3,880検査した。単体9、pair81、代表5Caseの計45構成、unsupported0を確認した。Unity Sceneの生成・実Camera Render・歩行の成功とは区別する。
- 完了: PASS2の構図を実Unityで生成し、保存Settingsから再生成した。出力`terrain-plazaandpath-seed-20261003-20261003-155946314`と`terrain-plazaandpath-seed-20261003-20261003-155950242`は両方生成成功、Errorなし、PNGと素材選択一致、元出力保持である。同じLibrary File IDへversion2として保存した。
- 完了: PASS2の本物CharacterActor2Dによる自動経路走行。Ready、Failure空、20.0545673秒、経路端への到達5回、総移動99.86139unitを確認した。PASS2の隔離Physics QAは実行していない。
- 確認事項: 旧R1の保存Settingsからの追加再生成`terrain-plazaandpath-seed-20261003-20261003-161257786`ではPNG3022pixelに差があった。Geometry、地形、native素材の意味情報は一致し、同じ足元Yの3組でUnity内部の描画順tieが確認された。初回2出力のPNG一致をすべての旧R1再生成へ一般化せず、未解決の再現性制約として記録する。元SceneとPNGは保持した。
- 完了: 凍結したPASS3 Sourceを担当RootがUnityへ導入し、実生成・撮影と保存Settingsからの再生成を確認した。出力`terrain-plazaandpath-seed-20261003-20261003-164136371`と`terrain-plazaandpath-seed-20261003-20261003-164140420`は両方生成成功、Errorなし、PNGと素材選択一致、元出力保持である。Generator SHA256は`73da7c22410cc1bf8b2f009ea0910e2d6b246c3c1443ee3cb0984dbe4cd825b7`。
- 完了: PASS3の実PNGを担当Rootが確認した。林道の上下に変化する輪郭と広場前面の拡張が表示されている。一方でCell単位の階段状輪郭は残り、A完成には達していない。成果画像は同じLibrary File IDのversion3へ保存し、親側へ早期共有した。
- 完了: 親側提供の追加Details12点のZIPを正規WSL経路で受領した。289,054bytes、SHA256 `db68e1244969ff653f803c29a01f98996538faff35bfbfc13bafa28d2ebe7f73`が一致し、別WSLプロセスでもLibrary ID保持を確認した。新Input/ForestDetailsR1へ33原本Fileをbytes一致で配置した。
- 完了: PASS3の本物CharacterActor2Dによる自動走行を確認した。Ready、Failure空、20.0163441秒、端への到達4回、総移動99.05261unit、`fixedFrames=1906`、終了時`running=false`である。20秒の曲がる経路での4回到達を、旧Caseの5回へ読み替えない。
- 完了: 修正済みSchema3のPASS3 Physics QAは`phase=complete`、passed / physicsPassed / cleanupPassedがtrue、Errorなし。1,992 simulation steps、Map Collider76個、主導線の往復と3lane・両方向6接続走行を確認した。小物停止74個は実配置への自由な接近15個と隔離shape59個で、後者のGameplay到達可能性は未証明である。
- 完了: PASS3の実Ground384Cell、Collision68Cell、ブロック所有240Cellのcoverageと、ForestBackdrop41個の実Collider Bounds・足元anchorとMetadataの一致を確認した。初期QAのSchemaと足元anchor読取りの誤りを修正したもので、Game Sourceと素材は変更していない。初期の失敗ログを保持し、素材不良の失敗と混同しない。PASS2の隔離Physicsは未実行のままとする。
- 完了: PASS3の実Yソートを本物Actorの明示2姿勢で確認した。北側(4.375,11.5)は樹冠の背面で隠れ、南側(4.25,9.5)は全身が前面へ表示される。両位置は実OverlapCapsuleでMap Collision非接触、Sorting LayerはWorldObjects、Sort PointはPivot。両Camera PNGの実ピクセルを担当Rootが確認した。animationと人間入力は検証していない。
- 完了: PASS3生成RunのQAへ実Actor・Physics・Yソートの3JSONと前後表示2PNGを保存し、PASS2の実Actor証跡も保存した。Details変更直前に120File、4,058,747bytesを`backup-before-forest-details-r1/pass3-before-detail-checkpoint.zip`へ保存し、全Entry Hashを確認した。
- 完了: Details ImporterをUnityへ導入し、12Derived PNGを原本と同じbytes・native寸法・PPU64・Point・提供Pivotで取り込んだ。Source/Derivedの実レビュー済み、Details `Ready=true`、Errorなし、全SpriteのPhysics Shape Count0。`FullTargetReady=false`を維持し、素材導入を装飾配置の生成成功へ置き換えない。
- 完了: Details対応BuilderとSettingsをUnityへ導入し、C# compile完了・Errorなしを確認した。Builder SHA256は`f7fb6061096d714cc52894a875ae93e8a64e638abdce64554dd36e42a6572f91`、Settingsは`743c371fbc6c81b141bc9c70a3066a45ecb0805b9ece692bd9e30f48b9545366`。
- 完了: Details付きのPlazaAndPathを実生成・撮影し、保存Settingsから再生成した。出力`terrain-plazaandpath-seed-20261003-20261003-171518297`と`terrain-plazaandpath-seed-20261003-20261003-171520443`は両方生成成功、Errorなし、PNG・素材選択一致、元出力保持。配置73個は草54、花10、踏み石7、切り株2である。
- 完了: 担当RootがDetails付きの実画像を確認した。泉・屋台へ向かう分岐、局所的な花と境界の草が表示されている。Cell単位の階段状輪郭は残り、A完成には達していない。同じLibrary File IDのversion4へ保存し、親側へ早期共有した。
- 完了: Details付きSceneの本物CharacterActor2D走行。Ready、Failure空、20.053339秒、端への到達4回、総移動99.25261unit、終了時`running=false`を確認した。
- 完了: Details付きSceneの実Sprite73個とMetadataが一致した。71個はColliderなし、切り株2個は指定された実Colliderを確認した。Sprite ImportのPhysics Shape Count0と、切り株GameObjectの明示Colliderを分ける。
- 完了: Details付きSceneのPhysics QAは`phase=complete`、passed / physicsPassed / cleanupPassedがtrue、Errorなし、2,030 simulation steps、Map Collider78個。主導線と接続口3lane・両方向を通過し、小物停止76個は自由な接近17個と隔離shape59個である。元Scene不変、Editor Sceneと時間設定の復帰も確認した。
- 完了: Details付き実装の集約回帰はEditor 150/150（8.7秒）、PlayMode 4/4（3.9秒）、failed / skipped / inconclusive0。PASS3の成功を流用せず、Details追加後の別実行で確認した。
- 完了: 親側の追加レビューで、Detailsを加えても64pixel単位の大きな土輪郭がAと異なると判定された。機能検査の成功とA画質未達を分け、PASS5の輪郭修正へ進む。
- 完了: Detailsの6恒久QA保存とEditor復帰を確認し、PASS5直前に1,322File・37,989,997bytesを全SHA検証したbackupへ保存した。描画順修正の直前も閉輪郭版1,667File・49,725,297bytesを別backupへ保存した。
- 完了: PASS5の閉輪郭をNative Prefabへ保存し、8pixel maskでR2土の原RGBを1:1転写、alpha0/255だけで丸い広場と曲がる林道を描画した。Runtime ContourとEditor Rendererを追加し、Builder / Settingsを変更した。既存GeneratorとDefinitionは変更していない。実Unity撮影を親側が2ブロックの検証構図として確認した。A品質完成とは判定していない。
- 完了: 保存Settingsを明示再読込すると等足元Yの3組で2,199pixelが反転したため、Fine有効時だけ同Yの重なる小物を個別の描画anchorで安定化した。最終Builderは`f9b4252a8643f87495253aa95bc0aaaa0d5de63e3d44c19c3f33ee2213c4871d`。旧Fine=falseの処理・原素材・Native Prefab・描画物とColliderの実位置は保持した。
- 完了: 最終版の実Unity 3出力（`184127731` / `184130211` / `184132437`）は保存Settingsの明示再読込を含めPNG・全選択Hashが一致。元の閉輪郭Scene・Settings・PNG・Prefabを保持した。Libraryは同じIDのversion6へ保存し、別WSLプロセスでIDとPNG SHAを確認した。
- 完了: 最終版の本物CharacterActor2Dは20.0004158秒で端へ5回到達、98.04995unit、Failure空。実土Spriteは1536x1024 / PPU64 / Point / Pivot0 / PhysicsShape0 / Collider0。実Details83個（Colliderなし81、切り株2）と個別描画anchor22個を照合した。
- 完了: 最終代表のPhysicsは2,014steps・Map Collider78個、主導線・3lane両方向接続・施設停止・cleanupが成功。実Actorの背面と前面を2つのCamera PNGで確認した。175Rendererと76BoxColliderの姿勢・形状を原閉輪郭版と独立比較して差分0だった。
- 完了: 最終BuilderでSeed17・20261004を再生成して実画像を確認し、両方のPhysicsを実行した。各2,014steps、接続・保護域・Collider・cleanup成功。初回seed出力は保持し、両設定のLayout・地形・Details・素材選択が一致した。
- 完了: 最終版の全Editor150/150（6.09秒）、PlayMode4/4（2.26秒）、failed / skipped / inconclusive0。通常Explorationの読込み・移動・Collision・Yソート・泉状態を含む既存Testが成功した。手動の通常起動、Player Build、人間入力と実戦は未実行。EditorはPlay停止・元Template・clean・runInBackground=falseへ戻した。

- 完了: 前版ExecPlan（SHA256 `4d47fb92f651e89e72344221f5c1b3c7b4c31a14a49a87fb59ff01a9d0a7477a`）をProjectへ反映後、Tools Knowledgeの67Markdown検査が成功した。今回のStage更新を反映した後の検査とは区別する。
- 完了: PASS1の集約Unity回帰はEditor 150/150、PlayMode 4/4成功、failed / skipped / inconclusive0である。初期の17件の対象Testと区別し、PASS2のUnity回帰結果へ流用しない。
- 継続: 代表の実歩行結果と実画像レビュー。3ブロック以上のSceneはレビュー前に生成しない。

初回5Caseでは草・土・水・橋を診断描画し、Heroを旧素材で静止表示していた。PASS1のPlazaAndPathは確認済みの地形16PNGとHero idle原型1PNGを使用する。以後も`StructuralOnly=true`、`FullTargetReady=false`で、生成時reportの`ActualActorWalkVerified=false`を後の別診断結果で書き換えない。PASS1の実歩行とYソートは確認できたが、親側の比較レビューではA画質に未達である。PASS2は実生成・再現と本物Actor走行を確認したが、Physics未実行である。PASS3は実生成・再現・描画、本物Actor・修正済みPhysics QA・実Yソートを確認した。Details追加後も実生成・再現、Actor・Physics・Sprite照合・集約Testを確認したが、親側レビューでは64pixel単位の大きな土輪郭がAと異なると判定された。PASS5の閉輪郭版では実生成・Actor・接続Physics・保存設定再現・少数seed・既存回帰を完了し、2ブロックの検証構図として保全した。目標全体の画質達成とは区別する。水・橋と全体の仕上げ、正式Hero表現とanimation、Enemy専用の許可領域・抽選契約は残り、Enemy候補を出力していない。

承認前の準備Builderは書込みのないPlan入口までで、Scene・Tilemap・Collision配置・撮影保存処理を含んでいなかった。今回の独立Studyでブロック保存、接続と実描画へ進む処理を追加する。当時のPlanにある細道1Cellと橋2x1Cellは仮配置で、現在の接続通路2unitの試験設計や正式なゲーム幅の成功記録とは扱わない。

ブロック方式の実装前に行った独立managed診断では、仮の素材寸法による131Seedの論理検査と1/16unit間隔の足元経路標本検査が成功した。今回のブロック接続、実素材、Unity Physics2D、連続歩行、画質の成功結果へ流用しない。

## 判断記録

### 区画Prefabの契約（今回の独立Study）

ユーザーから「素材単位でランダム生成するより、地形Prefabを保存して組み合わせる方が現実的ではないか」という方法相談を受け、その後「ブロック方式で行きましょう。テストを進めて。」と明示承認された。独立Studyで、品質を確認した広場・林道・橋付き川岸などの区画Prefabを保存し、接続規則で組み合わせる方式を実装・検証する。内部の許可領域には小物や既存Socket候補を限定配置する。全面的な本番方式変更や新しいRuntime基盤は対象外とし、現在の個別Spriteは区画を構成する部品として使用する。

単体ブロックの実表示と歩行を先に確認し、2ブロック接続の代表結果をレビューする。レビュー前に3ブロック以上のSceneを生成せず、未提供素材を含む区画を完成扱いしない。小広場、曲がる林道、橋付き川岸は後続の候補で、寸法と制作順は受領素材と画面レビューから決める。

| 契約 | 受領後の一画面で残す情報 |
| --- | --- |
| 原点と占有 | 区画のローカル原点、World寸法、配置Footprint、樹冠等の表示Bounds、境界の重ね合わせ範囲 |
| 接続口 | ローカル位置、外向き、幅、地形種別、歩行可否、境界部品。道の入口と川・岸の接続を区別する |
| 通行域 | 接続口同士を結ぶ歩行領域、通行不能領域、橋床と両岸着地、主導線・目的地の保護領域。最終の移動判定はSceneに保存したMapCollisionとする |
| 可変配置領域 | 花・石・低木等の配置許可と密度範囲、Enemy/Loot/Event等の既存Socket候補。接続口・主導線・橋出口・Heroと目的物の足元を保護する |
| 表示と接地 | 各Spriteの足元Pivot・Layer・CollisionBodyを保持する。区画全体を一括の描画順へまとめず、Characterと立体Spriteの前後関係を確認する |

橋付き川岸は東西の歩行接続口と、上下の水面・岸接続を別契約にする。接続時は見た目だけでなく、幅・方向・地形・接続口間の歩行到達性を検査し、境界のCollision重複・隙間・二重の岸表示を確認する。方向違いの候補は共通の光方向と作画視点を保つ。回転・反転・伸縮で未制作の接続を補うことは自動採用しない。

現PlanのMainRoute/RiverCenterline/Clearing/BridgeWalkRect、Cell通行集合、PlacementのFootprint/Collider/VisualBoundsは契約整理へ再利用できる。区画の接続口・地形接続型・可変配置領域とPrefab保存・接続は今回の独立Studyの実装範囲になった。実装と動作検証の成功は実証後に追記する。MapSocketMarkerはEnemy等の候補地点であり、地形接続口そのものには流用しない。

配置結果はUnity Sceneを正本とし、既存の固定マップ制作とランダムEvent方針を保つ。毎試合のRuntime地形生成はこの方法相談から採用しない。

Inputは原本として保持する。提供されたmanifestがあれば同梱し、未提供ならPNGを読んで対応表を確認する。manifestの欠如を、素材が正しく揃っているという判断へ置き換えない。

見た目を確認する前に生成を始めない。`visuallyReviewed=false`のBuildはAsset/Sceneの書込み前に拒否する。レビュー済みという判断は確認したInputのHashと対応させ、PNGが差し替わった場合は再確認する。

aspect、相対寸法、粒の一貫性を優先する。共通factorによる整数比downscale、必要な余白、足元Pivotで規格化し、色替え、任意の縦横伸縮、回転や反転で部品不足を補わない。Pivotは提供manifestを優先し、未指定の場合は整数pixelを基準にする。地面の連続面と透過Objectの余白は用途を分ける。

追加Details R1は規格化済みnative素材をそのまま導入し、承認済みmanifestの足元Pivotを保持する。12点にはX座標が0.5pixel刻みのPivotも含まれるため、初期の整数pixel Pivot案を理由に丸め直さない。画像のresize、tintや補間は行わない。SpriteのPhysics Shape Count0と配置GameObjectのCollisionは別に扱う。実配置は71個がColliderなし、切り株2個が明示Colliderであり、通行と停止をDetails追加後のPhysicsで確認した。

24x16worldを1536x1024で表示すると64画面pixel/worldになる。native32・PPU32は1native pixelが画面2pixel、native64・PPU64は画面1pixelとなる。粒度比較ではWorld寸法、配置、光源、内容、Cameraを揃える。32素材の単純拡大を64案の質感比較と扱わない。A64は暫定で、既存のPPU32契約や正式Character尺度を変更する根拠にはしない。

Hero、橋出口、主要導線、泉・屋台の足元の可読性を保つ。配置Footprint、表示Boundsと接地Colliderを分け、橋床はCharacterの背面、立体表示は既存の足元Yソートを維持する。新しいGameplay効果や操作経路は追加しない。

現行主道の3Cell / Radius1は制作基準で、Actor由来の最低通路幅や戦闘の最低面積ではない。今回のStudyは接続通路2unit以上を推奨設計値として試す。実ActorのCapsule0.48x0.55unit、OffsetY0.22、既存のRigidbody2D移動を維持し、曲がりと接続口を実歩行で確認する。通常CameraはOrthographic Size8。初回6素材による診断から、地形16PNGとHero原型1PNGを使う表示へ進めた。native64x80・PPU64のHero idleはWorld1x1.25、Pivot(32,11)pixelであり、既存ActorのColliderや正式Character規格を変更しない。花などのDetailsは配置生成・表示まで確認したが、水・橋は未提供で、階段状輪郭と全体の仕上げが残るため画質完成とは判定しない。

PASS1の地形は草2、土2、cardinal境界4、convex角4、concave角4の16素材で、原本を加工せずnative画素のまま導入した。初回補足ZIPは手動で受け渡した。別TaskでWSL経由の素材転送とmetadata適用を確認した結果を踏まえ、親側が提供したground R2 ZIP（Library File ID `libfile_5d9f509f5dfc8191bdd7db941a46515e`）を正規WSL経路で受領し、原本とbytesが一致する派生地形、実Unity ImportとSource/Derivedの実レビューを確認した。新しい依存は追加せず、同梱・提供されたR2スクリプトは実行しない。R2地形と構図改善は別々に検証する。

PASS2はPASS1で指摘された矩形面と孤立感を修正する。全体20x12に収まるPlazaとPath8x12の2ブロックを維持し、土面を絞って分岐を加え、ブロック間の森林を連続させる。Camera8と同じWorld尺度で比較し、密度の増加で主導線、Hero、泉・屋台の足元を隠さない。3ブロック以上へ拡大して画質課題を解消したとは扱わない。

PASS3は凍結したSourceを用い、最大2ブロック、Camera8、GroundR2と同じ素材で境界と広場の形を修正する。接続口幅3unit、林道の保護通行域2unitを維持し、描画する土肩は3～4unitで変化させる。これらは制作上の試験値で、ゲームの最低戦闘寸法を追加するものではない。凍結や論理検査を実描画・Physicsの合格へ置き換えない。

PASS5は、親側の最新指定に従い、大きな64pixel矩形Tileの土面を編集可能な閉じた輪郭へ変更する。8～16pixel刻みのmaskに既存土textureを使用し、ぼかしを加えずドットの粒を保つ。広場は丸い形からWorld X11付近の首へ絞り、林道は下側から上側へ緩く曲がる2.5～3unit幅とする。3unitの接続口幅は境界付近だけに使い、道全体を矩形帯へ広げない。この寸法はStudyの構図指定で、ゲーム全体の最低通路幅を追加するものではない。変更は新規生成用のSettingsだけに適用し、旧保存Settings・Scene・PNGを置き換えない。

## 検証

準備段階は、新規専用のSourceを取り込み、Unity CLIから起動中EditorのAssetDatabase Refreshと再コンパイルを行ってC# compileを確認する。素材未受領・未レビュー時のBuild拒否も確認し、拒否までに派生素材AssetやSceneの書込みへ到達しないことを確認する。これは素材のImportと通常の生成成功とは区別する。実装を写しただけのTestや、未実行のUnity生成結果は追加しない。

素材受領後は次を実際に確認する。

- PNGの存在、読取り、実ピクセル、原本Hashとmanifestの対応。原本を保持し、規格化後のaspect、factor、Padding、Pivot、Point、非圧縮、Mipmapなしを確認する。
- 単体ブロックと2ブロック接続の実Camera Render。接続口と主導線、泉・屋台、森林の塊が読み取れ、矩形の下地、継ぎ目、透明穴、周期的な反復が目立たないこと。未提供の水・橋、Details追加後の可読性と親側レビュー、Hero原型のanimation・正式表現は別に記録する。
- 実Actorで主要導線、曲がり、接続口を連続歩行し、Collision重複・隙間、泉・屋台と木の接地判定、Cameraと足元Yソートを確認する。橋や水は素材と地形が用意できた段階で通行・停止を確認する。表示の密度がHeroや歩行入口の可読性を損なわないこと。
- 代表2ブロックの親側実画像レビュー。レビュー前は3ブロック以上のSceneを生成せず、レビュー後に追加Seedと規模を判断する。
- 元の689FileのHash、欠落、想定外変更と、703File checkpointからの新Study内の許可差分、最終Diff。必要なKnowledge検証は新規Plan反映後に行う。

結果は実行後にPath、Seed、設定、Hashと未解決事項を記録する。Compileとguardの成功だけで、A基準の画が自動生成できたと判定しない。

### 今回確認済みの証跡

以下の5Caseは旧診断地形の履歴である。生成時reportはすべて`Generated=true`、Errorなし、SceneとPNGが存在する。全件`StructuralOnly=true`、`FullTargetReady=false`、`ActualActorWalkVerified=false`である。5Caseの隔離Physicsと2接続Caseの本物Actor走行は後の別証跡で確認済みで、生成時reportへ成功値を流用しない。

| Case | 旧診断出力ID | 保存済みScene / PNG | 本物Actor歩行 |
| --- | --- | --- | --- |
| Plaza | `diagnostic-plaza-seed-20261003-20261003-132924273` | あり | 独立した本物Actor走行は未記録 |
| ForestPath | `diagnostic-forestpath-seed-20261003-20261003-132926540` | あり | 独立した本物Actor走行は未記録 |
| BridgeBank | `diagnostic-bridgebank-seed-20261003-20261003-132928663` | あり | 独立した本物Actor走行は未記録 |
| PlazaAndPath | `diagnostic-plazaandpath-seed-20261003-20261003-132930784` | あり | 自動経路走行20.001秒、端への到達5回、99.60557unit |
| PathAndBridgeBank | `diagnostic-pathandbridgebank-seed-20261003-20261003-132932960` | あり | 自動経路走行20.001秒、端への到達5回、99.6055unit |

Sceneは`Assets/Scenes/MapAuthoring/OverheadStudyA/Blocks/{出力ID}.unity`。PNGとreportは`Assets/Art/Map/OverheadStudyA/BlockStudies/{出力ID}/QA/{出力ID}-1536x1024.png`と`{出力ID}-report.json`。Unity Editor Testの完全応答は作業Workspaceの`study-a-block-contract-tests.json`、対象Filterは`StudyABlockGeneratorTests`、Summaryは17 passed / 0 failed / 0 skipped / 0 inconclusiveである。これはProject全体の回帰Test数ではない。

旧診断地形のPhysics証跡は作業Workspaceの`study-a-physics-all-five-v2-summary.json`と`study-a-physics-case-summary-v3.json`である。v2の5CaseすべてPhysicsとcleanup成功、Errorなし。v3ではPlazaAndPathとPathAndBridgeBankの接続口を3lane、両方向の6走行ずつで確認し、すべて通過した。SceneのColliderとActorと同じ寸法・移動条件のRigidbody2Dを隔離Physicsで検査しており、CharacterActor2D本体のFixedUpdateを通した結果と区別する。

旧PlazaAndPathの実Actor証跡は作業Workspaceの`study-a-actual-actor-plazaandpath-summary.json`で、`verified=true`、Seed20261003、`passes=5`、`totalTravelled=99.60557`、`elapsedSeconds=20.0011444`を確認した。旧PathAndBridgeBankの`study-a-actual-actor-pathandbridgebank-summary.json`も`verified=true`、`passes=5`、`totalTravelled=99.6055`、`elapsedSeconds=20.00098`である。`StudyABlockMovementDiagnostic`が自動でMove Commandを渡し、本物のCharacterActor2DのFixedUpdateとRigidbody2Dで移動する。WASDなどの人間入力、Health処理、Event経路は今回の検証対象外である。

既存探索PreviewはBuildSettingsとEventSocket等の依存があり、この独立Studyへそのまま使用できなかった。既存コードを変更せず、新規の`Assets/Scripts/Playtest/Runtime/OverheadStudyA/StudyABlockMovementDiagnostic.cs`と診断Sceneを用意した。正式Gameplay全経路を通した結果とは扱わない。

PASS1のR1地形受領・導入証跡は作業Workspaceの`study-a-terrain-reviewed-ready.json`。17素材、RGBA32、Source/Derivedレビュー済み、`ApprovedTerrainReady=true`、`HeroReady=true`、Errorなし、`FullTargetReady=false`、missingはwater / bridge / flowerである。当時の入力とレビューのfingerprintは`ea52a563f4d644e219bc524f10c18da9e82e70e22d975766542979bd1866f66b`、BuilderのSHA256は`cd1d8d72536a4f5e8c7798f55a52eace39d10d08819062824b8e30b12b29b2b7`である。R2適用後の現状態と区別する。

PASS1の新しい地形によるPlazaAndPathと保存Settingsからの再生成は`study-a-forest-terrain-cases.json`で確認した。最初の出力IDは`terrain-plazaandpath-seed-20261003-20261003-145202284`、再生成は`terrain-plazaandpath-seed-20261003-20261003-145205035`。両件`Generated=true`、Errorなし、Custom地形Cell0、元出力保持、Pixel一致、素材選択一致である。Layout Hashは`9e33e87cc22ba8b850ff430bb53679a02cde1a3ccefb0f8abdbf457b0f197c94`、Terrain Selection Hashは`e400bedc9d3e1bc07ea200e989ec5dfe7d67ec0bcea861a019fac2d45a198e94`。SceneとArtの保存規則は上と共通で、診断履歴の出力へ上書きしない。

PASS1のPhysics証跡は`study-a-physics-forest-terrain.json`で、`passed=true`、1,070 simulation steps、実Map Collider28個、Errorなしを確認した。接続口の3lane・両方向6走行はすべて通過した。小物26個は、実配置の自由な接近経路を持つ10個と、他Colliderを一時無効にしてその実Collider形状だけを検査する16個の停止判定を合わせた数である。後者16個についてGameplay内の到達可能性を証明したとは記録しない。

PASS1の本物Actor証跡は`study-a-forest-terrain-actor-final.json`で、Ready、Failure空、端への到達5回、総移動100.405556unit、ゲーム時間20.16832秒、Frame4282、FixedUpdate4896回を確認した。Yソートの姿勢証跡は`study-a-forest-terrain-hero-behind-pose.json`と`study-a-forest-terrain-hero-front-pose.json`。北側(4.375,11.5)と南側(4.25,9.5)の実Physics非接触を確認し、担当側が両方の撮影PNGで前後表示を確認した。人間入力・Health・Event経路の検証対象は引き続き含まない。

PASS1の親側比較レビューではAとの差が大きく、矩形の土面、孤立した泉・屋台、4つの森林の島、高頻度のchecker状地面が指摘された。PASS1のPhysics・歩行・表示順の合格と、A画質未達を別に記録する。PASS1の集約回帰はEditor 150/150、PlayMode 4/4成功、失敗・skip・inconclusiveなし。集約後は旧Templateのclean Scene、Play停止、runInBackground=falseへ復帰した。R2旧構図の描画確認を経てPASS2を実生成したが、A完成と判定していない。

R2の受領証跡は`study-a-ground-r2-receipt.json`、導入・レビュー状態は`study-a-ground-r2-reviewed-ready.json`と`study-a-ground-r2-final-readiness.json`である。R2 fingerprintは`0cb4be7e4851a2f62815486bd13943677dd761a6bda6049dff526d5ba9e90e0d`、地形16点、Hero Pathは`Assets/Art/Map/OverheadStudyA/DerivedTerrain/hero_idle.png`、Pivot(32,11)、PPU64、Terrain/Hero ready、Error0である。R1のfingerprint `ea52a563f4d644e219bc524f10c18da9e82e70e22d975766542979bd1866f66b`はPASS1の履歴として保持し、R2の現状態と混同しない。

R2だけを旧構図へ適用した実Sceneの証跡は`study-a-ground-r2-old-composition-case.json`。出力IDは`terrain-plazaandpath-seed-20261003-20261003-154424663`、`Generated=true`、Errorなし、Custom地形Cell0、`FullTargetReady=false`である。Layout HashはPASS1と同じ`9e33e87cc22ba8b850ff430bb53679a02cde1a3ccefb0f8abdbf457b0f197c94`、Terrain Selection Hashも`e400bedc9d3e1bc07ea200e989ec5dfe7d67ec0bcea861a019fac2d45a198e94`を維持した。画素はR2へ変わるため、R1と同じPNGという意味ではない。実画像でcheckerは消えたが4つの森林の島と帯状構図は残り、A完成とは判定していない。

PASS2の独立Runtime検査は3,880件、単体9、pair81、代表5Caseの計45構成、unsupported0まで確認した。Unity生成と保存Settings再現は`study-a-pass2-cases.json`で確認した。初回出力`terrain-plazaandpath-seed-20261003-20261003-155946314`と再生成`terrain-plazaandpath-seed-20261003-20261003-155950242`は`Generated=true`、Errorなし、Composition Version2、Custom地形Cell0、元出力保持、PNGと素材選択一致である。Layout Hashは`4e62dac141de8a0ad46de9265c3120ee2724c60c94acbb2729758b1d4755bbd5`、Terrain Selection Hashは`2ae7dffaec5e95add76293d2c28fbc4ecefbe85d99fcf684d6a373e11dfc5d6b`。

PASS2の本物Actor証跡は`study-a-pass2-actor-final.json`。Ready、Failure空、端への到達5回、99.86139unit、20.0545673秒を確認した。PASS2の隔離Physics QAは未実行で、PASS3の実物理検証へ進む。PASS1の150 Editor / 4 PlayMode成功や独立Runtime検査をPASS2・PASS3のUnity回帰合格として流用しない。

旧R1の追加再生成は`terrain-plazaandpath-seed-20261003-20261003-161257786`で、PNG3022pixelだけが不一致だった。元のGeometry・地形・native素材の意味情報は一致し、同じ足元Yの3組についてUnity内部の描画順tieが確認された。`study-a-legacy-and-pass2-reproduction-raw.json`には旧R1のPNG一致条件が失敗した応答を残す。先の初回2出力が一致した事実と、この再現性制約を両方記録する。元SceneとPNGを保護し、修正済みとは扱わない。

PASS3の凍結状態は作業Workspaceの`pass3-proposal/handoff.json`に記録した。Generator SHA256は`73da7c22410cc1bf8b2f009ea0910e2d6b246c3c1443ee3cb0984dbe4cd825b7`。Unityへの導入と実生成・撮影後の証跡は`study-a-pass3-cases.json`である。初回出力`terrain-plazaandpath-seed-20261003-20261003-164136371`と再生成`terrain-plazaandpath-seed-20261003-20261003-164140420`は両方`Generated=true`、Errorなし、Custom地形Cell0、元出力保持、PNGと素材選択一致である。実装段階の呼称はPASS3だが、保存MetadataのComposition Versionは2である。Layout Hashは`a546dbae8ee2de479e6156eebc84017ba9b38eb4ed6812fbee78c3759247859a`、Terrain Selection Hashは`da0bc86b2bebedb5ba36ad0eb2203d7ed20eb714af6e1396d5b1447bb2791c2c`。

担当RootがPASS3の実PNGを確認し、林道の上下に変化する輪郭と広場前面の拡張を確認した。Cell単位の階段状輪郭は残り、A完成とは判定していない。その後、追加Details12点の実Unity導入・Source/Derivedレビュー、装飾clusterの実配置生成・撮影と保存Settings再現、別実行のActor・Physics・集約Testまで確認した。親側は64pixel単位の大きな土輪郭を引き続きA未達と判定し、PASS5の閉じた輪郭maskを指定した。

PASS3の本物Actor証跡は`study-a-pass3-actor-first.json`。Ready、Failure空、経路端への到達4回、総移動99.05261unit、20.0163441秒、`fixedFrames=1906`、`running=false`、本物`FantasyRoyale.Gameplay.Characters.Unity.CharacterActor2D`を確認した。曲がる経路の20秒走行による4回到達であり、旧Caseの5回を成功条件として流用しない。人間入力・Health・Event経路は検証対象外のままである。

PASS3の最終Physics証跡は`study-a-physics-pass3-verified-anchor.json`。Schema3、`phase=complete`、passed / physicsPassed / cleanupPassedがtrue、Errorなし、1,992 simulation steps、実Map Collider76個である。主導線の正逆38走行と、接続口3lane・両方向6走行を確認した。小物74個の停止は実配置へ接近できる15個と、他Colliderを一時無効にした隔離shape59個を合わせた数である。後者59個のGameplay到達可能性は証明していない。

同Physics証跡のGround384Cell、Collision68Cell、ブロック所有240Cellのcoverageはすべて成功した。ForestBackdrop41個は`Physics2D.SyncTransforms`後の実Collider Boundsと足元anchorをMetadataへ照合し、一致を確認した。初期helperの失敗はQA Schemaと足元anchor読取りの誤りを修正して検査し直した結果で、Game Source・素材の修正は行っていない。初期失敗ログを保持し、素材不良や通行不能が発生した結果へ読み替えない。PASS2の隔離Physicsは未実行の履歴を維持する。

PASS3の実Yソート証跡は`study-a-pass3-y-sorting.json`。本物Actorの北側(4.375,11.5)と南側(4.25,9.5)の明示姿勢で、両方`collisionFree=true`、WorldObjects / Pivotを確認した。QAの`hero-behind-canopy.png`では背面に隠れ、`hero-front-canopy.png`では全身が前面へ表示され、担当Rootが実画素を確認した。2姿勢の表示順と実Collision非接触の検査で、animation・人間入力の検証ではない。

PASS3の恒久証跡は出力`terrain-plazaandpath-seed-20261003-20261003-164136371`のQAにある`actual-character-validation.json`、`physics-play-validation.json`、`y-sorting-visual-validation.json`と上記2PNGである。PASS2の実Actor証跡もその生成Run内QAへ保存した。Details直前checkpointは作業Workspaceの`backup-before-forest-details-r1/pass3-before-detail-checkpoint.zip`、120File、4,058,747bytes、全Entry Hash確認済みである。

Details R1の受領証跡は`study-a-forest-details-receipt.json`、Library File ID `libfile_cec62bd7d26c8191b25ca9f6a7d57e83`、289,054bytes、SHA256 `db68e1244969ff653f803c29a01f98996538faff35bfbfc13bafa28d2ebe7f73`。正規bundled WSL helperの受領と別プロセスでのmetadata保持を確認した。`Input/ForestDetailsR1/`の33Fileをbytes一致で保持し、12点のnative PNGを`DerivedForestDetailsR1/`へ導入した。Importer SHA256は`1ae0994c4b28080e7893f3964b83526bfa932da0e559fbbe1990603350847b3b`。

Detailsの実導入・レビュー証跡は`study-a-details-final-readiness.json`。Source/Derivedレビュー済み、12点、PPU64、Point、native寸法と提供Pivot、全Sprite Physics Shape Count0、Errorなし、Details `Ready=true`、`FullTargetReady=false`である。fingerprintは`de71547607214513e1e69a31de1ded0e8110671d70ebf989c921b8069d908b7d`。素材の内訳は花3、草tuft2、踏み石4、切り株1、草patch2。Library version3はDetails配置前、version4はDetails配置済みの履歴である。現版は閉輪郭・安定描画のversion6。

Details付きの実生成と保存Settings再現は`study-a-details-cases.json`。初回出力`terrain-plazaandpath-seed-20261003-20261003-171518297`、再生成`terrain-plazaandpath-seed-20261003-20261003-171520443`は両方`Generated=true`、Errorなし、Custom地形Cell0、`ForestDetailsEnabled=true`、PNGと素材選択一致、元出力保持である。PNG SHA256は両方`6b763b59c2dd76ec5d97d98cce720ae781cf88f83abf37ede3ae8ec7556f0f71`。Metadataの配置数は計73個、草54・花10・踏み石7・切り株2。Forest Detail Selection Hashは`5057efc15fd33cf8cdaa223aa117bf56fb81ab6df78d480760d1ec8c6ffc31d9`である。Builder SHA256は`f7fb6061096d714cc52894a875ae93e8a64e638abdce64554dd36e42a6572f91`、Settingsは`743c371fbc6c81b141bc9c70a3066a45ecb0805b9ece692bd9e30f48b9545366`で、担当Rootが実Unity compile完了・Errorなしを確認した。

担当RootがDetails付きの実画像を確認し、泉・屋台へ向かう分岐と、局所的な花・境界の草が表示されていることを確認した。親側への早期共有後も、64pixel単位の大きな土輪郭はA未達と判定された。DetailsのPhysics・本物Actor・集約Testは別実行で成功したが、画質完成とは扱わない。

Details付き本物Actorの証跡は`study-a-details-actor-final.json`。Ready、Failure空、端への到達4回、99.25261unit、20.053339秒、`running=false`、本物`FantasyRoyale.Gameplay.Characters.Unity.CharacterActor2D`を確認した。実Sprite照合は`study-a-details-live-sprite-inspection.json`で、配置73個がMetadataと一致し、71個はColliderなし、切り株2個は指定Colliderと一致、Errorなしである。

Details付きPhysicsの証跡は`study-a-physics-details.json`。`phase=complete`、passed / physicsPassed / cleanupPassedがtrue、Errorなし、2,030 simulation steps、実Map Collider78個。主導線と接続口3lane・両方向は通過し、小物停止76個は実配置への自由な接近17個と隔離shape59個を合わせた数である。後者59個のGameplay到達可能性は未証明のまま。元Scene不変、Editor Sceneと時間設定の復帰を確認した。

Details付き集約回帰の証跡は`study-a-details-aggregate-editor-results.json`と`study-a-details-aggregate-playmode-results.json`。Editor 150/150（8.7秒）、PlayMode 4/4（3.9秒）、failed / skipped / inconclusive0である。Details段階のPost-run Editor復帰・6恒久QA保存とPASS5直前backupも完了した。その後のPASS5実検証は次段に分けて記録する。

PASS1の実画像`terrain-plazaandpath-seed-20261003-20261003-145202284-1536x1024.png`は、Library File ID `libfile_659bc5e0b85c8191a4e49dc4b2bec429`のversion1へ保存した。証跡`study-a-forest-terrain-library.json`で保存成功とWSL metadata適用Exit0を確認した。その後、PASS2の`terrain-plazaandpath-seed-20261003-20261003-155946314-1536x1024.png`をversion2、PASS3の`terrain-plazaandpath-seed-20261003-20261003-164136371-1536x1024.png`をversion3、Details付きの`terrain-plazaandpath-seed-20261003-20261003-171518297-1536x1024.png`をversion4へ同じIDで置換保存した。version2は`study-a-pass2-library.json`、version3は`study-a-pass3-library.json`、version4の証跡は`study-a-details-library.json`。閉輪郭のversion5は`study-a-fine-soil-library.json`、安定描画の現version6は`study-a-stable-fine-soil-library.json`。現画像は親側へ共有済みで、Library保存の成功はA品質の承認と分ける。

### PASS5の最終検証構図と証跡

代表出力は`terrain-plazaandpath-seed-20261003-20261003-184127731`。Sceneは`Assets/Scenes/MapAuthoring/OverheadStudyA/Blocks/{出力ID}.unity`、保存SettingsとCamera PNG・report・恒久QAは`Assets/Art/Map/OverheadStudyA/BlockStudies/{出力ID}/`にある。24x16world、Camera8、1536x1024、PlazaとForestPathの2ブロック。保存設定はSeed20261003、CompositionVersion2、GroundR2、ForestBackdrop / ForestDetails / FineSoilContourが有効である。

閉輪郭の原出力`175843443`とそのNative Prefabを保持した。現Settingsはこの原Prefabを参照する。`Prefabs/Plaza.prefab`と`Prefabs/ForestPath.prefab`のRootにある`StudyASoilContour`のPointsとPixelStepをInspectorで編集できる。8 / 16pixelの階段で土面のalphaだけを切り、原R2 textureのRGB・倍率・PPUを変更しない。構造Walk / Road / Solidと見える土輪郭は別契約で、実Actorの足全体を主導線・接続3laneの両方へ含める。全体のEnableFineSoilContour有効時のWater / Bridge / BridgeFloor / Stoneは未対応なので出力前に拒否する。後述の橋専用Fine設定とは別契約で、旧Fine=falseは維持する。

`study-a-stable-fine-soil-cases.json`は3回の実生成・撮影と保存Settings再読込の証跡。3PNGのSHAは`d08fe390a07296f508c98d7459b87294957660dbe3f501d23c1ef7fdcbd8c058`、Layout Hashは`2b7d69f58e5bbcf8b4d9ef3edd5527d4a5c63351e75529e416ee50f09cac3b45`、Fine maskは`9471d8ba60390b2e0bfb2c52ce9e6a2a5a73f1bac0d6b809492adccafdad0e69`。354,112不透明pixel。元の閉輪郭PNGとSceneも保持した。

閉輪郭初回の再読込で発見した2,199pixel差の失敗証跡は`study-a-fine-soil-final-reproduction.json`と`pass5-contours/reload-tie-order/independent-scene-audit.json`へ残す。原因は等足元Yの木・低木・岩3組の内部描画順で、原RGBの勝者反転が全差分を説明した。Fine有効時の重なる同Yだけ、1Spriteごとの透明SortingGroupの描画anchorに決定的な微小biasを付けて修正した。WorldObjects / order0 / Pivotと実Sprite・Collider世界位置は保持し、block全体のgroup化は行わない。22anchor、最大bias0.00244140625unit。Actorとの前後判定の境界はこの微小bandで変わり得るため、全Yで元の閾値と完全一致するとは扱わない。旧R1の3,022pixel再生成差は未解決の履歴として維持する。

`independent-stable-pose-audit.json`は実保存Scene3件を元と比較し、175Rendererのnative画像・材質・色・描画属性とworldfloat32姿勢、76BoxColliderのlocal/world形状、76障害物markerが差分0であることを示す。`independent-tie-numeric-audit.json`は元の異なるY4,157組の順序保持を確認した。実PNGの全RGBA独立検査と生産maskの連続足元検査96/96も成功し、現Fine soil PNGのbytesはその検査対象と同一である。

`study-a-stable-fine-soil-movement-result.json`は本物Actorの20.0004158秒・5回到達・98.04995unit。`study-a-stable-fine-soil-physics.json`は2,014steps・Map Collider78個・passed / physicsPassed / cleanupPassed、Error0。小物停止76個のうち、実配置への自由な接近17個と隔離shape59個を区別する。隔離59個のGameplay到達可能性は未証明である。土Sprite・Details83個・個別anchor22個の実Unity照合と、衝突しない2姿勢の前後Camera PNGも確認した。

最終別seedは`terrain-plazaandpath-seed-17-20261003-185241585`と`terrain-plazaandpath-seed-20261004-20261003-185243925`。実画像を確認し、各Physics2,014steps・Error0・cleanup成功。`study-a-stable-fine-seeds.json`と`study-a-stable-other-seeds-physics-summary.json`が証跡である。最終全回帰は`study-a-stable-fine-soil-aggregate-editor-results.json`の150/150と`study-a-stable-fine-soil-aggregate-playmode-results.json`の4/4、failed / skipped / inconclusive0。既存通常Exploration Testは成功したが、手動の通常起動・Player Build・人間入力・実戦を実行したことにはしない。

変更前の復元用ZIPはWorkspaceの`backup-before-fine-soil-contours/completed-details-source-scene-art-qa.zip`（1,322File、37,989,997bytes）と`backup-fine-soil-camera-checkpoint/fine-soil-source-scene-art-qa.zip`（1,667File、49,725,297bytes）。全Entry SHA検証済み。元の689保護Fileを維持し、元の未commit差分へ変更を加えていない。最終証跡は代表RunのQAへ保存する。

### ユーザーによる表示と再生成

最短の試し方は`Assets/Scenes/MapAuthoring/OverheadStudyA/StudyABlockStableFineSoilMovementDiagnostic.unity`を開いてPlayする。保存されたMap Scene`184127731`を読み込み、本物Actorが20秒自動走行し、HUDで到達回数・移動・Failureを表示する。WASD操作の検証Sceneではない。静止構図だけを見る場合は同出力のMap Sceneを開く。

輪郭を変えるときは保存Settingsが参照する上記Native PrefabのRootで`StudyASoilContour.Points`と`PixelStep`を編集して保存する。接続口付近の3unit帯と主導線の足元保護を維持し、保存Settingsを選択して生成Menuを実行する。変更はtimestamp付きの新出力として保存され、元Scene・PNGを上書きしない。Seedだけを変える場合も同SettingsのSeedで指定できる。

1. Playを停止し、開いているSceneを保存済みclean状態にする。無名Sceneや未保存変更がある状態ではBuilderが生成を拒否する。
2. Projectで`Assets/Art/Map/OverheadStudyA/BlockStudies/{出力ID}/StudyABlockSceneSettings.asset`を選ぶ。`Case`、`Seed`と必要なPrefab参照を確認する。代表は`PlazaAndPath`、Seed20261003、Camera8。限定配置は`EnableLimitedVariation`、`VariationProbability`、`MaxVariablePropsPerBlock`で設定する。現閉輪郭版の再現には出力`terrain-plazaandpath-seed-20261003-20261003-184127731`の保存Settingsを使い、`EnableFineSoilContour=true`、`EnableForestDetails=true`を確認する。古い保存SettingsがfalseならDetailsは追加せず、Prefab未指定の新規入力ではレビュー済みDetailsがある場合に有効化してtrueを保存する。
3. `FantasyRoyale/Map Authoring/Overhead Study A/Generate Selected Block Study`を実行する。Starterの原本・派生レビューと現在のHashが一致し、`StarterPropsReady`、`ApprovedTerrainReady`、`HeroReady`が揃う必要がある。Details有効時はそのSource/DerivedレビューとHashも一致している必要がある。不足時はPrefab、Settings、Scene、QAの書込み前に拒否する。生成成功時は`terrain-`を先頭にしたtimestamp付きの新しい出力先へ保存し、既存Prefabを上書きしない。
4. Consoleに出たScene PathをProjectから開いてCamera表示を確認する。PNGとreportは同じ出力IDのQAフォルダーで確認できる。生成後は元のActive Sceneへ戻るため、新しいSceneを明示的に開く。

生成Map内Heroは`ConceptScaleHero_StaticDiagnostic`という静止比較表示である。Map SceneをPlayするだけでWASD操作できるとは扱わない。

実Actorの自動走行は診断Sceneを開き、Unity EditorでPlayする。PASS3は`Assets/Scenes/MapAuthoring/OverheadStudyA/StudyABlockPass3MovementDiagnostic.unity`で、保存された`MapScenePath`はPASS3の出力`terrain-plazaandpath-seed-20261003-20261003-164136371`を指す。PASS2は`Assets/Scenes/MapAuthoring/OverheadStudyA/StudyABlockPass2MovementDiagnostic.unity`である。診断Componentに保存された`MapScenePath`と`Route`の対象を読み込み、静止Heroを抑制して本物Actorを生成する。旧PlazaAndPathの診断Sceneは`Assets/Scenes/MapAuthoring/OverheadStudyA/StudyABlockExplorationDiagnostic.unity`、旧PathAndBridgeBankは`Assets/Scenes/MapAuthoring/OverheadStudyA/StudyABlockBridgeMovementDiagnostic.unity`である。確認済みの走行条件はMoveSpeed5、MaxRunSeconds20、AutoTraverseとReverseAtEndがtrue。実行前に`MapScenePath`を確認し、別版の診断Sceneを起動した結果を現版の検証と混同しない。HUDで経路、端への到達回数、総移動距離、FrameとFailureを確認し、終了後にPlayを停止する。Editor専用の自動Command診断で、人間入力操作やPlayer Buildの実行方法ではない。

## 完了結果

閉輪郭版の2ブロック検証を完了した。実Unity描画、保存設定の再読込を含む3PNG完全一致、実CharacterActorの20秒・5回到達・98.04995unit、代表と別2seedの接続Physics各2,014steps、実Sprite・Collider姿勢保存、前後Yソート、全Editor150/150・PlayMode4/4を確認した。現画像は同じLibrary IDのversion6へ保存した。生成時Metadataの`StructuralOnly=true`・`FullTargetReady=false`・`ActualActorWalkVerified=false`は変更せず、後の実Actor・Physicsは別QAで記録する。689元Fileと未commit差分、旧Scene・Prefab・PNG・Settingsを保護し、変更前ZIPを全SHA検証した。親側は閉輪郭版を2ブロックの検証構図として確認したが、A品質の完成とは扱っていない。水・橋の仕上げ、正式Hero・animation、人間入力・実戦、Enemy専用許可域・抽選契約、Player Buildと手動の通常起動は残る。隔離shape59個のGameplay到達可能性と旧R1の3,022pixel再生成差も未解決の制約として保持する。Study全体はactiveのままとする。

受領したスターターZIPは6素材の加工版とReference Aを含む。加工版は大木256x256、中木192x192、低い泉128x128、屋台192x160、低木96x64、岩96x64で、仮PPU64・整数縮小factor1から診断できる。受領ZIPは3,698,657bytes、SHA256 `bc1f7eb1e8b6dfd38686a2d6af5065c900f1a4f2cb017eb7b0164c2345e7e4e1`が期待値と一致した。安全展開先は`Assets/Art/Map/OverheadStudyA/Input/StarterPackage/`。初回受領時の不足のうち地形とHero原型は補足ZIPで、花はDetails R1で提供・導入された。水・橋は初回には未提供で、現在は後述のBridgeRiverV1を受領している。手動保存の継続pollingは行っていない。当初のLibrary入力2画像は追加の正規WSL取得で確認済みとなり、このStudyの主ReferenceはA-overhead-vividで維持する。

ユーザーは今回生成した素材の透過縁・pixel規格処理を承認した。生成原稿の削除許可は今回の原稿だけに限り、選択したAコンセプト、既存76Sprite、既存資料の削除や置換を許可するものではない。この準備作業では素材を削除していない。


## 手動Study入口の実入力検証（2026-10-03）

確定済み閉輪郭2ブロックを保持し、`Assets/Scenes/MapAuthoring/OverheadStudyA/StudyABlockManualExploration.unity` を追加した。既存の `Assets/InputSystem_Actions.inputactions` は変更せず、実行中の複製の `Player/Move` → 既存 `HumanCharacterMoveInputAdapter` → `CharacterMoveCommand` → 本番 `CharacterActor2D` の経路を使う。`UseHumanInput=true`、`AutoTraverse=false`、MoveSpeed5。元の自動Sceneは手動入力を有効にせず、20秒停止と5回通過を維持した。通常起動のBuildSettingsは変更していない。

### 自分で歩く最短手順

1. UnityでPlayを停止し、開いているSceneの編集を保存する。
2. `Assets/Scenes/MapAuthoring/OverheadStudyA/StudyABlockManualExploration.unity` を開く。
3. Playを押してGame画面をクリックする。WASD／矢印キーで移動し、離すと停止する。画面を離れると停止し、戻ったら一度キーを離してから再開する。

手動Sceneは保存Map `terrain-plazaandpath-seed-20261003-20261003-184127731` を読込み、比較用の静止Heroを抑制して本番Actorを生成する。Map Scene単体を開くだけの場合は従来どおり静止比較表示である。この入口はEditor専用で、BuildSettingsには追加していない。ゲームパッドの左スティックも既存Bindingを保持するが、実コントローラーは未確認。

### 実行した確認と制約

HomePCのフォーカス中のUnity Game画面へWin32 SendInputで実OSキーイベントを送り、native RawInputの通常Unity UpdateでW/D・矢印押下、Player/Move値、Actor命令を記録した。入力デバイスの状態、Actorの命令・座標をQAハーネスから書き換えていない。右移動・斜め正規化・矢印・解放後停止を確認し、木の背後(4.314213,11.014227)から迂回して手前(4.314213,9.014219)へ実キーだけで歩いた。両画像を実画素で確認した。切り株に向けた下入力中は(7.914209,5.263124)で停止し、追加入力でも移動0。接触距離約-0.005unitは実Physics solverの接触許容であり、接触Colliderの重なり0と扱わない。

キー保持中のGame画面フォーカス喪失で命令0・clone破棄、非フォーカスのdisable→enable後の復帰、2回のニュートラル読取りを確認した。手動モードは20秒を過ぎても継続する。最終Source SHA `577489953c89451cdae99c29a10f18c7c80dbe51de5db39d2bac7ce983c29194` は表示文言のみ追加した版で、同版でも実キー移動・解放停止を再確認した。全Editor150/150・PlayMode4/4、failed/skipped/inconclusive0。元の自動Sceneは本番Actorで20.001175秒・5回到達・98.04995unit、手動cloneなしで停止した。

実入力・表示・回帰の証跡は `Assets/Art/Map/OverheadStudyA/ManualWalkQA/20261003-real-os-input/` に保存した。入口画像のLibrary IDは `libfile_6c651dcb325c819198e68e5637b2af92`。物理的な人によるキー操作、実ゲームパッド、対戦・Player Build・手動の通常ゲーム起動は実行していない。元InputActionAsset・BuildSettings・確定版Sceneと自動SceneはSHA一致を確認した。目標Aの画質完成は引き続き未達である。

### 橋岸の次段階

新しい5素材runtime ZIPを正規Library経路でHomePCへ受け取り、24,408bytes・SHA `978954cf5320cae7c8f422d6f8c60f40c4c3129d79d67aad836b974836d42a3a` の一致とnative5画像を確認した。水64x64、橋床256x192、柱16x48、岸大64x64／小32x48。素材は実Unityの画質合格を意味しない。確定版を保持して別の明示設定で橋岸単体→林道接続の最大2ブロックを検証する。初回macro版は水路の行shiftを維持した見た目の確認で、後述のFine版では閉輪郭を表示と進入禁止Colliderの共通正本に変更した。macro版のScene・PNG・保存Settingsは保持する。

### 橋岸Fine単体の実生成と確認（2026-10-03）

代表Fine単体は `terrain-bridgebank-seed-20261003-20261003-211607747`。Sceneは `Assets/Scenes/MapAuthoring/OverheadStudyA/Blocks/{出力ID}.unity`、保存SettingsとPNG・reportは `Assets/Art/Map/OverheadStudyA/BlockStudies/{出力ID}/` にある。Seed20261003、`Case=BridgeBank`、GroundR2、CompositionVersion2、Camera8、1536x1024。`EnableBridgeRiverArt=true`、`EnableFineBridgeRiverContours=true`、`EnableFineSoilContour=false`、ForestBackdropとForestDetailsはtrueである。生成成功・Error0と実Camera画像を確認し、画像は橋岸用Library ID `libfile_485130552e8481918678ef3daed0da3f` のversion1に保存した。PNG SHAは `4a8ed0fdff44ce3f1d11ca44e3db420488ca197982878b0edd98c637562f3a7c`。閉輪郭の確定版Library IDのversion6は変更していない。

BridgeBank Native Rootの `StudyABridgeRiverContour.Points` と `PixelStep`（8／16）を水面の正本とし、同じ二値maskから水Spriteと水Colliderを作る。表示水は橋の下にも残し、水Colliderだけから4x3unitの橋床全域を差し引く。したがってVisibleWaterとWaterCollisionのHashは同値ではなく、橋床差分を除いた形状対応を検査する。旧macroの水18Cellを退役させ、Solid34Cellと接続の構造情報を保持した。北・南の水接続口は2unit、橋床と歩行帯は3unit。岸は8～20pixelの非対称な幅、橋への土は別の閉輪郭で生成し、水Collider部分を除外する。受領した水・橋床・柱・岸大小のNative素材とR2土のRGBをPPU64／Point／Full Rectで使用し、新規の泡や影textureは追加していない。

実Actorの自動走行入口は `Assets/Scenes/MapAuthoring/OverheadStudyA/StudyABridgeRiverFineSingleAutomaticWalk.unity`。本番 `CharacterActor2D` が20.0007362秒で端へ9回到達し、総移動99.99898unit、Ready=true、Failure空、running=falseを確認した。証跡はWorkspaceの `study-fine-bridge-single-actual-actor-result.json`。柱の前後表示は走行後のActorを2姿勢に置く別診断で確認し、`study-fine-bridge-post-behind.png` と `study-fine-bridge-post-front.png` の実画素で足元Yに応じた前後を確認した。姿勢変更は人間歩行の証拠ではない。終了時に元位置(16.5,7.5)・速度・診断Component有効状態を復元し、Sceneを保存していない。

独立Physics QAのpass2は `study-fine-bridge-single-physics-pass2-report.json`。実保存SceneのColliderに対し、本番と同寸法の新しいDynamic Rigidbody2D／Foot Capsuleを独立PhysicsScene2Dで使用した。24,576点のwater対応検査は不一致0、Solid34Cell保持、橋の3laneを両方向に通る6試行、南北の水への進入停止、柱4本の足元Collider契約を個別に確認した。一方でCamera判定1件と橋床内のFoot Capsule検査2件が失敗し、`passed=false`／`physicsPassed=false` を保持する。上段2点はnative接触距離約-0.005unitで、12箇所の静的Capsuleが水Colliderへ完全に無接触という厳密条件は未成立である。橋床やmaskを拡大せず、QAの条件も緩和しない。検査SceneとActorの除去、元Scene・active Scene・Component状態・時間設定と357入力Fileの不変は確認され、`cleanupPassed=true` である。水停止とlane成功をQA全体の合格に読み替えない。旧失敗報告も保持し、新しい接続版の検証は別の実行結果として記録する。

親側はFine版の実画像で64pixel単位の直角水路が解消し、左右非対称の方向が改善したと確認した。右半分の草地は空白が多く、岸パッチ2個では周囲へのつながりが弱い。このため次段階は既存素材だけで右上・右下へ中木／低木／花／岩の小群を加え、岸大小6個を非対称な3群へ増やす。共有の水表示／Collision輪郭、2unit水接続口、3unit橋歩行帯を保持し、既存ForestPathとの最大2ブロック接続画像を次の画質Gateとする。単体の装飾調整だけでは完了としない。

### Landscape V2と既存林道の2ブロック接続

新規代表は `terrain-pathandbridgebank-seed-20261003-20261003-220953821`。Scene・保存Settings・QAの置き場所は上記と同じ規則で、`Case=PathAndBridgeBank`、Seed20261003、`BridgeRiverLandscapeVersion=2`、`EnableFineSoilContour=false`、BridgeRiverArtとFineBridgeRiverContoursはtrueである。既存確定版 `184127731` が参照する `175843443/Prefabs/ForestPath.prefab` を再利用し、新しいBridgeBankだけを生成した。生成は成功・Error0、Camera PNGは1536x1024／1,902,253bytes／SHA `d5a90e964b197cf691d49a5ded44ae3a09a5854f9b2c1711d3886f7052dc1ba4`。Rootが実画素を確認し、林道と橋への土が連続し、右上・右下の中木と低い小物群、岸大小6個が周囲へつながると判断した。画像は橋岸用の同じLibrary IDのversion2へ保存し、親側へ共有した。目標Aの完成とは判定していない。

新鮮なFine Bridge入力ではLandscape2を保存し、旧Fine Nativeを指定するversion0はLandscape1として従来配置を保持する。Landscape2では右草地に小群を加え、岸大小パッチ6個を橋から離れた非対称な3群へ配置する。既存林道Rootの `StudyASoilContour` と橋への土の閉輪郭を合わせ、共有水Colliderを除いた土maskを出力する。reportの `PartialFineForestPathEnabled=true` と `BridgeRoadPolygons` 2個を確認した。全体の `EnableFineSoilContour` はfalseを維持し、従来の水・橋に対するglobal Fine soilの拒否条件を解除していない。接続の再現には新規設定から組み直すのではなく、この接続出力の保存SettingsとそのNative Prefab参照を使う。

Cameraのtransparency sort設定がUnity再読込み後にDefaultへ戻ることを確認したため、新しいFineBridge出力だけへ `StudyACameraYSort` を付け、OnEnableでそのCameraのCustomAxisと上向きY軸を設定する。全体の描画設定を変更せず、確定閉輪郭版と旧Fine=falseの生成経路を保持する。接続版の保存Scene再読込み後の実CameraでCustomAxis／(0,1,0)を確認した。

接続版の自動走行入口は `Assets/Scenes/MapAuthoring/OverheadStudyA/StudyABridgeRiverFinePairAutomaticWalk.unity`。実 `CharacterActor2D` が20.0010815秒で端へ5回到達し、総移動99.02621unit、Ready=true、Failure空、running=falseを確認した。証跡は `study-fine-bridge-pair-actual-actor-result.json`。`study-fine-bridge-pair-physics-report.json` は349simulation steps、24,576点のwater対応不一致0、Solid64Cell保持とCapsule失敗0、橋3lane両方向6試行と南北水停止2試行が成功、柱4本のanchor／Collider／足元Yソート契約が一致した。静的floor Capsuleは(15,8)と(16,8)の約-0.005unit接触だけが残り、`passed=false`／`physicsPassed=false` を保持する。入力385File、元Scene・active Scene・Component状態・時間設定のfloat bitsは復元され、`cleanupPassed=true`。全体QA成功とは扱わない。

接続版の柱前後も実Actorの2つの制御姿勢で撮影し、Rootが `study-fine-bridge-pair-post-behind.png` と `study-fine-bridge-pair-post-front.png` の実画素を確認した。終了時にActor位置(18.9999981,7.5)・速度・診断有効状態を復元し、Sceneはcleanのまま保存していない。制御姿勢は人間入力や経路歩行の証拠には数えない。別の実OSキー検査では通常Native Keyboard→Runtime Action Clone→本番Actorの経路を通し、Y6.75でX2.5→20.60004→7.499995へ往復し、解放後の命令0を確認した。元InputActionAssetのfileは変更していない。

保存Settingsの再現検査は初回に検査器のMetadata読込みで出力前に停止した。読込みを修正したpass2は生成成功、mask・Layout・選択Hashと元File／Scene不変が一致したが、左下の岩と森の描画順で1,298RGBA pixelが異なった。異なる3保存Pairをfreshに開いてSortingGroupのcacheを更新すると、PNG／RGBAが一致した。新しいFineBridge生成時だけ撮影前のcache更新を加え、描画の正本を保存Sceneの再読込みと揃えた。失敗した旧PairのScene・PNG・報告は保持する。確定閉輪郭版のFineBridge=false再現は当時の `d08fe3…` に対し3,009RGBA pixel差が残り、元FileとFine maskを保持して失敗報告を残す。最新再現も当時3回一致したとの記述はしない。cache修正後の実Unity回帰も全Editor150/150・PlayMode4/4が成功した（`study-fine-bridge-sort-sync-editor-tests-result.json`／`study-fine-bridge-sort-sync-playmode-tests-result.json`）。EditorはPlay停止・compiling=false・runInBackground=false・元Template1Scene・cleanへ復帰した。この結果だけで3ブロック版の回帰成功とは扱わない。

cache修正後の代表は `terrain-pathandbridgebank-seed-20261003-20261003-230430688`。保存されたNative Prefab参照と上記Landscape2の設定・geometryを維持し、Rootが実画素を確認した。代表と保存Settingsからの2再生成 `230518753`／`230600673` は、PNG SHA `aac2557622ad100d188ff100dc4f42f5fd0c67c68bb8cfcd6b36f9bf13a68312`、decoded RGBA、Layout、全選択Hash、水表示／Collision・FreeWalk・土maskが一致し、元FileとEditor Scene状態も保持した。証跡は `study-fine-bridge-pair-synchronized-reproduction-1.json` と `-2.json`。通常再読込2回（各回で開く呼出しと撮影呼出しを分け、明示的cache更新なし）でPNG/RGBA差0だった（`study-fine-bridge-pair-synchronized-reload-pixel-check.json` と `study-fine-bridge-pair-synchronized-reload-2-pixel-check.json`）。新代表のScene・保存Settings・QA Pathは上記と同じ出力ID規則である。現時点のLibrary橋岸version2は旧 `220953821` の画像を保持しており、新代表のLibrary保存は未実行である。旧代表で確認した実Actor・OSキー・Physicsを新Sceneで再実行したことには数えない。

別seed17の `230721975` とseed20261004の `230724823` も実生成し、RootがPNGを実画素で確認した。保存Settings・入力File・Editor Sceneを保持し、固定geometryのHashが一致した（`study-fine-bridge-pair-two-seeds.json`）。この2seedは生成と画像確認だけで、Physics・本番Actor・OSキーを検証済みとはしない。生成時の `physicsVerified`／`actualProductionActorVerified`／`realOsKeyboardVerified`／`pixelReviewed` falseは後の別確認結果で書き換えない。

親側はこの接続画像を一続きの風景として確認し、22:09のレビューで既存NativeのPlaza→ForestPath→BridgeBankの3ブロック検証を次段階として承認した。広く均一な土帯、水面の密度、森林の反復、影とDetailsは引き続きAとの画質差である。3ブロックでは構造確認用の全景と、通常Camera size8による各接続の画像を分け、各接続の密度を通常Cameraで判断する。新しい保存Settingsと手動入口で進め、通常起動・Player Buildの完成とは扱わない。3ブロック開始前に修正後Pairのcheckpointを確定した。3ブロックの実生成・表示・実OS歩行・再現・導入後回帰は下記に記録する。

### 橋岸の表示と同設定の再生成

1. UnityでPlayを停止し、開いているSceneとPrefabの編集を保存する。未保存Sceneがある場合はBuilderが出力前に拒否する。
2. 単体の表示は上記 `211607747` のMap Sceneを開く。実Actorの確認は `StudyABridgeRiverFineSingleAutomaticWalk.unity` を開いてPlayする。Map Scene内の比較Heroは静止表示で、WASD入口ではない。
3. 同じ単体を再生成する場合は `Assets/Art/Map/OverheadStudyA/BlockStudies/terrain-bridgebank-seed-20261003-20261003-211607747/StudyABlockSceneSettings.asset` をProjectで選択し、上記Case／Seed／Fineの設定と保存されたNative Prefab参照を確認する。`FantasyRoyale/Map Authoring/Overhead Study A/Generate Selected Block Study` を実行すると、新しいtimestamp付きScene・PNG・report・保存Settingsを生成し、元出力を上書きしない。
4. Consoleに出た新Scene Pathを開き、同出力のQA PNGとreportを確認する。生成後のActive Sceneは元へ戻る。Seed変更は保存Settingsの `Seed`、水の変更は参照Native Prefabの `StudyABridgeRiverContour.Points`／`PixelStep` で指定する。編集前に当該Prefabとmetaをbackupし、同じPathで編集する。生成時のNative preview PNG／meta／GUIDを出自検査するため、別Pathへの複製や移動では再生成できない。preview PNGは元生成時の記録で、編集後の輪郭の正本はPointsである。2unit水接続口・3unit橋歩行帯・Solid保護を維持する。規格外の設定や水とSolidの競合は出力前に拒否する。

修正後の林道接続を再現する場合は `Assets/Art/Map/OverheadStudyA/BlockStudies/terrain-pathandbridgebank-seed-20261003-20261003-230430688/StudyABlockSceneSettings.asset` を選び、同じ生成Menuを実行する。`Case=PathAndBridgeBank`、Seed20261003、`BridgeRiverLandscapeVersion=2`、Camera8、FineBridgeとArt=true／global FineSoil=false、保存されたForestPath・BridgeBank Native参照を維持する。別seedはSettingsを複製してSeedだけを変更する。元のSettingsとNative Prefabを上書きしない。

### 復元点と既存作業の保護

確定閉輪郭 `184127731`、macro橋岸 `201913360`、手動入口 `StudyABlockManualExploration.unity` と元InputActions／BuildSettingsは保持する。Fine導入直前の復元用Workspace ZIPは `backup-completed-macro-bridge-checkpoint/macro-bridge-source-scene-art-qa.zip`。2,188File、76,329,155bytes、SHA `84511e58618719f2566fa4658cbe8b20b9f17155817bb9de309a407e4266ca08`、全Entry Hash検証済みである。これはmacro段階の復元点である。3ブロック導入前に修正後Pairの復元点 `backup-completed-fine-bridge-pair-checkpoint/fine-bridge-pair-source-scene-art-qa.zip` を確定した。2,900File、105,275,232bytes、SHA `c071232c772f36e92435380113a38f535865689813f626ed469eb9fba12e4551`、全Entry Hash検証済み。Pair確定時点のSource・art・Scene・QA・この文書・Packages・ProjectSettingsを対象とする復元用ZIPで、Repo全体のbackupではない。3ブロック導入後の変更までこのZIPに含まれているとは扱わない。元689保護Fileと既存の未commit差分を維持する。中間の更新は `source-deltas/` の変更Source／metaだけのZIP・manifest・RESTORE.txtで追跡し、全量ZIPは確定checkpointに限る。既存backupは削除しない。

柱前後の補助画像はWorkspaceに保持している。初回単体2枚の正規Library保存手順は実行環境で失敗し、補助画像の保存成功File IDは得られていない。追加再試行は行わず、上記の主画像version1／version2の保存成功とは分ける。Study全体の `StructuralOnly=true`／`FullTargetReady=false` を維持し、正式Hero／animation・Enemy契約・対戦・Player Buildと通常ゲーム起動の完成とは扱わない。


最新PairのQAは `Assets/Art/Map/OverheadStudyA/FineBridgeQA/20261003-connected-native/synchronized-pair` に39File・7,027,912bytesを保存済み。Workspaceの `study-fine-bridge-pair-checkpoint-summary.json` とpreservation証跡は689保護File不変、今回SourceのSHA一致、InputActions／BuildSettings不変を記録し、厳密床Capsule判定のfalseとA未完成を保持する。

## 既存Native 3ブロックの接続と通常Camera（2026-10-03～04）

Pairのcheckpoint確定後、既存NativeのPlaza→ForestPath→BridgeBankを新しいCase `PlazaPathAndBridgeBank`（enum値5）で接続した。代表は `terrain-plazapathandbridgebank-seed-20261003-20261003-232117617`。世界32×16、各幅12／8／12、原点(0,2)／(12,2)／(20,2)、Spawn(6.5,7.5)。PlazaとForestPathは既存Native `175843443`、BridgeBankは修正後Pair `230430688` のPrefabを参照し、3入力の元File／Metadata／Sceneを保持した。生成報告 `study-three-block-first-build-report.json` はGenerated=true、guard成功、Errors0、保護154File不変を記録する。Seed20261003、GroundR2、Composition2、BridgeRiverLandscape2、BridgeArtとFineBridge=true、global `EnableFineSoilContour=false`。3本の土閉輪郭を結合し、表示土をwater collisionから除外する。PartialFinePlaza／PartialFineForestPath=true、指定Nativeのcustom terrain 6Cellも保持した。旧5Caseと旧2ブロック出力は変更しない。

構造確認用の `QA/terrain-plazapathandbridgebank-seed-20261003-20261003-232117617-overview-2048x1024.png` はSHA `41070c1e7fc01d15750e7042c9932f5c673c1dc9f1af3018b41c3ed7cc831acd`。通常比較用の `-gameplay-1536x1024.png` はSHA `e1d16b12484f3d57f248f9b92a1c907a0c5ccebd03266119efae87ef5f02701f`。Rootが両画像の実画素で3ブロックの一続きの地形を確認した。全景2048×1024はCamera size8／aspect2の構造用で、MainCameraはsize8／aspect1.5の通常比較表示を維持する。広い均一土帯・水面、反復する森、影とDetailsの品質差は残る。Metadataの `StructuralOnly=true`／`FullTargetReady=false`／`ActualActorWalkVerified=false` を維持し、A品質や実歩行の完成と混同しない。

専用の手動入口 `Assets/Scenes/MapAuthoring/OverheadStudyA/StudyAThreeBlockNormalPlay.unity` を作成した。`UseHumanInput=true`／`AutoTraverse=false`／`FollowActorCamera=true`、MoveSpeed5、元InputActionsのruntime cloneから本番 `CharacterActor2D` を動かす。Camera size8を維持し、実Body.positionを追従してNative assembly bounds (0,2,32,12) でclampし、既存Exploration Previewと同じ1/32unitでsnapする。縦16unitのCamera視野よりNative boundsの縦12unitが小さいため、縦Camera中心は8に固定される。これは既存Previewに基づくEditor用Study入口で、正式ゲーム完成ではない。

操作入口はこの専用Sceneを開く→Play→Game画面をクリック→キーを離してからWASDまたは矢印で移動する。再生成は `Assets/Art/Map/OverheadStudyA/BlockStudies/terrain-plazapathandbridgebank-seed-20261003-20261003-232117617/StudyABlockSceneSettings.asset` を選択し、上記 `Generate Selected Block Study` を実行する。3Native参照、Case5、Seed20261003、Camera8とFine設定を維持した新runへ、構造全景と通常比較画像を保存する。seedを変える場合はSettingsを複製してSeedだけを変更する。専用手動Sceneはこの代表Metadataを参照するため、生成メニューで別seedの手動Sceneまで更新されたとは扱わない。

手動Sceneの実OSキー往復を完了した。Win32 SendInput→Native Keyboard→runtime Action clone→本番CharacterActor2Dを通し、検証器からActor命令・Unity Input状態・Transformを書込んでいない。y=7.5で開始x6.5から第1接続の撮影位置12.8000164、第2接続21.60005、橋上26.0000668、右岸28.8000774へ進み、22.1000519→15.3000259→8.6の広場側まで戻った。各キー解放後の命令は0、Failure空。最終実状態はReady=true、manual／Follow=true、running=true、Input有効、neutral待ちなし、移動命令0、総移動56.0997467unitである。証跡はWorkspaceの `study-manual-native-three-forward-{01,02,03,04,on-bridge,right-bank}.json`、`study-manual-native-three-first-connection-return.json`、`study-manual-native-three-reverse-{01,02,03}.json` と `study-three-block-normal-play-final-live.json`。これは実OSキー自動操作の証跡で、物理的な人間操作や実ゲームパッドの確認ではない。

両接続・橋上・右岸の実Actorを写したWorkspaceの `study-three-normal-{plaza-path,path-bridge,on-bridge,right-bank}.png` はRootが全4画像の実画素を確認した。各撮影は1536×1024／aspect1.5／Camera size8で、実行中GameViewのaspect1.77777779へ戻した状態を個別JSONに記録する。実行中のCameraはsize8、CustomAxis/up、clamp済み中心X14.21875～17.78125、Y8、1/32snapを維持した。全景の画面だけで密度を判断せず、通常倍率の両接続を確認した。3ブロックのこの往復を、旧Pairの厳密floor Capsule接触falseや24,576点／6laneの別Physics検査が新Sceneでも合格した証拠には流用しない。

保存Settingsからの再生成2回は `study-three-block-saved-reproduction-1.json`／`-2.json` に記録した。両回ともGenerated=true／passed=true、全景と通常画像それぞれのPNG fileとdecoded RGBA、Layout、素材選択、各Fine mask、3Native assemblyが一致し、元Fileと読込済みScene状態を保持した。同じ代表Map Sceneを通常再読込し、開く呼出しと次の撮影呼出しを分けた検査も、明示的cache更新なしで両画像のPNG／RGBA差0（`study-three-block-static-reload-pixel-check.json`）。手動Sceneを開き直してPlayすると、Ready=true、初期位置(6.5,7.5)、Camera8／CustomAxis/up、Native bounds、FollowとInput有効、移動命令0を保持した（`study-three-block-normal-play-reload-camera.json`）。再起動後の実RIGHT矢印キーでもx6.5→8.299999へ移動し、解放後の命令0（`study-manual-native-three-reloaded-arrow-right.json`）。終了後はPlay停止、元Template Scene1本・clean、runInBackground=falseへ戻した（`study-three-block-normal-play-restored-template.json`）。

3ブロックSourceと手動Follow入口を導入した後の全Editor150/150・PlayMode4/4が成功し、failed／skipped／inconclusive0を確認した（`study-three-block-final-editor-tests-result.json`／`study-three-block-final-playmode-tests-result.json`）。旧Pair時点の結果の流用ではない。今回の実入力・画像・再生成・回帰証跡は `Assets/Art/Map/OverheadStudyA/ThreeBlockQA/20261004-normal-play/` に保存した。payload 35File・8,436,911bytesと `manifest.json` があり、全コピーSHA一致を確認した。A全体の画質完成、正式Hero animation、実人間／gamepad操作、対戦、Enemy許可域・抽選契約、通常ゲーム起動とPlayer Buildは未完了のままである。旧Pairの厳密floor Capsule失敗とStable版再生成3,009pixel差を解消済みとは扱わず、失敗報告と旧PNG・Sceneを保持する。この時点では最新Pairと3ブロック画像のLibrary新規保存は未実施だった。3ブロックの後続保存は下記に記録する。既存橋岸Library version2は旧 `220953821` の画像を保持する。

## 現3Sceneの限定Physics診断とLibrary保存状況（2026-10-04）

代表 `232117617` の実Sceneで旧Pairの2床点をX+10へ移した限定検査を実行した。`study-three-block-current-floor-and-water-probe.json` はphase=complete／inspectionCompleted=trueだが、厳密床判定はFAILED。Cell(25,8)／(26,8)、実Body(25.5,8.5)／(26.5,8.5)の両方で `MapRoot/FineWaterCollision` に対しCapsule.Distance=-0.00500011444／isOverlapped=trueとなり、floorStrictPassed=false／physicsPassed=false／passed=falseを保持する。旧Pairだけの未確認情報ではなく、現3Sceneでも再現した問題である。独立した動的Capsuleによる橋3lane（Y6.5／7.5／8.5、X23.5↔28.5）の両方向6試行と南北水停止2試行は全PASS。停止位置は(26,6.059957)／(26,8.500001)、実FineWaterCollision接触を確認し、計349simulation steps。許容値・deck・corecodeは変更していない。この限定検査は全water mask対応点、RetainedSolid全件、prop全件、全map床、実ProductionActorの追加QAを実施したものではない。

Scene依存199FileのSHA不変、元Scene・active Scene・Component flag・時間／simulation状態の復元を確認し、cleanupPassed=true。active Scene／timeScale／fixedDeltaTime／simulationModeのsetterAttemptedはすべてfalse。別のEditor-state証跡 `study-three-block-current-probe-final3-editor-restored.json` でもPlay停止、clean Template、runInBackground=false、timeScale1、fixedDeltaTime0.0199999921、FixedUpdateを確認した。診断証跡は `Assets/Art/Map/OverheadStudyA/ThreeBlockQA/20261004-current-scene-diagnostic/` にpayload12File・238,673bytesと `manifest.json` を保存し、全コピーSHA一致を確認した。最終Library保存Outcomeと失敗分類JSONを追加し、先行失敗Outcomeを履歴として保持した。従来のQA35payloadは変更せず、この追加診断を分ける。旧StableのFineBridge=false分岐の3,009pixel再現差は依然未解決。現3のFineBridge=true＋撮影前同期では再生成2回と通常再読込の両PNG／RGBA差0を確認したが、旧差の原因をcacheだけと断定しない。

Library向けの新4PNGと短いTXTはWorkspaceの `three-block-library-deliverables/` に保持し、最終的に新5itemをLibraryへ保存した。初期の正規native helperは通常tools/list後にprepare_uploads相当toolが見つからず `Library prepare_uploads is not available` で停止した。HTTP401／403、RPCまたは認証拒否とは別のtool未提供の停止であり、その拒否は観測していない。限定WSL試行のauth context初期化失敗もHTTP前の停止だった。親は準備tool未提供の場合の利用可能な直接createによる全件新規保存を明示許可し、認証拒否の迂回を禁止した。その範囲で、利用可能な直接createを使用し、次の全5件の作成成功を確認した。初期失敗Outcomeは過去の試行記録として保持する。

- `FantasyRoyale_3Blocks_Normal_Plaza.png`: `libfile_6ff633da47908191a5ec5f01af1f1b62`
- `FantasyRoyale_3Blocks_Normal_ForestPath.png`: `libfile_b3a27650df708191828ae30918add8a9`
- `FantasyRoyale_3Blocks_Normal_Bridge.png`: `libfile_9e1df17b33d881918b5d5344038651d1`
- `FantasyRoyale_3Blocks_Structure_Overview.png`: `libfile_83d0c63d30a08191ae8d83e363a66963`
- `FantasyRoyale_3Blocks_Controls_QA.txt`: `libfile_c5d9f62e67dc8191ae08b10a63f74088`

初回新規保存では、すべて返却 `current_version_number=0` を記録した。5ファイルのローカルLibrary identity metadataは、正規helperのWSL経路で認証／networkなしに適用済み。4PNG＋TXTの合計7,601,635bytesは不変。既存Pair Library ID `libfile_485130552e8481918678ef3daed0da3f` は更新していない。凍結TXTのSHAは `59d6b7bd42fa7a1f1a4315992bfb91ac789558c716487aea02d3ffe84ce7589f` のままで、初回保存後のこの時点ではTXT／Library item編集を行っていなかった。後続R2の同一ID更新は下記に記録する。

## 現3ブロックの北側橋面1pixel修正と全域Physics検証の結果（2026-10-04）

現3Sceneの2床点FAILEDについて追加実測を行った。Workspaceの `study-three-block-water-geometry-measure.json` と `study-three-block-water-geometry-v2-measure-v2.json` は、実Body(25.5,8.5)のCapsule上端Y8.995001に対し、水のPolygon頂点がY9である一方、Capsule.Distanceの水側点BはY8.99、distance=-0.00500011444／isOverlapped=trueとなることを記録する。contactOffsetを0.0001／0.01／0.05に変えてもこのdistanceは不変で、元Compositeとsource Boxの直接測定でも同値だった。default contactOffset0.01では点のOverlap境界がY9でも、実CapsuleのOverlapはFineWaterCollisionを検出した。点サンプルの非重なりだけでは床Capsuleの安全性を確認できない。測定後のcontactOffsetは元0.01へ復元済みである。

親が承認した現3ブロックCase限定の修正で、北側橋面上端を1/64unitだけ延長した。元Native橋面の最上1行のRGBAを複写してDeckを256×193pixelとし、同じ延長を表示waterとcollisionの共有mask apertureへ適用する。初回生成は新しいbounds guardが2D SpriteのZ厚みまで比較したため停止した（`study-three-floor-fix-first-build-report.json`）。Generated=falseの記録と部分artを保持し、元Source・Scene不変を確認した。実2D XY測定後、guardはXY boundsに加えrect256×193／pivot(128,0)／PPU64を厳密確認する形へ修正した。Actor寸法、Physics閾値、core設定は変えていない。旧論理12床Cell、water／walk ports、routes、元Native artと参照Pair出力を保持し、旧FineBridge=false分岐の3,009pixel差は対象外とする。

修正後R2の代表は `terrain-plazapathandbridgebank-seed-20261003-20261004-013635908`。`study-three-floor-fix-generated-identity.json` はGenerated=true／generation guards成功、元Source・Scene保持、Errors0を記録する。Scene SHAは `870ec10d4471593eee3b6adb8677245621669245e2d8995cd9475a27a2ce3aba`。Settings SHAは元と同じ `4f4843e3468ee32cc21370e9ab7da8b345412bc7b5d634514763817341b9c544` である。Rootが構造全景と静止通常比較画像の実画素を確認した。新Settingsからの再生成2回と通常再読込では両PNG／decoded RGBA差0、mask・layout・Native assembly一致、元Sourceと読込済みScene保持を確認した（`study-three-floor-fix-r2-reproduction-summary.json`）。追加7Caseを含む最新Editor157/157・PlayMode4/4も成功し、failed／skipped／inconclusive0（`study-three-floor-fix-editor-tests-result.json`／`study-three-floor-fix-playmode-tests-result.json`）。

既存手動入口 `StudyAThreeBlockNormalPlay.unity` はMap Scene参照1行だけを新代表へ更新した。Scene SHAは `35d85274b56c92c50a41444d50473c36e148ba33701679ba6719a40ff7dca54f`、meta不変。事前のdirty PlayerSettings／Unity6 wrapper検査で停止した試行を保持する。V2保存は参照1行の正確deltaとProjectSettings内容保持を達成したが、UnityのSaveSceneが元PlayerSettingsのdirty markerだけを消したため保護guardはFAILとなった。このV2を成功へ書換えない。同じEntityとserialized内容SHA・全Settings file不変を確認して元のdirty markerだけを別処理で戻し、`study-three-floorfix-final-normal-entry-reconciled.json` のreconciliation成功を確認した。global SaveAssets／ClearDirty、Settings値の書込は行っていない。

更新後の入口で本番CharacterActor2Dを実Win32 D／A／RIGHTキー9試行（各hold0.05～1秒）で確認した（`study-three-floorfix-final-normal-summary.json`）。y7.5でx6.5→13.3000183→20.0000439→22.0000515→25.9000664→29.200079、逆方向22.3000526→15.3000259→8.5へ戻り、同じ入口を開き直した初期x6.5からRIGHTで8.6へ移動した。全解放後の命令0／位置安定、Actor命令やTransformの直接書込なし。新通常3PNG `study-three-floorfix-final-normal-{plaza-path,path-bridge,on-bridge}.png` の実画素をRootが確認し、実Actorを通常倍率の両接続と橋上に明瞭に表示した。Camera size8、1536×1024／撮影aspect1.5、live GameView aspect1.77777779を維持する。終了後はPlay停止・元clean Template1本・runInBackground=false、元PlayerSettings内容とdirty=trueを確認した。旧232の歩行報告の流用ではなく、新代表の実入力証跡である。

`study-three-floor-fix-full-physics.json` はphase=complete／inspectionCompleted=true／cleanupPassed=true、計1,965simulation steps。許容値なしの厳密床12点すべてPASSとなり、旧失敗2点の水へのdistanceは+0.0106248856／重なりfalse。独立した橋6lane・南北水停止2試行・新北側帯4点、RetainedSolid102Cellのexact Tilemap／実Capsule、全85propのfoot／水・deckとの配置／実物理停止もPASSである。一方、2048×1024の全2,097,152pixel中心を実CompositeCollider2Dと照合すると26点が不一致となり、`waterPixelPoints=FAIL`／physicsPassed=false／passed=falseを保持する。元mask全byte・保存Contour一致と実water PNG alpha検査のPASSを、実Composite全点照合のPASSとは混同しない。

26点の限定native診断を完了した（`study-three-water-delaunay-26-native-diagnostic.json`、SHA `2e7218bb7ee68fb3a018f5db52d72882e6259c41e0abac5453581d53d5f86212`）。全26点はsource Box rectangleの厳密な内側で最小余白1/128unit、実Capsule.Distanceはvalid／重なりtrue（約-0.25）、Capsule.Overlapも水を検出し、ClosestPoint距離0だった。独立読取りproof `study-three-water-point-independent-proof.json` でGetShapesのnative float32座標をexact Fraction計算すると、各点が許容値なしで正確に2shapeの辺上に乗っていた。共有内部辺の点query精度問題を支持する推論であり、歩行Capsuleが通過する穴を実証したものではない。owned CompositeだけのDelaunay比較は旧26点を解消したが、全点再走査で(1640,667)／(1703,676)の新2不一致が出たため不採用とし、元useDelaunayMesh=false・Source・設定を保持した。診断のpassed=trueは検査実行とcleanupだけの成功で、physicsPassed=falseと現在の全点26FAILを覆さない。閾値緩和や一般QA framework、旧Stable修正は行わない。

85propの物理停止は27件の到達可能な接近と58件のisolated検査に分けた。後者のゲーム上の到達性はNOTRUN。また25背景propは32×16raster canvas外でmask画素coverageはNOTRUNだが、全85件の実water Collider距離と物理形状検査には含めている。productionActor／renderedQualityは独立Physics報告ではNOTRUNのままで、実Actorと画像は上記の別証跡で確認した。全map床の総当たりやゲーム上の到達性が成功したとは扱わない。前項のFAILED報告と既存出力を履歴として保持し、今回の全QA合格とは扱わない。

修正後のPlaza／ForestPath／Bridge／Overview／TXTは前項の同一5Library File IDへ全件replaceし、返却versionはすべて1（expected previous version0）だった。`study-three-floorfix-final-library-save-outcome.json` と `.library-three-floor-fix-20261004/confirmed-identities.json` に5件の成功と正規helperによるローカルidentity metadata適用を記録した。準備tool未提供のhelper停止は書込前で、確認済みの所有itemへversion guardを伴う承認済みreplaceを使用した。新Library IDは作成せず、既存Pair IDとStableは更新していない。新4PNGと操作TXTをWorkspaceの `three-floor-fix-library-deliverables/` に保持し、元のTXTと画像も残した。新TXT SHAは `6f1d5b7276908e48fc646ed0915e6c806c99b132e7476204ee960e9150a8b4a1`、5payloadの合計7,600,418bytesである。

今回の証跡は `Assets/Art/Map/OverheadStudyA/ThreeBlockQA/20261004-floor-fix/` に新規保存した。payload37File・7,371,997bytesとSHAを記録した `manifest.json` があり、最終Physics・点診断と独立proof・再現・新通常3画像と実入力・回帰・Library保存Outcome・凍結TXTを保持する。旧normal-playの35payloadとcurrent-scene-diagnosticの12payloadは変更しない。床12点の修正と通常操作・成果更新を今回の区切りとし、全点26FAIL、isolated到達性などのNOTRUN、A画質未完成は残す。新しい一般QAや追加ゲーム機能は増やさず、次は見た目の改善へ進む。

### 将来の自動生成要件と次の見た目修正

最新のユーザー目標は、寸法の異なるblockを複数方向のportで接続し、地形・森の縁・道を部分的に変形できる生成である。固定ランドマークの核は保護し、pixel素材の縦横比を変える引伸しや任意角の回転は使わない。異なるバイオームも一続きのBattle Royale fieldとしてseamlessに移動でき、境界では地面・植生・岩などが段階的に混ざる移行帯で景観を自然につなぐ。連続した通行と当たり判定を含む接続データ・境界処理の要件として、可変寸法・多方向ports・部分変形と一緒に整理する。現行の横一列3ブロックは最初の接続検証であり、最終仕様ではない。既存の閉輪郭とportsを再利用する設計整理は後続で短く行うが、今回の区切りで生成器全体の再設計・置換、新バイオーム一式やstreaming基盤の実装は行わない。

次のart passは別Settingsで、施設へ導く配置、道の曲がりを読ませる石、交互に張り出す森の輪郭を調整する。選択した樹冠に寒色の暗部、下草に硬い右下の接地影を加える案を確認する。3unit歩行接続口・2unit川接続口・修正後Deck・Camera8を維持する。梁と岸highlight素材は親が準備中で、導入済みとは扱わない。通常倍率の画像で改善を確認し、広い全景だけで密度差を隠さない。この段落は次の作業計画で、今回の実装結果ではない。

## Art pass 1 の実結果と区切り（2026-10-04）

床修正の区切り後、既存Sourceの限定backupを取ってart用の3クラスとBuilder／Settingsの5Sourceを導入し、Native compile成功を確認した。`ArtFinishVersion=0` は旧動作を維持し、保存Settingsを複製して1を指定した新runだけに適用する。R3代表 `terrain-plazapathandbridgebank-seed-20261003-20261004-034716040` の生成は成功し、`study-art-pass-generated-r3.json` はGenerated=true／passed=true、Errors0、元Source・読込済みScene・PlayerSettings内容とdirty状態の保持を記録した。Seed20261003、Case5、BridgeRiverLandscape2、FineBridge=true／global FineSoil=false、Camera8、ArtFinishVersion1を保存する。既存 `013635908` のMapと床修正Library5件version1を履歴として保持する。目標A/Bの実画素はHomePC側では未確認で、親のreviewに基づく候補である。FullTargetReady=falseと将来の可変寸法・多方向ports・固定核を守る部分変形・seamlessバイオーム移行要件を保持する。床段階の全点26FAILは既知の限界として閉じ、新しい一般QAや生成器全体の再設計は追加しない。

R3 reportは施設2箇所と木6本の計8配置移動、単一の施設approachに曲線状の石3枚、69本のtree tint（広場の暖色3本を含む）、梁2本と波紋4枚を記録する。承認済み岸・梁・波紋7素材をnative64のまま使用し、岸方向ごとの輪郭とport開口を保持する。下草は既存shrubのpaletteを参照したprocedural描画で、Native葉画像の複写ではない。短い硬い右下の接地影を加え、追加装飾Collider数0、forest表示適用前後のCollider graph一致を記録した。配置移動の既存CollisionBodyはSpriteと共に移動するが形状recipeは変えていない。表示水／water collision／Fine free-walkの各SHAと修正後Deck boundsはart適用前後で一致し、3unit歩行帯・2unit川port・Camera8を保持する。初回の `treeLarge_04` guard失敗と部分Settings `032607792` は履歴として残し、限定integration修正後のR3成功と分ける。

別入口 `Assets/Scenes/MapAuthoring/OverheadStudyA/StudyAThreeBlockArtFinishNormalPlay.unity` を新規作成し、床修正入口のMap Scene参照だけをR3へ変更した。元床入口は保持する。新入口を実Playで読込み、本番CharacterActor2DをWin32 D／Aキーの順方向5・逆方向3の計8試行で操作した（`study-art-pass-final-actual-summary.json`）。y7.5を保ってx6.5→13.4000187→20.2000446→22.5000534→26.4000683→29.70008、逆方向22.9000549→16.100029→9.300003へ戻り、両接続と橋を往復した。Actor命令やTransformの直接書込は行わず、キー解放後の命令0と元clean Templateへの復帰、runInBackground=falseを記録した。生成段階のactualPixelsReviewed=false／actualActorWalkingVerified=false等は元報告のまま保持し、今回の実操作は別証跡とする。

通常3画像は `study-art-pass-final-normal-plaza-path.png`、`study-art-pass-final-normal-path-bridge.png`、`study-art-pass-final-normal-on-bridge.png`。Rootが各1536×1024の実画素とActor表示を確認し、Camera8／撮影aspect1.5、live GameView aspect1.77777779を記録した。全景2048×1024も確認し、R2の明緑checkerを暗い下草へ変更、南北2unit川portを横断する岸capを除いた。独立したR3実画素reviewでも両修正を確認したが、HomePCでは正確なA/B参照画像を確認できていない。通常3画像・全景・実入力・再生成FAILED報告・Library保存結果を `Assets/Art/Map/OverheadStudyA/ThreeBlockQA/20261004-art-finish1/` へ保存した。31payload・10,409,070bytes、manifest SHA `8ef660bdc7052f95d8e7a23f675a8183244fdbb9b1c7f9180cf90773675abd10` と全コピーhash一致を `study-art-pass-final-durable-qa.json` に記録する。

保存Settingsからの再生成はoverview／gameplay両PNGのfile SHAとdecoded RGBA、layout、Native assembly、shared fine masksが一致した。一方、再現helperの `passed=false`／`selectionsIdentical=false` は保持する。24件のdepth-tie記録のうち北側beam1件が新runのSprite asset pathを含み、同じ足元Yとsort anchorでも `FineDepthTieSelectionHash` が変わるためである。`final-depth-tie-replay-diagnosis.json` はこの原因を示す読取り診断であり、正規化によるPASSへの変更や元FAILED報告の置換は行っていない。

通常Plaza／ForestPath／Bridge／Overview／Controls_QAの5件は既存の同一Library File IDへreplaceし、全件version1→2とローカルidentity metadata適用の成功を `study-art-pass-final-library-save-outcome.json` に確認した。元floor version1のローカル成果を保持し、凍結Controls_QAも変更しない。今回承認された1回のart passは導入・表示・通常操作・成果更新の区切りとするが、A画質完成とは扱わずFullTargetReady=falseを維持する。全点26水query FAIL、58isolated propの到達性と25背景propのraster coverage NOTRUNは床checkpointのまま保存し、art passでは独立Physics全検査を再実行していない。battle／enemy／Player Build／gamepadも未検証で、全replay／全Physics合格とは記載しない。

再現操作は、上記の別入口を開いてPlayしGame Viewを選択してWASD／矢印キーを使う。再生成は保存Settings `Assets/Art/Map/OverheadStudyA/BlockStudies/terrain-plazapathandbridgebank-seed-20261003-20261004-034716040/StudyABlockSceneSettings.asset` を選び、`FantasyRoyale/Map Authoring/Overhead Study A/Generate Selected Block Study` を実行する。Seed20261003・ArtFinishVersion1・Camera8は保存済みで、元設定や既存Mapを上書きせず新runを作る。

## Art pass 1 後の下草だけの限定修正（2026-10-04）

親がversion2の実画像で指摘した、森の足元に広がる暗cyanの平面に対し、追加承認された1回の修正を下草のpaletteと形状だけに限定した。`StudyAForestArtFinishRenderer.cs` の限定backup後、SHA `05be1a2ac094a59a26742fbd74d1b4768f16e20e2854313e970519cf1cf8e2f4` を導入した。承認済みGroundR2の草緑を基調に既存shrubの近い緑を混ぜ、木のopaque幅と足元幅に合わせた小さいpatch、短い接続、硬い不均一な輪郭へ変えた。新規素材は追加していない。接地影、木のtint、通路／水のclip、施設と木の配置、Collider処理はこの限定source差分では変えていない。

R4代表 `terrain-plazapathandbridgebank-seed-20261003-20261004-042927767` はNative compile後に生成成功した。`study-art-understory-generated.json` はGenerated=true／generation guards passed=true、Errors0、元Source・読込済みScene・PlayerSettings内容とdirty状態の保持を記録する。生成報告のactualPixelsReviewed=false／actualActorWalkingVerified=falseは書き換えていない。別の独立読取りreviewで、実overview2048×1024とgameplay1536×1024の両PNGを見た。広い暗cyan面は消え、木の足元に小さく切れた緑の下草が残り、草地の連続性と樹冠／幹／地面の層を保つ。川の南北開口は開いたままで、橋の接地と主通路を新しい下草面が覆っていない。この限定修正の静止画像に明白なP1は認めなかった。

R3／R4の保存metadataを独立比較し、layout、terrain selection、既存8配置変更、visible water／water collision／Fine free-walk／road soilの各mask、Deck／post bounds、forest Collider graph、69本のtree tint、接地影maskとpixel数、bank／ripple pixels、beam／ripple anchorsは一致した。下草opaque pixelは522,964から61,861へ減り、下草のpalette・mask・recipeを変更した。証拠はWorkspaceの `art-pass-notes/understory-r4-read-only-review.json` に保存した。この照合と静止画像reviewを、実Actor歩行、独立Physics全域検査、saved Settings再生成の成功とは混同しない。

別のart用入口 `StudyAThreeBlockArtFinishNormalPlay.unity` は、backup後にMap Scene参照だけをR4へ更新した。Scene SHA `3a4a8ceca592b2e8094cdc4d5ed72b58e143ceab445e51a285b699755c06aef7`、meta不変、元の床修正入口も保持する。Rootが本番CharacterActor2Dを実Win32キーの順方向4試行で動かし、y7.5でx6.5→13.4000187→20.2000446→22.3000526→26.2000675と、両接続から橋上まで移動した。2試行目の最初の試みはfocus喪失を検出してキー送信前に停止し、focusを戻したretryを成功として記録する。`study-art-understory-actual-summary.json` に実入力、通常3PNG `study-art-understory-normal-{plaza-path,path-bridge,on-bridge}.png`、各1536×1024／Camera8／撮影aspect1.5、Rootによる実画素確認を記録した。独立reviewでも橋上PNGの実Actorと床板、森足元の草緑を確認した。終了後のPlay停止、元clean Template、runInBackground=false、元PlayerSettings内容SHAとEntity814／dirty markerの復元を別証拠で確認した。

R4保存Settingsからの新run再生成では、overview／gameplay両PNGのfile SHAとdecoded RGBA、layout、Native assembly、shared fine masks、元Sourceと読込済みSceneの保持が一致した。下草と接地影mask、forest selectionとCollider graphも一致した。一方 `study-art-understory-saved-settings-reproduction.json` のpassed=false／selectionsIdentical=falseは保持する。24件のdepth-tie記録中index17の北側beamだけが、新run `043801717` のSprite asset pathを含むためhashを変えた。足元Y9とsort anchor9.001953は同一で、前項と同じrun-local pathの差である。元FAILED報告を置換せず、全replay PASSへ変更していない。

通常Plaza／ForestPath／Bridge／Overview／Controls_QAの5件は、前項と同一のLibrary File IDへ全件replaceし、version2→3とローカルidentity metadata適用の成功を `study-art-understory-library-save-outcome.json` に記録した。元floorとArt pass 1のローカル成果およびLibraryのversion履歴を保持する。今回の実入力・通常3画像・全景・metadata比較・元FAILED再生成報告・Library保存結果を新規 `Assets/Art/Map/OverheadStudyA/ThreeBlockQA/20261004-art-understory-refinement/` に保存した。27payload／9,866,322bytes、manifest SHA `aeb646291887b0f217dfcfa4a43ba171842bde8133989ef2574532900e7a4abf` と全コピーhash一致を `study-art-understory-durable-qa.json` に記録する。前項の31payloadのQA保存先は変更していない。

再生成は保存Settings `Assets/Art/Map/OverheadStudyA/BlockStudies/terrain-plazapathandbridgebank-seed-20261003-20261004-042927767/StudyABlockSceneSettings.asset` を選び、`FantasyRoyale/Map Authoring/Overhead Study A/Generate Selected Block Study` を実行する。Seed20261003、ArtFinishVersion1、Camera8、3unit歩行帯、2unit川port、修正後Deckの条件を保持する。この限定修正は導入・実表示・通常入力・保存成果更新を今回の区切りとする。既知の全点26水query FAILとNOTRUN範囲を保持し、今回の独立Physics全域検査は再実行していない。HomePCで正確なA/B参照画像を未確認のためFullTargetReady=falseを維持し、A画質完成や全Physics／全replay合格とは扱わない。

## ユーザーの追加美術評価：接地と手作業の着色の揺れ（2026-10-04）

ユーザーから、現在の木や橋が景色になじまず、地面の上に「乗っている」ように見えること、A案と比べて「作り手の癖がない」感じがするとの指摘を受けた。これは即時の大改造ではなく、A案との差として認識・記録してほしいというフィードバックである。

接地については、木の根元・幹・樹冠・下草と地面、橋の端・岸・水の関係、前後の重なりが一つの風景としてまとまっているかを通常倍率で見る。この「景色から浮いて見える」問題は、以下の着色の問題とは別の評価軸として保持する。

後続のユーザー説明は「癖というのはアバウトでしたね。ドット絵など、均一で綺麗すぎるのかもしれません。人の手で作ったものなら十人十色着色にノイズがあるはず。」である。本件の「作り手の癖」は、主に構図の個性を求める意味として広く読むのではなく、ドットや色の置き方が均一にきれいすぎ、手作業の着色にある描き手ごとの揺れが不足しているという指摘として記録する。

美術比較では、色相・陰影の選択、ピクセルクラスターの形や色の置き方の不均一さなどに、描き手の判断が感じられるかを評価する。単なる白色ノイズやランダムな点の追加、ノイズ量の増加、低品質化と同一視しない。素材数や細部を追加したことだけを改善の根拠にしない。別画風への変更、派手な装飾追加、全素材への破壊的フィルタ適用を求める指示として扱わない。特定の技法・新素材・実装方式はこの記録では決めない。

参照状態も訂正する。前節までの「HomePCで正確なA/B参照画像を未確認」という記述は、現在の受領状態を表すものとして誤りだった。StarterPackageの `Assets/Art/Map/OverheadStudyA/Input/StarterPackage/Reference/A-overhead-vivid.png` は現存・読取可で、1536×1024、SHA256 `f35c20657371a260825e009a3f4f573710990e1c0563823a2421e63dc0acf17a` の実画素を確認した。原画像2枚もHomePCのWorkspaceに現存し、受領記録とのbyte数・SHA一致を再確認した。参照不足をFullTargetReady=falseの根拠にせず、Aとの具体的な空間構成・密度・川の表現の差と、今回の接地・着色の揺れの評価を未解決事項として保持する。過去のFAILED検証結果はそのまま残す。

今回の作業はこの評価記録と成果保全である。現代表R4 `terrain-plazapathandbridgebank-seed-20261003-20261004-042927767`、通常入口 `Assets/Scenes/MapAuthoring/OverheadStudyA/StudyAThreeBlockArtFinishNormalPlay.unity`、同runの `StudyABlockSceneSettings.asset`（Seed20261003／ArtFinishVersion1／Camera8）、QA保存先 `Assets/Art/Map/OverheadStudyA/ThreeBlockQA/20261004-art-understory-refinement/` を現在成果として維持する。Libraryの5件は前節の同一ID・version3の保存記録を維持し、このフィードバック記録による再生成・Scene／設定／素材／コード／Library成果の更新は行わない。A画質の完成や既知のFAIL／NOTRUNの解消としては扱わない。

## 比較実験と橋構造再設計の初回準備（2026-10-04）

ユーザーは、木のパターン追加、ブロック全体の描き合わせ、他の要因を調査・比較することを依頼した。その後、構造修正も並行で進め、特に現橋の形状はA参照と比べて自然さがなく品質NGであると明示した。旧床・柱・細梁を完成前提に固定せず、桁・板厚・横断方向・岸への取り付け・支柱／欄干のまとまりを再設計する。着色や波紋の追加だけで橋形状の問題を解消した扱いにしない。

3unitはゲーム上の橋の必須最小幅ではなく、このStudyの制作・接続基準である。上記143／149／151行の記録と[主道の基本幅の記述](../../product-specs/art/map-plan.md)を根拠に、橋内部を1.5～1.75unitへ絞り、両岸で外部3unit口へ開く別設定を試せる。ActorのCapsule0.48×0.55／offset(0,0.22)を維持する。見える床と水Collider開口を同時に変更し、見えない旧3unit幅の歩行床を残さない。旧12床点／6lane検査はR4専用の履歴として保持し、新橋の実Actor横断、岸との収束部、橋下の水停止を別に確認する。旧193pixel床は新橋の必須寸法ではない。

親が素材を準備する第一候補は4×1.5unit、256×96pixel相当で、縦横比2.67:1。初期物理配置案はWorldRect(24,6.75,4,1.5)、床pivot(128,0)pixel、anchor(26,6.75)とする。これは未検証の入力契約で、Native表示やPhysicsの合格ではない。現行生成器の旧4×3固定guard、北梁の+3、柱配置、Fine geometry再構成を、新規opt-inだけの別設定へ対応する必要がある。R4の設定・Scene・素材・旧branchは保全する。

比較ROIは既存通常画像1536×1024の左上(768,128)から768×768pixel。WorldRect(17.78125,2,12,12)、PPU64、Camera位置(17.78125,8,-10)／Size8、撮影aspect1.5、Hero足元(22.3000526,7.5)を共通条件にする。新橋導入後の安定区画を木／地面の3案へ共通適用し、R4との橋構造比較を別に記録する。木案はlarge256×256／pivot(128,8)、medium192×192／pivot(96,8)のfamily内差替えだけ、配色・接地案は既存shape／alpha／poseを維持して色相・陰影と地面の遷移を変更する。描き合わせ案は同768×768のpaintoverで上限を探り、採用候補を地面と個別footで並ぶ木・梁・柱へ分解する。一枚の前景にしてHeroの前後関係を失う方式を最終実装として決めない。

初回準備としてHomePCのWorkspace `visual-comparison-20261004/` に、基準通常PNGのbyte一致コピー、`material-layer-contract.json`、基準／新橋／木差替え／配色・接地／描き合わせの別 `comparison-settings.json` とInputs／Outputsを作成した。21本のSprite FullRect交差対象と、実alphaが交差する18本、各Hierarchy／足元／元PNG／pivot／Tint／描画層を記録した。これらJSONは素材・試験仕様であり、既存StudyABlockSceneSettingsが解釈する新規fieldや実装済みの実験入口ではない。親の新素材は未受領で、新橋・変更案のNative生成／表示／通過検証は未実施である。

構造の最初の小試作は、既存PlazaAndPath Caseで複製NativeのPlaza12×10とForestPath8×8を組み、East(11,4)／West(0,2)の幅3接続後、Path内をWest→Northへ曲げる。Path原点(12,2)、全Bounds20×10で現24×16 frameに収まる。次にSouth／North実接続を同じ素材で比べる。素材の90度回転、新しい巨大生成器、全バイオーム対応は導入しない。最小実装候補は新Prefabの定義・mask・経路・閉輪郭とFine soilのN/S即時接続strip対応であり、旧位置前提のBackdrop／Details配置はこの試作へ流用しない。今回ここまでの構造調査は読取り診断で、pilot生成は未実施。

## 可変寸法pilot・橋構造01 R2・木／地表4条件比較（2026-10-04）

既存生成器でPlaza12×10／ForestPath8×8の別cloneを作り、`terrain-plazaandpath-seed-20261003-20261004-055052832` を生成した（`structure-pilot-generated-second.json`）。24×16 frame内のassembly Bounds(2,3,20,10)、Plaza原点(2,3)／Path原点(14,5)、Spawn(8.5,8.5)を保存し、East→West接続の先をNorthへ曲げた。Rootは本番Actorを実OSキーでx8.5→12.3→15.1、y8.5の接続を通し、北側終点(19.4749222,12.5748959)まで歩かせた。`study-manual-pulse-structure-pilot-precise-w6.json` はNative Keyboard・runtime Input clone・キー解放後command0、Actor transform／Unity Input状態の非書込みを記録する。通常画像は `visual-comparison-20261004/structure-pilot-source/pilot-normal-1536x1024.png`。最初のVector2自己参照serialization失敗は `structure-pilot-build-first-native.json` に保持した。これは寸法と曲がる道の構造pilotで、South／North二ブロック接続や全バイオームの完成ではない。

橋は旧R4を保護したopt-in `BridgeStructureVersion=1`。Native3層320×192pixel／64PPU、共通pivot(160,96)、anchor(26,7.5)、scale1を使用し、床WorldRect(24,6.75,4,1.5)と水開口・両岸収束部を一緒に変更する。旧4×3床／柱／細梁を新出力から外し、body maskと前景投影を分けた。3unit外部口を保持するが内部床は1.5unitなので旧12床点／6laneを流用しない。初回 `055820143` は実横断できた一方、`bridge01-native-geometry-first.json` の65中心line点中7点OverlapCapsule阻害によるFAILを保持する。第2生成 `062044749` はGenerated=trueでも明示Refresh前の旧compiled body処理だったためR2採用証拠から除外した。

明示Refresh・Native recompile完了／failed=false／errors[]後の第3生成 `terrain-plazapathandbridgebank-seed-20261003-20261004-062425736` をR2とする（`bridge01-generated-third.json`）。BodyRecipeRevision2／NorthCornerInsetPixels1はnorth cornerの幅・上辺を保持し、下辺だけ1/64unit北へ動かす提案改訂で、元manifestの当初仕様とは扱わない。body maskは `cc28e4c82cd67466e1d9608363c258946fa58162b925d1bf9a7280217b500de4`。床4×1.5・水開口・Actor寸法・Physics設定を保持し、ArtFinishVersionは1のまま。独立native geometryは65点阻害0／continuous Cast0hit／境界11・内部点7・登録Sorting契約3件PASS、Actor transform／Capsule保持（`bridge01-native-geometry-r2.json`）。旧+1pixel corner座標2件のblocked観測は残す。別の `bridge01-r2-manual-sequence.json` は実Win32キーでx11.5000114→16.50003→21.50005→26.0000668→29.30008→24.20006→21.2000484の往復、北y7.75000143／南y6.809999の停止、全キー解放後command0を記録する。生成report内のActualActorMovementVerified=false等は生成時の記録として書換えない。

同じR2 layout／body／床を基準に `FoliageExperimentVersion` 0=既存木、1=P、2=V、3=地表統合を別Settings／Sceneへ生成した（`foliage-native-builds-first-summary.json`、`foliage-normal-entries-first-summary.json`）。Pは元alpha／根位置を維持した大きな樹冠色面、69本をSprite差替え。Vはサイズ別の安定hierarchy順をoriginal／V01／V02へ循環させ、69本中45本を差替える。Vは幹根も変わるため輪郭だけの効果と解釈しない。P/Vとも配置・Tint・Collider graphを保持する。地表条件3は既存木を維持し、指定12×12領域の17本の根近傍を草・下草・接地影の色で馴染ませる。生成receiptではoverlay37,438pixel、下草RGB13,362pixel／接地RGB3,481pixel、元alpha・SourcePNG・共有mask・地面Tilemap・Collider・layer state保持、範囲外／禁止領域0。P/Vとの合成条件ではない。Sourceは `StudyAFoliageExperiment.cs`／`StudyAGroundIntegrationExperiment.cs`、保存入力は `Assets/Art/Map/OverheadStudyA/Input/FoliageControlledPV/`。

`foliage-native-four-arm-capture-retry02-summary.json` は4条件全撮影成功。通常Play中の本番Actor(22.3000526,7.5)、Camera(17.78125,8,-10)／ortho8／CustomAxis upを揃え、1536×1024・capture aspect1.5で撮影した。live GameView aspect16:9は別値として記録する。比較用Spawn指定は歩行証拠と分け、最初とretry01の撮影失敗証跡も保持した。4PNGは `visual-comparison-20261004/native-four-arm-retry02/` の `00-NewBridge-ExistingTrees-1536x1024.png`／`01-NewBridge-Tree-P-1536x1024.png`／`02-NewBridge-Tree-V-1536x1024.png`／`03-NewBridge-Ground-Integration-1536x1024.png`。独立 `pixel-audit/independent-pixel-gates.json` は全gate PASS。P643,407／V436,287差分は69木のraw変更texel投影union内だけで非tree／許容外0、Hero64×80／Bridge320×192領域は全3条件差0。P/Vの川投影内7,504／8,899差は木の遮蔽範囲内でbareWater差0、水PNG・橋3層PNG・layout／geometry hash同一。川の画面全域差0とは言わない。地表条件は9,614差分、ROI外／川／Hero／橋0。追加の実ground texel投影union37,438pixelとの照合でもunion外0を確認し、旧座標による追加診断の失敗は `pixel-audit/failed-additional-ground-global-projection.json` 等に保持した。

Rootと独立review担当が4PNGとpilotを実画素確認した（`foliage-native-four-arm-independent-visual-review-ja.txt`）。Pは細葉の均一さが弱まり色面が大きくなったが同じ明部塊が繰り返される。Vは形の反復を弱めたが、色相・陰影選びの揺れや全ての接地を解決していない。地表条件は局所の根／地面色を近づける控えめな差で、景観全体の改善量とは別。pilotは曲がる道の構造差が明瞭でも裸砂面の広さ・草縁の階段が残る。1254×1254の親conceptは同WorldRect／Camera／Hero poseの比較画像ではない。A達成・勝者採用・FullTargetReady=trueを宣言せず、異寸法／多方向口／fixed landmark核を保護する部分変形／自然な移行帯と連続通行・衝突を持つseamless biomeの目標は前項のまま維持する。

操作入口は `Assets/Scenes/MapAuthoring/OverheadStudyA/StudyAStructurePilotNormalPlay.unity`、R2実歩行は `StudyABridge01R2NormalPlay.unity`。どちらもPlay→Game画面を選択→全キーを離す→WASD／矢印で操作する。4条件の入口は同Directoryの次の表。保存Settingsは `Assets/Art/Map/OverheadStudyA/BlockStudies/terrain-plazapathandbridgebank-seed-20261003-20261004-{run}/StudyABlockSceneSettings.asset` を選び、`FantasyRoyale/Map Authoring/Overhead Study A/Generate Selected Block Study` を実行する。Seed20261003／Camera8／Native参照／Fine設定／versionを保持して新timestampへ生成し、入口Sceneは自動更新されない。pilotのSettingsだけは同Rootの `terrain-plazaandpath-seed-20261003-20261004-055052832/StudyABlockSceneSettings.asset`。初回4条件生成の記録scriptはWorkspace `foliage-comparison-source/foliage-comparison-build.cs` の `StudyAFoliageComparisonBuild.Build(version, freshReportPath)`、入口作成は `visual-comparison-20261004/structure-pilot-source/study-foliage-comparison-normal-play-scene.cs`、撮影は `study-normal-play-camera-proof.cs`。既存Python wrapperは未使用proof名を要求するため保存済み証跡へ上書き実行しない。今回の保存Settings再生成・再読込一致検証はNOTRUNである。

| 条件 | run | 通常Play入口Scene名 | 新Library ID（全version0） |
| --- | --- | --- | --- |
| 既存木＋R2橋 | 062425736 | StudyAFoliageComparisonV0-20261004-063855183.unity | libfile_4dfe1c16a7d08191a4d45b1a259c839a |
| P | 063719394 | StudyAFoliageComparisonV1-20261004-063855711.unity | libfile_aafccb094c908191a341392b4e4cdb2d |
| V | 063726148 | StudyAFoliageComparisonV2-20261004-063856219.unity | libfile_41d5a60bf1fc8191ad4d029c30694c77 |
| 地表統合 | 063732568 | StudyAFoliageComparisonV3-20261004-063856702.unity | libfile_063209294168819193d1296c8bac508d |
| 20×10構造pilot | 055052832 | StudyAStructurePilotNormalPlay.unity | libfile_96b285daa4dc819184110f09ea1696c7 |

Library5件は新規保存成功、identity metadata適用／local bytes保持を `.library-current-comparison-upload/final-five-metadata/five-direct-create-confirmed-identities.json` で確認した。早期比較の旧R4 `libfile_b1067a4358bc81919a6ed2ff129a1c4f`／初回橋 `libfile_a5cca961ae0c8191aae4477f4793ee82`（双方version0）や旧5成果を置換していない。旧R4 full saved reproductionのselections不一致FAIL、旧Stable3,009pixel差、水OverlapPoint26FAIL、既存reachability等NOTRUNは保持する。今回の生成／比較PASSを全QA成功とは扱わず、新しい全map物理scan・PlayerBuild・人のhardware gamepad確認もNOTRUN。

読取り保全証跡はWorkspace `final-update-20261004/preservation-audit-final.json`。元304File中298不変、承認された既存StudyA Source6だけ変更、Bridge01／Foliage／GroundIntegration追加Sourceとmetaを別記録、unexpected0。R4 original82File、SourceNative基準入力4File、InputActions1File、ProjectSettings27File、既存Runtime／R4入口9Fileは元hash一致。foliage install manifestの24Fileも現行hash一致。既存162trackedのstatus／path集合は一致・追加消失0だが、行集合を全162内容の過去hash証明に代用しない。`final-five-capture-editor-restored.json` は停止・唯一clean Template・runInBackground=false、元PlayerSettings live JSON hash `7447231d…a1f7` とdirty=true保持を記録した。この監査は文書install前のsnapshotで、Rootのinstall／Knowledge validator結果は別のclosure証跡で確認する。現行文書124,294bytesは全prefixを保護して追記する。

## Northへの異寸法接続実証と独立PaintedBlock準備（2026-10-04、Native実歩行・描画確認、成果保存済み）

保存済みPlaza12×10／ForestPath8×8の定義・原点を保持したfresh Scene copyへ、第3block 6×3／原点(17,13)のSouth口を接続した。`north-connection-pilot-20261004/generated-first.json` はGenerated=true／passed=true、出力 `north-connection-pilot6x3-seed-20261003-20261004-073057972`、元Source・Scene・PlayerSettings内容とdirty状態保持を記録する。Workspace専用helperでAddConnectedと共有mask／FineSoilを新出力内だけ更新し、既存固定Caseを汎用化していない。`north-connection-prototype/generated-first-soil-pixel-preservation.json` は保存PNGのRGBA差28,928pixel、Third WorldRect外差0。読取り `native-physics-first.json` はOverlap1,714点blocked0、seam3lane双方向の連続Cast8segment hit0、境界7PASS、共有Collision67cell一致、Actor transform／Capsule・Camera保持によるPASS。実OSキー歩行は別証跡で確認した。

別入口 `Assets/Scenes/MapAuthoring/OverheadStudyA/StudyANorthConnectionNormalPlay-20261004-073259161.unity` は作成・readback成功、UseHumanInput=true／AutoTraverse=false／FollowActorCamera=true、Camera8、既存InputActions／本番CharacterActor2Dを使用する。実block union21×13、Camera clampは意図した24×16 frame。Play→Game画面を選択→全キーを離す→WASD／矢印で操作する。再生成はWorkspace `north-connection-prototype/study-north-connection-build.cs` の `StudyANorthConnectionBuild.Run(freshWorkspaceReport)` と出力QA recipeを使い、元pairから新Third Native／Scene／FineSoilを生成する。元pairの保存Settingsは二block recipeなので、通常Generate Selected Block Studyだけでこの三blockを再生成できるとは扱わない。

初回 `manual-sequence-first.json` はD2が予定15.1より0.1unit余分に進んだ厳密±0.055unit位置照合FAILを保持する。Actor failureは空で、物理失敗とは扱わない。`manual-sequence-resume.json` は実測起点(15.2000256,8.5)からD／W／DWの実Win32キーを再開し、North seam通過、第3block終点(21.44564,14.3456135)、北／東壁停止、逆方向seam通過、(8.480082,8.425074)へ復帰してPASS。全解放後command0、Actor failure空、Actor transform／Unity Input状態の書込み0。0.2unitは実OS入力のFixedUpdate量子化に対するwaypoint照合幅で、geometry・Collision・Physics閾値を変更していない。生成report内の実歩行falseは生成段階の記録として残す。

Rootと独立担当が通常画像 `north-normal-endpoint-1536x1024.png`／`north-normal-return-1536x1024.png` を実画素確認した（[独立レビュー](C:/Users/umiga/Documents/Codex/2026-10-03/task/north-connection-pilot-20261004/independent-visual-review-ja.txt)のWorkspace保存証跡）。広場→右へ湾曲する道→North pocketは読め、帰路はActor全身が見える。一方、終点Actorは樹冠に大半を隠され小さな兜だけが見え、到達位置／向きの視認性は未解決。裸砂面・階段状の境界・接地／手描き着色の限界も残るため、構造通行PASSを美術完成としない。Library新規保存は帰路 `libfile_36a93f5257bc81918e66b572d368f784`／終点 `libfile_75f0c2345a4081918244af062805f441`、双方version0、metadataApplied／local bytes保持を `.library-current-comparison-upload/north-two-image-metadata/prototype-direct-create-confirmed-identities.json` に記録する。

North段階の保全は `after-native-original-inputs-preserved.json` の453件全不変／unexpected0。変更前ZIP44,984,704bytes／SHA256 `91503ad528070b0677b6cfe67a371b88ec07a39223e5a81509063bb91d804ff5` とreadback証跡を保持する。`north-pilot-editor-restored.json` は停止・唯一clean Template・runInBackground=false、元PlayerSettings live JSON hash `7447231d…a1f7`／dirty=true復元を記録する。これはNorth段階の復元で、後続PaintedBlockの最終復元の代用にはしない。B guide／North再現helperを含む `FantasyRoyale_BlockPaint_Guides_NorthConnection_Prototype.zip` は6,155,676bytes／SHA256 `18702507de4028dbbdb4078b688c488834745a7582a257ea67e84b00ed6c3736`、新Library `libfile_81961a5295408191b233a8665fe84ca4`／version0へ保存成功した。既存20×10成果・P/V／地表4比較・旧Libraryは保持する。

親は先の5画像から、素材数／木Sprite差替えだけでは接地と手描きの色相・陰影選びの揺れを解決できていないと判断した。この1試作でvariation方式全体を否定せず、地区を一緒に描き合わせる独立prototypeへ進む。旧RouteB契約 `paint-block-notes/registered-bridgebank-contract.json` はR2 BridgeBank全域WorldRect(20,2,12,12)をnative768×768／64PPUに登録し、Camera(20,8,-10)／ortho8／capture aspect1.5を固定する代替提案として履歴に残す。旧ROI／Camera x17.78125とは分ける。concept `libfile_42c39b352a408191aec048872528972b` はRGBの見た目参考で、hidden groundやruntimeレイヤとは扱わない。現在は親が明示した、変更layoutと独立通行shapeを持つRoute Aの供給素材を使用する。B guide ZIPを置換してAの仕様であったかのように扱わない。

Route A素材はLibrary `libfile_9d42c5918ad0819183034493297513ee` の `FantasyRoyale_PaintedBlock_v1_Runtime_Test.zip`（10,034,129bytes／SHA256 `5452efcfc6a17a0717b318519d63ef82b78beba059ee7198e058bd49be8fd9ed`）。HomePC正規受取り後、Workspace `blockpaint-runtime-20261004/Inputs/` と `Unpacked/FantasyRoyale_PaintedBlock_v1/` に保持した。Rootと独立担当がPreview／Layer QAとruntime画素を読み、`independent-asset-audit.json`／`independent-asset-review-ja.txt` は15checksum全一致、RGBA768×768 base＋canopy合成がbaseと全点一致、canopy alphaが供給maskと一致、焼込みHeroを見ないことを記録する。新素材は12×12unit／64PPU、橋内側139×71pixel＝2.171875×1.109375unitで旧4unit橋／Collider／3unit portへdrop-inしない。nearest縮小素材を新規native768ドット作画とは呼ばない。

最初のbuildは新Sceneのactive化を検証器が拒否してGenerated=falseとなり、`painted-block-build-report.json` と初回helperを保持した。Workspace helperを修正した `painted-block-build-report-02.json` はGenerated=true／errors0、元Source・Scene・PlayerSettings保護を記録する。生成代表は `PaintedBlock-v1-20261004-084445691`、asset Rootは `Assets/Art/Map/OverheadStudyA/PaintedBlockStudies/PaintedBlock-v1-20261004-084445691`、独立map Sceneは `Assets/Scenes/MapAuthoring/OverheadStudyA/PaintedBlocks/PaintedBlock-v1-20261004-084445691.unity`。旧map／Colliderをinstantiateせず、manifest vector unionのfull pixel-cell四隅を内包する保守的通行maskから146個のBlocked Boxと外壁4個を作った。Inclusive PNGの72pixel幅へ床を広げていない。新fixture `StudyAPaintedBlockActorRenderSetup.cs` はこの別prototypeのRenderer order10だけを設定し、Actor Physics／Input／transformを変更しない。

通常入口は `Assets/Scenes/MapAuthoring/OverheadStudyA/StudyAPaintedBlockNormalPlay-PaintedBlock-v1-20261004-084445691.unity`。Play→Game画面を選択→全キーを離す→WASD／矢印で操作する。再生成はWorkspace `blockpaint-runtime-20261004/study-painted-block-build-combined.cs` の `StudyAPaintedBlockBuild.Run(freshReportPath)` を、保持したmanifest／runtime PNG／保守的maskとfresh出力条件で使用する。旧StudyA Settings／Generate Selected Block Studyではこの変更layoutの独立prototypeを再生成できるとは扱わない。実Cameraは(6,6,-10)／ortho8、capture1536×1024／aspect1.5、live GameView16:9。画面中央crop(384,128,768,768)が12×12blockで、B提案のCamera(20,8)や旧比較ROIと分ける。

`manual-sequence-first.json` は本番Actorを実Win32キーで15step動かし、西(0.25,6.03125)→橋中央(8.149994,6.03125)→東(11.7500076,6.03125)→橋へ戻り、北y6.281251／南y5.731874の床端・水側停止、浅いcanopy verge(1.34499931,3.83187556)と奥の境界停止、左側へ復帰してPASS。実行harnessはActor command／transform・Unity Input状態を書かず、全キー解放後command0。waypoint0.2unitはOS入力のFixedUpdate量子化の照合幅で、床・Actor・Physics設定を緩めていない。生成report内のactualActorWalkVerified=false等は生成時の記録として保持し、実操作proofと分ける。

初回 `native-physics-first.json` は宣言route880点のraw OverlapCapsuleで西(0.25,6.03125)／東(11.75,6.03125)の外壁hit2件によるpassed=falseを保持する。後続の読取り `native-distance-supplement-first.json` は同じ宣言virtual880位置×実Collider150件＝132,000 pairをexplicit poseのCollider2D.Distanceで測り、全valid／isOverlapped0／minimum distance0に限るPASS。元endpointのdistance0／isOverlapped=falseとraw wall hitは同時に残し、正の分離を主張しない。別観測の1pixel内側735点raw hit0は元queryFAILの置換ではない。Actor transform／Capsule／velocity・Camera・Physics profileを保持し、初回failureの再分類をしない。

さらに実OSで戻った確認時のActor(0.244999975,6.092358)にはraw Overlapの `BlockedRun_0109` と `BoundaryWest` hitがある。`final-live-state-preserved-02.json` の実pose全150Distanceはvalidだが、West=-0.005000025、BlockedRun_0109=-0.00360822678の2件がisOverlapped=trueを記録している。宣言virtual routeのPASSを現在poseや全Physicsの無接触へ拡張せず、壁停止後の実測を残す。geometryの変更や全QA成功宣言は行わない。

4地点の実engine ON／OFF／noActor画像は `native-render-pixel-audit.json` でPASS。noActor中央block cropはSourceRGBAとの差0、Actor Sprite差替え0。浅いprobeのON/OFF差179pixelは登録canopy alpha内だけで範囲外0、他3地点のON/OFF差0。同poseの実Actor表示で脚／足元に葉が重なることを確認し、offline proxy画像と分ける。[独立Native画像レビュー](C:/Users/umiga/Documents/Codex/2026-10-03/task/blockpaint-runtime-20261004/independent-native-visual-review-ja.txt)は根・苔・草・土、岸・水、橋端が一緒につながって見え、個別の木を平らな地面に載せた印象が弱まったと記録する。ただし新配置／小さい橋／縮小painted素材の別試作なので、描き合わせだけを改善原因とする旧R4同条件比較ではない。

ユーザーは現在のPaintedBlockの見た目を「この水準を目指したい」と肯定し、以後のvisual benchmarkとして採用する一方、一枚絵ごとの互換性を懸念した。これは見た目の基準の承認で、生成architectureや隣接接続の完成承認とは分ける。次の最小2neighbor契約は、現block中央を保持し境界の調整帯を分け、64PPU／道・川の明示port／palette／canopyを合わせる読取り設計として整理する。隣接art sampleは親が準備中で未受領、実seamでのHero通過はNOTRUN。契約は別成果として渡せるため、今回の有界prototype保存を新規art・Native build・frameworkの実装まで延ばさない。

これはbase0／実Actor10／固定canopy20の浅い重なりprototype。baseには樹冠RGBが残り、全木のY-sort・樹冠移動／伐採・深い木内通行・hidden ground・独立橋前景に対応していない。区画端は絵が切れ、隣接seam・自動組立は未評価で、A品質・汎用生成器・全map物理の完成としない。新しい美術・framework・追加QAはこの区切りの範囲に含めない。

実engine4PNGはLibraryへ新規保存成功、全version0／metadataApplied=true／local bytes不変を `.library-current-comparison-upload/painted-four-metadata-native/prototype-direct-create-confirmed-identities.json` で確認した。橋中央ON `libfile_22f0ddcc4bc88191a429386823fdb76b`、浅いcanopy ON `libfile_1849c21e808c81919840bc9112e8eb88`、同pose OFF `libfile_5462685762a88191b0067e6d6e503678`、橋中央noActor control `libfile_cb3a032d6c588191a58ef99bf167a8ce`。初回metadata適用のWSL起動失敗は保持し、同じofficial helperの限定再試行でidentity適用を成功させた。再uploadやauth retryはしていない。旧Library成果は置換しない。`FantasyRoyale_PaintedBlock_v1_Runtime_Proof_Source.zip` も新Library `libfile_b658d0c7349c8191bb61a1bf2da9b3c2`／version0へ保存成功、32,984,421bytes／SHA256 `6ac0934ee5efd251e7ec942798dac94af9fb657acd39ada2465b9735094e6dd4`、metadataApplied／bytes保持を `.library-current-comparison-upload/painted-source-zip-metadata/prototype-direct-create-confirmed-identities.json` で確認した。Workspace `painted-block-handoff-20261004/handoff-validation.json` は114entry／112source payload、実Native画像12枚・実キーpulse receipt15件を含み、CRC／元copyとZIP payload bytes一致／originals保持PASSを記録する。raw Physics FAILと現在の小Overlapも同梱履歴に残す。

import前保全は `blockpaint-runtime-20261004/before-import.json`、529件復元ZIP106,676,652bytes／SHA256 `ddfc618a436e84b8950f9c93dd867107a956c530e217ae034c7bde7416b7ddcb`、allEntriesReadbackVerified=true／originalFilesModified=false。`after-native-protected-inputs-preserved.json` は文書追記前の529件全hash不変／unexpected0を記録した。ユーザーが現状の表示保持を明示したため、今回はPaintedBlockの通常入口をPlay中・GameView表示・左側復帰poseのまま検査用に保持する。旧Templateへ戻すNorth段階の復元は今回の終了状態へ適用しない。`final-live-state-preserved-02.json` はreadOnly／Scene切替0、playing=true／paused=false、GameView focus、入口とmapの2Sceneがclean、Actor ready／manual=true／failure空／move0、runInBackground=false、元PlayerSettings live JSON SHA256 `7447231da8a9c929d5dc2570ea2c2132da7086064c5ff02603856ea63f50a1f7`／dirty=true保持を確認した。初回 `final-live-state-preserved.json` はUnityの.NETに無いToHexStringによるcompile失敗でcode実行前に停止し、失敗証跡を保持する。

追記元文書134,250bytes／SHA256 `fc768d4cc2dd20206735b272c61f453412ef75560b6622afe62957629e906473` の全prefixを保護して今回の結果を記録する。Rootがbackup付きguardで反映し、既存Knowledge validatorと文書反映後の保全確認を別closure証跡へ記録する。前項のFAIL／NOTRUN、A未達、異寸法・多方向口・核を保護する部分変形・自然な景観移行と連続通行／衝突を持つseamless biomeの原目標を保持する。

## paired絵素材の統合停止とPrefab構成への方針変更（2026-10-04）

ユーザーは現PaintedBlockの見た目を目指す水準として維持しつつ、「一枚絵だとアニメーションやギミック等入れられない」「プレハブとしてデザインを組み立てる」と指示した。これを最新方針とし、paired artの受取り・import・Native build・Scene統合を停止する。paired素材はHomePCへmaterializeせず、対応するSource編集・新Scene生成も実施していない。承認済みvisual benchmarkを、静止画方式や生成architectureの承認に拡張しない。現在の独立PaintedBlock通常入口はユーザーの確認用にPlay／GameView表示を保持する。

停止前の単体実証は別の完了した区切りとして保持する。元134,250bytesを保護した文書149,215bytes／SHA256 `46acf1781f7bd27d30c15ad56683e253f448b8fedb78845f67846c8276bd5c9d` はbackup付きguardで反映済み、Knowledge validator67件PASS。文書後の529件確認は528不変＋許可したprefix完全一致の文書追記1件、unexpected0。この段階のRepo変更は新規Runtime render fixture1,854bytes・独立PaintedBlock素材／Scene・文書追記に限定し、既存Sourceを上書きしていない。現在poseの負Distance2件、raw Physics FAIL、隣接seam未実証などの記録は変更しない。

読取りの2neighbor提案は停止前に `FantasyRoyale_PaintedBlock_Two_Neighbor_Contract.zip`（9,771bytes／SHA256 `a4886111eb20caef49c29886d5359244d5fa522fa7628fde80f03435bc7a4ffb`）を新Library `libfile_52f263d747408191a8ea0afbb1c3cb65`／version0へ保存した。Workspace `painted-neighbor-contract/` の契約とseam y394補足は履歴上の提案であり、paired実装・実Hero接続PASSを意味しない。新指示の下でpaired絵の統合を続ける根拠にはしない。

次は承認された見た目を独立した素材とGameObjectの組合せで組み立てる。アニメーションする水、ギミック、破壊可能な木・橋等の操作対象をStaticGroundへ焼き込まず、個別Prefab／表示layer／衝突shapeと動作を分離する。固定landmarkの核・明示port・pixel尺度・接地／前後関係を素材契約に含め、部分変形や連続するbiomeの目標を保持する。

読取りの確定契約はWorkspace [Prefab再構成契約](C:/Users/umiga/Documents/Codex/2026-10-03/task/painted-prefab-contract/painted-prefab-reconstruction-contract-ja.md) と同Directoryの `painted-prefab-reconstruction-contract.json`。[既存構造調査](C:/Users/umiga/Documents/Codex/2026-10-03/task/layered-prototype-collision-review/existing-composition-feasibility-ja.md)／`existing-composition-feasibility-scan.json`／`existing-prefab-component-inventory.json` は実Prefab59件＝Block55＋legacy Prop4、anim／controller0、独立した水・橋・破壊用Prefab0を記録する。既存ReadPrefabDefinitionはANY SortingGroupを拒否し、FineSceneryは既知Starter metrics以外を拒否する。FootSortProxyは1Sprite前提。FixedSceneryのInstantiateもsource-linked PrefabInstanceの保存ではないため、任意Prefabを旧入口へ渡せるとは扱わない。

最小案は品質を合わせた下地補完・個別木／接地影・水と必要frame・橋床／柱／欄干等が届いてから、専用の新しい構図fixture1個とlinked部品Prefab、新Scene1個で試す契約。古いSpriteへ見た目を差し戻さない。現在のbase＋南樹冠だけではhidden groundが不足し、素材抽出・Prefab／Scene再構成・アニメーション／ギミック実装はNOTRUN。これらの契約と調査はRepo／Native／素材変更0の設計証跡で、paired絵の統合再開や機能完成を意味しない。

## 独立Prefabの有限Runtime実証（2026-10-04、技術結果と美術判断を分離）

正規受取りした91入力から、`IndependentPrefabs-v1-20261004-102316219` を新規生成した。`independent-prefab-runtime-20261004/independent-prefab-build-report.json` はGenerated=true／errors0、構図Prefabと内部の個別Prefab16リンクの保存readback、474原本・元Scene・PlayerSettings保護を記録する。区画768×640／64PPU／12×10unit、Camera8。元生成器の既知metrics制限を通すために旧素材へ置換せず、独立した固定橋・部品owner・局所占有照会fixtureである。生成時点の歩行／animation／撤去verified=falseは書換えない。

`finite-runtime-proof-first.json` は実Win32キーを受ける本番Actor(.48×.55／offset(0,.22)／speed5)の有限ケースPASS。橋を双方向に歩き、橋OFFで表示3／rail・corner4をinactive、水gap40をactive、owner占有12→48に切替え、西岸x5.548386で停止。復元で48→12、x10.648まで再横断した。対象木OFFでは幹・樹冠・接地影等5rendererとBodyを外し、占有12→11、旧根位置y5.299→7.299を通過。復元11→12と前／後の描画も実画像で確認した。Actor transform／Body command／Input状態をharnessで書かず、geometryを変えないOS入力照合幅0.16unitを使う。これは撤去状態の試用で、攻撃・伐採・耐久値やproduction AIを実装済みとは扱わない。

水は独立64frame／8fps／8秒loop。実engine frame61／1の差1,662pixelはRGB水mask投影内のみ、陸・静的小物・背景差0（`independent-prefab-input-review-20261004/native-first-audit.json`）。そのmaskの白を非歩行水とする意味はRootが採用した局所技術案で、原作者のnavではない。木ON/OFFは同Actor footとframeで所有5部品のPoint投影alpha域外差0。整数nearest再構成の生111差も残す。橋ON/OFF画像はpose／frameが異なるため同条件pixel差分とはしない。独立 `native-first-review-ja.md` は有限技術PASSを支持する一方、4大木の反復・地形外へ出るtree／shadow・岸の反復・橋の強い占有を挙げ、美術は未承認。behind poseのActor全隠れも最終視認性仕様ではない。

通常入口は `Assets/Scenes/MapAuthoring/OverheadStudyA/StudyAIndependentPrefabsNormalPlay-IndependentPrefabs-v1-20261004-102316219.unity`。Play→Game画面→全キーを離す→WASD／矢印。再生成は停止・clean EditorでWorkspace `study-independent-prefab-build-combined.cs` の `StudyAIndependentPrefabBuild.Run(freshAbsoluteWorkspaceReportPath)` を使う。同Directoryの `README-reproduction-ja.md` に64PPU・owner・保存Scene／Prefab・入力／Runtime SHA guardを記録し、新timestampへ出す。通常画像は新Library `libfile_f215e30d5b048191a94fe1f656c88756`、残る水2／橋2／木3を含む8件すべてversion0、metadata／bytes保持は `.library-current-comparison-upload/independent-prefab-native-metadata/prototype-direct-create-confirmed-identities.json` に記録する。

初回 `native-geometry-present.json` の暗黙Staticへの位置指定Distance Runtime Error（内側result=false）は保持する。読取り `native-geometry-{present,absent}-v2-proof.json` は橋ON／OFF有限検査PASS。代表線y4.640625の705地点で全active ColliderへのOverlapCapsuleはON0、OFF222地点／3,644hit。双方向CastはON0／0、OFF水gapへ17／17hit。補足Distanceだけ暗黙StaticをON12／OFF8個除外し、Overlap／Castからは除外しない。別の名目中心線y4.734375のON生61hitも残し、全Physics成功とはしない。

`final-normal-state-readonly.json` は通常入口を再開しPlaying／Ready、Actor(.65,4.640625)・入力0、owner11すべてPresent／StateChanges0、局所占有12、水8fps・6loop、足SortingGroup WorldObjects／0を確認。GameViewをそのまま維持する。PS内容SHA・元dirty=true／background=falseも保持。`protected-before-doc-append.json` は474原本不変と新Runtime4 SHA一致を確認した。元文書153,052bytes／SHA256 `45dea857c3b40c16add72576569e5e3eadaca70678a46a5f06c3a35a1ed44d23` の全prefixを保全し、A完成・量産生成・全領域衝突保証は未達のまま。

## 独立Prefab v2の配置と有限実行（2026-10-04）

`independent-prefab-art-v2-runtime-20261004/independent-prefab-v2-build-report.json` は新規 `IndependentPrefabs-v2-20261004-111819103` のGenerated=true／errors0、owner25・個別Prefab30リンク、初期局所占有22を保存readbackで確認する。Box構成はstatic水140＋外壁4、owner22、通常inactiveの橋gap40で計206。v1の地形・水・固定橋定義は全文一致、岸eddy7だけ新配置に合わせて再clipし、旧成果とRuntime4を変更しない。Camera8／64PPU／12×10unit、生成段階の実歩行等verified=falseは保持する。

`finite-runtime-proof-first.json` は同じ本番HeroのWin32実キー16回すべてneutral、有限ケースPASS。Root選定safe-core経路y4.640625を橋の両方向へ歩き、橋OFFで局所占有22→58、水際x5.548386で停止、復元58→22でx10.6483936まで再横断した。木 `nw_main` は幹でy5.400926停止、OFFで占有22→21と旧根位置を越すy6.700925通過、復元を確認。Actor transform／Body／Input命令をharnessで書かず、位置照合0.16unitはOS／FixedUpdate量子化だけに使う。全木・全Physics・production AI／攻撃・伐採実装には一般化しない。

独立 `independent-prefab-art-v2-input-review-20261004/native-v2-first-audit.json` は水frame23／25の1,440pixel差がRGB水投影外0、対象木ON/OFFの同pose／同frameと他owner保持を記録する。木のdouble投影域外5pixelは初回生FAILを保持し、別 `native-v2-point-projection-supplement.json` の正確なfloat32 Point投影では域外0（alpha膨張・閾値緩和なし）。`native-v2-first-review-ja.md` は実8枚を確認し、大小の配置と幹・影の地形外突出改善を認める一方、behindの全Actor隠れ・canopy反復・大きな固定橋・平坦な細部を残課題とする。美術は未承認、地面は静止画のまま。

追加 `depth-behind-target-on-1536x1024.depth-pair.json` は同Actor／Camera／水frameで対象木だけを描画ON/OFFする観測PASS。両画像で他ownerを描画抑制し、対象のPresent／Collider／navはONのまま、Actor／入力を変更せず描画・水pause・force flagsを復元した。初回2回はInputEnabled=falseの事前停止でwrites／captures0。原helperとFAILを保持し、入力を使わない派生helperだけそのguardを外してfalse状態を観測・保存した。独立 `native-v2-depth-pair-audit.json` は32,980pixel差が所有5部品のfloat32投影外0、全段階の保護状態・Collider／nav一致を確認。対象木ONだけでHeroを全隠し、描画OFFで同poseのHeroが見える。足sort契約には整合するが、全隠れを表示品質承認とはしない。この対画像は入力falseの観測で、操作可能な状態の証拠には使わない。

通常入口は `Assets/Scenes/MapAuthoring/OverheadStudyA/StudyAIndependentPrefabsNormalPlay-IndependentPrefabs-v2-20261004-111819103.unity`。Play→Game画面をクリック→キーを離す→WASD／矢印。代表通常画像は新Library `libfile_44880b76ee788191b9547e3e3d58d761`。再生成は停止・clean Editorで `study-independent-prefab-v2-build-combined.cs` の `StudyAIndependentPrefabV2Build.Run(freshAbsoluteWorkspaceReportPath)`。同folder `README-reproduction-after-doc-append-ja.md` に部品撤去メニュー・固定設定を記録し、配置は固定manifest、seed引数なし。保存Sceneを開く→Playの再現は確認済みだが、この追記で690保護manifest内の文書SHAが古くなり既存Frozen builderのRun再実行は停止するため、新runの再生成には現source／inputに対する新しい復元可能backupと検証済manifestを用意し、旧690backup・Native証拠・metadata／成果物を上書きしない。`closure-readonly-pass2-report.json` は元690ファイル・Runtime4・ProjectSettings全27不変、eddy7のclipを確認する。直下26だけを数えて27と比較した初回FALSEも保持。

`final-normal-state-readonly.json` は再開した通常入口のReady／manual／InputEnabled／GameView focus、開始Actor(.65,4.640625)・命令0、owner25すべてPresent／StateChanges0、nav22、水unpaused／8fps／7loopを確認。Play中も保存済みのclean2Scene、元PS内容SHA・dirty=true／background=falseを維持する。現文書157,371bytes／SHA256 `f49f67ef17987e51010c6bb86f311293159d0f14beb367d3c763789f8678ab62` の全prefixを保全する。更新後保全はRootのguarded install後に確認する。

## 独立Prefab v3の岸・水差分と有限比較（2026-10-04）

v3入力ZIP（Library `libfile_7e1f5425572081919b6cacbca417a5bf`／version0、8,296,129B／SHA256 `97c7cd5758af60b9d3465bd250a9de74c26c581ce4adfa799e16197c1d01aaea`）をHomePCの正規経路で受取り、83fileを安全展開した。岸・水mask/base・64水流frameを新しい非対称輪郭へ差し替え、地面全byte・木・橋・全owner root・Runtime4を保持する。eddy7個を元画像から再clipし、子の中心Xは5変更＋2維持。地面変形案は除外され、道の直線性は残る。水mask全Xのrows `[241,416)` と端32row、橋床と橋穴40矩形を固定し、静的水だけを新maskから再生成する。新水流frameは未clipの元flowから作り、新しく濡れた領域のhighlightを欠落させない。

workspace `v3-prefab-bank-water-runtime-20261004/independent-prefab-v3-build-report.json` は新規 `IndependentPrefabs-v3-20261004-123422522` のGenerated=true／errors0、owner25・個別Prefab30リンク、Map/入口の保存readbackを確認する。Boxは派生recipeによる静的水283＋外壁4＋owner22＋初期inactiveのgap40で計349。`pre-doc-preservation-closure.json` は原本936件・Runtime4・ProjectSettings27・旧v2 assets241と2Sceneの不変、新asset244件／8,452,797B、eddy7のclipを確認する。生成段階の実歩行等verified=falseは当時の記録のまま保持する。

`finite-runtime-proof-first.json` は実Win32キー16回すべてneutral復帰、有限ケースPASS。本番Heroの元Capsule／Input／速度を保ち、代表線y4.640625で橋を双方向に横断した。橋OFFは占有22→58、水際x5.548386で停止、復元58→22でx10.6483936へ通過。木 `nw_main` はy5.400926で停止、OFFは占有22→21と旧根を越すy6.700925通過、側方で復元後に背後へ戻った。0.16unitの照合幅はOS／FixedUpdate量子化だけに用い、Actor／Body／Input命令をharnessで書いていない。

独立 `v3-prefab-bank-water-input-review-20261004/native-v3-first-audit.json` は水frame30／33の1,699pixel差が水域外0、同pose／同frameの木ON/OFF差31,420pixelが所有5部品外0、幹・影・接地等36部品の新land外0を確認する。`native-v3-depth-pair-audit.json` は同背後poseで対象だけ描画ON/OFFした差32,980pixelの所有域外0、22 Collider/nav対応、Actor／Camera／Input／全owner保護と描画・水の復元を確認。対象はPresentのままで、HeroはONで全隠れ・OFFで可視。恒久fade／outlineの方針は変更しない。

岸 `northwest_rocks` は同pose／水frame40で岩本体・所有eddyの2描画とBodyを外し、占有22→21→22で復元した。初回 `shoreline-owned-removal-proof.json` のRenderer数3決め打ちFAILを保持し、manifestの正確な2部品に基づく別 `shoreline-owned-removal-proof-validated.json` で実7callを読取り再評価した。独立 `native-v3-shore-validated-supplement.json` は新eddyのRGBA再clip一致／水外0、2画像差2,395pixelの所有域外0を確認。初回投影の境界1pixel/FALSEも残し、実ONと元opaque texelが一致するCENTER inverse float32 Point式を別補足へ記録した。alpha膨張・閾値緩和・native再実行はなく、GPU演算自体のinstrumentを主張しない。全owner root不変の限定差分に従い、移動試験／動的reclipはNOTRUN。

通常入口は `Assets/Scenes/MapAuthoring/OverheadStudyA/StudyAIndependentPrefabsNormalPlay-IndependentPrefabs-v3-20261004-123422522.unity`。Play→Game画面→キーを離す→WASD／矢印。Camera8／64PPU／12×10unit、比較Capture1536×1024。配置は固定manifestでseed引数なし。workspace `v3-prefab-bank-water-runtime-20261004/README-reproduction-ja.md` に保存Prefab/Sceneと状態切替を記録した。実12PNGを独立に見て岸の非対称形と維持した林・道・橋を確認し、offlineの色／位相差は一致を断定しない。直線道、樹冠反復、大きい固定橋、Hero全隠れ、外側の単色背景は残課題。参照Aの美術完成・全Physics・汎用自動生成・production AIへ有限ケースを一般化しない。

実12PNGは同workspaceに保持する。Library出力保存は正規prepared helperの1回実行が認証初期化でexit1、app結果／IDを得ず0件保存。証跡は `.library-v3-native-evidence-upload-20261004/ordered-twelve-native-stage-01/upload-helper-completion.json`。入力2件の正規受取り成功とは分け、旧v2画像IDを流用しない。開始済みhelperからのdirect切替／retryは行わず、素材ZIPはHomePC内だけで保持する。

`final-normal-state-readonly.json` は保存入口を再Playし、Ready／manual／入力有効／GameView focus、開始Actor(.65,4.640625)・命令0、全25owner Present／StateChanges0、nav22、水unpaused／8fps、clean2Scene、元PS内容／dirty=true／background=falseを確認する。936件backupは最新資料161,964B／SHA256 `1ce9d67a6132d284e4ebfb860749c08ba883e915cf37a4eac1a608b970d11f6a` を含み、その全prefixを保全して追記する。反映後の保全と `Tools/Knowledge/validate-knowledge.ps1` はRootが別closureへ記録する。保存SceneのPlay再現は追記後も使えるが、凍結初回Builderは旧文書SHAを保護するため再Runで停止する。再生成は最新source/inputと元作業の新しい復元可能backup／manifestを備えた新runを用い、旧証跡・backup・guardを上書き／緩和しない。

## 独立Prefabの東隣接2ブロック連結（2026-10-04）

既存v3のA12×10unitと新B6×10unitをorigin(12,0)で連結し、linked A/Bと新Pair構成Prefabを18×10unionへ保存した。Aの素材・全25owner・橋・水・Runtime4・source Prefab・旧Sceneは不変。変更は新Pair instance overrideだけで旧A外周Collider4と旧navを除き、共有registry1と閉外周4を配置する。内部x12壁・新floorは0。Bはground384×640px／64PPU／center(15,5)と木3・下草2・岩2の独立7owner、13renderer。木3のみBody/navを持ち、合計32owner／初期nav25。static水283＋外周4＝287、初期active Box312、installed352にはinactive Bridge gap40を含む。`component-prefab-two-block-runtime-20261004/pair-build-first-report.json` の保存Map／通常入口readback・source保全はtrue。生成時の実操作・画像検証falseは書き換えず、後続証拠と区別した。

A col767とB col0は全640row RGBA exact、列SHA256 `e2bfd811478bb0c3c57740bccf3fc62209b0db6be288e715d664fd402c3e67fc`。供給contractはB先頭32列だけの調整を宣言するが、調整前B原本は未提供で、その変更前後一致は独立未検証。今回保存したB groundと供給east_ground_clean.pngのバイト一致は確認済み。足(12,4.640625)はgrid-edge y343でtexel centerではない。入力のcrossing_band_x562は実Body rows[310,348)の説明に限定し、visual全帯[241,416)には水946pxがあることを保持する。raw入力とAを変更せず、既存v3 geometryを使った。独立保存geometry監査 `component-prefab-two-block-geometry-review-20261004/saved-native-pair-independent-review.json` は限定110照合PASS。

`finite-pair-runtime-first.json` は71records／22実OS pulses／14実Capture。最初の実往復は足(.65,4.640625)→(17.350029,4.640625)→(.55000037,4.640625)。目標17.25／返却.65との差に使う.16uはOS／FixedUpdate量子化だけの許容で、geometry／pixelには適用しない。1065posesと全距離往復CapsuleCastは橋present／restoredでoverlap・nav・cast全0。橋OFF原本はpassed=false／routeClear=falseのまま期待されるgap閉鎖を示し、nav25→61→25、実Heroはx5.548386で停止、復元後再横断した。東の木presentで実y5.548151停止、撤去後y6.74815へ通過。停止姿勢のlive overlapはOwnedBody1件、撤去直後0で、既存.01u contact範囲の結果として記録する。侵入深さは未測定で全Physics承認ではない。各B7ownerのremove／restoreは他31ownerとA25を保持、全B OFFは13renderer非active／nav25→22→25。

通常入口は `Assets/Scenes/MapAuthoring/OverheadStudyA/StudyAIndependentPrefabPairNormalPlay-IndependentPrefabPair-v1-20261004-143803789.unity`。Play→Gameビュー→キーを離す→WASD／矢印。固定manifest SHA256 `74c2cd5817943a9d72bb82c2d537f63d98761048f9e584599ea444ae97aa8ad7` でseed引数なし。保存Captureはortho8／1536×1024／aspect1.5、広中心(9,5)と元中心(6,5)を同Actor・水frame29で比較した。最終live GameViewは中心(9,5)、aspect1.77777779で区別する。詳細と14枚の実画像はworkspace `component-prefab-two-block-runtime-20261004/README-reproduction-ja.v2.md`。新画像と素材ZIPはlocalのみ、前段Libraryは認証初期化に失敗して画像保存0件となり、retry／direct切替を行っていない。

`component-prefab-two-block-input-review-20261004/native-pair-first-independent-audit.json` は実14PNGを読取り、A旧nativeとの差1463pxは水外0、接続Body帯38rowは両Camera／全B ON/OFFでRGBA exact、水frame4→8の1613px差分は水外0、木ON/OFF20830pxと同停止姿勢20404pxはowner source alpha投影外0。一方、全B撤去68698px差分の厳密CPU alpha投影外は40／41px、追加float32 UVモデルでも最小37px残る。GPU補間未計測の境界投影の未解決事項として当該gate=falseを保持し、mask膨張・閾値緩和・PixelSnap推測で0にしない。B col32..383の225280pxは裸地source RGBA exact、残る先頭29列の3266pxは不変A所有樹冠の張り出しで、B残留描画とは分類しない。所有・通行の限定結果と完成見た目承認を分ける。完成art／汎用生成／本番AI／全Physicsは未承認。owner移動・動的reclip・canopy単独操作・新sway・攻撃／伐採は未実行。

fresh backup1185件は最新資料167,537B／SHA256 `588f77137ecf011d7c5f6d5014c829a3b53628dd7910a4d854737c32a415d61b` を保護し、全文prefixを保持して追記する。`post-native-preservation-closure-01.json` は追記前の原本1185件／Builder保護source1213件・Runtime4・PlayerSettings27件・v3 assets244件と旧Scene不変、新asset28files／3,519,566B、Root meta＋Scene/meta込み新33filesを確認。`final-normal-live-inspect-validated.json` は保存入口Play再起動後、Hero(.65,4.640625)／command0／manual・Input・focus、32owner present／StateChanges0、nav25・static287・内部壁0、水64frame／8fps／unpaused／Failure無し・Actor overlap無しを確認。2保存Scene clean、PlayerSettings JSON SHA7447231da8a9c929d5dc2570ea2c2132da7086064c5ff02603856ea63f50a1f7／既存dirty=true／background=falseを保持しPlay中保存無し。追記後のKnowledgeと保全はRootが別closureへ記録する。初回Builderのdoc SHA guardは追記で古くなるため、保存SceneのPlayで再現し、再生成は最新source/input・元作業を含む新backup／manifestとfresh runで行う。

## 全B撤去のsource-alpha投影差をGPU実測で確認（2026-10-04）

前節の40／41px、追加UVモデル最小37pxは旧CPU Point sampling数学モデルのfalse判定として原本・旧README v2・14画像とともに保持する。元描画部品の実GPU alphaを独立に測った結果、B所有13部品の支持範囲外0を確認できた。旧falseを書き換えず、corrected original-material GPU source-alpha gateの成立を別証拠として追加する。完成art／汎用生成／本番AI／全Physicsの承認範囲は広げない。

Rootが実行した `alpha-discrepancy-gpu-20261004/native-original-alpha-v2-first.json`（1,891,157B／SHA256 `ff4cd12bfff80fc3114dccbbccdb824ff1a502fae0d720e4e8cabceeae69e116`）は、同じoriginal SpriteRenderer13個・material・mesh・UV・Point・tintを1部品ずつ描く。ARGBFloat／1536×1024／camera(9,5)・ortho8・aspect1.5／MSAA1、空背景のfloat alphaは全0。有限raw float alpha>0の厳密判定で得た13支持のunionは68698px、旧frame0の全B ON/OFF差68698pxの支持外0。新しく実SetPresent(false/true)した同Actor・同水frame14のARGB32 controlでもbyte／float差68698px、支持外0、復元ONはbyte・floatとも完全一致。素材／shaderの置換、mask膨張、近傍追加、epsilon、閾値緩和は行っていない。

旧40／41のunion42座標はすべてoriginal部品の実alpha1.0。内訳は北下草37、南木canopy4、南下草1で、旧CPU支持と実GPU samplingの不一致を示す。所有撤去・復元の不具合や水phase、部分alpha／AAによる差という説明にはしない。ただしGPU内部の正確な補間演算順・選択texel index・hardware bitは計測しておらず、その特定まで主張しない。独立検証の最終判定とsource対応は `newalpha-discrepancy-20261004/actual-original-material-gpu-alpha-validated-supplement.json` に記録する。独立補足は975照合／失敗0でPASS。最初の独立監査raw=falseは、補助Float描画のlinear RGBと通常ARGB32のsRGBを直接比較した監査上の誤りとして原本を保持した。同形式の42座標で旧ON・新ON・候補元texel RGBAが完全一致し、linear→sRGB8bit再符号化も完全一致した。RGB説明補足でalpha支持を変更せず、GPU内部の変換演算順も特定していない。

診断開始時のユーザー位置Hero(12.5087757,4.032297)、Input=false／focus状態／command0、Camera(9,5,-10)・ortho8・live aspect1.77777779を保持し、reset／Play再起動／Actor・Input・Body書込をしていない。B7ownerは実0→1→2 StateChangesを保持、nav25→22→25、他A25とstatic287は不変。比較中だけ停止した水frame14を元のunpausedへ戻した。fresh backupは1218件／36,249,411B／SHA256 `50eb6e97d35281141d4439dc5879f189666438afada99779ba62daa973eb28c2`、資料173,090B／SHA256 `07a661c48d0aeb093e8972f69aefc410365fbfe2ecfc2636d4bac2023978a06b` と新Pair33件を保護し、全文prefixへ追記する。新資料はworkspace `component-prefab-two-block-runtime-20261004/README-reproduction-ja.v4.md`。旧handoff・旧fail報告・README v2を保持し、新GPU画像／ZIPはlocalのみ、Library操作／retry無し。反映後のKnowledgeと1218件の追記保全はRootが別closureへ記録する。

## A画風の木1本と限定地表の比較pilot（2026-10-04、限定Native技術PASS・美術の最終レビュー待ち）

ユーザーの「近づいてますね 以降もピクセルの荒さは揃えていきましょう」を、今回の候補画風の方向承認と、以後の通常Cameraでの粒度合わせの指示として扱う。完成品質や全素材の採用承認には広げない。既存2ブロックPairを新Sceneへ複製し、西側の代表木 `MapRoot/WestBridgeA/nw_main` 1本と、その足元付近の限定地表だけを比較する。14本への展開、既存PrefabへのApply、全地形置換、新しい揺れ／攻撃／伐採／AI機能は今回の対象外である。

### 受領資料と原本保全

指定ZIP `FantasyRoyale_ConceptStyle_Pilot_v1_OFFLINE.zip`（Library `libfile_e3a1c04fb15881918581d60372f7058b`、5,060,901B／SHA256 `2e5c04e488117ff68d67fb40e11ab841abe7915db9e803f1b54f1943d8706b02`）、refined comparison PNG（972,425B／SHA256 `23f404cfa765bab714d05151d271c120a4ca7afd3a4a892e78769becb436600d`）、ownership PNG（566,658B／SHA256 `e2e58c4258fbb1153d3000eda3eba48b115e3f05b2b6ccbd5f195a93ee56b73a`）を、公式helperを変更しない正規WSL経路でHomePCへ受領した。Windows／WSL双方のbytes・SHA・Library属性、ZIP CRCと23fileの安全展開を確認し、Rootと独立担当が実PNGを閲覧した。最初のWindows属性処理の失敗記録は保持し、後続の正規受領成功を別記録とする。根拠はworkspace `.library-style-pilot-consumer-20261004/consumer-wsl-corrected-materialization-receipt.json` と `verified-inputs-and-extraction.json`。

画風・寸法contractはLibrary read完了後のsemantic text exportである。Library報告6,433Bに対してexportは6,432B／SHA256 `4635d7665df6ef9c553726df6b687ae7e27b4f9beba698eb5835303c655507e1` で、source exact bytesの確認はfalse。このテキストを画像／ZIPと同じbyte-exact materializationとして扱わない。供給側のoffline QAの `engine_verified=false`／`user_approved=false` はその時点の原本として保持し、最新の方向承認と今後のNative結果を分ける。

HomePCでは候補画像の作画・再着色・再sampleを行わず、供給された最終PNGをそのまま使用する。生成出典、3本のPrompt全文、最終5PNGのSHAは生成Asset台帳へ追記する。正規受領に使ったprivate URL／credential／transfer payloadをRepoへ複写しない。

### 固定寸法と地表の限定範囲

木の足anchorは既存 `(2.03125,5.953125)`、親uniform scaleは `0.87`。全Spriteはcenter pivot `(0.5,0.5)`、64PPU／Point／非圧縮RGBA32／sRGB／Mipmapなし／FullRect／physics outline生成なしを指定する。local XYは親scale適用前の値である。

| 部品 | 元canvas px | local XY | child scale | 親を含むscale |
| --- | --- | --- | ---: | ---: |
| canopy | 256×256 | `(0,2.1623563218390807)` | 1 | 0.87 |
| trunk | 100×132 | `(0,0.75)` | 0.75 | 0.6525 |
| owned shadow | 128×44 | `(0.015804597701149472,-0.0402298850574713)` | 1.0574712643678161 | 0.92 |
| contact vegetation | 128×48 | `(0.007183908045976986,-0.09698275862068963)` | 0.9770114942528736 | 0.85 |

canopyの旧local Y1.875からの差は `16/(64×0.87)` で、通常Capture上で16px上げる指定。shadow opacityは0.36。trunk／canopyは同じ足元SortingGroup、shadow／vegetationは木に所有されたMapGround。旧 `contact_3`／`contact_4` は両方OFF、新vegetationは1個だけとし、復元後も旧2個を再表示しない。対象ownerは全6renderer中4個を通常activeとして管理する。

地表の原本 `clean_ground_patch.png` は512×512pxの完全な不透明下地で、scale1なら8×8unit。今回の限定表示は原本Textureを参照する `Sprite.Create` のsourceRect `[128,0,304,512]` とし、WorldRect `[0,1,4.75,8]`、center `(2.375,5)`、scale1／64PPU／center pivot／MapGround order1を使う。両Rectは `[x,y,width,height]`、sourceのXは `[128,432)`、WorldはX `[0,4.75)`／Y `[1,9)`。新しいcrop PNGを作らず、原本の色・画素を変更しない。これは西側の小範囲で材質を比べる試験で、seamless tileや旧768×640地形の承認済み置換を意味しない。道・川・橋・東接続seam・既存Body/navを維持する。

64PPUはWorld寸法の指定であり、描いた葉・草・樹皮のクラスターの粗さを保証しない。1024px高／ortho8では64screen px/unitとなり、元1texelはcanopy0.87／trunk0.6525／shadow0.92／vegetation0.85／ground1screen pxに相当する。厳密な共通2×2／4×4gridやindexed paletteは未成立。通常Cameraで約2pxの輪郭step、意図的な1pxの細線と大きな連結色面を目標に、実表示の粒度・接地・移動時のちらつきを確認する。今後の素材は最終表示寸法に合わせて作り、旧instanceのscale違いへ無断展開しない。

### Native実行・検証・再現

初回 `style-pilot-runtime-20261004/native-style-build-first-report.json` は、新groundのcomponent数guardがTransform＋SpriteRendererの2個に対して3個を要求したため保存前に `generated=false`。次の `native-style-build-second-report.json` も入口field比較でfalseとなったが、読取り補足 `copied-entry-diagnostic-exact-two-field-diff.json` はMonoBehaviour wrapper内で許可した2本のMap pathだけが異なることを確認した。2つの失敗候補 `ConceptStylePilot-v1-20261004-180409403`／`ConceptStylePilot-v1-20261004-180945970` と報告は削除・上書きしない。候補画のbytesは変更せず、helperの確認を修正して別候補へ進んだ。

`native-style-build-third-report.json` は新規 `ConceptStylePilot-v1-20261004-181826149` の `generated=true`／errors0、Mapと入口の保存readback、元source／Scene／PlayerSettingsの保持を確認する。新rootは `Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/ConceptStylePilot-v1-20261004-181826149`、Mapは `Assets/Scenes/MapAuthoring/OverheadStudyA/IndependentPrefabs/ConceptStylePilot-v1-20261004-181826149.unity`、通常入口は `Assets/Scenes/MapAuthoring/OverheadStudyA/StudyAConceptStylePilotNormalPlay-ConceptStylePilot-v1-20261004-181826149.unity`。他31owner・元ground／water／bank・source Prefab linksとBody/nav geometryの保持、新5PNGの原本SHA、対象6renderer中4active・旧contacts2個OFF・新vegetation1個を保存状態で確認した。使用builderはworkspace `tree-ground-style-pilot-runtime-20261004/StudyAConceptStylePilotBuild.cs`、SHA256 `ac89adca26dfadcec6f124702e06a9a8b5d45f60e0b680e56fb0c8be79b227e3`。生成時の `actualHumanWalkVerified=false`／`actualOwnerRemovalVerified=false`／`sourceAlphaSupportVerified=false`／`pixelsReviewedInEngine=false` は当時の報告として保持する。

Rootが通常入口を実Playで開き、既存 `ForestRestartAssetBuilder.CaptureCamera` で現在の本物Cameraから1536×1024の `style-pilot-proof-20261004/normal-style-minimal-first-1536x1024.png` を取得した。1,917,792B／SHA256 `f37fd4f383431a1d7d0c94a59c13a174d9c2229a859cbe7a330515630ecbd811`。camera `(9,5,-10)`／ortho8、Hero足 `(.65,4.640625)` は変更せず、targetTexture nullとlive aspect1.77777779への復元を確認した。Captureはaspect1.5。metadataは `normal-style-minimal-first-capture.json`。Rootと独立担当が実PNGを閲覧し、新しい代表木と限定地表を確認した。x4.75の地表境界が見え、完成した地形の遷移としては承認していない。

最初のfull proof用通常Captureは110秒でtimeoutとなりPNGを得なかった。`normal-style-first-capture.json` を保持し、短いNative state読取りでPlaying／Ready／Failure空／camera targetTextureなしを確認した後に上記既存Capture経路で実画像を得た。timeoutを検証PASSに読み替えない。

`style-pilot-proof-20261004/phase-native-owner-first.json` は新しい4partのoriginal material／mesh／UV／Point／tintによる厳密raw float alpha>0のunion32,113px、empty alpha非zero0を記録する。通常ARGB32 ON/OFF差はbyte／floatとも31,149px、支持外0。union全域との完全一致は判定条件に含めず、実差分の支持外0を照合した。実 `nw_main.SetPresent(false/true)` によりnav25→24→25、全6renderer・Body/navがOFFとなり、復元ONはbytes／floatとも元ONと完全一致。counter0→2はresetせず保持し、旧contact2個はdisabledのまま4部品だけを通常activeへ戻した。他31owner・Hero／Input／Camera・地表・static・水と元material等は保護され、restoreErrors0。独立 `style-pilot-independent-native-review-20261004/actual-phase-owner-first-independent-audit.json` は83照合／失敗0。元4個のraw f32を独立に再構成し、mask膨張・epsilon・閾値緩和・shader置換を行っていない。

`phase-native-ground-first.json` は地表だけの描画ON/OFF差52,955px、登録crop投影 `[192,256,496,768)` の外0、元water／bank／bridge7部品のNative GPU alpha支持上の差0、復元ONのbytes／float完全一致を確認する。ownerのPresent呼出やcounter resetを行わず、nav25・static287・32owner・counter2を保持した。独立 `actual-phase-ground-first-independent-audit.json` は40照合／失敗0、通常RGBAとsemantic water mask、exported display alpha支持を独立に確認した。7部品のraw f32はexportされていないため、そのraw guard自体はNative reportをauthorityとする。限定cropの表示・保護PASSを全地形置換や完成art承認へ広げない。

Rootは実OS WASDでHeroを木の南側 `(2.049999,5.24062443)` と北側 `(2.44999862,6.540623)` へ移動し、各actual-pose PNGと同姿勢の描画OFF controlを保存した。`phase-native-depth-below-first.json` はcanopy／trunkと重なる不透明Hero138pxすべてが既存contextで見え、対象木の描画OFFによる変化0を示す。`phase-native-depth-above-first.json` は不透明重なり1,506pxのうち、対象木OFFのcontextでHero寄与を測れる1,393px全てが木ON/OFFで変化する。残る113pxは不変の隣接木等でHero寄与を確認できないため、Hero遮蔽の確定証拠から除く。両者はWorldObjects／order0共通の足元Y sortと一致し、Actor／Body／commandを直接書かず、描画・Camera／Input・owner／水を復元した。北側のBody.position Y6.540623とsortが使うgroup transform Y6.54062366は別々に記録し、強制的に同値とせず原因を推測しない。独立 `actual-phase-depth-first-independent-audit.json` は93照合／失敗0。全Heroの不可視化や全マップPhysicsの証明にはしない。`forceRenderingOff` のcontrolを所有撤去試験と混同しない。

`phase-native-moving-first.json`（559,896B／SHA256 `1f7cc2517f1268e2f944c9970cdd031d1791db51c0efcf0af4ce56cf6a7cb854`）は固定Cameraでの実OS入力をNative callbackで観測し、5枚の通常frame、入力保持1frame、Body位置変化2frameを確認した。`phase-native-moving-first-real-os-receipt.json` の実Win32 Dキー0.65秒指定により、Heroは `(2.94999814,4.540625)`→`(5.549996,4.540625)` へ移動し、キー解放・neutral復帰を確認する。frame開始windowは3秒、完了は3.233478秒。32owner・counter2・nav25・static287・camera `(9,5)`／ortho8・64frame／8fpsの水を保持し、水frame27→31→35→39→43は自然に進行した。Actor／Body／command／InputやCameraのposeをhelperで書かず、renderer／owner状態も変えていない。

moving frame01のBody.x3.84999728とrenderer transform.x3.80962634は別の実測値として保存し、描画支持は実際のrenderer値に対応させる。同値化・epsilonによる丸め・差の原因推測を行わない。

5枚の元PNGは証拠の正本とし、lossless APNG `phase-native-moving-first-lossless-apng.png`（3,193,048B／SHA256 `a188048ad0fb98db996115d26ada63169f68062d3a2d54fdbcc8ad1b68521421`）は閲覧用。`phase-native-moving-first-apng-readback.json` は全5frameのdecoded RGBA一致、resize／量子化／filterなしを確認する。duration671／667／663／678／537msは診断Capture開始間隔を丸めたもので、連続real-time動画ではない。Rootはframe00／01／04の実画素を確認した。native GameView1920×1080と診断Capture1536×1024を区別し、camera pan／zoom時のshimmerや厳密2pxgridを検証したとは記載しない。

独立 `style-pilot-independent-native-review-20261004/actual-phase-moving-cleanup-first-independent-audit.json`（31,176B／SHA256 `da4e88d38a9d1e57dc5116dba7d4cf814d61b9dafe90dd5f2cf9800c46c6fb22`）は移動と最終cleanup96照合／失敗0。全5実PNGをoriginal解像度で閲覧し、各frameのHero不透明支持1,688pxを元Source RGBAと照合して不一致0、隣接frame差4,966／5,098／1,585／1,406pxはsemantic waterと両frameの実renderer Hero alpha支持のunion外0を確認した。OS入力・32owner／nav25／static287・source facts・cleanup・APNGも独立に確認した。

`style-pilot-proof-20261004/final-native-cleanup-inspect.json`（170,984B／SHA256 `420fbce9bb956b054d388c2cff76bfd890253bf98767a82a643b020be19967e5`）は最後のread-only Native状態を記録する。`final-native-cleanup-summary.json` は14照合PASS。Ready／manual／focused／Failure空、実Actor Body `(5.549996,4.540625)`／入力neutral0／Overlap名0、32owner Present・対象counter2／他31counter0、nav25／static287とBody/nav対応、4新部品・地表の復元表示／旧contacts2個disabled、水unpaused／64frame／8fpsを確認した。camera `(9,5)`／ortho8／targetTextureなし／live aspect1.77777779と元terrain／waterのmaterial・metadataはmoving後の状態と一致し、Play中の保存やActor／Inputの書込みを行っていない。この最終姿勢のOverlap0を全領域Physicsの合格へ広げない。

Actor Body.xはmoving後と最終読取りで5.549996のまま、renderer transform.xはmoving.after5.549996／後続最終5.54999638を別記録する。継続するPlay中のActor Transform全体を完全一致とせず、差を許容値で消したり原因を推測したりしない。owner／nav／camera／static／terrain／materialの照合とは分ける。

静止表示・木の実所有撤去復元・地表の限定描画・前後表示・固定Cameraの有限移動観測・最終Native復元と各独立監査はPASS。追記後の保全／Knowledge validatorはRootの別closure待ち。地表の硬い境界、周囲の旧樹冠／岸の細かいgrainは残り、美術の最終承認は待ち。14本への展開、全地表置換、camera移動試験には進まない。

確認範囲は代表1ownerに限定した。通常PNGはARGB32／sRGB、raw支持測定は別のARGBFloatとし、RGBを同形式の証拠で比較した。実OSキーによる限定移動と同姿勢の前後描画は別証跡へ残し、未実行の項目をPASSと記載しない。有限の挙動・所有判定のPASSと、美術のレビュー待ちを分ける。

過去のCPU40／41px・UV最小37pxのfalse、旧GPU補足PASS、初回linear/sRGB監査falseと補足を含め、既存証跡の判断は全て凍結する。新候補の支持を旧素材のGPU証明で代用せず、新class／SHA pin／fresh outputを使う。mask膨張・epsilon・閾値緩和・shader置換で差分を消さない。

再現資料はworkspace `style-pilot-docs-20261004/README-reproduction-ja.md`。保存Scene、通常画像、木／地表の描画control、実OSで到達した前後poseと5frameの移動観測を確認できる。完成art／全Physics／汎用生成／本番AI／全14本の承認は未達のまま。実証前の計画にある入力未受領の状態と生成時の未検証fieldは書き換えず、後続結果を独立して記録する。

追記前の本ExecPlanは176,432B／SHA256 `80624caf9863c1c87025119f661d37117033637681479c6186a3dd355279384d`、生成Asset台帳は53,344B／SHA256 `1a971bfcc218426fd980587d40cd30dc5d3cbbde083942c02b792598ecbfc311`。workspace `style-pilot-docs-20261004/before-overhead-study-a.md` と `before-generated-assets.md` にbyte一致で退避した。Rootだけが全文prefixを保全してRepoへ追記し、反映後のKnowledge validatorと保全closureを別記録へ残す。

追記前preflight `final-pilot-preservation-20261004/preflight-preservation-audit-v2.json`（SHA256 `6f47bcb4f5cb5600ba54a4d0019e56894b180e942ceb59f22faab92999a7158a`）は原本1,232件・両文書の全文prefix・旧root SHAとSource5PNGの一致、新しい3試行prefixの24／26／26fileを確認した。Gitは既存210row（modified28／deleted134／untracked48）・staged0を保持する。比較した保護Fileの範囲の結果であり、未選定Fileの過去bytesやunsaved memoryの歴史を保証しない。文書追記後の最終保全とKnowledgeはRootの別closureへ記録予定。

文書反映の `document-append-receipt.json`、`final-pilot-preservation-20261004/final-preservation-audit.json`、`style-pilot-proof-20261004/knowledge-validation-receipt.json` は追記後に記録する予定で、この追記時点では未実行のPASSを主張しない。修正版を含む実際の最終receipt名と結果はRootの最終handoffで示す。


## A画風の代表木1本のラスタ登録（2026-10-04、64px条件で内部pattern安定・FullHD残差あり）

方向承認された `nw_main` の画風・見かけの大きさ・足anchorを保つため、既存pilotを新しい `RasterRegistration-v1-20261004-201454860` へ複製した。非整数の描画scaleで毎回再sampleしていた4部品を、実表示寸法へnearestで一度だけ変換し、実効world scale1／64PPUへ登録する。元pilot・他31owner・元Prefab links・Body/nav・地表・Runtime4・共通Camera／ProjectSettingsはNative保存readbackで保持した。全14本への展開やPrefabへのApplyは行わない。

### 寸法・所有・物理の判断

| 部品 | 派生canvas px | pivot px（左下原点） | 足からのoffset px | tint alpha |
| --- | --- | --- | --- | ---: |
| canopy | 223×223 | `(111,111)` | `(0,120)` | 1 |
| trunk | 65×86 | `(32,43)` | `(0,42)` | 1 |
| owned shadow | 118×40 | `(59,20)` | `(1,-2)` | 0.36 |
| contact vegetation | 109×41 | `(54,20)` | `(0,-6)` | 1 |

全Spriteは64PPU／Point／sRGB／非圧縮RGBA32／Mipmapなし／FullRect／custom pivot／physics outline生成なし。Pillow12.2.0 `Image.resize(..., Image.Resampling.NEAREST)` の派生で、元source bytes・binary alphaを保持し、作画・再着色・alpha閾値・shadow opacity焼込みを行わない。owner scale `(0.87,0.87,1)`／足world `(2.03125,5.953125)`／OwnedBodyとnavを動かさず、`PresentOwned/RasterRegistrationCarrier` のlocal inverse scale `1/0.87f` とleaf scale1で相殺した。新4部品も既存所有APIの子孫内に置き、canopy／trunkは共通足SortingGroup WorldObjects／0、shadow／vegetationはMapGround／9・10。旧6Rendererは旧transformとSpriteを残してdisabled、新4のみactive。保存reopenと実Playで4pivotが意図した整数、world matrixの線形部が厳密1、全mesh corner×64が厳密整数であることを確認した。

### 実装・通常表示・所有の実測

workspace `raster-registration-runtime-20261004/native-build-first-report.json` はGenerated=true／errors0、Map／入口保存reopenを確認。frozen builderは `StudyARasterRegistrationBuild-sortpoint-frozen.cs`、52,614B／SHA256 `32d0fdd8fd61532d1974ff3ba08509e2c05a66bdce5d8232f26fb9fe7e4f917c`。実Native compile264ms／run5408ms、errors0／obsolete API warning1。生成時の所有／画素／位相verified=falseは当時の記録として残し、後続実測で証明する。

Rootと独立担当が通常1536×1024／FullHD1920×1080の実PNGを確認した。大きな緑の色面、暗いtealの下面、幹と接地を維持する。fresh旧／新比較の通常RGBA差は18,138px、新repeatはbytes／GPU float完全一致。実isolated表示alpha bbox（左上原点half-open）は旧 `[214,237,430,476]`／新 `[214,236,431,476]`、各境界の最大差1pxで、旧RGBA完全一致を主張しない。独立通常照合はcanopyの全不透明28,367px、露出trunk806px／vegetation1,820pxが派生source RGBAと不一致0。blendされるshadowはこの色照合から除く。

`native-owner-first.json` は新4部品のoriginal-material raw float alpha>0 union32,213px、通常実ON/OFF差32,211px／支持外0、復元bytes／float完全一致。実 `SetPresent(false/true)` でnav25→24→25、counter0→2をresetせず保持し、旧6disabled／新4activeへ復元した。独立通常・所有は72照合／失敗0。

### Camera位相の実測と残る制約

Camera `(9,5,-10)`／ortho8の1536×1024は64screen px/unit、1920×1080は67.5screen px/unit。1call1位相の10有限ケースを、同じoriginal material／tint／sortingの孤立old／newで取得し、各finallyでCamera pose・matrices・target／aspect・描画flags・水／coreを厳密復元した。表は同形式のphase PNGにおける最良の整数位置合わせ後のRGBA差で、同一screen位置の生差や連続動画の滑らかさとは別の値である。

| screen px/unit | axis | 指定／実測offset px | 旧の最良位置合わせ後RGBA差 px | 新の最良位置合わせ後RGBA差 px |
| ---: | --- | --- | ---: | ---: |
| 64 | x | 1／1 | 0 | 0 |
| 64 | y | 1／1 | 0 | 0 |
| 64 | x | 0.25／0.25 | 7,737 | 0 |
| 64 | x | 0.5／0.5 | 15,158 | 0 |
| 64 | x | 0.75／0.75 | 7,591 | 0 |
| 64 | y | 0.25／0.25 | 7,636 | 0 |
| 64 | y | 0.5／0.5 | 15,218 | 0 |
| 64 | y | 0.75／0.75 | 7,646 | 0 |
| 67.5 | x | 0.5／0.499985218 | 16,048 | 14,057 |
| 67.5 | x | 1／0.999970436 | 0 | 0 |


64のfractional `.25/.5` は新表示がそのまま、`.75` は全体1px移動として一致し、新の内部pattern／alpha支持／Native float残差は全て0。整数+x／+y1pxは旧も新も残差0で、それだけを改善の証拠にしない。FullHDの指定0.5pxは実float32位相0.499985218px、新の最良位置合わせ後もRGBA14,057px／alpha支持417px／Native float16,546pxの残差が残る。FullHDのfractional phase残差は未解消で、ちらつき解消を保証しない。指定1pxの実位相0.999970436pxで両方一致した結果も、厳密整数位相の保証へ広げない。全10のcapture／復元完了と、画風／全shimmerの承認を分ける。

独立位相監査177照合／失敗0。phase PNGはARGBFloatからの表示RGBA8で同target同士だけを比較し、sRGB通常画像やsource RGBとは混ぜない。phaseのfull RGBAFloatは未exportのためfloat差はNative authority、所有の新4raw f32 alphaは独立再読取りである。共通Cameraの動作は変更しない。地表右境界sourceX432／worldX4.75は元304×512限定cropの人工切断で、tile seamではない。周囲の旧樹冠／岸の細かいgrainも保持する。

### 再現・保全・今回の完了範囲

通常再現は保存済み [新入口](../../../../Assets/Scenes/MapAuthoring/OverheadStudyA/StudyARasterRegistrationNormalPlay-RasterRegistration-v1-20261004-201454860.unity) を開く→Play→Game画面をクリック→全キーを離す→WASD／矢印。配置は既存固定recipeを継承しseed引数なし。Mapは [新Map](../../../../Assets/Scenes/MapAuthoring/OverheadStudyA/IndependentPrefabs/RasterRegistration-v1-20261004-201454860.unity)、元pilotの入口とMapは保存bytesを保持する。

新assetの [raster-bake-manifest.json](../../../../Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/RasterRegistration-v1-20261004-201454860/raster-bake-manifest.json) は入力／派生SHA・寸法・pivot・offsetの正本（SHA256 `867151675e9a4f04be5a8ee1c9fa101619a7ae11d73ac97d05107d2748a49848`）。今回backupは `raster-registration-preservation-20261004/before-raster-registration.json`（SHA256 `914c51edc88db8592cf0b47dad3932136c5bdbcf6c0e579903209fddffbd2783`）と `restorable-before-raster-registration.zip`（1,315entry／37,825,457B、SHA256 `b8ff188953d3e481df11f27f54ac6209d8cf8d4c716398a25fc4460cc400e497`）。現文書193,923B／台帳61,792Bの全文prefixを保護して追記する。再生成時は現source／入力のfresh backupを作り、凍結builderを未使用の別helperへcopyして `ApprovedBackupSHA` と固定件数1,315の照合をそのbackupの実SHA／実件数へ更新する。source／bake pinと各保全条件は維持し、新helperをレビュー→SHA freeze→compileしてfresh root／Scene／reportへ実行する。引数のmanifest／SHAだけを差し替えて旧builderを再Runしたり、guardを緩めて通したりしない。

workspace `raster-registration-proof-20261004/actual-ten-phase-table.json`（SHA256 `60015f1737dd07b147bbc8bdfeb9817079b1ae96f65f6a4c543631b3d49d5e4b`）、`raster-registration-independent-20261004/actual-native-baseline-owner-independent-audit.json`／`actual-native-ten-camera-phases-independent-audit.json`、`root-final-native-cleanup-observation.json` が実測の根拠。Root最終cleanup13照合PASSは新入口の初期Hero Body `(.65,4.640625)`／入力neutral／Overlap0、32owner Present・対象counter2／他0、nav25／static287、水unpaused／64frame／8fps、Camera9／5／8・live aspect1.77777779／targetなし、old6OFF／new4ONを確認する。旧Playは新入口を実行するため自然に終了し、旧counterをreset／保存していない。旧pilot／fullsceneの保全は保存source bytesの範囲であり、終了済みPlay memoryの同一性とはしない。 後続Editor frameのHero renderer transform X0.6499999は、Body X0.65／先行frame renderer X0.65と別記録し、原因や全transform完全一致を主張しない。nav0／Body0の非衝突owner10個は未登録が正常で、全32Presentとnav25を区別する。 最終限定独立freezeは6監査・369照合PASS（workspace `raster-registration-independent-20261004/finite-raster-registration-independent-freeze.json`、SHA256 `4723c6fe91bc1bda700483a4a79bfb7dd86c6230e11e87b2b9e598e8c2e21b7c`）。初回cleanup16/18FAILを保持し、型を区別した22照合PASSを別補足へ保存する。

代表木1本の登録・有限位相・所有の検証まで。描いたgrainの完全一致、厳密な共通2×2grid、美術の最終承認、連続pan／zoomの知覚評価、全14本／全地形／汎用mapgeneratorの完了は主張しない。 文書反映後のKnowledge validatorと最終source／Git保全はRootの別closure待ちであり、この追記時点で未実行PASSを予告しない。

## 2026-10-04: 疎な3本のStyle Family試作

`IsolatedStyleFamily-v2-20261004-215942621`を別Sceneに生成。承認方向の冠に横広・縦長を加え、草/土と岸RGBを静かな色面へ揃えた。12×10の固定recipeで、既存dense map/14本への展開ではない。

実Native正常1536×1024/ortho8は64px/unit。offline見本と水mask外RGBA差0、3冠の可視78,505画素もsourceと差0。幹/根・所有草/影の接地を実表示で確認。新2冠の明るい分割と地面の斑は残り、最終art承認は保留。

art scale1/中央pivotの半pixel offsetでmesh境界を整数登録。旧anchor比較は冠/影/草の差0、幹は契約通り1px左。物理carrier `.87`、local Box `.66×.36`/offset `(0,.12)`、保存Nav値を保持。translated body/NavはYmin約1.59e-7、width約4.77e-7の差を記録し、全Bounds一致やepsilon補正を主張しない。

個別木/岸小物とfamily_allのOFFは対象外画素差0、復元差0。木/岸小物の一時移動はrenderer/Nav追従と完全復元を確認。橋初回は対象外43画素FAILを保持。撮影直前SortingGroup更新を追加したv4の橋再検査は対象外/復元差0。自然水更新は1,748画素変化、対象外0。岸alpha、水64frames/mask、橋、static287、既存Heroとcamera policyを保持。

実WASDで既存Heroを歩かせ、3木と岩/葦の別足元proxyについて手前・奥の計10方向を確認。色を区別できる不透明な実Hero重なりを使い、手前はproxy OFF差0、奥は全witness画素が変化し、各検査の状態復元も完全一致した。最初の葦検査は橋で進めず重なり0のFAILを保存し、橋の西側を回る実歩行で再確認。upright near-firstの名前は実位置が奥なので奥の証跡として扱う。

FullHD67.5px/unitの既知aliasingを継承。producer prompt原文は未同梱、SHA256SUMS自己行はstaleのまま保存。証跡はworkspaceの`family-style-proof-20261004/`、再現案は`family-style-handoff-20261004/README-family-style-ja.md`。


## 2026-10-04: 同画素材の11本配置比較
固定layoutで既存familyを別Scene `DenseStyleFamily-v1-20261004-233100503` に複製。木11本と岸小物の足配置だけを変更し、新Texture/core Runtime/production generatorは追加していない。
通常入口は `Assets/Scenes/MapAuthoring/OverheadStudyA/StudyADenseStyleFamilyNormalPlay-DenseStyleFamily-v1-20261004-233100503.unity`。Native保存readbackはowner13/owned Nav15/static287/renderer54、元family・失敗試行・橋/水/物理/Camera方針を保持。
Rootは通常1536×1024/ortho8の実PNGを閲覧。実OSのDキーでHeroを(.65,4.640625)→(11.5500069,4.640625)へ渡らせ、Body/commandを直接書いていない。
NE手前は不透明witness71px/描画OFF差0、奥1645pxはすべて変化。NW手前の追加確認は8px/差0、奥1450pxはすべて変化し、各検査は完全復元。初回NW手前1pxの結果も保存。
実木Bodyの衝突によるHeroのX1.65→1.7676の押出しを観測。最終Heroは(1.76760817,4.427832)、実キー解放/入力neutral。
岸/橋のOFF→ONと代表木の一時移動/復元はPASS。自然水更新1578px変化、対象外0はPASS。
代表木/木11本集合の即時画素復元は双方115px差でFAIL。owner状態/Navは一致/counter正しく+2でも、足Y6.09375の不透明冠で再登録に伴う可視側の切替えを確認。
集合試験開始時ONは代表復元後と同じ側、集合復元後/後の通常Captureは逆側であり、自然フレームだけによる回復とは認定しない。即時/決定論的な復元engine gateは未合格のまま、独立比較用として操作可能。
次候補はnw_front_bankの足(282,250)→(282,249)だけでtieを除く案だが未実装。承認済み現layoutを維持し、素材/sort/cameraを修正していない。
冠の水/区画端への張り出しは許可、幹/影/接地草とBody/Nav全体は陸地内条件。64px通常と既知FullHD67.5px aliasingを分け、最終art承認/全gate PASS/汎用生成完成を主張しない。
再現メモはworkspace `dense-family-handoff-20261004/README-dense-reproduction-ja-v3.txt`。既存FAIL/旧版と全証跡を保持する。

実保存SceneのNav/Bodyと水mask・固定橋/287障害物・通路を別読取りで補完検査し104/104条件を確認。次1px案の安全性は読取り予測だけで、Native採用結果ではない。


## 2026-10-05: 同一Yの描画順を永続IDで固定する独立試作

`StableDepthTie-v1-20261005-002507369` の新Mapは旧Dense Mapと978,156Bの全バイト一致。新Entry `Assets/Scenes/MapAuthoring/OverheadStudyA/StudyAStableDepthTieNormalPlay-StableDepthTie-v1-20261005-002507369.unity` にだけ既定OFFの `StudyAIndependentStableDepthSort` を明示ONで追加。11本の位置/素材、岸/水/橋、Body/Nav、camera policyと旧Sceneを維持する。保存readbackはowner13/owned Nav15/static287/renderer54、Map著者時SG15/order0、実設定は元317pin＋新規6pin。

現在足元Y降順/TieOrder昇順/永続キーordinal順でMap15＋既存Heroの16順位を決定。全TieOrder0、所有物 `owner:<OwnerId>/<PartPath>` /Hero `actor:hero`。同一Yの2木の位置を変えず、InstanceID/登録順/epsilonに依存しない。実GameplayCameraのURP/camera描画前フックを使用。新Runtime21,027BのSHA256は `71d534aca3db593c8c03de33e1644d896e9bd6bbc7ce899746f840b966853f29`。

実Nativeの両順反復・個別OFF/ON・同時OFF/両復元順は32段階、14回すべて全画面復元RGBA8/live float差0、fresh GPU alpha支持外差0、状態/Nav/16順位一致。所有counter各+16、resetなし、helperはCoordinator.RefreshNowを呼ばない。代表木/全11本/岸/橋OFF差28,049/268,787/4,388/34,881px、支持外/復元差はいずれもRGBA8/live floatで0。bank移動 `(0.5,-0.25,0)` は所有renderer/Nav追従と厳密復元。自然水更新1,543pxは支持外0、frame/elapsed直接書換えなし。

Rootの実キー歩行を使ったHero前後4場面は、NE手前78witness/変化0、奥1,683/全変化、NW手前1/変化0、奥1,450/全変化で足元キーと一致し、各検査の状態を厳密復元。補助処理のactor/body/command/input書換えなし。NW手前は1画素のみで、全方向や全木への保証へ広げない。

2回の自然再読込B/CでRoute[0] `(0.65,4.640625)` のBody/実Transformのfloat bits厳密一致と自然水frame0を実取得。各capture/同一shot復元PASS。actor/frame index/elapsed/counter直接書換えなし。同期撮影中の水Pausedだけ一時保持し直後復元。独立CPU `compact-natural-reload-b-c-exact-first.json` は9項目PASS：全RGBA8/float画素差0、float channel bits差0、撮影source pose/16永続キーと順位/camera/config/現在323pin厳密一致、別Hero/Coordinator instance、全Present/Nav15。PNG各1,102,759B、float各25,165,824BもB/C間一致。floatは同ARGB32 targetのRGBAFloat readbackで未量子化ARGBFloat出力ではない。RootはCの実PNG1536×1024を閲覧した。

根拠はworkspace `tie-sort-proof-20261005/` の `native-tie-sequences-inactive-corrected-first.json`、代表/全木/岸/橋/移動/水の各実結果、`native-ne-near-key-corrected-first.json` /NEfar/NWnear/NWfar、`native-epoch-b-phase0-first.json` /C。旧Dense115画素FAILと元画像/分類/支持は保持。今回の非active親チェッカー漏れ、描画前キャッシュ照合、全木/NE手前の誤引数、epochA補間Transform1ULPの各失敗も保持。epochAは実capture未完了で、許容誤差による救済はしない。

最終Native監査 `root-final-native-normal-audit-first-call.json` はPASS：新Cの元spawn/input neutral、13Present/Nav15/static287、Hero込み55 renderer復元、水64frames/8fps/unpaused、URP、16順位0〜15、ロードした2 Scene dirty=false。再現は新Entry→Play→Game View focus→全キー解放→WASD/矢印。固定recipeでseed指定なし、通常比較1536×1024/ortho8/64px/unit。FullHD67.5px/unitの既知fractional resampling、最終art承認、汎用生成完成の扱いは維持する。文書追記後Knowledge/source保全はRootの別closure receiptで確定する。

事前backupはworkspace `tie-sort-preservation-20261005/before-tie-sort.json`（1,413ソース/455旧証跡）とCRC/SHA読戻し済みZIP39,396,931B。新規Repo範囲はRuntime/meta＋新Map/Entry/metaの6ファイルのみ。既存ExecPlan全207,847B prefixと旧family/Scene/Runtime/Input/Settings/Git210行/staged0を保護し、Root確認済みexact末尾追記のみ許可。再現詳細はworkspace `tie-sort-handoff-20261005/README-reproduction-ja-v2.md`。


## 2026-10-05: 承認Dense Aと新Style Bを接続する独立Pair計画

ユーザーが評価した11本配置のAを基準に、新しい6×10の東側Bを接続する。承認済みAの素材・配置・ObjectId・足元PartPath/TieOrderは維持し、新しいPair Scene/Entryへ限定する。A基準は `StableDepthTie-v1-20261005-002507369`。供給Bは木3/草2/岩2の7ownerと新地面だけで、旧fine Bの素材をそのまま使わない。

根拠はworkspace `dense-pair-input-20261005/extracted/Contracts/integration_contract.json` と `east_family_manifest.json`。Rootレビューはoffline画像/配置の承認であり、供給記録の `engine_verified=false` と実Native検証を区別する。宣言上の期待件数は20owner/20 Map足元group/18owned Nav/287static Nav/71 Map Renderer。既存Heroを別に1登録し、共通sort managerの期待登録数は21。保存/Playの実件数を確認するまでPASSとしない。

Aの原点(0,0)、Bの原点(12,0)、全体18×10、接続足元(12,4.640625)。B地面384×640/64PPU/scale1/Point/no mipmaps、中心local(3,5)。B左端1列は現行A右端767列の全640行RGBAに一致させる。端のSHA256は `88c7d0156f6c2cc00f12c668b5026cbc1c216e2ae1498ffb8eea85bc0098bf1e`。旧 `e2bfd` 契約は使わない。横断全身+marginの保護XYWHは `[0.25,4.575625,17.5,0.57]`。

両componentを同じ新Map Sceneに置き、Nav registryと描画順managerを各1個にする。外周4壁を18×10へ限定置換し、X12内部壁を置かない。A水283矩形、橋gap40の既存geometry/BridgeFloorRectを保持し、Bはdry floor/木Nav3/非衝突装飾4。実world足元Y降順/TieOrder昇順/永続キーordinal順でA+B+Heroを共通管理し、所有キー `owner:{ObjectId}/{PartPath}` とHero `actor:hero` を保存する。B固定7IDのAとの重複を拒否し、Pair/Block単位のSortingGroupを追加しない。

実装前に `updated-style-pair-preservation-20261005/` へ復元可能backupを作り、既存ソース/未コミット差分/旧証跡を固定する。進捗は入力確認済み、Native実装/実画像/両方向横断/岸水停止/橋ON-OFF/代表B所有物の除去・移動・復元/前後・tie/自然固定位相の再読込は未完了。実結果は `updated-style-pair-proof-20261005/`、再現手順はworkspace `updated-style-pair-handoff-20261005/` へ保存して後続追記する。

対象は固定Pair比較で、seed framework/汎用generator/全biomeへの展開ではない。既存Sceneと共通camera policyを保持し、FullHD67.5px/unitの既知aliasingと画像upload helperの制約は継承する。今回その再試行や外部公開は行わず、HomePCの実PNGで確認する。


## 2026-10-05: 承認Dense Aと更新Style Bの独立component Pair

評価された11本配置のAへ新しい6×10のBを接続する `UpdatedDensePair-v1-20261005-045154578` を生成した。旧StableDepthTie/旧Dense/旧PairとAの素材・位置・ObjectId・PartPath/TieOrderを保持し、旧fine Bをそのまま再使用していない。新Runtime追加0、既存stable Runtime/SHA `71d534aca3db593c8c03de33e1644d896e9bd6bbc7ce899746f840b966853f29` を再使用。新Repoファイルは40。

`dense-pair-runtime-20261005/native-build-first-report.json` はgenerated=true/errors0/保存readback済み。新Mapは `Assets/Scenes/MapAuthoring/OverheadStudyA/IndependentPrefabs/UpdatedDensePair-v1-20261005-045154578.unity`、入口は `Assets/Scenes/MapAuthoring/OverheadStudyA/StudyAUpdatedDensePairNormalPlay-UpdatedDensePair-v1-20261005-045154578.unity`。同名asset root配下にAComposition/BComposition/PairCompositionとB個別7owner/地面Prefabを保存した。`pair-geometry-recipe.json` は23,827B/SHA `dac24cd470279640b3a189a7d88356b59fd0263030ee42afff3bb07dd328a3b4`。workspace frozen builderは `dense-pair-runtime-20261005/StudyAUpdatedDensePairBuild-frozen-v1.cs`（62,459B/SHA `7cea18672028778479b6a76d7289385e98a2ed008ffb9acbea9bb5ab978c5569`）。freeze JSONにentry/3args、実Root requestにsource SHA/出力を固定し、`python -X utf8 run-raster-native.py <request>` でRootだけが実行した。保存Pairは毎Playで再現する。再生成はfresh復元backup/current pins・入力再レビュー、停止Editor/clean Scene、別request/report/config/unique rootを使用し、旧request/出力/旧before pinsの無変更再Runはしない。

保存実測は20owner（A13+B7）/20 Map足group/71 Map Renderer/18owned Nav/287static、Nav registry1。A原点(0,0)/12×10、B原点(12,0)/6×10、同一Map Sceneのunion18×10。外周4壁を新Pairだけで置換、X12内部壁0、A水283矩形/橋gap40 geometry/BridgeFloorRectを保持。Bは木3/草2/岩2、Navは木3だけ。art scale1と既存physical carrier .87を分けた。portは(12,4.640625)、横断全身+margin XYWH `[0.25,4.575625,17.5,0.57]`。B ground384×640/64PPU/Point/no mipmapsの左端640行は現A右端767列とRGBA一致、端SHA `88c7d0156f6c2cc00f12c668b5026cbc1c216e2ae1498ffb8eea85bc0098bf1e`。旧e2bfd端は使用しない。供給側のB修正x0..31申告は、修正前B未同梱につき消費側独立検証未成立。

1個のsort managerがA15+B5+既存Heroの21順位を、現在実world足元Y降順/TieOrder昇順/永続キーordinal順で扱う。全TieOrder0、A ID/PartPath保持、B固定7IDを保存、木/岩pathは `PresentOwned/VisualsFoot`、草はMapGround。キー `owner:{ObjectId}/{PartPath}` /Hero `actor:hero`。Pair/Block親SGなし、InstanceID/登録順/epsilonに依存しない。authoring時metadataをNative実行済みへ読み替えず、Pair Map全bytesが旧Aと同じとは主張しない。

Rootと文書担当は実1536×1024/ortho8/64px-unitの `native-normal-pair-first.png` を閲覧し、Bの広場/道/同family小物の接続を確認。実camera center(9,5)、live aspect1.77777779とcapture1.5を区別する。A登録cropは初回自然水49対0で1,492px差/水mask外0を記録。後続 `cpu-accepted-a-same-natural-phase-first.json` は両自然phase0でA world `[0,0,12,10]` 地面rect内の全PNG/raw bits差0、mask/epsilonなし。区画外の冠張り出し/global worldは有限crop外。

実B代表木/草/east_northeast_rockのOFF差は31,717/731/2,901px、橋34,564px、linked Aのnw_front_bank追加回帰28,049px。各original GPU alpha支持外0/全PNG・liveFloat復元0、代表各counter+2/resetなし、木Nav18→17→18/装飾18維持/橋18→54→18。B木移動(0.5,-0.25,0)はowned art/Nav追従と厳密復元PASS。A2木+B南木の足Y0.453125 tieは24段階、8復元の全PNG/raw bits差0、各counter+8、21順位/状態/Nav一致をNativeとCPUで確認。中間liveFloat支持境界はNative authority、復元rawと中間PNGは独立照合。helperのRefreshNow/Hero pose/body/input/water index/elapsed/counter直接書換えなし。旧Dense115px FAILと全初回失敗は保持。

自然水初回は背景停止で変化0の実完了FAILを保持。水/reload中だけ一時 `Application.runInBackground=true` とした後続は約1.333秒で1,624px変化/実水GPU alpha支持外0/静的契約保持、CPUもPNGを再照合。liveFloatはNative authority。`updated-style-pair-runtime-20261005/root-final-background-restore-first-call.json` でfalseへ復元しsource保存なし。自然な別Play epoch A/BでRoute(.65,4.640625)のBody/Transform bitsと水phase0を実取得、同shot復元PASS。全PNG各1,549,128B/SHA `389bad6abace2e9a2af151cb415ebf0e2e6127446df4308c06c5f598cee8564e`、ARGB32のRGBAFloat読戻し各25,165,824B/SHA `2fb1c1d75d4a82a110db9b22e5adc2c33cf10d4ee90e3876f8a9ba8cbeec97e1`。独立reload9条件PASS、全画面差0、21key/順位/pose/camera/config/370current pins一致、別Hero/manager instance、全20Present/Nav18。counter/位相を直接揃えない。

Native Capsule/Nav probeは橋ON/OFF/復元の各1,089足位置と両方向castでNav18/54/18、routeClear true/false/trueを実確認し、実OS歩行とは分ける。Physics Bounds/Nav対応だけ2e-5の既知float誤差幅を使用、source rect/順位/画像はepsilonなし。実OS初回は開始command非neutral/後続foreground Chromeで不採用。次strict試行はunfocusedでkey前に停止。旧RECTのRoot focus試行は矩形guardで停止し、`root-focus-pair-once-01-focus-receipt.json` はosEvents空/focus・cursor・mouse・key前FAILを保持。後続readonly `updated-style-pair-runtime-20261005/root-final-target-rect-readonly.json` が現RECT[80,80,3956,2132]（旧[80,80,1372,764]）を確認。原因不明/ユーザー原因推定なし、現在windowを旧位置・寸法へ戻さない。RECT guardだけ合わせた新helper（9,528B/SHA `d61bb5f3c5d45231957a4b35fdabb783f6fc22a531444782145345b2bee9fe8b`）の1回focusは `pair-input-diagnosis-20261005/root-focus-current-rect-01-focus-receipt.json` で成功、HTCAPTION2のcaptionクリック/keyboard送信なし/mouseup完了/cursor厳密復元/noMoveResize。続く `motion-controlled-forward-02-before.json` はunfocused/InputEnabled=falseをstrict guardがkey送信前に検出し停止、元Route/command0/velocity0/keys0/travel0。親の対話操作STOPを適用し、以後focus再取得/OS操作を行わない。制御された実A↔B横断/B Hero前後/岸水・橋OFF歩行停止はBLOCKED。将来の再開にはHomePCを5〜10分idle、Unity Game View foreground、他appのmouse/keyboard操作なしの時間が必要で、今回のSTOP中は再試行しない。全gate完了としない。

事前保全は1,449source/814旧証跡、CRC/SHA読戻し済みZIP39,492,823B/SHA `81b996c65a935efa0d340d6dd7ae58f6074048a322015e4a1defd07f2788c7e5`、Git210 grouped rows/staged0を固定。既存ExecPlan全212,125B/SHA `4cfa4f31f72f5161146dbd9865ad3d85a43256662bb9762861d31eff1ab919fa` prefixを保護し、Rootレビュー済み末尾だけを追記する。Root最終read-only `root-final-neutral-audit-v2-first-call.json` はPASS：全20Present/Nav18/registry1/manager1・21unit、元Hero Route/keys・command・velocity0、水64frames/8fps/unpaused/background=false、NWbank counter2は正当な回帰分/他0、ロードした2Scene dirty=false。最終Native inspectionもgeometry20/20/21/18/287/水283/noX12wall/Body-Nav対応/370source pins/derived ranksを確認。本追記作成時点ではRepo doc append/after source・Git保全/Knowledge validatorはPENDINGで、後続closure receiptで確定する。詳細はworkspace `updated-style-pair-handoff-20261005/README-reproduction-ja-v4.md` と `updated-style-pair-proof-20261005/`。固定2component比較までで、汎用generator/全biome/新seed frameworkの完成ではない。camera policy、FullHD67.5px-unitの既知aliasing、画像upload helper制約を継承し、今回upload再試行/外部公開は行っていない。


### 更新Pairの非入力closure

最終Native読取りは `root-final-neutral-audit-v2-first-call.json` と `native-final-inspection-first-call.json` でPASS。全20所有物/橋ON、owned Nav18、static287/水283、共通manager1/21順位、Heroは保存Route(.65,4.640625)でキー・指令・速度0。水64frame/8fps/unpaused、runInBackground=false、両Sceneは未保存変更なし。NWのcounter2は追加回帰による正当な増分で、リセットしていない。

文書追記後の初回 `updated-style-pair-preservation-20261005/after-updated-pair-v1.json` はPASS。旧1,449 sourceのうち1,448全文を保持し、ExecPlanだけは旧212,125B全文とレビュー済み10,832B末尾を照合。新40ファイル、814旧証跡、30個の既存metadata、Runtime/input/27 ProjectSettings、旧115px FAIL、事前ZIPを保持。Git210行/staged0、未承認変更0。Knowledge検証も67 MarkdownでPASS。このclosure追記も含む集約末尾を別authorizationで固定し、最終保全・Knowledge receiptを再取得する。

親の指示に従い対話入力検証は停止済み。実A↔B往復、BでのHero手前/奥、岸と橋OFFでの実歩行停止は未認定。将来の5〜10分idle/Unity Game View前面の時間枠まで再試行しない。最新版の引き継ぎはworkspace `updated-style-pair-handoff-20261005/README-reproduction-ja-final.md` と最終JSONへまとめる。全自動生成/全ゲーム完成とはしていない。


## 2026-10-06: 川ロケーションの制作コピー（静的構造）

目的は承認済みの川表現を、橋・両岸・小道を含む A のロケーションへ組み込むこと。ユーザーの継続指示に従い、画面・キー・フォーカス操作や Play、不具合調査は行わず、ファイル側の制作を進めた。

成功版 `UpdatedDensePair-v1-20261005-045154578/AComposition.prefab` を上書きせず、`Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/RiverLocationWorkCopy-v1-20261006/Prefabs/location-river-workcopy-preview-debug.prefab` に独立した制作コピーを保存した。A の 768×640 / 64 PPU / 12×10、地面・小道、橋の床と前後部品、樹木11・岸の岩と葦の配置を維持した。元の13 owner に加え、左右の岸と水面に表示専用 owner を3件追加した。水面の所有枝から静的水衝突・橋 gap を分離し、既存の衝突や Navigation を変更していない。追加 owner の Navigation は空。

静的照合と独立レビューは、元の470 world transform、54 SpriteRenderer、338 BoxCollider、13 owner・水アニメーション・SortingGroup・Rigidbody の保持を確認した。16 owner の ID は一意で、追加した PresentObjects は各 owner の子だけを参照する。内部参照と外部83 GUIDの存在も確認した。実 Unity import・表示・Play の確認は行っていない。A component は単独の Navigation registry / depth manager を含まないため、このコピーを単独の gameplay 完成版としない。

新しい全域の絵は未制作。旧水面64 frame / 8 fps は比較用として残し、泡の `RiverFoamArtPending_DISABLED` は inactive・Transform のみとした。BankPilot を拡大・反復して全域へ貼っていない。必要な絵は exact A wet mask に合う 768×640 の静的 clean water と、左右別の透明な局所泡6姿勢。泡なし水面へ深青の中央・広い cyan の浅瀬・静的水中石を描き、岸・橋・露出岩・植物・白泡は別素材とする。位相・表示時間は素材 manifest で明示してレビューする。

制作コピーの `workcopy-contract.json` と `art-request.json` が構造と不足素材の契約。workspace `location-river-workcopy-20261006/art-handoff/` に、既存 A の原PNG・wet mask・橋3層・承認川見本を無変更コピーし、作画用の位置ガイドと source hash を保存した。表示の承認や新しい絵の完成を意味しない。

変更前に current source 6,803ファイル、既存 Git status / staged / unstaged 差分を workspace の復元ZIPへ保存し、CRCと全 source SHA を読戻した。新規 Repo 保存は制作コピー8ファイルのみで、既存 source は保存前後とも保持。文書は本節の末尾追記だけ。次工程は不足する全域水面・泡の作画、登録位置での静的合成レビュー、承認後の素材差替えである。


## 2026-10-06: A の川ロケーション素材 v1 の中間組込み（ファイル作業のみ）

外部制作の `FantasyRoyale_A_River_Location_Assets_v1_20261006.zip` を正式 Library 経路で HomePC に受領した。Library ID は `libfile_39b5e9470a2c8191ac83a12a387edeef`、12,644,861 bytes、SHA-256 は `1d3246b315b028a5b8697e17506182a145e3a6a8b94be34c2515e7470d7f7893`。実ファイル・読取り・拡張属性の Library ID/版を確認し、ZIP 77ファイルを安全に展開した。13 PNG（clean water1、左右 foam各6）はすべて768×640 RGBA。実ピクセル、全素材 hash、現在 A の wet mask との water alpha 完全一致、wet mask 外への描画0、再利用9素材の無変更、6姿勢×200ms＝1.2秒のループを独立確認した。

前段の構造コピーを保持し、新規 `RiverLocationArtwork-v1-20261006/Prefabs/river-location-art-preview-debug.prefab` に組み込んだ。水面は order5、左右 foam は order7、岸は既存 order8。旧 WaterHighlights GameObject は inactive。各泡 owner の PresentObjects 子に SpriteRenderer と StudyAIndependentWaterAnimation を同居させ、別々の6 frame / 5 fps の配列を参照させた。64 PPU、scale1、Point、Clamp、RGBA32 非圧縮を素材 meta に固定した。元16 owner と新規左右 foam2 ownerで計18。Navigation/当たり判定/bridge gap/既存の木11と shore_rock_reeds の配置は保持した。

473個の元 world位置、元 renderer53個（水面以外）、338個の BoxCollider、既存 state/animation/SortingGroup/Rigidbody の記録を保持し、参照と import 設定を静的・独立レビューした。新規候補は単体の Navigation registry/depth host を持つプレイ可能シーンではない。シーン差替え、runtime code変更、Unity API、画面/キー/フォーカス操作、Play は行っていない。

workspace `location-river-integration-20261006/static-source-compositions/` の full-a-all-on-phase-00/03.png は、現在 A の木・橋・岸の岩を含む55層のネイティブ素材合成。元 SortingGroup15個について既存の足元比較規則に沿うオフライン計画順を使い、拡大縮小0で作った。Unity/GPU 描画の検証とは区別する。水面/泡/岸を外した差分も記録した。全体 PNG の姿勢00は Library `libfile_912cbbed38d48191a38c83d0bbf23eb0` に保存した。

現状は完成ではなく WATER_AND_SHORE_FOAM_INTERMEDIATE。実画像では深い青の中央、シアンの浅瀬、水中石が追加され、現在の道と橋の通路を保っている。一方、泡は承認 BankPilot の幅広い曲線・散在点より細い線状で、浅瀬の境界に規則的なジグザグ帯が見える。水中石は water texture に含まれ、独立した露出岩の追加ではない。親側で川の岩・周囲の局所白泡・根を固定した水草アニメーションを制作中。`AdditionalRiverArtPending_DISABLED` は inactive の空 Transform として追加レイヤー受入れを保持した。新規候補の構造保存は最終絵の承認を意味しない。

変更前の source6,811ファイルと既存 Git status/staged/unstaged差分を workspace preservation/restorable-before-location-artwork.zip にバックアップし、全 source hash/CRC を読戻した。ZIP は402,231,397 bytes、SHA-256 `9fb6e903605de8c1117a43d5eb90eae179e36aefbf5e58a81335c41362b27e78`。旧 A・BankPilot・構造コピー・未コミット変更は保持する。検証の契約は新規 integration-contract.json、保全/受領/独立レビューの詳細は workspace の receipt JSON に記録した。次工程は追加素材の同一登録位置での実ピクセル照合と、この新規候補への追加。


## 2026-10-06: A の川ロケーション v2 候補（岩・局所泡・根付き水草）

外部制作 v2 ZIP を正式 Library 経路で受領し、16,659,257 bytes / SHA-256 `5b9484c2238c074524caae023c08deac9161972415d3ec80be196aa12900ea68`、Library `libfile_72d1727213988191be502c9b0935f288`・版0・実ファイルを確認した。175ファイルを安全展開し、同梱 script は実行していない。宣言59 PNG（表示用53、補助sheet6）の実ピクセルと原本 hash を確認。water alpha は現在 A wet mask と完全一致、RGBと岸泡は v1 から更新。ground/bank/bridge等9再利用PNGは無変更。追加岩4・各局所泡6姿勢・水草4株各3姿勢の足元/pivot/offset、binary alpha、橋 y255..418 の追加不透明画素0、水草根元行63..70固定を独立確認した。

旧版を上書きせず、`Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/RiverLocationArtwork-v2-20261006/Prefabs/river-location-art-preview-debug.prefab` に新規候補を保存した。元18 owner に岩4と水草4を追加して26。岩 owner の PresentOwned 内で static rock と局所泡を一緒に削除・復帰する。岩 sprite だけを WorldObjects の足元 SortingGroup に入れ、局所泡は同 group の外で MapGround order7、岸は既存order8、水面は静的order5。水草は指定足元pivot(.5,2/72)で位置固定。素材53 PNG は64PPU/scale1/Point/Clamp/RGBA32非圧縮、受取り原本のまま。

岸泡・岩泡は6姿勢×200ms、5fps。水草は3枚の原画を[0,1,2,1]×300msで使い、既存 animator の Frames を[0,0,0,1,1,1,2,2,2,1,1,1]、10fpsで表す。画像や受取り時間指定は変えていない。各 animation は同じ owned 子の SR を参照し、owner OFF→ON に伴う既存 OnEnable の frame0 復帰を使う。新しい Navigation は空、元338 BoxCollider・2 Rigidbody・橋通路/水域を維持した。

独立構造確認27/27 PASS。元477位置・元15 SortingGroup・元18 owner と、変更対象7ブロック以外の1,381記録を保存した。全参照・53 importer・所有階層・描画位置を照合した。新規足元group8で計23となる。将来の既存 depth host は active/inactive を含む全WorldObjectsの登録を要求するため、新規8 unit の明示登録が必要。契約にその参照を記録したが、今回 bare prefab に host を追加しておらず、既存 Entry/scene/runtime code は変更していない。現行 Entry へ入れるだけで実機動作済みとはしない。

workspace `location-river-v2-integration-20261006/static-source-compositions/` に既存木11・橋・岸小物を含む67層の全体合成27PNGを保存。23 group の奥行きは既存の足元規則によるオフライン計画順。拡大縮小0。12時点のうち8画素状態、14通りの部品OFF/復帰、0ms＝1200msを検証した。比較用GIFは固定256色/ディザなしの1.2秒、APNGは原画RGBAの1.2秒ループ。これらは素材合成であり、Unity/GPU表示や自然な流れの承認ではない。

素材の独立QAは登録・マスク・時間についてconsumerManifestContractAssetsValid=true。ただし入力見本の合成順は岩泡を岸の上に描き、manifest指定の泡7<岸8と4〜8画素違う。phase0差は[409,79],[409,80],[409,81],[410,82]。制作側の実順で見本/GIF再現は一致するが、厳密なmanifest順の見本一致2項目FAIL/総合passed=falseを隠さず残した。候補は指定順を守り、原画は修正していない。

ユーザーは「もう少し流れている感」の改善案を希望しており、作画側で検討中。v2は比較候補として保持し、提案だけでアニメを本番差替えしない。最終表現の確定や派生ブロックへの量産は保留。今回は画面占有、Unity API、GUI、focus/key、Play、runtime調査を行っていない。

変更前 source6,856ファイルと未コミットGit status/staged/unstaged差分を preservation/restorable-before-location-artwork-v2.zip に復元可能に保存し、全CRC/hashを読戻した。415,239,789 bytes / SHA-256 `b61c40d01469aee231934790c16d5b063f3884b8006a76af22d47ac73a1cc3d5`。新規候補126ファイルの追加と本節の末尾追記以外は既存 source を保護した。詳細はintegration-contract.jsonとworkspaceの各receipt/独立reviewに記録した。


## 2026-10-06: 選択A v3の受入れ候補と28足元登録（画面テスト前）

ユーザーは10:41:50 UTCに水面案A「Aベースで」を選択。作業継続の指示に基づき、既存v2を保持して独立した受入れ候補を作成した。今回の範囲はファイル接続と実原画によるオフライン合成まで。Unity API・GUI・focus/key・Play・Editor.log読取り・新しいruntime検証基盤の追加は行っていない。画面テスト前の状態報告として扱う。

- 候補Root: `Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/RiverLocationAReceiver-v1-20261006`。
- A: `Prefabs/ACompositionReceiver.prefab`、Pair: `Prefabs/PairCompositionReceiver.prefab`。旧UpdatedDensePairと旧v2は書き換えていない。
- Map: `Assets/Scenes/MapAuthoring/OverheadStudyA/IndependentPrefabs/RiverLocationAReceiver-v1-20261006.unity`。
- Normal entry: `Assets/Scenes/MapAuthoring/OverheadStudyA/StudyARiverLocationAReceiverNormalPlay-v1-20261006.unity`。既存のdiagnostic・actor setup・stable depthの3パスを同じ新Mapへ合わせた。camera/input/route/18×10boundsは元のまま。
- 正式受領: Library `libfile_59804a6e379c8191b8c921e99d83d629`、`FantasyRoyale_A_River_SurfaceA_Production_v3_20261006.zip` 8,967,303 bytes、SHA256 `813da98c6081ba24481460d96b6c55d892856184da36b7c8fe73fdaa2d943295`。Library公式helperでHomePCローカル化、file/version xattrsと実バイトを確認。元A提案Library `libfile_0a1af9e722508191b7157261e5596719`も正式経路で読み、A fullcanvas8枚が元Aとバイト一致することを独立確認した。受領scriptsは実行していない。
- `consumer-replacement-plan.json`を置換の正として、独立owner `river_surface_current_A`をMapGround order6、world(6,5)、pivot(.5,.5)、8枚@6.25FPS、Nav空・SGなしで追加。static WaterBase order5はそのまま。
- 既存 `location_river_foam_west/east` のObjectIdと所有構造はそのままで、新しい固定接触岸泡8枚@6.25FPS/order7に置換。古い岸泡6枚は候補のFramesから外した。
- 岩4組の岩画像・足点・foam所有位置・6枚GUIDは変更せず、foamだけ4.6875FPSへ。植物4組の根元pivot(.5,2/72)・足点・3枚GUIDはそのままでFramesを[0,1,2,1]の4参照@3.125FPSへ。
- 全周回1280ms。岩泡の各holdは640/3ms。既存animatorはOnEnableごと独立位相リセットであり、shared clockを導入したとは主張しない。GIFは10ms単位の都合で岩イベントだけ最大3.34ms丸める。runtimeは4.6875FPS、APNGは有理数の正確な時間を使用。
- 新規render PNGは24枚だけ（A8+岸16）。WaterBase/rocks/rockfoam/plantsの41枚はv2既存GUIDでバイト同一再利用。全64PPU・scale1・Point/Clamp・FullRect・RGBA32。Supplier参考前景225803px holdout・B素材・別3streak案をruntimeassetとして取り込んでいない。
- A27 owners、B元7 owners、Pair Nav.Owners34。旧20refsと旧20Unitsの順序/値は保持、v2追加8足元を `PresentOwned_<OwnerId>/Foot_<OwnerId>` で登録してMap28Units（A23+B5）。runtime Hero追加後29group想定。新surfaceはMapGroundなので足元groupに入れない。古い固定20チェックbuilderでこの候補を再生成しない。
- 実際の木11owner・橋3部品・旧land props・26旧owner配置・23足元SG・Nav/colliderを保持。実原画768×640の全体合成は68layer、24時間点で12画素状態、16種のOFF→復元と0ms/1280ms一致を確認。前景は保存済みPrefab由来でありSupplierpreviewholdoutではない。
- `receiver-contract.json`と`material-bindings-v3.json`が現行接続記録。`material-slots-current-v2.json`は53slotの準備時baselineとして残した。`Provenance/production-package.zip`に正式受領素材とconsumer planを保存。
- 作業前6982filesを完全バックアップ、432,411,029 bytes、SHA256 `8ba27a29736889caa900bd25b0dafab37a23d27297bb2de414c3d8d65e7b0eda`。workspace `river-a-reception-prep-20261006/preservation/restorable-before-location-a-receiver.zip`。旧source/Git dirty・staged差分を保護し、新root+指定2scene/metasのみnew-only保存。この節は元文書を保った追記。

独立受領pixel review、host wiring review、actual candidate/source composition整合と保護receiptはworkspace `river-a-production-v3-20261006`。Libraryの証拠ZIPへまとめる。Unityのimport/GPU表示・Play・Hero depth/owner実bindingは未検証で、ファイル検証やオフライン原画合成を実行済み画面として扱わない。次は候補の状態を親へ報告してから、ユーザーの次の工程指示に従う。


## 2026-10-06: A v3の見た目評価と次の異寸法林道C接続

ユーザーは新Scene案内後11:24:36 UTCに「いい感じ」と評価した（親から伝達された参照 `Sentinel_fd2cab1921ac8191a50ae3c8260896b5`）。選択A v3を今後の見た目基準とする。これは見た目への肯定であり、全runtime試験・Hero奥行き・owner lifecycleの合格へ拡張しない。Unityをユーザーが操作している可能性を尊重し、今回もAPI・画面・キー・focus・Playを操作していない。

現保存済み `RiverLocationAReceiver-v1-20261006` の接続を読取り確認した。川Aは原点(0,0)/12×10、Bは原点(12,0)/6×10、全体18×10。A ground右端767列とB左端0列の640行RGBA一致契約、足点(12,4.640625)、283静的水矩形・4外壁・元橋床、Pair34owners/28map足元Unitsを保持する。実sourceのA68+B17表示layerを1152×640へ等倍合成し、x12の道・草と前景の連続を確認した。BはAより開けた東向きの土道で、原画合成上の透明な隙間や矩形切断は見られない。これはUnity/GPU描画や連続歩行の追加合格ではない。

次に実装する一候補を、**B東側からC西口へ接続する8×6の緩く北へ曲がる林道**に決めた。C原点(18,2)、native512×384px/64PPU/scale1/rotation0。入口foot world(18,4.640625)、C local(0,2.640625)、top-left pixel grid edge(0,215)。Bの既存土道を使える東側から接続し、現在のB北側の森を無根拠に開けない。

新しい絵は **`forest_path_C_ground_clean.png` 1枚** が必要。旧8×8・12×10・6×3のFineSoilは細粒土面で、今のA/B草と土道のclean groundと異なるため引き伸ばして流用しない。木/幹/樹冠/影/接地草/岩は現在の同family画像を等倍で再利用し、立体・接地表示・collider・Nav・animationを同じ独立owner配下に保つ。地面原画へ立体小物・影・木を焼き込まない。Point/Clamp/FullRect/RGBA32/Mipmapなし、pivot(.5,.5)、C local center(4,3)を引き継ぐ。

西側境界は **C_RGBA[0,v] = B_RGBA[383,v+128]（v=0..383）**。B原画 `UpdatedDensePair-v1-20261005-045154578/Sources/east_ground_clean.png` のrows[128,512)が基準。最初32pxは入口向きと既存草/土輪郭を引き継ぎ、新blocking body/owner足点を置かない。32px全体のコピー・mirror・stretchを要求するものではなく、exactは左端1列のみ。B東の実可視土は約94px、C内部/将来北口の可視土は96pxを基準にする。旧3unit整数Cellのport幅へ読み替えない。

入口の広いart予約はC row177..253（foot余裕）、151..237（Hero表示）のinclusive帯。現足点1線のminimum body+marginはworld y4.575625..5.145625 / C row182.68..219.16（外側整数丸め[182,220)）で、操舵可能幅や土の見た目幅の合格ではない。Bの保存済み木3body/Navはこの入口帯と交差せず、草/岩4ownersはbody/Navなし。現x18外壁（center18.5,5 /size1,12、Nav[18,-1,1,12]）は現在閉じている。元protected crossingはx17.75までなので、今回x18の全身通過を試験済みとしない。

C素材受領後の新assemblyだけでeast外壁を組み替え、C接続帯と外周を作る。A/Bの元候補を改造しない。union bounding rectは26×10だが、C上下の欠落領域[18,0,8,2]と[18,8,8,2]はstatic Nav/Physics外周として扱う。矩形FrameWorldBoundsだけを広げてvoidを歩行可能にしない。床collider追加やSprite alphaからのNav生成は行わない。既存一つのNav registryと共有foot depthを引き継ぎ、34owners/28Unitsに実際のC owner/foot分だけ明示追加する。Entryの3Map pathを同じ新Mapへ揃える。

将来の広場へ向ける北口はC edge(384,0)/local(6,6)/world(24,8)、可視土96px/x[336,432)を準備する。今回の選択はC一つだけで、広場はまだ制作・接続しない。northはmetadata/原画の予定口であり、初回C gameplay assemblyでは境界を残し、相手のsouth口が接続されてから開ける。元actor body+marginを上端y8内に保つなら足点y<=7.495である。旧 `StudyABlockPort` は整数Cell/幅であり、現在のfractional64PPU足点を直接castできない。新runtime frameworkを導入せず、このfixtureのworld/pixel契約を次候補の施工仕様にする。

実装順は新clean ground受領と実画素確認→独立C Prefabとfresh A+B+C assembly/Entryをファイル接続→原寸全体・境界画像を報告→次の許可範囲で実Actor/Camera/奥行きを確認。今回C原画、C Prefab、C Sceneを生成済みとは記録しない。

具体契約・B東端の等倍素材資料・現在Pair全体PNGはworkspace `location-next-connection-20261006` に保存し、Libraryへ一つの仕様ZIPで共有する。新フェーズ前7062sourceとGit dirty/stagedを記録、受入れ済み80files+ExecPlan全体+Git差分を復元overlay ZIP53,253,228bytes/SHA256 `21897c328d0d4b38c5ae794389d1b4205e665c97abf712a985c66a775b3fd4b5` に保存した。既存432,411,029bytes full source backupとの復元chainを記録。今回のRepo変更はこのappend-only記録だけで、完成したA v3/B候補を保持する。


## 2026-10-06: 正式C地面を受領し、B東→C西の1接続を別候補へ保存（files-only）

選定済みA v3とBを保護し、8×6の北へ曲がる林道Cを追加した。今回はUnity API・GUI・画面・key/focus・Play・Editor.logを操作/読取せず、新runtime frameworkも作成していない。以下は実Prefab/PNG/Metaのファイル側検証と原寸オフライン合成の結果であり、Unity import/GPU/Actor移動/Camera/runtime depth/lifecycleの合格ではない。

- 正式入力 Library `libfile_cfb8fec4fb308191b6cb863eec5d0df7`、`FantasyRoyale_Forest_Path_C_Ground_v1_20261006.zip`、5,127,609 bytes、SHA256 `447e900c29ad9532186b2c185eb280b8751736a3481824f85537d2ffaf536d11`。現行Library helperでHomePCへ1回で受領し、実bytes・file/version xattrs・全18 ZIP entriesのCRC/readbackを確認。supplier scriptsは実行していない。
- 受領PNG `forest_path_C_ground_clean.png` は512×384、RGBA全opaque、SHA256 `95cde8c91ba7241a61476eb66d8c052e2a52113da2b8cc023047ebcf1d590f88`。原画を無変更で登録。64PPU/scale1/rotation0、Point/Clamp/FullRect/mipmapなし/RGBA32 uncompressed、pivot(.5,.5)、C local center(4,3)。C origin(18,2)、world8×6。B右端rows[128,512)とC左端384行は全RGBA一致。
- 新Root `Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/ForestPathCReception-v1-20261006`。`Prefabs/CCompositionReceiver.prefab` と `Prefabs/ABCCompositionReceiver.prefab`。新Map `Assets/Scenes/MapAuthoring/OverheadStudyA/IndependentPrefabs/ForestPathCReception-v1-20261006.unity`。新Entry `Assets/Scenes/MapAuthoring/OverheadStudyA/StudyAForestPathCReceptionNormalPlay-v1-20261006.unity`。Entryのdiagnostic/actor setup/stable depthの3参照を新Mapへ合わせ、CameraMapBounds frameを26×10へ。既存HumanInput/Camera follow設定と旧routeは保持。
- Cに元のTree4/Rock1/Grass2を同一素材・scale1で配置。7つのpersistent owner IDは新規で、既存A/B34 ownersと衝突しない。19 owned visible layers、5独立foot SG、Tree4のPhysicalCarrier.87・Box/Nav footprintを元templateのまま保持。Grass2はflat MapGround、rockは既存decor ownerでBody/Navなし。地面へ影・木・小物を焼き込まず、元のowned構造を再利用。
- 実土道に合わせて `c_tree_south_broad03` の足だけC grid(296,376)→(320,376)、local(4.625,.125)→(5,.125)、world(23,2.125)へ24px/.375world東移動。ID/scale/内部parts/physics/Nav local/足yは不変。他6ownerの位置も不変。道端の自然な葉overhangは残し、実路上Hero表示と物理通路を別に確認。
- ABCは41 Nav owners、33 map foot Units（既存34 owners/28 Unitsのprefixを保持、C7/5を追加）。runtime Hero追加時の期待は34 Unitsだがruntime未検証。Cの接地影/草/幹/葉は各Tree ownerの同一SG、Cはblock SGで束ねていない。
- 旧東外壁を新assemblyだけから外し、B/C seam x18,y[2,8)を開いた。外壁8、静的Nav293（旧water283のraw bytesは保持、外周8+void2）。右上下void [18,0,8,2] / [18,8,8,2]はNav除外し、物理外周で閉じる。duplicate void floor colliderやground colliderは追加しない。C北y8は閉じたまま、north soil x[336,432)は将来用art contract。Geometry recipe SHA256 `2262fa80c27bb0253bd5ce7f6d573626e6ca6b8d46375f1773afed2cbd9abdbe`。
- 実候補からA/B85+C20 layersをshared33 foot orderingで全ABC1664×640へ原寸合成。C ground/sprite登録/owner位置/foot orderを独立確認。7つの実土道Hero posesは全身1688 opaque pixelsを保持し、前景alpha交差0。C3のowned4 layers OFF→restoreは全ABC RGBA完全一致。地面・見た目の境界を原寸画像で確認した。
- protected7062旧source/Git dirty・staged・unstagedを作業前に固定。full source ZIP432,411,029 bytes、accepted-v3 overlay53,253,228 bytes、直前Plan ZIP121,940 bytesの3段restore chainは全7062 bytesを復元可能と独立検証。指定新Root+新2scenes/metasへnew-onlyで保存し、旧asset書込み0。旧7062 sourceはこのappend以外バイト不変。

再現設定は新Rootの`receiver-contract.json`、`C-placement-proposal.json`、`material-slot-C.json`、`assembly-geometry-recipe.json`と新Entryに固定。workspace `location-c-ground-v1-integration-20261006`に受領記録・独立art/structure/preservation/native-composition reviews・保存receipt・全ABC/C/境界/静的Hero画像を置く。今回の1接続を候補として親へ報告し、別ロケーションへの拡張やUnity画面試験はこの記録の完成範囲に含めない。


## 2026-10-06: 正式D広場をC北へ接続して別名保存（files-only）

既存ABCを保護し、C北の林道からDの開けた広場へ接続する新ABCDを追加した。Unity API・GUI・画面操作・key/focus・Play・Editor.log・runtime logを使わず、新しいruntime frameworkや依存関係も追加していない。以下は実PNG/Prefab/Metaと原寸静止合成からの検証であり、Unity import/compile/GPU/Actor走行/runtime深度/Camera/lifecycleの合格ではない。

- 正式納品はLibrary `libfile_35a6f3b3e8c081918c9d19e6dfcd5508`、`FantasyRoyale_Location_D_Woodland_Clearing_v1_20261006.zip`、15,136,029 bytes、SHA256 `e51e719e462e2af23d807cc1411996b76708366f98332c6ea91d717f4c398bbc`。現行の正式Library受取でローカルbytesとversion0を確認し、全67 entriesのCRC・元brief46 entriesの完全保持を独立確認。供給元scriptは実行していない。
- `woodland_plaza_D_ground_clean.png` は640×512・RGBA全opaque、SHA256 `e4663662521b559cba8295b97f3fc7c9ad19433529f7271aa81116a244b43ebc`。64PPU/scale1/rotation0、Point/Clamp/FullRect/Single/mipmapなし/RGBA32 uncompressed、pivot(.5,.5)、local center(5,4)。D origin(18,8)、world10×8。南512画素=C北512、西下128画素=B東上128は全RGBA一致。入口96pxは絵の幅であり、不可視gateではない。共有草地も既存方式で歩行可能。
- 新Root `Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/WoodlandPlazaDReception-v1-20261006`。`Prefabs/DCompositionReceiver.prefab` と `Prefabs/ABCDCompositionReceiver.prefab`、新Map `Assets/Scenes/MapAuthoring/OverheadStudyA/IndependentPrefabs/WoodlandPlazaDReception-v1-20261006.unity`、新Entry `Assets/Scenes/MapAuthoring/OverheadStudyA/StudyAWoodlandPlazaDReceptionNormalPlay-v1-20261006.unity` を別名保存。Entryのdiagnostic/actor setup/stable depthの3参照を新Mapへ統一、bounds28×16。既存HumanInput・spawn・camera follow実装を維持。
- Dに既存native tree2本を独立ownerで配置。北西anchor `d_tree_northwest_anchor01` はlocal(1,6.25)/world(19,14.25)、北東upright `d_tree_northeast_upright02` はlocal(9,6.25)/world(27,14.25)。旧診断のbroad計測を使わず、正式uprightの実alphaで確認。元4部品/owner（影・接地・幹・樹冠）、原画scale1、PhysicalCarrier .87、Body/Navと2foot SGを保持。groundにSG/Colliderや焼き込み小物はない。
- 旧A/B/Cの3instances・全41 owner登録・旧33 map foot Unitsをprefix保持し、新D2を追加して43 Nav owners・35 map foot Units＋Hero。新ABCDだけ旧C北壁/UpperStepを撤去し、C東壁を6→5へ変更してD外周を接続。旧ABC北閉鎖は不変。新外周10本、Nav296=旧water283 raw byte prefix＋外周10＋void3。frame28×16、void [18,0,10,2]/[26,2,2,6]/[0,10,18,6]。内部共有辺に壁を置かず、floor colliderや重複void slabを作っていない。
- 独立静的構造30/30合格。実owner Nav矩形を含む保守的Actor capsule bbox sweepでC→D→C・B↔D・北接近の代表経路が通り、北外へのbodyが閉鎖されることを静的に確認。実走行や経路探索を実行した合格ではない。
- 実candidate114 layers・35 map foot orderingから全ABCD1792×1024を原寸再合成。旧105 layersは同world位置・旧領域RGBA一致。中央7×4と南進入路に新木のWorldObjects alpha侵入0。8つのactual Hero静止位置は各1688 opaque pixels/前景alpha侵入0。D各木の4owned layers OFFと両OFF、restore全RGBA一致。接地草と半透明影の中央周縁への重なりはMapGroundとして記録し、前景遮蔽と混同しない。
- 変更前7092 source/既存Git dirty・staged・unstagedを現bytesで保存。従来4段の復元chainを保持し、current ABC30＋Planの31-source overlayを追加してCRC/SHA/currentreadback確認。28新規filesだけを保存し、旧sourceはこの末尾append以外不変。復元作業やuser差分の取消しはしていない。

再現設定は新Rootの`receiver-contract.json`、`D-placement-registry.json`、`assembly-geometry-recipe.json`、新Entryに固定。workspace `location-d-ground-v1-integration-20261006` に正式受取、独立art/structure/preservation/native reviews、実source全体・境界・Hero・owned OFF画像、保存receiptを残した。全体静止画像はLibrary `libfile_5db7bac5739c8191a4214c0e934ecfe3`。overviewとActor-followの表示は正式実行許可後に個別確認が必要。現cameraコードのorthographicSize=8は条件付きの既存実装で、Dの新zoom採用や実画面の証拠としては扱わない。


## 2026-10-06 native location 自動構成 v1

### Goal / Decision

ABCD 保存後はロケーション素材追加を止め、現在の方向別4個を適合口から自動構成する最小入口へ進めた。既存生成器は Extent 可変だが、候補選択はなく、旧Builderは固定Case・cell/Tilemap前提だった。native独立Prefabを旧maskへ無理に変換せず、既存生成器へ pure partial入口と、同じEditor assemblyの専用Builderを追加する。[責務と再現設定](../../design-docs/scenes/native-location-assembly.md) を追加した。

認定済み道は A東–B西、B東–C西、C北–D南。B–Dの128px追加草地接触も正式登録するが拡張口にしない。A西–B東やA東–C西は実RGBAが不一致なので候補から除外。回転・反転・拡縮なし、各素材最大1個、2–4個をseed選択し、AB / BC / CD / ABC / BCD / ABCDを生成する。全4個指定の正規化配置は一意であり「random ABCD」と呼ばない。全4個の異なる配置には対応する反対側の地面・アンカー・入口予約契約が不足している。今回は新素材を作らない。

### Implementation

- `StudyABlockGenerator.cs` は partial 1語のみ追加。NativeLocations extension は明示PRNG・順序固定・探索上限4096・unique owner/foot検証・適合口と実共有辺の全面coverage検査・checked pixel origin・正規化ContentHashを持つ。seed、探索順、InstanceIdをhashへ混ぜない。未達個数は小さい結果へ黙って置換せず拒否する。
- `StudyANativeLocationGeometry` は選択床union、1world外周、Frame内voidを整数native座標から再計算。道の土幅を物理ゲートにせず、voidはNavのみ。A選択時だけ固定水283矩形を転用する。
- `StudyANativeLocationSceneBuilder` はJSONから生成し、既存独立Prefabを位置だけ移す。テンプレートを新フォルダへコピーしてMap/Entry・selected owner/foot登録・Nav・外周・spawnを更新する。元テンプレート・旧出力は編集しない。dirty Scene、Prefab Mode、Play/Compile/Import中、既存出力、sourceSHA変更を保存前に拒否。足位置とHero身体AABBも検査する。
- Editor asmdef に既存 `FantasyRoyale.Playtest.Runtime` 参照を1個追加。新外部dependencyなし、内部循環なし。旧Builder/Test/既存metaは変更しない。
- 設定は `Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/NativeLocationAssembly-v1-20261006/native-assembly-config.json`。v1、MinBlocks2、MaxBlocks4、RequestedBlockCount0、既定seed0。seed11=AB、21=BC、25=CD、7=ABC、1=BCD、0=ABCDを実生成で確認した。

### Verification / Evidence

- 変更前7120sourceの現状hash・Git dirty/staged/unstagedと6段の復元ZIPを保護し、独立CRC/hash確認済み。変更直前にsource/Gitを再比較し、既存差分を維持するguard付き保存を使用する。
- 実際のC#4sourceを、インストール済みUnity6000.4.3f1のmanaged math DLLでオフラインコンパイル・実行。seed0–383の384結果は同seedJSON byte一致、6構成すべて出現。forced4の384seedはcanonical hash1種 `9b43d4858afce1d22f48dad11f75add6f8e38892d4165845140dee229fca1d79`。整数独立参照との外周/void配列一致、平行移動/逆順不変性、無効接続・coverage不足・owner/foot不正・overflow拒否、旧API回帰3項目を含む46チェックPASS。
- 新Editor BuilderとNUnit51ケースのsourceを実managed DLLで構文コンパイル成功。NUnitは実行していない。JsonUtilityやScene APIはこの作業で呼び出していない。
- 実C#出力JSONを直接消費し、保存済み原画・Prefab配置のnative source layersだけで6枚を合成。新しいBC/CD/BCDを作り、AB/ABC/ABCDは既存PNG bytes/hashと完全一致。回転・反転・resampling0、選択外owner/foot/layer0。rootはBC、BCD、CD+Heroの実画素を確認し、既存の森林密度、道の接続、広場への縦接続を確認した。
- 4個の整数spawnには実Hero原画を合成。A開始のAB/ABCはX41.6pxの連続座標を記録し、無断round/resamplingで画像へ置き換えない。
- workspace証拠: `location-assembly-generator-v1-20261006/actual-managed-compile-probe-receipt.json`、`compiled/managed-probe/output/actual-representative-subsets.json`、`native-editor-offline-syntax-compile.json`、`native-nunit-offline-syntax-compile.json`、`actual-generated-native-compositions/`。出力・設定・sourceをまとめたLibrary成果物に引き継ぐ。Unity proprietary DLLは成果物へ含めない。

### Remaining

これは実生成器の出力と静的native表示の確認であり、Unity/GPUのSSではない。Unity操作、import、Scene生成メニュー、Physics、人間入力、camera follow、animated phase、実runtime深度、Test Runnerは非占有指示により未実施。Builderが書き出す新NativeMap/NativeEntryも今回まだ生成していない。最初の目標参照画像に対する最終一致判定は保留する。既存ABCD・素材・ユーザー差分を守り、許可された環境確認へ進める状態で引き継ぐ。


## 2026-10-06 E接続原画の受入れと同4種2配置

### 目的

既存 A–B–C–D（S0）と追加 A–B–D–C（E）を、同じ論理4種と所有者で seed から選べるようにする。原画 B_E / D_W の2枚の正式受入れ、実C#生成、native出力画像による2配置確認までを一つの節目とする。原画到着前の受入れ準備だけで完了扱いしない。

### 背景と根拠

v1の全4個配置は一意である。Library設計 `libfile_baf3840beb7881918bc1a368bb9b220f` のE案がユーザーに選ばれた。新B東–D西512pxとB東–C西128pxアダプターを追加し、既存AB640px / CD512pxを保持する。新規地面は2枚だけで、N北分岐案は制作しない。

### 対象範囲

方向別地面のversion付き境界profile、編集マスク、source pinを受入れ契約にする。既存v1へ別partial入口のrecipe/variant解決を加え、各logical A/B/C/Dを1定義へ解決する。Editorの実ファイル検査後に新instanceのgroundだけを差し替え、既存owner/foot/Nav/spawn/cameraの生成処理を再利用する。

### 対象外

旧生成器・旧設定・独立Prefab・原画・固定Map/Entryの置換、propsの移動・回転・反転・拡縮、新dependency、N案、pushや外部公開を行わない。画面操作、Unity起動・API実行・Play・ログ取得は行わない。

### 実装計画

1. 現sourceとGit差分を保護し、復元用ZIP連鎖と全current sourceの復元被覆を確認する。
2. 未受入れEを選択候補から除外するrecipe/variant解決を追加する。明示E指定は実原画の検査が通るまで拒否する。
3. 原画の全opaque RGBA8、native寸法、black-mask外の元RGBA、正規512/128 profile、旧AB/CD、三者角、D南row全640pxを検査する。Unityのimport設定は別のEditor gateで確認する。
4. 実C#で再現性・旧v1回帰・未受入れ拒否を確認する。正式原画到着後に実sourceを使用し、S0/Eの実生成とnative出力画像を確認する。

### 進捗

- 保護完了：7134 source・515952086 bytes。前回V1最終とsource/Git raw差分が一致。7 ZIPのSHA/CRCとcurrent全source復元被覆を確認し、欠落・不一致0。
- 進行中：接続契約と生成器受入れ準備。作業証拠はworkspace `location-assembly-e-acceptance-v2-20261006/`。
- 未着手：新原画2枚の正式受取、画質確認、受入れ後の2配置実生成。仮画像で認定しない。

### 判断記録

元v1のcatalog ID/version・PRNG・ContentHash・subset入口を維持する。v2 recipe選択は別streamを用い、variant/evidenceのhashは別receiptへ記録する。元/新B,Dを全catalogに同時登録せず、recipe選択後に各logical種を1variantへ解決し、OwnerId重複guardを保持する。新instanceの単一ground SpriteRendererだけを明示overrideし、元Prefabは変更しない。

Eの口予約64px（offset−32）と内部の見える道幅96pxは別概念である。土色・alphaを狭い物理gateにしない。基準の境界RGBAは有限長のversion付きprofileとして保管し、元PNG全辺hashを互換keyにしない。

### 検証

原画前は既存実managed C#の実行・拒否条件・元source保護だけを確認する。Eの新地面画質と配置の完成を主張しない。原画到着後もnative静的合成とUnity実表示・Physics・実移動・animated phaseを区別して記録する。

### 完了結果

継続中。正式原画と受入れ後の2配置生成・出力画像検証が未完了である。


## 2026-10-06 E受入れ・実2配置生成のファイル側完了

### 目的

正式素材2枚の受領から同じ4ロケーションのS0/E実生成・原寸画像確認までを一つの節目として完成させる。

### 背景と現状

正式ZIPのLibrary受取が成功し、実B_E/D_Wの画素12/12と114層全景・native collarの独立visual受入れを確認した。元画像を流用するnegative controlとは別の実素材である。

### 対象範囲

版付きrecipe/variant partial、新2ground、mask/profile/source pin/正式受入れ記録、新設定、既存SceneBuilderの最小分岐。原素材・43 owners・35 foot units・114層を保持する。

### 対象外

N案、元Prefab/Map/Entry置換、props移動・回転・反転・拡縮、新dependency、push/公開、Unity操作は行わない。

### 実装計画

保護→受入れgate→実C#2配置→戻り値から原寸source合成→保存前後の差分保護確認を完了順に記録する。

### 進捗

- 正式2PNG受領・pixel12/12・独立受入れ契約35/35を確認。
- 実C# production 14/14、64 Auto seedでS0/Eの2geometry/2hash、旧S0と元subsetを保持。
- Auto代表seed S0=3 / E=0。明示S0/Eは各seed0でも再現可能。
- source-native全景・接続部・353足位置のphase0 Hero検査を実施。実C#戻り値でS0/Eを合成。

### 判断記録

Eのmouth64pxとvisible road96pxは区別する。境界は版付きprofile、元画像保護はmask/source pin、正式受取とvisual gateは受入れreceiptに分離する。新groundはinstance Sprite overrideとして保存し、Prefab sourceは変更しない。

### 検証

実C#とsource静的画像で2配置を検証した。Editor/NUnitはcompileのみ、Unity/Scene SaveReload/Physics/入力/camera/animated phaseは未実行。詳細は既存設計文書 `native-location-assembly.md` とworkspace evidenceを参照する。最初の目標参照2画像への最終見た目判定はHomePC未受領のため保留。

### 引き継ぎ

新設定TextAssetのRecipeId/Seedとreceipt pinsから再現する。Unity操作が許可された別作業では既存menuで新出力先に生成し、import・SaveReload・実Playを別に確認する。今回の停止理由は許可されたfiles-only範囲であり、画面表示を実行したとは報告しない。


## 2026-10-07 S0/E 品質調整 v1 — C西口の終端

承認済み川A v3を品質基準にS0/Eの全体・接続帯を原寸で見直した。Eでは未使用C西口の土道が空白へ出るように見えたため、原D地面の64×128pxを地面の表示層として再利用し、丸い草縁で道を内側に終わらせる。床外へ岩を置く試作は浮いて見えたため不採用。新規PNG、固体小物、Collider、owner、足元単位は追加しない。原4Prefab/地面、川A v3、すべてのowned描画・位置・木のはみ出し、旧S0/v1設定を保持する。

再現設定は `Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/NativeLocationLayouts-v2-20261006/native-layout-quality-config-v1.json`。`RecipeId=Auto`、Seed0=E、Seed3=S0。Unity操作が許可される時にこのTextAssetを選び、既存Generate Configured Native Location Assemblyを実行する。別の試行は別OutputRootに保存する。旧 `native-layout-config.json` は従来表示を保ち、新設定だけが原寸の地面表示層を追加する。自動生成は認定済み2つの固定配置を選ぶ方式で、自由な多方向生成ではない。

表示仕様は `e-c-west-ground-presentation-v1.json`、SHA256 `6a4a22dac37872ff2a6f2db8d5821327212e7571f2b82a921d11d6afc25a72d9`。原D PNGのUnity矩形(0,256,64,128)をC内矩形(0,128,64,128)へ等倍・回転0で置く。PPU64/Point、MapGround/order1、生成Cと同じ材質・色で、owned/WorldObjectsより下に描画する。SourceTextureとmetaのSHA、native寸法、未使用C.W、E接続、限定範囲を生成前に検査する。切出しSprite.assetは新OutputRoot内だけに保存し、共有PNGやimporterを変更しない。表示profileは独立SHAで記録し、GeometryHash/LayoutContentHashは元の意味を保持する。原114表示パーツにhost地面1枚を加え、原43owners/35Map foot units・既存Nav/床/外周壁を保つ。草と土は従来と同じ歩ける床であり、閉じた外周壁が通行の境界となる。

実C#生成器と純粋な表示計画は14/14を通過。Auto Seed0のEとSeed3のS0を実生成して保存source層から原寸構成し、E差分はfull左上(1152,640)-(1216,768)の8192画素だけ、全体alphaと範囲外RGBAは完全一致した。E PNG SHA256 `d40322374cc07d90b6aa159bf6bfef5a5ad2d24676ad9ff6ef9a0efb49a26d05`、S0 PNGは旧 `336a0cf4c3c7614cb119622c8e4da2bafadc814c8813a6127628956091618f3f` と完全一致。接続帯5枚も不変。全体・局所の実出力を目視済み。証拠は作業フォルダ `quality-review-s0-e-20261007/actual-native-ground-quality-verification.json` と `actual-native-quality-plan-compile-probe-receipt.json`。

これは非占有の実C#計算・静的source構成の検証。Unity importer/Scene Builder実行/Sprite.asset保存再読込/実プレイ/カメラ/移動・衝突・runtime深度は未確認。次の許可されたruntime確認では、新設定のE/S0生成・保存再読込、C終端への移動と壁、B-D/C-D通行、Heroと樹冠の前後関係、camera followの外周表示を確認する。今回修正したのはC西口1箇所で、直角の外周・透明void全体の見せ方は未仕上げ。初期目標2画像の未確認とは別に、川A v3と原コンセプトの実ピクセルは2026-10-06正式受取・確認済み。


## 2026-10-07 S0/E 外周表示の仕上げ v1

保存済み C 西口修正に続き、S0 の12辺・E の11辺の露出外周を仕上げた。元の川岸 `bank_west_style.png` から床外の土・岩面だけを BL `(389,384,31,64)` で切り出し、原寸・四半回転で外側へ繰り返す。可視幅6～9pxは既存64px物理壁内にあり、草地の歩行面を追加しない。川出口は A の x352～608pxを南北とも除外し、両岸の32px端部は元のUV位相を保つ部分切り出しとした。浮いた岩や新しい森林で塞がず、元114レイヤー・43 owners・35 Map foot units・地面・道の接続・C西口補正を保つ。角の直交形状は既存の床を保持する。

再現設定は `Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/NativeLocationLayouts-v2-20261006/native-layout-exterior-quality-config-v1.json`。`RecipeId=Auto`、Seed0=E／Seed3=S0。旧デフォルトと C 西口のみの品質設定は変更しない。外周profile `native-exterior-ground-presentation-v1.json` のSHA256は `b655253b0d6d0ba465d28c7bc7d623bd838c117063c7b5739e649e5b40ea8d9a`。生成先は新規 `Assets/Scenes/MapAuthoring/OverheadStudyA/NativeLocationLayouts-Exterior-Quality-v1-20261007`。新規出力内に共通Sprite assets3点とS0の82／Eの78表示部品を保存し、original PNG/importer/Prefab/materialを更新しない。MapGround順序は縦面2・南面3・北面4で固定し、元の水5以降・接地影9・接地草10より下に保つ。直交する角の2面は異なる順序で安定させる。表示profileのSHAを別に記録し、元のlayout/content/geometry hashの意味は変えない。

元の床FrameとNavを保持し、`PresentationFrameWorldBounds` だけ全方向128px拡張して初期カメラとfollow-cameraの表示範囲に使う。元Spriteの樹冠はみ出しが表示枠で欠けないようにする。Hero、Nav、壁、接地順序・入力システム・spawnは変更しない。

検証は現行C#生成器と純粋な外周計画の実行、および元sourceを使ったphase0原寸静的合成。Auto0 E／Auto3 S0の床内RGBA変更0、既存壁外の追加画素0、川出口変更0、旧生成器の戻り値は完全一致。全体・全角・川出口を実ピクセルで確認する。証拠は作業フォルダ `exterior-quality-s0-e-20261007` の `actual-native-exterior-verification.json`、`actual-native-exterior-plan-compile-probe-receipt.json`、`independent-exterior-source-contract-review.json`。原寸変更前後PNGを同フォルダに保存しLibraryへ単独納品する。

ここまでの確認は非占有のファイル／純C#／静的画像。Unity Import・Scene Builder実行・Sprite.assetの保存再読込・実描画・移動／衝突／camera／runtime深度は未実行。Unity操作が許可された別工程で、このTextAssetを選択して既存 `FantasyRoyale > Map Authoring > Overhead Study A > Generate Configured Native Location Assembly` を実行し、新規Map/Entryを保存再読込する。その後S0/E全周の壁・川際・C終端・道の接続とHero前後関係を実プレイで確認する。追加の実行では別OutputRootを使い、既存出力を上書きしない。本変更は既存2配置の外周表示仕上げであり、自由な多方向配置生成の完成を意味しない。


## 2026-10-08 S0/E の実Unity生成・保存再読込

### 目的

承認済み4ロケーションのS0（Auto Seed3）とE（Auto Seed0）を、既存C西口終端補正と外周23辺の表示を含め、実Unityで生成・保存する。別のUnityプロセスでMap/EntryとSprite assetsを開き直して維持を確認し、実描画を証拠にする。

### 背景と根拠

HomePC実行環境が復旧し、Editor asmdefのUnity.InputSystem直接参照不足を修正した。Unity6000.4.3f1の実コンパイルは正常終了・C#エラー0。ユーザーが生成保存工程を実行依頼し、親から当工程の開始指示を受領した。前段の静的画像とオフライン検査をUnity保存再読込の証拠に転用しない。

### 対象範囲

既存BuildConfiguredを呼ぶ明示的batch入口、新規Seed3保存設定、既存品質OutputRoot内の新規2 Seed出力、Map/Entry・Spriteの別プロセス読取り検証とCamera画像。既存source・settings・uncommitted差分を保護する。

### 対象外

本工程にはPlay検証、移動・衝突・runtime Hero深度、大小ロケーション接続、biome遷移の新実装を含めない。元Prefab/原画/入力/Packageの置換、push、外部公開は行わない。

### 実装計画

1. 現行資料と生成入力を確認し、7198 sourceのSHAとGit差分、34関連入力のCRC確認済みZIPを退避する。
2. E設定を保持し、同じprofile/OutputRootのS0 Seed3設定を新規追加する。owned batch Editorから既存Builderを呼ぶ。
3. 新規Map/Entry/Spriteを生成・保存し、生成結果が実E/実S0であることとファイルSHAを記録する。
4. 別プロセスで開き直し、4 native配置・ground overrides・43 owners・35 map foot groups・Nav・外周/終端Sprite・Entry参照を照合してGPU描画する。
5. 実画像を確認し、生成source/設定/既存差分の保護照合と設計文書の結果更新を行う。

### 進捗

- 完了：現行入力・未作成OutputRoot・生成guard・別プロセス検証契約の独立読取り、変更前バックアップ。
- 完了：batch入口とS0設定の追加、実Unity生成・保存。
- 完了：別起動の再読込・Direct3D11描画・画像確認、既存EditMode67/67成功。
- 完了：資料更新とKnowledge 68 Markdown検証、最終7198 source保護照合。既存変更は許可範囲のBuilder/Presentation/2文書だけ、その他7194 filesと既存staged/unstaged差分は保持。新規71 filesは指定出力・設定・batch入口の範囲内。

### 判断記録

利用可能なFantasyRoyale UnityMCPがないため、repo-local手順に従い同ProjectのEditor未起動を確認してUnity本体のbatchmodeを使用する。Builderが全loaded Sceneのpath/clean状態を要求するので、fresh owned batch session内で保存済みMap templateを開いてから呼ぶ。Auto Eは受入れ失敗時のS0 fallbackを許すため、結果recipe/seedを明示検査する。再読込時のcapture framingはin-memoryだけで、保存済みSceneを変更しない。

### 検証

ファイル側のSHA/構造検査、実Unity生成・保存、別プロセスScene/Sprite読取り、GPU Camera画像を別々に記録する。実Playの成功はこの結果から主張しない。証拠はworkspace `two-layout-generation-save-20261008/`。

### 完了結果

E（Auto0）とS0（Auto3）のMap/Entry計4 Scene、外周6点＋E C終端1点のSprite assetsを実保存した。Unity生成PID55380と独立再読込PID45764はexit0、生成64filesのSHAは再読込前後一致。43 owners / 35 Map foot groups / 原115 SpriteRenderer components（active114）を両配置で保持した。外周はE11辺78部品、S012辺82部品、E C終端1点。物理/Nav/spawn/Entry参照/保存SpriteのUV・PPU・GUID/localIDも実Unity読取りで照合した。

実GPU画像2枚を64PPU提示枠で保存し、rootと独立reviewerが実ピクセル確認した。Direct3D11描画のE2048×1152、S02048×1280を生成結果の証拠とする。原川口、道の接続、E C終端、外周と樹冠の保持に明らかな破綻はない。画像から実Playの移動/衝突/Hero深度の成功は主張しない。

実起動時に発見した既存sourceの限定修正は、元Aのinactive preview Cameraをsource条件付きで許可して旧コピーの重複を防ぐ処理と、元C/D地面のstale Sprite localFileIDを新規instanceで正しいSingle Spriteへbindingする処理である。原Prefab/PNG/metaは不変。既存NativeLayoutAssemblyTests/NativeLocationGeneratorTestsは別owned Unity PID52296でEditMode67/67 passed、failed/skipped/inconclusive0、exit0。テストXMLはworkspaceに保存した。

再現設定・出力・限定修正・再読込コマンドは[Native Location Assembly](../../design-docs/scenes/native-location-assembly.md)の2026-10-08節に追記した。変更前バックアップ `before-generation-save.zip` は34files/187243bytes、SHA256 `f1819dabad754be824b2d966ce1c79ab74a281a7890c04e1e4aba89febe072fc`。最終保護照合の結果はworkspace receiptに記録し、工程引継ぎに添える。本段階を終えた後もPlay・接続拡張・biomeは親の次段階合図を待つ。


## 2026-10-08 最新S0/Eの実Play検証

### 目的・範囲

ユーザー実行依頼と親の次段階指示により、最新S0 Auto3/E Auto0の保存Entryを実Playで読み、ロケーション間移動、岸/外周衝突、橋の状態切替と通行、Hero足元前後、川animation、独立部品の状態連動と通常cameraを検証する。大小ロケーション接続・biome新実装は対象外。

### 計画・進捗

1. 7269 sourceのSHAとGit差分を記録し、124関連ファイルをCRC/SHA確認済みZIPへ退避した。
2. 正式desktop入力ツールがないため既存PlayModeテストassembly内にPreviewDebug検証を追加し、Input Systemの仮想Keyboard状態を通常Human adapter/Actorの実Update・FixedUpdateに渡す。先に非表示batchのfocus/input可否を確認する。
3. 実Playで通常cameraのGPUフレームと位置/接触/状態/時間変化を記録し、検出した対象バグだけを限定修正する。
4. 原Scene/素材/ユーザー差分の不変を照合し、実証済みと未確認の範囲を明記する。

### 判断・検証の条件

UnityMCP/desktop入力が利用できないためrepo手順のowned batch PlayMode Test Runnerを使用する。同ProjectのEditor未起動を確認し、GPU検証では-nographicsを付けない。Entry/Mapは保存Sceneを読み、Play中の試験用位置・状態変更をSceneへ保存しない。関連Scene/PrefabのAudioSource/Listenerは読取り調査で0件、試験sessionもAudioListener.volume0/pauseで静音化する。音やユーザー入力を必要とする試験は停止する。

### 結果

完了。最新保存E Auto0/S0 Auto3を実Playで実行した。Unity6000.4.3f1/Direct3D11、最終attempt4 PID57880（19:03:34–19:04:56 UTC）はexit0、PlayMode 2/2 passed、failed/skipped/inconclusive0、67.2138172秒。通常cameraの実GPU PNG計25枚をrootと独立reviewerが実ピクセル確認した。

両配置の認定接続3口と追加共有草地を実Actorで往復し、通常camera追従・ortho8/Z保持・1/32位置snapを確認した。岸の実capsule停止、外周全18 Collider（E8/S010）の実入力接触、E C西capでの停止を確認した。橋present/restoredは往復でき、absentは水gap40 Collider/Navで停止する。C tree present/absent/restoredは描画・body・Navが連動し、absent時は約2.1world通過、無関係42ownersは保持。35 Map foot transforms、43owners、Hero込み36depth unitsを維持した。Heroが木の足元Yを実移動で跨ぎ、同frame GPU control差で前1688px/後42pxの遮蔽を両配置で確認した。川11 active animationの実時間進行・loopと全11点の個別pause/resume、他10点の継続を確認した。川GPU phase差はE12562/S014146px。

非表示batchはfocused=falseで原Diagnostic人間入力guardが停止する。runtimeを変更せず、テスト専用Update driverの仮想Keyboard→既存Human adapter→同Actor.SetMoveCommand→通常FixedUpdate/実Physicsを使用した。camera LateUpdateも通常経路。probe準備のActor再配置と、その後の実入力移動を区別する。focus=trueの原Diagnostic入力、desktop実キー操作、GUI/HUDは未確認。独立ownerの自律移動は未実装で、状態連動とanimation個別停止だけを実証した。

attempt1のfocus制約失敗、attempt2の入力probe限定成功、attempt3のcached Time.time期限によるdepth待機不足を保存した。テストDriveをadapter Update確認後の実FixedFrame数待機へ直したattempt4で全検証成功。ゲームruntimeの修正は不要だった。追加は既存PlayMode assembly内の2テストsource/metaだけで、元Scene/素材/入力guard/Package/settingsを保持した。AudioSourceは実Playのinactive込みで0件、試験sessionも静音化し、音やユーザーの操作を必要とする試験は実施していない。

再現コマンド、入力資格、実証項目、限界は[Native Location Assembly](../../design-docs/scenes/native-location-assembly.md)の実Play節に記録した。証拠はworkspace `native-play-verification-20261008/`（attempt4 log/XML/receipts/25 PNG、独立review、保護照合、最終handoff）。変更前CRC/SHA確認済みZIPは124files/350532bytes、SHA256 `291e5eda569a88d78af911117883d60faf021956bd67b899f891241df2ff3645`。元7269 source、既存staged/文書以外unstaged raw差分、前工程の64生成filesの保持を最終照合する。

本Play段階を完了する。元目標2画像の未受取とLibrary upload helper接続失敗は残り、手動入力の未確認を成功扱いしない。自由な方向別接続・大小ロケーション・biomeの段階は親の次段階指示を待つ。


## 2026-10-08 B/J/D/C 異サイズ・複数方向接続の最小実装

### 目的と範囲

親から追加承認されたB東→新規J→D西、D南→C北を実装し、J6unitを代表、4/8unitを比較する。元B/C/D coreと受領済みE地面、元S0/E、既存ユーザー差分を保持する。新port素材、T分岐、biome、森林全体のランダム生成は後続工程。実装と検証を終えた候補を提示し、ユーザー確認待ちにする。

### 実装計画と結果

1. 変更前7273ファイルのSHA・元Git差分を固定し、元175ファイルの復元ZIPをCRC/全SHA検証した。
2. 新規pure plan、地面writer、既存builderの新規partial、batch検証、追加Edit/Play試験の6sourceを別名で追加した。元runtime/build経路は変更していない。
3. 長さだけ異なる4/6/8unit設定を追加し、曲がる実床の二値maskから共通Nav/外周Physicsを生成した。native64PPU/Point/非圧縮、端32pxの受領済みRGBAを保持した。
4. 実Unity生成・保存、別PID再読込、全体GPU、通常Actor/Physics/カメラでの往復・3レーン・境界接触・木の前後・owner復元を行った。
5. 既存S0/Eの実Play2件とEditMode既存67+追加34件を回帰検証した。通常画像の重大な端帯を修正した最終ART3を保存し、通常Play3件を再確認した。
6. 日本語の設計資料、再現設定、最新出力key、既存差分保護・実際に確認できた範囲と制限を記録した。ユーザー確認前に親taskを完了へ移さない。

### 最終候補の証跡

workspace `flexible-native-implementation-20261008/unity-art3-attempt1` に最終生成/再読込receipt、Direct3D11全体画像3+3枚、Play `play-attempt1` の通常画像42枚と3receipt/XML/log、Edit `edit-regression` の101件XML/logを保存した。J6最新出力は `Assets/Scenes/MapAuthoring/OverheadStudyA/NativeFlexibleConnectionPilot-v1-20261008/J-6u-Seed-0-6ff81ef83adf/NativeEntry.unity`。比較J4は `J-4u-Seed-0-cbce6d84b1c8`、J8は `J-8u-Seed-0-b5bb84212290`。設定・メニュー・詳細手順は[Native Location Assembly](../../design-docs/scenes/native-location-assembly.md)の同日B/J/D/C節を参照する。

最終Playは3/3 passed、2026-10-08 20:52:56–20:53:42 UTC、46.7093953秒。計63移動/39代表境界接触、Heroの木前後GPU対照（前1688px/後44px）、通常cameraの追従とbounds/snapを確認した。最終Editは101/101 passed。既存S0/E回帰は `native-play-regression`、2/2 passed、20:29:17–20:30:24 UTC、67.0974114秒、通常画像25枚。各XMLのfailed/skipped/inconclusiveは0。

元coreは16owners/12接地unit。追加Jは岩2/草6、8owners/2接地unit、body/Nav障害0の装飾。全Map24owners/14接地unit、Hero込み15。床/予約口/coreの同一性、保存済みデータ照合と物理接触を別々に検証した。保存生成receiptの `StructuralOnly=true/UnityRuntimeVerified=false` は生成時の範囲を表し、別Play検証の結果を偽って書き戻していない。

### 視覚修正と失敗の記録

初期H64のfanをH80へ変更して外形の角を弱めた。ただし全境界の接線連続性を保証しない。ART1は通常画像で端帯内側の直線的なトーン差が見つかった。ART2の元画素延長では土の突き出しが生じたため、草/土の同分類条件を全延長画素に適用するART3へ修正した。最終版は4/6/8とも固定帯RGBA差分0、床mask差分0、床外不透明0、草/土分類変更0、元palette外0。実画像で重大な旧帯と土の突き出しが改善したことをrootと独立reviewerが確認する。

最初の再読込失敗2回はColliderのSerializedObject比較にObjectReference子のm_FileIDが含まれたためで、診断は4 identity参照値のみの差を示した。ObjectReference subtreeをskipし、材質はasset pathで比較するよう修正し、shape/offset/trigger/layer等の物理設定比較を保持した。失敗receipt/logと `collider-reference-iterator-diagnosis.json` を保存している。旧ART1/ART2出力も上書き・削除せず残した。

生成/再読込全体GPUには元C下部の重なる樹冠に5869 RGB画素差がある（J内差0/alpha差0）。元足元同Y・EditMode同sort順位による局所的な描画順差を記録し、全体画像一致を主張しない。保存済み全ファイルSHAと実Playの通常sorter確認を別の証拠として扱う。

### 残る確認と制限

最小接続候補の確認待ち。J8は最も緩い外形、J6が代表、J4は短く絞られる。native草の反復、外周と暗い床外の浮島感、新規J樹冠0は残る。初期参照2画像をHomePCへ正規Library経路で受け取れなかったため、元目標画像との完成判断は保留する。

batchのfocus=falseで元DiagnosticはInputEnabled=false/InputReadFrames=0。試験専用仮想Keyboard→元Human input adapter→通常Actor/Physicsという経路で実走した。focus=trueのdesktop実キー/GUI操作は未確認。音声試験やユーザー操作は要求していない。代表13接触/版は全境界上の実走を意味しない。Seedは固定外形のmetadataと地面samplingに使う。Jの非ゼロ障害切替は未実装で、J装飾のgraphics/足元復元と既存S0/Eの元障害切替を区別する。

### 保護・復元

復元ZIP `before-flexible-native.zip` は175files/4213032bytes、SHA256 `672483717bcb8455f40878046884afe912a99ccd0f3ac4c5072859079b9cda1b`。`baseline.json` と元Git patch、`installed-source-receipt.json`、最終 `preservation-result.json` をworkspaceに保存する。既存source/art/S0/E/settingsを変更せず、許可された既存2資料と新規6source/meta、専用設定/生成出力だけに限定する。Library納品はhosted helper接続失敗のためローカル証跡で引き継ぐ。購入・publish・pushは行わない。

## 2026-11-05 終了

所有者が[森マップの画作り基準](forest-look-lock.md)を承認し、森マップは別の構成と生成手順へ移った。この計画の続きは行わない。未完の項目は履歴として残す。
