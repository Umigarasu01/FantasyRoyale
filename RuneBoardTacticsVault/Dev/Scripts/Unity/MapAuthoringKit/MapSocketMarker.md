# MapSocketMarker

## 役割

`MapSocketMarker` は、Map Scene上にPlayer Start、Enemy、Loot、Merchant、汎用Eventなどの候補地点を置くための制作マーカー。

Spawn、抽選、ゲーム進行は実装しない。種別、配置固有ID、占有範囲、向き、任意Tagを保持する。Eventの場合だけ、定義Assetまたは定義IDと接近半径上書きを保持する。Socket中心はGameObjectの`Transform Position`そのもので、Cell中心へ固定しない。

## 配置先

- Script: `Assets/Scripts/MapAuthoringKit/Runtime/MapSocketMarker.cs`
- Scene: `MapRoot/Sockets` 配下

## 公開情報

- `SocketKind`: 候補地点の用途。
- `SocketId`: マップ上の配置Instanceを識別する固定ID。使用済み状態や自動生成データのキーに使う。
- `Size`: ローカル座標での占有範囲。
- `Facing`: 正面。向きを限定しない場合は `Any`。
- `SocketTag`: ゲーム側が必要に応じて解釈する任意文字列。
- `EventDefinition`: Eventだけが参照するScriptableObject。手動配置で利用する。
- `SerializedEventDefinitionId`: Event定義のID。自動生成や外部データから解決する場合に利用する。
- `InteractionRadiusOverride`: Eventの接近判定半径をSocket単位で上書きする値。0は定義の初期値を使う。
- `EventDefinitionId`: 参照Assetがある場合はAssetのID、参照がない場合は保存済みIDを返す。
- `InteractionRadius`: Socket上書きまたは直接参照した定義から解決した接近判定半径。IDだけを持つ生成データはCatalog解決時に定義の初期値を適用する。
- `TryResolveEventDefinition(...)`: 直接参照を優先し、なければCatalogからID解決する。
- `ResolveInteractionRadius(...)`: 個別上書きまたはCatalog解決済み定義から実効半径を返す。
- `SetInteractionRadiusOverride(...)`: Scene View Handleなどから個別半径だけを更新する。
- `WorldBounds`: Transformを反映したWorld AABB。
- `GetWorldCorners()`: 回転とScaleを反映したWorld四隅。
- `Configure(...)`: Editor生成やTestから制作情報をまとめて設定する。既存の4引数呼出しは非Event Socketとして維持できる。

## 設計上の注意

- `Size` は両軸とも0より大きい値にする。
- `SocketId`はScene内で重複させない。
- Eventは定義Assetまたは定義IDを持つ。直接参照時は実効接近半径が0以下にならないようにし、IDだけの生成データはCatalog解決時に定義の初期値を適用する。
- Event以外へEvent定義情報を設定しない。
- EventのTransform位置、接近半径、表示物、MapCollisionは別契約として扱う。接近円はColliderではない。
- Event中心が障害物上にあってもよいが、接近範囲内にPlayerが立てるGroundが必要。
- Socket同士を意図せず重ねない。
- 全範囲を `GroundTilemap` の使用Cell内へ収める。
- 判定は `MapAuthoringValidator` が担当し、Marker自身は値を自動修正しない。

Scene Viewの選択Labelと半径Handleは[[../../Editor/MapAuthoringKit/MapSocketMarkerEditor|MapSocketMarkerEditor]]が担当する。
