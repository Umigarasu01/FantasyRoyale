# Exploration Preview Debug Scene

ファイル名`PrototypeSoloScene.md`は既存リンク互換のため残す。旧Prototype実装と未作成の`Milestone1ExplorationScene`への移行案は廃止し、現在の探索確認Sceneをこのノートで扱う。

状態: **探索・足元Y描画順・回復の泉Eventを検証中 / 本番Gameplay Sceneではない**

## 対象

- Playtest: `Assets/Scenes/Playtest/GbaForestBattleRoyaleExplorationPreviewDebug.unity`
- Map正本: `Assets/Scenes/MapAuthoring/GbaForestBattleRoyaleReference.unity`

## 保存Hierarchy

    PlaytestRoot
    └─ BattleRoyaleExplorationPreviewDebug

Playtest SceneにはBootstrapだけを保存する。Play開始後、Reference MapをAdditiveで読み、仮Playerを`PlayerStart`へ生成する。MapのTilemap、自由配置表示、Socket、CameraはReference Scene側に残し、Playtest Sceneへ複製しない。

## Runtime接続

1. `InputSystem_Actions`のRuntime複製から`Player/Move`を読む。
2. `Update`で正規化した移動入力値（Vector2）を保持する。
3. `FixedUpdate`でDynamic `Rigidbody2D.MovePosition`へ渡す。
4. 足元`CapsuleCollider2D`とReference SceneのMapCollision Layer（CollisionTilemap + CollisionBody）で衝突する。
5. Reference SceneのCameraを一つだけ再利用し、`LateUpdate`でPlayerへ追従する。
6. 仮Playerを`WorldObjects` / Order 0 / Pivotへ置き、Map内の木、崖、Props、背の高い装飾と同じ足元Yソートへ参加させる。
7. `Player/Interact`のE押下で、足元から最寄りの未使用EventSocketを実行する。
8. `HealingFountainBasic`の回復量を仮HPへ適用し、成功時だけSocket ID単位のRuntime DictionaryへOneShot状態を保存する。

CameraはOrthographic Size 8、Map端Clamp、1 / 32 unit Snap、Transparency Sort Custom Axis / `Vector3.up`を使う。URP 2Dの正本はRenderer2D Dataとし、追加CameraやSocket Trigger、Runtime生成Colliderは作らない。

## 現在確認できること

- WASD / Arrow Keysによる96x72 Map内の歩行。
- 崖、水際、外周のCollisionTilemapと、木・森・岩などの幹・接地点CollisionBodyでの停止。
- 木の幹中央は衝突し、樹冠側は通行できる判定分離。
- Player追従Cameraと現在座標の確認。
- Playerが立体表示の上側では背面、下側では前面になる足元Y描画順。
- EventSocketの自由なTransform位置と個別半径による近接案内。
- E操作による回復、HP表示、成功後のSocket単位OneShot。満タン時は消費しない。

## 未実装

- Loot / Enemy / Merchant SocketのRuntime選定と生成。
- 泉以外のEvent Handler、Event Pool抽選、Cooldown、Save連携。
- 本番CharacterのHP、戦闘、CPU、試合開始・終了、エリア収縮。

仮PlayerのRuntime生成Spriteと直接移動は探索確認専用。本番Characterと入力Commandの設計を確定した扱いにはしない。

関連:

- [[GbaForestMapAuthoringScene|GBA Forest Map Authoring Scene]]
- [[../../Dev/Scripts/Unity/Playtest/BattleRoyaleExplorationPreviewDebug|Runtime Bootstrap]]
