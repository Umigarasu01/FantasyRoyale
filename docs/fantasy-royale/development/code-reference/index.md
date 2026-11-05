# C#コード参照 Index

既存実装の入口と、移行前に作成された詳細解説をまとめる。新しいC# Fileごとに解説Documentを増やすことは必須ではない。DurableなBehaviorやArchitectureはProduct Spec / Design Docを正とする。

## Runtime / Gameplay Characters

- [CharacterHealth](gameplay/characters/character-health.md)
- [CharacterMoveCommand](gameplay/characters/character-move-command.md)
- [CharacterActor2D](gameplay/characters/character-actor-2d.md)
- [HumanCharacterMoveInputAdapter](gameplay/characters/human-character-move-input-adapter.md)

## Runtime / Map Authoring Kit

- [MapSocketMarker](map-authoring/map-socket-marker.md)
- [MapEventDefinition](map-authoring/map-event-definition.md)
- [HealingFountainEventDefinition](map-authoring/healing-fountain-event-definition.md)
- [MapEventCatalog](map-authoring/map-event-catalog.md)
- [MapObstacleVisualMarker](map-authoring/map-obstacle-visual-marker.md)
- [RoadConnectionRuleTile](map-authoring/road-connection-rule-tile.md)
- [GroundVariationTile](map-authoring/ground-variation-tile.md)

## Runtime / Gameplay Events

- [MapEventRuntime](gameplay/events/map-event-runtime.md)
- [HealingFountainEventHandler](gameplay/events/healing-fountain-event-handler.md)
- [MapEventPoolDefinition](gameplay/events/map-event-pool-definition.md)
- [MapEventPlacementSelector](gameplay/events/map-event-placement-selector.md)

## Runtime / Playtest

- [BattleRoyaleExplorationPreviewDebug](playtest/battle-royale-exploration-preview-debug.md)
- [PreviewDebugMapEventPresentation](playtest/preview-debug-map-event-presentation.md)

## Editor / Map Authoring Kit

2026-10-03の仕切り直しにより、現行実装は[森林素材再制作Kit](../../design-docs/map-authoring/forest-restart-kit.md)へ移行した。`ForestRestartAssetBuilder`が76Sprite、3Tile、4Prefabを生成し、`ForestRestartLayout`が48x32のSeed付きPlanを検査する。`ForestRestartLayoutTests`は同Seed再現性、異Seed地形差分、複数Seedの到達・占有、不正Plan検出を確認する。40論理Plan、4保存Scene、8QA画像を生成し、旧素材は撤去済み。本番の固定Map + ランダム配置方針と、Unity Scene / MapCollisionを正本とする責務は維持する。

- [ForestRestartAssetBuilder / ForestRestartLayout / ForestRestartLayoutTests](../../design-docs/map-authoring/forest-restart-kit.md)
- Source: `Assets/Scripts/MapAuthoringKit/Editor/ForestRestartAssetBuilder.cs`、`ForestRestartLayout.cs`、`Assets/Scripts/MapAuthoringKit/Tests/Editor/ForestRestartLayoutTests.cs`。

以下の既存詳細ノートは冒頭で現行責務を案内し、旧v4.3のAtlas205Sprite・96x72Map・過去の成功数を履歴として保持する。旧Builder2点は新Builderへ委譲する互換入口であり、旧生成処理へ戻らない。

- [GbaMapSpritePostprocessor](map-authoring/gba-map-sprite-postprocessor.md)
- [MapAuthoringValidator](map-authoring/map-authoring-validator.md)
- [MapSocketMarkerEditor](map-authoring/map-socket-marker-editor.md)
- [GbaForestMapAuthoringSceneBuilder](map-authoring/gba-forest-map-authoring-scene-builder.md)
- [MapAuthoringKitBuilder](map-authoring/map-authoring-kit-builder.md)
- [GbaForestBattleRoyaleMapBuilder](map-authoring/gba-forest-battle-royale-map-builder.md)
- [MapRoadPathRasterizer](map-authoring/map-road-path-rasterizer.md)
- [MapAuthoringAssetContractTests](map-authoring/map-authoring-asset-contract-tests.md)
- [MapAuthoringValidatorTests](map-authoring/map-authoring-validator-tests.md)

## Editor / Gameplay Events

- [MapEventRuntimeTests](gameplay/events/map-event-runtime-tests.md)

## Editor / Gameplay Characters

- [CharacterHealthTests](gameplay/characters/character-health-tests.md)
- [CharacterMoveCommandTests](gameplay/characters/character-move-command-tests.md)

## Editor / Playtest

- [BattleRoyaleExplorationPreviewDebugSceneBuilder](playtest/battle-royale-exploration-preview-debug-scene-builder.md)
- [BattleRoyaleExplorationPreviewDebugPlayModeTests](playtest/battle-royale-exploration-preview-debug-play-mode-tests.md)

探索確認用PlayMode Suite: 4 passed / 0 failed / 0 skipped。Reference Map加算読込、人間入力Adapterから共通Move Command、本番Character Actorによる移動、実MapCollision衝突、撃破時停止、Camera追従、足元Y描画順、幹中央と樹冠側の判定分離、本番Character Healthを使うRegistry経由の回復、撃破中の通常回復拒否、Presentation Cue、OneShot状態を検証する。

2026-10-03 ForestRestart移行後のEditMode全Suite: 98 passed / 0 failed / 0 skipped。新素材の実Pixel / Import / Scene契約、Seed付きPlanの再現性・到達性に加え、Character Healthの初期化、Clamp、撃破、通常回復非復活、Move Commandのアナログ保持・斜めClamp・不正値拒否と、Event Poolの純C#抽選、Pool Asset、PoolCandidate Socket契約を含む。

履歴: 移行前のdotnet生成済みC# 12 Projectは0 warning / 0 errorだった。Gameplay Characters 3 Project、Map Authoring、Gameplay Events、Playtest、既定Assemblyを含む。この旧結果を今回のdotnet再Build結果として流用しない。

履歴: v3の最終EditMode Suiteは18 passed、v4は23 passed、v4.1は39 passed、v4.2は41 passed、v4.3のGameplayを含む全Suiteは83 passed。いずれも現行ForestRestartの検証結果として流用しない。

旧FR_Map、Exploration、BiomeAtlasMapSceneBuilderは2026-07-10の方式変更で削除した。履歴はGit snapshot ce2d1d3以前を参照する。
