# MapAuthoringValidator

状態: **v4.3実装・描画順を含むUnity Validator / Test検証済み**

## 役割

Unity標準機能で制作したMap SceneとProduction Atlasを完成扱いにする前に、v4.3構造からの逸脱を検出するEditor専用Validator。

SceneやAssetを自動修正せず、Collisionを生成・更新しない。検出結果を安定したCode、重大度、Context付きで返す。

- Script: `Assets/Scripts/MapAuthoringKit/Editor/MapAuthoringValidator.cs`
- メニュー: `FantasyRoyale/Map Authoring/Validate Active Scene`

## v4.3 Scene検証

- Scene直下に一つだけある`MapRoot`。
- `MapRoot/Grid`とGround、Terrain、Collision、Foregroundの4 Tilemap。
- 旧`DetailTilemap`または`ObstacleTilemap`がGrid直下へ残っていないこと。
- `Decorations`、`ObstacleVisuals`、`Props`、`Sockets`がMapRoot直下にあること。
- Decorations / ObstacleVisuals / Props配下にGrid、Tilemap、TilemapRendererが混入していないこと。
- TilemapRendererのSorting LayerとOrder in Layer。
- Renderer2D DataのTransparency Sort ModeがCustom Axis、軸が`Vector3.up`であること。
- ObstacleVisuals / Props / TallGrass / ReedsがWorldObjects / Order 0 / Pivot、FlowerPatch / WildflowersがMapDetail / Order 0 / Pivotであること。
- CollisionTilemapの非表示Renderer、TilemapCollider2D、CompositeCollider2D、Static Rigidbody2D、MapCollision Layer。
- Decorations / Props自身と全子孫、Obstacle Prefabの表示RootがDefault Layer、Collider2Dなし、Rigidbody2Dなし。
- CollisionBody方式のObstacleだけが、Marker直下に同名子を一つ持つ。MapCollision Layer、Scale 1、Renderer / Rigidbody2Dなし、Collider2D一つ、有効、非Trigger、形状・Size・Offset・Rotation・Capsule DirectionがMarkerと一致すること。
- TilemapFootprint方式のObstacleはCollisionBodyを持たず、Footprint全CellがCollisionTilemapへ保存済みであること。
- 3表示Root直下の表示PrefabのPositionが1 / 32 unit、Rotation 0、Scale 1。
- Decorations配下に`MapObstacleVisualMarker`がないこと。Markerを必要とする表示物はObstacleVisualsへ分類する。
- `MapObstacleVisualMarker`のFootprint Size / Offset、Collision Mode / Shape / Size / Offset / Rotation / DirectionとScene保存済み判定の差分。
- Markerとの不一致は報告だけとし、CollisionTilemapやCollisionBodyを変更しない。
- `MapSocketMarker`の所属、Socket ID一意性、Size、Ground内包、相互重複。
- Event Socketの配置方式を検査する。`PoolCandidate`は固定定義を禁止して正の個別半径を必須とし、`FixedDefinition`は定義Asset / 定義ID、参照ID一致、実効半径を検査する。Event以外への配置方式・Event情報混入もErrorとする。
- Event中心はMapCollision上でも許可する。接近円内のGroundを0.25 unit間隔で調べ、足元中心と周囲8点がMapCollision外になる地点が一つもなければ`SOCKET_EVENT_NO_REACHABLE_POINT` Errorとする。

## Production Atlas検証

- 単一Production AtlasとTextureImporterの存在。
- Multiple、PPU 32、Point、Compression None、Mip Map Off、Clamp、Alpha Is Transparency On、NPOT Scale None、Full Rect。
- v4.3 Catalogのschema、Atlas path、Sprite名、Rect、Pivot、Family、Source出典。
- 期待Sprite名集合はGrass 8、Dirt 64、Stone 64、Water 50、Detail 4、Props 10、Obstacle Visual 5の全205名だけ。
- 必須名は`ground_grass_01`〜`08`、Dirt / Stoneの全16 Maskについて基準名と`_v02`〜`v04`、Waterの47 Canonical Maskと`ff_v02`〜`v04`、既存Detail / Props / Obstacle Visual。
- Atlas buildはGrass、Dirt、Stone、Waterの4 Surface Source SheetとDirt / Stoneの2 Road Edge Profile Sheetを使う。ValidatorはCatalogの各EntryについてSource pathとRectが有効であることを検査する。
- Import済みSub-SpriteとCatalogの名前、個数、Rect、Pivotが一致する。
- ForestWall / Cliff Family、旧Loose PNG、Atlas外の表示参照がない。

ValidatorはAtlasのPixelを修正せず、道路SocketのAlpha topologyもここでは走査しない。中央`[6, 26)`の20pxと条件付き角という実Pixel契約は`MapAuthoringAssetContractTests`がchecked-in PNGに対して検査する。

## 公開API

- `ValidateScene(Scene, bool)`: Sceneを変更せず`MapAuthoringValidationReport`を返す。第2引数を`false`にするとProduction Atlas走査だけを省略できる。
- `SocketsOverlap(first, second)`: 回転とScaleを反映したSocket矩形の面積重複を判定する。辺が接するだけの場合は重複としない。

## 判定の考え方

- Errorが一つでもあるSceneは完成扱いにしない。
- Warningは制作継続を止めないが、完成前に確認する。
- CollisionTilemapと規定のCollisionBodyを合わせたMapCollision Layerを移動Physicsの正本とする。
- Markerは制作契約であり、ゲーム判定そのものやRuntime Collision生成の正本にしない。
- Event接近範囲は論理距離であり、Collider / Triggerへ変換しない。
- Atlas、Catalog、Sceneを修正せず、検査だけを行う。
- 立体表示はカテゴリ別Orderではなく共通WorldObjects層の足元Yで比較する。平面装飾と固定Tilemapは明示した固定層から動かさない。

v4.3 + Event Socket操作のMap Authoring系テストは63 passed / 0 failed / 0 skipped。Gameplay Character / Eventを含む全EditMode Suiteは**83 passed / 0 failed / 0 skipped**。

## 旧構造履歴

- v3 Validatorは6 Tilemap、Props Grid、Prefab Collider、v3 Catalog / 191 Spriteを検査していた。
- v4は5 TilemapとObstacleVisuals / Propsを検査していた。
- v4.1では4 TilemapとDecorations / ObstacleVisuals / Props、schema v4.1 / 102 Spriteを標準とし、旧表示Tilemapと表示Root内のGrid系Componentを明示的に拒否した。
- v4.2はScene構造を維持しつつ、期待Sprite名を115点へ更新した。v4.1の102点契約は履歴としてのみ残す。
- v4.3はScene構造を維持しつつ、Dirt / Stone全Maskの4差分を含む期待Sprite名を205点へ更新した。v4.2以前の契約は履歴としてのみ残す。
