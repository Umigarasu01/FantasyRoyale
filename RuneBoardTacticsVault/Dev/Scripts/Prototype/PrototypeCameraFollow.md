# PrototypeCameraFollow

## 対応スクリプト

`Assets/Scripts/Prototype/PrototypeCameraFollow.cs`

## 役割

広いプロトタイプマップを探索できるよう、プレイヤーを追いかける簡易カメラ。
マップ外周の外を映しすぎないよう、追従位置を指定範囲内に制限する。

## 公開API

- `Initialize(Transform target, Vector2 minPosition, Vector2 maxPosition)`: 追跡対象とカメラ移動範囲を設定する。

## 関連仕様

- 5-6人規模のバトロワ用マップを一人用プロトタイプで歩き回るための仮実装。
