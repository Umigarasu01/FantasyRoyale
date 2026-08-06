# HealingFountainEventDefinition

## 役割

`HealingFountainEventDefinition`は、共通の`MapEventDefinition`へ泉固有の回復量を追加する`ScriptableObject`。

- Script: `Assets/Scripts/MapAuthoringKit/Runtime/HealingFountainEventDefinition.cs`
- Asset: `Assets/Data/MapAuthoring/Events/HealingFountainBasic.asset`
- Event Definition ID: `healing-fountain-basic`

同じAssetを複数のEventSocketから参照する。配置位置、個別接近半径、使用済み状態は保持しない。

## 公開情報

- `HealAmount`: 一回の成功時に要求する回復量。初期値30。
- `ConfigureHealingFountain(...)`: 共通Event設定と回復量をEditor生成やTestからまとめて設定する。

## 境界

回復処理はAsset自身が実行しない。Playtestでは`BattleRoyaleExplorationPreviewDebug`が仮PlayerのHPへ適用し、成功後だけSocket ID単位のOneShot状態を更新する。本番CharacterのHP契約は未確定のため、Preview実装をそのまま本番APIとは扱わない。
