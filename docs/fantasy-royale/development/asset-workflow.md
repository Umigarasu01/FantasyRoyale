# Asset導入・生成Workflow

## 外部Asset

- Unity Asset Storeなどの外部AssetをProjectへ取り込む前に、Asset名、用途、提供元、License上の注意点をユーザーへ共有して確認を取る。
- 採用後は用途に応じて`Assets/Art/`、`Assets/Audio/`、`Assets/Prefabs/`などへ整理する。
- Licenseと提供元は[外部Asset候補・導入記録](assets/external-assets.md)へ残す。

## AI生成Asset

- 原則として`Assets/Art/Generated/`または用途別の`Assets/Art/`配下へ保存する。
- 初期導入ではPlaceholderとして扱い、正式素材へ差し替え可能な参照構造を保つ。
- 採用済みAssetを保護し、候補生成とQAは別Fileで行う。採用判断前に既存AssetやScriptableObject参照を置き換えない。
- Unityから参照する生成AssetはGit管理対象に含める。
- 用途、保存先、生成日、生成方法、Prompt概要、Hashは[生成Asset台帳](assets/generated-assets.md)へ記録する。

## 森マップの生成手順

`Tools/MapAuthoring/`の3つのScriptを順に使用する。Python 3.10とPillowを使い、追加Packageは不要。

1. 画像生成: `python generate_forest_asset.py <asset-name> Assets/Art/Generated/ForestLook`。実行のたびに課金され、同じ画素は再生成できない。既存Fileへの上書きは拒否する。APIキーはユーザー環境変数`OPENAI_IMAGE_API_KEY`から読み、表示や保存はしない。
2. 部品化: `python build_forest_parts.py`。生成シートを切り出し、`Parts/`へ部品と`parts.json`を書き出す。
3. 地図生成: `Tools/MapAuthoring/`で`python build_forest_map.py <出力先> [--area x0 x1 y0 y1]`を実行する。省略時は全域、`--area`で一部だけを生成する。全域は2〜4分かかる。道、川、施設の位置は`forest_layout.py`、デザイン画の画素から地面を合成する処理は`forest_texture.py`にある。

デザイン案は`Tools/MapAuthoring/Inputs/`にある。同じ場所の施設の原画は旧素材で、地図生成では使わない。生成した地図の出力は再生成できるのでGitへ入れない。

次はディスクにだけ残し、Gitへ入れない: `Assets/NemoBlocks/Scenes/`、`Assets/NemoBlocks/Generated/`、OverheadStudyAの素材とScene、`NemoValidation*/`、`Assets/_Recovery/`。新しいCloneにはないため、OverheadStudyAの素材を読むTestは失敗する。

Unity確認は、Repositoryではなく検証用Project`C:/Users/umiga/Documents/Codex/2026-10-12/task/nemo-visual-implementation/UnityVisualValidation/`で行う。出力を同Projectの`Assets/ForestLookSlice/`へ置き、`Tools/MapAuthoring/Unity/`の`.cs.txt`を拡張子`.cs`で配置する。Runnerは`Editor/`、`ForestLookWalker`は同Directoryの直下に置く。環境変数`FOREST_LOOK_OUT`へ結果の出力先を指定し、`-batchmode -projectPath <検証用Project> -executeMethod ForestLookSliceRunner.Run`で実行する。結果は`report.json`と撮影画像である。Sceneは`Assets/ForestLookSlice/ForestLookMap.unity`に保存され、Playすると矢印キーまたはWASDで移動できる。

## 森マップを走るSceneの作り方

1. 騎士のコマ: `Tools/MapAuthoring/`で`python build_hero_frames.py [--preview <gif>]`を実行する。生成シートから走り20コマと立ち姿5枚を書き出す。出力は決まった結果になる。
2. 地図の配置: 地図出力の地面画像、`objects.json`、`collision.json`、`Parts/`を`Assets/ForestMapPreview/`へ置く。
3. Scene生成: Unityで`FantasyRoyale/Playtest/Build Forest Map Preview Debug Scene`を実行する。

`Assets/ForestMapPreview/`はSceneを含めてGitへ入れない。新しいCloneでは上の手順で作り直す。

## 森マップの設置物を足す

宝箱と敵はUnityが置く絵なので、`Tools/MapAuthoring/`で`python forest_objects.py`を実行して書き出す。

設置物の絵は、草地の切り抜きと既存のシートを参照にして、3行3列のシートで生成する。`Tools/MapAuthoring/forest_objects.py`の一覧へ、シートの位置、地図での幅、足元、判定を足す。大きさは高さ72pxの騎士を基準にする。配置は`forest_layout.py`の`FACILITIES`で持つ。

## 装備の見た目を足す

体のアニメーションは全身セットごとに素手の1組だけで、装備は部品として手と背中へ重ねる。体は`Hero/anchors.json`にコマごとの手と背中の位置、武器の角度を持ち、部品はその位置へ毎Frame付いて動く。新しい装備に走りや攻撃のコマは要らない。

部品は形で2種類に分ける。

- どの向きからも形が同じ物（剣など）: 絵を1枚描く。ツールが24方向へ回転させ、全ての姿勢で使える。
- 向きで見える面が変わる物（盾、矢筒など）: 5方向の絵を描く。

1. `hero-gear-01.png`を参照にして、部品シートへ行を足すか、同じ形のシートを生成する。列は正面、斜め前、横、斜め後ろ、背面の順である。
2. `Tools/MapAuthoring/build_hero_frames.py`の`ROTATED_ITEMS`か`GEAR_ITEMS`へ、付ける位置、高さ、持つ点を足して実行する。回転は大きな絵のまま行ってから縮小するので、線が荒れにくい。
3. Scene Builderを実行すると部品の定義Assetができる。`CharacterEquipmentDefinition`の`Gear`へ設定する。

攻撃の体のコマは手で見た手の位置を`ATTACK_HANDS`に持つ。走りと立ち姿の利き手の位置と角度は、ツールが`hero-run-*`と`hero-idle-*`の剣入りの絵と素手の絵の差から測る。走りの角度は向きごとに1つへ固定し、武器は手の動きだけについていく。反対の手は素手の絵から測る。

拳で握る部品は、拳の絵を上へ重ねる。柄が拳の中を通って見え、持っているように見える。走りと立ち姿の利き手の拳は剣入りの絵の握った手から、それ以外は体の絵から切り出す。拳の絵はツールが`Hand/`へ全コマ分を書き出す。盾は拳を隠すので重ねない。

動きの絵は武器ごとに作らず、`CharacterActionDefinition`の`Motion`で型を選ぶ。今の型は`Swing`（利き手の武器を振る）と`Shoot`（弓で射る）である。武器の差は範囲や速さの数値で出す。`Shoot`の動きの絵はまだないので、弓は走りと立ち姿のまま撃つ。

部品が体へ重ねられることは機械的に確認する。全ての絵、体と同じPPU、全コマの手の位置が揃っていなければScene Builderが止まり、EditMode Testの`CharacterSpriteContractTests`が失敗する。`FindEquipmentProblems`は全身セットと装備の組み合わせを調べ、振る動きのコマがない体と、型に合わない部品も止める。

## 全身セットを足す

防具は全身セットで増やす。全身セットは騎士の体の絵の着替えで、同じ姿勢のシート一式を持つ。手の位置、武器の角度、拳は騎士の値を共有するので、どの武器もそのまま持てる。

作業量は「セットの数×シートの枚数」と「武器の数」の足し算で、武器の組み合わせの数は入らない。1セットのシートは走りと立ち姿の4枚と、動きの絵がある型ごとに2枚である。新しい型を足すときは、全てのセットへ2枚ずつ足す。

1. 各`hero-body-*.png`を参照画像にしてOpenAI Image APIのeditで着替えさせ、`hero-set-<name>-*.png`として保存する。手足と頭の位置を変えないこと、手袋は同じ茶色の革のままにすること、武器を描かないことをPromptで指定する。
2. `Tools/MapAuthoring/build_hero_frames.py`の`ARMOUR_SETS`へ名前を足して実行する。騎士のコマと同じ位置へ切り出し、`Sets/<name>/`へ書き出す。
3. ツールは騎士の輪郭からの外れを検査する。輪郭の重なりが85%未満か、手の周りの輪郭が2 pixelを超えて動くと止まる。体に重なる手は輪郭で測れないので、実画面で確認する。
4. `ForestMapPreviewDebugSceneBuilder`へ定義Assetの保存先とFolderを足してScene Builderを実行する。

手袋の色を変えるセットは、騎士の拳の絵が合わないので未対応である。

## GraphicとCollision

- 見た目と移動判定を分離する。
- Map Objectは素材ごとの幹・接地点へ寄せたCollisionを使用し、表示Bounds全体を判定にしない。
- 詳細は[Map Authoring Kit](../design-docs/map-authoring/map-authoring-kit.md)を参照する。

