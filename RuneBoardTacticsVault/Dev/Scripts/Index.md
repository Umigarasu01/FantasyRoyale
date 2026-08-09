# C# スクリプト解説 Index

## Runtime / Gameplay Characters

- [CharacterHealth](Unity/Gameplay/Characters/CharacterHealth.md)
- [CharacterMoveCommand](Unity/Gameplay/Characters/CharacterMoveCommand.md)
- [CharacterActor2D](Unity/Gameplay/Characters/CharacterActor2D.md)
- [HumanCharacterMoveInputAdapter](Unity/Gameplay/Characters/HumanCharacterMoveInputAdapter.md)

## Runtime / Map Authoring Kit

- [MapSocketMarker](Unity/MapAuthoringKit/MapSocketMarker.md)
- [MapEventDefinition](Unity/MapAuthoringKit/MapEventDefinition.md)
- [HealingFountainEventDefinition](Unity/MapAuthoringKit/HealingFountainEventDefinition.md)
- [MapEventCatalog](Unity/MapAuthoringKit/MapEventCatalog.md)
- [MapObstacleVisualMarker](Unity/MapAuthoringKit/MapObstacleVisualMarker.md)
- [RoadConnectionRuleTile](Unity/MapAuthoringKit/RoadConnectionRuleTile.md)
- [GroundVariationTile](Unity/MapAuthoringKit/GroundVariationTile.md)

## Runtime / Gameplay Events

- [MapEventRuntime](Unity/Gameplay/Events/MapEventRuntime.md)
- [HealingFountainEventHandler](Unity/Gameplay/Events/HealingFountainEventHandler.md)
- [MapEventPoolDefinition](Unity/Gameplay/Events/MapEventPoolDefinition.md)
- [MapEventPlacementSelector](Unity/Gameplay/Events/MapEventPlacementSelector.md)

## Runtime / Playtest

- [BattleRoyaleExplorationPreviewDebug](Unity/Playtest/BattleRoyaleExplorationPreviewDebug.md)
- [PreviewDebugMapEventPresentation](Unity/Playtest/PreviewDebugMapEventPresentation.md)

## Editor / Map Authoring Kit

v4.3実装・検証済み。単一Production Atlas、v4.3 Catalog、205 named Sub-Sprite、4 Tilemapと3つの自由配置Rootを使う。移動PhysicsはMapCollision Layer上のCollisionTilemapと、木・森・岩などの幹・接地点CollisionBodyを正本とする。地面は8 Spriteの`GroundVariationTile`、Dirt / Stoneは全16 Mask各4差分を使う。96x72のBattle Royale Reference Sceneも生成済み。

- [GbaMapSpritePostprocessor](Editor/MapAuthoringKit/GbaMapSpritePostprocessor.md)
- [MapAuthoringValidator](Editor/MapAuthoringKit/MapAuthoringValidator.md)
- [MapSocketMarkerEditor](Editor/MapAuthoringKit/MapSocketMarkerEditor.md)
- [GbaForestMapAuthoringSceneBuilder](Editor/MapAuthoringKit/GbaForestMapAuthoringSceneBuilder.md)
- [MapAuthoringKitBuilder](Editor/MapAuthoringKit/MapAuthoringKitBuilder.md)
- [GbaForestBattleRoyaleMapBuilder](Editor/MapAuthoringKit/GbaForestBattleRoyaleMapBuilder.md)
- [MapRoadPathRasterizer](Editor/MapAuthoringKit/MapRoadPathRasterizer.md)
- [MapAuthoringAssetContractTests](Editor/MapAuthoringKit/MapAuthoringAssetContractTests.md)
- [MapAuthoringValidatorTests](Editor/MapAuthoringKit/MapAuthoringValidatorTests.md)

## Editor / Gameplay Events

- [MapEventRuntimeTests](Editor/Gameplay/Events/MapEventRuntimeTests.md)

## Editor / Gameplay Characters

- [CharacterHealthTests](Editor/Gameplay/Characters/CharacterHealthTests.md)
- [CharacterMoveCommandTests](Editor/Gameplay/Characters/CharacterMoveCommandTests.md)

## Editor / Playtest

- [BattleRoyaleExplorationPreviewDebugSceneBuilder](Editor/Playtest/BattleRoyaleExplorationPreviewDebugSceneBuilder.md)
- [BattleRoyaleExplorationPreviewDebugPlayModeTests](Editor/Playtest/BattleRoyaleExplorationPreviewDebugPlayModeTests.md)

探索確認用PlayMode Suite: 4 passed / 0 failed / 0 skipped。Reference Map加算読込、人間入力Adapterから共通Move Command、本番Character Actorによる移動、実MapCollision衝突、撃破時停止、Camera追従、足元Y描画順、幹中央と樹冠側の判定分離、本番Character Healthを使うRegistry経由の回復、撃破中の通常回復拒否、Presentation Cue、OneShot状態を検証する。

EditMode全Suite: 83 passed / 0 failed / 0 skipped。Character Healthの初期化、Clamp、撃破、通常回復非復活、Move Commandのアナログ保持・斜めClamp・不正値拒否と、Event Poolの純C#抽選、Pool Asset、PoolCandidate Socket契約を含む。

dotnet生成済みC# 12 Project: 0 warning / 0 error。Gameplay Characters 3 Project、Map Authoring、Gameplay Events、Playtest、既定Assemblyを含む。

履歴: v3の最終EditMode Suiteは18 passed、v4は23 passed、v4.1は39 passed、v4.2は41 passed。いずれも現行v4.3の検証結果として流用しない。

旧FR_Map、Exploration、BiomeAtlasMapSceneBuilderは2026-07-10の方式変更で削除した。履歴はGit snapshot ce2d1d3以前を参照する。
