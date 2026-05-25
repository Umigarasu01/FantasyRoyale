# PrototypePlayerController2D

## 対応スクリプト

`Assets/Scripts/Prototype/PrototypePlayerController2D.cs`

## 役割

一人用プロトタイプのプレイヤー操作を受け持つ。
キーボード移動、向きの保持、近接攻撃、Eキーでのインタラクト、コインと攻撃力ボーナスの保持を行う。

## 公開API

- `ConfigureForPrototype(...)`: Scene生成ツールから仮設定を受け取る。
- `ConfigureAttackEffect(PrototypeAttackEffect prefab)`: 攻撃時に出す仮エフェクトを設定する。
- `ApplyItem(PrototypeItem item)`: アイテム効果を反映する。
- `AddCoins(int amount)`: 敵撃破報酬を加算する。
- `TrySpendCoins(int amount)`: 商人購入などでコインを支払えるか判定し、足りていれば消費する。
- `SetInputLocked(bool locked)`: ゲームオーバー時などに入力を止める。

## 関連仕様

- 操作は一旦キーボードのみ。
- 攻撃は向いている方向への短い範囲攻撃。デフォルト攻撃力は、初期スライムを1発で倒せるように2。
- 攻撃時に判定位置へ斬撃エフェクトを生成する。
- 斬撃の見た目と判定のズレを減らすため、攻撃判定は根元側と先端側の円を組み合わせる。
- 将来的にはInput Actionsや本番用Controllerへ置き換える。
