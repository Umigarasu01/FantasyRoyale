# PrototypeHealth

## 対応スクリプト

`Assets/Scripts/Prototype/PrototypeHealth.cs`

## 役割

プレイヤーと敵が共有するHP管理。
ダメージ、回復、死亡通知、HP変更通知を受け持つ。

## 公開API

- `Initialize(int hitPoints)`: 最大HPと現在HPを初期化する。
- `TakeDamage(int amount)`: HPを減らす。
- `Heal(int amount)`: HPを回復する。

## 関連仕様

- プレイヤーHPが0になるとゲームオーバー。
- 敵HPが0になると撃破報酬が入る。
