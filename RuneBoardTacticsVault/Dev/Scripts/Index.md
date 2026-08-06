# C# スクリプト解説 Index

## Runtime / Map Authoring Kit

- [MapSocketMarker](Unity/MapAuthoringKit/MapSocketMarker.md)
- [MapEventDefinition](Unity/MapAuthoringKit/MapEventDefinition.md)
- [HealingFountainEventDefinition](Unity/MapAuthoringKit/HealingFountainEventDefinition.md)
- [MapEventCatalog](Unity/MapAuthoringKit/MapEventCatalog.md)
- [MapObstacleVisualMarker](Unity/MapAuthoringKit/MapObstacleVisualMarker.md)
- [RoadConnectionRuleTile](Unity/MapAuthoringKit/RoadConnectionRuleTile.md)
- [GroundVariationTile](Unity/MapAuthoringKit/GroundVariationTile.md)

## Runtime / Playtest

- [BattleRoyaleExplorationPreviewDebug](Unity/Playtest/BattleRoyaleExplorationPreviewDebug.md)

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

## Editor / Playtest

- [BattleRoyaleExplorationPreviewDebugSceneBuilder](Editor/Playtest/BattleRoyaleExplorationPreviewDebugSceneBuilder.md)
- [BattleRoyaleExplorationPreviewDebugPlayModeTests](Editor/Playtest/BattleRoyaleExplorationPreviewDebugPlayModeTests.md)

探索確認用PlayMode Suite: 4 passed / 0 failed / 0 skipped。Reference Map加算読込、WASD入力、移動、実MapCollision衝突、Camera追従、足元Y描画順、幹中央と樹冠側の判定分離、E操作による回復とOneShot状態を検証する。

v4.3 / Event Socket操作 / Battle Royale Reference EditMode Suite: 61 passed / 0 failed / 0 skipped。

dotnet `Assembly-CSharp-Editor` / `Editor.Tests`: 0 warning / 0 error。

履歴: v3の最終EditMode Suiteは18 passed、v4は23 passed、v4.1は39 passed、v4.2は41 passed。いずれも現行v4.3の検証結果として流用しない。

旧FR_Map、Exploration、BiomeAtlasMapSceneBuilderは2026-07-10の方式変更で削除した。履歴はGit snapshot ce2d1d3以前を参照する。
