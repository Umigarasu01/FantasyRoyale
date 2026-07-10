# Current Work

## 2026-06-05 追記: 旧マップ生成物の削除

参考画像の方向性に合わない旧マップ生成物とEditor上のビルド窓口を削除した。

削除対象:
- `Assets/Editor/Milestone1SceneBuilder.cs`
- `Assets/Editor/Milestone1OrganicSceneBuilder.cs`
- `Assets/Art/Generated/Milestone1/`
- `Assets/Art/Generated/Milestone1Organic/`
- `Assets/Data/Milestone1/`
- `Assets/Data/Milestone1Organic/`
- `Assets/Prefabs/Milestone1/`
- `Assets/Prefabs/Milestone1Organic/`
- `Assets/Scenes/Milestone1ExplorationScene.unity`
- `Assets/Scenes/Milestone1OrganicMapScene.unity`

次は `$imagegen` で森バイオームのトップダウン2Dピクセルアート風サンプルマップ画像を1枚だけ生成し、見た目の方向性を確認する。

## 2026-06-05 追記: 高密度ピクセルアート方針への再整理

前回生成されたマップがフラットな図形素材に見え、参考画像の方向性と大きく異なっていたため、実装前のアート設計を再整理した。

今回更新した成果物:
- `Art/ArtDirection.md`
- `Art/TilesetPlan.md`
- `Art/ObjectPlan.md`
- `Art/MapPlan.md`

新しい方針:
- 単色地面、円/四角の記号的オブジェクト、巨大な長方形道、四角いエリア分けは禁止。
- 草地、土道、石畳、水、森壁、崖には境界タイルを必須にする。
- 森・草原、火山、雪は同じマップ構造を保ち、素材役割を差し替える。
- まずはテキスト設計を更新し、その後に仮素材とサンプルマップを作り直す。

## 2026-06-05 追記: 有機的サンプルマップ生成器

高密度ピクセルアート方針を実装へ移すため、`Milestone1OrganicSceneBuilder` を追加。

今回の実装内容:
- `FantasyRoyale/Build Milestone 1 Organic Pixel Map` メニューを追加。
- `Assets/Art/Generated/Milestone1Organic/` に森・草原用タイルと共通オブジェクトを生成する構成を追加。
- 既存の矩形MapDefinitionではなく、曲線状の道、池、森壁、崖、草むらをマスクで描く生成方式を追加。
- 96x72 の有機的な森・草原サンプルマップ `Assets/Scenes/Milestone1OrganicMapScene.unity` を生成対象にした。
- 初回MCP生成でTilemapが空になる問題を確認し、Tile/Prefab保存後にロードし直してから描画するよう修正。

確認状況:
- `dotnet build Assembly-CSharp.csproj --no-restore` 成功。
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore` 成功。
- 初回MCP実行は成功したが、Tilemapが空だったため生成順を修正。
- 修正後は `unity-mcp-fantasyroyale` が `Unity not detected`、Unity batchmodeはEditor起動中のため未実行。

次の確認:
- Unity Editorで `FantasyRoyale/Build Milestone 1 Organic Pixel Map` を実行し、Tilemapの地面、道、水辺、森壁、当たり判定を確認する。

## 2026-06-05 追記: 森・草原マップ再設計

採用したアート方針に合わせて、Milestone 1 の生成器を森・草原ベースの探索マップへ作り直す方針で更新。

今回の実装内容:
- `Milestone1SceneBuilder` の生成対象を、雪/火山混在マップから森・草原のベースマップへ変更。
- 草地、草むら、土道、石畳、水、崖などのタイルを、矩形ベタ塗りではなく簡易ピクセルアート風の生成素材へ変更。
- 木、低木、岩、商人屋台、キノコ、切り株、倒木、花、看板、宝箱、祭壇、葦などの仮オブジェクトを追加。
- 外周、池、崖、大きめの樹木/低木/岩には当たり判定を残し、草むらや花などは通行可能な装飾として扱う。
- 中央広場、南の主道、西の草むら通路、北側の回遊路、北東の池を持つ 96x72 の探索確認用マップに変更。

確認状況:
- `dotnet build Assembly-CSharp.csproj --no-restore` 成功。
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore` 成功。
- 既存警告として `System.Net.Http` のバージョン競合あり。
- Unity batchmode は同じプロジェクトがEditorで開かれていたため実行できず。
- `unity-mcp-fantasyroyale` は `Unity not detected` で接続できず。

次の確認:
- Unity Editor上で `FantasyRoyale/Build Milestone 1 Exploration Scene` を実行し、`Milestone1ExplorationScene` の見た目と当たり判定を確認する。

## 2026-05-31

Milestone 1「ビジュアル・探索マップ基盤」を実装中。

今回の範囲:
- 固定マップを `MapDefinitionAsset` で管理する。
- Core側は `FR_` 接頭辞の純C#型で、スロット抽選と配置結果を扱う。
- マップはTilemapで構成し、草原、雪原、火山、湖、遺跡、道を持つ探索用サイズにする。
- オブジェクト用スロットはサイズ分類とグリッドサイズを持ち、大スロットに小物を複数配置できる。
- 仮素材は `Assets/Art/Generated/Milestone1/` に隔離する。
- 低木や水・溶岩・崖は通行不可、草むらは通行可能。

次の確認:
- Unity Editorで `Assets/Scenes/Milestone1ExplorationScene.unity` を開き、探索感、マップ密度、当たり判定の感触を確認する。

## 2026-06-05

参考画像のコピーではなく、雰囲気、構図、密度、色の方向性をもとにした素材設計を先に整理する。

今回作成するテキスト成果物:
- `Art/ArtDirection.md`
- `Art/TilesetPlan.md`
- `Art/ObjectPlan.md`
- `Art/MapPlan.md`

この段階では画像生成やUnity実装は行わない。
## 2026-06-05 追記: 森マップ構造ベースの火山/雪バイオーム展開

`Assets/Art/Concept/forest-biome-sample-map.png` を基準画像として、同じ構図・通路・広場・障害物配置を保った火山バイオーム版と雪バイオーム版を `$imagegen` で生成する方針。

生成画像を Unity Tilemap で再構成するため、専用ノート `Art/BiomeTilemapAtlasPlan.md` を追加した。

今回の分解方針:
- 森・火山・雪で同じタイルID構造を使い、Sprite差し替えでバイオーム化する。
- 中央石畳、左の曲がるサブ通路、右の水辺/溶岩/凍結池、崖、森壁相当、複数の小広場を共通骨格にする。
- 地形タイルは `Ground`、`Path`、`Stone`、`Liquid`、`Cliff`、`Wall` に分ける。
- 木、低木、岩、キノコ、切り株、倒木、看板、宝箱、祭壇は透明PNGのオブジェクトアトラスとして扱う。
- `$imagegen` は現在レート制限により火山/雪の生成待ち。
## 2026-06-05 追記: バイオーム仮アトラス作成

`$imagegen` で森・火山・雪を同一シートにまとめた仮アトラスを生成し、`Assets/Art/Generated/BiomeAtlas/biome-tile-object-atlas.png` に保存した。

扱い:
- 左側は Tilemap 用地形タイル候補。
- 右側は SpriteRenderer/Prefab 用オブジェクト候補。
- 画像サイズは 1254x1254 で、完全な32x32自動グリッドスライス用ではない。
- 次段では、この仮アトラスから必要タイルとオブジェクトを切り出し、Unity用の正規グリッド版に再整形する。
## 2026-06-05 追記: バイオームアトラス探索マップ生成導線

`Assets/Editor/BiomeAtlasMapSceneBuilder.cs` を追加し、仮アトラスから探索用Tilemap Sceneを生成する導線を作成した。

生成予定:
- `Assets/Art/Generated/BiomeAtlas/Runtime/Tiles/`
- `Assets/Art/Generated/BiomeAtlas/Runtime/Objects/`
- `Assets/Data/BiomeAtlas/Tiles/`
- `Assets/Prefabs/BiomeAtlas/`
- `Assets/Scenes/BiomeAtlasExplorationMapScene.unity`

現在の状態:
- C#ビルドは成功。
- Unity batchmodeは、同じプロジェクトをUnity Editorが開いているため実行不可。
- `unity-mcp-fantasyroyale` は `Unity not detected` で実行不可。
- Editor接続が復旧したら `FantasyRoyale/Build Biome Atlas Exploration Map` を実行してScene生成する。
