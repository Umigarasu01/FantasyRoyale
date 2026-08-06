# Independent GBA Map Creation Tool Design

> [!warning] 廃案
> 2026-07-10の再検討により、専用EditorWindowと独自 `.frmap` は現段階では作らない。
> 現行方針は [[MapAuthoringKit|Map Authoring Kit]] を正とする。

## 位置づけ

GBA風トップダウンマップを作成し、ゲーム非依存の `.frmap` を出力するUnity Editorツールの設計。

状態: **旧ドラフト / 廃案**

旧 `BiomeAtlasMapSceneBuilder`、`ExplorationMapBuilder`、`MapDefinitionAsset` は2026-07-10に削除済み。削除前の履歴はGit snapshot `ce2d1d3` 以前を参照する。

## 設計の中心

ゲームとツールを次の境界で分離する。

```text
Map Authoring Project        Asset Catalog
  地形の意味データ            安定assetIdとPreview素材
  手動配置・Socket              GBA素材契約
  Seed・生成設定
          \                     /
           Map Compiler / Validator
                     |
              Compiled Map Data
                     |
      .frmap + preview.png + validation report
                     |
              FantasyRoyale Consumer
```

- ToolはProducer、FantasyRoyaleはConsumerとする。
- Game Assembly、ゲーム固有MonoBehaviour、Scene構造をToolから参照しない。
- Tool内部の編集形式は将来変更できる。ゲームが把握するのは `Architecture/MapDataFormat.md` の公開形式だけとする。
- Tool側でオートタイル、地面差分、静的装飾、当たり判定を解決してからPublishする。
- Consumerは生成処理を再実装せず、公開データを表示・利用するだけにする。

## Package境界

新ツールはEmbedded UPM Package `com.umiga.gba-map-tool` として分離し、名前空間は `GbaMapTool.*` とする。

```text
Packages/com.umiga.gba-map-tool/
  package.json
  Runtime/
    Format/          # Unity非依存の公開DTO、Serializer、Reader
    Core/            # 編集モデル、接続解決、Compiler、Validator
  Editor/
    Authoring/       # EditorWindowと編集操作
    Assets/          # Asset Catalog、Import検証
    Publishing/      # .frmapとQA成果物の出力
    Preview/         # Tilemap Preview
  Tests/
    EditMode/
  Samples~/
    MinimalConsumer/ # Game非依存の読込・表示サンプル
```

Assemblyも `Format`、`Core`、`Editor`、`Tests` に分ける。`Format` と `Core` は可能な限り純C#とし、Unity型へ依存させない。

## Tool内部の編集データ

Tool内部では公開済みの最終タイルIDではなく、意味データを編集する。

### MapProject

- Project ID、表示名、Map Size、Seed。
- Surface Layer。
- Barrier / Height Layer。
- Detail Density Layer。
- 手動Prop配置。
- Socket配置。
- 使用Asset CatalogとPublish設定。

### Surface Layer

初期Role:

- `Ground`
- `DirtPath`
- `StonePath`
- `ShallowWater`
- `DeepWater`
- `CliffTop`

### Barrier Layer

初期Role:

- `None`
- `ForestWall`
- `CliffFace`
- `HardBlocker`

SurfaceとBarrierを分け、地面の見た目と移動可否を一つの値へ混在させない。

### Detail Density Layer

- `None`
- `Sparse`
- `Normal`
- `Dense`

既存の「歩行可能エリアの約70%へディテール」という方針は、全セルへ一律適用せず、Density Zoneの目標値として扱う。戦闘広場と主通路には別Profileを設定できるようにする。

### Sockets

Player Start、CPU Start、Enemy、Loot、Merchant、Event、Landmarkなどの配置候補地を編集する。ToolはSocketのゲーム処理を知らず、種別、位置、サイズ、向き、Tagだけを出力する。

## Asset Catalog

Tool内のSpriteやPrefab参照と、出力用の安定 `assetId` を対応させる。

- ToolのPreviewはUnity Assetを参照してよい。
- `.frmap` にはUnity GUIDやpathを出力しない。
- Candidate素材をCatalogへ登録できない。
- Approved素材から決定的に生成したProduction素材だけを登録する。
- Catalog ID、Version、Content HashをPublishデータへ記録する。
- Consumer側は別のRegistryで同じ `assetId` をゲーム用SpriteやPrefabへ対応させる。

## GBA素材契約 v1

| 項目 | v1 |
| --- | --- |
| 論理タイル | 32x32 pixel |
| Unity PPU | 32 |
| Filter | Point |
| Compression | None |
| Mipmap | Off |
| Wrap | Clamp |
| 光源 | 左上固定 |
| 静的素材Alpha | 原則 0 または 255 |
| 地形Pivot | 中央 |
| Prop Pivot | 足元中央、Manifestでpixel指定 |
| 回転・任意変形 | 禁止。向きは別Asset ID |

Propは32、64、96 pixel単位のCanvasを基本とし、見た目の大きさと `footprintCells`、足元Anchor、Colliderを別々に定義する。

## 地形接続

- RuleTileを公開データの正本にしない。
- Coreの8方向接続Resolverで隣接Maskを決定する。
- Bitは `N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128` とする。
- 斜め接続は隣接する両直交方向が存在するときだけ有効にする。
- Path、Stone、Liquid、TallGrassは標準Blob-47へ正規化する。
- Groundは複数の全面Tileから座標Hashで差分を選ぶ。
- CliffとForest Wallは高さを持つため、Top、Face、Shadow、Canopy、Trunk、Foregroundを持つ専用Stampとして扱う。
- Unity Tilemap ExtrasはPreview用Brushとして利用できるが、Publish結果はResolverが決める。

## Editor UX

メニュー: `GBA Map Tool/Open Map Editor`

### Project

- 新規作成、開く、複製。
- Map Size、Seed、Asset Catalog、Validation Profileを設定。
- 初期検証は64x48、ストレス確認は96x72に対応する。

### Terrain

- Pencil / Eraser。
- Rectangle / Fill。
- 幅付きPolyline。曲がる道を描き、丸い端でRasterizeする。
- Ellipse。池や広場を作る。
- Cliff / Wall専用Stamp。

### Objects and Sockets

- CatalogからPropを配置。
- Footprint、Anchor、重複をPreview表示。
- Socket種別、Size、Facing、Tagを編集。

### Overlays

- Walkability。
- Collision Flags。
- Reachability。
- Detail Density。
- Socket / Footprint。
- 接続Mask。

### Validate and Publish

- Error / Warning一覧からセルへ移動。
- Publish前に全検証を実行。
- Errorが1件でもあれば出力を変更しない。
- 成功時だけ `.frmap`、Preview、Validation Reportを原子的に置き換える。

## Compilerと決定性

1. MapProjectとAsset Catalogを不変DTOへ変換する。
2. SchemaとAsset参照を検証する。
3. 接続Maskと複層地形Stampを解決する。
4. 座標Hashと用途別Seedから地面差分、静的装飾を選ぶ。
5. 最終Tile Layer、Object、Collision、Socketを作る。
6. 到達可能性と配置重複を検証する。
7. 正規化JSONを生成し、SHA-256を付けてPublishする。

地形、静的装飾、Socket候補は別Seed Streamとする。入力Collectionは安定ID順に並べ、登録順に結果が依存しないようにする。

## Validation

### Publish Error

- 未対応Schema。
- ID重複。
- Layer Size不一致。
- Map外参照。
- 未承認または未知のAsset ID。
- Blob-47や専用Stampの不足。
- Propの重複、Footprint違反。
- Required Socketが移動不可。
- Required StartからRequired Landmarkへ到達不能。
- Content Hash不一致。

### Warning

- 主道がValidation Profileより狭い。
- Detail Densityが目標範囲外。
- 到達可能な歩行セル率が目標未満。
- Optional Socketが孤立している。

検証値はコードへ直書きせず、Version付きValidation Profileへ置く。

## Publish成果物

必須:

- `<map-id>.frmap`
- `<map-id>.validation.json`
- `<map-id>.preview.png`

QA用:

- Collision / Reachability Overlay。
- Density Heatmap。
- Asset Contact Sheet。
- Canonical Map SnapshotとHash。

`.frmap` が唯一のゲーム向け成果物で、その他は検査とレビュー用とする。

## v1 Scope

含む:

- 64x48と96x72。
- 複数Tile Layer。
- 8方向接続と地面差分。
- Ground、Path、Stone、Water、Cliff、Wall。
- Propの手動配置とSeed付き静的装飾。
- Collision Grid。
- 任意文字列型のSocket。
- Undo / Redo。
- Validate / Publish / Reopen。
- Game非依存のMinimal Consumer Sample。

含まない:

- 戦闘、敵AI、アイテム、商人のゲーム処理。
- FantasyRoyale Sceneの生成。
- Runtimeでの地形再生成。
- Tool内部からの画像生成API呼び出し。
- 火山・雪素材のProduction化。Tool自体はCatalog差し替えへ対応する。

## 完成条件

- Game AssemblyやFantasyRoyale固有型への参照がない。
- 64x48森林サンプルと96x72検証サンプルを作成できる。
- 保存、再オープン、Publishが成功する。
- 同一入力を3回Publishして `.frmap` Hashが一致する。
- Schema、Asset、接続、重複、到達可能性の自動テストが通る。
- 不正入力時に既存出力を変更しない。
- Minimal Consumerが公開データだけから同じPreviewを再構築できる。
- Unity上で黒背景、Missing Sprite、明確なTile seamがないことを確認できる。
- UnityMCPまたはUnity batchmodeで実機検証済み。

## 実装順

実装開始時に `.gitignore` の包括的な `*.json` 除外を修正し、Package定義とValidation ReportをGit管理できる状態にする。

1. 公開 `MapData v1` DTO、Serializer、Validator、Round-trip Test。
2. Tool内部のMapProject、Asset Catalog、Compiler。
3. Terrain Resolverと決定性Test。
4. EditorWindowと基本Brush。
5. Object / Socket編集とOverlay。
6. Publish、Preview、QA成果物。
7. Minimal Consumerと森林サンプル。
8. 96x72ストレス確認と完成監査。
