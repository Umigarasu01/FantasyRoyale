# FantasyRoyale Vault Index

> **Legacy:** 旧`RuneBoardTacticsVault/`の入口を移行履歴として保存したもの。現行の入口は[FantasyRoyale Knowledge Base](../../index.md)。

このDocumentは、移行前の企画、仕様、実装方針へのLink構成を保存する。

## Game Design

- [FantasyRoyale 企画メモ](../../product-specs/game-concept.md)
- [Prototype Direction](../../product-specs/prototype-direction.md)

## Art

- [Art Direction](../../product-specs/art/art-direction.md)
- [Tileset Plan](../../product-specs/art/tileset-plan.md)
- [Object Plan](../../product-specs/art/object-plan.md)
- [Map Plan](../../product-specs/art/map-plan.md)

## Architecture

現行のマップ制作方式はMap Authoring Kit v4.3。指定画像をDesign Masterとし、205 Spriteの単一Production Atlas、4 Tilemap、座標Hashで差分を選ぶ地面Tileと道路端、自由配置Decoration / Obstacle VisualをUnity標準機能で編集する。表示Rootと判定を分離し、水・崖は不可視CollisionTilemap、木・森・岩などは幹・接地点へ寄せたPrefab内CollisionBodyを使う。Shaderや独自Map Editorには依存しない。Sample / 96x72 Reference再構築と自由配置EventSocketを実装済み。Event RuntimeはRegistry、Handler、共通実行結果、Presenterへ分離し、回復の泉を最初の縦切りとして接続した。EventSocketはPool候補と固定配置を明示分離し、ScriptableObject Poolと純C#のSeed抽選で6候補から3地点を有効化できる。Milestone 2は純C#の本番`CharacterHealth`と`CharacterMoveCommand`、Unity側`CharacterActor2D`、人間入力Adapterまで実装し、通常回復非復活と撃破時移動停止を含む本番経路へPreviewを移行した。全83件のEditMode Testと4件のPlayMode Testで検証済み。

- [Scene とクラス構成](../../design-docs/scene-and-class-structure.md)
- [Exploration Preview Debug Scene](../../design-docs/scenes/exploration-preview-debug-scene.md)
- [Map Authoring Kit v4.3](../../design-docs/map-authoring/map-authoring-kit.md)
- [Forest Production Atlas Contract v4.3](../../design-docs/map-authoring/forest-production-atlas-contract.md)
- [GBA Forest Map Authoring Scene](../../design-docs/scenes/gba-forest-map-authoring-scene.md)

### 旧ドラフト

- [廃案: Independent GBA Map Creation Tool Design](map-creation-tool-design.md)
- [保留: Map Data Format](map-data-format.md)

## Dev

- [C# スクリプト解説 Index](../../development/code-reference/index.md)
- [生成素材台帳](../../development/assets/generated-assets.md)
- [外部素材候補](../../development/assets/external-assets.md)

## Roadmap

- [進行中作業](current-work-history.md)
- [実装状況](implementation-status-history.md)
- [Prototype Milestones](../../product-specs/prototype-milestones.md)
