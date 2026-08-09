# GbaForestBattleRoyaleMapBuilder

## 役割

`GbaForestBattleRoyaleMapBuilder`は、既存のMap Authoring Kitだけを使い、96x72の森・草原バトロワ基準Mapを決定的に再生成するEditor専用Builder。

30x20の`GbaForestMapSample`は素材とRuleTileの回帰確認用として維持し、本Builderは別Sceneの`GbaForestBattleRoyaleReference`だけを更新する。

## 公開入口

- `BuildReferenceMap()`
  - Menu: `FantasyRoyale/Map Authoring/Build Battle Royale Reference Map`
  - Unity batchmodeの`-executeMethod`からも実行できる。
  - 固定生成計画を構築・検証し、Template複製へ一括反映してSceneを保存する。
- `CaptureReferencePreviews()`
  - Menu: `FantasyRoyale/Map Authoring/Capture Battle Royale Reference Map`
  - 全景、中央広場、東水辺の3枚をUnity CameraからPNGへ描画する。
  - Graphics Deviceが必要なので`-nographics`では実行しない。

## 出力

- Scene: `Assets/Scenes/MapAuthoring/GbaForestBattleRoyaleReference.unity`
- Overview: `Assets/Art/Generated/MapAuthoring/QA/gba-forest-battle-royale-reference-overview.png`
- Central: `Assets/Art/Generated/MapAuthoring/QA/gba-forest-battle-royale-reference-central.png`
- Waterfront: `Assets/Art/Generated/MapAuthoring/QA/gba-forest-battle-royale-reference-waterfront.png`

## 生成順

1. 96x72のGround Cellを作る。
2. 東の池と小島をWater Cell集合として確定する。
3. 複数の直交Waypoint列をRadius 1で3 Cell幅へ変換する。
4. 中央・北・東水辺・南の不整形Stone広場を道路集合へ統合する。
5. Start、Landmark、Enemy、Loot、Merchant、Event Socketを定義する。Eventは`Event_01`〜`Event_06`を配置固有Socket IDとし、6点すべてを固定定義なしの`PoolCandidate`として保存する。
6. 道路・水域・Socket周辺を障害物禁止領域として確定する。
7. 大型森林Stamp、崖、単木、低木、岩、切株、倒木のPrefab位置と論理占有Footprintを計画する。物理方式はMarkerのModeから分ける。
8. 葦と草花のDecoration、Chest、Sign、MushroomのProp位置を計画する。
9. 範囲、連結、重複、開始地点間隔、主要地点到達性を検証する。
10. Templateを複製し、4 Tilemap、3表示Root、Socketsへ一括反映する。
11. `MapAuthoringValidator`でScene契約を検証してから保存する。

## 不変条件

- Groundは6,912 Cell。
- Roadは4近傍で単一Component。
- RoadとWater、RoadとCollisionを重ねない。
- StoneはRoadの部分集合。
- Water、外周、TilemapFootprint方式の崖をCollisionTilemapへ明示保存する。
- CollisionBody方式の森・木・低木・岩・切株・倒木はPrefab内の幹・接地点Colliderを使い、広い論理FootprintをCollisionTilemapへ焼かない。
- 道路、Decoration、Socket、到達性はCollisionCellsとObstacleFootprintCellsの和を生成時の禁止領域として使う。これによりPhysicsを細くしても配置密度と経路の読みやすさを維持する。
- 表示Prefabは既存v4.3 Prefabだけを参照し、Positionは1 / 32 unit、Rotation 0、Scale 1とする。
- ObstacleVisuals、Props、TallGrass、Reedsは既存Prefabの`WorldObjects` / Order 0 / Pivotを継承し、FlowerPatch / Wildflowersは`MapDetail`固定背面を継承する。Builder側でカテゴリ別Orderを上書きしない。
- StartはPlayer 1 + CPU 5。開始地点間隔は18 Cell以上。
- 全Start、Landmark、MerchantへCollisionを避けて到達できる。
- Event SocketはRuntime処理を発火せず、Pool候補の配置情報だけをSceneへ保存する。Event種類の抽選、近接、E操作、回復、使用済み状態はGameplay / Playtest側へ分離する。
- Reference再生成前に既存EventSocketのTransform位置と個別半径をSocket IDで退避し、同じIDへ復元する。旧固定定義は復元せず、PoolCandidate契約へ収束させる。
- Sceneへ逐次生成しながら検証せず、計画を完成・検証してから一括反映する。

## 自動生成へ移す場合

現在は`LayoutSeed = 20260803`と固定POI・Waypointを持つ完成基準Map。プレイ結果が良好なら、`ReferenceMapPlan`を作る部分を入力Seed、Bounds、POI Graph、密度、Socket要件から生成する純C#ロジックへ分離する。

Scene書き込み、既存Asset参照、MapCollision正本、`MapAuthoringValidator`はそのまま維持する。RuleTileのSprite選択、Atlas生成、敵やLootの試合開始時抽選、エリア収縮は本Builderの責務に含めない。

## 関連

- [[../../../../Architecture/MapAuthoringKit|Map Authoring Kit]]
- [[../../../../Art/MapPlan|Map Plan]]
- [[MapRoadPathRasterizer]]
- [[MapAuthoringValidator]]
- [[MapAuthoringKitBuilder]]
