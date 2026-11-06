# Exploration Preview Debug Scene

状態: **ForestRestartの48x32 Reference Mapへ移行・Character/Event基盤を維持・PlayMode 4件回帰成功 / 本番Gameplay Sceneではない**

## 対象

- Playtest: `Assets/Scenes/Playtest/GbaForestBattleRoyaleExplorationPreviewDebug.unity`
- Map正本: `Assets/Scenes/MapAuthoring/GbaForestBattleRoyaleReference.unity`

2026-10-03の素材再制作でMap正本の内容をForestRestartのSeed20261003へ置換し、同じpath/GUIDでAdditive読込を維持する。新しいMapは48x32Cellであり、以前の96x72構成を前提にしない。MapScene内の`ConceptScaleHero`は素材縮尺比較用で、Playtestの操作Playerを置き換えた正式Characterではない。Seed差分の追加Scene3点はEditor検証用で、正式Match SeedによるMap抽選機能は追加していない。

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

## 現在の確認対象

- WASD / Arrow Keysによる48x32 ForestRestart Map内の歩行。
- 人間入力Adapter、純C# Move Command、本番Character Actorを通る共通移動経路。
- 入口を除く外周のCollisionTilemapと、oak / grove / undergrowthの幹・接地点CollisionBodyでの停止。現行の小規模検証Mapには旧Mapの水域・崖を含めない。
- 木の幹中央は衝突し、樹冠側は通行できる判定分離。
- Player追従Cameraと現在座標の確認。
- Playerが立体表示の上側では背面、下側では前面になる足元Y描画順。
- EventSocketの自由なTransform位置と個別半径による近接案内。
- 6候補からSeedで再現可能に3地点だけを有効化するEvent Pool抽選。
- E操作による回復、HP表示、成功後のSocket単位OneShot。満タン時と撃破時は消費しない。
- HP 0では通常回復を拒否して理由をHUDへ表示し、撃破状態から復活しない。
- HP 0では本番Actorが移動Commandを破棄して停止する。
- Handlerが返す回復Message Key、Effect Cue ID、Audio Cue IDと、Preview PresenterによるHUD文言。

## 攻撃の確認

PlayerStart付近に確認用の的を置く。HPは100で、倒すと2秒後に戻る。

| 操作 | Keyboard | Mouse |
| --- | --- | --- |
| 通常攻撃 | J、Enter | 左Click |
| 特殊Action | K | 右Click |
| 装備の切り替え | 1、2 | なし |

剣の装備は通常攻撃が剣で特殊Actionが弓、弓の装備はその逆である。斬撃、矢、的の色の変化はRuntime生成の仮表示である。このSceneは承認済みの森マップではなく、従来のReference Mapを読む。

検証用Project`UnityVisualValidation`にも同じ名前のSceneがあるが、古いコピーなので攻撃は入っていない。攻撃はRepositoryのProjectで確認する。

## 未実装

- Loot / Enemy / Merchant SocketのRuntime選定と生成。
- 泉以外のEvent Handler、地域Tag・距離制約付きPool、正式Match Seed、Cooldown、Save連携。
- Effect / Audio CueをPrefabやAudioSourceへ接続する正式Presenter。
- 正式Character Prefab / Animation、CPU / Network入力Adapter、試合開始・終了。
- 2026-10-09に決めた育成中脱落なし・妨害・最終バトルへの移行は未実装。エリア収縮は当面採用なしで、今後の実装目標としない。[Prototype方針](../../product-specs/prototype-direction.md)を参照する。既存PreviewのHP 0死亡・通常回復による非復活の検証履歴は変更しない。

Runtime生成Spriteと確認HUDは探索確認専用。本番HP Core、Move Command、Character Actor、人間入力Adapterは本番Assemblyへ分離済み。攻撃や正式表示の設計を確定した扱いにはしない。

## 検証と履歴の境界

2026-10-03の旧素材撤去後、Unity Test RunnerのPlayMode 4件が成功した。実移動、外周と幹の衝突、樹冠側の通行、Camera追従とSnap、Yソート、Event抽選と回復処理を検証した。Camera追従のTestは新Map中央で行い、端Clampと区別する。EditModeは98件成功。素材の正式採用は未承認で、本番の固定Map + ランダム配置方針は維持する。

仕切り直し前の96x72 Reference Mapでは、探索・足元Y描画順・共通Event Runtime・Event Pool抽選・本番Character Health / 移動経路を検証済みだった。旧Mapの崖・水際・表示素材とそのQA画像は撤去済みであり、その検証結果を現在の48x32素材へ自動で引き継がない。旧`PrototypeSoloScene.md`と未作成`Milestone1ExplorationScene`への移行案はLegacyで、現行Sceneはこのノートに記載したpathを参照する。

関連:

- [GBA Forest Map Authoring Scene](gba-forest-map-authoring-scene.md)
- [Runtime Bootstrap](../../development/code-reference/playtest/battle-royale-exploration-preview-debug.md)
