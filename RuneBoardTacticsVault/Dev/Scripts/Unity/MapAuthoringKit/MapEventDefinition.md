# MapEventDefinition

## 役割

`MapEventDefinition` は、Event Poolまたは`FixedDefinition` EventSocketが参照するイベントの不変設定を保存する`ScriptableObject`。同じ泉や祠を複数配置しても設定を共有でき、対戦中の抽選結果や使用済み状態は保持しない。

- Script: `Assets/Scripts/MapAuthoringKit/Runtime/MapEventDefinition.cs`
- Assetメニュー: `FantasyRoyale/Map Authoring/Event Definition`
- 初期Asset: `Assets/Data/MapAuthoring/Events/HealingFountainBasic.asset`（`HealingFountainEventDefinition`）

## 公開情報

- `EventDefinitionId`: マップデータやカタログ検索で使う固定ID。
- `DisplayName`: UIやデバッグ表示用の名称。
- `PromptText`: 近接時に表示する操作案内の初期文言。
- `DefaultInteractionRadius`: Socket側の上書きがない場合の接近判定半径（World unit）。
- `OneShot`: 対戦中に一度だけ実行する定義か。実際の使用済み状態はRuntime側で管理する。
- `Configure(...)`: Editor生成やTestから最小設定を記録する。

## 設計上の注意

- Asset名を識別子にせず、`EventDefinitionId`を安定した契約として扱う。
- イベント固有設定は派生定義へ置く。初期実装では`HealingFountainEventDefinition`が回復量30を保持する。
- Gameplay側は派生Definitionの実型をRuntime DictionaryのKeyとしてHandlerを検索する。Definition自身は効果、UI、Effect、Audioを実行しない。
- ScriptableObjectへ使用済みフラグを書き戻さない。共有Assetが別Socketや別対戦へ影響するため、状態は`socketId`単位のRuntimeデータへ分離する。
