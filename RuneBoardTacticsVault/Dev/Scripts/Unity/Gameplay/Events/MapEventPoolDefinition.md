# MapEventPoolDefinition

対象: `Assets/Scripts/Gameplay/Events/Runtime/MapEventPoolDefinition.cs`

## 役割

対戦開始時に抽選できるEvent定義、整数重み、有効Socket数をInspector編集可能なScriptableObjectとして保存する。

保存の正本は`List<MapEventPoolEntry>`。実行時のEvent Definition ID検索だけ`Dictionary<string, MapEventDefinition>`へ変換する。Seed、抽選結果、使用済み状態は対戦ごとに変わるためAssetへ保存しない。

初期Asset:

- `Assets/Data/Gameplay/Events/BattleRoyaleReferenceEventPool.asset`
- Pool ID: `battle-royale-reference-events`
- Active Socket Count: `3`
- Entry: `healing-fountain-basic` / Weight `1`

## 主な型とAPI

- `MapEventPoolEntry`: Event Definition参照と1以上の整数重み。
- `PoolId`: Poolを識別する固定ID。
- `ActiveSocketCount`: PoolCandidateから有効化する地点数。0も許可する。
- `Entries`: Inspectorへ保存する候補List。
- `TryBuildRuntimeCandidates(...)`: Unity参照を除いたEvent Definition IDと重みへ変換する。
- `TryGetDefinition(...)`: 抽選結果IDをRuntime Dictionaryで定義参照へ戻す。
- `Validate(...)`: Pool ID、候補参照、Event ID、重み、ID重複を検査する。
- `Configure(...)`: Editor BuilderとTest用の一括設定口。

## 境界

Poolは「何を何件抽選できるか」だけを保存する。「どのSocketへ何が出たか」は[[MapEventPlacementSelector|MapEventPlacementSelector]]の結果であり、Match終了時に破棄またはMatch状態として別保存する。

Event定義自体は[[../../MapAuthoringKit/MapEventDefinition|MapEventDefinition]]を参照し、全定義のID検索正本は`MapEventCatalog`を維持する。
