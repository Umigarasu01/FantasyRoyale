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

v4.3実装・検証済み。単一Production Atlas、v4.3 Catalog、205 named Sub-Sprite、4 Tilemapと3つの自由配置Rootを使う。移動PhysicsはMapCollision Layer上のCollisionTilemapと、木・森・岩などの幹・接地点CollisionBodyを正本とする。地面は8 Spriteの`GroundVariationTile`、Dirt / Stoneは全16 Mask各4差分を使う。96x72のBattle Royale Reference Sceneも生成済み。

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

EditMode全Suite: 83 passed / 0 failed / 0 skipped。Character Healthの初期化、Clamp、撃破、通常回復非復活、Move Commandのアナログ保持・斜めClamp・不正値拒否と、Event Poolの純C#抽選、Pool Asset、PoolCandidate Socket契約を含む。

dotnet生成済みC# 12 Project: 0 warning / 0 error。Gameplay Characters 3 Project、Map Authoring、Gameplay Events、Playtest、既定Assemblyを含む。

履歴: v3の最終EditMode Suiteは18 passed、v4は23 passed、v4.1は39 passed、v4.2は41 passed。いずれも現行v4.3の検証結果として流用しない。

旧FR_Map、Exploration、BiomeAtlasMapSceneBuilderは2026-07-10の方式変更で削除した。履歴はGit snapshot ce2d1d3以前を参照する。
