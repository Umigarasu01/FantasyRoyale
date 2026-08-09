# BattleRoyaleExplorationPreviewDebug

対象: `Assets/Scripts/Playtest/Runtime/BattleRoyaleExplorationPreviewDebug.cs`

状態: **探索・Event Runtime・Event Pool確認専用の一時実装 / 本番Character Health・移動経路接続済み**

## 役割

96x72の`GbaForestBattleRoyaleReference`を加算読込し、基準Mapを変更せずに次だけをPlayModeで確認する。

- `PlayerStart`への仮Player生成。
- `Player/Move` Action → 人間入力Adapter → 純C# Move Command → 本番Character ActorによるWASD / Arrow Keys移動。
- 本番ActorのDynamic `Rigidbody2D`と足元`CapsuleCollider2D`によるMapCollision Layer（CollisionTilemap + CollisionBody）との衝突。
- Reference Sceneに一つだけあるCameraの追従化、Map端Clamp、1 / 32 unit Snap。
- SeedによるPoolCandidate抽選と、選択済みEventSocket Transformまでの距離による最寄り候補選択。
- `Player/Interact`のE操作、回復の泉、Socket ID単位のOneShot状態。
- 操作、座標、HP、有効Event数、Seed、Event案内、実行結果、経過時間、初期化失敗を表示する確認HUD。

戦闘、CPU、試合進行、エリア収縮は担当しない。HP、Move Command、Actor、人間入力Adapterは本番実装を使い、Eventと将来の戦闘が同じCharacter経路へ接続できることを確認する。

## Scene境界

- Playtest SceneにはBootstrapだけを保存する。
- Map Sceneは`LoadSceneMode.Additive`で読み、対象SceneのRootだけからTilemap、Camera、Socketを検索する。
- Unity SceneをMap配置と物理判定の正本とし、MapCollision Layerに保存済みのCollisionTilemapとCollisionBodyを使う。
- Decoration、Prop、`MapObstacleVisualMarker`からRuntimeでColliderを生成しない。Obstacle Prefab内のCollisionBodyはScene読込時から存在する。
- 仮PlayerはPlaytest Scene側へ所属させ、Reference Sceneの再生成対象へ混ぜない。

## 入力と移動

`Assets/InputSystem_Actions.inputactions`をRuntime中だけ複製し、`Player/Move`と`Player/Interact`を有効化する。`HumanCharacterMoveInputAdapter`がMove Actionを`CharacterMoveCommand`へ変換し、`CharacterActor2D`が物理更新で`Rigidbody2D.MovePosition`へ適用する。BootstrapはRigidbodyを直接移動しない。Eの押下Frameで現在のEvent候補を実行する。Asset本体へPlayMode中の状態を残さない。

本番ActorはGravity 0、Z回転固定、Continuous Collision、Interpolateを使用する。Spawn確認はMapCollision Layer全体へ問い合わせ、Tilemapだけでなく幹・接地点CollisionBodyとの初期重複も拒否する。表示Spriteは8x12pxのRuntime生成Markerであり、正式素材や生成素材台帳の対象ではない。HP 0ではActorがMove Commandを破棄して停止する。

## 描画順

仮PlayerのSpriteRendererは`WorldObjects` / Order 0 / `SpriteSortPoint.Pivot`を使う。Reference MapのObstacleVisuals、Props、TallGrass、Reedsと同じ層へ参加し、world Yが低い足元ほど手前に描画する。FlowerPatch / WildflowersはMapDetail固定背面、ForegroundはMapForeground固定前面のまま扱う。

## Camera

Reference Sceneの全景確認CameraをRuntime中だけOrthographic Size 8の追従Cameraへ切り替える。追加Cameraは作らない。Camera中心は画面半径を考慮してGround範囲内へ制限し、32 PPUのPixel Gridへ丸める。CameraにもTransparency Sort Custom Axis / `Vector3.up`をFallbackとして設定するが、URP 2Dで実描画に使う正本はRenderer2D Dataとする。

## Event縦切り

- `MapEventCatalog`と`MapEventPoolDefinition`をPlaytest Sceneから参照する。
- Reference Sceneの6 EventSocketは`PoolCandidate`であり、固定Event定義を持たない。開始時にSocket IDとPoolのEvent ID / 重みを純C#抽選器へ渡し、Seed `20260807`で3地点を有効化する。
- 抽選PlanのEvent Definition IDをPoolとCatalogの両方へ照合してからRuntime参照へ戻す。非選択Socketは近接候補へ追加しない。
- `FixedDefinition` Socketが追加された場合はPool抽選を迂回し、直接参照またはCatalog ID解決で常時有効にする。
- Player足元とSocket Transformの距離だけで接近判定し、Collider / Triggerは追加しない。
- 範囲内の未使用Eventが複数ある場合は最短距離、同距離ならSocket ID順で一つを選ぶ。
- `MapEventExecutionService`がDefinition型RegistryからHandlerを検索し、Bootstrap自身は泉型を判定しない。
- `HealingFountainEventHandler`が本番`CharacterHealth`へ回復量を適用し、実回復量が1以上の場合だけ成功とする。
- OneShot成功後だけ共通実行サービスが`PreviewDebugMapEventRuntimeStateStore`の`Dictionary<SocketId, State>`へ使用済みを保存する。HP満タン、撃破状態、未対応、不正では消費しない。
- 撃破中の通常回復は専用Message Keyで拒否し、Preview Presenterが確認HUDへ理由を表示する。復活は行わない。
- HandlerはMessage / Effect / Audio Cue IDを返し、`PreviewDebugMapEventPresenter`が日本語HUD文言へ変換する。正式なEffect / Audio再生は行わない。
- ScriptableObjectとReference SceneへPlay中の状態を書き戻さない。

## Test用API

`SetMoveInputOverrideForTests`、`ClearMoveInputOverrideForTests`、`TryInteractWithCurrentEventForTests`、`SetPreviewHealthForTests`は、Input System、移動、Event効果を分離して検証するための注入口。本番Gameplay APIとして使用しない。

関連:

- [[../../../../Architecture/Scenes/PrototypeSoloScene|Exploration Preview Debug Scene]]
- [[../../Editor/Playtest/BattleRoyaleExplorationPreviewDebugSceneBuilder|Scene Builder]]
- [[../../Editor/Playtest/BattleRoyaleExplorationPreviewDebugPlayModeTests|PlayMode Tests]]
- [[../Gameplay/Events/MapEventRuntime|Map Event Runtime]]
- [[../Gameplay/Characters/CharacterMoveCommand|Character Move Command]]
- [[../Gameplay/Characters/CharacterActor2D|Character Actor 2D]]
- [[../Gameplay/Characters/HumanCharacterMoveInputAdapter|Human Move Input Adapter]]
- [[../Gameplay/Events/MapEventPoolDefinition|Map Event Pool]]
- [[../Gameplay/Events/MapEventPlacementSelector|Map Event Placement Selector]]
- [[PreviewDebugMapEventPresentation|Preview Event Presentation]]
