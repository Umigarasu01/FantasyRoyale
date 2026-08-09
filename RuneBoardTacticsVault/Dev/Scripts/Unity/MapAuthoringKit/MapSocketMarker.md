# MapSocketMarker

## 役割

`MapSocketMarker` は、Map Scene上にPlayer Start、Enemy、Loot、Merchant、汎用Eventなどの候補地点を置くための制作マーカー。

Spawn、抽選、ゲーム進行は実装しない。種別、配置固有ID、占有範囲、向き、任意Tagを保持する。Eventの場合は`PoolCandidate`または`FixedDefinition`を明示し、配置方式に応じて接近半径または固定定義を保持する。Socket中心はGameObjectの`Transform Position`そのもので、Cell中心へ固定しない。

## 配置先

- Script: `Assets/Scripts/MapAuthoringKit/Runtime/MapSocketMarker.cs`
- Scene: `MapRoot/Sockets` 配下

## 公開情報

- `SocketKind`: 候補地点の用途。
- `SocketId`: マップ上の配置Instanceを識別する固定ID。使用済み状態や自動生成データのキーに使う。
- `Size`: ローカル座標での占有範囲。
- `Facing`: 正面。向きを限定しない場合は `Any`。
- `SocketTag`: ゲーム側が必要に応じて解釈する任意文字列。
- `EventPlacementMode`: `PoolCandidate`は対戦開始時抽選、`FixedDefinition`は固定Event配置。
- `EventDefinition`: `FixedDefinition`だけが参照するScriptableObject。
- `SerializedEventDefinitionId`: Event定義のID。自動生成や外部データから解決する場合に利用する。
- `InteractionRadiusOverride`: Eventの接近判定半径をSocket単位で保存する値。`FixedDefinition`の0は定義初期値を使うが、`PoolCandidate`は正値必須。
- `EventDefinitionId`: 参照Assetがある場合はAssetのID、参照がない場合は保存済みIDを返す。
- `InteractionRadius`: Socket上書きまたは直接参照した定義から解決した接近判定半径。IDだけを持つ生成データはCatalog解決時に定義の初期値を適用する。
- `TryResolveEventDefinition(...)`: `FixedDefinition`だけを直接参照またはCatalog IDから解決する。Pool候補はfalseを返す。
- `ResolveInteractionRadius(...)`: 個別値、抽選済み定義、固定定義の順で実効半径を返す。
- `SetInteractionRadiusOverride(...)`: Scene View Handleなどから個別半径だけを更新する。
- `WorldBounds`: Transformを反映したWorld AABB。
- `GetWorldCorners()`: 回転とScaleを反映したWorld四隅。
- `Configure(...)`: Editor生成やTestから制作情報をまとめて設定する。既存の4引数呼出しは非Event Socketとして維持できる。

## 設計上の注意

- `Size` は両軸とも0より大きい値にする。
- `SocketId`はScene内で重複させない。
- `PoolCandidate`は固定Event定義を持たず、配置固有の正の接近半径を持つ。
- `FixedDefinition`は定義Assetまたは定義IDを持つ。直接参照時は実効接近半径が0以下にならないようにし、IDだけの生成データはCatalog解決時に定義の初期値を適用する。
- Event以外へEvent定義情報を設定しない。
- EventのTransform位置、接近半径、表示物、MapCollisionは別契約として扱う。接近円はColliderではない。
- Event中心が障害物上にあってもよいが、接近範囲内にPlayerが立てるGroundが必要。
- Socket同士を意図せず重ねない。
- 全範囲を `GroundTilemap` の使用Cell内へ収める。
- 判定は `MapAuthoringValidator` が担当し、Marker自身は値を自動修正しない。

Scene Viewの選択Labelと半径Handleは[[../../Editor/MapAuthoringKit/MapSocketMarkerEditor|MapSocketMarkerEditor]]が担当する。
