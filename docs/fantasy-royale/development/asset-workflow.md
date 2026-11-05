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

デザイン案と施設の原画は`Tools/MapAuthoring/Inputs/`にある。生成した地図の出力は再生成できるのでGitへ入れない。

次はディスクにだけ残し、Gitへ入れない: `Assets/NemoBlocks/Scenes/`、`Assets/NemoBlocks/Generated/`、OverheadStudyAの素材とScene、`NemoValidation*/`、`Assets/_Recovery/`。新しいCloneにはないため、OverheadStudyAの素材を読むTestは失敗する。

Unity確認は、Repositoryではなく検証用Project`C:/Users/umiga/Documents/Codex/2026-10-12/task/nemo-visual-implementation/UnityVisualValidation/`で行う。出力を同Projectの`Assets/ForestLookSlice/`へ置き、`Tools/MapAuthoring/Unity/`の`.cs.txt`を拡張子`.cs`で配置する。Runnerは`Editor/`、`ForestLookWalker`は同Directoryの直下に置く。環境変数`FOREST_LOOK_OUT`へ結果の出力先を指定し、`-batchmode -projectPath <検証用Project> -executeMethod ForestLookSliceRunner.Run`で実行する。結果は`report.json`と撮影画像である。Sceneは`Assets/ForestLookSlice/ForestLookMap.unity`に保存され、Playすると矢印キーまたはWASDで移動できる。

## GraphicとCollision

- 見た目と移動判定を分離する。
- Map Objectは素材ごとの幹・接地点へ寄せたCollisionを使用し、表示Bounds全体を判定にしない。
- 詳細は[Map Authoring Kit](../design-docs/map-authoring/map-authoring-kit.md)を参照する。

