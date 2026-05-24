# PrototypeSpriteAnimator

## 対応スクリプト

`Assets/Scripts/Prototype/PrototypeSpriteAnimator.cs`

## 役割

プロトタイプ用の簡易スプライトアニメーション再生役。
プレイヤーや敵の移動状態を見て、待機スプライトと移動スプライトを一定間隔で差し替える。

## 公開API

現時点では外部から呼ぶ公開メソッドはない。
`PrototypeSceneBuilder` が `SerializedObject` 経由で待機/移動スプライトと再生速度を設定する。

## 関連仕様

- 本番のAnimator Controllerや正式素材へ移行する前の仮実装。
- 一人用プロトタイプが無機質になりすぎないよう、最低限の生き物感を出す。
