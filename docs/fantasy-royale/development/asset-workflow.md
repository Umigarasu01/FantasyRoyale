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

## GraphicとCollision

- 見た目と移動判定を分離する。
- Map Objectは素材ごとの幹・接地点へ寄せたCollisionを使用し、表示Bounds全体を判定にしない。
- 詳細は[Map Authoring Kit](../design-docs/map-authoring/map-authoring-kit.md)を参照する。

