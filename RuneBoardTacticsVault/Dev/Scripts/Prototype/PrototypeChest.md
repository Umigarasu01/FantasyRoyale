# PrototypeChest

## 対応スクリプト

`Assets/Scripts/Prototype/PrototypeChest.cs`

## 役割

Eキーで開けられる宝箱。
未開封なら `PrototypeGameController` にアイテム抽選を依頼し、プレイヤーへ効果を反映する。

## 公開API

- `Initialize(PrototypeGameController controller)`: 抽選とログ表示を行う管理者を設定する。
- `Interact(PrototypePlayerController2D player)`: 宝箱を開ける。

## 関連仕様

- 宝箱は幸運による強化の入口。
- 低確率で伝説級アイテムが出る。
