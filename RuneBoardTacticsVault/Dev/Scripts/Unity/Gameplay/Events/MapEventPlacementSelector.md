# MapEventPlacementSelector

対象: `Assets/Scripts/Gameplay/Events/Runtime/MapEventPlacementSelector.cs`

## 役割

Socket候補、Event候補、有効数、Seedから、再現可能なEvent配置計画を作る純C#サービス。GameObject、Transform、ScriptableObject、`UnityEngine.Random`へ依存しない。

## 入出力

- `MapEventSocketCandidate`: Socket IDだけを持つ入力。
- `MapEventPoolCandidate`: Event Definition IDと整数重みだけを持つ入力。
- `MapEventPlacementAssignment`: 有効Socket IDと割当Event Definition ID。
- `MapEventPlacementPlan`: Socket ID順に確定したAssignment一覧。

## 抽選順

1. Socket ID空値・重複、有効数超過を検証する。
2. Event ID空値・重複、非正重み、重み合計を検証する。
3. 入力List順の影響を除くため、SocketとEventをID順へ整列する。
4. 固定実装のXorshift32をSeedで初期化する。
5. SocketをFisher-Yates Shuffleし、先頭から有効数だけを重複なしで選ぶ。
6. 選択SocketをID順へ戻し、Eventを整数重み付きで割り当てる。

入力検証が一つでも失敗した場合は空Planを返し、部分的な配置結果を採用しない。同じID集合、重み、有効数、Seedなら、入力Listの順序に関係なく同じPlanになる。

## Unity側との接続

[[../../Playtest/BattleRoyaleExplorationPreviewDebug|BattleRoyaleExplorationPreviewDebug]]がSceneの`PoolCandidate`をID入力へ変換し、PlanのIDを`MapEventPoolDefinition`とScene Markerへ戻す。`FixedDefinition` Socketはこの抽選を迂回する。
