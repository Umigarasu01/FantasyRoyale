# BattleRoyaleExplorationPreviewDebugPlayModeTests

対象: `Assets/Scripts/Playtest/Tests/PlayMode/BattleRoyaleExplorationPreviewDebugPlayModeTests.cs`

状態: **4件実装 / Unity PlayMode検証済み**

## 狙い

小さな単体検査だけで成功扱いにせず、生成済みPlaytest Sceneから実際のReference Mapを加算読込する経路全体、描画順、幹判定をPlayMode Testで確認する。

## 確認内容

- Reference Map、`PlayerStart`、仮Player、Collision、Cameraが正しく初期化される。
- Runtime複製された`Player/Move`がWASD Bindingを持ち、仮想Keyboard入力が移動Commandへ届く。
- 仮Playerが空き方向へ移動する。
- 実`CollisionTilemap`の衝突Cellに向かって進み、壁を貫通しない。
- 実Reference MapのCollisionBody方式ObstacleがMapCollision Layerへ存在する。
- 単木の幹中央はCollider内、樹冠側は同じCollider外であり、実Player Colliderも幹では重なり樹冠側では離れる。旧1 Cell全体判定へ戻っていない。
- CameraがPlayerへ追従し、Zを維持して1 / 32 unitへSnapする。
- CameraがTransparency Sort Custom Axis / `Vector3.up`、仮PlayerがWorldObjects / Order 0 / Pivotで初期化される。
- 実Reference MapのObstacleVisuals、Props、TallGrass、Reedsが仮Playerと同じWorldObjects契約、FlowerPatch / WildflowersがMapDetail固定背面を維持する。
- `Player/Interact`がKeyboard E Bindingを持つ。
- Reference SceneのEventSocket 6点が同じ`HealingFountainEventDefinition`を共有し、各Transform位置と個別半径をRuntimeで読む。
- 泉の回復成功時だけHPが増え、Socket ID単位のOneShot状態がDictionaryへ保存される。
- HP満タン時は失敗結果となり、Socketを使用済みにしない。

Tilemap Collision検査は固定座標へ依存せず、実Tilemapから「衝突Cellに隣接する空Cell」を探索して条件を作る。木の検査も実Reference MapからCollisionTilemapと重ならない単木を選び、Colliderの幹中央と樹冠側を比較する。Mapの細かな配置変更後も契約が維持されていれば検査を継続できる。

Unity PlayMode Suite: **4 passed / 0 failed / 0 skipped**。

関連:

- [[../../Unity/Playtest/BattleRoyaleExplorationPreviewDebug|Runtime Bootstrap]]
- [[BattleRoyaleExplorationPreviewDebugSceneBuilder|Scene Builder]]
