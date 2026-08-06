# MapRoadPathRasterizer

状態: **実装・Unity Test検証済み / 3 Cell幅Sample道路で使用**

## 役割

複数の直交Waypoint列を、表示素材や道路材質を選ぶ前に連続した道路Cell集合へ変換するEditor専用Utility。

- Script: `Assets/Scripts/MapAuthoringKit/Editor/MapRoadPathRasterizer.cs`
- Namespace: `FantasyRoyale.MapAuthoringKit.Editor`

各区間を両端込みで1 CellずつRasterizeし、各中心Cellへ正方形Brushを重ねる。曲がり角でもBrush同士が面で重なるため、道幅を増やしても斜めの穴を残さない。

## 公開API

- `RasterizePath(IEnumerable<Vector3Int> waypoints, int squareBrushRadius = 0)`: 一つのWaypoint列を道路Cellへ変換する。
- `RasterizePaths(IEnumerable<IEnumerable<Vector3Int>> waypointPaths, int squareBrushRadius = 0)`: 複数経路を同じ`HashSet`へ統合し、交差・合流の重複を除く。
- `IsSingleCardinalComponent(IEnumerable<Vector3Int> cells)`: 同じZ平面の4近傍で単一Componentかを判定する。空集合はfalse、1 Cellはtrue。

## 処理順

1. null入力と負のBrush Radiusを拒否する。
2. Waypoint列の先頭Cellを追加する。
3. 隣接Waypoint間が水平または垂直で、同じZ平面かを検査する。
4. 各区間を始点から終点まで1 Cell刻みで追加する。
5. 各中心Cellへ一辺`2 * radius + 1`の正方形Brushを適用する。
6. 全経路を`HashSet`へ統合する。
7. 必要に応じて4近傍BFSで単一Componentかを確認する。
8. 連結確認後にDirt / Stoneなどの材質を割り当てる。

Sampleは`RasterizePaths(..., 1)`を使い、中心線の周囲へ1 Cellずつ広げた3 Cell幅とする。主道、広場分岐、水辺分岐を形状として先に統合し、Dirt / Stoneは共通`road`グループの`RoadConnectionRuleTile`で描画する。

## 例外条件

- XとYが同時に変わる斜め区間。
- Waypoint間でZが変わる区間。
- 負のBrush Radius。
- 複数経路の中にnullのWaypoint列がある場合。

## テスト観点

- 水平・垂直区間が昇順、降順とも両端を含む。
- 同じWaypointの重複で余分なCellを作らない。
- 直角の経路へRadius 1を適用すると角を含む3 Cell幅が4近傍連結する。
- 複数経路の交差と分岐が一つの集合へ統合される。
- 斜め、Z変更、負Radius、nullを例外にする。
- 空、単一Cell、連結、分断した集合のComponent判定が期待通りになる。
- Sampleの入口、広場、水辺Anchorを含む全道路Cellが単一Componentになる。

## 責務外

- A*などによるWaypoint自体の探索。
- Dirt / Stoneの選択とTilemapへのPaint。
- RuleTile Spriteの接続Socket検証。
- Collisionやゲーム中の経路探索。
