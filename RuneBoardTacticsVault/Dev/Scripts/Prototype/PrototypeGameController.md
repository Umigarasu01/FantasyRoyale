# PrototypeGameController

## 対応スクリプト

`Assets/Scripts/Prototype/PrototypeGameController.cs`

## 役割

一人用プロトタイプ全体の管理役。
プレイヤー参照、HUD参照、宝箱アイテム抽選、入手ログ、ゲームオーバー表示を受け持つ。

## 公開API

- `RollChestItem()`: 宝箱から出る仮アイテムを抽選する。
- `NotifyItemFound(PrototypeItem item)`: 入手アイテムをHUDへ表示する。
- `NotifyMerchantPurchase(PrototypeItem item, int price)`: 商人購入の結果をHUDへ表示する。
- `NotifyMerchantNeedsCoins(int price)`: コイン不足をHUDへ表示する。

## 関連仕様

- 宝箱から通常、レア、伝説級アイテムを抽選する。
- 伝説級アイテムは低確率で出現し、ログに事件として表示する。
- 商人購入でも同じ仮アイテム抽選を使い、「安く買ったものが伝説級だった」体験を確認する。
