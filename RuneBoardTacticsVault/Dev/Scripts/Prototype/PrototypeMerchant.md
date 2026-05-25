# PrototypeMerchant

## 対応スクリプト

`Assets/Scripts/Prototype/PrototypeMerchant.cs`

## 役割

コインを支払ってランダム商品を買える仮商人。
商人が価値を知らずに伝説級アイテムを売る体験を、ショップ画面なしで確認する。

## 公開API

- `Initialize(PrototypeGameController controller, int price)`: 抽選とログ表示を行う管理者、購入価格を設定する。
- `Interact(PrototypePlayerController2D player)`: プレイヤーからの `E` 操作を受け取り、購入可否を判定する。

## 関連仕様

- 価格は仮に10コイン。
- コイン不足時は購入できない。
- 購入成功時は宝箱と同じ仮アイテム抽選を使う。
- 低確率で伝説級アイテムも買える。
