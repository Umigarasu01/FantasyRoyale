# RoadConnectionRuleTile

状態: **v4.3実装・Unity Test検証済み / 3 Cell幅Sample道路で使用**

## 役割

DirtとStoneのように表示材質が異なる道路RuleTileを、共通の道路接続グループで一続きとして判定するRuntime Asset。

- Script: `Assets/Scripts/MapAuthoringKit/Runtime/RoadConnectionRuleTile.cs`
- Namespace: `FantasyRoyale.MapAuthoringKit`
- Base: `RuleTile`

標準RuleTileの`Neighbor.This`は同じAssetだけへ接続するため、DirtとStoneの境界では双方の縁が閉じる。本型は両Assetを共通の`road`グループへ設定し、材質境界でも同じCardinal接続Maskを選べるようにする。

## 公開API

- `ConnectionGroup`: 現在の接続グループ名。空の場合は同じAsset自身だけへ接続する。
- `Configure(string group, int seed = 0)`: 前後の空白を除いて接続グループを設定し、表示差分用Seedを保存する。SampleのDirt / Stoneは共に`road`とSeed 0を指定する。
- `GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)`: 基底`RuleTile`で接続Maskを決めた後、複数Spriteを持つRuleだけ表示差分を座標Hashで選び直す。
- `GetVariantIndex(Vector3Int position, int variantCount, int ruleId)`: Cell座標、Seed、Rule IDを使い、指定数未満の決定的な差分Indexを返す。4差分の場合は専用の4 Cell区間方式を使う。
- `CanConnectTo(TileBase other)`: 同じAssetまたは同じ接続グループの`RoadConnectionRuleTile`かを判定する。`RuleOverrideTile`は実体または元Tileへ解決する。
- `RuleMatch(int neighbor, TileBase other)`: `Neighbor.This`を`CanConnectTo`、`Neighbor.NotThis`をその否定で評価し、それ以外の拡張条件は基底実装へ渡す。

## 処理順

1. BuilderがDirt / Stone Assetを生成し、双方へ`Configure("road")`を適用する。
2. Tilemap更新時にRuleTileから`RuleMatch`が呼ばれる。
3. Overrideを元の道路Tileへ解決する。
4. 同一Assetを優先して接続扱いにする。
5. 別Assetなら、空でない接続グループが完全一致した場合だけ接続する。
6. 道路の4近傍に対応したRule ID、すなわちCardinal Maskを選ぶ。
7. Dirt / Stoneの全16 Maskはそれぞれ4 Spriteを持つため、`GetTileData`が座標、Rule ID、Seedから表示差分を選び直す。

4差分では横4 Cellを一つの区間とし、24通りある全順列から区間ごとの並びをHashで選ぶ。各区間内で4差分を一度ずつ使い、区間境界を含む同一差分の横連続を最大2 Cellに抑える。負のX座標では剰余を補正して床除算と同じ区切りにする。Y座標、Rule ID、Seedは順列選択へ混合するが、Z座標は2D Mapの表示差分へ影響させない。

Rule Asset上は全Maskを4 Spriteの`Random / Fixed`として保持するが、実際のSprite選択に標準RuleTileのPerlin値は使わない。接続Maskの選択は基底実装に任せ、輪郭・質感差分だけをこの決定的Hashで置き換える。

v4.3の道路Socketを中央`[6, 26)`の20pxと条件付き角へ変更しても、この差分選択手順は変わらない。4差分は同じMask内で同一のSocket topologyを持ち、本型はその中から表示だけを選ぶ。実PixelのSocket保証はAtlas buildと`MapAuthoringAssetContractTests`が担当する。

Sampleでは`MapRoadPathRasterizer`がRadius 1の正方形Brushで作った3 Cell幅の道路へDirt / Stoneを割り当てる。形状を先に確定し、材質を後から選ぶことで、広場入口のDirt / Stone境界を切断しない。

## テスト観点

- 同じAssetはグループ未設定でも接続する。
- DirtとStoneへ同じ`road`を設定すると相互接続する。
- 異なるグループ、空グループ、通常Tile、nullへは接続しない。
- `Neighbor.NotThis`が`Neighbor.This`と対称になる。
- `RuleOverrideTile`経由でも元の接続グループを維持する。
- Dirt / Stone境界で、双方が相手方向のCardinal bitを持つSpriteを選ぶ。
- 全16 Maskについて4 Cell区間内で4差分を各1回使い、区間境界を含む同一差分の横連続が2 Cell以下になる。
- 同じ座標、Rule ID、Seedは常に同じ差分を返し、Z座標だけを変えても結果が変わらない。
- 負のX座標でも4 Cell区間の境界が正しく扱われる。

## 責務外

- 道路形状、道幅、Waypoint、材質配置の決定。
- Sprite内の接続SocketやPixel品質の保証。
- Collisionや移動判定の生成。
