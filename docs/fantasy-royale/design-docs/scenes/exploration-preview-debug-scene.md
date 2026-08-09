# Exploration Preview Debug Scene

ファイル名`PrototypeSoloScene.md`は既存リンク互換のため残す。旧Prototype実装と未作成の`Milestone1ExplorationScene`への移行案は廃止し、現在の探索確認Sceneをこのノートで扱う。

状態: **探索・足元Y描画順・共通Event Runtime・Event Pool抽選・本番Character Health / 移動経路を検証完了 / 本番Gameplay Sceneではない**

## 対象

- Playtest: `Assets/Scenes/Playtest/GbaForestBattleRoyaleExplorationPreviewDebug.unity`
- Map正本: `Assets/Scenes/MapAuthoring/GbaForestBattleRoyaleReference.unity`

## 保存Hierarchy

    PlaytestRoot
    └─ BattleRoyaleExplorationPreviewDebug

Playtest SceneにはBootstrapだけを保存する。Play開始後、Reference MapをAdditiveで読み、仮Playerを`PlayerStart`へ生成する。MapのTilemap、自由配置表示、Socket、CameraはReference Scene側に残し、Playtest Sceneへ複製しない。

## Runtime接続

1. `InputSystem_Actions`のRuntime複製から`Player/Move`を人間入力Adapterが読む。
2. AdapterがUnity `Vector2`を純C# `CharacterMoveCommand`へ変換する。
3. `CharacterActor2D`が最新Commandを`FixedUpdate`でDynamic `Rigidbody2D.MovePosition`へ適用する。
4. 本番Actorの足元`CapsuleCollider2D`とReference SceneのMapCollision Layer（CollisionTilemap + CollisionBody）で衝突する。
5. Reference SceneのCameraを一つだけ再利用し、`LateUpdate`でPlayerへ追従する。
6. 仮Playerを`WorldObjects` / Order 0 / Pivotへ置き、Map内の木、崖、Props、背の高い装飾と同じ足元Yソートへ参加させる。
7. Reference Sceneの6 `PoolCandidate`をSocket ID入力へ変換し、Event PoolとPreview Seed `20260807`から3地点の配置Planを生成する。
8. PlanのEvent Definition IDをPoolとCatalogへ照合し、有効Socketだけを近接候補として登録する。
9. `Player/Interact`のE押下で、足元から最寄りの未使用EventSocketを選ぶ。
10. `MapEventExecutionService`がDefinition型RegistryからHandlerを検索し、`HealingFountainEventHandler`が本番`CharacterHealth`へ回復量を適用する。
11. 成功時だけSocket ID単位のRuntime DictionaryへOneShot状態を保存し、PresenterがMessage KeyをHUD文言へ変換する。

CameraはOrthographic Size 8、Map端Clamp、1 / 32 unit Snap、Transparency Sort Custom Axis / `Vector3.up`を使う。URP 2Dの正本はRenderer2D Dataとし、追加CameraやSocket用Triggerは作らない。

## 現在確認できること

- WASD / Arrow Keysによる96x72 Map内の歩行。
- 人間入力Adapter、純C# Move Command、本番Character Actorを通る共通移動経路。
- 崖、水際、外周のCollisionTilemapと、木・森・岩などの幹・接地点CollisionBodyでの停止。
- 木の幹中央は衝突し、樹冠側は通行できる判定分離。
- Player追従Cameraと現在座標の確認。
- Playerが立体表示の上側では背面、下側では前面になる足元Y描画順。
- EventSocketの自由なTransform位置と個別半径による近接案内。
- 6候補からSeedで再現可能に3地点だけを有効化するEvent Pool抽選。
- E操作による回復、HP表示、成功後のSocket単位OneShot。満タン時と撃破時は消費しない。
- HP 0では通常回復を拒否して理由をHUDへ表示し、撃破状態から復活しない。
- HP 0では本番Actorが移動Commandを破棄して停止する。
- Handlerが返す回復Message Key、Effect Cue ID、Audio Cue IDと、Preview PresenterによるHUD文言。

## 未実装

- Loot / Enemy / Merchant SocketのRuntime選定と生成。
- 泉以外のEvent Handler、地域Tag・距離制約付きPool、正式Match Seed、Cooldown、Save連携。
- Effect / Audio CueをPrefabやAudioSourceへ接続する正式Presenter。
- 通常攻撃 / 特殊アクション、装備、正式Character Prefab / Animation、CPU / Network入力Adapter、試合開始・終了、エリア収縮。

Runtime生成Spriteと確認HUDは探索確認専用。本番HP Core、Move Command、Character Actor、人間入力Adapterは本番Assemblyへ分離済み。攻撃や正式表示の設計を確定した扱いにはしない。

関連:

- [GBA Forest Map Authoring Scene](gba-forest-map-authoring-scene.md)
- [Runtime Bootstrap](../../development/code-reference/playtest/battle-royale-exploration-preview-debug.md)
