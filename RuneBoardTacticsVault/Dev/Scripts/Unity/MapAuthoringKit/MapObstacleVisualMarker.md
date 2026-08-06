# MapObstacleVisualMarker

状態: **12 Obstacle PrefabへMode別判定を設定・自動テスト済み**

## 役割

障害物Prefabに、自動配置用の占有Footprintと、Sceneへ保存する物理判定方式・足元形状を保持するRuntime参照可能な制作マーカー。

- Script: `Assets/Scripts/MapAuthoringKit/Runtime/MapObstacleVisualMarker.cs`
- Namespace: `FantasyRoyale.MapAuthoringKit`

このComponent自体はPhysics判定ではない。RuntimeはMarkerからColliderを生成せず、Sceneへ保存済みの`CollisionTilemap`と`CollisionBody`を合わせたMapCollision Layerを使う。

## 保持データ

- `FootprintSize`: 推奨範囲の幅と高さ。初期値は1x1。
- `FootprintOffset`: 配置Anchor Cellから推奨範囲左下までのCell Offset。初期値は0x0。
- `CollisionMode`: `TilemapFootprint`または`CollisionBody`。
- `CollisionShape`: CollisionBodyの`Box`または`Capsule`。
- `CollisionSize`: 幹・接地点に合わせるColliderの大きさ。
- `CollisionOffset`: 表示Rootの足元PivotからCollisionBodyまでのLocal位置。
- `CollisionRotationDegrees`: CollisionBodyのLocal Z回転。
- `CapsuleDirection`: CapsuleCollider2Dの向き。

## 公開API

- `Configure(...)`: FootprintとCollision Mode / Shape / Size / Offset / Rotation / Directionをまとめて設定する。Footprint Sizeの各軸は最低1 Cell、CollisionBodyのSizeは最低1pxへ補正する。
- `Configure(Vector2Int size, Vector2Int offset)`: 崖などTilemap Footprint方式の互換入口。
- `GetFootprintCells(Vector3Int anchorCell)`: Anchor Cellを基準に、生成時の占有・間隔確認へ使うFootprint Cellを列挙する。

## 使用方針

- 必要なObstacleVisuals向けPrefabだけへ付与する。
- 論理Footprintは道路、Socket、他素材との配置間隔に使い、実際のPhysics形状として扱わない。
- `CollisionBody`方式はPrefab直下の同名子にMarkerどおりのCollider2Dを一つ保存する。表示RootはDefault、子はMapCollision Layerとする。
- `TilemapFootprint`方式は子Colliderを持たず、Builderまたは制作者がCollisionTilemapへFootprintを保存する。
- ゲーム側はこの値から移動判定を復元しない。
- 表示RootはDefault Physics Layer、Position 1 / 32 unit、Rotation 0、Scale 1を守る。CollisionBodyにはRenderer、Rigidbody2D、Triggerを持たせない。

## 責務外

- RuntimeでのPhysics Collider生成。
- NavMeshや経路探索用データの生成。
- Scene保存済みCollisionの実行中の生成、修正。
- 表示Spriteの描画順制御。
