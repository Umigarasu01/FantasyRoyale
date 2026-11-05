# Native Location Assembly v1

Overhead Study A の独立ロケーション A/B/C/D を、実画素で認定された接続口から選んで組み合わせる試作入口。既存の固定 Map/Entry、独立 Prefab、旧 Tilemap Builder は保持する。試合進行へ自動的に組み込む変更ではない。判断と検証結果は [Overhead Study A ExecPlan](../../exec-plans/completed/overhead-study-a.md) を参照する。

## 2026-10-09 全体配置の目標と暫定寸法

所有者の「一旦それで資料に追加お願いします」により、以下を配置設計の暫定規格として記録する。今回は資料更新であり、全体Map生成の実装、既存Sceneの寸法変更、既存素材の拡縮は行わない。

### 全体の構成目標

大きな一枚の長方形全体を、サイズ・縦横比の異なる長方形エリアと長方形の通路ブロックで、隙間なく埋める。エリアの位置をずらし、通路ブロック内の曲がる道と地形・植生の不規則な輪郭で有機的につなぐ。均等な3×2 gridへの固定配置と、孤立した島を細道だけでつなぐ構成は全体案として採用しない。配置用の長方形と、見える道・バイオーム境界の自然な輪郭は別の責務とする。矩形用地が隣接しても、全共有辺を自動的に歩行接続とせず、通行口と障害物を別に設計する。

### 配置単位と寸法

| 項目 | 暫定規格 |
| --- | --- |
| 全体Map用地 | 暫定24×18U = 144×108 Unity単位。最小grid2単位で72×54区画 |
| 配置用1U | 6 Unity単位。地形64PPUでは384px |
| 最小配置grid | 2 Unity単位 = 128px = 1/3U |
| 標準通路用地幅 | 6 Unity単位 = 1U。脇の草木・地形表示を含むブロック総幅 |
| エリアの幅・高さ | 最小gridの倍数で大小・縦横比を変える。例: 6×10、8×12、12×18 Unity単位 |
| 可歩幅・見える道幅 | 用地幅とは別に定める。1U全幅が歩ける道を意味しない |
| 接続口の位置・幅 | 用地grid、局所offset、床の接続条件、経路予約を分ける。接続口1U刻みは未確定 |

ここでいう1Uは配置用の呼称であり、Unityの1 world unitではない。2×2U、2×3U、3×3U、2×4Uのエリアと、幅1U・長さ1〜4Uの通路は例として検討できるが、全エリアを整数Uへ無理に丸める規則ではない。既存A12×10、B6×10、C8×6、D10×8は2単位gridに適合する一方、6単位の整数Uには全てが適合するわけではない。既存coreは原寸で保持し、今後の配置枠や接続口offsetを別に扱う。

### 2026-10-09 全域サイズと進行方針の追加

所有者の「一旦その広さで進めましょう」により、全体Map用地は暫定24×18U（144×108 Unity単位）で進める。前段の1U・2単位grid・総幅／可歩幅／見える道幅の区別、既存core原寸保持の規格は維持する。地形64PPUでは全域座標が9216×6912pxに相当するが、単一Textureの制作や全域生成の実装を指定するものではない。

進行イメージは、育成中脱落なしで互いを妨害しながらビルドを完成させ、最後のバトルへ進むシティトライアル風とする。エリア収縮は当面採用せず、Mapを狭めない。人数・各フェーズの時間・妨害のペナルティ・最終バトルの詳細は未確定で、[Prototype方針](../../product-specs/prototype-direction.md)を参照する。今回の追加は資料更新のみで、既存v1/S0/E/J、Scene寸法、素材、移動・HPの実装を変更したものではない。

### 実値に基づく比較

最新NativeEntryが参照する `DerivedTerrain/hero_idle.png` は64×80px、64PPU、Scale1。Sprite全rectは1×1.25 Unity単位、透明余白を除く剣・盾を含む静止姿勢のalpha範囲は48×61px、見かけ0.75×0.953125単位である。足元CapsuleCollider2Dは0.48×0.55、offset(0,0.22)。`CharacterActor2D`とNativeEntryの移動速度は5単位/秒で、斜め入力も正規化する。正式戦闘Animationの最大表示幅は未計測である。

通常cameraはorthographicSize8で縦16単位を表示する。追従時はGameViewのaspectを使い、直近の4:3実表示では横約21.33単位、16:9なら横約28.44単位となる。

| 配置用1U候補 | 地形64PPU | Hero見かけ幅0.75との比 | 画面縦16に占める用地幅 | 直線1Uの通過時間 | 2×2Uエリア |
| --- | --- | --- | --- | --- | --- |
| 4 Unity単位 | 256px | 約5.33体分 | 25% | 0.8秒 | 8×8単位 |
| 6 Unity単位 | 384px | 8体分 | 37.5% | 1.2秒 | 12×12単位 |
| 8 Unity単位 | 512px | 約10.67体分 | 50% | 1.6秒 | 16×16単位 |

6単位は現在のJの用地総幅と一致し、約4単位の内部床を持つ表示と脇の装飾用地を両立しやすい候補である。2×2Uエリアは画面縦幅の75%となる。4単位は細かな配置に向くが、現在の床幅を維持すると脇の余地が小さい。8単位は余地が広い一方、例示した2×2Uエリアだけで画面縦全体を占める。

現在のJ4/J6/J8は長さ4/6/8単位の比較で、用地総幅は全て6単位である。幅4/6/8を実生成して比較した実験ではない。最新J6の内部床はPNG alphaと保存Geometryの照合で3.75〜4単位、全列最小は3.734375単位。見える土道の公称幅1.5単位、B–J–Dの予約口幅1単位とは別であり、これらを将来の標準可歩幅として自動採用しない。現在の床外を装飾用地として数えても、そこを歩けることの保証にはしない。

比較表の「体分」は距離の比で、横並びで通れる人数・戦える人数ではない。時間は障害物のない直線を速度5で進む計算値で、曲がり・停止・衝突・戦闘を含まない。戦闘射程、回避に必要な幅、総人数、幅候補の実表示・通行比較は未確定または未検証であり、6単位を最適値と確定しない。

実値の確認先は `StudyABlockMovementDiagnostic`、`CharacterActor2D`、最新J6のNativeEntryとHero PNG/importer、`StudyANativeFlexibleConnectionPlan`、`StudyANativeFlexibleGroundWriter`。戦闘の未確定事項は [Character・戦闘基盤ExecPlan](../../exec-plans/active/combat-foundation.md) を参照する。以下のv1/S0/E/Jは保持する既存接続試作の記録であり、この全体配置方針を実装済みとする証拠ではない。

`StudyABlockGenerator.GenerateNativeAssembly` は catalog version と seed からロケーション数・開始候補・接続候補を選ぶ。各ロケーションは一度だけ使用し、回転、反転、拡縮は行わない。認定口は A東–B西、B東–C西、C北–D南。B東上部–D西下部は許可済みの追加草地接触で、拡張用の道口ではない。他の共有辺は拒否する。

入口は native pixel 境界アンカー同士を一致させ、整数 origin のまま正規化する。旧 cell 接続の `+1` を加えず、保存時だけ64PPUで world へ変換する。口の土幅は描画・入口予約であり、広い共有草地を狭い物理ゲートへ変えない。A spawn の41.6pxは連続座標のまま保つ。

現在の catalog は AB / BC / CD / ABC / BCD / ABCD の6種類の内容と床形状を作れる。全4個を指定すると正規化された ABCD 配置は一意であり、seed が異なっても random な地形と呼ばない。`ContentHash` は位置・素材hash・接続の正規形から計算し、seed や探索順を含めない。4個を使う別配置には、反対側の接続地面と入口予約を認定した新しい方向別ロケーションが必要になる。

`StudyANativeLocationGeometry` は選択床の union から1world幅の物理外周を作る。Frame 内の欠けは navigation のみへ登録する。水283矩形は A が選ばれた場合のみ、固定済み水契約から引き継ぐ。選択された所有者と foot unit だけを既存の状態・深度システムへ渡す。

## 再現設定と Scene 生成

設定は `Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/NativeLocationAssembly-v1-20261006/native-assembly-config.json`。`Seed`、`MinBlocks`、`MaxBlocks`、`RequestedBlockCount` を変更する。通常は2–4個、`RequestedBlockCount=0` で個数もseed選択。特定の個数には2/3/4を指定する。catalog は同フォルダの `native-assembly-catalog.json` に保存する。素材・テンプレートSHAは入力変更検出用である。

v1 catalog、MinBlocks=2、MaxBlocks=4、RequestedBlockCount=0 で実C#生成器から確認した代表結果:

| Seed | 選択 | Native frame px | 所有者 / Map foot units |
| --- | --- | --- | --- |
| 11 | AB | 1152×640 | 34 / 28 |
| 21 | BC | 896×640 | 14 / 10 |
| 25 | CD | 640×896 | 9 / 7 |
| 7 | ABC | 1664×640 | 41 / 33 |
| 1 | BCD | 1024×1024 | 16 / 12 |
| 0 | ABCD | 1792×1024 | 43 / 35 |

foot units はMapだけの個数で、HeroはEntry側からさらに1個加わる。探索開始ロケーションによってspawnが変わるため、結果JSONのspawnLocationIdも確認する。

Unity操作が許可され、Play/Import中でなく、開いているSceneが保存済みのとき、設定TextAssetを選び `FantasyRoyale > Map Authoring > Overhead Study A > Generate Configured Native Location Assembly` を実行する。指定しなければ既定設定を使う。新しい `Seed-{seed}-{hash先頭12字}` フォルダへ NativeMap / NativeEntry と入力・幾何・結果JSONを保存する。存在済み出力は上書きせず拒否する。再生成したい場合は別OutputRootを設定する。

新しいEntryは既存Hero・人間入力・camera follow・深度システムを保持し、Route[0]を選択ロケーションのspawnへ移す。自動歩行経路は作らない。コピーSceneのcameraだけを選択Frameへ合わせ、既存runtime camera codeは変更しない。

## 検証の範囲

今回の検証は実C#生成器のオフライン実行と、保存済みPrefabのnative画素をその出力配置で合成する静的表示。Unityのimport、Scene Builder実行、Physics、人間入力、camera follow、実画面での深度は未確認である。NUnit fixtureの実行結果はUnity Test Runnerで別途確認する。オフラインのコンパイル成功や合成PNGをUnity動作保証として扱わない。


## Native Location Layouts v2 — S0 / E

正式受領した B_E / D_W の地面2枚だけを追加し、同じ A/B/C/D を S0（A–B–C–D）または E（A–B–D–C）へ組み合わせる。43 owners・35 Map foot units・114表示パーツを保持する。N案は作らない。原Prefab、原画、propsの位置・影・接地・foot・Nav・sortは変更せず、回転・反転・拡縮を行わない。新instanceの単一ground SpriteRendererへ選択地面をoverrideし、Prefab instance変更として記録する。

### 再現と互換性

設定は `Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/NativeLocationLayouts-v2-20261006/native-layout-config.json`。`MinBlocks=MaxBlocks=RequestedBlockCount=4`、`RecipeId=Auto` で受入れ済みrecipeをseed選択する。今回の実C#64seedで S0 の代表seedは 3、Eは 0。`RecipeId=S0` または `E` ならseed0でも配置を明示できる。各recipeの空間構成は1種類で、Autoの変化は2つのrecipe間で生じる。

`native-layout-set.json` は不変のBaseCatalogと6variantを持つ。recipe選択後にlogical A/B/C/Dごとに1variantへ解決し、OwnerId重複guardを維持する。S0は旧catalog/version/seedのv1生成器を使い、旧 `Assembly.ContentHash` と全4枚PNGを保持する。旧v1の2–4枚subset設定・出力も変更しない。v2の `LayoutContentHash` はvariant/source/band/maskを含み、seedやrun証拠SHAを含めない。結果へrecipe/variant/seed/versionの再現情報を保存する。

Unity操作が許可される別の作業時には、この新設定TextAssetを選択し、既存の `FantasyRoyale > Map Authoring > Overhead Study A > Generate Configured Native Location Assembly` を使う。旧設定を既定値として保持しているので新設定の選択が必要。新OutputRootは `Assets/Scenes/MapAuthoring/OverheadStudyA/NativeLocationLayouts-v2-20261006`。既存出力の上書きは拒否する。今回はScene生成・Unity操作を実行していない。

### Eの接続契約

PPU64、B_E384×640 / D_W640×512。mouth予約64px（offset−32）と内部の見える土道96pxは別概念で、土色alphaの二値検査を通行gateにしない。primary keyは `native-band-v2/grass-family-1/soil64/height0/phase-port-centred`。版付きRGBA profileで境界を固定し、PNG全体hashを互換keyとして使わない。

原点は A(0,256), B(768,256), C(1152,0), D(1152,384)、Frame1792×896。AB640 / BD512 / CD512 / BC128の全共有辺を承認する。BC128は通行接続ではなくCに接するgrass adapter。三者角とDのrow511全640pxを保護する。旧B west / D south ports、A/C定義を保持し、EのC west portは未使用の外周閉塞とする。

### 素材受入れと拒否条件

正式Library ZIP `libfile_78db3b1085dc81918719712fff9b7e1a`、SHA `bba6fb2b7d789d7f620bb1ff3ebccb110d6f40463c0ba8f331d8e95169299cd7` から実2PNGを受領した。B_E SHA `1e398377e2d2c8cd315f5e3729fe2f2e383db48510487eccf2a37489cf1a049a`、D_W SHA `fbbc53d4792e550859fe3069df2048bbde265e61ec0ce2066d3f6b1651a35487`。原33PNG/metaと4Prefab/metaをpinする。

`e-art-intake-spec.json`、`primary-edge-profile.json`、編集mask2枚、`AcceptanceEvidence/art-acceptance-receipt.json` を実byteに結び付ける。PNG RGBA8・全opaque・native寸法・APNGなし・mask外RGBA同一・primary512・legacy128・旧AB640/CD512・角色・D下端全640を検査する。Unity importerはPPU64/Point/Single/uncompressed/pivot/寸法を別gateで検査する。

Pixel検査だけでEを適格にしない。正式受領証拠と、同じB/Dハッシュに結び付いた全景・native collarの技術visual受入れ記録が必要。configに記録path/SHAを固定し、保存前に再検査する。欠落・不一致時はAutoがS0を選び、E指定は拒否する。未編集variantのmaskはUTF8文字列 `FantasyRoyale.NativeGroundEditMask.v1\nmode=no-repaint\n` のSHA `d74ed6f9538e75e9f68a8385449418896ce67a34c1e6362eb5358f57609b44cd` と明示し、編集B/Dへ転用しない。

### 実施した検証と制約

元画像を新画像とするnegative controlは正しく拒否。実素材の画素検査12/12、独立受入れ契約35/35、実C# production検査 14/14 を通過した。64 Auto seedは2recipe・2空間構成・2内容hashを返し、同seed再現性と旧S0 hashを確認した。Eは外周8矩形・void2矩形・水283矩形で static293、S0は既存の10/3/283で296を保持する。

実C#の戻り値から元の114独立層を合成し、S0/Eを原寸で比較した。切り出したlocation完成画像を貼り付けていない。B東の樹冠20px、C北の樹冠136/144pxを含む全Spriteを保持する。353足位置のphase0 Hero実opaque1688pxを使った前景・旧owner Nav・保守的body検査を実施した。

これはファイル側のC#生成と保存済みsourceによる静的表示検証。EditorコードとNUnit fixtureはoffline compile済みだが、Unity importer、Scene Builder実行・SaveReload、NUnit実行、Physics、人間入力、camera follow、animated phase/実プレイは未確認。最初の目標参照2画像はHomePCで実ピクセル受領が成功していないため、その目標に対する最終見た目判定は保留する。


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


## 2026-10-08 実Unity生成・保存再読込の完了

Unity 6000.4.3f1で外周品質設定のE（Auto Seed0）とS0（Auto Seed3）を実生成した。Map/Entry計4 Sceneと外周Sprite assets計6点、EのC西終端Sprite asset1点を保存し、別のUnityプロセスで開き直した。生成PID55380、再読込PID45764は双方exit0。64生成ファイルのSHA256は再読込前後で一致した。以前の節の「Unity未実行」は各工程当時の状態であり、この生成・保存・再読込については今回確認済みである。

| 配置 | 保存設定（Layouts-v2-20261006内） | 出力Seedフォルダ | 外周 / C終端 | 実GPU画像 |
| --- | --- | --- | --- | --- |
| E | `native-layout-exterior-quality-config-v1.json` / Auto Seed0 | `Seed-0-fbdad6ba333c` | 11辺78部品 / 1点 | `E-Seed0-unity-reloaded-native.png` / 2048×1152 |
| S0 | `native-layout-exterior-quality-s0-seed3-config-v1.json` / Auto Seed3 | `Seed-3-9b43d4858afc` | 12辺82部品 / 0点 | `S0-Seed3-unity-reloaded-native.png` / 2048×1280 |

出力親は `Assets/Scenes/MapAuthoring/OverheadStudyA/NativeLocationLayouts-Exterior-Quality-v1-20261007`。各Seedフォルダに `NativeMap.unity`、`NativeEntry.unity`、生成設定・assembly・geometry・reportとSprite assetsを保存した。両配置の原43 owner identities、35 Map foot groups、原115 SpriteRenderer components（うちactive表示114点）、4 native locationの位置・回転0・scale1が維持された。Mapに元Aのinactive water highlightとinactive preview cameraも保持する。EのB_E/D_W地面override、Eの外周壁8/void2/water283/static293、S0の10/3/283/296、EntryのMap参照・35 serialized depth units・input/spawn/camera設定を別起動のUnity型読取りで照合した。Heroの36個目のfoot unitはPlay時の予定値であり、本工程では実行確認していない。

### Unity実行で見つかった生成入口の修正

元Aには `WestBaselineCamera_DISABLED` という非activeのプレビューCameraが含まれる。元Prefabを保持するため、Builderのsource guardで名前・非active・leaf・Transform/Camera/StudyACameraYSortのみという限定条件を許可した。コピー元Mapを組み直す時はactive MainCameraだけを退避し、旧Aのinactive previewを重複コピーしない。生成後はactive MainCamera1台と継承inactive preview1台である。

元C/D地面PrefabのSprite参照は古いlocalFileIDで、実ImporterのSingle Sprite（localFileID21300000）へ解決されなかった。原PNG/meta/Prefabを変更せず、catalogが指す原地面を型付きで読込み、新規Prefab instanceの地面だけを正しいSpriteへbindingしてoverrideを記録する。Eのaccepted B/D地面overrideより前に行い、C終端は同じ原C materialを使用する。他のSprite欠落は引き続き拒否する。

### 再現手順と証拠

Unity EditorでPlayを停止し、開いているSceneを保存した状態で上表の設定TextAssetを選択する。既存 `FantasyRoyale > Map Authoring > Overhead Study A > Generate Configured Native Location Assembly` を実行する。保存済み出力は上書き拒否するため、追加の生成試行では設定を別ファイルへ複製し、`OutputRoot` を未使用フォルダへ変更してから実行する。既存E設定のSeedと入力SHAは保持し、S0 Seed3用設定を新規追加した。

batch入口は `FantasyRoyale.MapAuthoringKit.Editor.StudyANativeLocationBatchVerification.GenerateBatch` と `VerifyReloadBatch`。前者は上表2設定を固定で読み、後者は別PIDで保存Scene/Spriteの再読込を照合する。Unity本体へ `-batchmode -automated -quit -projectPath <project> -executeMethod <method> -studyNativeEvidence <absolute evidence folder> -logFile <fresh log>` を指定する。生成時のみ `-nographics` を追加し、GPU撮影を行う再読込時は付けない。証拠フォルダには前者が保存する `unity-generation-receipt.json` が必要で、receipt/画像の再上書きも拒否する。二重Editor起動を避ける。

作業workspace `two-layout-generation-save-20261008` に生成・再読込receipt、各Unity log、67/67成功の既存EditModeテストXML、バックアップZIP、保護照合を保存した。テストはNativeLayoutAssemblyTestsとNativeLocationGeneratorTestsの2クラスのみ、failed/skipped/inconclusive0。実画像はDirect3D11のCamera.Renderで描画し、原寸64PPUの提示枠を使用した。撮影時のCamera framingはin-memoryのみで、保存Sceneを書き換えていない。実画像を確認し、異なる2配置、川の南北開口、橋、外周土面、EのC西終端、元の樹冠のはみ出しに明らかな欠けはなかった。

本工程の完了範囲は実生成・保存・別起動再読込・EditMode GPU描画と既存テストである。人間入力、移動・衝突、Heroと小物のruntime前後関係、camera followの実挙動は次のPlay工程で確認する。初期目標2画像のHomePC受取未完了による最終的な目標一致判断も継続保留する。自由な多方向接続・大小ロケーション・biome遷移は別工程である。


## 2026-10-08 保存済みS0/Eの実Play検証完了

上記の最新保存EntryをUnity 6000.4.3f1の実PlayModeで読込み、通常ActorのFixedUpdate/Rigidbody2Dと実Physics2D、通常follow camera、足元深度、owner状態、川animationを実行した。最終attempt4はPID57880、2026-10-08 19:03:34–19:04:56 UTC、exit0。Unity Test RunnerはPlayMode 2/2 passed、failed/skipped/inconclusive0、67.2138172秒。Direct3D11で通常cameraのGPU PNGをE13枚・S012枚、計25枚（各1365×1024、ortho8、64PPU相当）保存し、rootと独立reviewerが実画素を確認した。static合成や前工程のEditMode画像を今回のPlay証拠に転用していない。

### 入力経路と確認の限界

正式desktop入力ツールは利用できず、非表示batchではApplication.isFocused=false。原Diagnosticの意図したfocus guardがInputEnabled=false/InputReadFrames=0で通常人間入力を停止することを最初の実Play probeで確認した。runtime guardを変更せず、テスト限定Update driverをその後に実行し、元Player/Move assetを複製した専用仮想Keyboardから既存HumanCharacterMoveInputAdapterへ渡し、同じCharacterActor2D.SetMoveCommandを呼ぶ。以降は通常Actor.FixedUpdate、実Rigidbody2D/Physics2D、Diagnostic.LateUpdateの通常camera followを使用する。DiagnosticのUseHumanInput=true/AutoTraverse=falseを保持し、反射によるprivate callback呼出しや手動Physics simulationは使っていない。

これはテスト専用入力経路による実Play移動・衝突・描画の検証であり、focus=trueの原Diagnostic入力、desktopの実キー操作、GUI Game View/HUDの確認は未実施。各接触probe前のActor配置は準備で、通行成功はその後のDriveによる実移動量と接触から判定した。GPU撮影では通常cameraのtransform/orthoを変えず、RenderTextureへ描画している。Play中の位置・owner状態・animation停止・撮影設定をSceneへ保存していない。

### 実際に確認した挙動

| 対象 | E（Auto0） | S0（Auto3） |
| --- | --- | --- |
| 認定接続3口 | A–B / B–D / D–Cを各往復 | A–B / B–C / C–Dを各往復 |
| 追加共有草地 | B–Cの共有草地を往復 | B–Dの共有草地を往復 |
| 外周の実接触 | 全8 Colliderを個別に停止確認 | 全10 Colliderを個別に停止確認 |
| C西の未使用道終端 | 草cap表示と西壁での停止を確認 | 対象なし |
| HeroとC anchor tree | 前1688px、後42px | 前1688px、後42px |
| 川の2時点GPU画素差 | 12562px | 14146px |

両配置で橋present/restoredを往復した。absentではbridge-owned graphicsが消え、橋のNav4矩形に代わって水gap40矩形と40 active gap Colliderが有効となり、Heroが西側で停止した。水283矩形を引継いだ両岸でも実capsuleが停止する。全外周・岸・橋gapはCapsuleCastで対象を確認したうえで実Actorを入力移動させ、最終Physics2D.Distanceで接触/非貫通を検査した。Nav値だけを通行結果として扱っていない。

C anchor treeをpresent→absent→restoredとし、owned graphics/body/Navが同時に消えて戻り、absent時は実Actorが元trunkを約2.1world通過することを確認した。live Navは24→23。無関係42 ownersの状態・Nav・body/graphics設定は保持された。35 Map foot transforms、43 ownersとHero込み36 depth unitsも保持した。Heroは実移動でtree foot Yを跨ぎ、前後sortが変化した。同一Frame/FixedFrame/Actor/CameraでHero表示と一時非表示controlをGPU撮影し、両配置とも樹冠前は1688画素、後ろは42画素だけHeroが見えることを独立画素比較と原寸目視で確認した。

通常cameraは実A–B移動に往復追従し、ortho8/Z=-10を保持、1/32world位置snapも維持した。11 active川部品（surface1、岸foam2、岩foam4、水草4）は実時間で全て進行・loopし、各部品だけを個別にpause/resumeした時も他10部品が進行した。元inactive 64-frame WaterHighlightsはinactiveのまま保持し、11点の成功に含めない。独立挙動として検証したのはownerのpresent状態連動とanimationの個別pause/resumeであり、owner自律移動は現行実装にないため確認済みとしない。

### 再現と証拠

新規テストは既存PlayMode assembly内の `StudyANativeLayoutsPlayPreviewDebugTests.cs` と `.Checks.cs`（各meta付き）だけ。ゲームruntime、保存Map/Entry、元Prefab/PNG/importer、Packages/ProjectSettingsは本Play工程で変更していない。GUIではUnity Test RunnerのPlayModeから `StudyANativeLayoutsPlayPreviewDebugTests` の2テストを実行できる。batchでは同ProjectのEditor未起動を確認し、Unity本体へ次を指定する（evidence/log/XMLは未使用の絶対path）。

```text
-batchmode -automated
-projectPath C:\Users\umiga\Repository\FantasyRoyale
-runTests -testPlatform PlayMode
-testFilter FantasyRoyale.Playtest.Tests.PlayMode.StudyANativeLayoutsPlayPreviewDebugTests
-studyNativePlayEvidence <fresh absolute evidence folder outside Assets>
-testResults <fresh absolute native-play-results.xml>
-logFile <fresh absolute native-play.log>
```

GPU描画のため `-nographics` を付けず、runTestsには `-quit` を付けない。引数に空白を含むpathは各shellで適切にquoteする。入力と状態は試験専用device/instanceに限定し、cleanupで復元する。Scene/Prefab読取りではAudioSource/Listener0件、実Playでもinactiveを含むAudioSource0件を確認し、試験中はAudioListener.volume0/pause=trueとした。音やユーザー入力を必要とする試験は行っていない。

workspace `native-play-verification-20261008/attempt4` に実Play log/XML、E/S0 receipt、25 PNGを保存した。独立読取り/全画像reviewと最終保護照合、引継ぎは同workspace親に保存する。attempt1は通常入力focus制約で失敗、attempt2は入力probeだけ成功、attempt3はGPU保存後のcached Time.time期限によるテスト待機不足で両depth probe失敗として保持する。Driveを実adapter Update確認後の実FixedFrame数待機へ変更したattempt4が全項目成功した。ゲーム本体の変更が必要な不具合は今回検出されていない。

変更前ZIPは124 files / 350532bytes、SHA256 `291e5eda569a88d78af911117883d60faf021956bd67b899f891241df2ff3645`、CRCと各original SHAを確認済み。元7269 sourceと既存Git staged/文書以外unstaged差分、前工程の64保存生成filesをSHA/raw差分で保護する。最終結果は `preservation-result.json` を参照する。Library保存は正規upload helperのhosted tools接続失敗により成立しておらず、画像はローカルに保存されている。

初期目標2画像のHomePC受取未完了による最終目標一致判断は保留する。今回完了したのは既存認定2配置の実Play検証であり、自由な方向別接続、大小ロケーション、biome遷移の実装は次工程である。


## 2026-10-08 異サイズ・複数方向接続の最小実装 B/J/D/C

承認済みの追加工程として、元B（384×640px）、C（512×384px）、D（640×512px）を保持し、B東→新規J西、J東→D西、D南→C北を接続する候補生成を追加した。J6unitを代表とし、4/8unitを同じ入力条件で比較した。従来S0/Eの生成処理と保存済みSceneを保持したまま、別の設定・メニュー・出力先を使う。森林全体のランダム生成、北東の新port素材、T分岐、biome遷移は今回の実装範囲に含めない。状態はユーザー確認待ちである。

### 床・接続面・素材の契約

Jの高さは384px、長さは256/384/512px。B東portの中心Y297pxとD西portの中心Y169pxを曲線で結ぶ。端の32px帯は高さ384px全域で元の受領済みB東/D西地面のRGBAを1px単位で保持する。両端の予約口64px、C北/D南の予約口96px、共有辺の長さと元coreの配置を固定した。内部の草肩と床外形が実際に絞られ曲がるため、床は長方形ではない。床形状を決めるfanは80px。全境界の接線連続性を保証する設計ではない。

BL原点の二値床マスクからfloor union、void Nav、64pxの外周Physics帯を同時に作る。土色の領域を歩行判定に使わない。J長4/6/8の外周BoxCollider2Dは230/387/471、void Nav矩形は171/221/252。元coreの16owners/12接地unitを保存し、Jには元B由来の岩2・草6を新規ownerとして配置した。Jの8owners中、接地unitは岩2、Physics bodyとNav障害矩形はともに0で、装飾である。全Mapは24owners/14接地unit、Heroを含めて15深度unitとなる。元coreの木・岩のbody/Nav/階層・Sprite・ローカル変換は保持した。

地面は64PPU、Point、非圧縮、FullRectで保存する。元地面の画素を1:1で選び、64pxパッチ・16px重なりの最小cutで草/土を組み、元の土草境界の細部を採用する。固定端帯の内側は同じ草/土分類の元画素だけを延長し、不規則cutで継ぐ。以前の直線的な色帯を弱め、試作で生じた土の突き出しを除いた。固定帯RGBA差分0、床外の不透明画素0、分類変更0、元palette外の新色0を4/6/8全版で照合した。地面textureやSpriteを伸縮・補間していない。Jの新規樹冠は0で、元Cの樹冠のはみ出しを保存する。

### 保存済み候補と再現

設定の親フォルダは `Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/NativeFlexibleConnectionPilot-v1-20261008`。`flexible-j6-config.json` が代表、`flexible-j4-config.json` と `flexible-j8-config.json` が比較用である。全て `Seed=0`、`SpawnLocationId=B`、既存Eの受領済み地面・素材admission設定を参照する。`ConnectorLengthPx` は384/256/512。現時点でseedが床外形をランダム化するわけではなく、固定外形の記録と決定的な地面パッチ選択に使われる。

保存済み出力の親フォルダは `Assets/Scenes/MapAuthoring/OverheadStudyA/NativeFlexibleConnectionPilot-v1-20261008`。

| 候補 | 最新フォルダ | 開くScene |
| --- | --- | --- |
| 代表J6 | `J-6u-Seed-0-6ff81ef83adf` | `NativeEntry.unity` |
| 比較J4 | `J-4u-Seed-0-cbce6d84b1c8` | `NativeEntry.unity` |
| 比較J8 | `J-8u-Seed-0-b5bb84212290` | `NativeEntry.unity` |

Unity 6000.4.3f1で代表の `NativeEntry.unity` を開きPlayすると、Bのspawnから通常Actor・人間入力・follow cameraを使う。同フォルダの `NativeMap.unity`、J ground PNG/Prefab、床/土mask、geometry recipe、resolved catalog、生成receiptが保存されている。旧試作フォルダは比較証跡として残しているので、上表の最新keyを使う。

再生成する場合は設定を複製し、`OutputRoot` を未使用の `Assets/Scenes/MapAuthoring/OverheadStudyA/NativeFlexibleConnectionPilot-v1-20261008-repro` などに変更する。EditorのPlayを停止し、開いているSceneを保存してから、その設定TextAssetを選択し `FantasyRoyale > Map Authoring > Overhead Study A > Generate Flexible Native Connection Pilot` を実行する。既存出力への上書きは拒否する。出力keyはgeometry/contentと生成地面RGBAとprop recipeから作り、地面処理の変更で古い候補に衝突しない。Prefab/外周Sprite/地面の実ファイルSHAは別のpresentation receiptに保存する。

実行証跡はworkspace `flexible-native-implementation-20261008/unity-art3-attempt1`。batch入口は新規 `FantasyRoyale.MapAuthoringKit.Editor.StudyANativeFlexibleBatchVerification.GenerateBatch` と `VerifyReloadBatch`、flagは `-studyFlexibleEvidence <absolute evidence folder>`。再読込には生成receiptが必要。GPU描画のため `-nographics` を付けない。Play試験は新規 `FantasyRoyale.Playtest.Tests.PlayMode.StudyANativeFlexibleConnectionsPlayPreviewDebugTests`、flagは `-studyFlexiblePlayEvidence <generation evidence folder>/play-attempt1`。この親に生成receiptを置く。`-runTests` には `-quit` を付けず、Editorの多重起動を避ける。

### 実生成・GPU・Playで確認した範囲

最新ART3を実Unityで生成して保存し、別PIDで再読込み、4/6/8全版の源素材・固定帯・実床mask・Nav・外周Collider・元ownerを再導出して照合した。Direct3D11による全体画像3枚と再読込画像3枚を保存した。通常Playは3/3 passed、通常follow cameraのGPU画像42枚（各1365×1024、ortho8）を保存した。各版21移動（B/J/D往復2、3接続面×3レーン×両方向18、C木前後1）と13接触（曲線上下12、J/C凹角1）、計63移動/39接触を実Actor/FixedUpdate/Rigidbody2D/Physics2Dで確認した。接触は代表点の検証であり、全境界上を実走したものではない。

batchは `Application.isFocused=false` のため、元Diagnosticのfocus guardを保持すると元入力readは0となる。試験専用の仮想Keyboardと元HumanCharacterMoveInputAdapterを通してActorに入力し、その後は通常FixedUpdate/Physics/cameraを使った。private callbackの反射呼出しや手動Physics simulationは使っていない。focus=trueのdesktop実キー・GameView操作は未確認である。音声Source/Listenerは0を確認し、試験中も音声を停止した。

C木の前後で実際にHeroを動かし、同じframeのHero表示/非表示GPU対照を保存した。独立画素比較は前1688px・後44pxで、樹冠による遮蔽と足元深度を確認した。J owner absent/restoredは装飾8ownerのgraphicsと接地groupの復元を検証するものであり、Jの非ゼロbody/Nav切替の証明ではない。既存S0/Eの元Play試験2/2も再実行し、元木の非ゼロ障害・橋・外周・水animation等の既存契約を保持した。最終EditModeは追加34件と既存67件、計101/101 passed。

全体EditMode画像の生成時/再読込時を比較すると、元C下部の同順位で重なる樹冠に5869 RGB画素差があり、J内の差は0、alpha差は0である。保存ファイルのSHAは一致するが全体GPU画像のbyte/pixel一致とは扱わない。元Cの足元同Y・EditMode同sort順位を保持した結果の局所的な描画順差であり、Playの通常sorterと木の前後は別に確認した。元coreの描画設計をこの工程で変更していない。

rootと独立reviewerが実画素を確認し、矩形の外形だけを曲がった道で装う状態から、実床が変化する接続へ進んだ。J8は曲がりが最も緩く、J6を代表候補、J4は短く砂時計状に絞られる比較候補とする。固定端の直線的なトーン差は弱まったが、草Sprite/textureの反復、長い外周と暗い床外による浮島感は残る。最小接続proofの通常表示は確認できたが、完全seamlessや森林全体の完成とは判定しない。初期目標2画像のHomePC受取りは正規Library経路で失敗しており、その目標との最終一致判断は保留する。

### 保護と引継ぎ

変更前7273ファイルのSHA台帳、元staged差分、59608701byteの元unstaged差分を保存した。復元ZIP `flexible-native-implementation-20261008/before-flexible-native.zip` は元175files/4213032bytes、SHA256 `672483717bcb8455f40878046884afe912a99ccd0f3ac4c5072859079b9cda1b`、CRC/全original SHAを確認済み。既存変更は本設計資料と実行計画の追記だけとし、新規6source/meta・専用設定/出力のみを追加した。元PNG/Prefab/meta、元S0/E、Packages/ProjectSettings、元Git stagedと資料以外のunstaged差分をSHA/rawで保護する。最終証跡と各パスはworkspace `final-handoff.json`、`preservation-result.json`、新規Play/Edit XML/ログ/独立review JSONに残す。push・外部公開・購入は行っていない。親側のtaskはユーザー確認待ちを維持する。
