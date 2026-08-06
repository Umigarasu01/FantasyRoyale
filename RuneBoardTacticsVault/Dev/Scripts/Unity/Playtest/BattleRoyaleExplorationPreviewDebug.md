# BattleRoyaleExplorationPreviewDebug

対象: `Assets/Scripts/Playtest/Runtime/BattleRoyaleExplorationPreviewDebug.cs`

状態: **探索・回復の泉確認専用の一時実装 / Step 2検証中**

## 役割

96x72の`GbaForestBattleRoyaleReference`を加算読込し、基準Mapを変更せずに次だけをPlayModeで確認する。

- `PlayerStart`への仮Player生成。
- `Player/Move` ActionによるWASD / Arrow Keys移動。
- Dynamic `Rigidbody2D`と足元`CapsuleCollider2D`によるMapCollision Layer（CollisionTilemap + CollisionBody）との衝突。
- Reference Sceneに一つだけあるCameraの追従化、Map端Clamp、1 / 32 unit Snap。
- EventSocket Transformまでの距離による最寄り候補選択。
- `Player/Interact`のE操作、回復の泉、Socket ID単位のOneShot状態。
- 操作、座標、HP、Event案内、実行結果、経過時間、初期化失敗を表示する確認HUD。

本番Character、戦闘、CPU、試合進行、エリア収縮は担当しない。仮HPとEvent実行はデータ契約を通す確認用であり、本番Character API確定後に責務を分割する。

## Scene境界

- Playtest SceneにはBootstrapだけを保存する。
- Map Sceneは`LoadSceneMode.Additive`で読み、対象SceneのRootだけからTilemap、Camera、Socketを検索する。
- Unity SceneをMap配置と物理判定の正本とし、MapCollision Layerに保存済みのCollisionTilemapとCollisionBodyを使う。
- Decoration、Prop、`MapObstacleVisualMarker`からRuntimeでColliderを生成しない。Obstacle Prefab内のCollisionBodyはScene読込時から存在する。
- 仮PlayerはPlaytest Scene側へ所属させ、Reference Sceneの再生成対象へ混ぜない。

## 入力と移動

`Assets/InputSystem_Actions.inputactions`をRuntime中だけ複製し、`Player/Move`と`Player/Interact`を有効化する。移動入力値は`Update`で取得して長さ1へ制限し、`FixedUpdate`で`Rigidbody2D.MovePosition`へ渡す。Eの押下Frameで現在のEvent候補を実行する。Asset本体へPlayMode中の状態を残さない。

仮PlayerはGravity 0、Z回転固定、Continuous Collision、Interpolateを使用する。Spawn確認はMapCollision Layer全体へ問い合わせ、Tilemapだけでなく幹・接地点CollisionBodyとの初期重複も拒否する。表示Spriteは8x12pxのRuntime生成Markerであり、正式素材や生成素材台帳の対象ではない。

## 描画順

仮PlayerのSpriteRendererは`WorldObjects` / Order 0 / `SpriteSortPoint.Pivot`を使う。Reference MapのObstacleVisuals、Props、TallGrass、Reedsと同じ層へ参加し、world Yが低い足元ほど手前に描画する。FlowerPatch / WildflowersはMapDetail固定背面、ForegroundはMapForeground固定前面のまま扱う。

## Camera

Reference Sceneの全景確認CameraをRuntime中だけOrthographic Size 8の追従Cameraへ切り替える。追加Cameraは作らない。Camera中心は画面半径を考慮してGround範囲内へ制限し、32 PPUのPixel Gridへ丸める。CameraにもTransparency Sort Custom Axis / `Vector3.up`をFallbackとして設定するが、URP 2Dで実描画に使う正本はRenderer2D Dataとする。

## Event縦切り

- `MapEventCatalog`をPlaytest Sceneから参照し、直接参照または定義IDを解決する。
- Player足元とSocket Transformの距離だけで接近判定し、Collider / Triggerは追加しない。
- 範囲内の未使用Eventが複数ある場合は最短距離、同距離ならSocket ID順で一つを選ぶ。
- `HealingFountainEventDefinition.HealAmount`を仮HPへ適用し、実回復量が1以上の場合だけ成功とする。
- OneShot成功後だけ`PreviewDebugMapEventRuntimeStateStore`の`Dictionary<SocketId, State>`へ使用済みを保存する。HP満タンでは消費しない。
- ScriptableObjectとReference SceneへPlay中の状態を書き戻さない。

## Test用API

`SetMoveInputOverrideForTests`、`ClearMoveInputOverrideForTests`、`TryInteractWithCurrentEventForTests`、`SetPreviewHealthForTests`は、Input System、移動、Event効果を分離して検証するための注入口。本番Gameplay APIとして使用しない。

関連:

- [[../../../../Architecture/Scenes/PrototypeSoloScene|Exploration Preview Debug Scene]]
- [[../../Editor/Playtest/BattleRoyaleExplorationPreviewDebugSceneBuilder|Scene Builder]]
- [[../../Editor/Playtest/BattleRoyaleExplorationPreviewDebugPlayModeTests|PlayMode Tests]]
