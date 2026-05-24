# PrototypeSceneBuilder

## 対応スクリプト

`Assets/Editor/PrototypeSceneBuilder.cs`

## 役割

一人用プロトタイプSceneをEditor上で生成する。
仮ピクセルスプライト、固定小マップ、プレイヤー、敵、宝箱、HUD、Build Settings登録をまとめて行う。

## 実行方法

Unity Editorで以下を実行する。

`FantasyRoyale > Build Solo Prototype Scene`

## 生成物

- `Assets/Scenes/PrototypeSoloScene.unity`
- `Assets/Art/Prototype/PlayerIdle0.png` などのプレイヤー仮スプライト
- `Assets/Art/Prototype/SlimeIdle0.png` などの敵仮スプライト
- `Assets/Art/Prototype/ChestClosed.png`
- `Assets/Art/Prototype/ChestOpen.png`
- `Assets/Art/Prototype/SlashEffect.png`
- `Assets/Art/Prototype/GrassTile.png`
- `Assets/Art/Prototype/StoneTile.png`

## 関連仕様

- 仮素材で最小プレイ確認を行うための足場。
- プレイヤーと敵には `PrototypeSpriteAnimator` を付け、移動時だけ簡易アニメーションする。
- プレイヤーには `PrototypeAttackEffect` のテンプレートを設定し、攻撃時に斬撃を表示する。
- 初期配置のスライムHPは、攻撃エフェクトが当たった手応えを確認しやすいように1発撃破の値にする。
- 本番Sceneや正式素材へ移行する前提のため、`Prototype` 名を付けている。
