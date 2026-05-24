# IPrototypeInteractable

## 対応スクリプト

`Assets/Scripts/Prototype/IPrototypeInteractable.cs`

## 役割

Eキーで起動できる仮オブジェクトの共通口。
現在は `PrototypeChest` が実装している。

## 公開API

- `Interact(PrototypePlayerController2D player)`: プレイヤーからの操作を受け取る。

## 関連仕様

- 将来的に商人、鑑定、悪魔の取引などへ拡張できる入口として残す。
