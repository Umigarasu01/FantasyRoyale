# FantasyRoyale Knowledge Base

このDirectoryをFantasyRoyaleのProject KnowledgeとSystem of Recordとして扱う。実装作業ではTaskに必要なDocumentだけを、このIndexから段階的に参照する。

## 現在地

- Map Authoring Kit v4.3、96x72森林Reference Map、EventSocket、回復の泉、Event Pool抽選を実装・検証済み。
- Character基盤は`CharacterHealth`、`CharacterMoveCommand`、`CharacterActor2D`、人間入力Adapterまで実装済み。
- 次のDecision Gateは通常攻撃・装備Action基盤。詳細は[Character・戦闘基盤ExecPlan](exec-plans/active/combat-foundation.md)を参照する。

## Product Specs

- [ゲーム企画](product-specs/game-concept.md)
- [Prototype方針](product-specs/prototype-direction.md)
- [Prototype Milestones](product-specs/prototype-milestones.md)
- [Art Direction](product-specs/art/art-direction.md)
- [Map Plan](product-specs/art/map-plan.md)
- [Object Plan](product-specs/art/object-plan.md)
- [Tileset Plan](product-specs/art/tileset-plan.md)

## Design Docs

- [Architecture原則](design-docs/architecture-principles.md)
- [SceneとClass構成](design-docs/scene-and-class-structure.md)
- [Map Authoring Kit](design-docs/map-authoring/map-authoring-kit.md)
- [Forest Production Atlas Contract](design-docs/map-authoring/forest-production-atlas-contract.md)
- [GBA Forest Map Authoring Scene](design-docs/scenes/gba-forest-map-authoring-scene.md)
- [Exploration Preview Debug Scene](design-docs/scenes/exploration-preview-debug-scene.md)

## Exec Plans

- 規約: [PLANS.md](PLANS.md)
- 進行中: [Character・戦闘基盤](exec-plans/active/combat-foundation.md)
- 完了済み: [Knowledge Base移行](exec-plans/completed/knowledge-base-migration.md)

## Development

- [Unity開発Workflow](development/unity-workflow.md)
- [Build・Test手順](development/testing.md)
- [Coding規約](development/coding-conventions.md)
- [Asset導入・生成Workflow](development/asset-workflow.md)
- [C#コード参照Index](development/code-reference/index.md)
- [生成Asset台帳](development/assets/generated-assets.md)
- [外部Asset候補・導入記録](development/assets/external-assets.md)

## References / Legacy

移行前の履歴、廃案、保留設計は[旧Vault Index](references/legacy/legacy-vault-index.md)から参照できる。Legacy Documentは現行仕様のSource of Truthとして使用しない。
