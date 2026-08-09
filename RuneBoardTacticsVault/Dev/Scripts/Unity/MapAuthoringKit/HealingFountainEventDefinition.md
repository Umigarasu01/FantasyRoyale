# HealingFountainEventDefinition

## 役割

`HealingFountainEventDefinition`は、共通の`MapEventDefinition`へ泉固有の回復量を追加する`ScriptableObject`。

- Script: `Assets/Scripts/MapAuthoringKit/Runtime/HealingFountainEventDefinition.cs`
- Asset: `Assets/Data/MapAuthoring/Events/HealingFountainBasic.asset`
- Event Definition ID: `healing-fountain-basic`

同じAssetをEvent Poolの候補や複数の`FixedDefinition` Socketから共有できる。配置位置、個別接近半径、抽選結果、使用済み状態は保持しない。

## 公開情報

- `HealAmount`: 一回の成功時に要求する回復量。初期値30。
- `ConfigureHealingFountain(...)`: 共通Event設定と回復量をEditor生成やTestからまとめて設定する。

## 境界

回復処理はAsset自身が実行しない。`MapEventHandlerRegistry`がDefinition実型から`HealingFountainEventHandler`を選び、共通実行サービスが成功後だけSocket ID単位のOneShot状態を更新する。HandlerとPlaytestは本番`ICharacterHealthTarget` / `CharacterHealth`を使い、HP満タンまたは撃破中の通常回復拒否ではOneShotを消費しない。
