# FantasyRoyale Knowledge Base

このDirectoryをFantasyRoyaleのProject KnowledgeとSystem of Recordとして扱う。実装作業ではTaskに必要なDocumentだけを、このIndexから段階的に参照する。

## 現在地

- 2026-11-05の決定で、ベースマップを固定し、その上のオブジェクトを試合ごとにランダムにする。当面は森マップだけを対象とする。全体構成を作り直した全域マップを検証用Projectで確認し、所有者が構成と画作りの基準として承認した。本番Sceneへは未組込み。[Map Plan](product-specs/art/map-plan.md)と[森マップの画作り基準](exec-plans/completed/forest-look-lock.md)を参照する。
- 2026-10-09の設計方針は、全体Mapを暫定24×18U（1U=6 Unity単位、144×108 Unity単位）で進め、育成中は脱落せず、妨害しながらビルドを完成させて最後のバトルへ進むシティトライアル風の流れとする。エリア収縮は当面採用なし。人数・各フェーズの時間・妨害ペナルティは未確定で、この進行や全域生成の実装済みを意味しない。[Prototype方針](product-specs/prototype-direction.md)と[Map Plan](product-specs/art/map-plan.md)を参照する。

- 森林マップの空間構成改善で、中央の不規則な広場、曲がる周回路、水路と2本の橋、段丘、森縁を生成する。代表4SeedのScene/Physics検査と修正後描画を確認し、既存ReferenceのPath/GUIDを維持してSeed20261003へ移行した。最終EditMode113件 / PlayMode4件と実Gameplayの歩行・Camera追従を確認済み。記録は[森林素材再制作Kitの現在の構成改善](design-docs/map-authoring/forest-restart-kit.md#現在の構成改善)と[構成改善ExecPlan](exec-plans/active/forest-composition-improvement.md)を参照する。指定Library画像のHomePC受取りは未完了で、見た目の最終一致判断と残る輪郭・岸・段差の改善は継続課題である。
- 前段階のForestRestartではコンセプトv005基準の素材を制作し、48x32・40 Seed論理Plan / 4 Sceneを検証した。旧素材135 File撤去、EditMode 98件 / PlayMode 4件成功はその時点の結果である。[森林素材再制作Kit](design-docs/map-authoring/forest-restart-kit.md)に履歴を保持する。
- EventSocket、回復の泉、Event Pool抽選とCharacter基盤は引き継ぐ。
- Character基盤は`CharacterHealth`、`CharacterMoveCommand`、`CharacterActor2D`、人間入力Adapterまで実装済み。
- 通常攻撃と装備の差し替えを実装し、探索Previewの的で確認できる。Player同士の被弾は育成中のHP 0の扱いが決まるまで保留する。詳細は[Character・戦闘基盤ExecPlan](exec-plans/active/combat-foundation.md)を参照する。

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
- [森林素材再制作Kit](design-docs/map-authoring/forest-restart-kit.md)
- [Forest Production Atlas Contract](design-docs/map-authoring/forest-production-atlas-contract.md)
- [GBA Forest Map Authoring Scene](design-docs/scenes/gba-forest-map-authoring-scene.md)
- [Exploration Preview Debug Scene](design-docs/scenes/exploration-preview-debug-scene.md)
- [Forest Map Preview Debug Scene](design-docs/scenes/forest-map-preview-debug-scene.md)

## Exec Plans

- 規約: [PLANS.md](PLANS.md)
- 進行中: [Character・戦闘基盤](exec-plans/active/combat-foundation.md)
- 進行中: [森林マップの空間構成改善](exec-plans/active/forest-composition-improvement.md)
- 完了済み: [Knowledge Base移行](exec-plans/completed/knowledge-base-migration.md)
- 完了済み: [森林素材の再制作と自動配置検証](exec-plans/completed/forest-art-restart.md)
- 完了済み: [森マップの画作り基準](exec-plans/completed/forest-look-lock.md)
- 終了: [Nemo ビジュアル改善](exec-plans/completed/nemo-visual-finish.md)
- 終了: [Nemo 外周の森と通行輪郭](exec-plans/completed/nemo-outer-forest-and-boundaries.md)
- 終了: [A基準の俯瞰2D一画面自動構成Study](exec-plans/completed/overhead-study-a.md)

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

- [Unity CLI MCP導入・接続確認](development/unity-cli-mcp.md)


- [Nemo modular random map](design-docs/scenes/nemo-modular-random-map.md): 区画表示と歩行領域を分けた Standard 生成 Scene。
