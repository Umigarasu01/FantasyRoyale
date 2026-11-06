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

騎士の体のアニメーションは素手の1組だけで、装備は部品として手と背中へ重ねる。体は`Hero/anchors.json`にコマごとの手と背中の位置を持ち、部品はその位置へ毎Frame付いて動く。新しい装備に走りのコマは要らず、5方向の部品の絵だけで足せる。

1. `hero-gear-01.png`を参照にして、5列の部品シートへ行を足すか、同じ形のシートを生成する。列は正面、斜め前、横、斜め後ろ、背面の順である。
2. `Tools/MapAuthoring/build_hero_frames.py`の`GEAR_ITEMS`へ、付ける位置、高さ、持つ点を足して実行する。
3. Scene Builderを実行すると部品の定義Assetができる。`CharacterEquipmentDefinition`の`Gear`へ設定する。

部品が体へ重ねられることは機械的に確認する。全方向の絵、体と同じPPU、全コマの手の位置が揃っていなければScene Builderが止まり、EditMode Testの`CharacterSpriteContractTests`が失敗する。別の体を足すときも、同じ`anchors.json`を作れば全ての部品がそのまま使える。

## GraphicとCollision

- 見た目と移動判定を分離する。
- Map Objectは素材ごとの幹・接地点へ寄せたCollisionを使用し、表示Bounds全体を判定にしない。
- 詳細は[Map Authoring Kit](../design-docs/map-authoring/map-authoring-kit.md)を参照する。

