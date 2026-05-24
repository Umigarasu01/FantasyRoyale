# PrototypeEnemy

## 対応スクリプト

`Assets/Scripts/Prototype/PrototypeEnemy.cs`

## 役割

プレイヤーを追いかける最小構成の雑魚敵。
接触時に一定間隔でダメージを与え、死亡時にプレイヤーへコイン報酬を渡す。
既存Sceneに `PrototypeHealth` の古い初期値が残る場合があるため、起動時に敵側のプロトタイプHPで上書きする。

## 公開API

- `Initialize(...)`: 追跡対象、報酬付与先、HPを設定する。
- `TakeDamage(int damage)`: プレイヤー攻撃からのダメージを受け取る。

## 関連仕様

- 地道な強化ルートとして、雑魚敵撃破でコインを得る。
