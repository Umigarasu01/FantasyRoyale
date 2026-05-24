# PrototypeAttackEffect

## 対応スクリプト

`Assets/Scripts/Prototype/PrototypeAttackEffect.cs`

## 役割

攻撃時に表示する仮の斬撃エフェクト。
攻撃判定とは分け、見た目だけを短時間で拡大、フェードアウトさせる。

## 公開API

- `Play(Vector2 direction)`: 攻撃方向を受け取り、斬撃の角度と寿命を初期化する。

## 関連仕様

- `Space` 攻撃時に、プレイヤーが向いている方向へ斬撃を表示する。
- 本番では武器種やスキルごとのエフェクトへ差し替える想定。
