# MapEventCatalog

## 役割

`MapEventCatalog` は、Inspectorで編集する`List<MapEventDefinition>`を保存の正本とし、実行時に`Dictionary<string, MapEventDefinition>`へ変換する検索索引。Unity標準シリアライザで扱いやすいListと、ゲーム側がIDから取得しやすいDictionaryを分離する。

- Script: `Assets/Scripts/MapAuthoringKit/Runtime/MapEventCatalog.cs`
- Assetメニュー: `FantasyRoyale/Map Authoring/Event Catalog`
- 初期Asset: `Assets/Data/MapAuthoring/Events/MapEventCatalog.asset`
- 初期登録: `Assets/Data/MapAuthoring/Events/HealingFountainBasic.asset`（派生型`HealingFountainEventDefinition`）

## 公開情報

- `Definitions`: Inspectorで編集・保存する定義List。
- `TryGet(eventDefinitionId, out definition)`: 固定IDからRuntime定義を取得する。
- `Validate(out error)`: null参照、空ID、半径不正、ID重複を検査する。

## 実行時の流れ

1. Catalog AssetはListとしてScene外の設定に保存する。
2. 最初の検索時にListを走査し、IDをキーにしたDictionaryを構築する。
3. EventSocketや自動生成MapはIDを渡して`TryGet`する。
4. Asset再読み込み・Inspector編集時はRuntime索引を破棄し、次回検索時に再構築する。

DictionaryはAssetへ保存せず、Listとの二重正本を作らない。未登録IDは`false`で返し、重複IDはCatalog検証でErrorにする。

保存Listの型は`List<MapEventDefinition>`なので、泉などの派生定義Assetも同じCatalogへ登録できる。検索結果は基底型で返し、実行側が対応する派生型の固有設定を読む。
