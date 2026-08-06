# FantasyRoyale Vault Index

このVaultは、ファンタジー2Dパーティバトロワの企画、仕様、実装方針を整理するための正本置き場。

## Game Design

- [[GameDesign/FantasyRoyaleConcept|FantasyRoyale 企画メモ]]
- [[GameDesign/PrototypeDirection|Prototype Direction]]

## Art

- [[Art/ArtDirection|Art Direction]]
- [[Art/TilesetPlan|Tileset Plan]]
- [[Art/ObjectPlan|Object Plan]]
- [[Art/MapPlan|Map Plan]]

## Architecture

現行のマップ制作方式はMap Authoring Kit v4.3。指定画像をDesign Masterとし、205 Spriteの単一Production Atlas、4 Tilemap、座標Hashで差分を選ぶ地面Tileと道路端、自由配置Decoration / Obstacle VisualをUnity標準機能で編集する。表示Rootと判定を分離し、水・崖は不可視CollisionTilemap、木・森・岩などは幹・接地点へ寄せたPrefab内CollisionBodyを使う。Shaderや独自Map Editorには依存しない。Sample / 96x72 Reference再構築、自由配置EventSocket、回復の泉まで実装し、61件のEditMode Testで検証済み。別Playtest SceneからのWASD移動、MapCollision、Camera追従、幹と樹冠の判定分離、E操作による回復とOneShot状態を4件のPlayMode Testで検証済み。

- [[Architecture/SceneClassStructure|Scene とクラス構成]]
- [[Architecture/Scenes/PrototypeSoloScene|Exploration Preview Debug Scene]]
- [[Architecture/MapAuthoringKit|Map Authoring Kit v4.3]]
- [[Architecture/ForestProductionAtlasContract|Forest Production Atlas Contract v4.3]]
- [[Architecture/Scenes/GbaForestMapAuthoringScene|GBA Forest Map Authoring Scene]]

### 旧ドラフト

- [[Architecture/MapCreationToolDesign|廃案: Independent GBA Map Creation Tool Design]]
- [[Architecture/MapDataFormat|保留: Map Data Format]]

## Dev

- [[Dev/Scripts/Index|C# スクリプト解説 Index]]
- [[Dev/Assets/GeneratedAssets|生成素材台帳]]
- [[Dev/Assets/ExternalAssets|外部素材候補]]

## Roadmap

- [[Roadmap/CurrentWork|進行中作業]]
- [[Roadmap/ImplementationStatus|実装状況]]
- [[Roadmap/PrototypeMilestones|Prototype Milestones]]
